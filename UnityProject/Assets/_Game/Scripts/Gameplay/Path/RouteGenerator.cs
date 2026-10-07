using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// The seeded route generator (spec 003 section 4): a sequence of beats (bends, S-bends, rolls, rises and falls,
    /// clearings, crossings, bridges) steered by a jerk-limited curvature follower and a rate-limited pitch follower,
    /// so the limits of section 4.3 hold by construction. Vine and gateway chunks of the track force straight, level
    /// zones; the route eases into them early enough (<see cref="RouteTuning.ReadAheadM"/>) or, with too little
    /// notice, applies the emergency ease (<see cref="EmergencyEaseCount"/>).
    /// <para>Deterministic: every decision is a function of the Route random stream and of the chunk kinds within
    /// <see cref="RouteTuning.ReadAheadM"/> of the sample being built (which the caller keeps committed by not building
    /// beyond <c>CommittedEndS - ReadAheadM</c>), never of frame rate or of when samples are requested.
    /// Every beat pick makes the same six draws, so the stream position depends on the beat count only.</para>
    /// <para>Presentation only; no allocation after construction. Layers (Ascent, Descent, High) are T5: those beats
    /// are in the table with weight 0 and, if given a weight, are plain grade pulses on the Floor layer.</para>
    /// </summary>
    public sealed class RouteGenerator : IRouteSource
    {
        private const double DegToRad = Math.PI / 180.0;
        private const int DrawnKinds = 11;
        private const double LowPassM = 300.0;
        private const double TurnGuardFactor = 0.8;
        private const double RestoreFlipFactor = 0.9;
        private const double MaxBendTurnRad = 0.7;
        private const double Epsilon = 1e-9;

        /// <summary>[ASSUMED] Percent of baseline grade per m/km of net elevation drift (tuned against the fuzz: River about -27 m, Mountains about +55 m per world).</summary>
        private const double BaselineGradePerDrift = 0.14;

        private readonly RouteTuning _tuning;
        private readonly IRouteChunkSource _chunks;
        private readonly double _ds;
        private readonly double _kJerk;
        private readonly double _kJerkEmergency;
        private readonly double _crestR;
        private readonly double _sagR;
        private readonly double _fullBankKappa;
        private readonly double _restoreRad;
        private readonly double _turnLimitRad;
        private readonly double _zoneKappa;
        private readonly int _window;
        private readonly double[] _yawHistory;

        private IRandom _rng;
        private bool _first;
        private double _x;
        private double _y;
        private double _z;
        private double _psi;
        private double _theta;
        private double _kappa;
        private double _psiLowPass;
        private double _centerY;
        private int _historyCount;
        private int _historyHead;

        private WorldKind _world;
        private RouteBeatKind _beatKind;
        private PathSurface _beatSurface;
        private double _beatStart;
        private double _beatEnd;
        private double _beatLength;
        private double _beatKappa;
        private double _beatGradePct;
        private double _beatAmplitudePct;
        private int _beatId;
        private int _lastKind;
        private int _run;
        private double _lastClearingEndS;
        private bool _glade;
        private bool _wasInZone;
        private double _calmUntilS;

        public RouteGenerator(RouteTuning tuning, IRouteChunkSource chunks)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _chunks = chunks;
            _ds = Math.Max(0.25, tuning.SampleSpacingM);
            _kJerk = Math.Max(1e-6, tuning.KappaJerk);
            _kJerkEmergency = Math.Max(_kJerk, tuning.EmergencyKappaJerk);
            _crestR = Math.Max(1.0, tuning.CrestRadiusMinM);
            _sagR = Math.Max(1.0, tuning.SagRadiusMinM);
            _fullBankKappa = 1.0 / Math.Max(1.0, tuning.FullBankRadiusM);
            _restoreRad = tuning.RestoringDeg * DegToRad;
            _turnLimitRad = tuning.TurnWindowMaxDeg * DegToRad;
            _zoneKappa = 1.0 / Math.Max(1.0, tuning.ZoneRadiusM);
            _window = Math.Max(2, (int)Math.Round(tuning.TurnWindowM / _ds));
            _yawHistory = new double[_window + 1];
            _calmUntilS = tuning.CalmStartM;
        }

        /// <summary>The tuning this generator reads.</summary>
        public RouteTuning Tuning => _tuning;

        /// <summary>Samples shaped by the emergency ease so far this run (AC-312: 0 in normal runs).</summary>
        public int EmergencyEaseCount { get; private set; }

        /// <summary>Arc length of the last sample shaped by the emergency ease (negative infinity if none).</summary>
        public double LastEmergencyS { get; private set; }

        /// <summary>Low-passed heading in radians (300 m), the quantity the heading-restoring limit is about.</summary>
        public double HeadingLowPassRad => _psiLowPass;

        /// <summary>Beats started so far this run (zones are not counted).</summary>
        public int BeatCount => _beatId;

        /// <summary>
        /// Until this arc length the route is calm (R at least <see cref="RouteTuning.CalmRMinM"/>, grade at most
        /// <see cref="RouteTuning.CalmGradeMaxPct"/>). Defaults to <see cref="RouteTuning.CalmStartM"/>; the tutorial may
        /// raise it. Read when a beat is picked, so set it before <see cref="Begin"/> or early in the run.
        /// </summary>
        public double CalmUntilS
        {
            get => _calmUntilS;
            set => _calmUntilS = value;
        }

        public void Begin(IRandom routeRandom, double startS)
        {
            _rng = routeRandom ?? throw new ArgumentNullException(nameof(routeRandom));
            _first = true;
            _x = 0.0;
            _y = 0.0;
            _z = startS;
            _psi = 0.0;
            _theta = 0.0;
            _kappa = 0.0;
            _psiLowPass = 0.0;
            _centerY = 0.0;
            _historyCount = 0;
            _historyHead = 0;
            _world = WorldKind.Jungle;
            _beatKind = RouteBeatKind.Straight;
            _beatSurface = PathSurface.Trail;
            _beatStart = startS;
            _beatEnd = double.NegativeInfinity;
            _beatLength = 0.0;
            _beatKappa = 0.0;
            _beatGradePct = 0.0;
            _beatAmplitudePct = 0.0;
            _beatId = 0;
            _lastKind = -1;
            _run = 0;
            _lastClearingEndS = startS;
            _glade = false;
            _wasInZone = false;
            EmergencyEaseCount = 0;
            LastEmergencyS = double.NegativeInfinity;
        }

        public void NextSample(double s, out RouteSample sample)
        {
            if (_rng == null)
            {
                throw new InvalidOperationException("Call Begin before NextSample.");
            }

            bool emergency = false;
            RouteBeatKind tag = RouteBeatKind.Straight;
            PathSurface surface = PathSurface.Trail;
            if (_first)
            {
                _first = false;
                _z = s;
                PushYaw(0.0);
                _world = WorldAt(s);
            }
            else
            {
                Advance(s, out tag, out surface, out emergency);
            }

            double bankMaxDeg = _tuning.GetWorld(_world).BankMaxDeg;
            double bankDeg = bankMaxDeg * _kappa / _fullBankKappa;
            if (bankDeg > bankMaxDeg)
            {
                bankDeg = bankMaxDeg;
            }
            else if (bankDeg < -bankMaxDeg)
            {
                bankDeg = -bankMaxDeg;
            }

            sample = default(RouteSample);
            sample.X = _x;
            sample.Y = _y;
            sample.Z = _z;
            sample.YawRad = (float)_psi;
            sample.PitchRad = (float)_theta;
            sample.BankRad = (float)(bankDeg * DegToRad);
            sample.Curvature = (float)_kappa;
            sample.HalfWidthM = StraightRouteSource.HalfWidthM;
            sample.Layer = PathLayer.Floor;
            sample.Surface = surface;
            sample.Beat = tag;
            sample.BeatId = _beatId;
            sample.Emergency = emergency;
        }

        private void Advance(double s, out RouteBeatKind tag, out PathSurface surface, out bool emergency)
        {
            emergency = false;
            bool calm = s < _calmUntilS;

            RouteZone zone = default(RouteZone);
            bool hasZone = _chunks != null && _chunks.TryFindZone(s, _tuning.ReadAheadM, out zone);

            bool inZone = hasZone && s >= zone.StartS;
            double kappaTarget = 0.0;
            double gradeTarget = 0.0;

            if (inZone)
            {
                _wasInZone = true;
                _lastClearingEndS = zone.EndS;
                if (zone.Kind == RouteBeatKind.SwingZone)
                {
                    _glade = true;
                }

                _beatEnd = double.NegativeInfinity;
                tag = zone.Kind;
                surface = PathSurface.Trail;
            }
            else
            {
                if (_wasInZone)
                {
                    _wasInZone = false;
                    _beatEnd = double.NegativeInfinity;
                    _lastKind = -1;
                    _run = 0;
                }

                if (s >= _beatEnd)
                {
                    _world = WorldAt(s);
                    PickBeat(s, calm);
                }

                EvaluateBeat(s, calm, out kappaTarget, out gradeTarget);
                tag = _beatKind;
                surface = _beatSurface;
            }

            double jerk = _kJerk;
            if (hasZone)
            {
                kappaTarget = 0.0;
                gradeTarget = 0.0;
                double distance = zone.StartS - s;
                double excess = Math.Abs(_kappa) - _zoneKappa;
                double needed = (excess > 0.0 ? excess / _kJerk : 0.0) + _tuning.ZoneNoticeM;
                if (excess > Epsilon && distance < needed)
                {
                    jerk = _kJerkEmergency;
                    emergency = true;
                }
            }

            double kappaNext = Follow(kappaTarget, jerk);

            // Turn window guard (section 4.3): never plan a heading further than the limit from the heading one window ago.
            if (kappaTarget != 0.0)
            {
                double reference = YawBack(_window - 1);
                double endYaw = _psi + (kappaNext * _ds) + (kappaNext * Math.Abs(kappaNext) / (2.0 * jerk));
                if (Math.Abs(endYaw - reference) > TurnGuardFactor * _turnLimitRad)
                {
                    kappaNext = Follow(0.0, jerk);
                }
            }

            if (emergency && Math.Abs(kappaNext - _kappa) <= (_kJerk * _ds) + Epsilon)
            {
                emergency = false;
            }

            if (emergency)
            {
                EmergencyEaseCount++;
                LastEmergencyS = s;
            }

            _kappa = kappaNext;
            double psiNext = _psi + (_kappa * _ds);

            double pitchTarget = Math.Atan(gradeTarget * 0.01);
            double dPitch = pitchTarget - _theta;
            double maxDown = _ds / _crestR;
            double maxUp = _ds / _sagR;
            if (dPitch < -maxDown)
            {
                dPitch = -maxDown;
            }
            else if (dPitch > maxUp)
            {
                dPitch = maxUp;
            }

            double thetaNext = _theta + dPitch;

            double yawMid = 0.5 * (_psi + psiNext);
            double pitchMid = 0.5 * (_theta + thetaNext);
            double cosPitch = Math.Cos(pitchMid);
            _x += Math.Sin(yawMid) * cosPitch * _ds;
            _z += Math.Cos(yawMid) * cosPitch * _ds;
            _y += Math.Sin(pitchMid) * _ds;
            _psi = psiNext;
            _theta = thetaNext;

            _psiLowPass += (_psi - _psiLowPass) * _ds / LowPassM;
            PushYaw(_psi);
            _centerY += _tuning.GetWorld(_world).NetElevationPerKmM * 0.001 * _ds;
        }

        private double Follow(double kappaTarget, double jerk)
        {
            double step = jerk * _ds;
            double dk = kappaTarget - _kappa;
            if (dk > step)
            {
                dk = step;
            }
            else if (dk < -step)
            {
                dk = -step;
            }

            return _kappa + dk;
        }

        private WorldKind WorldAt(double s)
        {
            return _chunks != null ? _chunks.WorldKindAt(s) : WorldKind.Jungle;
        }

        private void PushYaw(double yaw)
        {
            _historyHead = (_historyHead + 1) % _yawHistory.Length;
            _yawHistory[_historyHead] = yaw;
            if (_historyCount < _yawHistory.Length)
            {
                _historyCount++;
            }
        }

        /// <summary>Heading <paramref name="steps"/> samples before the newest one (the oldest known if the run is younger).</summary>
        private double YawBack(int steps)
        {
            if (_historyCount == 0)
            {
                return 0.0;
            }

            int back = steps < _historyCount ? steps : _historyCount - 1;
            int n = _yawHistory.Length;
            return _yawHistory[(((_historyHead - back) % n) + n) % n];
        }

        private void EvaluateBeat(double s, bool calm, out double kappaTarget, out double gradeTargetPct)
        {
            double t = s - _beatStart;
            double length = _beatLength > 0.0 ? _beatLength : 1.0;
            kappaTarget = 0.0;
            gradeTargetPct = _beatGradePct;

            switch (_beatKind)
            {
                case RouteBeatKind.GentleBend:
                case RouteBeatKind.Bend:
                    kappaTarget = t < length - (Math.Abs(_beatKappa) / _kJerk) ? _beatKappa : 0.0;
                    break;
                case RouteBeatKind.SBend:
                    if (t < length * 0.5)
                    {
                        kappaTarget = _beatKappa;
                    }
                    else if (t < length - (Math.Abs(_beatKappa) / _kJerk))
                    {
                        kappaTarget = -_beatKappa;
                    }

                    break;
                case RouteBeatKind.Roll:
                    gradeTargetPct = _beatAmplitudePct * Math.Sin(2.0 * Math.PI * t / length);
                    break;
                case RouteBeatKind.RiseFall:
                case RouteBeatKind.Ascent:
                case RouteBeatKind.Descent:
                    gradeTargetPct = _beatAmplitudePct * Math.Sin(Math.PI * t / length);
                    break;
            }

            RouteWorldTuning world = _tuning.GetWorld(_world);
            if (_beatKind != RouteBeatKind.Clearing && _beatKind != RouteBeatKind.Crossing && _beatKind != RouteBeatKind.Bridge)
            {
                gradeTargetPct += world.NetElevationPerKmM * BaselineGradePerDrift;
            }

            double gradeCap = calm ? Math.Min(world.GradeMaxPct, _tuning.CalmGradeMaxPct) : world.GradeMaxPct;
            if (gradeTargetPct > gradeCap)
            {
                gradeTargetPct = gradeCap;
            }
            else if (gradeTargetPct < -gradeCap)
            {
                gradeTargetPct = -gradeCap;
            }

            if (calm)
            {
                double calmKappa = 1.0 / Math.Max(1.0, _tuning.CalmRMinM);
                if (kappaTarget > calmKappa)
                {
                    kappaTarget = calmKappa;
                }
                else if (kappaTarget < -calmKappa)
                {
                    kappaTarget = -calmKappa;
                }
            }
        }

        /// <summary>Pick weight of beat kind <paramref name="k"/>: the table weight per mean beat length, 0 after two repeats.</summary>
        private double LengthShare(RouteWorldTuning world, int k)
        {
            if (k == _lastKind && _run >= 2)
            {
                return 0.0;
            }

            var kind = (RouteBeatKind)k;
            double w = world.GetWeight(kind);
            if (w <= 0.0)
            {
                return 0.0;
            }

            double mean = 0.5 * (_tuning.GetMinLengthM(kind) + _tuning.GetMaxLengthM(kind));
            return mean > 1.0 ? w / mean : w;
        }

        /// <summary>Draws the next beat (always six draws) and sets the beat fields.</summary>
        private void PickBeat(double s, bool calm)
        {
            RouteWorldTuning world = _tuning.GetWorld(_world);
            double rMin = calm ? Math.Max(world.RMinM, _tuning.CalmRMinM) : world.RMinM;
            double gradeMax = calm ? Math.Min(world.GradeMaxPct, _tuning.CalmGradeMaxPct) : world.GradeMaxPct;

            double dKind = _rng.NextFloat();
            double dLength = _rng.NextFloat();
            double dSign = _rng.NextFloat();
            double dGrade = _rng.NextFloat();
            double dA = _rng.NextFloat();
            double dB = _rng.NextFloat();

            double total = 0.0;
            for (int k = 0; k < DrawnKinds; k++)
            {
                total += LengthShare(world, k);
            }

            var chosen = RouteBeatKind.Straight;
            if (total > 0.0)
            {
                double u = dKind * total;
                for (int k = 0; k < DrawnKinds; k++)
                {
                    double w = LengthShare(world, k);
                    if (w <= 0.0)
                    {
                        continue;
                    }

                    chosen = (RouteBeatKind)k;
                    if (u < w)
                    {
                        break;
                    }

                    u -= w;
                }
            }

            if (_glade)
            {
                chosen = RouteBeatKind.Clearing;
                _glade = false;
            }

            double length = Math.Floor(_tuning.GetMinLengthM(chosen) + (dLength * (_tuning.GetMaxLengthM(chosen) - _tuning.GetMinLengthM(chosen))));
            if (chosen != RouteBeatKind.Clearing && (s + length) - _lastClearingEndS > _tuning.ClearingEveryM)
            {
                chosen = RouteBeatKind.Clearing;
                length = Math.Floor(_tuning.GetMinLengthM(chosen) + (dLength * (_tuning.GetMaxLengthM(chosen) - _tuning.GetMinLengthM(chosen))));
            }

            double pRight = 0.5 - (0.5 * _psi / Math.Max(1e-6, _restoreRad));
            pRight = pRight < 0.1 ? 0.1 : (pRight > 0.9 ? 0.9 : pRight);
            double sign = dSign < pRight ? 1.0 : -1.0;

            double gradeCorridor = Math.Max(1.0, world.ElevationCorridorM);
            double pUp = world.RiseBias + ((_centerY - _y) / gradeCorridor);
            pUp = pUp < 0.05 ? 0.05 : (pUp > 0.95 ? 0.95 : pUp);
            double gradeSign = dGrade < pUp ? 1.0 : -1.0;

            double kappa = 0.0;
            double grade = 0.0;
            double amplitude = 0.0;
            PathSurface surface = world.BaseSurface;

            switch (chosen)
            {
                case RouteBeatKind.Straight:
                    grade = gradeSign * dA * Math.Min(3.0, gradeMax);
                    break;
                case RouteBeatKind.GentleBend:
                {
                    double radius = Math.Max(150.0 + (dB * 150.0), rMin);
                    kappa = sign / radius;
                    grade = gradeSign * dA * Math.Min(4.0, gradeMax);
                    break;
                }

                case RouteBeatKind.Bend:
                {
                    double radius = rMin + (dB * Math.Max(0.0, 150.0 - rMin));
                    kappa = sign / radius;
                    double ramp = (1.0 / radius) / _kJerk;
                    length = Math.Min(length, Math.Floor(ramp + (MaxBendTurnRad * radius)));
                    grade = gradeSign * dA * Math.Min(6.0, gradeMax);
                    break;
                }

                case RouteBeatKind.SBend:
                {
                    double low = Math.Max(rMin, 100.0);
                    double radius = low + (dB * Math.Max(0.0, 200.0 - low));
                    kappa = sign / radius;
                    grade = gradeSign * dA * Math.Min(5.0, gradeMax);
                    break;
                }

                case RouteBeatKind.Roll:
                {
                    double metres = 1.0 + (1.5 * dA);
                    double slope = Math.Min(
                        2.0 * Math.PI * metres / length,
                        Math.Min(0.5 * gradeMax * 0.01, 0.9 * length / (2.0 * Math.PI * _crestR)));
                    amplitude = slope * 100.0;
                    break;
                }

                case RouteBeatKind.RiseFall:
                case RouteBeatKind.Ascent:
                case RouteBeatKind.Descent:
                {
                    double peak = Math.Min(
                        gradeMax,
                        Math.Min(0.95 * length / (Math.PI * _crestR), 0.95 * length / (Math.PI * _sagR)) * 100.0);
                    double signed = chosen == RouteBeatKind.Ascent ? 1.0 : (chosen == RouteBeatKind.Descent ? -1.0 : gradeSign);
                    amplitude = signed * peak * (0.5 + (0.5 * dA));
                    break;
                }

                case RouteBeatKind.Clearing:
                    grade = -dA * 2.0;
                    break;
                case RouteBeatKind.Crossing:
                    surface = dB < 0.5 ? PathSurface.Ford : PathSurface.Mud;
                    break;
                case RouteBeatKind.Bridge:
                    grade = (dA * 4.0) - 2.0;
                    surface = PathSurface.Planks;
                    break;
            }

            if (chosen == RouteBeatKind.GentleBend || chosen == RouteBeatKind.Bend || chosen == RouteBeatKind.SBend)
            {
                double magnitude = Math.Abs(kappa);
                double ramp = magnitude / _kJerk;
                double peakTurn = chosen == RouteBeatKind.SBend
                    ? magnitude * Math.Max((length * 0.5) - (ramp * 0.5), 0.0)
                    : magnitude * Math.Max(length - ramp, 0.0);
                if ((Math.Sign(kappa) * _psi) + peakTurn > RestoreFlipFactor * _restoreRad)
                {
                    kappa = -kappa;
                }
            }

            if (chosen == RouteBeatKind.Clearing)
            {
                _lastClearingEndS = s + length;
            }

            if ((int)chosen == _lastKind)
            {
                _run++;
            }
            else
            {
                _run = 1;
                _lastKind = (int)chosen;
            }

            _beatId++;
            _beatKind = chosen;
            _beatSurface = surface;
            _beatStart = s;
            _beatLength = length;
            _beatEnd = s + length;
            _beatKappa = kappa;
            _beatGradePct = grade;
            _beatAmplitudePct = amplitude;
        }
    }
}
