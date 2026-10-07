using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Deterministic runner simulation: movement (spec 001 sections 4 to 8) and collisions (spec 001 section 9 with
    /// the spec 002 changes: final boxes from <see cref="ITrackQuery.GetBoxes"/>, relative-motion sweep for movers,
    /// stumble, daze, edge forgiveness, near-miss, invulnerability).
    /// One call to <see cref="Step(InputCommand)"/> = one 60 Hz tick, processed in the order of rule I8 as amended by
    /// spec 002 section 13.3 (hook points 7a, 11a, 11b in <see cref="IRunnerStepHooks"/>).
    /// Vine swinging (GDD 7): when the track also implements <see cref="IVineTrackQuery"/>, an airborne HERO who
    /// enters a vine's grab zone is <see cref="Locomotion.Carried"/> along the swing arc (safe from collisions);
    /// Jump releases (graded by swing phase), Move aims at the next vine, Slide is kept for the landing. The
    /// release is a launch on a ballistic arc (state <see cref="Locomotion.Falling"/> with the vine gravity),
    /// guided into the next vine of a chain. Steps 9a (grab and passed-vine check) runs after step 9.
    /// Plain C#: no engine time, engine randomness or physics; no allocations per step.
    /// </summary>
    public sealed class RunnerSimulation
    {
        /// <summary>Tolerance for "Y reached the surface" so float rounding never adds a tick to a fast-fall.</summary>
        private const double SurfaceEpsilonM = 1e-6;

        /// <summary>Boxes read from the track per query. HERO's swept window holds a handful; overflow is counted.</summary>
        private const int BoxBufferCapacity = 64;

        /// <summary>Obstacles tracked during HERO's pass (stumble ignore list and near-miss); overflow is counted.</summary>
        private const int EngagedCapacity = 32;

        /// <summary>Float tolerance for the edge-forgiveness threshold (1.44 m is not exact in float).</summary>
        private const double EdgeForgivenessToleranceM = 1e-5;

        /// <summary>Float tolerance for the near-miss threshold.</summary>
        private const double NearMissToleranceM = 1e-6;

        /// <summary>Vines read from the track per query.</summary>
        private const int VineBufferCapacity = 16;

        /// <summary>A chained launch shorter than this is not guided (the target is practically reached).</summary>
        private const double MinGuidedFlightS = 0.05;

        /// <summary>Range behind HERO searched for the vine of a chasm he fell into ("Missed vine").</summary>
        private const double MissedVineLookBackM = 40.0;

        /// <summary>Range ahead of HERO searched for the vine of a chasm he fell into.</summary>
        private const double MissedVineLookAheadM = 6.0;

        private const byte EngagedContact = 1;
        private const byte EngagedInvulnerable = 2;

        private readonly RunnerConfig _config;
        private readonly SpeedCurve _curve;
        private readonly ITrackQuery _track;
        private readonly IHeadroomQuery _headroom;
        private readonly VineConfig _vines;
        private readonly IVineTrackQuery _vineTrack;
        private readonly VineAnchor[] _vineBuffer = new VineAnchor[VineBufferCapacity];
        private readonly RunnerEventBuffer _events;
        private readonly CommandOutcomeCounters _counters = new CommandOutcomeCounters();

        private readonly ObstacleBox[] _boxes = new ObstacleBox[BoxBufferCapacity];
        private readonly int[] _contactBox = new int[BoxBufferCapacity];
        private readonly double[] _contactTime = new double[BoxBufferCapacity];
        private readonly ContactEntry[] _contactEntry = new ContactEntry[BoxBufferCapacity];
        private readonly bool[] _contactLateral = new bool[BoxBufferCapacity];

        private readonly int[] _engagedId = new int[EngagedCapacity];
        private readonly double[] _engagedMaxZ = new double[EngagedCapacity];
        private readonly double[] _engagedMinGap = new double[EngagedCapacity];
        private readonly byte[] _engagedFlags = new byte[EngagedCapacity];
        private readonly ObstacleArchetype[] _engagedArchetype = new ObstacleArchetype[EngagedCapacity];
        private int _engagedCount;

        private long _tick;
        private float _x;
        private double _y;
        private double _z;
        private double _speed;
        private Locomotion _locomotion;
        private int _targetLane;

        private bool _moveActive;
        private float _moveStartX;
        private int _moveEndLane;
        private int _moveDir;
        private int _moveElapsed;
        private int _queuedDir;

        private bool _bufferedJump;
        private long _bufferedJumpTick;

        private long _jumpStartTick;
        private long _airStartTick;
        private bool _slideOnLanding;

        private long _fastFallStartTick;
        private double _fastFallStartY;
        private double _fastFallSpeed;

        private long _fallStartTick;
        private double _fallStartY;
        private double _fallStartVelocity;

        private int _slideTicksLeft;
        private int _coyoteTicksLeft;
        private int _invulnerableTicks;
        private int _nextSpeedRow;
        private DeathCause _deathCause;
        private ObstacleArchetype _deathArchetype;
        private int _deathEntityId;
        private bool _deathAfterStumble;

        private bool _bounceActive;
        private float _bounceStartX;
        private int _bounceEndLane;
        private int _bounceElapsed;
        private int _dazeTicksLeft;

        private bool _stumbledThisTick;
        private int _nearMissesThisTick;

        private TickOutcomes _lastOutcomes;

        // ---- Vine state (GDD 7) ----
        private double _airStartZ;
        private double _fallGravity;
        private int _swingVineId;
        private int _swingGroup;
        private int _swingRow;
        private int _swingLane;
        private bool _swingOverChasm;
        private int _swingElapsed;
        private float _swingGrabX;
        private double _swingGrabY;
        private int _aimLane;
        private int _aimVineId;
        private double _aimVineZ;
        private bool _aimVineOverChasm;
        private bool _releaseBuffered;
        private long _releaseBufferedTick;
        private int _chainPerfects;
        private bool _launchedFromVine;
        private VineReleaseGrade _lastReleaseGrade;
        private VineReleaseGrade _releaseThisTick;
        private float _releaseMultiplierThisTick;
        private bool _grabbedThisTick;
        private bool _autoJumpRequested;
        private double _launchStartY;
        private double _launchVelocityY;
        private double _launchGravity;
        private double _launchSpeed;
        private double _launchStartZ;
        private double _launchFlightS;
        private int _launchLane;
        private double _vineScanZ;
        private int _grabbedGroup;
        private int _grabbedRow;
        private int _missedGroup;
        private int _missedRow;

        // ---- Companion Lift state (GDD 15.1) ----
        private LiftParameters _lift;
        private int _liftElapsed;
        private double _liftStartY;
        private bool _liftDescending;
        private int _liftDescentElapsed;
        private double _liftDescentStartY;

        public RunnerSimulation(
            RunnerConfig config,
            SpeedCurve speedCurve,
            ITrackQuery track = null,
            IHeadroomQuery headroom = null,
            VineConfig vines = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _curve = speedCurve ?? throw new ArgumentNullException(nameof(speedCurve));
            _track = track ?? FlatTrackQuery.Instance;

            // Null = derive headroom from the track's obstacle boxes (spec 001 6.4.5). Tests may override it.
            _headroom = headroom;
            _events = new RunnerEventBuffer(config.EventBufferCapacity);
            _vines = vines ?? VineConfig.CreateDefault();
            _vineTrack = _track as IVineTrackQuery;
            _fallGravity = config.GravityMps2;
            _vineScanZ = double.NegativeInfinity;
            _grabbedGroup = -1;
            _grabbedRow = -1;
            _missedGroup = -1;
            _missedRow = -1;

            _targetLane = config.StartLane;
            _x = config.LaneCenterX(config.StartLane);
            _locomotion = Locomotion.Running;
            SpeedMultiplier = 1.0;

            _nextSpeedRow = 0;
            while (_nextSpeedRow < _curve.RowCount && _curve.GetRowDistance(_nextSpeedRow) <= 0.0)
            {
                _nextSpeedRow++;
            }

            Current = BuildSnapshot(-1);
            Previous = Current;
        }

        public RunnerConfig Config => _config;

        /// <summary>The track this runner queries (ground, boxes; vines if it also implements <see cref="IVineTrackQuery"/>).</summary>
        public ITrackQuery Track => _track;

        /// <summary>HERO is carried by the companion's Lift (GDD 15.1).</summary>
        public bool IsLifted => _locomotion == Locomotion.Lifted;

        /// <summary>Ticks flown in the current Lift (0 when not lifted).</summary>
        public int LiftElapsedTicks => _locomotion == Locomotion.Lifted ? _liftElapsed : 0;

        /// <summary>The current Lift is in its descent.</summary>
        public bool IsLiftDescending => _locomotion == Locomotion.Lifted && _liftDescending;

        /// <summary>
        /// May a Lift start now? Not while dead, already lifted, on a vine (GDD 15.1: queued until landing) or below
        /// the track surface (falling into a gap).
        /// </summary>
        public bool CanStartLift =>
            _locomotion != Locomotion.Dead && _locomotion != Locomotion.Lifted && _locomotion != Locomotion.Carried && !(_y < 0.0);

        /// <summary>The tick the next <see cref="Step(InputCommand)"/> call will process.</summary>
        public long NextTick => _tick;

        /// <summary>State after the last step.</summary>
        public RunnerState Current { get; private set; }

        /// <summary>State after the step before the last one (for view interpolation).</summary>
        public RunnerState Previous { get; private set; }

        /// <summary>Events written by the simulation. The reader clears it after reading.</summary>
        public RunnerEventBuffer Events => _events;

        /// <summary>Outcome counters (rule I9).</summary>
        public CommandOutcomeCounters Outcomes => _counters;

        /// <summary>Per-flag outcomes of the last processed tick.</summary>
        public TickOutcomes LastOutcomes => _lastOutcomes;

        /// <summary>Hook for Speed Boost (spec 4.1.4). Default 1.</summary>
        public double SpeedMultiplier { get; set; }

        /// <summary>While true, the tutorial speed replaces the speed curve (spec 4.1.2). Set by onboarding.</summary>
        public bool TutorialActive { get; set; }

        /// <summary>
        /// Speed-source hook (spec 002 16.1.6). Null (default) = the speed curve, or its tutorial speed while
        /// <see cref="TutorialActive"/>. The run-start ramp and <see cref="SpeedMultiplier"/> still apply.
        /// </summary>
        public ISpeedSource SpeedSource { get; set; }

        /// <summary>Track, coin and score systems called at steps 7a, 11a and 11b (spec 002 13.3). Null = none.</summary>
        public IRunnerStepHooks StepHooks { get; set; }

        /// <summary>Power-up collision hooks (shield absorb, smash while invulnerable) called at step 11. Null = none.</summary>
        public IRunnerContactHooks ContactHooks { get; set; }

        /// <summary>
        /// Speed Boost gap auto-jump (GDD 10): the next step jumps as if the player swiped up, if HERO is on the
        /// ground (running, sliding or in coyote time) and the player gave no vertical command on that tick.
        /// Call from a step hook; the request is consumed by the next step either way.
        /// </summary>
        public void RequestAutoJump()
        {
            _autoJumpRequested = true;
        }

        /// <summary>Vine tuning used by this runner.</summary>
        public VineConfig Vines => _vines;

        /// <summary>
        /// GDD 7.3 step 3 "hang": presentation runs at <c>HangTimeScale</c> for the first <c>HangMs</c> of a swing,
        /// otherwise 1. The session multiplies real frame time by this (simulation ticks are unchanged).
        /// </summary>
        public double PresentationTimeScale =>
            _locomotion == Locomotion.Carried && _swingElapsed < _vines.HangTicks ? _vines.HangTimeScale : 1.0;

        /// <summary>A too-early release swipe is waiting (GDD 7.3 step 4).</summary>
        public bool HasBufferedRelease => _releaseBuffered;

        /// <summary>Perfect releases so far in the current chain (score multiplier level).</summary>
        public int ChainPerfects => _chainPerfects;

        /// <summary>Launch of the last vine release: feet height at release (m).</summary>
        public double LaunchStartY => _launchStartY;

        /// <summary>Launch of the last vine release: vertical speed (m/s).</summary>
        public double LaunchVelocityYMps => _launchVelocityY;

        /// <summary>Launch of the last vine release: gravity of the arc (m/s²).</summary>
        public double LaunchGravityMps2 => _launchGravity;

        /// <summary>Launch of the last vine release: forward speed at release (m/s).</summary>
        public double LaunchForwardSpeedMps => _launchSpeed;

        public double LaunchStartZ => _launchStartZ;

        /// <summary>Planned flight time of the last launch (to the ground, or to the next vine of a chain) (s).</summary>
        public double LaunchFlightSeconds => _launchFlightS;

        /// <summary>Aimed lane of the last launch.</summary>
        public int LaunchLane => _launchLane;

        public DeathCause DeathCause => _deathCause;

        /// <summary>Archetype that killed HERO (<see cref="ObstacleArchetype.Gap"/> for a fall; None while alive).</summary>
        public ObstacleArchetype DeathArchetype => _deathArchetype;

        /// <summary>Obstacle id that killed HERO; 0 for a fall or while alive.</summary>
        public int DeathEntityId => _deathEntityId;

        /// <summary>The death was a second stumble while dazed ("Tripped twice", spec 001 9.4.5).</summary>
        public bool DeathAfterStumble => _deathAfterStumble;

        /// <summary>Ticks left in the daze window after a stumble (spec 001 9.4.4).</summary>
        public int DazeTicksLeft => _dazeTicksLeft;

        /// <summary>A side-stumble bounce is running (spec 001 9.4.2, rule L2).</summary>
        public bool IsBouncing => _bounceActive;

        /// <summary>Times a track query filled the whole box buffer. Non-zero is an error in dev builds.</summary>
        public int BoxBufferOverflowCount { get; private set; }

        /// <summary>Times the pass-tracking table was full. Non-zero is an error in dev builds.</summary>
        public int EngagedOverflowCount { get; private set; }

        public bool HasBufferedJump => _bufferedJump;

        public int QueuedLateral => _queuedDir;

        public int CoyoteTicksLeft => _locomotion == Locomotion.Coyote ? _coyoteTicksLeft : 0;

        /// <summary>Invulnerability hook (spec 9.6). Counts down by one per tick.</summary>
        public int InvulnerableTicks => _invulnerableTicks;

        public void SetInvulnerableTicks(int ticks)
        {
            _invulnerableTicks = Math.Max(0, ticks);
        }

        /// <summary>
        /// Writes an event from a step hook (track, coins, score) into this runner's event buffer, so views read one
        /// ordered stream. Only call from <see cref="IRunnerStepHooks"/> during a step.
        /// </summary>
        public void EmitExternal(in RunnerEvent e)
        {
            _events.Add(e);
        }

        /// <summary>Reads this tick's commands from <paramref name="input"/> and steps once.</summary>
        public void Step(IInputProvider input)
        {
            Step(input.ReadCommands(_tick));
        }

        /// <summary>Steps once with a recorded frame. The frame's tick must be <see cref="NextTick"/>.</summary>
        public void Step(InputFrame frame)
        {
            if (frame.Tick != _tick)
            {
                throw new ArgumentException("Frame tick " + frame.Tick + " does not match the next tick " + _tick + ".", nameof(frame));
            }

            Step(frame.Commands);
        }

        /// <summary>Processes one 60 Hz tick (rule I8, amended by spec 002 section 13.3).</summary>
        public void Step(InputCommand commands)
        {
            long tick = _tick;
            Previous = Current;
            _lastOutcomes = default;
            _stumbledThisTick = false;
            _nearMissesThisTick = 0;
            _releaseThisTick = VineReleaseGrade.None;
            _releaseMultiplierThisTick = 0f;
            _grabbedThisTick = false;

            // HERO's box at the start of the tick, for the swept collision test (step 11).
            float xStart = _x;
            double yStart = _y;
            double zStart = _z;
            float heightStart = CurrentHitboxHeight();

            if (tick == 0)
            {
                Emit(RunnerEventType.RunStarted, tick, 0, (byte)_config.StartLane, 0, 0);
            }

            // (1) Read commands.
            bool left = (commands & InputCommand.MoveLeft) != 0;
            bool right = (commands & InputCommand.MoveRight) != 0;
            bool jump = (commands & InputCommand.Jump) != 0;
            bool slide = (commands & InputCommand.Slide) != 0;
            bool resumed = (commands & InputCommand.PauseResumed) != 0;

            // Power-up auto-jump (GDD 10, Speed Boost over gaps): only from the ground and only without a player command.
            if (_autoJumpRequested)
            {
                _autoJumpRequested = false;
                bool onGround = _locomotion == Locomotion.Running || _locomotion == Locomotion.Sliding || _locomotion == Locomotion.Coyote;
                if (onGround && !jump && !slide)
                {
                    jump = true;
                }
            }

            if (_locomotion == Locomotion.Dead)
            {
                IgnoreAll(left, right, jump, slide);
                FinishTick(tick);
                return;
            }

            // (2) Resume after pause clears the jump buffer and the lateral queue (rule I6).
            if (resumed)
            {
                ClearBuffersOnResume();
            }

            // (3) Same-tick conflicts (rule I7).
            if (left && right)
            {
                _counters.Receive(CommandOutcome.Cancelled);
                _counters.Receive(CommandOutcome.Cancelled);
                _lastOutcomes.MoveLeft = CommandOutcome.Cancelled;
                _lastOutcomes.MoveRight = CommandOutcome.Cancelled;
                left = false;
                right = false;
            }

            if (jump && slide)
            {
                _counters.Receive(CommandOutcome.Superseded);
                _lastOutcomes.Slide = CommandOutcome.Superseded;
                slide = false;
            }

            // (4) Age the jump buffer (rules I2, I4).
            if (_bufferedJump && tick - _bufferedJumpTick > _config.InputBufferTicks)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Expired);
            }

            if (_releaseBuffered && tick - _releaseBufferedTick > _vines.ReleaseBufferTicks)
            {
                _releaseBuffered = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Expired);
            }

            // (5) Lateral command.
            if (left)
            {
                CommandOutcome outcome = ProcessLateral(-1, tick);
                _counters.Receive(outcome);
                _lastOutcomes.MoveLeft = outcome;
            }
            else if (right)
            {
                CommandOutcome outcome = ProcessLateral(1, tick);
                _counters.Receive(outcome);
                _lastOutcomes.MoveRight = outcome;
            }

            // (6) Vertical command.
            if (jump)
            {
                CommandOutcome outcome = ProcessJump(tick);
                _counters.Receive(outcome);
                _lastOutcomes.Jump = outcome;
            }
            else if (slide)
            {
                CommandOutcome outcome = ProcessSlide(tick);
                _counters.Receive(outcome);
                _lastOutcomes.Slide = outcome;
            }

            // Hitbox height for this tick's sweep. A command (step 6) changes the box at the start of the tick:
            // a slide shrinks it for the whole tick, a jump out of a slide grows it during the tick (so rising into a
            // high barrier is detected as "from below"). Changes in steps 9 and 10 (slide on landing, standing up
            // after the slide timer) take effect from the next tick's sweep, so standing up just after a barrier's
            // back face can never be swept into it.
            float heightCmd = CurrentHitboxHeight();
            float sweepHeightStart = heightCmd < heightStart ? heightCmd : heightStart;

            // (7) Speed and z.
            UpdateSpeedAndDistance(tick);

            // (7a) HOOK: track update (generate ahead, despawn behind, movers, ChunkEntered/TierChanged).
            IRunnerStepHooks hooks = StepHooks;
            if (hooks != null)
            {
                RunnerTickInfo trackInfo = BuildTickInfo(tick, xStart, yStart, zStart);
                hooks.OnTrackUpdate(this, in trackInfo);
            }

            // (8) Lane tween and queued-move start.
            UpdateLaneMove(tick);

            // (9) Vertical motion, ground check, landing and buffered fire (and the vine swing).
            UpdateVertical(tick);

            // (9a) Vine grab and passed vines (GDD 7.3 step 2, 7.4).
            if (_vineTrack != null && _locomotion != Locomotion.Dead)
            {
                UpdateVines(tick);
            }

            // (10) Slide timer.
            if (_locomotion == Locomotion.Sliding)
            {
                UpdateSlideTimer(tick);
            }

            // (11) Collisions against the track's boxes (section 9). Skipped if HERO fell to death in step 9.
            if (_locomotion != Locomotion.Dead)
            {
                UpdateCollisions(tick, xStart, yStart, zStart, sweepHeightStart, heightCmd);
            }

            if (_invulnerableTicks > 0)
            {
                _invulnerableTicks--;
            }

            if (hooks != null)
            {
                RunnerTickInfo info = BuildTickInfo(tick, xStart, yStart, zStart);

                // (11a) HOOK: coin pickups and streak, skipped on the death tick.
                if (_locomotion != Locomotion.Dead)
                {
                    hooks.OnCoinPickups(this, in info);
                }

                // (11b) HOOK: score (distance, bonuses from this tick's near-misses and streaks).
                hooks.OnScore(this, in info);
            }

            // (12) Events were written in place; publish the snapshot.
            FinishTick(tick);
        }

        /// <summary>
        /// Test hook: puts HERO in the air at <paramref name="heightM"/> with no vertical velocity, as if released
        /// there on the previous tick (state <see cref="Locomotion.Falling"/> above the surface). Later specs
        /// (vine release) will add a proper public entry point.
        /// </summary>
        internal void DebugPlaceInAir(double heightM)
        {
            if (!(heightM > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(heightM), "Must be above the surface.");
            }

            if (_locomotion == Locomotion.Sliding)
            {
                _slideTicksLeft = 0;
            }

            _locomotion = Locomotion.Falling;
            _y = heightM;
            _fallStartY = heightM;
            _fallStartVelocity = 0.0;
            _fallStartTick = _tick - 1;
            _airStartTick = _tick - 1;
            _airStartZ = _z;
            _fallGravity = _config.GravityMps2;
            _slideOnLanding = false;
        }

        /// <summary>
        /// Stable 64-bit FNV-1a hash of the whole simulation state, for determinism and replay checks.
        /// Allocation-free.
        /// </summary>
        public ulong ComputeStateHash()
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, _tick);
            h = Mix(h, (double)_x);
            h = Mix(h, _y);
            h = Mix(h, _z);
            h = Mix(h, _speed);
            h = Mix(h, (long)_locomotion);
            h = Mix(h, _targetLane);
            h = Mix(h, _moveActive ? 1L : 0L);
            h = Mix(h, (double)_moveStartX);
            h = Mix(h, _moveEndLane);
            h = Mix(h, _moveDir);
            h = Mix(h, _moveElapsed);
            h = Mix(h, _queuedDir);
            h = Mix(h, _bufferedJump ? 1L : 0L);
            h = Mix(h, _bufferedJumpTick);
            h = Mix(h, _jumpStartTick);
            h = Mix(h, _airStartTick);
            h = Mix(h, _slideOnLanding ? 1L : 0L);
            h = Mix(h, _fastFallStartTick);
            h = Mix(h, _fastFallStartY);
            h = Mix(h, _fastFallSpeed);
            h = Mix(h, _fallStartTick);
            h = Mix(h, _fallStartY);
            h = Mix(h, _fallStartVelocity);
            h = Mix(h, _slideTicksLeft);
            h = Mix(h, _coyoteTicksLeft);
            h = Mix(h, _invulnerableTicks);
            h = Mix(h, _nextSpeedRow);
            h = Mix(h, (long)_deathCause);
            h = Mix(h, (long)_deathArchetype);
            h = Mix(h, _deathEntityId);
            h = Mix(h, _deathAfterStumble ? 1L : 0L);
            h = Mix(h, _bounceActive ? 1L : 0L);
            h = Mix(h, (double)_bounceStartX);
            h = Mix(h, _bounceEndLane);
            h = Mix(h, _bounceElapsed);
            h = Mix(h, _dazeTicksLeft);
            h = Mix(h, _engagedCount);
            for (int i = 0; i < _engagedCount; i++)
            {
                h = Mix(h, _engagedId[i]);
                h = Mix(h, _engagedMaxZ[i]);
                h = Mix(h, _engagedMinGap[i]);
                h = Mix(h, _engagedFlags[i]);
                h = Mix(h, (long)_engagedArchetype[i]);
            }

            h = Mix(h, _airStartZ);
            h = Mix(h, _fallGravity);
            h = Mix(h, _swingVineId);
            h = Mix(h, _swingGroup);
            h = Mix(h, _swingRow);
            h = Mix(h, _swingLane);
            h = Mix(h, _swingOverChasm ? 1L : 0L);
            h = Mix(h, _swingElapsed);
            h = Mix(h, (double)_swingGrabX);
            h = Mix(h, _swingGrabY);
            h = Mix(h, _aimLane);
            h = Mix(h, _aimVineId);
            h = Mix(h, _aimVineZ);
            h = Mix(h, _aimVineOverChasm ? 1L : 0L);
            h = Mix(h, _releaseBuffered ? 1L : 0L);
            h = Mix(h, _releaseBufferedTick);
            h = Mix(h, _chainPerfects);
            h = Mix(h, _launchedFromVine ? 1L : 0L);
            h = Mix(h, (long)_lastReleaseGrade);
            h = Mix(h, _launchStartY);
            h = Mix(h, _launchVelocityY);
            h = Mix(h, _launchGravity);
            h = Mix(h, _launchSpeed);
            h = Mix(h, _launchStartZ);
            h = Mix(h, _launchFlightS);
            h = Mix(h, _launchLane);
            h = Mix(h, _vineScanZ);
            h = Mix(h, _grabbedGroup);
            h = Mix(h, _grabbedRow);
            h = Mix(h, _missedGroup);
            h = Mix(h, _missedRow);
            h = Mix(h, _autoJumpRequested ? 1L : 0L);
            h = Mix(h, _lift.TotalTicks);
            h = Mix(h, _lift.RiseTicks);
            h = Mix(h, _lift.DescentTicks);
            h = Mix(h, _lift.GlideHeightM);
            h = Mix(h, _lift.LandingInvulnerableTicks);
            h = Mix(h, _liftElapsed);
            h = Mix(h, _liftStartY);
            h = Mix(h, _liftDescending ? 1L : 0L);
            h = Mix(h, _liftDescentElapsed);
            h = Mix(h, _liftDescentStartY);
            return h;
        }

        /// <summary>
        /// Makes this runner's simulation state an exact copy of <paramref name="source"/>'s (spec 002 16.1.6:
        /// cheap state copies for the chunk validator's search). Copies every field that affects future steps,
        /// the outcome counters and the published snapshots. Does not copy the event buffer contents, the track,
        /// headroom query, speed source or hooks (each runner keeps its own). Both runners must use the same
        /// <see cref="RunnerConfig"/> instance. No allocations.
        /// </summary>
        public void CopyStateFrom(RunnerSimulation source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!ReferenceEquals(source._config, _config))
            {
                throw new ArgumentException("Both runners must share one RunnerConfig.", nameof(source));
            }

            _tick = source._tick;
            _x = source._x;
            _y = source._y;
            _z = source._z;
            _speed = source._speed;
            _locomotion = source._locomotion;
            _targetLane = source._targetLane;
            _moveActive = source._moveActive;
            _moveStartX = source._moveStartX;
            _moveEndLane = source._moveEndLane;
            _moveDir = source._moveDir;
            _moveElapsed = source._moveElapsed;
            _queuedDir = source._queuedDir;
            _bufferedJump = source._bufferedJump;
            _bufferedJumpTick = source._bufferedJumpTick;
            _jumpStartTick = source._jumpStartTick;
            _airStartTick = source._airStartTick;
            _slideOnLanding = source._slideOnLanding;
            _fastFallStartTick = source._fastFallStartTick;
            _fastFallStartY = source._fastFallStartY;
            _fastFallSpeed = source._fastFallSpeed;
            _fallStartTick = source._fallStartTick;
            _fallStartY = source._fallStartY;
            _fallStartVelocity = source._fallStartVelocity;
            _slideTicksLeft = source._slideTicksLeft;
            _coyoteTicksLeft = source._coyoteTicksLeft;
            _invulnerableTicks = source._invulnerableTicks;
            _nextSpeedRow = source._nextSpeedRow;
            _deathCause = source._deathCause;
            _deathArchetype = source._deathArchetype;
            _deathEntityId = source._deathEntityId;
            _deathAfterStumble = source._deathAfterStumble;
            _bounceActive = source._bounceActive;
            _bounceStartX = source._bounceStartX;
            _bounceEndLane = source._bounceEndLane;
            _bounceElapsed = source._bounceElapsed;
            _dazeTicksLeft = source._dazeTicksLeft;
            _stumbledThisTick = source._stumbledThisTick;
            _nearMissesThisTick = source._nearMissesThisTick;
            _engagedCount = source._engagedCount;
            Array.Copy(source._engagedId, _engagedId, _engagedCount);
            Array.Copy(source._engagedMaxZ, _engagedMaxZ, _engagedCount);
            Array.Copy(source._engagedMinGap, _engagedMinGap, _engagedCount);
            Array.Copy(source._engagedFlags, _engagedFlags, _engagedCount);
            Array.Copy(source._engagedArchetype, _engagedArchetype, _engagedCount);
            _airStartZ = source._airStartZ;
            _fallGravity = source._fallGravity;
            _swingVineId = source._swingVineId;
            _swingGroup = source._swingGroup;
            _swingRow = source._swingRow;
            _swingLane = source._swingLane;
            _swingOverChasm = source._swingOverChasm;
            _swingElapsed = source._swingElapsed;
            _swingGrabX = source._swingGrabX;
            _swingGrabY = source._swingGrabY;
            _aimLane = source._aimLane;
            _aimVineId = source._aimVineId;
            _aimVineZ = source._aimVineZ;
            _aimVineOverChasm = source._aimVineOverChasm;
            _releaseBuffered = source._releaseBuffered;
            _releaseBufferedTick = source._releaseBufferedTick;
            _chainPerfects = source._chainPerfects;
            _launchedFromVine = source._launchedFromVine;
            _lastReleaseGrade = source._lastReleaseGrade;
            _releaseThisTick = source._releaseThisTick;
            _releaseMultiplierThisTick = source._releaseMultiplierThisTick;
            _grabbedThisTick = source._grabbedThisTick;
            _launchStartY = source._launchStartY;
            _launchVelocityY = source._launchVelocityY;
            _launchGravity = source._launchGravity;
            _launchSpeed = source._launchSpeed;
            _launchStartZ = source._launchStartZ;
            _launchFlightS = source._launchFlightS;
            _launchLane = source._launchLane;
            _vineScanZ = source._vineScanZ;
            _grabbedGroup = source._grabbedGroup;
            _grabbedRow = source._grabbedRow;
            _missedGroup = source._missedGroup;
            _missedRow = source._missedRow;
            _lift = source._lift;
            _liftElapsed = source._liftElapsed;
            _liftStartY = source._liftStartY;
            _liftDescending = source._liftDescending;
            _liftDescentElapsed = source._liftDescentElapsed;
            _liftDescentStartY = source._liftDescentStartY;
            _lastOutcomes = source._lastOutcomes;
            _counters.CopyFrom(source._counters);
            SpeedMultiplier = source.SpeedMultiplier;
            _autoJumpRequested = source._autoJumpRequested;
            TutorialActive = source.TutorialActive;
            Current = source.Current;
            Previous = source.Previous;
        }

        private static ulong Mix(ulong hash, double value)
        {
            return Mix(hash, BitConverter.DoubleToInt64Bits(value));
        }

        private static ulong Mix(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int i = 0; i < 8; i++)
            {
                hash ^= v & 0xFFUL;
                hash = unchecked(hash * 1099511628211UL);
                v >>= 8;
            }

            return hash;
        }

        private void IgnoreAll(bool left, bool right, bool jump, bool slide)
        {
            if (left)
            {
                _counters.Receive(CommandOutcome.Ignored);
                _lastOutcomes.MoveLeft = CommandOutcome.Ignored;
            }

            if (right)
            {
                _counters.Receive(CommandOutcome.Ignored);
                _lastOutcomes.MoveRight = CommandOutcome.Ignored;
            }

            if (jump)
            {
                _counters.Receive(CommandOutcome.Ignored);
                _lastOutcomes.Jump = CommandOutcome.Ignored;
            }

            if (slide)
            {
                _counters.Receive(CommandOutcome.Ignored);
                _lastOutcomes.Slide = CommandOutcome.Ignored;
            }
        }

        private void ClearBuffersOnResume()
        {
            if (_bufferedJump)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            if (_queuedDir != 0)
            {
                _queuedDir = 0;
                _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Invalidated);
            }

            if (_releaseBuffered)
            {
                _releaseBuffered = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }
        }

        // ---- Lanes (section 5) ----

        private CommandOutcome ProcessLateral(int dir, long tick)
        {
            // L1. (Dead is handled before any command processing.)
            if (_y < 0.0)
            {
                return CommandOutcome.Ignored;
            }

            // GDD 7.3 step 3: on a vine, left/right aims at the next vine instead of moving.
            if (_locomotion == Locomotion.Carried)
            {
                return ProcessAim(dir, tick);
            }

            // L2: during a stumble bounce, store the newest lateral command; it starts when the bounce ends.
            if (_bounceActive)
            {
                if (_queuedDir != 0)
                {
                    _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Superseded);
                }

                _queuedDir = dir;
                return CommandOutcome.Queued;
            }

            // L3.
            if (!_moveActive)
            {
                int to = _targetLane + dir;
                if (IsValidLane(to))
                {
                    StartLaneMove(to, dir, tick, false, false);
                    return CommandOutcome.Executed;
                }

                EmitLaneBlocked(dir, tick);
                return CommandOutcome.Bumped;
            }

            if (dir == -_moveDir)
            {
                // L4.
                if (_queuedDir != 0)
                {
                    Emit(RunnerEventType.LaneChangeCancelled, tick, (sbyte)_queuedDir, (byte)_targetLane, 0, 0);
                    _queuedDir = 0;
                    _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Cancelled);
                    return CommandOutcome.Executed;
                }

                // L5: reverse from the current X back to the origin lane.
                int origin = _moveEndLane - _moveDir;
                StartLaneMove(origin, dir, tick, true, false);
                return CommandOutcome.Executed;
            }

            // L7.
            if (_queuedDir != 0)
            {
                EmitLaneBlocked(dir, tick);
                return CommandOutcome.Bumped;
            }

            // L6.
            int next = _targetLane + dir;
            if (!IsValidLane(next))
            {
                EmitLaneBlocked(dir, tick);
                return CommandOutcome.Bumped;
            }

            if (_moveElapsed >= _config.LaneQueueStartTick)
            {
                StartLaneMove(next, dir, tick, false, false);
                return CommandOutcome.Executed;
            }

            _queuedDir = dir;
            return CommandOutcome.Queued;
        }

        private void StartLaneMove(int endLane, int dir, long tick, bool reversal, bool fromQueue)
        {
            int fromLane = _targetLane;
            _moveActive = true;
            _moveStartX = _x;
            _moveEndLane = endLane;
            _moveDir = dir;
            _moveElapsed = 0;
            _targetLane = endLane;

            byte flags = 0;
            if (reversal)
            {
                flags |= RunnerEventFlags.Reversal;
            }

            if (fromQueue)
            {
                flags |= RunnerEventFlags.Queued;
            }

            Emit(RunnerEventType.LaneChangeStarted, tick, (sbyte)dir, (byte)endLane, flags, (short)fromLane);
        }

        private void UpdateLaneMove(long tick)
        {
            // A queued move waits for a running bounce to finish (rule L2); it starts on the tick after the bounce
            // arrives, so the bounce always ends exactly on the origin lane center.
            if (_queuedDir != 0 && !_bounceActive && (!_moveActive || _moveElapsed >= _config.LaneQueueStartTick))
            {
                int dir = _queuedDir;
                _queuedDir = 0;
                int to = _targetLane + dir;
                if (IsValidLane(to))
                {
                    StartLaneMove(to, dir, tick, false, true);
                    _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Executed);
                }
                else
                {
                    // Unreachable with the queue rules, kept so a command can never vanish.
                    EmitLaneBlocked(dir, tick);
                    _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Bumped);
                }
            }

            if (_bounceActive)
            {
                UpdateBounce();
                return;
            }

            if (!_moveActive)
            {
                return;
            }

            _moveElapsed++;
            float endX = _config.LaneCenterX(_moveEndLane);
            if (_moveElapsed >= _config.LaneSwitchTicks)
            {
                _x = endX;
                _moveActive = false;
                _moveElapsed = 0;
                return;
            }

            double p = EaseProgress(_moveElapsed);
            _x = (float)(_moveStartX + (endX - _moveStartX) * p);
        }

        private void UpdateBounce()
        {
            _bounceElapsed++;
            float endX = _config.LaneCenterX(_bounceEndLane);
            if (_bounceElapsed >= _config.StumbleBounceTicks)
            {
                _x = endX;
                _bounceActive = false;
                _bounceElapsed = 0;
                return;
            }

            double u = (double)_bounceElapsed / _config.StumbleBounceTicks;
            double p = 1.0 - Math.Pow(1.0 - u, _config.LaneSwitchEaseExponent);
            _x = (float)(_bounceStartX + (endX - _bounceStartX) * p);
        }

        private double EaseProgress(int elapsedTicks)
        {
            double u = (double)elapsedTicks / _config.LaneSwitchTicks;
            return 1.0 - Math.Pow(1.0 - u, _config.LaneSwitchEaseExponent);
        }

        private bool IsValidLane(int lane)
        {
            return lane >= 0 && lane < _config.LaneCount;
        }

        private void EmitLaneBlocked(int dir, long tick)
        {
            Emit(RunnerEventType.LaneBlocked, tick, (sbyte)dir, (byte)_targetLane, 0, 0);
        }

        private int ComputeOccupiedLane()
        {
            double f = _x / (double)_config.LaneWidthM + (_config.LaneCount - 1) * 0.5;
            int lower = (int)Math.Floor(f);
            double frac = f - lower;
            int lane;
            if (frac < 0.5)
            {
                lane = lower;
            }
            else if (frac > 0.5)
            {
                lane = lower + 1;
            }
            else
            {
                lane = _targetLane > lower ? lower + 1 : lower;
            }

            if (lane < 0)
            {
                return 0;
            }

            return lane >= _config.LaneCount ? _config.LaneCount - 1 : lane;
        }

        // ---- Vertical (section 6) ----

        private CommandOutcome ProcessJump(long tick)
        {
            if (_y < 0.0)
            {
                return CommandOutcome.Ignored;
            }

            switch (_locomotion)
            {
                case Locomotion.Running:
                case Locomotion.Sliding:
                case Locomotion.Coyote:
                    StartJump(tick, false);
                    return CommandOutcome.Executed;

                case Locomotion.Airborne:
                case Locomotion.FastFalling:
                case Locomotion.Falling:
                    if (_bufferedJump)
                    {
                        _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Superseded);
                    }

                    _bufferedJump = true;
                    _bufferedJumpTick = tick;
                    return CommandOutcome.Buffered;

                case Locomotion.Carried:
                    return ProcessRelease(tick);

                default:
                    return CommandOutcome.Ignored;
            }
        }

        private CommandOutcome ProcessSlide(long tick)
        {
            if (_y < 0.0)
            {
                return CommandOutcome.Ignored;
            }

            switch (_locomotion)
            {
                case Locomotion.Running:
                    StartSlide(tick, false);
                    return CommandOutcome.Executed;

                case Locomotion.Sliding:
                    StartSlide(tick, true);
                    return CommandOutcome.Executed;

                case Locomotion.Airborne:
                case Locomotion.Coyote:
                    StartFastFall(tick);
                    return CommandOutcome.Executed;

                case Locomotion.Falling:
                    if (_y > 0.0)
                    {
                        StartFastFall(tick);
                        return CommandOutcome.Executed;
                    }

                    return CommandOutcome.Ignored;

                case Locomotion.FastFalling:
                    return CommandOutcome.AlreadyActive;

                case Locomotion.Carried:
                    // GDD 5.1: ignored on the vine, kept as a slide for the landing.
                    _slideOnLanding = true;
                    return CommandOutcome.Executed;

                default:
                    return CommandOutcome.Ignored;
            }
        }

        private void StartJump(long tick, bool buffered)
        {
            byte flags = 0;
            if (_locomotion == Locomotion.Sliding)
            {
                _slideTicksLeft = 0;
                Emit(RunnerEventType.SlideEnded, tick, 0, (byte)_targetLane, 0, (short)SlideEndReason.Jump);
                flags |= RunnerEventFlags.FromSlide;
            }

            if (_locomotion == Locomotion.Coyote)
            {
                flags |= RunnerEventFlags.Coyote;
            }

            if (buffered)
            {
                flags |= RunnerEventFlags.Buffered;
            }

            _locomotion = Locomotion.Airborne;
            _jumpStartTick = tick;
            _airStartTick = tick;
            _airStartZ = _z;
            _launchedFromVine = false;
            _coyoteTicksLeft = 0;
            _slideOnLanding = false;
            _y = 0.0;
            Emit(RunnerEventType.JumpStarted, tick, 0, (byte)_targetLane, flags, 0);
        }

        private void StartSlide(long tick, bool restart)
        {
            _locomotion = Locomotion.Sliding;
            _slideTicksLeft = _config.SlideTicks;
            Emit(RunnerEventType.SlideStarted, tick, 0, (byte)_targetLane, restart ? RunnerEventFlags.Restart : (byte)0, 0);
        }

        private void StartFastFall(long tick)
        {
            if (_locomotion == Locomotion.Coyote)
            {
                _airStartTick = tick;
                _airStartZ = _z;
                _coyoteTicksLeft = 0;
            }

            _fastFallStartY = _y;
            _fastFallSpeed = Math.Max(_config.FastFallMinSpeedMps, _y / _config.FastFallMaxSeconds);
            _fastFallStartTick = tick;
            _locomotion = Locomotion.FastFalling;
            _slideOnLanding = true;
            Emit(RunnerEventType.FastFallStarted, tick, 0, (byte)_targetLane, 0, 0);
        }

        private void UpdateVertical(long tick)
        {
            switch (_locomotion)
            {
                case Locomotion.Running:
                case Locomotion.Sliding:
                    if (!HasGroundUnderHero())
                    {
                        LeaveGround(tick);
                    }

                    break;

                case Locomotion.Coyote:
                    if (HasGroundUnderHero())
                    {
                        _locomotion = Locomotion.Running;
                        _coyoteTicksLeft = 0;
                    }
                    else
                    {
                        _coyoteTicksLeft--;
                        if (_coyoteTicksLeft <= 0)
                        {
                            _coyoteTicksLeft = 0;
                            BeginFall(tick, 0.0, 0.0);
                        }
                    }

                    break;

                case Locomotion.Airborne:
                    UpdateJumpArc(tick);
                    break;

                case Locomotion.FastFalling:
                    UpdateFastFall(tick);
                    break;

                case Locomotion.Falling:
                    UpdateFall(tick);
                    break;

                case Locomotion.Carried:
                    UpdateSwing(tick);
                    break;

                case Locomotion.Lifted:
                    UpdateLift(tick);
                    break;
            }
        }

        private void UpdateJumpArc(long tick)
        {
            int n = (int)(tick - _jumpStartTick);
            if (n >= _config.JumpAirtimeTicks)
            {
                _y = 0.0;
                if (HasGroundUnderHero())
                {
                    Land(tick, false);
                }
                else
                {
                    // No ground at the landing tick: continue the same parabola below the surface (6.2.4).
                    double t = n * RunnerConfig.TickSeconds;
                    BeginFall(tick, 0.0, _config.JumpVelocityMps - _config.GravityMps2 * t);
                }

                return;
            }

            double s = n * RunnerConfig.TickSeconds;
            _y = _config.JumpVelocityMps * s - 0.5 * _config.GravityMps2 * s * s;
            if (n == _config.JumpApexTick && n > 0)
            {
                Emit(RunnerEventType.JumpApex, tick, 0, (byte)_targetLane, 0, 0);
            }
        }

        private void UpdateFastFall(long tick)
        {
            long n = tick - _fastFallStartTick + 1;
            double y = _fastFallStartY - _fastFallSpeed * n * RunnerConfig.TickSeconds;
            if (y > SurfaceEpsilonM)
            {
                _y = y;
                return;
            }

            _y = 0.0;
            if (HasGroundUnderHero())
            {
                Land(tick, true);
            }
            else
            {
                _slideOnLanding = false;
                BeginFall(tick, 0.0, -_fastFallSpeed);
            }
        }

        private void UpdateFall(long tick)
        {
            double previousY = _y;
            double t = (tick - _fallStartTick) * RunnerConfig.TickSeconds;
            double y = _fallStartY + _fallStartVelocity * t - 0.5 * _fallGravity * t * t;

            if (previousY > 0.0 && y <= SurfaceEpsilonM)
            {
                // Coming down from above the surface: this is a landing tick.
                _y = 0.0;
                if (HasGroundUnderHero())
                {
                    Land(tick, false);
                }
                else
                {
                    BeginFall(tick, 0.0, _fallStartVelocity - _fallGravity * t);
                }

                return;
            }

            _y = y;
            if (_y <= -_config.FallDeathDepthM)
            {
                DieFalling(tick);
            }
        }

        private void BeginFall(long tick, double startY, double startVelocity)
        {
            _locomotion = Locomotion.Falling;
            _fallStartTick = tick;
            _fallStartY = startY;
            _fallStartVelocity = startVelocity;
            _fallGravity = _config.GravityMps2;
            _y = startY;
        }

        private void LeaveGround(long tick)
        {
            if (_locomotion == Locomotion.Sliding)
            {
                _slideTicksLeft = 0;
                Emit(RunnerEventType.SlideEnded, tick, 0, (byte)_targetLane, 0, (short)SlideEndReason.Ledge);
            }

            bool coyote = _config.CoyoteTicks > 0;
            Emit(RunnerEventType.LeftGround, tick, 0, (byte)_targetLane, coyote ? RunnerEventFlags.Coyote : (byte)0, 0);
            if (coyote)
            {
                _locomotion = Locomotion.Coyote;
                _coyoteTicksLeft = _config.CoyoteTicks;
            }
            else
            {
                BeginFall(tick, 0.0, 0.0);
            }
        }

        private void Land(long tick, bool wasFastFall)
        {
            _y = 0.0;
            long airTicks = tick - _airStartTick;
            Emit(
                RunnerEventType.Landed,
                tick,
                0,
                (byte)_targetLane,
                wasFastFall ? RunnerEventFlags.WasFastFall : (byte)0,
                (short)Math.Min(airTicks, short.MaxValue));

            bool slideOnLanding = _slideOnLanding;
            _slideOnLanding = false;
            _locomotion = Locomotion.Running;
            _fallGravity = _config.GravityMps2;
            _launchedFromVine = false;
            _chainPerfects = 0;

            // Rule I3 first, then the pending slide from a fast-fall (6.2.3, 6.3.4).
            if (_bufferedJump)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Executed);
                StartJump(tick, true);
            }
            else if (slideOnLanding)
            {
                StartSlide(tick, false);
            }
        }

        private void UpdateSlideTimer(long tick)
        {
            if (_slideTicksLeft > 0)
            {
                _slideTicksLeft--;
                return;
            }

            // Timer done: stand up only if the standing box is clear (6.4.5); otherwise keep sliding.
            if (CanStandHere())
            {
                _locomotion = Locomotion.Running;
                Emit(RunnerEventType.SlideEnded, tick, 0, (byte)_targetLane, 0, (short)SlideEndReason.Timeout);
            }
        }

        private bool HasGroundUnderHero()
        {
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            return _track.HasGround(_x, _z - halfDepth, _z + halfDepth);
        }

        /// <summary>
        /// Spec 001 6.4.5: may a sliding HERO stand up here? Uses the <see cref="IHeadroomQuery"/> override when one
        /// was given, otherwise checks the standing box (bottom at the surface) against the track's obstacle boxes.
        /// </summary>
        private bool CanStandHere()
        {
            float halfWidth = _config.PlayerHitboxWidthM * 0.5f;
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            if (_headroom != null)
            {
                return _headroom.CanStand(_x - halfWidth, _x + halfWidth, _z - halfDepth, _z + halfDepth, _config.StandingHeightM);
            }

            double zMin = _z - halfDepth;
            double zMax = _z + halfDepth;
            int count = QueryBoxes(zMin, zMax);
            float xMin = _x - halfWidth;
            float xMax = _x + halfWidth;
            float top = (float)_y + _config.StandingHeightM;
            for (int i = 0; i < count; i++)
            {
                if (_boxes[i].XMin < xMax && _boxes[i].XMax > xMin
                    && _boxes[i].YMin < top && _boxes[i].YMax > (float)_y
                    && _boxes[i].ZMin < zMax && _boxes[i].ZMax > zMin)
                {
                    return false;
                }
            }

            return true;
        }

        private int QueryBoxes(double zMin, double zMax)
        {
            int count = _track.GetBoxes(zMin, zMax, new Span<ObstacleBox>(_boxes));
            if (count >= _boxes.Length)
            {
                BoxBufferOverflowCount++;
                count = _boxes.Length;
            }

            return count;
        }

        private float CurrentHitboxHeight()
        {
            return _locomotion == Locomotion.Sliding ? _config.SlidingHeightM : _config.StandingHeightM;
        }

        private void Die(long tick, DeathCause cause)
        {
            bool fall = cause == DeathCause.Fell || cause == DeathCause.MissedVine;
            Die(tick, cause, fall ? ObstacleArchetype.Gap : ObstacleArchetype.None, 0, false);
        }

        private void Die(long tick, DeathCause cause, ObstacleArchetype archetype, int entityId, bool afterStumble)
        {
            _locomotion = Locomotion.Dead;
            _deathCause = cause;
            _deathArchetype = archetype;
            _deathEntityId = entityId;
            _deathAfterStumble = afterStumble;
            _speed = 0.0;
            _moveActive = false;
            _bounceActive = false;
            _slideOnLanding = false;
            _swingVineId = 0;
            _launchedFromVine = false;
            if (_releaseBuffered)
            {
                _releaseBuffered = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            // Rule I5: the buffer and the lateral queue can no longer fire.
            if (_bufferedJump)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            if (_queuedDir != 0)
            {
                _queuedDir = 0;
                _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Invalidated);
            }

            Emit(
                RunnerEventType.Died,
                tick,
                0,
                (byte)_targetLane,
                afterStumble ? RunnerEventFlags.AfterStumble : (byte)0,
                (short)cause,
                entityId,
                archetype);
        }

        // ---- Companion Lift (GDD 15.1) and Continue (GDD 14.4) ----

        /// <summary>
        /// Starts a companion Lift now (between ticks; the next <see cref="Step(InputCommand)"/> is its first tick).
        /// HERO rises to the glide height, is invulnerable, can still change lanes (Jump and Slide are ignored) and
        /// is set down at the end; if there is no ground where the descent would end, the glide is extended until
        /// there is. Returns false (nothing changes) when <see cref="CanStartLift"/> is false. No allocation.
        /// </summary>
        public bool TryStartLift(in LiftParameters parameters)
        {
            if (!CanStartLift || parameters.TotalTicks <= 0)
            {
                return false;
            }

            long tick = _tick;
            if (_locomotion == Locomotion.Sliding)
            {
                _slideTicksLeft = 0;
                Emit(RunnerEventType.SlideEnded, tick, 0, (byte)_targetLane, 0, (short)SlideEndReason.Lift);
            }

            // Rule I5: buffered actions belong to the ground; they cannot fire during Lift.
            if (_bufferedJump)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            if (_releaseBuffered)
            {
                _releaseBuffered = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            _lift = parameters;
            if (_lift.DescentTicks < 1)
            {
                _lift.DescentTicks = 1;
            }

            if (_lift.DescentTicks > _lift.TotalTicks)
            {
                _lift.DescentTicks = _lift.TotalTicks;
            }

            if (_lift.RiseTicks < 1)
            {
                _lift.RiseTicks = 1;
            }

            _slideOnLanding = false;
            _coyoteTicksLeft = 0;
            _launchedFromVine = false;
            _chainPerfects = 0;
            _fallGravity = _config.GravityMps2;
            _liftElapsed = 0;
            _liftStartY = _y > 0.0 ? _y : 0.0;
            _y = _liftStartY;
            _liftDescending = false;
            _liftDescentElapsed = 0;
            _liftDescentStartY = 0.0;
            _locomotion = Locomotion.Lifted;
            Emit(RunnerEventType.CompanionLiftStarted, tick, 0, (byte)_targetLane, 0, (short)Math.Min(_lift.TotalTicks, short.MaxValue));
            return true;
        }

        /// <summary>
        /// Continue (GDD 14.4): brings a dead HERO back running at <paramref name="z"/> in <paramref name="lane"/>
        /// with <paramref name="invulnerableTicks"/> of invulnerability. Clears the death, daze, buffers, lane moves,
        /// vine and lift state; vines behind the respawn point are not reported as missed. Both snapshots are set to
        /// the respawn state so views snap there. Returns false if HERO is not dead.
        /// </summary>
        public bool Revive(double z, int lane, int invulnerableTicks)
        {
            if (_locomotion != Locomotion.Dead)
            {
                return false;
            }

            if (lane < 0)
            {
                lane = 0;
            }
            else if (lane >= _config.LaneCount)
            {
                lane = _config.LaneCount - 1;
            }

            long tick = _tick;
            _locomotion = Locomotion.Running;
            _targetLane = lane;
            _x = _config.LaneCenterX(lane);
            _y = 0.0;
            if (z > _z)
            {
                _z = z;
            }

            _moveActive = false;
            _moveElapsed = 0;
            _queuedDir = 0;
            _bounceActive = false;
            _bounceElapsed = 0;
            _dazeTicksLeft = 0;
            _slideTicksLeft = 0;
            _coyoteTicksLeft = 0;
            _bufferedJump = false;
            _releaseBuffered = false;
            _slideOnLanding = false;
            _deathCause = DeathCause.None;
            _deathArchetype = ObstacleArchetype.None;
            _deathEntityId = 0;
            _deathAfterStumble = false;
            _engagedCount = 0;
            _swingVineId = 0;
            _swingElapsed = 0;
            _launchedFromVine = false;
            _chainPerfects = 0;
            _fallGravity = _config.GravityMps2;
            _liftElapsed = 0;
            _liftDescending = false;
            _liftDescentElapsed = 0;

            double passLimit = _z - _config.PlayerHitboxDepthM * 0.5 - _vines.GrabZoneLengthM * 0.5;
            if (passLimit > _vineScanZ)
            {
                _vineScanZ = passLimit;
            }

            _invulnerableTicks = Math.Max(0, invulnerableTicks);
            Emit(RunnerEventType.Revived, tick, 0, (byte)lane, 0, (short)Math.Min(_invulnerableTicks, short.MaxValue));
            Current = BuildSnapshot(tick - 1);
            Previous = Current;
            return true;
        }

        /// <summary>Step 9 during Lift: rise, glide, then descend once there is ground where the descent ends.</summary>
        private void UpdateLift(long tick)
        {
            _liftElapsed++;
            if (!_liftDescending)
            {
                int rise = _lift.RiseTicks;
                if (_liftElapsed < rise)
                {
                    double u = (double)_liftElapsed / rise;
                    u = 1.0 - (1.0 - u) * (1.0 - u);
                    _y = _liftStartY + (_lift.GlideHeightM - _liftStartY) * u;
                }
                else
                {
                    _y = _lift.GlideHeightM;
                }

                if (_liftElapsed >= _lift.TotalTicks - _lift.DescentTicks)
                {
                    // GDD 15.1: a lift that would end over a gap or chasm glides on until there is ground.
                    double landZ = _z + _speed * _lift.DescentTicks * RunnerConfig.TickSeconds;
                    if (HasGroundAt(_config.LaneCenterX(_targetLane), landZ))
                    {
                        _liftDescending = true;
                        _liftDescentElapsed = 0;
                        _liftDescentStartY = _y;
                        Emit(RunnerEventType.CompanionLiftDescending, tick, 0, (byte)_targetLane, 0, 0);
                    }
                }

                return;
            }

            _liftDescentElapsed++;
            int descent = _lift.DescentTicks;
            if (_liftDescentElapsed < descent)
            {
                double u = (double)_liftDescentElapsed / descent;
                double s = u * u * (3.0 - 2.0 * u);
                _y = _liftDescentStartY * (1.0 - s);
                return;
            }

            // Touchdown, or hover just above the surface until there is ground (a lane change over a gap).
            _y = 0.0;
            if (HasGroundUnderHero())
            {
                EndLift(tick);
            }
        }

        private void EndLift(long tick)
        {
            _locomotion = Locomotion.Running;
            _y = 0.0;
            if (_invulnerableTicks < _lift.LandingInvulnerableTicks)
            {
                _invulnerableTicks = _lift.LandingInvulnerableTicks;
            }

            Emit(RunnerEventType.CompanionLiftEnded, tick, 0, (byte)_targetLane, 0, (short)Math.Min(_liftElapsed, short.MaxValue));
            _liftElapsed = 0;
            _liftDescending = false;
            _liftDescentElapsed = 0;
        }

        private bool HasGroundAt(float x, double z)
        {
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            return _track.HasGround(x, z - halfDepth, z + halfDepth);
        }

        // ---- Vines (GDD 7) ----

        /// <summary>GDD 7.3 step 3: left/right on a vine aims at the next vine (the swinging lane ± 1).</summary>
        private CommandOutcome ProcessAim(int dir, long tick)
        {
            int to = _aimLane + dir;
            if (!IsValidLane(to) || Math.Abs(to - _swingLane) > 1)
            {
                EmitLaneBlocked(dir, tick);
                return CommandOutcome.Bumped;
            }

            _aimLane = to;
            RefreshAimTarget();
            Emit(RunnerEventType.VineAimChanged, tick, (sbyte)dir, (byte)to, 0, 0, _aimVineId, ObstacleArchetype.None);
            return CommandOutcome.Executed;
        }

        /// <summary>GDD 7.3 step 4: Jump on a vine releases; too early it is buffered for <c>ReleaseBufferMs</c>.</summary>
        private CommandOutcome ProcessRelease(long tick)
        {
            VineReleaseGrade grade = _vines.GradeForSwingTick(_swingElapsed);
            if (grade == VineReleaseGrade.None)
            {
                if (_releaseBuffered)
                {
                    _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Superseded);
                }

                _releaseBuffered = true;
                _releaseBufferedTick = tick;
                return CommandOutcome.Buffered;
            }

            ReleaseVine(tick, grade, true);
            return CommandOutcome.Executed;
        }

        /// <summary>Step 9 while carried: advance the swing, settle onto the arc, fire a buffered or automatic release.</summary>
        private void UpdateSwing(long tick)
        {
            _swingElapsed++;
            float laneX = _config.LaneCenterX(_swingLane);
            double arcY = _vines.SwingFeetYAt(_swingElapsed);
            int blend = _vines.GrabBlendTicks;
            if (_swingElapsed < blend)
            {
                double u = (double)_swingElapsed / blend;
                u = 1.0 - (1.0 - u) * (1.0 - u);
                _x = (float)(_swingGrabX + (laneX - _swingGrabX) * u);
                _y = _swingGrabY + (arcY - _swingGrabY) * u;
            }
            else
            {
                _x = laneX;
                _y = arcY;
            }

            RefreshAimTarget();

            if (_releaseBuffered && _swingElapsed >= _vines.GoodStartTick)
            {
                _releaseBuffered = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Executed);
                ReleaseVine(tick, _vines.GradeForSwingTick(_swingElapsed), false);
                return;
            }

            if (_swingElapsed >= _vines.SwingTicks)
            {
                ReleaseVine(tick, VineReleaseGrade.Auto, false);
            }
        }

        /// <summary>
        /// Lets go of the vine (GDD 7.3 steps 4 and 5): a launch on a ballistic arc. If a next vine of the chain is
        /// aimed at (and, for an auto release, is safe), the arc is guided so HERO arrives at its grab point:
        /// gravity is solved from the flight time and clamped, then the vertical speed is solved again.
        /// <paramref name="fromCommand"/>: released at step 6, so the arc already moves on this tick's step 9.
        /// </summary>
        private void ReleaseVine(long tick, VineReleaseGrade grade, bool fromCommand)
        {
            float multiplier = _vines.GetChainMultiplier(_chainPerfects);
            if (grade == VineReleaseGrade.Perfect)
            {
                _chainPerfects++;
            }

            double y0 = _y;
            double vy = _vines.LaunchSpeedFor(grade);
            double g = _vines.LaunchGravityMps2;
            double speed = _speed > 0.1 ? _speed : 0.1;
            bool chained = _aimVineId != 0 && (grade != VineReleaseGrade.Auto || !_aimVineOverChasm);
            double flight;
            double guidedT = (_aimVineZ - _z) / speed;
            if (chained && guidedT > MinGuidedFlightS)
            {
                double arrive = _vines.ChainArriveFeetM;
                g = 2.0 * (y0 + vy * guidedT - arrive) / (guidedT * guidedT);
                if (g < _vines.ChainGravityMinMps2)
                {
                    g = _vines.ChainGravityMinMps2;
                }
                else if (g > _vines.ChainGravityMaxMps2)
                {
                    g = _vines.ChainGravityMaxMps2;
                }

                vy = (arrive - y0 + 0.5 * g * guidedT * guidedT) / guidedT;
                flight = guidedT;
            }
            else
            {
                flight = (vy + Math.Sqrt(vy * vy + 2.0 * g * (y0 > 0.0 ? y0 : 0.0))) / g;
            }

            int vineId = _swingVineId;
            _releaseThisTick = grade;
            _releaseMultiplierThisTick = multiplier;
            _lastReleaseGrade = grade;
            _launchStartY = y0;
            _launchVelocityY = vy;
            _launchGravity = g;
            _launchSpeed = speed;
            _launchStartZ = _z;
            _launchFlightS = flight;
            _launchLane = _aimLane;

            _swingVineId = 0;
            _swingElapsed = 0;
            _locomotion = Locomotion.Falling;
            _fallStartTick = fromCommand ? tick - 1 : tick;
            _fallStartY = y0;
            _fallStartVelocity = vy;
            _fallGravity = g;
            _airStartTick = tick;
            _airStartZ = _z;
            _launchedFromVine = true;

            if (_aimLane != _targetLane)
            {
                StartLaneMove(_aimLane, _aimLane > _targetLane ? 1 : -1, tick, false, false);
            }

            Emit(
                RunnerEventType.VineReleased,
                tick,
                0,
                (byte)_aimLane,
                chained ? RunnerEventFlags.VineChained : (byte)0,
                (short)grade,
                vineId,
                ObstacleArchetype.None);
        }

        /// <summary>Step 9a: grab a vine when airborne inside its grab zone; report vines passed without a grab.</summary>
        private void UpdateVines(long tick)
        {
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            double zoneHalf = _vines.GrabZoneLengthM * 0.5;

            if (CanGrab())
            {
                int n = QueryVines(_z - halfDepth - zoneHalf, _z + halfDepth + zoneHalf);
                float reachX = _vines.GrabZoneWidthM * 0.5f + _config.PlayerHitboxWidthM * 0.5f;
                double top = _y + _config.StandingHeightM;
                for (int i = 0; i < n; i++)
                {
                    VineAnchor v = _vineBuffer[i];
                    if (v.Group == _grabbedGroup && v.Row <= _grabbedRow)
                    {
                        continue; // already swung on (or behind) in this section
                    }

                    if (Math.Abs(_x - _config.LaneCenterX(v.Lane)) >= reachX)
                    {
                        continue; // wrong lane
                    }

                    if (!(top > _vines.GrabZoneBottomM && _y < _vines.GrabZoneTopM))
                    {
                        continue;
                    }

                    if (!_launchedFromVine)
                    {
                        // A jump started up to GrabEarliness before HERO's front reached the zone counts.
                        double zoneNear = v.Z - zoneHalf;
                        double frontAtTakeoff = _airStartZ + halfDepth;
                        if (zoneNear - frontAtTakeoff > _vines.GrabEarlinessS * _speed + 1e-6)
                        {
                            continue;
                        }
                    }

                    GrabVine(tick, in v);
                    break;
                }
            }

            double passLimit = _z - halfDepth - zoneHalf;
            if (passLimit > _vineScanZ)
            {
                int n = QueryVines(_vineScanZ, passLimit);
                for (int i = 0; i < n; i++)
                {
                    VineAnchor v = _vineBuffer[i];
                    if (!(v.Z > _vineScanZ) || v.Z > passLimit)
                    {
                        continue;
                    }

                    bool grabbedRow = v.Group == _grabbedGroup && v.Row == _grabbedRow;
                    bool reported = v.Group == _missedGroup && v.Row == _missedRow;
                    if (grabbedRow || reported || v.Id == _swingVineId)
                    {
                        continue;
                    }

                    _missedGroup = v.Group;
                    _missedRow = v.Row;
                    Emit(
                        RunnerEventType.VineMissed,
                        tick,
                        0,
                        (byte)v.Lane,
                        v.OverChasm ? RunnerEventFlags.VineOverChasm : (byte)0,
                        (short)v.Row,
                        v.Id,
                        ObstacleArchetype.None);
                }

                _vineScanZ = passLimit;
            }
        }

        private bool CanGrab()
        {
            switch (_locomotion)
            {
                case Locomotion.Airborne:
                    return true;
                case Locomotion.FastFalling:
                case Locomotion.Falling:
                    return _y > 0.0;
                default:
                    return false;
            }
        }

        private void GrabVine(long tick, in VineAnchor v)
        {
            // Rule I5: a buffered jump means something else on a vine; the lateral queue cannot fire either.
            if (_bufferedJump)
            {
                _bufferedJump = false;
                _counters.Resolve(CommandOutcome.Buffered, CommandOutcome.Invalidated);
            }

            if (_queuedDir != 0)
            {
                _queuedDir = 0;
                _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Invalidated);
            }

            if (!_launchedFromVine)
            {
                _chainPerfects = 0;
            }

            _moveActive = false;
            _moveElapsed = 0;
            _bounceActive = false;
            _slideOnLanding = false;
            _launchedFromVine = false;
            _fallGravity = _config.GravityMps2;

            _locomotion = Locomotion.Carried;
            _swingVineId = v.Id;
            _swingGroup = v.Group;
            _swingRow = v.Row;
            _swingLane = v.Lane;
            _swingOverChasm = v.OverChasm;
            _swingElapsed = 0;
            _swingGrabX = _x;
            _swingGrabY = _y;
            _targetLane = v.Lane;
            _aimLane = v.Lane;
            _grabbedGroup = v.Group;
            _grabbedRow = v.Row;
            _grabbedThisTick = true;
            RefreshAimTarget();

            Emit(
                RunnerEventType.VineGrabbed,
                tick,
                0,
                (byte)v.Lane,
                v.OverChasm ? RunnerEventFlags.VineOverChasm : (byte)0,
                (short)v.Row,
                v.Id,
                ObstacleArchetype.None);
        }

        /// <summary>The next vine of the current section in the aimed lane (lowest later row), if any.</summary>
        private void RefreshAimTarget()
        {
            _aimVineId = 0;
            _aimVineZ = 0.0;
            _aimVineOverChasm = false;
            int n = QueryVines(_z, _z + _vines.ChainMaxReachM);
            int bestRow = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                VineAnchor v = _vineBuffer[i];
                if (v.Group != _swingGroup || v.Row <= _swingRow || v.Lane != _aimLane || v.Row >= bestRow)
                {
                    continue;
                }

                bestRow = v.Row;
                _aimVineId = v.Id;
                _aimVineZ = v.Z;
                _aimVineOverChasm = v.OverChasm;
            }
        }

        private int QueryVines(double zMin, double zMax)
        {
            if (_vineTrack == null)
            {
                return 0;
            }

            int count = _vineTrack.GetVines(zMin, zMax, new Span<VineAnchor>(_vineBuffer));
            if (count >= _vineBuffer.Length)
            {
                BoxBufferOverflowCount++;
                count = _vineBuffer.Length;
            }

            return count;
        }

        /// <summary>
        /// GDD 7.4: a fall into the chasm under a vine HERO did not swing on is a "Missed vine" death; any other fall
        /// is <see cref="DeathCause.Fell"/>.
        /// </summary>
        private void DieFalling(long tick)
        {
            int n = QueryVines(_z - MissedVineLookBackM, _z + MissedVineLookAheadM);
            for (int i = 0; i < n; i++)
            {
                VineAnchor v = _vineBuffer[i];
                if (v.OverChasm && !(v.Group == _grabbedGroup && v.Row == _grabbedRow))
                {
                    Die(tick, DeathCause.MissedVine);
                    return;
                }
            }

            Die(tick, DeathCause.Fell);
        }

        // ---- Collisions (spec 001 section 9, spec 002 section 5) ----

        private void UpdateCollisions(long tick, float xStart, double yStart, double zStart, float heightStart, float heightEnd)
        {
            // Daze countdown (9.4.4). A stumble on tick s sets 180; ticks s+1 .. s+179 are still dazed.
            if (_dazeTicksLeft > 0)
            {
                _dazeTicksLeft--;
                if (_dazeTicksLeft == 0)
                {
                    Emit(RunnerEventType.DazeEnded, tick, 0, (byte)_targetLane, 0, 0);
                }
            }

            double halfWidth = _config.PlayerHitboxWidthM * 0.5;
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            HeroSweep sweep = HeroSweep.FromCenters(
                xStart,
                yStart,
                zStart,
                heightStart,
                _x,
                _y,
                _z,
                heightEnd,
                halfWidth,
                halfDepth);

            double zLo = (zStart < _z ? zStart : _z) - halfDepth;
            double zHi = (zStart > _z ? zStart : _z) + halfDepth;
            int count = QueryBoxes(zLo, zHi);
            // GDD 7.3 step 3: nothing can hit HERO on a vine (same handling as invulnerability).
            // GDD 15.1: nothing can hit HERO during Lift either.
            bool invulnerable = _invulnerableTicks > 0 || _locomotion == Locomotion.Carried || _locomotion == Locomotion.Lifted;
            int contacts = 0;

            for (int i = 0; i < count; i++)
            {
                int engaged = FindOrAddEngaged(in _boxes[i]);
                if (engaged >= 0)
                {
                    if (invulnerable)
                    {
                        _engagedFlags[engaged] |= EngagedInvulnerable;
                    }

                    if ((_engagedFlags[engaged] & EngagedContact) != 0)
                    {
                        // Stumbled on (or passed through while invulnerable): ignored for the rest of the pass.
                        continue;
                    }
                }

                bool forgiven = CollisionRules.IsEdgeForgiven(
                    xStart,
                    _x,
                    _boxes[i].CenterX,
                    _config.LaneWidthM * (double)_config.EdgeForgivenessFraction,
                    EdgeForgivenessToleranceM);

                if (!forgiven
                    && SweptAabb.TrySweep(in sweep, in _boxes[i], out double time, out ContactEntry entry, out bool lateral))
                {
                    InsertContact(contacts, i, time, entry, lateral);
                    contacts++;
                    continue;
                }

                if (engaged >= 0)
                {
                    double gap = SweptAabb.GapXY(in sweep, in _boxes[i]);
                    if (gap < _engagedMinGap[engaged])
                    {
                        _engagedMinGap[engaged] = gap;
                    }
                }
            }

            for (int c = 0; c < contacts; c++)
            {
                int boxIndex = _contactBox[c];
                int engaged = FindEngaged(_boxes[boxIndex].Id);
                if (engaged >= 0 && (_engagedFlags[engaged] & EngagedContact) != 0)
                {
                    // Another box of an obstacle already handled on this tick (multi-lane obstacles).
                    continue;
                }

                if (engaged >= 0)
                {
                    _engagedFlags[engaged] |= EngagedContact;
                }

                if (invulnerable)
                {
                    // 9.6: no death and no stumble; the obstacle is ignored for the rest of the pass. [ASSUMED]
                    // GDD 10: a boosting (or just-shielded) HERO smashes what he touches.
                    ContactHooks?.OnInvulnerableContact(this, tick, in _boxes[boxIndex]);
                    continue;
                }

                ContactEntry entry = _contactEntry[c];
                if (CollisionRules.IsLethal(entry))
                {
                    if (ContactHooks != null && ContactHooks.TryAbsorbLethalContact(this, tick, in _boxes[boxIndex]))
                    {
                        // GDD 10 Shield: the hit is absorbed; the rest of this tick is invulnerable.
                        invulnerable = true;
                        continue;
                    }

                    DieOnContact(tick, boxIndex, _contactTime[c], false, xStart, yStart, zStart);
                    return;
                }

                if (_stumbledThisTick)
                {
                    // A second clip on the very tick of a stumble is the same "you clipped it" moment: the obstacle
                    // is ignored for its pass (flag set above) and no second stumble is counted. Ties and doubt go
                    // to the player (pillar 2). [ASSUMED]
                    continue;
                }

                if (_dazeTicksLeft > 0)
                {
                    // 9.4.5: a second stumble while dazed is lethal ("Tripped twice"), unless a shield absorbs it.
                    if (ContactHooks != null && ContactHooks.TryAbsorbLethalContact(this, tick, in _boxes[boxIndex]))
                    {
                        invulnerable = true;
                        continue;
                    }

                    DieOnContact(tick, boxIndex, _contactTime[c], true, xStart, yStart, zStart);
                    return;
                }

                Stumble(tick, boxIndex, _contactLateral[c]);
            }

            UpdatePasses(tick, halfDepth);
        }

        /// <summary>Inserts a contact keeping the list sorted by entry time (stable: equal times keep box order).</summary>
        private void InsertContact(int count, int boxIndex, double time, ContactEntry entry, bool lateral)
        {
            int at = count;
            while (at > 0 && _contactTime[at - 1] > time)
            {
                _contactBox[at] = _contactBox[at - 1];
                _contactTime[at] = _contactTime[at - 1];
                _contactEntry[at] = _contactEntry[at - 1];
                _contactLateral[at] = _contactLateral[at - 1];
                at--;
            }

            _contactBox[at] = boxIndex;
            _contactTime[at] = time;
            _contactEntry[at] = entry;
            _contactLateral[at] = lateral;
        }

        private void DieOnContact(long tick, int boxIndex, double time, bool afterStumble, float xStart, double yStart, double zStart)
        {
            // Freeze HERO at the moment of contact, so the death pose is never drawn inside the obstacle.
            _x = (float)(xStart + (_x - xStart) * time);
            _y = yStart + (_y - yStart) * time;
            _z = zStart + (_z - zStart) * time;
            Die(tick, DeathCause.Hit, _boxes[boxIndex].Archetype, _boxes[boxIndex].Id, afterStumble);
        }

        private void Stumble(long tick, int boxIndex, bool lateral)
        {
            _stumbledThisTick = true;
            _dazeTicksLeft = _config.StumbleDazeTicks;
            sbyte bounceDir = 0;
            if (lateral)
            {
                bounceDir = StartBounce(_boxes[boxIndex].CenterX);
            }

            Emit(
                RunnerEventType.Stumbled,
                tick,
                bounceDir,
                (byte)_targetLane,
                CollisionRules.StumbleFlag(lateral),
                0,
                _boxes[boxIndex].Id,
                _boxes[boxIndex].Archetype);
        }

        /// <summary>
        /// 9.4.2 side stumble: if a lane move is heading toward the obstacle, cancel it and its queue and tween back
        /// to the origin lane center over <c>StumbleBounceTicks</c> with the lane ease-out. Returns the bounce
        /// direction (0 = no bounce). With no move toward the obstacle (for example a mover rolling into a standing
        /// HERO) there is no bounce and X is unchanged. [ASSUMED]
        /// </summary>
        private sbyte StartBounce(float obstacleCenterX)
        {
            if (!_moveActive)
            {
                return 0;
            }

            float side = obstacleCenterX - _x;
            bool towardObstacle = (side > 0f && _moveDir > 0) || (side < 0f && _moveDir < 0);
            if (!towardObstacle)
            {
                return 0;
            }

            int origin = _moveEndLane - _moveDir;
            sbyte dir = (sbyte)(-_moveDir);
            _moveActive = false;
            _moveElapsed = 0;
            _targetLane = origin;
            if (_queuedDir != 0)
            {
                _queuedDir = 0;
                _counters.Resolve(CommandOutcome.Queued, CommandOutcome.Cancelled);
            }

            _bounceActive = true;
            _bounceStartX = _x;
            _bounceEndLane = origin;
            _bounceElapsed = 0;
            return dir;
        }

        /// <summary>9.7: drops obstacles HERO has fully passed and emits NearMiss for the close, clean ones.</summary>
        private void UpdatePasses(long tick, double halfDepth)
        {
            double heroBack = _z - halfDepth;
            double threshold = _config.NearMissDistanceM + NearMissToleranceM;
            int write = 0;
            for (int i = 0; i < _engagedCount; i++)
            {
                if (heroBack > _engagedMaxZ[i])
                {
                    if ((_engagedFlags[i] & (EngagedContact | EngagedInvulnerable)) == 0 && _engagedMinGap[i] <= threshold)
                    {
                        _nearMissesThisTick++;
                        Emit(RunnerEventType.NearMiss, tick, 0, (byte)_targetLane, 0, 0, _engagedId[i], _engagedArchetype[i]);
                    }

                    continue;
                }

                if (write != i)
                {
                    _engagedId[write] = _engagedId[i];
                    _engagedMaxZ[write] = _engagedMaxZ[i];
                    _engagedMinGap[write] = _engagedMinGap[i];
                    _engagedFlags[write] = _engagedFlags[i];
                    _engagedArchetype[write] = _engagedArchetype[i];
                }

                write++;
            }

            _engagedCount = write;
        }

        private int FindEngaged(int id)
        {
            for (int i = 0; i < _engagedCount; i++)
            {
                if (_engagedId[i] == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindOrAddEngaged(in ObstacleBox box)
        {
            int index = FindEngaged(box.Id);
            if (index >= 0)
            {
                if (box.ZMax > _engagedMaxZ[index])
                {
                    _engagedMaxZ[index] = box.ZMax;
                }

                return index;
            }

            if (_engagedCount >= EngagedCapacity)
            {
                EngagedOverflowCount++;
                return -1;
            }

            index = _engagedCount++;
            _engagedId[index] = box.Id;
            _engagedMaxZ[index] = box.ZMax;
            _engagedMinGap[index] = double.PositiveInfinity;
            _engagedFlags[index] = 0;
            _engagedArchetype[index] = box.Archetype;
            return index;
        }

        private RunnerTickInfo BuildTickInfo(long tick, float xStart, double yStart, double zStart)
        {
            return new RunnerTickInfo
            {
                Tick = tick,
                X = _x,
                XPrev = xStart,
                Y = (float)_y,
                YPrev = (float)yStart,
                Z = _z,
                ZPrev = zStart,
                Speed = _speed,
                HitboxHeight = CurrentHitboxHeight(),
                HalfWidth = _config.PlayerHitboxWidthM * 0.5f,
                HalfDepth = _config.PlayerHitboxDepthM * 0.5f,
                OccupiedLane = ComputeOccupiedLane(),
                IsDead = _locomotion == Locomotion.Dead,
                StumbledThisTick = _stumbledThisTick,
                NearMissesThisTick = _nearMissesThisTick,
                VineRelease = _releaseThisTick,
                VineBonusMultiplier = _releaseMultiplierThisTick,
                VineGrabbedThisTick = _grabbedThisTick,
                Locomotion = _locomotion,
                InVineFlight = _launchedFromVine && _locomotion != Locomotion.Carried && _locomotion != Locomotion.Dead,
            };
        }

        // ---- Speed (section 4) ----

        private void UpdateSpeedAndDistance(long tick)
        {
            double speed;
            if (SpeedSource != null)
            {
                speed = SpeedSource.GetBaseSpeedMps(_z);
            }
            else
            {
                speed = TutorialActive ? _curve.TutorialSpeedMps : _curve.Evaluate(_z);
            }

            int ramp = _config.RunStartRampTicks;
            if (tick < ramp)
            {
                double f = _config.RunStartSpeedFraction;
                speed *= f + (1.0 - f) * tick / ramp;
            }

            speed *= SpeedMultiplier;
            _speed = speed;
            _z += speed * RunnerConfig.TickSeconds;

            while (_nextSpeedRow < _curve.RowCount && _z >= _curve.GetRowDistance(_nextSpeedRow))
            {
                Emit(RunnerEventType.SpeedStepReached, tick, 0, (byte)_targetLane, 0, (short)_nextSpeedRow);
                _nextSpeedRow++;
            }
        }

        // ---- Snapshot and events ----

        private void FinishTick(long tick)
        {
            Current = BuildSnapshot(tick);
            _tick = tick + 1;
        }

        private RunnerState BuildSnapshot(long tick)
        {
            float jumpPhase = 0f;
            if (_locomotion == Locomotion.Airborne)
            {
                jumpPhase = (float)((tick - _jumpStartTick) / (double)_config.JumpAirtimeTicks);
            }

            bool carried = _locomotion == Locomotion.Carried;
            return new RunnerState
            {
                Tick = tick,
                X = _x,
                Y = (float)_y,
                Z = _z,
                Speed = (float)_speed,
                Locomotion = _locomotion,
                TargetLane = _targetLane,
                OccupiedLane = ComputeOccupiedLane(),
                LaneMoveProgress = _moveActive ? (float)EaseProgress(_moveElapsed) : 1f,
                JumpPhase = jumpPhase,
                SlideTicksLeft = _locomotion == Locomotion.Sliding ? _slideTicksLeft : 0,
                DazeTicksLeft = _dazeTicksLeft,
                StumbleBounceActive = _bounceActive,
                InvulnerableTicks = _invulnerableTicks,
                HitboxHeight = CurrentHitboxHeight(),
                IsDead = _locomotion == Locomotion.Dead,
                SwingPhase = carried ? (float)_vines.PhaseAt(_swingElapsed) : 0f,
                SwingAngleRad = carried ? (float)_vines.SwingAngleAt(_swingElapsed) : 0f,
                SwingTick = carried ? _swingElapsed : 0,
                VineId = carried ? _swingVineId : 0,
                VineLane = carried ? _swingLane : -1,
                AimLane = carried ? _aimLane : -1,
                AimVineId = carried ? _aimVineId : 0,
                InVineFlight = _launchedFromVine && !carried && _locomotion != Locomotion.Dead,
                LastReleaseGrade = _lastReleaseGrade,
                ReleaseBuffered = _releaseBuffered,
            };
        }

        private void Emit(RunnerEventType type, long tick, sbyte dir, byte lane, byte flags, short value)
        {
            Emit(type, tick, dir, lane, flags, value, 0, ObstacleArchetype.None);
        }

        private void Emit(
            RunnerEventType type,
            long tick,
            sbyte dir,
            byte lane,
            byte flags,
            short value,
            int entityId,
            ObstacleArchetype archetype)
        {
            var e = new RunnerEvent
            {
                Type = type,
                Tick = tick,
                Dir = dir,
                Lane = lane,
                Flags = flags,
                EntityId = entityId,
                Archetype = (byte)archetype,
                Value = value,
            };
            _events.Add(e);
        }
    }
}
