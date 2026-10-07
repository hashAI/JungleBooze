using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Pure-C# checks of a built route against spec 003 sections 4.3 (limits), 6.1 (next 40 m on screen), 9 and
    /// AC-304 / AC-305 / AC-306 / AC-312. Usable from tests and from the editor tool. Allocates (it is a tool).
    /// Vine and gateway zones are read from the beat tags of the samples, so the chunk list is not needed afterwards;
    /// the world of every sample is given by the caller (see <see cref="Build"/>).
    /// </summary>
    public sealed class RouteValidator
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double Eps = 1e-6;
        private const double ClearingTolerance = 10.0;
        private const double HeadingTolerance = 1.0 * DegToRad;
        private const double WindowTolerance = 0.5 * DegToRad;
        private const double GateLevelPct = 0.5;

        private readonly RouteTuning _tuning;

        public RouteValidator(RouteTuning tuning)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Probe = new ViewportProbe();
        }

        /// <summary>The camera model of the on-screen rule; change its fields to test other camera numbers.</summary>
        public ViewportProbe Probe { get; }

        /// <summary>Check the next-40 m rule (AC-305). On by default.</summary>
        public bool CheckVisibility { get; set; } = true;

        /// <summary>Check that the route never comes back to itself (AC-306). On by default.</summary>
        public bool CheckSelfIntersection { get; set; } = true;

        /// <summary>Samples between probed hero positions for the visibility rule.</summary>
        public int VisibilityStride { get; set; } = 5;

        /// <summary>Closest allowed approach of two parts of the route more than <see cref="SelfWindowM"/> apart in s (AC-306).</summary>
        public double SelfMinDistanceM { get; set; } = 60.0;

        /// <summary>The s-window inside which neighbouring samples are allowed to be close (AC-306).</summary>
        public double SelfWindowM { get; set; } = 120.0;

        /// <summary>How far ahead (in s) the self-intersection search looks.</summary>
        public double SelfSearchM { get; set; } = 700.0;

        /// <summary>
        /// Builds a route with <see cref="RouteGenerator"/> from the run seed (Route stream) between
        /// <paramref name="startS"/> and <paramref name="endS"/> and fills <paramref name="worlds"/> with the world
        /// reported for every sample. The caller must make sure the chunks are committed far enough:
        /// <c>endS &lt;= CommittedEndS - ReadAheadM</c> for the determinism rule.
        /// </summary>
        public static RouteSample[] Build(RouteTuning tuning, ulong runSeed, double startS, double endS, IRouteChunkSource chunks, out WorldKind[] worlds)
        {
            var generator = new RouteGenerator(tuning, chunks);
            generator.Begin(new Pcg32Random(runSeed).Fork(RandomStreamIds.Route), startS);
            double ds = Math.Max(0.25, tuning.SampleSpacingM);
            int count = (int)Math.Ceiling((endS - startS) / ds) + 1;
            var samples = new RouteSample[count];
            worlds = new WorldKind[count];
            for (int i = 0; i < count; i++)
            {
                double s = startS + (i * ds);
                generator.NextSample(s, out samples[i]);
                worlds[i] = chunks != null ? chunks.WorldKindAt(s) : WorldKind.Jungle;
            }

            return samples;
        }

        /// <summary>
        /// Checks <paramref name="count"/> samples, sample <c>i</c> at <c>startS + i * spacing</c>.
        /// <paramref name="worlds"/> gives the world per sample (null = all Jungle).
        /// </summary>
        public RouteReport Validate(RouteSample[] samples, WorldKind[] worlds, int count, double startS)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var report = new RouteReport();
            count = Math.Min(count, samples.Length);
            report.SamplesChecked = count;
            if (count < 2)
            {
                return report;
            }

            double ds = Math.Max(0.25, _tuning.SampleSpacingM);
            int window = Math.Max(2, (int)Math.Round(_tuning.TurnWindowM / ds));
            double yawRateKappa = _tuning.MaxYawRateDegS * DegToRad / Math.Max(1e-3, _tuning.MaxBoostSpeedMps);
            double fullBankKappa = 1.0 / Math.Max(1.0, _tuning.FullBankRadiusM);
            double zoneKappa = 1.0 / Math.Max(1.0, _tuning.ZoneRadiusM);
            double lowPass = 0.0;

            for (int i = 0; i < count; i++)
            {
                ref RouteSample sample = ref samples[i];
                double s = startS + (i * ds);
                WorldKind worldKind = worlds != null && i < worlds.Length ? worlds[i] : WorldKind.Jungle;
                RouteWorldTuning world = _tuning.GetWorld(worldKind);
                bool calm = s < _tuning.CalmStartM;
                double kappa = sample.Curvature;
                double rMin = calm ? Math.Max(world.RMinM, _tuning.CalmRMinM) : world.RMinM;
                double gradeCap = calm ? Math.Min(world.GradeMaxPct, _tuning.CalmGradeMaxPct) : world.GradeMaxPct;

                if (sample.Emergency)
                {
                    report.EmergencySamples++;
                }

                if (Math.Abs(kappa) > (1.0 / rMin) + Eps)
                {
                    report.RadiusViolations++;
                    report.Note("radius: s=" + s + " kappa=" + kappa + " limit 1/" + rMin);
                }

                if (Math.Abs(kappa) > yawRateKappa + Eps)
                {
                    report.YawRateViolations++;
                    report.Note("yaw rate: s=" + s + " kappa=" + kappa);
                }

                double gradePct = Math.Tan(sample.PitchRad) * 100.0;
                if (Math.Abs(gradePct) > gradeCap + 0.05)
                {
                    report.GradeViolations++;
                    report.Note("grade: s=" + s + " grade=" + gradePct + "% limit " + gradeCap);
                }

                double bankMaxRad = world.BankMaxDeg * DegToRad;
                double expectedBank = Math.Max(-bankMaxRad, Math.Min(bankMaxRad, bankMaxRad * kappa / fullBankKappa));
                if (Math.Abs(sample.BankRad) > bankMaxRad + 1e-4 || Math.Abs(sample.BankRad - expectedBank) > 1e-4)
                {
                    report.BankViolations++;
                    report.Note("bank: s=" + s + " bank=" + (sample.BankRad / DegToRad) + " deg");
                }

                if (i > 0)
                {
                    ref RouteSample previous = ref samples[i - 1];
                    double jerkLimit = sample.Emergency ? _tuning.EmergencyKappaJerk : _tuning.KappaJerk;
                    double jerk = Math.Abs(kappa - previous.Curvature) / ds;
                    if (jerk > jerkLimit + Eps)
                    {
                        report.JerkViolations++;
                        report.Note("jerk: s=" + s + " dk/ds=" + jerk);
                    }

                    double dPitch = sample.PitchRad - previous.PitchRad;
                    if (dPitch < (-ds / _tuning.CrestRadiusMinM) - Eps || dPitch > (ds / _tuning.SagRadiusMinM) + Eps)
                    {
                        report.VerticalCurveViolations++;
                        report.Note("vertical curve: s=" + s + " dPitch/ds=" + (dPitch / ds));
                    }
                }

                if (i >= window)
                {
                    double turn = Math.Abs(sample.YawRad - samples[i - window].YawRad);
                    if (turn > (_tuning.TurnWindowMaxDeg * DegToRad) + WindowTolerance)
                    {
                        report.TurnWindowViolations++;
                        report.Note("turn window: s=" + s + " turn=" + (turn / DegToRad) + " deg");
                    }
                }

                lowPass += (sample.YawRad - lowPass) * ds / 300.0;
                if (Math.Abs(lowPass) > (_tuning.RestoringDeg * DegToRad) + HeadingTolerance)
                {
                    report.RestoringViolations++;
                    report.Note("heading restoring: s=" + s + " low-passed heading=" + (lowPass / DegToRad) + " deg");
                }
            }

            CheckZones(samples, count, startS, ds, zoneKappa, report);
            CheckBeats(samples, count, startS, ds, report);

            if (CheckSelfIntersection)
            {
                CheckSelf(samples, count, startS, ds, report);
            }

            if (CheckVisibility)
            {
                report.VisibilityViolations = Probe.Probe(samples, count, startS, ds, 0, count - 1, Math.Max(1, VisibilityStride), out double worst);
                report.WorstVisibilityRatio = worst;
                if (report.VisibilityViolations > 0)
                {
                    report.Note("visibility: " + report.VisibilityViolations + " points outside the margin, worst ratio " + worst);
                }
            }

            return report;
        }

        /// <summary>Straight, near-level ground from <see cref="RouteTuning.ZoneNoticeM"/> before each vine or gateway zone to its end.</summary>
        private void CheckZones(RouteSample[] samples, int count, double startS, double ds, double zoneKappa, RouteReport report)
        {
            int noticeSamples = (int)Math.Ceiling(_tuning.ZoneNoticeM / ds);
            int i = 0;
            while (i < count)
            {
                RouteBeatKind kind = samples[i].Beat;
                if (kind != RouteBeatKind.SwingZone && kind != RouteBeatKind.Gateway)
                {
                    i++;
                    continue;
                }

                int begin = i;
                while (i < count && samples[i].Beat == kind)
                {
                    i++;
                }

                report.Zones++;
                int from = Math.Max(0, begin - noticeSamples);
                for (int j = from; j < i; j++)
                {
                    double gradePct = Math.Abs(Math.Tan(samples[j].PitchRad) * 100.0);
                    double limit = (j >= begin && kind == RouteBeatKind.Gateway) ? GateLevelPct : _tuning.ZoneGradeMaxPct;
                    if (Math.Abs(samples[j].Curvature) > zoneKappa + Eps || gradePct > limit + 0.05)
                    {
                        report.ZoneViolations++;
                        report.Note("zone: s=" + (startS + (j * ds)) + " kappa=" + samples[j].Curvature + " grade=" + gradePct + "% before " + kind + " at s=" + (startS + (begin * ds)));
                        break;
                    }
                }
            }
        }

        /// <summary>No beat kind more than twice in a row (zones separate runs) and a breath within every clearing distance.</summary>
        private void CheckBeats(RouteSample[] samples, int count, double startS, double ds, RouteReport report)
        {
            int lastBeatId = -1;
            RouteBeatKind lastKind = RouteBeatKind.Straight;
            int run = 0;
            double lastBreathS = startS;
            double limit = _tuning.ClearingEveryM + ClearingTolerance;

            for (int i = 1; i < count; i++)
            {
                RouteBeatKind kind = samples[i].Beat;
                double s = startS + (i * ds);
                if (kind == RouteBeatKind.SwingZone || kind == RouteBeatKind.Gateway)
                {
                    lastBreathS = s;
                    lastBeatId = -1;
                    run = 0;
                    continue;
                }

                if (kind == RouteBeatKind.Clearing)
                {
                    lastBreathS = s;
                }
                else if (s - lastBreathS > limit)
                {
                    report.ClearingViolations++;
                    report.Note("clearing: no breath for " + (s - lastBreathS) + " m before s=" + s);
                    lastBreathS = s;
                }

                if (samples[i].BeatId == lastBeatId)
                {
                    continue;
                }

                lastBeatId = samples[i].BeatId;
                if (run > 0 && kind == lastKind)
                {
                    run++;
                }
                else
                {
                    run = 1;
                    lastKind = kind;
                }

                if (run > 2)
                {
                    report.RepeatViolations++;
                    report.Note("repeat: " + kind + " " + run + " times in a row at s=" + s);
                }
            }
        }

        /// <summary>AC-306: no two samples further apart than the s-window come closer than the minimum distance.</summary>
        private void CheckSelf(RouteSample[] samples, int count, double startS, double ds, RouteReport report)
        {
            int minGap = (int)Math.Ceiling(SelfWindowM / ds) + 1;
            int maxGap = (int)Math.Ceiling(SelfSearchM / ds);
            double minDistanceSq = SelfMinDistanceM * SelfMinDistanceM;
            int stride = Math.Max(1, (int)Math.Round(2.0 / ds));
            for (int i = 0; i < count; i += stride)
            {
                int jEnd = Math.Min(count, i + maxGap);
                for (int j = i + minGap; j < jEnd; j += stride)
                {
                    double dx = samples[i].X - samples[j].X;
                    double dy = samples[i].Y - samples[j].Y;
                    double dz = samples[i].Z - samples[j].Z;
                    if ((dx * dx) + (dy * dy) + (dz * dz) < minDistanceSq)
                    {
                        report.SelfIntersections++;
                        report.Note("self intersection: s=" + (startS + (i * ds)) + " and s=" + (startS + (j * ds)));
                        break;
                    }
                }
            }
        }
    }
}
