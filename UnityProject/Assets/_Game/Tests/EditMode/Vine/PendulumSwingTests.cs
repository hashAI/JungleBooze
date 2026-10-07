using System;
using System.IO;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Vine;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 AC-401 to AC-408, AC-428 and AC-436: the fixed-pivot pendulum in the runner simulation.</summary>
    public sealed class PendulumSwingTests
    {
        private const double Rope = 14.0;
        private const double Gravity = 22.0;
        private const double Dt = 1.0 / 60.0;

        private static VineScenario Canonical(double entrySpeed, bool chasm = true)
        {
            var scenario = new VineScenario(15.0, chasm);
            scenario.GrabAt(-1.25, 1.2, entrySpeed);
            return scenario;
        }

        [Test]
        public void AC402_TheHandStaysOnTheCircleAroundTheFixedPivot([Values(8.0, 15.0, 28.0)] double entrySpeed)
        {
            VineScenario sc = Canonical(entrySpeed);
            int carried = 0;
            for (int i = 1; i <= 120; i++)
            {
                RunnerState s = sc.Step();
                if (s.Locomotion != Locomotion.Carried)
                {
                    break;
                }

                carried++;
                Assert.AreEqual(VineScenario.PivotZ, s.SwingPivotZ, 0.0, "the pivot never moves, tick " + i);
                Assert.AreEqual(1, s.VineId);
                if (i >= 6)
                {
                    double dz = s.Z - s.SwingPivotZ;
                    double dy = (s.Y + sc.Vines.HandToFeetM) - sc.Vines.PivotHeightM;
                    Assert.AreEqual(Rope, Math.Sqrt((dz * dz) + (dy * dy)), 1e-5, "tick " + i);
                }
            }

            Assert.Greater(carried, 60);
        }

        [Test]
        public void AC404_CatchSpeedInTheSimulationIsTheClampOfTheEntrySpeed()
        {
            double[] entry = { 8, 10, 12, 13, 14, 15, 16, 18, 21, 24, 28 };
            double[] expected = { 13, 13, 13, 13, 14, 15, 16, 16, 16, 16, 16 };
            for (int i = 0; i < entry.Length; i++)
            {
                VineScenario sc = Canonical(entry[i]);
                Assert.AreEqual(expected[i], sc.State.SwingOmegaRadS * Rope, 0.001, "entry " + entry[i]);
            }
        }

        [Test]
        public void AC405_StartAngleComesFromTheGrabPositionAndTheHeroSettlesOntoTheArcInSixTicks()
        {
            VineScenario sc = Canonical(15.0);
            RunnerState grab = sc.State;
            Assert.AreEqual(Locomotion.Carried, grab.Locomotion);
            Assert.AreEqual(VineScenario.PivotZ - 1.25, grab.Z, 1e-9, "z does not jump at the grab");
            Assert.AreEqual(1.2f, grab.Y, 1e-6f, "y does not jump at the grab");
            Assert.AreEqual(Math.Asin(-1.25 / Rope), grab.SwingAngleRad, 1e-6);

            RunnerState first = sc.Step();
            Assert.LessOrEqual(Math.Abs(first.Z - grab.Z), 15.0 * Dt * 1.02, "the pendulum moves at the catch speed");

            RunnerState s = first;
            for (int i = 2; i <= 6; i++)
            {
                s = sc.Step();
            }

            double theta = s.SwingAngleRad;
            double handY = 17.0 - (Rope * Math.Cos(theta));
            Assert.AreEqual(handY - 1.75, s.Y, 1e-5, "y reaches the pendulum within 6 ticks");
            Assert.AreEqual(0f, s.X, 0f, "x is the lane centre within 6 ticks");
        }

        [TestCase(13.0, 43.87)]
        [TestCase(14.0, 47.39)]
        [TestCase(15.0, 50.96)]
        [TestCase(16.0, 54.58)]
        public void AC406_PeakAngleMatchesTheT401Table(double catchSpeed, double expectedDegrees)
        {
            Assert.AreEqual(expectedDegrees, PeakAngleDegrees(catchSpeed), 0.1);
        }

        [Test]
        public void AC406_PeakAngleNeverExceeds62DegreesForAnyEntrySpeedFrom0To40()
        {
            for (int v = 0; v <= 40; v++)
            {
                Assert.LessOrEqual(PeakAngleDegrees(v), 62.0, "entry " + v);
            }
        }

        [TestCase(13.0)]
        [TestCase(14.0)]
        [TestCase(15.0)]
        [TestCase(16.0)]
        public void AC407_ApexIsOnTickEightyFiveAndTheAutoReleaseIsPoor(double catchSpeed)
        {
            VineScenario sc = Canonical(catchSpeed);
            int tick = sc.RunUntilReleased(0);
            Assert.AreEqual(85, tick);
            RunnerEvent released = sc.LastOf(RunnerEventType.VineReleased);
            Assert.AreEqual((short)VineReleaseGrade.Auto, released.Value);
            Assert.LessOrEqual(tick, sc.Vines.SwingMaxTicks);
        }

        [Test]
        public void AC407_AGrabLateInTheZoneApexesEarlierButStillBeforeTheFailsafe()
        {
            var sc = new VineScenario(15.0, true);
            sc.GrabAt(1.25, 1.2, 15.0);
            int tick = sc.RunUntilReleased(0);
            Assert.GreaterOrEqual(tick, 70);
            Assert.LessOrEqual(tick, 85);
        }

        [Test]
        public void AC403_EnergyDriftIsAtMostOnePointFivePercentOver100TicksForAThousandStartStates()
        {
            var rng = new Pcg32Random(403UL);
            double worst = 0.0;
            for (int n = 0; n < 1000; n++)
            {
                double entry = rng.NextFloat(8f, 28f);
                double zRel = rng.NextFloat(-1.25f, 1.25f);
                double theta = DeterministicMath.AsinSmall(zRel / Rope);
                double omega = Math.Min(Math.Max(entry, 13.0), 16.0) / Rope;
                double e0 = Energy(theta, omega);
                for (int k = 0; k < 100; k++)
                {
                    omega -= (Gravity / Rope) * DeterministicMath.Sin(theta) * Dt;
                    theta += omega * Dt;
                    worst = Math.Max(worst, Math.Abs(Energy(theta, omega) - e0) / e0);
                }
            }

            Assert.LessOrEqual(worst, 0.015, "worst drift " + worst);
        }

        [Test]
        public void AC435_FloatSwingStaysWithinFiveMicroradiansOfTheDoubleReference()
        {
            var rng = new Pcg32Random(435UL);
            double worst = 0.0;
            for (int n = 0; n < 300; n++)
            {
                double entry = rng.NextFloat(13f, 16f);
                double zRel = rng.NextFloat(-1.25f, 1.25f);
                double theta = DeterministicMath.AsinSmall(zRel / Rope);
                double omega = entry / Rope;
                float thetaF = (float)theta;
                float omegaF = (float)omega;
                for (int k = 0; k < 90; k++)
                {
                    omega -= (Gravity / Rope) * DeterministicMath.Sin(theta) * Dt;
                    theta += omega * Dt;
                    omegaF -= (float)(Gravity / Rope) * SinF(thetaF) * (float)Dt;
                    thetaF += omegaF * (float)Dt;
                    worst = Math.Max(worst, Math.Abs(theta - thetaF));
                }
            }

            Assert.LessOrEqual(worst, 5e-6, "worst float error " + worst);
        }

        [Test]
        public void AC408_TwoRunsWithTheSameInputsGiveEqualStateHashesEveryTick()
        {
            VineScenario a = Canonical(21.0);
            VineScenario b = Canonical(21.0);
            Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash());
            for (int i = 1; i <= 150; i++)
            {
                InputCommand c = i == 47 ? InputCommand.Jump : InputCommand.None;
                a.Step(c);
                b.Step(c);
                Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash(), "step " + i);
            }
        }

        [Test]
        public void AC408_TheRunnerSimulationDoesNotCallSystemSinOrCos()
        {
            string path = Path.Combine(Application.dataPath, "_Game/Scripts/Gameplay/Runner/RunnerSimulation.cs");
            if (!File.Exists(path))
            {
                Assert.Inconclusive("Source file not found at " + path);
            }

            string text = File.ReadAllText(path).Replace("DeterministicMath.", string.Empty);
            Assert.IsFalse(text.Contains("Math.Sin("), "Math.Sin in the simulation");
            Assert.IsFalse(text.Contains("Math.Cos("), "Math.Cos in the simulation");
            Assert.IsFalse(text.Contains("UnityEngine.Random"), "UnityEngine.Random in the simulation");
            Assert.IsFalse(text.Contains("Time.deltaTime"), "Time.deltaTime in the simulation");
        }

        [Test]
        public void AC428_XIsTheVineLaneCentreFromTick6UntilTheReleaseAndAimingDoesNotMoveIt()
        {
            VineScenario sc = Canonical(15.0);
            for (int i = 1; i <= 80; i++)
            {
                InputCommand c = i == 10 ? InputCommand.MoveLeft : (i == 20 ? InputCommand.MoveRight : InputCommand.None);
                RunnerState s = sc.Step(c);
                Assert.AreEqual(Locomotion.Carried, s.Locomotion, "step " + i);
                if (i >= 6)
                {
                    Assert.AreEqual(0f, s.X, 0f, "step " + i);
                }

                if (i == 10)
                {
                    Assert.AreEqual(0, s.AimLane);
                }

                if (i == 20)
                {
                    Assert.AreEqual(1, s.AimLane);
                }
            }
        }

        [Test]
        public void AC436_SnapshotRoundTripMidSwingAndMidFlightGivesEqualHashes()
        {
            int[] copyAt = { 30, 47 + 20 };
            for (int c = 0; c < copyAt.Length; c++)
            {
                VineScenario a = Canonical(15.0);
                for (int i = 1; i <= copyAt[c]; i++)
                {
                    a.Step(i == 47 ? InputCommand.Jump : InputCommand.None);
                }

                VineScenario b = a.Twin();
                b.Sim.CopyStateFrom(a.Sim);
                Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash(), "copy at " + copyAt[c]);
                Assert.AreEqual(a.State.SwingAngleRad, b.State.SwingAngleRad);
                Assert.AreEqual(a.State.SwingOmegaRadS, b.State.SwingOmegaRadS);

                for (int i = 1; i <= 120; i++)
                {
                    a.Step();
                    b.Step();
                    Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash(), "copy at " + copyAt[c] + " + " + i);
                }
            }

            VineScenario m = Canonical(15.0);
            ulong h0 = m.Sim.ComputeStateHash();
            m.Step();
            Assert.AreNotEqual(h0, m.Sim.ComputeStateHash());
        }

        [Test]
        public void VineFreeRunsDoNotMixThePendulumStateIntoTheirHash()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            RunnerConfig config = RunnerConfig.FromDesignValues(values);
            var sim = new RunnerSimulation(config, SpeedCurve.CreateConstant(12.0));
            InputCommand[] script =
            {
                InputCommand.MoveLeft, InputCommand.None, InputCommand.Jump, InputCommand.None, InputCommand.None,
                InputCommand.MoveRight, InputCommand.Slide,
            };
            for (int i = 0; i < 120; i++)
            {
                sim.Step(i < script.Length ? script[i] : InputCommand.None);
                sim.Events.Clear();
            }

            ulong before = sim.ComputeStateHash();
            System.Reflection.FieldInfo theta = typeof(RunnerSimulation).GetField("_swingTheta", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo used = typeof(RunnerSimulation).GetField("_swingUsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(theta);
            Assert.IsNotNull(used);
            Assert.IsFalse((bool)used.GetValue(sim), "no vine was touched");
            theta.SetValue(sim, 0.5);
            Assert.AreEqual(before, sim.ComputeStateHash(), "unused pendulum state is not part of the hash, so vine-free runs hash as they did before the pendulum");
        }

        private static double PeakAngleDegrees(double entrySpeed)
        {
            VineScenario sc = Canonical(entrySpeed);
            double peak = 0.0;
            for (int i = 1; i <= 200; i++)
            {
                RunnerState s = sc.Step();
                if (s.Locomotion != Locomotion.Carried)
                {
                    break;
                }

                peak = Math.Max(peak, s.SwingAngleRad);
            }

            peak = Math.Max(peak, sc.Sim.ReleasedSwingThetaRad);
            return peak * 180.0 / Math.PI;
        }

        private static double Energy(double theta, double omega)
        {
            return (0.5 * Rope * Rope * omega * omega) + (Gravity * Rope * (1.0 - Math.Cos(theta)));
        }

        private static float SinF(float x)
        {
            float x2 = x * x;
            float p = (float)(-1.0 / 39916800.0);
            p = (p * x2) + (float)(1.0 / 362880.0);
            p = (p * x2) - (float)(1.0 / 5040.0);
            p = (p * x2) + (float)(1.0 / 120.0);
            p = (p * x2) - (float)(1.0 / 6.0);
            p = (p * x2) + 1.0f;
            return x * p;
        }
    }
}
