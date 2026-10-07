using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 004 AC-435: the schema v2 golden traces 07 to 12 (tools/sim/golden, float64 reference) replayed on the C#
    /// runner. Position tolerance 1e-4 m, theta and omega 5e-6 (section 15). The chain trace is replayed in one lane
    /// (aiming across two lanes is not possible), so its x is not compared.
    /// </summary>
    public sealed class VineGoldenReplayTests
    {
        private static void Replay(GoldenVineTrace g, bool compareX)
        {
            var sc = new VineScenario(15.0, g.OverChasm, g.VineZ);
            sc.Sim.SpeedMultiplier = g.SpeedMultiplier;
            sc.GrabAt(g.GrabZ - VineScenario.PivotZ, g.GrabY, g.EntrySpeed);

            AssertTick(g, sc, 0, compareX);
            for (int t = 1; t < g.TickCount; t++)
            {
                bool swipe = Array.IndexOf(g.SwipeTicks, t) >= 0;
                sc.Step(swipe ? InputCommand.Jump : InputCommand.None);
                AssertTick(g, sc, t, compareX);
            }

            // Events: grabs and releases on the golden ticks with the golden grades.
            int grabIndex = 0;
            int releaseIndex = 0;
            for (int i = 0; i < sc.Events.Count; i++)
            {
                RunnerEvent e = sc.Events[i];
                if (e.Type == RunnerEventType.VineGrabbed)
                {
                    grabIndex++;
                }
                else if (e.Type == RunnerEventType.VineReleased)
                {
                    Assert.Less(releaseIndex, g.ReleaseGrades.Length, g.Name + " extra release");
                    Assert.AreEqual(g.ReleaseGrades[releaseIndex], e.Value, g.Name + " release " + releaseIndex + " grade");
                    releaseIndex++;
                }
            }

            Assert.AreEqual(g.GrabTicks.Length, grabIndex, g.Name + " grabs");
            Assert.AreEqual(g.ReleaseTicks.Length, releaseIndex, g.Name + " releases");
        }

        private static void AssertTick(GoldenVineTrace g, VineScenario sc, int t, bool compareX)
        {
            RunnerState s = sc.State;
            string at = g.Name + " tick " + t;
            switch (g.State[t])
            {
                case GoldenVineTrace.StateCarried:
                    Assert.AreEqual(Locomotion.Carried, s.Locomotion, at);
                    Assert.AreEqual(g.Theta[t], s.SwingAngleRad, GoldenVineVectors.ThetaTolerance, at + " theta");
                    Assert.AreEqual(g.Omega[t], s.SwingOmegaRadS, GoldenVineVectors.ThetaTolerance, at + " omega");
                    break;
                case GoldenVineTrace.StateFalling:
                    Assert.AreEqual(Locomotion.Falling, s.Locomotion, at);
                    break;
                default:
                    Assert.AreEqual(Locomotion.Running, s.Locomotion, at);
                    break;
            }

            Assert.AreEqual(g.Z[t], s.Z, GoldenVineVectors.PositionTolerance, at + " z");
            Assert.AreEqual(g.Y[t], s.Y, GoldenVineVectors.PositionTolerance, at + " y");
            if (compareX)
            {
                Assert.AreEqual(g.X[t], s.X, GoldenVineVectors.PositionTolerance, at + " x");
            }
        }

        [Test]
        public void Golden07_PerfectRelease()
        {
            Replay(GoldenVineVectors.Perfect07, true);
        }

        [Test]
        public void Golden08_PoorAutoRelease()
        {
            Replay(GoldenVineVectors.Poor08, true);
        }

        [Test]
        public void Golden09a_EntrySpeed8IsPulledUpTo13()
        {
            Replay(GoldenVineVectors.ClampEntry8, true);
        }

        [Test]
        public void Golden09b_EntrySpeed21IsSlowedTo16()
        {
            Replay(GoldenVineVectors.ClampEntry21, true);
        }

        [Test]
        public void Golden10_ChainOfTwoVines18MetresApart()
        {
            Replay(GoldenVineVectors.Chain10, false);
        }

        [Test]
        public void Golden12_BoostEntryIsTheSameSwingAsEntry21()
        {
            Replay(GoldenVineVectors.Boost12, true);

            // The multiplier plays no role: the swing equals the entry-21 swing tick by tick.
            GoldenVineTrace boost = GoldenVineVectors.Boost12;
            Assert.AreEqual(1.6, boost.SpeedMultiplier, 0.0);
            Assert.AreEqual(GoldenVineVectors.ClampEntry21.Theta[10], boost.Theta[10], 1e-9);
        }

        [Test]
        public void Golden11_ALateJumpDoesNotGrabAndFallsIntoTheChasmAsMissedVine()
        {
            // The trace starts at z = 90 with the pivot at 100: shift the world by -90 (start at z = 0, pivot at 10).
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            RunnerConfig config = RunnerConfig.FromDesignValues(values);
            VineConfig vines = VineConfig.CreateDefault();
            var track = new VineTestTrack(config);
            track.AddVine(1, 10.0, 0, true);
            track.Ground.AddGap(6.0, 22.0);
            var sim = new RunnerSimulation(config, SpeedCurve.CreateConstant(10.0), track, null, vines);

            int deathTick = -1;
            for (int t = 0; t < 70 && deathTick < 0; t++)
            {
                sim.Step(t == 54 ? InputCommand.Jump : InputCommand.None);
                for (int e = 0; e < sim.Events.Count; e++)
                {
                    Assert.AreNotEqual(RunnerEventType.VineGrabbed, sim.Events[e].Type, "no grab");
                    if (sim.Events[e].Type == RunnerEventType.Died)
                    {
                        deathTick = t;
                    }
                }

                sim.Events.Clear();
            }

            Assert.AreEqual(57, deathTick, "golden death tick");
            Assert.AreEqual(DeathCause.MissedVine, sim.DeathCause, "the golden's Fell is relabelled MissedVine by the vine layer");
        }
    }
}
