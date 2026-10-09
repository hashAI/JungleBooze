using System;
using System.IO;
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

            public InputFrame ReadInput(long tick)
            {
                // Roughly one command every ten ticks plus steering most ticks, like a busy player.
                InputCommand command = _random.Chance(0.1f) ? (InputCommand)(1 << _random.NextInt(0, 5)) : InputCommand.None;
                short lateral = _random.Chance(0.6f) ? (short)_random.NextInt(-120, 121) : (short)0;
                return new InputFrame(command, lateral);
            }
        }

        [Test]
        public void Replay_ReturnsExactlyWhatWasRecorded()
        {
            const int ticks = 5000;
            var recording = new InputRecording(seed: 42UL);
            var recorder = new RecordingInputProvider(new RandomInputProvider(new Pcg32Random(9UL)), recording);

            var live = new InputFrame[ticks];
            for (int tick = 0; tick < ticks; tick++)
            {
                live[tick] = recorder.ReadInput(tick);
            }

            var replay = new ReplayInputProvider(recording);
            for (int tick = 0; tick < ticks; tick++)
            {
                Assert.AreEqual(live[tick], replay.ReadInput(tick), $"Mismatch at tick {tick}.");
            }

            Assert.IsTrue(replay.IsFinished);
            Assert.AreEqual(42UL, recording.Seed);
        }

        [Test]
        public void Recording_SkipsEmptyTicks()
        {
            var recording = new InputRecording(1UL);
            recording.Add(0, InputFrame.Empty);
            recording.Add(3, InputFrame.FromCommands(InputCommand.Jump));
            recording.Add(4, InputFrame.Empty);
            recording.Add(5, new InputFrame(InputCommand.None, 7));

            Assert.AreEqual(2, recording.Count);
            Assert.AreEqual(3L, recording[0].Tick);
            Assert.AreEqual((short)7, recording[1].Frame.LateralDeltaMm);
        }

        [Test]
        public void Recording_RejectsNonIncreasingTicks()
        {
            var recording = new InputRecording(1UL);
            recording.Add(5, InputFrame.FromCommands(InputCommand.Jump));
            Assert.Throws<ArgumentException>(() => recording.Add(5, InputFrame.FromCommands(InputCommand.Slide)));
            Assert.Throws<ArgumentException>(() => recording.Add(4, InputFrame.FromCommands(InputCommand.Slide)));
        }

        [Test]
        public void Replay_CombinedFlagsAndDragSurviveRoundTrip()
        {
            var recording = new InputRecording(1UL);
            recording.Add(10, new InputFrame(InputCommand.TouchBegan | InputCommand.DodgeLeft, -321));

            var replay = new ReplayInputProvider(recording);
            Assert.AreEqual(InputFrame.Empty, replay.ReadInput(9));
            Assert.AreEqual(new InputFrame(InputCommand.TouchBegan | InputCommand.DodgeLeft, -321), replay.ReadInput(10));
            Assert.AreEqual(InputFrame.Empty, replay.ReadInput(11));
        }

        [Test]
        public void Serializer_RoundTripsHeaderAndFrames()
        {
            var recording = new InputRecording(77UL, 0xABCDEF0123UL, "1.2.3 (45)");
            var source = new RandomInputProvider(new Pcg32Random(3UL));
            for (int tick = 0; tick < 2000; tick++)
            {
                recording.Add(tick, source.ReadInput(tick));
            }

            var stream = new MemoryStream();
            InputRecordingSerializer.Write(recording, stream);
            stream.Position = 0;
            InputRecording loaded = InputRecordingSerializer.Read(stream);

            Assert.AreEqual(InputRecording.CurrentFormatVersion, loaded.FormatVersion);
            Assert.AreEqual(3, InputRecording.CurrentFormatVersion, "ADR 0006 amendment: expedition replays are format 3.");
            Assert.AreEqual(77UL, loaded.Seed);
            Assert.AreEqual(0xABCDEF0123UL, loaded.ConfigHash);
            Assert.AreEqual("1.2.3 (45)", loaded.BuildVersion);
            Assert.AreEqual(recording.Count, loaded.Count);
            for (int i = 0; i < recording.Count; i++)
            {
                Assert.AreEqual(recording[i].Tick, loaded[i].Tick);
                Assert.AreEqual(recording[i].Frame, loaded[i].Frame);
            }
        }

        [Test]
        public void Serializer_RejectsOtherFormatVersions()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(new[] { (byte)'J', (byte)'B', (byte)'R', (byte)'P' });
            writer.Write(1); // lane-era format
            writer.Write(0UL);
            writer.Flush();
            stream.Position = 0;
            Assert.Throws<NotSupportedException>(() => InputRecordingSerializer.Read(stream));
        }

        [Test]
        public void Discrete_PicksOneAction()
        {
            Assert.AreEqual(InputCommand.Jump, InputCommands.Discrete(InputCommand.Jump | InputCommand.TouchBegan));
            Assert.AreEqual(InputCommand.None, InputCommands.Discrete(InputCommand.TouchBegan));
            Assert.AreEqual(InputCommand.DodgeRight, InputCommands.Discrete(InputCommand.DodgeRight));
        }
    }
}
