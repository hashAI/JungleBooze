using System;
using JungleBooze.Core;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    public sealed class InputReplayTests
    {
        private sealed class RandomInputProvider : IInputProvider
        {
            private readonly IRandom _random;

            public RandomInputProvider(IRandom random)
            {
                _random = random;
            }

            public InputCommand ReadCommands(long tick)
            {
                // Roughly one command every ten ticks, like a busy player.
                return _random.Chance(0.1f)
                    ? (InputCommand)(1 << _random.NextInt(0, 5))
                    : InputCommand.None;
            }
        }

        [Test]
        public void Replay_ReturnsExactlyWhatWasRecorded()
        {
            const int ticks = 5000;
            var recording = new InputRecording(seed: 42UL);
            var recorder = new RecordingInputProvider(new RandomInputProvider(new Pcg32Random(9UL)), recording);

            var live = new InputCommand[ticks];
            for (int tick = 0; tick < ticks; tick++)
            {
                live[tick] = recorder.ReadCommands(tick);
            }

            var replay = new ReplayInputProvider(recording);
            for (int tick = 0; tick < ticks; tick++)
            {
                Assert.AreEqual(live[tick], replay.ReadCommands(tick), $"Mismatch at tick {tick}.");
            }

            Assert.IsTrue(replay.IsFinished);
            Assert.AreEqual(42UL, recording.Seed);
        }

        [Test]
        public void Recording_SkipsEmptyTicks()
        {
            var recording = new InputRecording(1UL);
            recording.Add(0, InputCommand.None);
            recording.Add(3, InputCommand.Jump);
            recording.Add(4, InputCommand.None);

            Assert.AreEqual(1, recording.Count);
            Assert.AreEqual(3L, recording[0].Tick);
        }

        [Test]
        public void Recording_RejectsNonIncreasingTicks()
        {
            var recording = new InputRecording(1UL);
            recording.Add(5, InputCommand.Jump);
            Assert.Throws<ArgumentException>(() => recording.Add(5, InputCommand.Slide));
            Assert.Throws<ArgumentException>(() => recording.Add(4, InputCommand.Slide));
        }

        [Test]
        public void Replay_CombinedFlagsSurviveRoundTrip()
        {
            var recording = new InputRecording(1UL);
            recording.Add(10, InputCommand.MoveLeft | InputCommand.Jump);

            var replay = new ReplayInputProvider(recording);
            Assert.AreEqual(InputCommand.None, replay.ReadCommands(9));
            Assert.AreEqual(InputCommand.MoveLeft | InputCommand.Jump, replay.ReadCommands(10));
            Assert.AreEqual(InputCommand.None, replay.ReadCommands(11));
        }
    }
}
