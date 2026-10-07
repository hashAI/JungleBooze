using System;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Track;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>Spec 003 T2: AC-303 (determinism), AC-304 and AC-306 (limits, no doubling back), AC-312 (emergency ease).</summary>
    public sealed class RouteGeneratorTests
    {
        private const double RouteLengthM = 5200.0;

        private static RouteSample[] BuildScripted(RouteTuning tuning, ulong seed, double lengthM, ScriptedChunkSource chunks, out WorldKind[] worlds)
        {
            return RouteValidator.Build(tuning, seed, -tuning.BehindM, lengthM, chunks, out worlds);
        }

        [Test]
        public void AC303_SameSeedAndChunksGiveTheSameRoute()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(seed, 1500.0);
                RouteSample[] a = BuildScripted(tuning, seed, 1500.0, chunks, out WorldKind[] worldsA);
                RouteSample[] b = BuildScripted(tuning, seed, 1500.0, chunks, out WorldKind[] worldsB);
                Assert.AreEqual(a.Length, b.Length);
                int firstDifference = -1;
                for (int i = 0; i < a.Length && firstDifference < 0; i++)
                {
                    bool same = a[i].Beat == b[i].Beat
                        && a[i].BeatId == b[i].BeatId
                        && Math.Abs(a[i].X - b[i].X) <= 0.01
                        && Math.Abs(a[i].Y - b[i].Y) <= 0.01
                        && Math.Abs(a[i].Z - b[i].Z) <= 0.01;
                    if (!same)
                    {
                        firstDifference = i;
                    }
                }

                Assert.AreEqual(-1, firstDifference, "seed " + seed + " differs at sample " + firstDifference);
                Assert.AreEqual(worldsA[worldsA.Length - 1], worldsB[worldsB.Length - 1]);
            }
        }

        [Test]
        public void AC303_DifferentSeedsGiveDifferentRoutes()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var empty = new ScriptedChunkSource();
            int pairs = 0;
            int different = 0;
            RouteSample[] previous = BuildScripted(tuning, 1UL, 1500.0, empty, out WorldKind[] unusedWorlds);
            for (ulong seed = 2; seed <= 1000; seed++)
            {
                RouteSample[] current = BuildScripted(tuning, seed, 1500.0, empty, out unusedWorlds);
                RouteSample endA = previous[previous.Length - 1];
                RouteSample endB = current[current.Length - 1];
                pairs++;
                if (Math.Abs(endA.X - endB.X) > 0.01 || Math.Abs(endA.Y - endB.Y) > 0.01 || endA.BeatId != endB.BeatId)
                {
                    different++;
                }

                previous = current;
            }

            Assert.GreaterOrEqual(different, (int)Math.Ceiling(pairs * 0.99), different + " of " + pairs + " neighbouring seeds differ");
        }

        [Test]
        public void AC303_RouteDoesNotDependOnHowOftenItIsExtended()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            for (ulong seed = 1; seed <= 3; seed++)
            {
                RouteTrackSimulator fine = RouteTrackSimulator.Build(seed, 2500.0, 0.7, tuning);
                RouteTrackSimulator coarse = RouteTrackSimulator.Build(seed, 2500.0, 13.0, tuning);
                int common = Math.Min(fine.Count, coarse.Count);
                Assert.Greater(common, 2000);
                for (int i = 0; i < common; i++)
                {
                    Assert.AreEqual(fine.Samples[i].X, coarse.Samples[i].X, 1e-9, "seed " + seed + " sample " + i);
                    Assert.AreEqual(fine.Samples[i].Z, coarse.Samples[i].Z, 1e-9, "seed " + seed + " sample " + i);
                    Assert.AreEqual(fine.Samples[i].Beat, coarse.Samples[i].Beat, "seed " + seed + " sample " + i);
                }
            }
        }

        [Test]
        public void AC304_AC306_ScriptedChunksRespectEveryLimit()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var validator = new RouteValidator(tuning);
            for (ulong seed = 1; seed <= 150; seed++)
            {
                ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(seed, RouteLengthM);
                RouteSample[] samples = BuildScripted(tuning, seed, RouteLengthM, chunks, out WorldKind[] worlds);
                RouteReport report = validator.Validate(samples, worlds, samples.Length, -tuning.BehindM);
                Assert.IsTrue(report.IsValid, "seed " + seed + ": " + report);
                Assert.AreEqual(0, report.EmergencySamples, "seed " + seed);
                Assert.GreaterOrEqual(report.Zones, 4, "seed " + seed + " should pass four gateways");
            }
        }

        [Test]
        public void AC304_AC306_RealTrackRoutesRespectEveryLimit()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var validator = new RouteValidator(tuning);
            for (ulong seed = 1; seed <= 12; seed++)
            {
                RouteTrackSimulator route = RouteTrackSimulator.Build(seed, RouteLengthM, 5.0, tuning);
                RouteReport report = validator.Validate(route.Samples, route.Worlds, route.Count, route.StartS);
                Assert.IsTrue(report.IsValid, "seed " + seed + ": " + report);
                Assert.AreEqual(0, report.EmergencySamples, "AC-312: no emergency ease with the real generator, seed " + seed);
                Assert.GreaterOrEqual(route.WorstReadMarginM, 0.0, "the route must never be built closer than ReadAheadM to the end of the committed track, seed " + seed);
            }
        }

        [Test]
        public void AC304_BeatsAreVariedAndFollowTheWorldTables()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var counts = new int[RouteTuning.BeatKindCount];
            for (ulong seed = 1; seed <= 40; seed++)
            {
                ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(seed, RouteLengthM);
                RouteSample[] samples = BuildScripted(tuning, seed, RouteLengthM, chunks, out WorldKind[] worlds);
                for (int i = 0; i < samples.Length; i++)
                {
                    counts[(int)samples[i].Beat]++;
                }
            }

            Assert.Greater(counts[(int)RouteBeatKind.Bend], 0);
            Assert.Greater(counts[(int)RouteBeatKind.SBend], 0);
            Assert.Greater(counts[(int)RouteBeatKind.RiseFall], 0);
            Assert.Greater(counts[(int)RouteBeatKind.Clearing], 0);
            Assert.Greater(counts[(int)RouteBeatKind.GentleBend], 0);
            Assert.Greater(counts[(int)RouteBeatKind.SwingZone], 0);
            Assert.Greater(counts[(int)RouteBeatKind.Gateway], 0);
            Assert.Greater(counts[(int)RouteBeatKind.Ascent], 0, "Ascent is scheduled by the layer schedule (T5)");
            Assert.Greater(counts[(int)RouteBeatKind.Descent], 0, "Descent is scheduled by the layer schedule (T5)");
        }

        [Test]
        public void AC312_ShortNoticeUsesTheEmergencyEaseWithinTheEmergencyJerk()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var validator = new RouteValidator(tuning)
            {
                CheckVisibility = false,
                CheckSelfIntersection = false,
            };

            int eased = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var chunks = new ScriptedChunkSource { NoticeLimitM = 18.0 };
                for (int k = 0; k < 6; k++)
                {
                    chunks.AddVine(400.0 + (k * 450.0), 80.0);
                }

                RouteSample[] samples = BuildScripted(tuning, seed, 3300.0, chunks, out WorldKind[] worlds);
                RouteReport report = validator.Validate(samples, worlds, samples.Length, -tuning.BehindM);
                Assert.AreEqual(0, report.JerkViolations, "seed " + seed + ": " + report);
                if (report.EmergencySamples > 0)
                {
                    eased++;
                }

                for (int i = 1; i < samples.Length; i++)
                {
                    double jerk = Math.Abs(samples[i].Curvature - samples[i - 1].Curvature);
                    Assert.LessOrEqual(jerk, tuning.EmergencyKappaJerk + 1e-6, "seed " + seed + " sample " + i);
                }
            }

            Assert.Greater(eased, 0, "with 18 m notice at least one seed must be mid-bend when a vine chunk appears");
        }

        [Test]
        public void StartIsCalmAndBeginsAtTheOrigin()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(7UL, 800.0);
            RouteSample[] samples = BuildScripted(tuning, 7UL, 800.0, chunks, out WorldKind[] worlds);
            Assert.AreEqual(0.0, samples[0].X, 1e-9);
            Assert.AreEqual(-tuning.BehindM, samples[0].Z, 1e-9);
            for (int i = 0; i < samples.Length; i++)
            {
                double s = -tuning.BehindM + i;
                if (s >= tuning.CalmStartM)
                {
                    break;
                }

                Assert.LessOrEqual(Math.Abs(samples[i].Curvature), (1.0 / tuning.CalmRMinM) + 1e-6, "calm start, s=" + s);
                Assert.LessOrEqual(Math.Abs(Math.Tan(samples[i].PitchRad) * 100.0), tuning.CalmGradeMaxPct + 0.05, "calm start, s=" + s);
            }
        }
    }
}
