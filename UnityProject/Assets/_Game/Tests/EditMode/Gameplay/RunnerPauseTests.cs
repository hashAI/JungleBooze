using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 8: pause (AC-45, AC-46). AC-47 to AC-49 are PlayMode tests (driver and touch).</summary>
    public sealed class RunnerPauseTests
    {
        [Test]
        public void AC45_PauseResumedClearsBufferAndQueue_ActiveMoveAndArcContinue()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.StartLane = 0;

            // A: jump, lane move, queued move, buffered jump, then resume from pause on tick 4.
            var paused = new RunnerTestHarness(values.Clone());
            BotInputProvider scriptA = new BotInputProvider()
                .At(0, InputCommand.Jump)
                .At(1, InputCommand.MoveRight)
                .At(2, InputCommand.MoveRight)
                .At(3, InputCommand.Jump)
                .At(4, InputCommand.PauseResumed);

            // B: the same run without the commands the resume should clear.
            var reference = new RunnerTestHarness(values.Clone());
            BotInputProvider scriptB = new BotInputProvider()
                .At(0, InputCommand.Jump)
                .At(1, InputCommand.MoveRight);

            for (int tick = 0; tick < 80; tick++)
            {
                RunnerState a = paused.Step(scriptA.ReadCommands(tick));
                RunnerState b = reference.Step(scriptB.ReadCommands(tick));
                if (tick == 3)
                {
                    Assert.AreEqual(1, paused.Sim.QueuedLateral);
                    Assert.IsTrue(paused.Sim.HasBufferedJump);
                }

                if (tick >= 4)
                {
                    AssertSameMotion(b, a, tick);
                }
            }

            Assert.AreEqual(0, paused.Sim.QueuedLateral);
            Assert.IsFalse(paused.Sim.HasBufferedJump);
            Assert.AreEqual(2, paused.Sim.Outcomes.Get(CommandOutcome.Invalidated));
            Assert.AreEqual(paused.Sim.Outcomes.CommandsReceived, paused.Sim.Outcomes.Total);
            Assert.AreEqual(RunnerTestHarness.LaneX(1), paused.State.X, "the active move finished, the queued one never started");
        }

        [Test]
        public void AC45_SlideTimerContinuesAfterResume()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Slide);
            h.RunThrough(9);
            int before = h.State.SlideTicksLeft;
            h.Step(InputCommand.PauseResumed); // tick 10
            Assert.AreEqual(before - 1, h.State.SlideTicksLeft);

            h.RunThrough(38);
            Assert.AreEqual(Locomotion.Sliding, h.State.Locomotion);
            h.Step();
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
        }

        [Test]
        public void AC45_CoyoteCounterContinuesAfterResume()
        {
            var h = RunnerTestHarness.ConstantSpeed(10.0, new TestTrackQuery().AddGap(10.0, 200.0));
            while (h.CountOf(RunnerEventType.LeftGround) == 0)
            {
                h.Step();
            }

            long e = h.LastTick;
            h.Step(); // e + 1
            Assert.AreEqual(4, h.Sim.CoyoteTicksLeft);
            h.Step(InputCommand.PauseResumed); // e + 2
            Assert.AreEqual(3, h.Sim.CoyoteTicksLeft);
            h.RunThrough(e + 4);
            Assert.AreEqual(Locomotion.Coyote, h.State.Locomotion);
            h.Step(); // e + 5
            Assert.AreEqual(Locomotion.Falling, h.State.Locomotion);
        }

        [Test]
        public void AC46_ReplayWithPauseReproducesEveryStateHash()
        {
            const int ticks = 900;
            BotInputProvider bot = new BotInputProvider()
                .At(5, InputCommand.MoveLeft)
                .At(6, InputCommand.MoveRight)
                .At(30, InputCommand.Jump)
                .At(40, InputCommand.Jump)
                .At(41, InputCommand.PauseResumed)
                .At(90, InputCommand.Slide)
                .At(100, InputCommand.MoveRight | InputCommand.PauseResumed)
                .At(101, InputCommand.MoveRight)
                .At(200, InputCommand.Jump)
                .At(219, InputCommand.Slide)
                .At(220, InputCommand.Jump)
                .At(221, InputCommand.PauseResumed)
                .At(500, InputCommand.CompanionAssist)
                .At(600, InputCommand.MoveLeft | InputCommand.Jump);

            var recording = new InputRecording(seed: 1UL);
            var recorder = new RecordingInputProvider(bot, recording);
            var live = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            var hashes = new ulong[ticks];
            for (int tick = 0; tick < ticks; tick++)
            {
                live.Step(recorder);
                live.Events.Clear();
                hashes[tick] = live.ComputeStateHash();
            }

            var replay = new ReplayInputProvider(recording);
            var replayed = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            for (int tick = 0; tick < ticks; tick++)
            {
                replayed.Step(replay);
                replayed.Events.Clear();
                Assert.AreEqual(hashes[tick], replayed.ComputeStateHash(), "hash at tick " + tick);
            }

            Assert.IsTrue(replay.IsFinished);
        }

        private static void AssertSameMotion(RunnerState expected, RunnerState actual, int tick)
        {
            Assert.AreEqual(expected.X, actual.X, "X at tick " + tick);
            Assert.AreEqual(expected.Y, actual.Y, "Y at tick " + tick);
            Assert.AreEqual(expected.Z, actual.Z, "Z at tick " + tick);
            Assert.AreEqual(expected.Locomotion, actual.Locomotion, "locomotion at tick " + tick);
            Assert.AreEqual(expected.TargetLane, actual.TargetLane, "target lane at tick " + tick);
            Assert.AreEqual(expected.LaneMoveProgress, actual.LaneMoveProgress, "lane progress at tick " + tick);
            Assert.AreEqual(expected.JumpPhase, actual.JumpPhase, "jump phase at tick " + tick);
        }
    }
}
