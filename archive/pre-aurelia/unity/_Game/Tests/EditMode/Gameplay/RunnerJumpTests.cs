using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 sections 6.2 and 6.3: jump and fast-fall (AC-16 to AC-21).</summary>
    public sealed class RunnerJumpTests
    {
        [Test]
        public void AC16_GroundJumpApexAtTick18_LandsAtTick36()
        {
            var h = new RunnerTestHarness();
            var ys = new List<float>();
            ys.Add(h.Step(InputCommand.Jump).Y);
            for (int i = 1; i <= 40; i++)
            {
                ys.Add(h.Step().Y);
                if (i < 36)
                {
                    Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion, "tick " + i);
                }
            }

            int apexTick = 0;
            for (int i = 1; i < ys.Count; i++)
            {
                if (ys[i] > ys[apexTick])
                {
                    apexTick = i;
                }
            }

            Assert.AreEqual(18, apexTick);
            Assert.AreEqual(1.5f, ys[18], 0.001f);
            Assert.AreEqual(0f, ys[36]);

            Assert.AreEqual(1, h.CountOf(RunnerEventType.JumpStarted));
            Assert.AreEqual(0L, h.EventsOf(RunnerEventType.JumpStarted)[0].Tick);
            Assert.AreEqual(18L, h.EventsOf(RunnerEventType.JumpApex)[0].Tick);
            List<RunnerEvent> landed = h.EventsOf(RunnerEventType.Landed);
            Assert.AreEqual(1, landed.Count);
            Assert.AreEqual(36L, landed[0].Tick);
            Assert.AreEqual(36, (int)landed[0].Value);
            Assert.IsFalse(landed[0].HasFlag(RunnerEventFlags.WasFastFall));
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
        }

        [Test]
        public void AC17_JumpArcIsIdenticalAtEverySpeed()
        {
            float[] reference = JumpTrace(10.0, 1.0);
            Assert.AreEqual(0f, reference[36]);
            CollectionAssert.AreEqual(reference, JumpTrace(8.0, 1.0), "8 m/s");
            CollectionAssert.AreEqual(reference, JumpTrace(21.0, 1.0), "21 m/s");
            CollectionAssert.AreEqual(reference, JumpTrace(21.0, 1.6), "33.6 m/s");
        }

        [Test]
        public void AC18_JumpInTheAirIsBufferedNotADoubleJump()
        {
            var h = new RunnerTestHarness();
            float[] reference = JumpTrace(10.0, 1.0);
            h.Step(InputCommand.Jump);
            h.RunThrough(9);
            h.Step(InputCommand.Jump); // tick 10, in the air

            Assert.AreEqual(CommandOutcome.Buffered, h.Sim.LastOutcomes.Jump);
            Assert.IsTrue(h.Sim.HasBufferedJump);
            Assert.AreEqual(reference[10], h.State.Y, 1e-6f, "the arc continues unchanged");

            h.RunThrough(40);
            Assert.AreEqual(1, h.CountOf(RunnerEventType.JumpStarted), "no double jump; the old buffer expired");
            Assert.AreEqual(1, h.Sim.Outcomes.Get(CommandOutcome.Expired));
        }

        [Test]
        public void AC19_FastFallFromApexLandsWithinSixTicks()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(18);
            Assert.AreEqual(1.5f, h.State.Y, 0.001f);

            h.Step(InputCommand.Slide); // tick 19
            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.Slide);
            Assert.AreEqual(Locomotion.FastFalling, h.State.Locomotion);
            h.RunThrough(30);

            Assert.AreEqual(19L, h.EventsOf(RunnerEventType.FastFallStarted)[0].Tick);
            RunnerEvent landed = h.EventsOf(RunnerEventType.Landed)[0];
            Assert.IsTrue(landed.HasFlag(RunnerEventFlags.WasFastFall));
            Assert.LessOrEqual(landed.Tick - 19 + 1, 6, "ticks from fast-fall start to landing, inclusive");
            Assert.AreEqual(24L, landed.Tick);
        }

        [TestCase(4.0, 6)]
        [TestCase(0.3, 2)]
        public void AC19_FastFallFromForcedHeightLandsWithinCap(double height, int maxTicks)
        {
            var h = new RunnerTestHarness();
            h.Sim.DebugPlaceInAir(height);
            h.Step(InputCommand.Slide); // tick 0
            Assert.AreEqual(Locomotion.FastFalling, h.State.Locomotion);
            h.RunThrough(20);

            List<RunnerEvent> landed = h.EventsOf(RunnerEventType.Landed);
            Assert.AreEqual(1, landed.Count);
            Assert.LessOrEqual(landed[0].Tick + 1, maxTicks);
        }

        [Test]
        public void AC19_FastFallTwiceIsAlreadyActive()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(9);
            h.Step(InputCommand.Slide);
            h.Step(InputCommand.Slide);

            Assert.AreEqual(CommandOutcome.AlreadyActive, h.Sim.LastOutcomes.Slide);
        }

        [Test]
        public void AC20_FastFallLandingSlidesFor39Ticks()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(18);
            h.Step(InputCommand.Slide); // tick 19, lands on 24
            h.RunThrough(24);

            const long landTick = 24;
            Assert.AreEqual(landTick, h.EventsOf(RunnerEventType.Landed)[0].Tick);
            List<RunnerEvent> slides = h.EventsOf(RunnerEventType.SlideStarted);
            Assert.AreEqual(1, slides.Count);
            Assert.AreEqual(landTick, slides[0].Tick);
            Assert.AreEqual(Locomotion.Sliding, h.State.Locomotion);

            for (long t = landTick + 1; t <= landTick + 38; t++)
            {
                Assert.AreEqual(Locomotion.Sliding, h.Step().Locomotion, "tick " + t);
            }

            Assert.AreEqual(Locomotion.Running, h.Step().Locomotion, "stands up on landing + 39");
            RunnerEvent ended = h.EventsOf(RunnerEventType.SlideEnded)[0];
            Assert.AreEqual(landTick + 39, ended.Tick);
            Assert.AreEqual((short)SlideEndReason.Timeout, ended.Value);
        }

        [Test]
        public void AC21_JumpDuringFastFallJumpsOnLandingInsteadOfSliding()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(18);
            h.Step(InputCommand.Slide); // tick 19
            h.Step(InputCommand.Jump); // tick 20, buffered
            Assert.AreEqual(CommandOutcome.Buffered, h.Sim.LastOutcomes.Jump);
            h.RunThrough(30);

            List<RunnerEvent> jumps = h.EventsOf(RunnerEventType.JumpStarted);
            Assert.AreEqual(2, jumps.Count);
            Assert.AreEqual(24L, jumps[1].Tick);
            Assert.IsTrue(jumps[1].HasFlag(RunnerEventFlags.Buffered));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.SlideStarted));
            Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion);
        }

        /// <summary>Y for the 41 ticks starting with a jump on tick 10 at a constant speed.</summary>
        internal static float[] JumpTrace(double speedMps, double multiplier)
        {
            var h = RunnerTestHarness.ConstantSpeed(speedMps);
            h.Sim.SpeedMultiplier = multiplier;
            h.RunThrough(9);
            var ys = new float[41];
            ys[0] = h.Step(InputCommand.Jump).Y;
            for (int i = 1; i < ys.Length; i++)
            {
                ys[i] = h.Step().Y;
            }

            Assert.AreEqual(46L, h.EventsOf(RunnerEventType.Landed)[0].Tick);
            return ys;
        }
    }
}
