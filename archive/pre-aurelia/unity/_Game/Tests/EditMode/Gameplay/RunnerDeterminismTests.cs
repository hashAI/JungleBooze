using System.IO;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 001 section 13, determinism (AC-62, AC-63).</summary>
    public sealed class RunnerDeterminismTests
    {
        private const int Ticks = 3600;

        /// <summary>Busy random player: about one command every eight ticks, any flag combination.</summary>
        private sealed class SeededRandomInput : IInputProvider
        {
            private readonly IRandom _random;

            public SeededRandomInput(ulong seed)
            {
                _random = new Pcg32Random(seed);
            }

            public InputCommand ReadCommands(long tick)
            {
                return _random.Chance(0.125f) ? (InputCommand)_random.NextInt(1, 64) : InputCommand.None;
            }
        }

        private static ulong[] RunHeadless(ulong seed)
        {
            var sim = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            var input = new SeededRandomInput(seed);
            var hashes = new ulong[Ticks];
            for (int tick = 0; tick < Ticks; tick++)
            {
                sim.Step(input);
                sim.Events.Clear();
                hashes[tick] = sim.ComputeStateHash();
            }

            return hashes;
        }

        private static ulong[] RunWithFramePacing(ulong seed, double[] frameTimes)
        {
            var sim = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            var input = new SeededRandomInput(seed);
            var time = new FixedStepTimeSource(RunnerConfig.TicksPerSecond, maxStepsPerFrame: 8);
            var hashes = new ulong[Ticks];
            int frame = 0;
            while (time.Tick < Ticks)
            {
                int steps = time.Accumulate(frameTimes[frame % frameTimes.Length]);
                frame++;
                for (int i = 0; i < steps && time.Tick < Ticks; i++)
                {
                    Assert.AreEqual(time.Tick, sim.NextTick);
                    sim.Step(input);
                    hashes[time.Tick] = sim.ComputeStateHash();
                    time.Step();
                }

                // Presentation would read the events here, once per frame.
                sim.Events.Clear();
            }

            return hashes;
        }

        [Test]
        public void AC62_SameSeedAndStreamGiveIdenticalHashesOverTenRuns()
        {
            ulong[] first = RunHeadless(20261007UL);
            for (int run = 1; run < 10; run++)
            {
                CollectionAssert.AreEqual(first, RunHeadless(20261007UL), "run " + run);
            }

            CollectionAssert.AreNotEqual(first, RunHeadless(42UL), "a different stream must give a different run");
        }

        [Test]
        public void AC62_FramePacingDoesNotChangeTheRun()
        {
            ulong[] headless = RunHeadless(99UL);
            CollectionAssert.AreEqual(headless, RunWithFramePacing(99UL, new[] { 1.0 / 30.0 }), "30 fps");
            CollectionAssert.AreEqual(headless, RunWithFramePacing(99UL, new[] { 1.0 / 60.0 }), "60 fps");
            CollectionAssert.AreEqual(headless, RunWithFramePacing(99UL, new[] { 1.0 / 120.0 }), "120 fps");
            CollectionAssert.AreEqual(
                headless,
                RunWithFramePacing(99UL, new[] { 0.004, 0.031, 0.016, 0.050, 0.001, 0.022, 0.012 }),
                "jittery");
        }

        [Test]
        public void AC63_RunnerSimulationSourceUsesNoEngineTimeRandomOrPhysics()
        {
            string folder = Path.Combine(Application.dataPath, "_Game", "Scripts", "Gameplay", "Runner");
            string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 5, "runner sources not found in " + folder);

            string[] banned =
            {
                "UnityEngine",
                "System.Random",
                "new Random(",
                "Time.deltaTime",
                "Time.time",
                "Time.fixedDeltaTime",
                "Physics.",
                "Physics2D.",
                "DateTime.Now",
                "Guid.NewGuid",
            };

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = lines[i];
                    int comment = code.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0)
                    {
                        code = code.Substring(0, comment);
                    }

                    foreach (string token in banned)
                    {
                        Assert.IsFalse(
                            code.Contains(token),
                            Path.GetFileName(file) + " line " + (i + 1) + " uses banned API '" + token + "'.");
                    }
                }
            }
        }
    }
}
