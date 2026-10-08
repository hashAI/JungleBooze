using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 001 section 6.5: coyote time, gaps and falling (AC-30 to AC-33).
    /// All tests run at a constant 10 m/s, so Z after tick k is (k + 1) / 6.
    /// </summary>
    public sealed class RunnerCoyoteTests
    {
        /// <summary>Steps until <see cref="RunnerEventType.LeftGround"/> fires and returns that tick.</summary>
        private static long RunToLeaveTick(RunnerTestHarness h)
        {
            while (h.CountOf(RunnerEventType.LeftGround) == 0)
            {
                h.Step();
                Assert.Less(h.LastTick, 10000L, "never left the ground");
            }

            return h.LastTick;
        }

        [Test]
        public void AC30_JumpOnLastCoyoteTickIsAFullJump()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(10.0, 14.0));
            Assert.AreEqual(5, h.Config.CoyoteTicks);
            long e = RunToLeaveTick(h);
            Assert.AreEqual(Locomotion.Coyote, h.State.Locomotion);
            Assert.AreEqual(0f, h.State.Y);

            h.RunThrough(e + 4);
            Assert.AreEqual(Locomotion.Coyote, h.State.Locomotion);
            h.Step(InputCommand.Jump); // e + 5
            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.Jump);

            RunnerEvent jump = h.EventsOf(RunnerEventType.JumpStarted)[0];
            Assert.AreEqual(e + 5, jump.Tick);
            Assert.IsTrue(jump.HasFlag(RunnerEventFlags.Coyote));

            float maxY = 0f;
            while (h.CountOf(RunnerEventType.Landed) == 0 && h.LastTick < e + 60)
            {
                maxY = System.Math.Max(maxY, h.Step().Y);
            }

            RunnerEvent landed = h.EventsOf(RunnerEventType.Landed)[0];
            Assert.AreEqual(e + 5 + 36, landed.Tick);
            Assert.AreEqual(36, (int)landed.Value);
            Assert.AreEqual(1.5f, maxY, 0.001f);
            Assert.IsFalse(h.State.IsDead);
        }

        [Test]
        public void AC30_JumpAfterCoyoteWindowIsBufferedAndHeroFalls()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(10.0, 14.0));
            long e = RunToLeaveTick(h);
            h.RunThrough(e + 5);
            Assert.AreEqual(Locomotion.Falling, h.State.Locomotion);

            h.Step(InputCommand.Jump); // e + 6
            Assert.AreEqual(CommandOutcome.Buffered, h.Sim.LastOutcomes.Jump);
            Assert.Less(h.State.Y, 0f);

            h.RunThrough(e + 60);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.JumpStarted));
            Assert.IsTrue(h.State.IsDead);
            Assert.AreEqual(DeathCause.Fell, h.Sim.DeathCause);
        }

        [Test]
        public void AC31_NoInputAfterLedgeDiesFromFall_CommandsBelowSurfaceIgnored()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(10.0, 200.0));
            long e = RunToLeaveTick(h);
            Assert.AreEqual(61L, e, "footprint fully over the gap once Z - 0.25 >= 10");

            float previousY = 0f;
            bool alternate = false;
            while (!h.State.IsDead)
            {
                previousY = h.State.Y;
                if (h.State.Y < 0f)
                {
                    InputCommand commands = alternate
                        ? InputCommand.MoveLeft | InputCommand.Jump
                        : InputCommand.MoveRight | InputCommand.Slide;
                    alternate = !alternate;
                    h.Step(commands);
                    TickOutcomes o = h.Sim.LastOutcomes;
                    if (alternate)
                    {
                        Assert.AreEqual(CommandOutcome.Ignored, o.MoveRight);
                        Assert.AreEqual(CommandOutcome.Ignored, o.Slide);
                    }
                    else
                    {
                        Assert.AreEqual(CommandOutcome.Ignored, o.MoveLeft);
                        Assert.AreEqual(CommandOutcome.Ignored, o.Jump);
                    }
                }
                else
                {
                    h.Step();
                }

                Assert.Less(h.LastTick, e + 100);
            }

            Assert.LessOrEqual(h.State.Y, -1.0f);
            Assert.Greater(previousY, -1.0f);
            Assert.AreEqual(e + 5 + 15, h.LastTick, "coyote ends on e + 5, then about 15 ticks of falling");

            List<RunnerEvent> died = h.EventsOf(RunnerEventType.Died);
            Assert.AreEqual(1, died.Count);
            Assert.AreEqual((short)DeathCause.Fell, died[0].Value);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.LaneChangeStarted));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.JumpStarted));

            // After death every command is ignored as well.
            h.Step(InputCommand.Jump);
            Assert.AreEqual(CommandOutcome.Ignored, h.Sim.LastOutcomes.Jump);
        }

        [Test]
        public void AC32_LandingWithoutGroundGetsNoCoyote_BufferedJumpNeverFires()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(5.0, 100.0));
            h.RunThrough(9);
            h.Step(InputCommand.Jump); // tick 10, Z = 1.83; lands on tick 46 at Z = 7.83, inside the gap
            h.RunThrough(40);
            h.Step(InputCommand.Jump); // tick 41, buffered
            Assert.AreEqual(CommandOutcome.Buffered, h.Sim.LastOutcomes.Jump);

            h.RunThrough(46);
            Assert.AreEqual(Locomotion.Falling, h.State.Locomotion);
            Assert.AreEqual(0f, h.State.Y);
            h.Step();
            Assert.Less(h.State.Y, 0f, "continues below the surface on the next tick");

            h.RunThrough(80);
            Assert.IsTrue(h.State.IsDead);
            Assert.AreEqual(DeathCause.Fell, h.Sim.DeathCause);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.Landed));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.LeftGround));
            Assert.AreEqual(1, h.CountOf(RunnerEventType.JumpStarted));
        }

        [TestCase(11.0, false)]
        [TestCase(15.5, true)]
        public void AC33_JumpOverFourMetreGap(double edgeZ, bool dies)
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(edgeZ, edgeZ + 4.0));
            h.RunThrough(58);
            h.Step(InputCommand.Jump); // tick 59, Z = 10.0
            Assert.AreEqual(10.0, h.State.Z, 1e-9);
            Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion);

            h.RunThrough(200);
            Assert.AreEqual(dies, h.State.IsDead);
            if (dies)
            {
                Assert.AreEqual(DeathCause.Fell, h.Sim.DeathCause);
                Assert.AreEqual(0, h.CountOf(RunnerEventType.Landed));
            }
            else
            {
                Assert.AreEqual(95L, h.EventsOf(RunnerEventType.Landed)[0].Tick);
            }
        }

        [Test]
        public void CoyoteEndsWhenALaneMoveFindsGround()
        {
            // Gap only under the middle lane.
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(10.0, 200.0, -1.2f, 1.2f));
            long e = RunToLeaveTick(h);
            h.Step(InputCommand.MoveRight); // e + 1
            Assert.AreEqual(Locomotion.Coyote, h.State.Locomotion);
            h.Step(); // e + 2, X = 1.18: footprint reaches past the gap edge
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            Assert.AreEqual(0f, h.State.Y);
            Assert.Greater(e, 0L);
        }
    }
}
