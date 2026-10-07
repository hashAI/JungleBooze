using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// The generated-track world of one run (spec 002): plugs into <see cref="GameSession"/> exactly like
    /// <see cref="FlatRunWorld"/>, through <see cref="TrackRunWorldFactory"/>. It builds the runner on the
    /// <see cref="TrackSimulation"/> (ground, gaps, obstacle boxes; headroom derived by the runner from the boxes)
    /// and registers itself as the runner's <see cref="IRunnerStepHooks"/>, so the track update (7a), coin
    /// pickups (11a) and score (11b) run inside <see cref="RunnerSimulation.Step(InputCommand)"/> in spec order.
    /// <see cref="AfterRunnerStep"/> has nothing left to do.
    /// <para>Random streams: the root generator is seeded with the run seed and forked once with
    /// <see cref="RandomStreamIds.TrackGeneration"/> at setup; the generator is the only user of that stream.</para>
    /// </summary>
    public sealed class TrackRunWorld : IRunWorld, IRunnerStepHooks
    {
        private readonly TrackRunSetup _setup;
        private RunnerConfig _runnerConfig;
        private SpeedCurve _curve;

        public TrackRunWorld(TrackRunSetup setup, ulong seed)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Seed = seed;
        }

        public ulong Seed { get; private set; }

        public TrackRunSetup Setup => _setup;

        /// <summary>The track (rings, ground, boxes). Null until <see cref="CreateRunner"/>.</summary>
        public TrackSimulation Track { get; private set; }

        /// <summary>Coins, streak and score. Null until <see cref="CreateRunner"/>.</summary>
        public RunScoring Scoring { get; private set; }

        /// <summary>The runner built by <see cref="CreateRunner"/>.</summary>
        public RunnerSimulation Runner { get; private set; }

        /// <summary>The root random generator of the run (other subsystems fork their own streams from it).</summary>
        public IRandom RootRandom { get; private set; }

        public int Coins => Scoring == null ? 0 : Scoring.Totals.Coins;

        /// <summary>HUD and Game Over values (spec 13.2).</summary>
        public RunTotals Totals => Scoring == null ? default : Scoring.Totals;

        /// <summary>How the run ended (Game Over cause text through <see cref="TrackRunSetup.Skin"/>).</summary>
        public DeathInfo Death => Scoring == null ? default : Scoring.Death;

        /// <summary>Game Over cause text for this run's death (spec 12.4); empty while alive. Does not allocate.</summary>
        public string DeathCauseText
        {
            get
            {
                DeathInfo d = Death;
                return d.HasDied ? _setup.Skin.GetCauseText(d) : string.Empty;
            }
        }

        public RunnerSimulation CreateRunner(RunnerConfig config, SpeedCurve speedCurve)
        {
            _runnerConfig = config ?? throw new ArgumentNullException(nameof(config));
            _curve = speedCurve ?? throw new ArgumentNullException(nameof(speedCurve));
            Track = new TrackSimulation(
                _setup.Track,
                _setup.Kit,
                _setup.Coins,
                _setup.Library,
                _setup.GetTiers(speedCurve),
                speedCurve,
                config);
            Scoring = new RunScoring(_setup.Coins, _setup.Score, config);
            ResetRun(Seed);
            return Runner;
        }

        /// <summary>
        /// Rebuilds the run in place with <paramref name="seed"/> (no new rings): new runner, re-seeded root
        /// random and forks, generator state, rings and id counters, totals (spec 12.3 "resets" column).
        /// Use the same seed for "Same track". Requires a prior <see cref="CreateRunner"/>.
        /// </summary>
        public RunnerSimulation ResetRun(ulong seed)
        {
            if (Track == null)
            {
                throw new InvalidOperationException("Call CreateRunner first.");
            }

            Seed = seed;
            RootRandom = new Pcg32Random(seed);
            Track.Reset(RootRandom.Fork(RandomStreamIds.TrackGeneration));
            Scoring.Reset();
            Runner = new RunnerSimulation(_runnerConfig, _curve, Track)
            {
                StepHooks = this,
            };

            return Runner;
        }

        public void AfterRunnerStep(long tick, RunnerSimulation runner)
        {
            // Track, coins and score run inside the step through IRunnerStepHooks (spec 13.3 order).
        }

        public void OnTrackUpdate(RunnerSimulation runner, in RunnerTickInfo info)
        {
            Track.Update(info, runner);
        }

        public void OnCoinPickups(RunnerSimulation runner, in RunnerTickInfo info)
        {
            Scoring.OnCoinPickups(info, Track, runner);
        }

        public void OnScore(RunnerSimulation runner, in RunnerTickInfo info)
        {
            Scoring.OnScore(info, Track, runner);
        }

        /// <summary>Hash of runner, track and scoring state (AC-241, AC-247). Allocation-free.</summary>
        public ulong ComputeStateHash()
        {
            ulong h = StableHash.Seed;
            if (Runner != null)
            {
                h = StableHash.Mix(h, Runner.ComputeStateHash());
            }

            if (Track != null)
            {
                h = StableHash.Mix(h, Track.ComputeStateHash());
            }

            if (Scoring != null)
            {
                h = Scoring.ComputeStateHash(h);
            }

            return h;
        }
    }
}
