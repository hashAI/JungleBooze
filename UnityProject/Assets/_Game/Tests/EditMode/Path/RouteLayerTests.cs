using System;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Track;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.RouteFrame
{
    /// <summary>Spec 003 T5: the layer schedule (AC-325) and that the layers never touch the track (AC-326).</summary>
    public sealed class RouteLayerTests
    {
        private const double RouteLengthM = 5200.0;
        private const double TenSecondsM = 210.0;

        private static RouteSample[] Build(RouteTuning tuning, ulong seed, ScriptedChunkSource chunks)
        {
            return RouteValidator.Build(tuning, seed, -tuning.BehindM, RouteLengthM, chunks, out _);
        }

        [Test]
        public void AC325_GatewayTableMirrorsTheWorldSchedule()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            WorldScheduleConfig schedule = WorldScheduleConfig.CreateDefault();
            double expected = 0.0;
            for (int n = 1; n <= 8; n++)
            {
                expected += tuning.GetWorldSegmentLengthM((n - 1) % tuning.WorldSegmentCount);
                Assert.AreEqual(schedule.SegmentStartZ(n), expected, 1e-6, "segment " + n);
            }
        }

        [Test]
        public void AC325_SectionsStartLateKeepClearOfGatewaysAndKeepGradeLimits()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            double[] gateways = { 1100.0, 2300.0, 3600.0, 5000.0 };
            int sections = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(seed, RouteLengthM);
                RouteSample[] samples = Build(tuning, seed, chunks);
                double lastAscentEndS = double.NegativeInfinity;
                bool sawAscent = false;
                bool wasHigh = false;
                bool lastWasZone = false;
                double runStartS = 0.0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double s = -tuning.BehindM + i;
                    RouteSample sample = samples[i];
                    bool high = sample.Layer == PathLayer.High;

                    if (sample.Beat == RouteBeatKind.Ascent)
                    {
                        sawAscent = true;
                        lastAscentEndS = s;
                        Assert.AreEqual(PathLayer.Floor, sample.Layer, "the Ascent is the root ramp on the Floor layer, seed " + seed + " s " + s);
                    }

                    if (sample.Beat == RouteBeatKind.Ascent || sample.Beat == RouteBeatKind.Descent)
                    {
                        double grade = Math.Abs(Math.Tan(sample.PitchRad) * 100.0);
                        Assert.LessOrEqual(grade, tuning.AscentGradePct + 0.05, "AC-325 grade, seed " + seed + " s " + s);
                    }

                    if (sample.Beat == RouteBeatKind.Descent)
                    {
                        Assert.IsTrue(sawAscent, "a Descent needs an earlier Ascent, seed " + seed);
                        Assert.AreEqual(PathLayer.High, sample.Layer, "seed " + seed + " s " + s);
                    }

                    if (high)
                    {
                        Assert.AreEqual(PathSurface.Bough, sample.Surface, "High is the bough surface, seed " + seed + " s " + s);
                        Assert.GreaterOrEqual(s, tuning.CalmStartM + tuning.CanopyFirstAfterCalmM - 1.0, "first High layer, seed " + seed);
                        Assert.GreaterOrEqual(s, 300.0, "AC-325: not in the first 300 m, seed " + seed);
                        Assert.AreNotEqual(RouteBeatKind.Gateway, sample.Beat, "a gateway is never on the High layer, seed " + seed);
                        for (int g = 0; g < gateways.Length; g++)
                        {
                            bool inside = s > gateways[g] - 60.0 - 27.0 && s < gateways[g] + TenSecondsM;
                            Assert.IsFalse(inside, "AC-325: High layer within 10 s of gateway " + gateways[g] + ", seed " + seed + " s " + s);
                        }

                        if (!wasHigh)
                        {
                            sections++;
                            runStartS = s;
                        }

                        bool zone = sample.Beat == RouteBeatKind.SwingZone;
                        if (zone && !lastWasZone)
                        {
                            Assert.GreaterOrEqual(s - lastAscentEndS, tuning.VineAfterAscentM, "AC-325: vine inside High starts 30 m after the Ascent, seed " + seed + " s " + s);
                        }

                        lastWasZone = zone;
                        Assert.LessOrEqual(s - runStartS, tuning.HighRunMaxM + (2.0 * tuning.GetMaxLengthM(RouteBeatKind.Descent)) + 40.0, "one High run is too long, seed " + seed + " s " + s);
                    }
                    else
                    {
                        lastWasZone = false;
                    }

                    wasHigh = high;
                }
            }

            Assert.Greater(sections, 20, "canopy sections must actually be scheduled (Jungle and River)");
        }

        [Test]
        public void AC325_LayerScheduleIsDeterministicAndFollowsTheWorldMask()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            RouteTuning off = RouteTuning.CreateDefault();
            off.SetCanopyWorldMask(0);
            for (ulong seed = 1; seed <= 40; seed++)
            {
                ScriptedChunkSource chunks = ScriptedChunkSource.CreateFor(seed, RouteLengthM);
                RouteSample[] a = Build(tuning, seed, chunks);
                RouteSample[] b = Build(tuning, seed, chunks);
                RouteSample[] floorOnly = Build(off, seed, chunks);
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].Layer, b[i].Layer, "seed " + seed + " sample " + i);
                    Assert.AreEqual(a[i].Surface, b[i].Surface, "seed " + seed + " sample " + i);
                    Assert.AreEqual(a[i].Beat, b[i].Beat, "seed " + seed + " sample " + i);
                    Assert.AreEqual(PathLayer.Floor, floorOnly[i].Layer, "mask 0 means Floor only, seed " + seed);
                    Assert.AreNotEqual(RouteBeatKind.Ascent, floorOnly[i].Beat, "seed " + seed);
                }
            }
        }

        [Test]
        public void AC325_RoutesWithLayersRespectEveryLimit()
        {
            RouteTuning tuning = RouteTuning.CreateDefault();
            var validator = new RouteValidator(tuning);
            for (ulong seed = 1; seed <= 12; seed++)
            {
                RouteTrackSimulator route = RouteTrackSimulator.Build(seed, RouteLengthM, 5.0, tuning);
                RouteReport report = validator.Validate(route.Samples, route.Worlds, route.Count, route.StartS);
                Assert.IsTrue(report.IsValid, "seed " + seed + ": " + report);
                Assert.AreEqual(0, report.EmergencySamples, "seed " + seed);
                for (int i = 0; i < route.Count; i++)
                {
                    if (route.Samples[i].Layer != PathLayer.High)
                    {
                        continue;
                    }

                    double s = route.StartS + i;
                    Assert.AreNotEqual(RouteBeatKind.Gateway, route.Samples[i].Beat, "seed " + seed + " s " + s);
                    Assert.IsTrue(route.Worlds[i] == WorldKind.Jungle || route.Worlds[i] == WorldKind.River, "canopy only in Jungle and River, seed " + seed + " s " + s);
                }
            }
        }

        [Test]
        public void AC326_LayersDoNotChangeTheChunksOrTheGaps()
        {
            RouteTuning layered = RouteTuning.CreateDefault();
            RouteTuning floorOnly = RouteTuning.CreateDefault();
            floorOnly.SetCanopyWorldMask(0);
            int seedsWithHigh = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                RouteTrackSimulator a = RouteTrackSimulator.Build(seed, 2600.0, 5.0, layered);
                RouteTrackSimulator b = RouteTrackSimulator.Build(seed, 2600.0, 5.0, floorOnly);
                for (int i = 0; i < a.Count; i++)
                {
                    if (a.Samples[i].Layer == PathLayer.High)
                    {
                        seedsWithHigh++;
                        break;
                    }
                }

                TrackSimulation ta = a.TrackSim;
                TrackSimulation tb = b.TrackSim;
                Assert.AreEqual(ta.GeneratedChunkCount, tb.GeneratedChunkCount, "seed " + seed);
                Assert.AreEqual(ta.GeneratedEndZ, tb.GeneratedEndZ, 1e-9, "seed " + seed);
                for (int serial = ta.OldestChunkSerial; serial <= ta.GeneratedChunkCount; serial++)
                {
                    ChunkPeek ca = ta.PeekChunk(serial);
                    ChunkPeek cb = tb.PeekChunk(serial);
                    Assert.AreEqual(ca.Kind, cb.Kind, "seed " + seed + " chunk " + serial);
                    Assert.AreEqual(ca.StartZ, cb.StartZ, 1e-9, "seed " + seed + " chunk " + serial);
                    Assert.AreEqual(ca.LengthM, cb.LengthM, 1e-6f, "seed " + seed + " chunk " + serial);
                }

                Assert.AreEqual(ta.ObstacleCount, tb.ObstacleCount, "seed " + seed);
                for (int i = 0; i < ta.ObstacleCount; i++)
                {
                    ref readonly ObstacleInstance oa = ref ta.GetObstacle(i);
                    ref readonly ObstacleInstance ob = ref tb.GetObstacle(i);
                    Assert.AreEqual(oa.Archetype, ob.Archetype, "seed " + seed + " obstacle " + i);
                    Assert.AreEqual(oa.Z, ob.Z, 1e-9, "seed " + seed + " obstacle " + i);
                    Assert.AreEqual(oa.LaneMask, ob.LaneMask, "seed " + seed + " obstacle " + i);
                    Assert.AreEqual(oa.GapLengthM, ob.GapLengthM, 1e-6f, "seed " + seed + " obstacle " + i);
                }
            }

            Assert.Greater(seedsWithHigh, 0, "the layered runs must contain a High layer for the comparison to mean something");
        }
    }
}
