using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.PowerUps;
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
    public sealed class TrackRunWorld : IRunWorld, IRunWorldSummary, IRunnerStepHooks, IRunWorldRecovery, ICompanionWorld
    {
        /// <summary>Respawn search step and range for a Continue over a gap or chasm (m).</summary>
        private const double RespawnSearchStepM = 0.25;
        private const double RespawnSearchRangeM = 160.0;

        /// <summary>Extra distance past the far edge of a gap before the respawn point [ASSUMED] (m).</summary>
        private const double RespawnEdgeMarginM = 1.5;

        /// <summary>The clear stretch starts this far behind the respawn point (removes the obstacle that killed HERO).</summary>
        private const double ClearBehindM = 1.5;

        /// <summary>Range searched behind / ahead of a vine-chasm death for the section's chasm vines (m).</summary>
        private const double ChasmVineLookBackM = 60.0;
        private const double ChasmVineLookAheadM = 80.0;

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

        /// <summary>Active power-ups and their pickups (GDD 10). Null until <see cref="CreateRunner"/>.</summary>
        public PowerUpSystem PowerUps { get; private set; }

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

        /// <summary>Score of this run (<see cref="IRunWorldSummary"/>).</summary>
        public long Score => Totals.Score;

        public string DescribeDeath(DeathCause cause, ObstacleArchetype archetype, bool afterStumble)
        {
            return _setup.Skin.GetCauseText(cause, archetype, afterStumble);
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
                config,
                _setup.Vines,
                _setup.PowerUps,
                _setup.Hazards);
            PowerUps = new PowerUpSystem(_setup.PowerUps, config);
            Scoring = new RunScoring(_setup.Coins, _setup.Score, config, _setup.Vines);
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
            IRandom trackStream = RootRandom.Fork(RandomStreamIds.TrackGeneration);
            IRandom vineStream = _setup.Vines.Enabled ? RootRandom.Fork(RandomStreamIds.VineSchedule) : null;
            // GDD 10: power-up pickups on their own stream, forked after the vine stream (track and vines unchanged).
            IRandom pickupStream = _setup.PowerUps.Enabled ? RootRandom.Fork(RandomStreamIds.Pickups) : null;
            Track.Reset(trackStream, vineStream, pickupStream);
            PowerUps.Reset();
            PowerUps.Bind(Track);
            Scoring.Reset();
            Runner = new RunnerSimulation(_runnerConfig, _curve, Track, null, _setup.Vines)
            {
                StepHooks = this,
                ContactHooks = PowerUps,
            };

            return Runner;
        }

        public void AfterRunnerStep(long tick, RunnerSimulation runner)
        {
            // Track, coins and score run inside the step through IRunnerStepHooks (spec 13.3 order).
        }

        public void OnTrackUpdate(RunnerSimulation runner, in RunnerTickInfo info)
        {
            // Before generation, so a live Speed Boost can hold the next vine section (GDD 7.2 / 10).
            PowerUps.HoldVineSections();
            Track.Update(info, runner);
        }

        public void OnCoinPickups(RunnerSimulation runner, in RunnerTickInfo info)
        {
            if (info.VineRelease != JungleBooze.Gameplay.Vine.VineReleaseGrade.None)
            {
                // GDD 7.3 step 4: the coin shower / Perfect ring along the launch arc, before this tick's pickups.
                Track.SpawnVineBonusCoins(info.VineRelease, runner);
            }

            // GDD 10: power-up pickups, then the Magnet / Speed Boost coin pull, before the coin pickups.
            PowerUps.OnCoinPickups(info, runner);
            Scoring.OnCoinPickups(info, Track, runner);

            // GDD 15.1: during Lift the companion pulls in coins from all lanes ahead.
            if (runner.IsLifted && LiftCoinPullAheadM > 0.0)
            {
                Scoring.CollectAhead(info, Track, runner, LiftCoinPullAheadM);
            }
        }

        /// <summary>Lift coin pull range (<see cref="ICompanionWorld"/>); 0 until the session sets it.</summary>
        public double LiftCoinPullAheadM { get; set; }

        /// <summary>
        /// Continue (GDD 14.4, <see cref="IRunWorldRecovery"/>): respawn where HERO died if there is ground, else on the
        /// first ground ahead (after a "Missed vine" death: past the last chasm vine of that section, on the landing
        /// pad); remove the killer and clear the stretch ahead; then revive the runner and forget the death.
        /// </summary>
        public bool Revive(RunnerSimulation runner, int invulnerableTicks, double clearSeconds)
        {
            if (runner == null || Track == null || !runner.Current.IsDead)
            {
                return false;
            }

            RunnerState state = runner.Current;
            int lane = state.TargetLane;
            double scanFrom = state.Z;
            if (runner.DeathCause == DeathCause.MissedVine)
            {
                scanFrom = LastChasmVineZ(state.Z, scanFrom);
            }

            double depth = runner.Config.PlayerHitboxDepthM;
            double ground = Track.FindGroundAhead(lane, scanFrom, depth, RespawnSearchStepM, RespawnSearchRangeM);
            double respawnZ = scanFrom;
            bool overGap = runner.DeathCause != DeathCause.Hit || ground > scanFrom + 1e-6;
            if (overGap)
            {
                double edge = Track.FindGroundAhead(lane, ground + depth, depth, RespawnSearchStepM, RespawnSearchRangeM);
                respawnZ = edge + depth + RespawnEdgeMarginM;
            }

            double clearM = SpeedAt(respawnZ) * (clearSeconds > 0.0 ? clearSeconds : 0.0);
            ClearStretch(respawnZ - ClearBehindM, respawnZ + clearM);
            if (!runner.Revive(respawnZ, lane, invulnerableTicks))
            {
                return false;
            }

            Scoring.ClearDeath();
            return true;
        }

        public int ClearStretch(double fromZ, double toZ)
        {
            return Track == null ? 0 : Track.ClearObstacles(fromZ, toZ);
        }

        public double SpeedAt(double z)
        {
            return _curve == null ? 0.0 : _curve.Evaluate(z);
        }

        /// <summary>Z of the farthest chasm vine of the vine section around <paramref name="deathZ"/> (or <paramref name="fallback"/>).</summary>
        private double LastChasmVineZ(double deathZ, double fallback)
        {
            int group = -1;
            double nearest = double.PositiveInfinity;
            int count = Track.VineCount;
            for (int i = 0; i < count; i++)
            {
                ref readonly VineInstance v = ref Track.GetVine(i);
                if (!v.OverChasm || v.Z < deathZ - ChasmVineLookBackM || v.Z > deathZ + ChasmVineLookAheadM)
                {
                    continue;
                }

                double d = Math.Abs(v.Z - deathZ);
                if (d < nearest)
                {
                    nearest = d;
                    group = v.ChunkSerial;
                }
            }

            double last = fallback;
            for (int i = 0; i < count; i++)
            {
                ref readonly VineInstance v = ref Track.GetVine(i);
                if (v.ChunkSerial == group && v.OverChasm && v.Z > last)
                {
                    last = v.Z;
                }
            }

            return last;
        }

        public void OnScore(RunnerSimulation runner, in RunnerTickInfo info)
        {
            Scoring.OnScore(info, Track, runner);

            // GDD 10: power-up timers and effects for the next tick (speed, invulnerability, gap auto-jump).
            PowerUps.OnScore(info, runner);
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

            if (PowerUps != null)
            {
                h = PowerUps.ComputeStateHash(h);
            }

            return h;
        }
    }
}
