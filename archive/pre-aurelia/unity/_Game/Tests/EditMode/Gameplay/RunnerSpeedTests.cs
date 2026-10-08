using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 4: forward running and speed (AC-01 to AC-05).</summary>
    public sealed class RunnerSpeedTests
    {
        [Test]
        public void AC01_SpeedCurveInterpolatesAndCaps()
        {
            SpeedCurve curve = SpeedCurve.CreateDefault();

            Assert.AreEqual(10.0, curve.Evaluate(0.0), 1e-9);
            Assert.AreEqual(11.0, curve.Evaluate(250.0), 1e-9);
            Assert.AreEqual(21.0, curve.Evaluate(7000.0), 1e-9);
            Assert.AreEqual(21.0, curve.Evaluate(20000.0), 1e-9);
            Assert.AreEqual(10.0, curve.Evaluate(-5.0), 1e-9);
        }

        [Test]
        public void AC01_TutorialFlagUsesTutorialSpeed()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.RunStartRampMs = 0f;
            var h = new RunnerTestHarness(values);
            h.Sim.TutorialActive = true;

            Assert.AreEqual(8.0f, h.Step().Speed, 1e-5f);

            h.Sim.TutorialActive = false;
            Assert.AreEqual(10.0f, h.Step().Speed, 1e-5f);
        }

        [Test]
        public void AC02_StartRampEasesFromHalfToCurveSpeed()
        {
            var h = new RunnerTestHarness();
            var speeds = new List<float>();
            for (int i = 0; i < 40; i++)
            {
                speeds.Add(h.Step().Speed);
            }

            Assert.AreEqual(5.0f, speeds[0], 1e-4f);
            Assert.AreEqual(7.5f, speeds[15], 1e-4f);
            for (int tick = 30; tick < 40; tick++)
            {
                Assert.AreEqual(10.0f, speeds[tick], 1e-4f, "tick " + tick);
            }

            for (int tick = 1; tick < 30; tick++)
            {
                Assert.Greater(speeds[tick], speeds[tick - 1], "ramp must rise every tick");
            }
        }

        [Test]
        public void AC02_InputIsAcceptedDuringTheRamp()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.MoveRight);

            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.MoveRight);
            Assert.Greater(h.State.X, 0f);
        }

        [Test]
        public void AC03_SixHundredTicksAtTenMetresPerSecondIsOneHundredMetres()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0);
            h.RunThrough(599);

            Assert.AreEqual(600L, h.Sim.NextTick);
            Assert.AreEqual(100.0, h.State.Z, 0.001);
        }

        [Test]
        public void AC04_SpeedStepReachedFiresOncePerRowOnTheCrossingTick()
        {
            var h = new RunnerTestHarness();
            SpeedCurve curve = SpeedCurve.CreateDefault();
            var crossingTicks = new long[curve.RowCount];
            for (int i = 0; i < crossingTicks.Length; i++)
            {
                crossingTicks[i] = -1;
            }

            double previousZ = 0.0;
            while (h.State.Z < 7100.0)
            {
                RunnerState s = h.Step();
                for (int row = 1; row < curve.RowCount; row++)
                {
                    double d = curve.GetRowDistance(row);
                    if (previousZ < d && s.Z >= d)
                    {
                        crossingTicks[row] = s.Tick;
                    }
                }

                previousZ = s.Z;
            }

            List<RunnerEvent> steps = h.EventsOf(RunnerEventType.SpeedStepReached);
            Assert.AreEqual(curve.RowCount - 1, steps.Count, "row 0 is at 0 m and is never crossed");
            for (int i = 0; i < steps.Count; i++)
            {
                int row = i + 1;
                Assert.AreEqual(row, (int)steps[i].Value);
                Assert.AreEqual(crossingTicks[row], steps[i].Tick, "row " + row);
            }
        }

        [Test]
        public void AC05_SpeedMultiplierAtCapMovesPointFiveSixMetresPerTick_AirtimeUnchanged()
        {
            var h = RunnerTestHarness.ConstantSpeed(21.0);
            h.Sim.SpeedMultiplier = 1.6;
            h.RunThrough(9);
            double z0 = h.State.Z;
            h.Step();

            Assert.AreEqual(0.56, h.State.Z - z0, 1e-6);

            h.Step(InputCommand.Jump);
            long jumpTick = h.LastTick;
            h.RunThrough(jumpTick + 40);

            List<RunnerEvent> landed = h.EventsOf(RunnerEventType.Landed);
            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(jumpTick + 36, landed[0].Tick);
            Assert.AreEqual(36, (int)landed[0].Value);
        }
    }
}
