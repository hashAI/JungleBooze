using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 AC-418 to AC-420 and AC-426: chains of vines 18 m apart and the vine chunk data.</summary>
    public sealed class VineChainTests
    {
        private static ChunkData Chunk(string id)
        {
            ChunkData[] chunks = JungleChunkLibraryDefaults.CreateVineChunks();
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i].Id == id)
                {
                    return chunks[i];
                }
            }

            throw new InvalidOperationException("No chunk " + id);
        }

        [TestCase("V-05", 110f)]
        [TestCase("V-06", 120f)]
        public void AC418_ChainPivotsAreEighteenMetresApart(string id, float length)
        {
            ChunkData chunk = Chunk(id);
            Assert.AreEqual(length, chunk.LengthM);
            Assert.GreaterOrEqual(chunk.VineCount, 2);
            for (int i = 1; i < chunk.VineCount; i++)
            {
                Assert.AreEqual(18f, chunk.GetVine(i).Zc - chunk.GetVine(i - 1).Zc, 1e-4f);
                Assert.IsFalse(chunk.GetVine(i).OverChasm, "chains are over ground");
            }

            Assert.AreEqual(34f, chunk.GetVine(0).Zc);
        }

        [Test]
        public void AC418_TheChainReleaseIsGuidedWithinTheFlightTimeLimitsAndArrivesAtOnePointFiveMetres()
        {
            double[] catchSpeeds = { 13.0, 14.0, 15.0, 16.0 };
            int[] swipes = { 27, 47, 60, 0 };
            for (int c = 0; c < catchSpeeds.Length; c++)
            {
                for (int s = 0; s < swipes.Length; s++)
                {
                    string label = "vC " + catchSpeeds[c] + " swipe " + swipes[s];
                    var sc = new VineScenario(15.0, false, new[] { VineScenario.PivotZ, VineScenario.PivotZ + 18.0 });
                    sc.GrabAt(-1.25, 1.2, catchSpeeds[c]);
                    sc.RunUntilReleased(swipes[s]);

                    double t = sc.Sim.LaunchFlightSeconds;
                    Assert.GreaterOrEqual(t, 0.85 - 1e-9, label);
                    Assert.LessOrEqual(t, 1.5 + 1e-9, label);

                    // Closed form at the next vine's z: the feet are 1.5 m high.
                    double tb = (VineScenario.PivotZ + 18.0 - sc.Sim.LaunchStartZ) / sc.Sim.LaunchForwardSpeedMps;
                    double yb = sc.Sim.LaunchStartY + (sc.Sim.LaunchVelocityYMps * tb) - (8.0 * tb * tb);
                    Assert.AreEqual(1.5, yb, 0.1, label);
                    Assert.AreEqual(t, tb, 1e-6, label);

                    // The catch of the next rope is automatic.
                    bool caught = false;
                    for (int i = 0; i < 200 && !caught; i++)
                    {
                        sc.Step();
                        caught = sc.CountOf(RunnerEventType.VineGrabbed) == 2;
                    }

                    Assert.IsTrue(caught, "second vine caught, " + label);
                    Assert.AreEqual(Locomotion.Carried, sc.State.Locomotion, label);
                    Assert.AreEqual(2, sc.State.VineId, label);
                }
            }
        }

        [Test]
        public void AC419_TheSecondSwingStartsAtTheArrivalSpeedClampedToTheCatchRange()
        {
            double[] catchSpeeds = { 13.0, 16.0 };
            int[] swipes = { 27, 47, 0 };
            for (int c = 0; c < catchSpeeds.Length; c++)
            {
                for (int s = 0; s < swipes.Length; s++)
                {
                    var sc = new VineScenario(15.0, false, new[] { VineScenario.PivotZ, VineScenario.PivotZ + 18.0 });
                    sc.GrabAt(-1.25, 1.2, catchSpeeds[c]);
                    sc.RunUntilReleased(swipes[s]);
                    double arrivalVx = sc.Sim.LaunchForwardSpeedMps;
                    for (int i = 0; i < 200 && sc.CountOf(RunnerEventType.VineGrabbed) < 2; i++)
                    {
                        sc.Step();
                    }

                    Assert.AreEqual(2, sc.CountOf(RunnerEventType.VineGrabbed));
                    double second = sc.State.SwingOmegaRadS * 14.0;
                    Assert.GreaterOrEqual(second, 13.0 - 0.001);
                    Assert.LessOrEqual(second, 14.78 + 0.001, "T401: later swings catch at 13 to 14.78 m/s");
                    Assert.AreEqual(Math.Min(Math.Max(arrivalVx, 13.0), 16.0), second, 0.001);
                }
            }
        }

        [Test]
        public void AC420_AnAimedLaneWithoutAVineIsANormalUnguidedRelease()
        {
            var sc = new VineScenario(15.0, true);
            sc.GrabAt(-1.25, 1.2, 15.0);
            for (int i = 1; i < 47; i++)
            {
                sc.Step(i == 10 ? InputCommand.MoveLeft : InputCommand.None);
            }

            Assert.AreEqual(0, sc.State.AimLane);
            Assert.AreEqual(0, sc.State.AimVineId, "no vine in the aimed lane");
            sc.Step(InputCommand.Jump);
            RunnerEvent released = sc.LastOf(RunnerEventType.VineReleased);
            Assert.IsFalse(released.HasFlag(RunnerEventFlags.VineChained));
            Assert.AreEqual((short)VineReleaseGrade.Perfect, released.Value);

            double y0 = sc.Sim.LaunchStartY;
            double vy = sc.Sim.LaunchVelocityYMps;
            Assert.AreEqual((vy + Math.Sqrt((vy * vy) + (32.0 * y0))) / 16.0, sc.Sim.LaunchFlightSeconds, 1e-9, "free flight, no guidance");
            Assert.AreEqual(0, sc.Sim.LaunchLane);
            Assert.Greater(sc.RunUntilLanded(), 0);
            Assert.AreEqual(0, sc.CountOf(RunnerEventType.Died));
        }

        [Test]
        public void AC426_ChasmChunksAreSixteenMetresWithTheRimFourMetresBeforeThePivot()
        {
            string[] ids = { "V-03", "V-04" };
            for (int i = 0; i < ids.Length; i++)
            {
                ChunkData chunk = Chunk(ids[i]);
                Assert.AreEqual(1, chunk.ObstacleCount);
                ObstaclePlacement gap = chunk.GetObstacle(0);
                Assert.AreEqual(ObstacleArchetype.Gap, gap.Archetype);
                Assert.AreEqual(16.0f, gap.GapLengthM, 1e-4f);
                float pivot = chunk.GetVine(0).Zc;
                Assert.AreEqual(pivot - 4.0f, gap.Zc, 1e-4f, "rim");
                Assert.AreEqual(pivot + 12.0f, gap.Zc + gap.GapLengthM, 1e-4f, "far edge");
                Assert.IsTrue(chunk.GetVine(0).OverChasm);
                Assert.AreEqual(110f, chunk.LengthM);
            }
        }

        [Test]
        public void AC426_APlainJumpAt21MetresPerSecondWithCoyoteCannotCrossTheChasm()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            RunnerConfig config = RunnerConfig.FromDesignValues(values);
            int survivors = 0;
            for (int jumpTick = 0; jumpTick < 400; jumpTick++)
            {
                var track = new TestTrackQuery(config);
                track.AddGap(96.0, 112.0);
                var sim = new RunnerSimulation(config, SpeedCurve.CreateConstant(21.0), track);
                bool dead = false;
                for (int i = 0; i < 500 && !dead; i++)
                {
                    sim.Step(i == jumpTick ? InputCommand.Jump : InputCommand.None);
                    for (int e = 0; e < sim.Events.Count; e++)
                    {
                        if (sim.Events[e].Type == RunnerEventType.Died)
                        {
                            dead = true;
                        }
                    }

                    sim.Events.Clear();
                }

                if (!dead)
                {
                    survivors++;
                }
            }

            Assert.AreEqual(0, survivors, "no plain jump crosses 16 m at 21 m/s");
        }

        [Test]
        public void PadCoinLinesStartPastTheLongestLanding()
        {
            // Farthest landing is pivot + 25.6 m (T401): coin lines in the pad start at 62 (single vine), 82 (V-05), 100 (V-06).
            AssertPadCoinStart("V-01", 62f);
            AssertPadCoinStart("V-03", 62f);
            AssertPadCoinStart("V-05", 82f);
            AssertPadCoinStart("V-06", 100f);
        }

        private static void AssertPadCoinStart(string id, float expected)
        {
            ChunkData chunk = Chunk(id);
            float lastVine = chunk.GetVine(chunk.VineCount - 1).Zc;
            float start = float.MaxValue;
            for (int i = 0; i < chunk.CoinPatternCount; i++)
            {
                CoinPattern p = chunk.GetCoinPattern(i);
                if (p.ZStart > lastVine)
                {
                    start = Math.Min(start, p.ZStart);
                }
            }

            Assert.AreEqual(expected, start, 1e-4f, id);
            Assert.GreaterOrEqual(start, lastVine + 25.6f - 1e-3f, id + ": pad coins are past the farthest landing");
        }
    }
}
