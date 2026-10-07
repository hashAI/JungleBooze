using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// FP1 run lifecycle (spec 002 section 12): <c>Ready → Running → Dying → GameOver</c>, then Run again (new
    /// seed) or Same track (same seed) straight back to <c>Running</c>. Plain C# driven by real time
    /// (<see cref="Advance"/>) so EditMode tests can use a fake clock. It decides states and seeds and keeps the
    /// session bests; the run driver owns the simulation and asks this object whether to step it and whether to
    /// pass an input on. No allocation.
    /// <para>Driver contract:</para>
    /// <list type="number">
    /// <item><see cref="BeginSession"/> → <c>Ready</c> with <see cref="Seed"/>: build the run with that seed.</item>
    /// <item>Every player input: call <see cref="OnPlayerInput"/>; if it returns true the input is consumed (the
    /// first input in Ready only starts the run; input in Dying and GameOver is ignored) and must not reach the
    /// simulation.</item>
    /// <item>Step the simulation only while <see cref="ShouldStepSimulation"/>.</item>
    /// <item>On <c>Died</c>: <see cref="OnRunnerDied"/>. Every frame: <see cref="Advance"/> with real time.</item>
    /// <item>Game Over buttons: <see cref="TryRestart"/>; on true rebuild the run with <see cref="Seed"/> (the run
    /// scope resets as in spec 12.3; views go back to their pools; camera snaps).</item>
    /// </list>
    /// </summary>
    public sealed class RunSession
    {
        private readonly RunFlowConfig _flow;
        private readonly IRunSeedSource _seeds;
        private double _elapsed;

        public RunSession(RunFlowConfig flow, IRunSeedSource seeds)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        }

        public RunFlowConfig Flow => _flow;

        public RunLifecycleState State { get; private set; }

        /// <summary>Seed of the current run (also the "last seed" Same track reuses).</summary>
        public ulong Seed { get; private set; }

        /// <summary>0 before <see cref="BeginSession"/>, then 1, +1 per restart.</summary>
        public int RunNumber { get; private set; }

        /// <summary>Real seconds since the current state was entered (Dying and GameOver timing).</summary>
        public double StateElapsedSeconds => _elapsed;

        /// <summary>Session best distance (memory only in FP1). Only rises.</summary>
        public double BestDistanceM { get; private set; }

        /// <summary>Session best score (memory only in FP1). Only rises.</summary>
        public long BestScore { get; private set; }

        /// <summary>The simulation steps only in <see cref="RunLifecycleState.Running"/>.</summary>
        public bool ShouldStepSimulation => State == RunLifecycleState.Running;

        /// <summary>The pause button exists only while running (no pause in Ready, Dying or GameOver).</summary>
        public bool CanPause => State == RunLifecycleState.Running;

        /// <summary>Game Over buttons accept input once <c>gameOverInputLockMs</c> has passed (spec 12.1).</summary>
        public bool GameOverButtonsEnabled =>
            State == RunLifecycleState.GameOver && _elapsed >= _flow.GameOverInputLockSeconds;

        /// <summary>
        /// Starts the session: <c>Ready</c> with a new seed (or <c>Running</c> right away when
        /// <c>startOnFirstInput</c> is off).
        /// </summary>
        public void BeginSession()
        {
            Seed = PickSeed(RestartMode.RunAgain);
            RunNumber = 1;
            _elapsed = 0.0;
            State = _flow.StartOnFirstInput ? RunLifecycleState.Ready : RunLifecycleState.Running;
        }

        /// <summary>
        /// A tap, swipe or key. Returns true when the input is consumed by the lifecycle and must not be passed to
        /// the simulation: the first input in Ready (it only starts the run, [ASSUMED] spec 12.1), and every input
        /// in Dying and GameOver (Game Over buttons go through <see cref="TryRestart"/>).
        /// </summary>
        public bool OnPlayerInput()
        {
            switch (State)
            {
                case RunLifecycleState.Ready:
                    State = RunLifecycleState.Running;
                    _elapsed = 0.0;
                    return true;
                case RunLifecycleState.Running:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>The runner emitted <c>Died</c>: freeze the simulation and record the session bests.</summary>
        public bool OnRunnerDied(double distanceM, long score)
        {
            if (State != RunLifecycleState.Running)
            {
                return false;
            }

            if (distanceM > BestDistanceM)
            {
                BestDistanceM = distanceM;
            }

            if (score > BestScore)
            {
                BestScore = score;
            }

            State = RunLifecycleState.Dying;
            _elapsed = 0.0;
            return true;
        }

        /// <summary>Advances real time (s). Dying turns into GameOver after hit-pause + camera hold (1,150 ms).</summary>
        public void Advance(double realDeltaSeconds)
        {
            if (double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds) || realDeltaSeconds < 0.0)
            {
                realDeltaSeconds = 0.0;
            }

            switch (State)
            {
                case RunLifecycleState.Dying:
                    _elapsed += realDeltaSeconds;
                    if (_elapsed >= _flow.DyingDurationSeconds - 1e-9)
                    {
                        EnterGameOver();
                    }

                    break;
                case RunLifecycleState.GameOver:
                    _elapsed += realDeltaSeconds;
                    break;
            }
        }

        /// <summary>The app came back from the background. During Dying, Game Over shows directly (spec 12.1).</summary>
        public void OnAppResumed()
        {
            if (State == RunLifecycleState.Dying)
            {
                EnterGameOver();
            }
        }

        /// <summary>
        /// A Game Over button. Ignored (false) outside GameOver or during the input lock. On true the new run's
        /// <see cref="Seed"/> is set (new for Run again, the same for Same track) and the state is
        /// <c>Running</c>: the press is the "tap to run" (spec 12.2).
        /// </summary>
        public bool TryRestart(RestartMode mode)
        {
            if (!GameOverButtonsEnabled)
            {
                return false;
            }

            Seed = PickSeed(mode);
            RunNumber++;
            _elapsed = 0.0;
            State = RunLifecycleState.Running;
            return true;
        }

        private void EnterGameOver()
        {
            State = RunLifecycleState.GameOver;
            _elapsed = 0.0;
        }

        private ulong PickSeed(RestartMode mode)
        {
            if (_flow.DevFixedSeed != 0UL)
            {
                return _flow.DevFixedSeed;
            }

            if (mode == RestartMode.SameTrack && Seed != 0UL)
            {
                return Seed;
            }

            return _seeds.NextSeed();
        }
    }
}
