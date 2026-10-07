using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 5: lanes (AC-06 to AC-14).</summary>
    public sealed class RunnerLaneTests
    {
        private static RunnerTestHarness StartInLane(int lane)
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.StartLane = lane;
            return new RunnerTestHarness(values);
        }

        [Test]
        public void AC06_LaneSwitchStartsSameTickAndArrivesOnSeventhTick_NoOvershoot()
        {
            var h = StartInLane(1);
            Assert.AreEqual(7, h.Config.LaneSwitchTicks);

            var xs = new List<float>();
            xs.Add(h.Step(InputCommand.MoveRight).X);
            for (int i = 1; i < 20; i++)
            {
                xs.Add(h.Step().X);
            }

            Assert.Greater(xs[0], 0f, "X must change on the tick the command is processed");
            for (int tick = 0; tick < 6; tick++)
            {
                Assert.Less(xs[tick], 2.4f, "tick " + tick);
                if (tick > 0)
                {
                    Assert.Greater(xs[tick], xs[tick - 1], "tick " + tick);
                }
            }

            Assert.AreEqual(2.4f, xs[6], "arrives exactly on tick 6 (the 7th tick of the move)");
            for (int tick = 0; tick < xs.Count; tick++)
            {
                Assert.LessOrEqual(xs[tick], 2.4f, "no overshoot at tick " + tick);
            }

            Assert.AreEqual(2, h.State.TargetLane);
            Assert.AreEqual(2, h.State.OccupiedLane);
        }

        [Test]
        public void AC07_LaneSwitchSevenTicks_EaseOutProgress()
        {
            var h = StartInLane(1);
            float p1 = h.Step(InputCommand.MoveRight).X / 2.4f;
            float p2 = h.Step().X / 2.4f;
            float p3 = h.Step().X / 2.4f;

            Assert.AreEqual(0.265f, p1, 0.001f);
            Assert.AreEqual(0.490f, p2, 0.001f);
            Assert.AreEqual(0.673f, p3, 0.001f);
        }

        [Test]
        public void AC08_DoubleSwipeStartsSecondMoveOnTickFourAndArrivesOnTickTen()
        {
            var h = StartInLane(0);
            h.Step(InputCommand.MoveRight);
            h.Step(InputCommand.MoveRight);
            Assert.AreEqual(CommandOutcome.Queued, h.Sim.LastOutcomes.MoveRight);

            h.RunThrough(3);
            float xAtTick3 = h.State.X;
            Assert.Less(xAtTick3, RunnerTestHarness.LaneX(1), "HERO has not reached lane 1 yet");

            h.Step(); // tick 4
            List<RunnerEvent> starts = h.EventsOf(RunnerEventType.LaneChangeStarted);
            Assert.AreEqual(2, starts.Count);
            Assert.AreEqual(4L, starts[1].Tick);
            Assert.IsTrue(starts[1].HasFlag(RunnerEventFlags.Queued));
            Assert.AreEqual(2, (int)starts[1].Lane);
            Assert.AreEqual(1, (int)starts[1].Value, "from lane");

            // The second move starts from the current X: first change of 26.5% of the remaining distance.
            float expected = xAtTick3 + (RunnerTestHarness.LaneX(2) - xAtTick3) * (13f / 49f);
            Assert.AreEqual(expected, h.State.X, 1e-4f);

            h.RunThrough(9);
            Assert.Less(h.State.X, RunnerTestHarness.LaneX(2));
            h.Step(); // tick 10
            Assert.AreEqual(RunnerTestHarness.LaneX(2), h.State.X);
            Assert.AreEqual(2, h.Sim.Outcomes.Get(CommandOutcome.Executed), "queued move resolved to Executed");
            Assert.AreEqual(0, h.Sim.Outcomes.Get(CommandOutcome.Queued));
        }

        [Test]
        public void AC09_SameDirectionSwipeOnTickFiveStartsImmediately()
        {
            var h = StartInLane(0);
            h.Step(InputCommand.MoveRight);
            h.RunThrough(4);
            h.Step(InputCommand.MoveRight); // tick 5

            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.MoveRight);
            List<RunnerEvent> starts = h.EventsOf(RunnerEventType.LaneChangeStarted);
            Assert.AreEqual(2, starts.Count);
            Assert.AreEqual(5L, starts[1].Tick);
            Assert.IsFalse(starts[1].HasFlag(RunnerEventFlags.Queued));

            h.RunThrough(10);
            Assert.Less(h.State.X, RunnerTestHarness.LaneX(2));
            h.Step(); // tick 11
            Assert.AreEqual(RunnerTestHarness.LaneX(2), h.State.X);
        }

        [Test]
        public void AC10_OppositeSwipeReversesFromCurrentXToOriginLane()
        {
            var h = StartInLane(1);
            h.Step(InputCommand.MoveRight);
            h.Step();
            float xBefore = h.Step().X; // tick 2
            float xReverse = h.Step(InputCommand.MoveLeft).X; // tick 3

            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.MoveLeft);
            Assert.Less(xReverse, xBefore, "reversal moves back on the same tick");
            List<RunnerEvent> starts = h.EventsOf(RunnerEventType.LaneChangeStarted);
            Assert.AreEqual(2, starts.Count);
            Assert.IsTrue(starts[1].HasFlag(RunnerEventFlags.Reversal));
            Assert.AreEqual(3L, starts[1].Tick);
            Assert.AreEqual(1, (int)starts[1].Lane);
            Assert.AreEqual(-1, (int)starts[1].Dir);
            Assert.AreEqual(1, h.State.TargetLane);

            h.RunThrough(8);
            Assert.Greater(h.State.X, 0f);
            h.Step(); // tick 9 = 3 + 6, the 7th tick of the reversal
            Assert.AreEqual(0f, h.State.X);
        }

        [Test]
        public void AC11_OppositeSwipeWhileQueuedCancelsOnlyTheQueue()
        {
            var h = StartInLane(0);
            h.Step(InputCommand.MoveRight);
            h.Step(InputCommand.MoveRight); // queued
            h.Step(InputCommand.MoveLeft); // cancels the queue

            Assert.AreEqual(1, h.CountOf(RunnerEventType.LaneChangeCancelled));
            Assert.AreEqual(1, (int)h.EventsOf(RunnerEventType.LaneChangeCancelled)[0].Dir);
            Assert.AreEqual(0, h.Sim.QueuedLateral);
            Assert.AreEqual(1, h.Sim.Outcomes.Get(CommandOutcome.Cancelled));

            h.RunThrough(30);
            Assert.AreEqual(RunnerTestHarness.LaneX(1), h.State.X);
            Assert.AreEqual(1, h.State.TargetLane);
            Assert.AreEqual(1, h.CountOf(RunnerEventType.LaneChangeStarted));
        }

        [TestCase(0, InputCommand.MoveLeft, -1)]
        [TestCase(2, InputCommand.MoveRight, 1)]
        public void AC12_EdgeLaneBumpDoesNotMoveAndNeverFiresLater(int lane, InputCommand command, int dir)
        {
            var h = StartInLane(lane);
            float x0 = RunnerTestHarness.LaneX(lane);
            h.Step(command);

            Assert.AreEqual(CommandOutcome.Bumped, h.Sim.LastOutcomes.MoveLeft == CommandOutcome.None
                ? h.Sim.LastOutcomes.MoveRight
                : h.Sim.LastOutcomes.MoveLeft);
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(x0, h.Step().X);
            }

            List<RunnerEvent> blocked = h.EventsOf(RunnerEventType.LaneBlocked);
            Assert.AreEqual(1, blocked.Count);
            Assert.AreEqual(dir, (int)blocked[0].Dir);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.LaneChangeStarted));
            Assert.AreEqual(1, h.Sim.Outcomes.Get(CommandOutcome.Bumped));
        }

        [Test]
        public void AC13_ThirdSameDirectionSwipeIsBumped()
        {
            var h = StartInLane(0);
            h.Step(InputCommand.MoveRight);
            h.Step(InputCommand.MoveRight);
            h.Step(InputCommand.MoveRight);

            Assert.AreEqual(CommandOutcome.Bumped, h.Sim.LastOutcomes.MoveRight);
            Assert.AreEqual(1, h.CountOf(RunnerEventType.LaneBlocked));

            h.RunThrough(40);
            Assert.AreEqual(RunnerTestHarness.LaneX(2), h.State.X);
        }

        [Test]
        public void AC14_LaneMoveDuringJumpDoesNotChangeYOrLanding()
        {
            AssertVerticalUnchanged(
                new BotInputProvider().At(0, InputCommand.Jump),
                new BotInputProvider().At(0, InputCommand.Jump).At(5, InputCommand.MoveRight).At(20, InputCommand.MoveLeft),
                60);
        }

        [Test]
        public void AC14_LaneMoveDuringSlideDoesNotChangeSlideTimer()
        {
            AssertVerticalUnchanged(
                new BotInputProvider().At(0, InputCommand.Slide),
                new BotInputProvider().At(0, InputCommand.Slide).At(3, InputCommand.MoveLeft).At(15, InputCommand.MoveRight),
                60);
        }

        [Test]
        public void AC14_LaneMoveDuringFastFallDoesNotChangeYOrSlide()
        {
            AssertVerticalUnchanged(
                new BotInputProvider().At(0, InputCommand.Jump).At(12, InputCommand.Slide),
                new BotInputProvider().At(0, InputCommand.Jump).At(12, InputCommand.Slide).At(13, InputCommand.MoveRight),
                80);
        }

        private static void AssertVerticalUnchanged(BotInputProvider withoutMove, BotInputProvider withMove, int ticks)
        {
            var a = new RunnerTestHarness();
            var b = new RunnerTestHarness();
            for (int i = 0; i < ticks; i++)
            {
                RunnerState sa = a.Step(withoutMove.ReadCommands(i));
                RunnerState sb = b.Step(withMove.ReadCommands(i));
                Assert.AreEqual(sa.Y, sb.Y, "Y at tick " + i);
                Assert.AreEqual(sa.Locomotion, sb.Locomotion, "locomotion at tick " + i);
                Assert.AreEqual(sa.SlideTicksLeft, sb.SlideTicksLeft, "slide timer at tick " + i);
                Assert.AreEqual(sa.JumpPhase, sb.JumpPhase, "jump phase at tick " + i);
                Assert.AreEqual(sa.HitboxHeight, sb.HitboxHeight, "hitbox at tick " + i);
            }

            Assert.Greater(b.CountOf(RunnerEventType.LaneChangeStarted), 0, "the lane move must actually happen");
            Assert.AreEqual(a.EventsOf(RunnerEventType.Landed).Count, b.EventsOf(RunnerEventType.Landed).Count);
        }
    }
}
