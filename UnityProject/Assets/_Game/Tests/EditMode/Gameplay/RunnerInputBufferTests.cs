using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 7: input buffer and same-tick rules (AC-26 to AC-29).</summary>
    public sealed class RunnerInputBufferTests
    {
        private const InputCommand MovementFlags =
            InputCommand.MoveLeft | InputCommand.MoveRight | InputCommand.Jump | InputCommand.Slide;

        [Test]
        public void AC26_JumpNineTicksBeforeLandingFiresOnLanding()
        {
            var h = new RunnerTestHarness();
            Assert.AreEqual(9, h.Config.InputBufferTicks);
            h.Step(InputCommand.Jump); // lands on tick 36
            h.RunThrough(26);
            h.Step(InputCommand.Jump); // tick 27 = 36 - 9
            Assert.AreEqual(CommandOutcome.Buffered, h.Sim.LastOutcomes.Jump);
            h.RunThrough(36);

            List<RunnerEvent> jumps = h.EventsOf(RunnerEventType.JumpStarted);
            Assert.AreEqual(2, jumps.Count);
            Assert.AreEqual(36L, jumps[1].Tick);
            Assert.IsTrue(jumps[1].HasFlag(RunnerEventFlags.Buffered));
            Assert.AreEqual(Locomotion.Airborne, h.State.Locomotion);
            Assert.AreEqual(0, h.Sim.Outcomes.Get(CommandOutcome.Buffered), "the buffered jump resolved");
            Assert.AreEqual(2, h.Sim.Outcomes.Get(CommandOutcome.Executed));
        }

        [Test]
        public void AC26_JumpTenTicksBeforeLandingExpires()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(25);
            h.Step(InputCommand.Jump); // tick 26 = 36 - 10
            h.RunThrough(40);

            Assert.AreEqual(1, h.CountOf(RunnerEventType.JumpStarted));
            Assert.AreEqual(1, h.Sim.Outcomes.Get(CommandOutcome.Expired));
            Assert.AreEqual(Locomotion.Running, h.State.Locomotion);
            Assert.IsFalse(h.Sim.HasBufferedJump);
        }

        [Test]
        public void AC27_NewerBufferedJumpReplacesOlder()
        {
            var h = new RunnerTestHarness();
            h.Step(InputCommand.Jump);
            h.RunThrough(23);
            h.Step(InputCommand.Jump); // tick 24 = 36 - 12 (would expire on 34)
            h.RunThrough(30);
            h.Step(InputCommand.Jump); // tick 31 = 36 - 5
            h.RunThrough(36);

            List<RunnerEvent> jumps = h.EventsOf(RunnerEventType.JumpStarted);
            Assert.AreEqual(2, jumps.Count);
            Assert.AreEqual(36L, jumps[1].Tick);
            Assert.IsTrue(jumps[1].HasFlag(RunnerEventFlags.Buffered));
            Assert.AreEqual(1, h.Sim.Outcomes.Get(CommandOutcome.Superseded));
            Assert.AreEqual(0, h.Sim.Outcomes.Get(CommandOutcome.Expired));
        }

        [Test]
        public void AC28_LeftAndRightInOneTickCancelEachOther()
        {
            var h = new RunnerTestHarness();
            RunnerState s = h.Step(InputCommand.MoveLeft | InputCommand.MoveRight);

            Assert.AreEqual(0f, s.X);
            Assert.AreEqual(CommandOutcome.Cancelled, h.Sim.LastOutcomes.MoveLeft);
            Assert.AreEqual(CommandOutcome.Cancelled, h.Sim.LastOutcomes.MoveRight);
            Assert.AreEqual(2, h.Sim.Outcomes.Get(CommandOutcome.Cancelled));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.LaneChangeStarted));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.LaneBlocked));
        }

        [Test]
        public void AC28_JumpAndSlideInOneTickJumps_SlideSuperseded()
        {
            var h = new RunnerTestHarness();
            RunnerState s = h.Step(InputCommand.Jump | InputCommand.Slide);

            Assert.AreEqual(Locomotion.Airborne, s.Locomotion);
            Assert.AreEqual(CommandOutcome.Executed, h.Sim.LastOutcomes.Jump);
            Assert.AreEqual(CommandOutcome.Superseded, h.Sim.LastOutcomes.Slide);
            Assert.AreEqual(0, h.CountOf(RunnerEventType.SlideStarted));
            Assert.AreEqual(0, h.CountOf(RunnerEventType.FastFallStarted));
        }

        [TestCase(20261007UL, false)]
        [TestCase(77UL, true)]
        public void AC29_RandomCommandStreamHasNoSilentDrops(ulong seed, bool withGaps)
        {
            var random = new Pcg32Random(seed);
            ITrackQuery track = null;
            if (withGaps)
            {
                // A long gap well down the track, so the run also covers falling and death.
                track = new TestTrackQuery().AddGap(600.0, 700.0);
            }

            var h = new RunnerTestHarness(null, null, track);
            int received = 0;
            for (int tick = 0; tick < 10000; tick++)
            {
                InputCommand commands = InputCommand.None;
                if (random.Chance(0.3f))
                {
                    commands = (InputCommand)random.NextInt(0, 64);
                }

                received += CountBits((int)(commands & MovementFlags));
                h.Step(commands);

                TickOutcomes o = h.Sim.LastOutcomes;
                AssertHasOutcome(commands, InputCommand.MoveLeft, o.MoveLeft, tick);
                AssertHasOutcome(commands, InputCommand.MoveRight, o.MoveRight, tick);
                AssertHasOutcome(commands, InputCommand.Jump, o.Jump, tick);
                AssertHasOutcome(commands, InputCommand.Slide, o.Slide, tick);
                Assert.AreEqual(received, h.Sim.Outcomes.Total, "outcome sum at tick " + tick);
                Assert.AreEqual(received, h.Sim.Outcomes.CommandsReceived, "received at tick " + tick);
            }

            Assert.Greater(received, 1000);
            Assert.AreEqual(withGaps, h.State.IsDead);
        }

        private static void AssertHasOutcome(InputCommand commands, InputCommand flag, CommandOutcome outcome, int tick)
        {
            if ((commands & flag) != 0)
            {
                Assert.AreNotEqual(CommandOutcome.None, outcome, flag + " at tick " + tick);
            }
            else
            {
                Assert.AreEqual(CommandOutcome.None, outcome, flag + " at tick " + tick);
            }
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }
}
