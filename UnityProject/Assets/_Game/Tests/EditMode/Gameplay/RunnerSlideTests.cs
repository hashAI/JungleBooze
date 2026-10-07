using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 6.4: slide (AC-22 to AC-25).</summary>
    public sealed class RunnerSlideTests
    {
        /// <summary>Stand-in for a high barrier: standing is blocked while the footprint overlaps [zStart, zEnd].</summary>
        private sealed class BlockedHeadroom : IHeadroomQuery
        {
            private readonly double _zStart;
            private readonly double _zEnd;

            public BlockedHeadroom(double zStart, double zEnd)
            {
                _zStart = zStart;
                _zEnd = zEnd;
            }

            public bool CanStand(float xMin, float xMax, double zMin, double zMax, float standingHeightM)
            {
                return !(zMax > _zStart && zMin < _zEnd);
            }
        }

        [Test]
        public void AC22_GroundSlideLasts39Ticks_HitboxShortThenStanding()
        {
            var h = new RunnerTestHarness();
            Assert.AreEqual(39, h.Config.SlideTicks);

            RunnerState s = h.Step(InputCommand.Slide);
            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.Slide);
            Assert.AreEqual(0.8f, s.HitboxHeight, "short hitbox from the start tick");

            for (int tick = 1; tick <= 38; tick++)
            {
                s = h.Step();
                Assert.AreEqual(Locomotion.Sliding, s.Locomotion, "tick " + tick);
                Assert.AreEqual(0.8f, s.HitboxHeight, "tick " + tick);
            }

            s = h.Step(); // tick 39
            Assert.AreEqual(Locomotion.Running, s.Locomotion);
            Assert.AreEqual(1.8f, s.HitboxHeight);

            List<RunnerEvent> ended = h.EventsOf(RunnerEventType.SlideEnded);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(39L, ended[0].Tick);
            Assert.AreEqual((short)SlideEndReason.Timeout, ended[0].Value);
            Assert.IsFalse(h.EventsOf(RunnerEventType.SlideStarted)[0].HasFlag(RunnerEventFlags.Restart));
        }

        [Test]
        public void AC23_SlideAgainRestartsTheTimer()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Slide);
            h.RunThrough(19);
            h.Step(InputCommand.Slide); // tick 20

            List<RunnerEvent> starts = h.EventsOf(RunnerEventType.SlideStarted);
            Assert.AreEqual(2, starts.Count);
            Assert.IsTrue(starts[1].HasFlag(RunnerEventFlags.Restart));

            h.RunThrough(58);
            Assert.AreEqual(Locomotion.Sliding, h.State.Locomotion);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.SlideEnded));
            h.Step(); // tick 59 = 20 + 39
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            Assert.AreEqual(59L, h.EventsOf(RunnerEventType.SlideEnded)[0].Tick);
        }

        [Test]
        public void AC24_JumpDuringSlideJumpsSameTickWithStandingHitbox()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Slide);
            h.RunThrough(9);
            RunnerState s = h.Step(InputCommand.Jump); // tick 10

            Assert.AreEqual(Locomotion.Airborne, s.Locomotion);
            Assert.AreEqual(1.8f, s.HitboxHeight);

            List<RunnerEvent> ended = h.EventsOf(RunnerEventType.SlideEnded);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(10L, ended[0].Tick);
            Assert.AreEqual((short)SlideEndReason.Jump, ended[0].Value);

            RunnerEvent jump = h.EventsOf(RunnerEventType.JumpStarted)[0];
            Assert.AreEqual(10L, jump.Tick);
            Assert.IsTrue(jump.HasFlag(RunnerEventFlags.FromSlide));

            h.RunThrough(46);
            Assert.AreEqual(46L, h.EventsOf(RunnerEventType.Landed)[0].Tick);
        }

        [Test]
        public void AC25_SlideExtendsWhileStandingIsBlocked_ThenStandsUp()
        {
            // Constant 10 m/s: Z after tick k is (k + 1) / 6. The timer runs out on tick 39 (Z = 6.67).
            var headroom = new BlockedHeadroom(5.0, 9.0);
            var h = RunnerTestHarness.ConstantSpeed(10.0, null, headroom);
            h.Step(InputCommand.Slide);

            long standTick = -1;
            for (int tick = 1; tick < 120 && standTick < 0; tick++)
            {
                RunnerState s = h.Step();
                Assert.IsFalse(s.IsDead);
                if (s.Locomotion == Locomotion.Running)
                {
                    standTick = s.Tick;
                    Assert.GreaterOrEqual(s.Z - 0.25, 9.0, "stands only once the standing box is clear");
                }
                else
                {
                    Assert.AreEqual(Locomotion.Sliding, s.Locomotion, "tick " + tick);
                }
            }

            Assert.Greater(standTick, 39L, "the slide must have been extended");
            Assert.AreEqual(55L, standTick, "first tick with Z - 0.25 >= 9.0");
            Assert.AreEqual(1, h.CountOf(RunnerEventType.SlideEnded));
            Assert.AreEqual(standTick, h.EventsOf(RunnerEventType.SlideEnded)[0].Tick);
        }

        [Test]
        public void SlideOffALedgeEndsTheSlideAndOpensCoyoteTime()
        {
            var track = new TestTrackQuery().AddGap(10.0, 20.0);
            var h = RunnerTestHarness.ConstantSpeed(10.0, track);
            h.RunThrough(49);
            h.Step(InputCommand.Slide); // tick 50, Z = 8.5
            while (h.State.Locomotion == Locomotion.Sliding)
            {
                h.Step();
            }

            Assert.AreEqual(Locomotion.Coyote, h.State.Locomotion);
            RunnerEvent ended = h.EventsOf(RunnerEventType.SlideEnded)[0];
            Assert.AreEqual((short)SlideEndReason.Ledge, ended.Value);
            Assert.IsTrue(h.EventsOf(RunnerEventType.LeftGround)[0].HasFlag(RunnerEventFlags.Coyote));
        }
    }
}
