using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Deterministic runner movement simulation, spec 001 sections 4 to 8 (collisions come with the obstacle stage).
    /// One call to <see cref="Step(InputCommand)"/> = one 60 Hz tick, processed in the order of rule I8.
    /// Plain C#: no UnityEngine time, randomness or physics; no allocations per step.
    /// </summary>
    public sealed class RunnerSimulation
    {
        /// <summary>Tolerance for "Y reached the surface" so float rounding never adds a tick to a fast-fall.</summary>
        private const double SurfaceEpsilonM = 1e-6;

        private readonly RunnerConfig _config;
        private readonly SpeedCurve _curve;
        private readonly ITrackQuery _track;
        private readonly IHeadroomQuery _headroom;
        private readonly RunnerEventBuffer _events;
        private readonly CommandOutcomeCounters _counters = new CommandOutcomeCounters();

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

        private TickOutcomes _lastOutcomes;

        public RunnerSimulation(
            RunnerConfig config,
            SpeedCurve speedCurve,
            ITrackQuery track = null,
            IHeadroomQuery headroom = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _curve = speedCurve ?? throw new ArgumentNullException(nameof(speedCurve));
            _track = track ?? FlatTrackQuery.Instance;
            _headroom = headroom ?? OpenHeadroomQuery.Instance;
            _events = new RunnerEventBuffer(config.EventBufferCapacity);

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

        public DeathCause DeathCause => _deathCause;

        public bool HasBufferedJump => _bufferedJump;

        public int QueuedLateral => _queuedDir;

        public int CoyoteTicksLeft => _locomotion == Locomotion.Coyote ? _coyoteTicksLeft : 0;

        /// <summary>Invulnerability hook (spec 9.6). Counts down by one per tick.</summary>
        public int InvulnerableTicks => _invulnerableTicks;

        public void SetInvulnerableTicks(int ticks)
        {
            _invulnerableTicks = Math.Max(0, ticks);
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

        /// <summary>Processes one 60 Hz tick (rule I8).</summary>
        public void Step(InputCommand commands)
        {
            long tick = _tick;
            Previous = Current;
            _lastOutcomes = default;

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

            // (7) Speed and z.
            UpdateSpeedAndDistance(tick);

            // (8) Lane tween and queued-move start.
            UpdateLaneMove(tick);

            // (9) Vertical motion, ground check, landing and buffered fire.
            UpdateVertical(tick);

            // (10) Slide timer.
            if (_locomotion == Locomotion.Sliding)
            {
                UpdateSlideTimer(tick);
            }

            // (11) Collisions: obstacle stage. Only the invulnerability countdown lives here for now.
            if (_invulnerableTicks > 0)
            {
                _invulnerableTicks--;
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
            return h;
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
        }

        // ---- Lanes (section 5) ----

        private CommandOutcome ProcessLateral(int dir, long tick)
        {
            // L1. (Dead is handled before any command processing.)
            if (_y < 0.0)
            {
                return CommandOutcome.Ignored;
            }

            // L2 (stumble bounce) arrives with the collision stage.

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
            if (_queuedDir != 0 && (!_moveActive || _moveElapsed >= _config.LaneQueueStartTick))
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
            double y = _fallStartY + _fallStartVelocity * t - 0.5 * _config.GravityMps2 * t * t;

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
                    BeginFall(tick, 0.0, _fallStartVelocity - _config.GravityMps2 * t);
                }

                return;
            }

            _y = y;
            if (_y <= -_config.FallDeathDepthM)
            {
                Die(tick, DeathCause.Fell);
            }
        }

        private void BeginFall(long tick, double startY, double startVelocity)
        {
            _locomotion = Locomotion.Falling;
            _fallStartTick = tick;
            _fallStartY = startY;
            _fallStartVelocity = startVelocity;
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
            float halfWidth = _config.PlayerHitboxWidthM * 0.5f;
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            if (_headroom.CanStand(_x - halfWidth, _x + halfWidth, _z - halfDepth, _z + halfDepth, _config.StandingHeightM))
            {
                _locomotion = Locomotion.Running;
                Emit(RunnerEventType.SlideEnded, tick, 0, (byte)_targetLane, 0, (short)SlideEndReason.Timeout);
            }
        }

        private bool HasGroundUnderHero()
        {
            float halfWidth = _config.PlayerHitboxWidthM * 0.5f;
            double halfDepth = _config.PlayerHitboxDepthM * 0.5;
            return _track.HasGround(_x - halfWidth, _x + halfWidth, _z - halfDepth, _z + halfDepth);
        }

        private void Die(long tick, DeathCause cause)
        {
            _locomotion = Locomotion.Dead;
            _deathCause = cause;
            _speed = 0.0;
            _moveActive = false;
            _slideOnLanding = false;

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

            Emit(RunnerEventType.Died, tick, 0, (byte)_targetLane, 0, (short)cause);
        }

        // ---- Speed (section 4) ----

        private void UpdateSpeedAndDistance(long tick)
        {
            double speed = TutorialActive ? _curve.TutorialSpeedMps : _curve.Evaluate(_z);
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
                DazeTicksLeft = 0,
                InvulnerableTicks = _invulnerableTicks,
                HitboxHeight = _locomotion == Locomotion.Sliding ? _config.SlidingHeightM : _config.StandingHeightM,
                IsDead = _locomotion == Locomotion.Dead,
            };
        }

        private void Emit(RunnerEventType type, long tick, sbyte dir, byte lane, byte flags, short value)
        {
            var e = new RunnerEvent
            {
                Type = type,
                Tick = tick,
                Dir = dir,
                Lane = lane,
                Flags = flags,
                ObstacleId = 0,
                Archetype = 0,
                Value = value,
            };
            _events.Add(e);
        }
    }
}
