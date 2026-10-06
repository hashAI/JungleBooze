using JungleBooze.Core;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// End-to-end proof of the determinism contract with a toy simulation that uses all three
    /// Core seams: IRandom, ITimeSource and IInputProvider. Real gameplay systems follow the same pattern.
    /// </summary>
    public sealed class DeterministicSimulationTests
    {
        private sealed class ToySimulation
        {
            private readonly IRandom _random;
            private readonly ITimeSource _time;
            private readonly IInputProvider _input;

            public ToySimulation(IRandom random, ITimeSource time, IInputProvider input)
            {
                _random = random;
                _time = time;
                _input = input;
            }

            public int Lane { get; private set; }

            public float Distance { get; private set; }

            public int Coins { get; private set; }

            public void Step()
            {
                InputCommand commands = _input.ReadCommands(_time.Tick);
                if ((commands & InputCommand.MoveLeft) != 0 && Lane > -1)
                {
                    Lane--;
                }

                if ((commands & InputCommand.MoveRight) != 0 && Lane < 1)
                {
                    Lane++;
                }

                Distance += 10f * _time.DeltaTime;
                if (_random.NextInt(-1, 2) == Lane && _random.Chance(0.05f))
                {
                    Coins++;
                }
            }
        }

        private sealed class ScriptedBot : IInputProvider
        {
            private readonly IRandom _random;

            public ScriptedBot(IRandom random)
            {
                _random = random;
            }

            public InputCommand ReadCommands(long tick)
            {
                if (tick % 30 != 0)
                {
                    return InputCommand.None;
                }

                return _random.Chance(0.5f) ? InputCommand.MoveLeft : InputCommand.MoveRight;
            }
        }

        private struct Outcome
        {
            public long Tick;
            public int Lane;
            public float Distance;
            public int Coins;
        }

        private static Outcome RunWithFramePacing(ulong seed, int targetTicks, double[] frameTimes, IInputProvider input = null)
        {
            var root = new Pcg32Random(seed);
            IRandom simRandom = root.Fork(1UL);
            IRandom botRandom = root.Fork(2UL);
            var time = new FixedStepTimeSource(60, maxStepsPerFrame: 8);
            var sim = new ToySimulation(simRandom, time, input ?? new ScriptedBot(botRandom));

            int frame = 0;
            while (time.Tick < targetTicks)
            {
                int steps = time.Accumulate(frameTimes[frame % frameTimes.Length]);
                frame++;
                for (int i = 0; i < steps && time.Tick < targetTicks; i++)
                {
                    sim.Step();
                    time.Step();
                }
            }

            return new Outcome { Tick = time.Tick, Lane = sim.Lane, Distance = sim.Distance, Coins = sim.Coins };
        }

        private static readonly double[] SmoothFrames = { 1.0 / 60.0 };
        private static readonly double[] JitteryFrames = { 0.004, 0.031, 0.016, 0.050, 0.001, 0.022, 0.012 };
        private static readonly double[] SlowFrames = { 1.0 / 30.0 };

        [Test]
        public void SameSeed_SameResult_RegardlessOfFrameRate()
        {
            const ulong seed = 20261006UL;
            const int ticks = 60 * 120; // two simulated minutes

            Outcome smooth = RunWithFramePacing(seed, ticks, SmoothFrames);
            Outcome jittery = RunWithFramePacing(seed, ticks, JitteryFrames);
            Outcome slow = RunWithFramePacing(seed, ticks, SlowFrames);

            Assert.AreEqual(smooth.Tick, jittery.Tick);
            Assert.AreEqual(smooth.Lane, jittery.Lane);
            Assert.AreEqual(smooth.Distance, jittery.Distance);
            Assert.AreEqual(smooth.Coins, jittery.Coins);

            Assert.AreEqual(smooth.Lane, slow.Lane);
            Assert.AreEqual(smooth.Distance, slow.Distance);
            Assert.AreEqual(smooth.Coins, slow.Coins);
        }

        [Test]
        public void DifferentSeeds_DifferentResult()
        {
            const int ticks = 60 * 120;
            Outcome a = RunWithFramePacing(1UL, ticks, SmoothFrames);
            Outcome b = RunWithFramePacing(2UL, ticks, SmoothFrames);

            Assert.AreNotEqual(a.Coins, b.Coins);
        }

        [Test]
        public void RecordedRun_ReplaysToIdenticalResult()
        {
            const ulong seed = 555UL;
            const int ticks = 60 * 60;

            var botRandom = new Pcg32Random(seed).Fork(2UL);
            var recording = new InputRecording(seed);
            var recorder = new RecordingInputProvider(new ScriptedBot(botRandom), recording);

            Outcome live = RunWithFramePacing(seed, ticks, JitteryFrames, recorder);
            Outcome replayed = RunWithFramePacing(recording.Seed, ticks, SmoothFrames, new ReplayInputProvider(recording));

            Assert.AreEqual(live.Lane, replayed.Lane);
            Assert.AreEqual(live.Distance, replayed.Distance);
            Assert.AreEqual(live.Coins, replayed.Coins);
        }
    }
}
