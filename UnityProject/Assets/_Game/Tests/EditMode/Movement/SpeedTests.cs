using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §2.2: AC-101-01, AC-101-02, stumble part of AC-101-26.</summary>
    public sealed class SpeedTests
    {
        [TestCase(0f)]
        [TestCase(500f)]
        [TestCase(1500f)]
        [TestCase(3000f)]
        [TestCase(8000f)]
        public void AC01_SpeedCurve_MatchesFormula(float d)
        {
            var config = new RunSpeedConfig();
            var curve = new SpeedCurve(config);
            double expected = config.V0 + ((config.VMax - config.V0) * (1.0 - Math.Exp(-d / config.DScale)));
            Assert.AreEqual(expected, curve.Evaluate(d), 0.01);
        }

        [Test]
        public void AC01_SpecReferenceValues()
        {
            var curve = new SpeedCurve(new RunSpeedConfig());
            Assert.AreEqual(10.92f, curve.Evaluate(500f), 0.01f);
            Assert.AreEqual(12.36f, curve.Evaluate(1500f), 0.01f);
            Assert.AreEqual(13.79f, curve.Evaluate(3000f), 0.01f);
            Assert.AreEqual(14.87f, curve.Evaluate(5000f), 0.01f);
            Assert.AreEqual(15.58f, curve.Evaluate(8000f), 0.01f);
        }

        [Test]
        public void AC01_SimulationUsesCurveAfterRamp()
        {
            var rig = new SimRig(SimRig.Path(3.5f), RunOptions.Default);
            var curve = new SpeedCurve(new RunSpeedConfig());
            rig.Steps(600);
            float before = rig.State.Distance;
            rig.Steps(1);
            Assert.AreEqual(curve.Evaluate(before), rig.State.Speed, 1e-4f);
        }

        [Test]
        public void AC02_StartRampReachesV0AtExactly48Ticks_InputsLive()
        {
            var rig = new SimRig(SimRig.Path(3.5f), RunOptions.Default);
            var curve = new SpeedCurve(new RunSpeedConfig());
            Assert.AreEqual(48, rig.Sim.StartRampTicks);
            float previousSpeed = 0f;
            for (int tick = 1; tick <= 48; tick++)
            {
                float d = rig.State.Distance;

                // Steering is live during the ramp.
                rig.Step(new InputFrame(InputCommand.None, 10));
                float full = curve.Evaluate(d);
                if (tick < 48)
                {
                    Assert.Less(rig.State.Speed, full, "tick " + tick);
                    Assert.Greater(rig.State.Speed, previousSpeed, "monotonic ramp, tick " + tick);
                }
                else
                {
                    Assert.AreEqual(full, rig.State.Speed, 1e-5f);
                    Assert.GreaterOrEqual(rig.State.Speed, 10f);
                }

                previousSpeed = rig.State.Speed;
            }

            Assert.AreEqual(0.48f, rig.State.XTarget, 1e-4f);
            Assert.Greater(rig.State.X, 0.1f);
        }

        [Test]
        public void AC02_JumpDuringRampExecutes()
        {
            var rig = new SimRig(SimRig.Path(3.5f), RunOptions.Default);
            rig.Steps(5);
            rig.Step(InputCommand.Jump);
            Assert.IsTrue(rig.State.Jumped);
            Assert.Greater(rig.State.Y, 0f);
        }

        [Test]
        public void AC26_StumbleSlowsTo80PercentAndRecoversIn48Ticks()
        {
            SimRig rig = SimRig.Flat(12f, c => SimRig.Low(c, 20f, 20.5f));
            rig.StepUntil(s => s.Hits > 0);
            Assert.AreEqual(1, rig.State.Hits);
            Assert.AreEqual(12f * 0.8f, rig.State.Speed, 1e-4f, "instant ×0.80 on the hit tick");
            long hitTick = rig.State.Tick;
            rig.Steps(1);
            Assert.Less(rig.State.Speed, 12f);
            rig.StepUntil(s => s.Tick == hitTick + 47);
            Assert.Less(rig.State.Speed, 12f);
            rig.Steps(1);
            Assert.AreEqual(12f, rig.State.Speed, 1e-4f, "back to 1.0× after 48 ticks");
        }
    }
}
