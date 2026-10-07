using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Owns one play session: the fixed-step clock, the current run's simulations (runner + world), the run seed,
    /// pause / resume countdown, death timing and Restart (spec 001 sections 8 and 10.6, ARCHITECTURE section 4).
    /// Plain C#: the run driver feeds it real frame time; EditMode tests can drive it directly.
    /// No allocations in <see cref="Advance"/>; <see cref="Restart"/> allocates (new run scope).
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>Maximum simulation steps per rendered frame (ARCHITECTURE 5.2).</summary>
        public const int MaxStepsPerFrame = 5;

        /// <summary>Stream id for the seed generator (fixed, never renumber).</summary>
        private const ulong SeedStreamId = 0x5EED5EEDUL;

        private readonly RunnerConfig _config;
        private readonly SpeedCurve _speedCurve;
        private readonly IInputProvider _input;
        private readonly IRunWorldFactory _worldFactory;
        private readonly Pcg32Random _seedSource;
        private readonly FixedStepTimeSource _time;

        private InputCommand _pendingFlags;
        private double _countdownLeft;
        private double _deathElapsed;

        public GameSession(
            RunnerConfig config,
            SpeedCurve speedCurve,
            IInputProvider input,
            IRunWorldFactory worldFactory,
            SessionTimings timings,
            ulong sessionSeed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _speedCurve = speedCurve ?? throw new ArgumentNullException(nameof(speedCurve));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _worldFactory = worldFactory ?? throw new ArgumentNullException(nameof(worldFactory));
            Timings = timings;
            _seedSource = new Pcg32Random(sessionSeed, SeedStreamId);
            _time = new FixedStepTimeSource(RunnerConfig.TicksPerSecond, MaxStepsPerFrame);
            StartNewRun();
        }

        public RunnerConfig Config => _config;

        public SessionTimings Timings { get; }

        /// <summary>The current run's movement simulation. Replaced on <see cref="Restart"/>.</summary>
        public RunnerSimulation Runner { get; private set; }

        /// <summary>The current run's world (track, obstacles, coins). Replaced on <see cref="Restart"/>.</summary>
        public IRunWorld World { get; private set; }

        public ITimeSource Time => _time;

        /// <summary>Seed of the current run.</summary>
        public ulong RunSeed { get; private set; }

        /// <summary>1 for the first run, +1 per <see cref="Restart"/>.</summary>
        public int RunNumber { get; private set; }

        public SessionPhase Phase { get; private set; }

        /// <summary>Real seconds left in the resume countdown (0 outside <see cref="SessionPhase.Countdown"/>).</summary>
        public double CountdownSecondsLeft => Phase == SessionPhase.Countdown ? _countdownLeft : 0.0;

        /// <summary>Real seconds since <c>Died</c> (meaningful in <see cref="SessionPhase.Dying"/>).</summary>
        public double DeathElapsedSeconds => _deathElapsed;

        /// <summary>True during the first part of <see cref="SessionPhase.Dying"/> where views stay frozen.</summary>
        public bool InHitPause => Phase == SessionPhase.Dying && _deathElapsed < Timings.HitPauseSeconds;

        /// <summary>Distance run in m (HUD).</summary>
        public double DistanceM => Runner.Current.Z;

        /// <summary>Coins collected in this run (HUD).</summary>
        public int Coins => World.Coins;

        /// <summary>Interpolation factor for views. Constant while nothing steps, so views hold still.</summary>
        public float InterpolationAlpha
        {
            get
            {
                float a = _time.InterpolationAlpha;
                return a < 0f ? 0f : (a > 1f ? 1f : a);
            }
        }

        /// <summary>Commands passed to the last simulation step (tests and debug overlay).</summary>
        public InputCommand LastStepCommands { get; private set; }

        /// <summary>Simulation steps run by the last <see cref="Advance"/> call.</summary>
        public int LastFrameSteps { get; private set; }

        /// <summary>
        /// Advances the session by one rendered frame of real time. Steps the simulation only while
        /// <see cref="SessionPhase.Running"/>. Returns the number of simulation steps run.
        /// </summary>
        public int Advance(double realDeltaSeconds)
        {
            if (double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds) || realDeltaSeconds < 0.0)
            {
                realDeltaSeconds = 0.0;
            }

            int stepsRun = 0;
            switch (Phase)
            {
                case SessionPhase.Running:
                    stepsRun = RunSteps(_time.Accumulate(realDeltaSeconds));
                    break;

                case SessionPhase.Countdown:
                    _countdownLeft -= realDeltaSeconds;
                    if (_countdownLeft <= 0.0)
                    {
                        // Spec 8.4: no catch-up steps; spec 8.5: the next tick carries PauseResumed.
                        _countdownLeft = 0.0;
                        _time.ClearAccumulator();
                        _pendingFlags |= InputCommand.PauseResumed;
                        Phase = SessionPhase.Running;
                    }

                    break;

                case SessionPhase.Dying:
                    _deathElapsed += realDeltaSeconds;
                    if (_deathElapsed >= Timings.HitPauseSeconds + Timings.DeathHoldSeconds)
                    {
                        Phase = SessionPhase.GameOver;
                    }

                    break;
            }

            LastFrameSteps = stepsRun;
            return stepsRun;
        }

        /// <summary>Pause button, P/Escape, app background or focus loss. Works while running or counting down.</summary>
        public bool RequestPause()
        {
            if (Phase != SessionPhase.Running && Phase != SessionPhase.Countdown)
            {
                return false;
            }

            // Spec 8.6: pausing during the countdown stops it; resuming starts it again from 3.
            _countdownLeft = 0.0;
            Phase = SessionPhase.Paused;
            return true;
        }

        /// <summary>Resume button: starts the 3-2-1 countdown (never auto-resumes, spec 8.3).</summary>
        public bool RequestResume()
        {
            if (Phase != SessionPhase.Paused)
            {
                return false;
            }

            _countdownLeft = Timings.ResumeCountdownSeconds;
            Phase = SessionPhase.Countdown;
            if (_countdownLeft <= 0.0)
            {
                Advance(0.0);
            }

            return true;
        }

        /// <summary>Keyboard pause toggle: pause while running/counting down, resume while paused.</summary>
        public bool TogglePause()
        {
            return Phase == SessionPhase.Paused ? RequestResume() : RequestPause();
        }

        /// <summary>
        /// Starts a new run with a new seed. Allowed from any phase (the HUD only offers it on Game Over).
        /// Allocates the new run scope.
        /// </summary>
        public void Restart()
        {
            StartNewRun();
        }

        /// <summary>
        /// Development aid until collisions exist: ends the run as if HERO died, without touching the simulation.
        /// The run driver only calls this in development builds.
        /// </summary>
        public bool DebugEndRun()
        {
            if (Phase == SessionPhase.Dying || Phase == SessionPhase.GameOver)
            {
                return false;
            }

            _deathElapsed = 0.0;
            Phase = SessionPhase.Dying;
            return true;
        }

        private int RunSteps(int due)
        {
            int run = 0;
            for (int i = 0; i < due; i++)
            {
                long tick = Runner.NextTick;
                InputCommand commands = _input.ReadCommands(tick) | _pendingFlags;
                _pendingFlags = InputCommand.None;
                LastStepCommands = commands;

                Runner.Step(commands);
                World.AfterRunnerStep(tick, Runner);
                _time.Step();
                run++;

                if (Runner.Current.IsDead)
                {
                    _deathElapsed = 0.0;
                    Phase = SessionPhase.Dying;
                    break;
                }
            }

            return run;
        }

        private void StartNewRun()
        {
            RunSeed = ((ulong)_seedSource.NextUInt() << 32) | _seedSource.NextUInt();
            World = _worldFactory.Create(RunSeed, _config)
                ?? throw new InvalidOperationException("The world factory returned null.");
            Runner = World.CreateRunner(_config, _speedCurve)
                ?? throw new InvalidOperationException("The world returned a null runner.");

            _time.Reset();
            _pendingFlags = InputCommand.None;
            _countdownLeft = 0.0;
            _deathElapsed = 0.0;
            LastStepCommands = InputCommand.None;
            LastFrameSteps = 0;
            RunNumber++;
            Phase = SessionPhase.Running;
        }
    }
}
