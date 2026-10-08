using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>AC-101-03 and record → replay (ADR 0002/0006).</summary>
    public sealed class DeterminismTests
    {
        internal static ulong Fingerprint(in RunnerState s)
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, s.Tick);
            h = Mix(h, BitConverter.SingleToInt32Bits(s.S));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.X));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.Y));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.XTarget));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.VLat));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.Vy));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.Speed));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.Distance));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.DodgeOriginX));
            h = Mix(h, BitConverter.SingleToInt32Bits(s.RegenProgress));
            h = Mix(h, (s.Grounded ? 1 : 0) | (s.Sliding ? 2 : 0) | (s.FastFalling ? 4 : 0) | (s.Jumped ? 8 : 0) | (s.Dead ? 16 : 0) | (s.Finished ? 32 : 0));
            h = Mix(h, s.Health);
            h = Mix(h, s.Hits);
            h = Mix(h, s.Coins);
            h = Mix(h, s.DroppedInputs);
            h = Mix(h, s.SlideEndTick);
            h = Mix(h, s.InvulnerableUntilTick);
            h = Mix(h, (long)s.Buffered);
            return h;
        }

        private static ulong Mix(ulong h, long value)
        {
            unchecked
            {
                h ^= (ulong)value;
                h *= 1099511628211UL;
                return h;
            }
        }

        /// <summary>A busy random player: steering most ticks, an action every ~0.5 s.</summary>
        private static InputFrame[] RandomInputs(ulong seed, int ticks)
        {
            var random = new Pcg32Random(seed).Fork(RandomStreamIds.Bot);
            var frames = new InputFrame[ticks];
            for (int i = 0; i < ticks; i++)
            {
                InputCommand command = InputCommand.None;
                if (random.Chance(0.03f))
                {
                    command = (InputCommand)(1 << random.NextInt(0, 4));
                    if (random.Chance(0.3f))
                    {
                        command |= InputCommand.TouchBegan;
                    }
                }

                frames[i] = new InputFrame(command, (short)random.NextInt(-80, 81));
            }

            return frames;
        }

        private static CourseData OpenCourse()
        {
            CourseData c = SimRig.Path(3.5f);
            for (int i = 0; i < 40; i++)
            {
                // Walkable logs and small steps so vertical code paths run; nothing that can kill.
                float s = 40f + (i * 37f);
                SimRig.Low(c, s, s + 1f, 0.9f, -10f, 10f, true);
                SimRig.Floor(c, s + 15f, s + 20f, 0.3f);
            }

            return c;
        }

        private static ulong RunPaced(InputFrame[] inputs, ulong pacingSeed, int maxStepsPerFrame)
        {
            var path = new CoursePath(OpenCourse());
            var sim = new RunnerSimulation(SpecConfig.Create(), path, 1f / 60f, new RunEventBuffer(256));
            sim.Reset(new RunOptions { FirstRun = true });
            var pacing = new Pcg32Random(pacingSeed);
            int tick = 0;
            while (tick < inputs.Length)
            {
                // One rendered frame runs 1–N ticks.
                int steps = pacing.NextInt(1, maxStepsPerFrame + 1);
                for (int i = 0; i < steps && tick < inputs.Length; i++)
                {
                    sim.Step(inputs[tick++]);
                }

                sim.Events.Clear();
            }

            return Fingerprint(sim.State);
        }

        [Test]
        public void AC03_SameInputsSameStateAfter3600Ticks_AnyFramePacing()
        {
            InputFrame[] inputs = RandomInputs(20261009UL, 3600);
            ulong a = RunPaced(inputs, 1UL, 1);
            ulong b = RunPaced(inputs, 1UL, 1);
            ulong c = RunPaced(inputs, 7UL, 5);
            ulong d = RunPaced(inputs, 99UL, 3);
            Assert.AreEqual(a, b, "run twice");
            Assert.AreEqual(a, c, "1–5 ticks per frame");
            Assert.AreEqual(a, d);
            Assert.AreNotEqual(a, RunPaced(RandomInputs(5UL, 3600), 1UL, 1), "different inputs differ");
        }

        [Test]
        public void AC03_FixedStepClockWithJitteryFramesGivesSameRun()
        {
            InputFrame[] inputs = RandomInputs(42UL, 3600);
            ulong reference = RunPaced(inputs, 1UL, 1);
            var path = new CoursePath(OpenCourse());
            var time = new FixedStepTimeSource(60, 5);
            var sim = new RunnerSimulation(SpecConfig.Create(), path, time.DeltaTime, new RunEventBuffer(256));
            sim.Reset(new RunOptions { FirstRun = true });
            double[] frameTimes = { 0.004, 0.031, 0.016, 0.050, 0.001, 0.022, 0.012, 0.0083 };
            int frame = 0;
            while (time.Tick < inputs.Length)
            {
                int steps = time.Accumulate(frameTimes[frame++ % frameTimes.Length]);
                for (int i = 0; i < steps && time.Tick < inputs.Length; i++)
                {
                    sim.Step(inputs[time.Tick]);
                    time.Step();
                }

                sim.Events.Clear();
            }

            Assert.AreEqual(reference, Fingerprint(sim.State));
        }

        [Test]
        public void RecordedBotRunOnFeelCourse_ReplaysBitIdentical()
        {
            MovementConfig config = ShippedAssets.Config();
            var path = new CoursePath(ShippedAssets.Course());
            var time = new FixedStepTimeSource(60, 5);
            var live = new RunSession(config, path, time, new RunEventBuffer(256));
            live.Restart(RunOptions.Default);
            var bot = new PerfectBot(live.Simulation, false);
            var recording = new InputRecording(123UL);
            var recorder = new RecordingInputProvider(bot, recording);
            while (live.Phase != RunPhase.Results && live.SessionTick < 60 * 120)
            {
                live.Step(recorder.ReadInput(live.SessionTick));
                live.Simulation.Events.Clear();
            }

            Assert.IsTrue(live.Finished, "bot finished the course");
            Assert.Greater(recording.Count, 50);

            var replaySession = new RunSession(config, path, time, new RunEventBuffer(256));
            replaySession.Restart(RunOptions.Default);
            var replay = new ReplayInputProvider(recording);
            var pacing = new Pcg32Random(3UL);
            while (replaySession.Phase != RunPhase.Results && replaySession.SessionTick < 60 * 120)
            {
                int steps = pacing.NextInt(1, 6);
                for (int i = 0; i < steps && replaySession.Phase != RunPhase.Results; i++)
                {
                    replaySession.Step(replay.ReadInput(replaySession.SessionTick));
                }

                replaySession.Simulation.Events.Clear();
            }

            Assert.AreEqual(Fingerprint(live.Simulation.State), Fingerprint(replaySession.Simulation.State));
            Assert.AreEqual(live.SessionTick, replaySession.SessionTick);
        }
    }
}
