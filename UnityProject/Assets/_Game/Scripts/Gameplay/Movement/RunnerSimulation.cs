using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Deterministic runner simulation (spec 101 §2 and §4): forward speed, steering servo with soft edges and dodge,
    /// jump/coyote/buffer/apex hang, slide/fast-fall/ceiling rules, ground and ledge handling, collisions, health,
    /// i-frames, stumble, shield and revive. Plain C#, fixed step, no allocation in <see cref="Step"/>.
    ///
    /// Order inside one step (spec 101 §2.1): (1) read input, (2) resolve the discrete command (buffer, coyote,
    /// ceiling hold), (3) lateral, (4) vertical and ground/ledge, (5) forward, (6) collisions and coins,
    /// (7) timers, (8) events. Timers are tick stamps; a slide that started on tick T is active on ticks
    /// T … T + slideTicks − 1 and its expiry is evaluated at the start of the next tick.
    /// </summary>
    public sealed class RunnerSimulation
    {
        private const int QueryCapacity = 32;
        private const int SafeRingSize = 64;
        private const long Never = long.MinValue / 4;

        private readonly MovementConfig _config;
        private readonly IPathQuery _path;
        private readonly SpeedCurve _speedCurve;
        private readonly float _dt;
        private readonly int[] _ids = new int[QueryCapacity];
        // Per-item state keyed by id % slots and stamped with id + 1 (0 = none), so a streamed path can recycle
        // slots without the simulation clearing anything (IPathQuery ids).
        private readonly int[] _resolved;
        private readonly int[] _removed;
        private readonly int[] _collected;
        private readonly float[] _safeS = new float[SafeRingSize];
        private readonly float[] _safeX = new float[SafeRingSize];
        private readonly float[] _safeY = new float[SafeRingSize];

        private readonly int _coyoteTicks;
        private readonly int _bufferTicks;
        private readonly int _ceilingHoldTicks;
        private readonly int _slideTicks;
        private readonly int _dodgeBoostTicks;
        private readonly int _invulnerableTicks;
        private readonly int _shieldTicks;
        private readonly int _reviveInvulnerableTicks;
        private readonly int _startRampTicks;
        private readonly int _stumbleTicks;
        private readonly int _reviveRampTicks;
        private readonly int _ftueTicks;
        private readonly int _crashStopTicks;

        private RunnerState _state;
        private RunnerState _previous;
        private RunOptions _options;
        private int _safeCount;
        private int _safeHead;

        public RunnerSimulation(MovementConfig config, IPathQuery path, float stepSeconds, RunEventBuffer events)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            if (!(stepSeconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            }

            _dt = stepSeconds;
            Events = events ?? new RunEventBuffer(128);
            _speedCurve = new SpeedCurve(config.Speed);
            _resolved = new int[Math.Max(1, path.ObstacleSlots)];
            _removed = new int[Math.Max(1, path.ObstacleSlots)];
            _collected = new int[Math.Max(1, path.CoinSlots)];

            JumpSlideConfig js = config.JumpSlide;
            _coyoteTicks = ToTicks(js.CoyoteTime);
            _bufferTicks = ToTicks(js.InputBufferTime);
            _ceilingHoldTicks = ToTicks(js.CeilingHoldTime);
            _slideTicks = Math.Max(1, ToTicks(js.SlideDuration));
            _dodgeBoostTicks = ToTicks(config.Lateral.DodgeBoostTime);
            _invulnerableTicks = ToTicks(config.Health.InvulnerableTime);
            _shieldTicks = ToTicks(config.Health.ShieldInvulnerableTime);
            _reviveInvulnerableTicks = ToTicks(config.Health.ReviveInvulnerableTime);
            _startRampTicks = ToTicks(config.Speed.StartRampTime);
            _stumbleTicks = ToTicks(config.Speed.StumbleRecoverTime);
            _reviveRampTicks = ToTicks(config.Speed.ReviveRampTime);
            _ftueTicks = ToTicks(config.Health.FtueHealthFloorTime);
            _crashStopTicks = Math.Max(1, ToTicks(config.Health.CrashStopTime));
            Reset(RunOptions.Default);
        }

        public MovementConfig Config => _config;

        public IPathQuery Path => _path;

        public RunEventBuffer Events { get; }

        public float StepSeconds => _dt;

        /// <summary>State after the last step.</summary>
        public ref readonly RunnerState State => ref _state;

        /// <summary>State before the last step (for interpolation).</summary>
        public ref readonly RunnerState Previous => ref _previous;

        public RunOptions Options => _options;

        /// <summary>When true, no events are written (bot look-ahead probes).</summary>
        public bool MuteEvents { get; set; }

        public int SlideTicks => _slideTicks;

        public int CoyoteTicks => _coyoteTicks;

        public int BufferTicks => _bufferTicks;

        public int CeilingHoldTicks => _ceilingHoldTicks;

        public int InvulnerableTicks => _invulnerableTicks;

        public int StartRampTicks => _startRampTicks;

        public int StumbleTicks => _stumbleTicks;

        public bool IsObstacleResolved(int id)
        {
            return Has(_resolved, id);
        }

        /// <summary>Removed by a revive: no longer exists for the simulation (views hide it).</summary>
        public bool IsObstacleRemoved(int id)
        {
            return Has(_removed, id);
        }

        public bool IsCoinCollected(int id)
        {
            return Has(_collected, id);
        }

        /// <summary>Current hitbox height (posture), m.</summary>
        public float HitboxHeight
        {
            get
            {
                HitboxConfig hb = _config.Hitbox;
                if (_state.Sliding)
                {
                    return hb.SlideHeight;
                }

                return _state.Grounded ? hb.RunHeight : hb.AirHeight;
            }
        }

        public float HitboxDepth => _state.Sliding ? _config.Hitbox.SlideDepth : _config.Hitbox.RunDepth;

        /// <summary>Starts a new run. Allocation-free.</summary>
        public void Reset(RunOptions options)
        {
            _options = options;
            Array.Clear(_resolved, 0, _resolved.Length);
            Array.Clear(_removed, 0, _removed.Length);
            Array.Clear(_collected, 0, _collected.Length);
            _safeCount = 0;
            _safeHead = 0;

            _state = default;
            _state.S = options.StartS;
            _state.X = options.StartX;
            _state.XTarget = options.StartX;
            _state.Grounded = true;
            _state.Health = _config.Health.MaxHealth;
            _state.SlideEndTick = Never;
            _state.DodgeBoostUntilTick = Never;
            _state.InvulnerableUntilTick = Never;
            _state.StumbleTick = Never;
            _state.ReviveTick = Never;
            _state.AirborneSinceTick = Never;
            _state.DeathObstacle = -1;
            _state.LastHitObstacle = -1;
            _state.NudgedFork = -1;
            if (_path.TryGetFloor(_state.S, _state.X, out float floor))
            {
                _state.Y = floor;
                _state.GroundY = floor;
                _state.LastGroundY = floor;
            }

            _state.Speed = options.SkipStartRamp ? BaseSpeed() : 0f;
            _previous = _state;
            RecordSafePoint(true);
        }

        /// <summary>Copies the full state of another simulation on the same path (bot look-ahead). No allocation.</summary>
        public void CopyFrom(RunnerSimulation other)
        {
            if (other._path != _path)
            {
                throw new ArgumentException("Both simulations must share the same path.", nameof(other));
            }

            _state = other._state;
            _previous = other._previous;
            _options = other._options;
            Array.Copy(other._resolved, _resolved, _resolved.Length);
            Array.Copy(other._removed, _removed, _removed.Length);
            Array.Copy(other._collected, _collected, _collected.Length);
            Array.Copy(other._safeS, _safeS, SafeRingSize);
            Array.Copy(other._safeX, _safeX, SafeRingSize);
            Array.Copy(other._safeY, _safeY, SafeRingSize);
            _safeCount = other._safeCount;
            _safeHead = other._safeHead;
        }

        /// <summary>Tests only: overwrite the runner state to set up an exact scenario.</summary>
        internal void SetStateForTest(in RunnerState state)
        {
            _state = state;
            _previous = state;
        }

        /// <summary>Power-up hook: the next Minor or Crash is absorbed (spec 101 §4.2). No time limit.</summary>
        public void GrantShield()
        {
            _state.Shield = true;
            _state.ShieldUntilTick = 0;
        }

        /// <summary>
        /// Shield power-up (spec 103 §9.3): absorbs the next Minor, Bump or Crash (not Fall) and expires after
        /// <paramref name="durationTicks"/> (emits <see cref="RunEventType.ShieldExpired"/>).
        /// </summary>
        public void GrantShield(int durationTicks)
        {
            _state.Shield = true;
            _state.ShieldUntilTick = durationTicks > 0 ? _state.Tick + durationTicks : 0;
        }

        /// <summary>FTUE window active (first run, first 60 s).</summary>
        public bool HealthFloorActive => _options.FirstRun && _state.Tick <= _ftueTicks;

        public bool CanJumpFromGround(long tick)
        {
            return _state.Grounded || InCoyote(tick);
        }

        /// <summary>Advances one fixed step.</summary>
        public void Step(InputFrame frame)
        {
            _previous = _state;
            _state.Tick++;
            long t = _state.Tick;

            if (_state.Dead)
            {
                StepDying(t);
                return;
            }

            if (t == 1)
            {
                Emit(RunEventType.RunStarted, -1, 0, 0f);
            }

            // Slide timer expiry (with the ceiling guard) takes effect at the start of the tick.
            if (_state.Sliding && t >= _state.SlideEndTick && !CeilingOverStanding())
            {
                EndSlide();
            }

            // (1) Input. TouchBegan is recorded for analysis only; the dodge no longer uses a gesture origin.
            InputCommand discrete = InputCommands.Discrete(frame.Commands);

            // (2) Discrete command resolution.
            ResolveCeilingHold(t);
            if (discrete == InputCommand.Jump)
            {
                HandleJump(t);
            }
            else if (discrete == InputCommand.Slide)
            {
                HandleSlide(t);
            }

            // (3) Lateral.
            int dodgeDir = discrete == InputCommand.DodgeLeft ? -1 : discrete == InputCommand.DodgeRight ? 1 : 0;
            StepLateral(t, frame.LateralDeltaM, dodgeDir);

            // (4) Vertical, ground and ledge.
            StepVertical(t);
            if (_state.Dead)
            {
                _state.S += _state.Speed * _dt;
                return;
            }

            // (5) Forward.
            float speed = CurrentSpeed(t);
            _state.Speed = speed;
            float ds = speed * _dt;
            _state.S += ds;
            _state.Distance += ds;

            // (6) Collisions and pickups. A pickup and a fatal hit on the same tick both count (AC-103-35).
            StepCollisions(t);
            StepCoins();
            if (_state.Dead)
            {
                return;
            }

            // (7) Timers.
            StepTimers(t, ds);
        }

        /// <summary>
        /// Revive (spec 101 §4.2): full health at the last safe ground ≥ reviveBackDistance before the hazard, obstacles
        /// in the next reviveClearDistance removed, i-frames and the revive speed ramp. Returns false if not dead.
        /// </summary>
        public bool Revive()
        {
            if (!_state.Dead)
            {
                return false;
            }

            HealthConfig health = _config.Health;
            float hazardS = _state.S;
            if (_state.Cause == DeathCause.Fall)
            {
                // The hazard is where she left the ground; the dying fall carried s forward.
                hazardS = Math.Min(hazardS, LastGroundedS());
            }

            // The most recent safe point far enough back whose (clamped) spot still has floor.
            float limit = hazardS - health.ReviveBackDistance;
            float s = Math.Max(0f, limit);
            float x = 0f;
            float y = 0f;
            bool found = false;
            for (int i = 0; i < _safeCount && !found; i++)
            {
                int index = (_safeHead - 1 - i + SafeRingSize) % SafeRingSize;
                if (_safeS[index] <= limit)
                {
                    float cx = ClampToPath(_safeS[index], _safeX[index]);
                    if (_path.TryGetFloor(_safeS[index], cx, out float floor))
                    {
                        s = _safeS[index];
                        x = cx;
                        y = floor;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                x = ClampToPath(s, 0f);
                _path.TryGetFloor(s, x, out y);
            }

            int n = _path.FindObstacles(s - 1f, s + health.ReviveClearDistance, _ids);
            for (int i = 0; i < n; i++)
            {
                Mark(_removed, _ids[i]);
            }

            long t = _state.Tick;
            _state.Dead = false;
            _state.Cause = DeathCause.None;
            _state.DeathObstacle = -1;
            _state.S = s;
            _state.X = x;
            _state.XTarget = x;
            _state.VLat = 0f;
            _state.Y = y;
            _state.Vy = 0f;
            _state.GroundY = y;
            _state.LastGroundY = y;
            _state.Grounded = true;
            _state.Sliding = false;
            _state.FastFalling = false;
            _state.Jumped = false;
            _state.BelowLip = false;
            _state.Buffered = InputCommand.None;
            _state.Health = health.MaxHealth;
            _state.RegenProgress = 0f;
            _state.InvulnerableUntilTick = t + _reviveInvulnerableTicks;
            _state.ReviveTick = t;
            _state.StumbleTick = Never;
            _state.Revives++;
            _previous = _state;
            Emit(RunEventType.Revived, -1, 0, 0f);
            return true;
        }

        /// <summary>True if a High obstacle overlaps the standing hitbox (ceiling guard / ceiling hold).</summary>
        public bool CeilingOverStanding()
        {
            HitboxConfig hb = _config.Hitbox;
            float halfDepth = hb.RunDepth * 0.5f;
            float halfWidth = hb.Width * 0.5f;
            int n = _path.FindObstacles(_state.S - halfDepth, _state.S + halfDepth, _ids);
            for (int i = 0; i < n; i++)
            {
                int id = _ids[i];
                if (Has(_removed, id))
                {
                    continue;
                }

                ObstacleBox box = _path.GetObstacle(id);
                if (box.Class != ObstacleClass.High)
                {
                    continue;
                }

                EffectiveBox(box, out float ex0, out float ex1, out float ey0, out float ey1);
                if (box.SMin < _state.S + halfDepth && box.SMax > _state.S - halfDepth &&
                    ex0 < _state.X + halfWidth && ex1 > _state.X - halfWidth &&
                    ey0 < _state.Y + hb.RunHeight && ey1 > _state.Y)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The forgiving hitbox of an authored obstacle (spec 101 §2.6).</summary>
        public void EffectiveBox(in ObstacleBox box, out float x0, out float x1, out float y0, out float y1)
        {
            HitboxConfig hb = _config.Hitbox;
            x0 = box.XMin + hb.ObstacleShrinkX;
            x1 = box.XMax - hb.ObstacleShrinkX;
            if (x1 < x0)
            {
                float c = box.CenterX;
                x0 = c;
                x1 = c;
            }

            y0 = box.YMin;
            y1 = box.YMax;
            if (box.Class == ObstacleClass.Low)
            {
                y1 -= hb.LowTopForgiveness;
            }
            else if (box.Class == ObstacleClass.High)
            {
                y0 += hb.HighBottomForgiveness;
            }
        }

        private static bool Has(int[] stamps, int id)
        {
            return id >= 0 && stamps[id % stamps.Length] == id + 1;
        }

        private static void Mark(int[] stamps, int id)
        {
            stamps[id % stamps.Length] = id + 1;
        }

        private int ToTicks(float seconds)
        {
            return Math.Max(0, (int)Math.Round(seconds / _dt));
        }

        private bool InCoyote(long tick)
        {
            return !_state.Grounded && !_state.Jumped && !_state.BelowLip && tick - _state.AirborneSinceTick <= _coyoteTicks;
        }

        private void HandleJump(long t)
        {
            if (CanJumpFromGround(t))
            {
                if (CeilingOverStanding())
                {
                    BufferCommand(InputCommand.Jump, DropReason.Ceiling, t);
                }
                else
                {
                    DoJump(t);
                }
            }
            else
            {
                BufferCommand(InputCommand.Jump, DropReason.Buffer, t);
            }
        }

        private void HandleSlide(long t)
        {
            // Latest intent wins: a slide replaces any held jump.
            _state.Buffered = InputCommand.None;
            if (_state.Grounded)
            {
                if (_state.Sliding)
                {
                    _state.SlideEndTick = t + _slideTicks;
                }
                else
                {
                    StartSlide(t);
                }
            }
            else if (InCoyote(t))
            {
                StartSlide(t);
            }
            else
            {
                if (_state.Vy > -_config.JumpSlide.FastFallSpeed)
                {
                    _state.Vy = -_config.JumpSlide.FastFallSpeed;
                }

                _state.FastFalling = true;
                Emit(RunEventType.FastFall, -1, 0, 0f);
            }
        }

        private void ResolveCeilingHold(long t)
        {
            if (_state.Buffered == InputCommand.Jump && _state.BufferedKind == DropReason.Ceiling &&
                CanJumpFromGround(t) && !CeilingOverStanding())
            {
                DoJump(t);
            }
        }

        private void BufferCommand(InputCommand command, DropReason kind, long t)
        {
            _state.Buffered = command;
            _state.BufferedKind = kind;
            _state.BufferedTick = t;
        }

        private void DoJump(long t)
        {
            if (_state.Sliding)
            {
                EndSlide();
            }

            if (_state.Grounded)
            {
                _state.LastGroundY = _state.GroundY;
                _state.AirPeakY = _state.Y;
            }

            _state.Vy = _config.JumpSlide.JumpVelocity;
            _state.Grounded = false;
            _state.Jumped = true;
            _state.FastFalling = false;
            _state.AirborneSinceTick = t;
            _state.Buffered = InputCommand.None;
            Emit(RunEventType.Jump, -1, 0, 0f);
        }

        private void StartSlide(long t)
        {
            _state.Sliding = true;
            _state.SlideEndTick = t + _slideTicks;
            Emit(RunEventType.SlideStart, -1, 0, 0f);
        }

        private void EndSlide()
        {
            _state.Sliding = false;
            Emit(RunEventType.SlideEnd, -1, 0, 0f);
        }

        private void StepLateral(long t, float delta, int dodgeDir)
        {
            LateralMovementConfig lat = _config.Lateral;
            _path.GetLateralBounds(_state.S, _state.X, out float bMin, out float bMax);
            float xMin = bMin + lat.EdgeMargin;
            float xMax = bMax - lat.EdgeMargin;
            if (xMin > xMax)
            {
                float mid = (xMin + xMax) * 0.5f;
                xMin = mid;
                xMax = mid;
            }

            // 1. Target (clamped itself: no debt past the edge).
            _state.XTarget = Clamp(_state.XTarget + delta, xMin, xMax);
            ApplyForkNudge(t, xMin, xMax);

            if (dodgeDir != 0)
            {
                // [ASSUMED 2026-10-09] The dodge moves dodgeDistance from where Pista is now and replaces any steering
                // target still pending (in either direction), so a flick after a drag never throws her further.
                _state.XTarget = Clamp(_state.X + (dodgeDir * lat.DodgeDistance), xMin, xMax);
                _state.DodgeBoostUntilTick = t + _dodgeBoostTicks - 1;
                Emit(RunEventType.Dodge, -1, (byte)(dodgeDir > 0 ? 1 : 0), 0f);
            }

            // 2–3. Servo.
            bool boost = t <= _state.DodgeBoostUntilTick;
            float factor = _state.Grounded ? (_state.Sliding ? lat.SlideLateralFactor : 1f) : lat.AirLateralFactor;
            float vMax = (boost ? lat.DodgeVLatMax : lat.VLatMax) * factor;
            float accel = (boost ? lat.DodgeAccel : lat.AccelLat) * factor;
            float decel = lat.DecelLat * factor;
            float vDes = Clamp((_state.XTarget - _state.X) / lat.Tau, -vMax, vMax);
            float v = _state.VLat;
            float rate;
            if (v == 0f || (v > 0f) == (vDes > 0f))
            {
                rate = Math.Abs(vDes) > Math.Abs(v) ? accel : decel;
            }
            else
            {
                rate = decel;
            }

            v = MoveTowards(v, vDes, rate * _dt);

            // 4. Soft edge.
            if (v > 0f && _state.X > xMax - lat.SoftZone)
            {
                float pen = Clamp01((_state.X - (xMax - lat.SoftZone)) / lat.SoftZone);
                v *= 1f + ((lat.SoftEdgeMinFactor - 1f) * pen);
            }
            else if (v < 0f && _state.X < xMin + lat.SoftZone)
            {
                float pen = Clamp01(((xMin + lat.SoftZone) - _state.X) / lat.SoftZone);
                v *= 1f + ((lat.SoftEdgeMinFactor - 1f) * pen);
            }

            // 5–6. Integrate, hard clamp, narrowing push.
            float x = _state.X;
            if (x > xMax)
            {
                x = Math.Max(xMax, x - (lat.NarrowPushSpeed * _dt));
                v = 0f;
            }
            else if (x < xMin)
            {
                x = Math.Min(xMin, x + (lat.NarrowPushSpeed * _dt));
                v = 0f;
            }
            else
            {
                x += v * _dt;
                if (x > xMax)
                {
                    x = xMax;
                    v = 0f;
                }
                else if (x < xMin)
                {
                    x = xMin;
                    v = 0f;
                }
            }

            _state.X = x;
            _state.VLat = v;

            bool pushRight = delta > 0f || dodgeDir > 0 || vDes > 0f;
            bool pushLeft = delta < 0f || dodgeDir < 0 || vDes < 0f;
            if ((pushRight && x >= xMax - lat.EdgeBrushDistance) || (pushLeft && x <= xMin + lat.EdgeBrushDistance))
            {
                if ((pushRight && delta > 0f) || (pushLeft && delta < 0f) || dodgeDir != 0 || Math.Abs(vDes) > 0.01f)
                {
                    Emit(RunEventType.EdgeBrush, -1, (byte)(x > 0f ? 1 : 0), 0f);
                }
            }
        }

        private void ApplyForkNudge(long t, float xMin, float xMax)
        {
            LateralMovementConfig lat = _config.Lateral;
            float halfWidth = _config.Hitbox.Width * 0.5f;
            for (int i = 0; i < _path.ForkCount; i++)
            {
                ForkPoint fork = _path.GetFork(i);
                if (_state.S < fork.SFront - lat.ForkNudgeLookahead || _state.S >= fork.SFront)
                {
                    continue;
                }

                float c = fork.DividerCenterX;
                float free = fork.DividerHalfWidth + lat.EdgeMargin;
                if (Math.Abs(_state.X - c) >= fork.DividerHalfWidth + halfWidth + lat.ForkNudgeZone ||
                    Math.Abs(_state.XTarget - c) >= free)
                {
                    continue;
                }

                int side = _state.XTarget > c ? 1 : _state.XTarget < c ? -1 : _state.X > c ? 1 : _state.X < c ? -1 : fork.SafeSide;
                _state.XTarget = Clamp(c + (side * free), xMin, xMax);
                _state.DodgeBoostUntilTick = t + _dodgeBoostTicks - 1;
                int forkId = fork.Id >= 0 ? fork.Id : i;
                if (_state.NudgedFork != forkId)
                {
                    _state.NudgedFork = forkId;
                    Emit(RunEventType.ForkNudge, forkId, 0, 0f);
                }
            }
        }

        private void StepVertical(long t)
        {
            JumpSlideConfig js = _config.JumpSlide;
            if (_state.Grounded)
            {
                if (TryGetSupport(_state.S, _state.X, _state.Y, true, out float floor))
                {
                    float rise = floor - _state.Y;
                    if (rise > js.StepUpHeight)
                    {
                        // A rise above stepUpHeight is a wall, not a step: minor hit, then she clambers up (the
                        // simulation has no blocking floor). Chunks author such rises as obstacles (spec 102).
                        _state.Y = floor;
                        _state.GroundY = floor;
                        ApplyMinorHit(t, -1, HitKind.Wall);
                    }
                    else if (rise >= -js.StepDownSnap)
                    {
                        // Walk up a step ≤ stepUpHeight, or snap down a drop ≤ stepDownSnap.
                        _state.Y = floor;
                        _state.GroundY = floor;
                    }
                    else
                    {
                        LeaveGround(t);
                    }
                }
                else
                {
                    LeaveGround(t);
                }

                return;
            }

            float yPrev = _state.Y;
            float y = _state.Y;
            float vy = _state.Vy;
            VerticalMotion.Integrate(js, ref y, ref vy, _state.FastFalling, _dt);
            _state.Y = y;
            _state.Vy = vy;
            if (y > _state.AirPeakY)
            {
                _state.AirPeakY = y;
            }

            bool hasFloor = false;
            if (!_state.BelowLip)
            {
                hasFloor = TryGetSupport(_state.S, _state.X, yPrev, vy <= 0f, out float floor);
                if (hasFloor)
                {
                    if (y <= floor)
                    {
                        if (yPrev >= floor - js.LedgeAssistDrop)
                        {
                            Land(t, floor);
                            return;
                        }

                        _state.BelowLip = true;
                    }
                }
                else if (vy <= 0f && _path.TryFindFloorAhead(_state.S, _state.X, js.LedgeAssistReach, out float lipS, out float lipY))
                {
                    if (y < lipY && y >= lipY - js.LedgeAssistDrop)
                    {
                        _state.S = lipS;
                        Land(t, lipY);
                        Emit(RunEventType.LedgeAssist, -1, 0, 0f);
                        return;
                    }

                    if (y < lipY - js.LedgeAssistDrop)
                    {
                        _state.BelowLip = true;
                    }
                }
            }
            else
            {
                hasFloor = false;
            }

            if ((_state.BelowLip || !hasFloor) && y < _state.LastGroundY - js.FallKillDepth)
            {
                Die(t, DeathCause.Fall, -1);
            }
        }

        private void LeaveGround(long t)
        {
            _state.Grounded = false;
            _state.Jumped = false;
            _state.Vy = 0f;
            _state.AirborneSinceTick = t;
            _state.LastGroundY = _state.GroundY;
            _state.AirPeakY = _state.Y;
        }

        private void Land(long t, float floor)
        {
            JumpSlideConfig js = _config.JumpSlide;
            float fall = _state.AirPeakY - floor;
            _state.Y = floor;
            _state.Vy = 0f;
            _state.Grounded = true;
            _state.Jumped = false;
            _state.BelowLip = false;
            _state.GroundY = floor;
            _state.LastGroundY = floor;
            LandingKind kind = fall >= js.HardLandingFall ? LandingKind.Hard : fall >= js.SoftLandingFall ? LandingKind.Soft : LandingKind.Light;
            Emit(RunEventType.Land, -1, (byte)kind, fall);

            // Landing order (spec 101 §2.7): land → auto-slide (fast-fall) → buffered command.
            if (_state.FastFalling)
            {
                _state.FastFalling = false;
                if (_state.Sliding)
                {
                    _state.SlideEndTick = t + _slideTicks;
                }
                else
                {
                    StartSlide(t);
                }
            }

            if (_state.Buffered == InputCommand.Jump)
            {
                if (CeilingOverStanding())
                {
                    BufferCommand(InputCommand.Jump, DropReason.Ceiling, t);
                }
                else
                {
                    DoJump(t);
                }
            }
        }

        /// <summary>
        /// Floor under (s, x): the path floor or a walkable top the feet are on (feet ≥ effective top − tolerance,
        /// not rising).
        /// </summary>
        private bool TryGetSupport(float s, float x, float feetY, bool notRising, out float floor)
        {
            bool has = _path.TryGetFloor(s, x, out floor);
            if (!notRising && !_state.Grounded)
            {
                return has;
            }

            HitboxConfig hb = _config.Hitbox;
            int n = _path.FindObstacles(s, s, _ids);
            for (int i = 0; i < n; i++)
            {
                int id = _ids[i];
                if (Has(_removed, id))
                {
                    continue;
                }

                ObstacleBox box = _path.GetObstacle(id);
                if (!box.WalkableTop || box.Class != ObstacleClass.Low || x < box.XMin || x > box.XMax || s < box.SMin || s > box.SMax)
                {
                    continue;
                }

                float effectiveTop = box.YMax - hb.LowTopForgiveness;
                if (feetY >= effectiveTop - hb.WalkableTopTolerance && (!has || box.YMax > floor))
                {
                    floor = box.YMax;
                    has = true;
                }
            }

            return has;
        }

        private float BaseSpeed()
        {
            return _options.ForcedSpeed > 0f ? _options.ForcedSpeed : _speedCurve.Evaluate(_state.Distance);
        }

        private float CurrentSpeed(long t)
        {
            RunSpeedConfig cfg = _config.Speed;
            float speed = BaseSpeed();
            if (!_options.SkipStartRamp && _startRampTicks > 0 && t < _startRampTicks)
            {
                float u = 1f - ((float)t / _startRampTicks);
                speed *= 1f - (u * u);
            }

            if (_state.StumbleTick != Never)
            {
                long k = t - _state.StumbleTick;
                if (k < _stumbleTicks)
                {
                    float p = _stumbleTicks > 0 ? (float)k / _stumbleTicks : 1f;
                    speed *= cfg.StumbleSpeedFactor + ((1f - cfg.StumbleSpeedFactor) * p);
                }
            }

            if (_state.ReviveTick != Never)
            {
                long k = t - _state.ReviveTick;
                if (k < _reviveRampTicks)
                {
                    float p = _reviveRampTicks > 0 ? (float)k / _reviveRampTicks : 1f;
                    speed *= cfg.ReviveRampFrom + ((1f - cfg.ReviveRampFrom) * p);
                }
            }

            return speed;
        }

        private void StepCollisions(long t)
        {
            HitboxConfig hb = _config.Hitbox;
            float halfDepth = HitboxDepth * 0.5f;
            float height = HitboxHeight;
            float halfWidth = hb.Width * 0.5f;
            float s0 = _state.S - halfDepth;
            float s1 = _state.S + halfDepth;
            float x0 = _state.X - halfWidth;
            float x1 = _state.X + halfWidth;
            float y0 = _state.Y;
            float y1 = _state.Y + height;

            int majorId = -1;
            int minorId = -1;
            HitKind minorKind = HitKind.None;
            ObstacleBox minorBox = default;
            bool invulnerable = t <= _state.InvulnerableUntilTick;

            int n = _path.FindObstacles(s0, s1, _ids);
            for (int i = 0; i < n; i++)
            {
                int id = _ids[i];
                if (Has(_resolved, id) || Has(_removed, id))
                {
                    continue;
                }

                ObstacleBox box = _path.GetObstacle(id);
                EffectiveBox(box, out float ex0, out float ex1, out float ey0, out float ey1);
                if (!(box.SMin < s1 && box.SMax > s0 && ex0 < x1 && ex1 > x0 && ey0 < y1 && ey1 > y0))
                {
                    continue;
                }

                Mark(_resolved, id);

                // Walkable top: feet near the top and not rising → it becomes floor.
                if (box.Class == ObstacleClass.Low && box.WalkableTop && _state.Y >= ey1 - hb.WalkableTopTolerance && _state.Vy <= 0f)
                {
                    if (_state.Grounded)
                    {
                        _state.Y = box.YMax;
                        _state.GroundY = box.YMax;
                    }
                    else
                    {
                        Land(t, box.YMax);
                    }

                    Emit(RunEventType.WalkableLanding, id, 0, 0f);
                    continue;
                }

                if (invulnerable)
                {
                    continue;
                }

                HitKind kind = Classify(box, ex0, ex1);
                if (kind == HitKind.Crash)
                {
                    if (majorId < 0)
                    {
                        majorId = id;
                    }
                }
                else if (minorId < 0)
                {
                    minorId = id;
                    minorKind = kind;
                    minorBox = box;
                }
            }

            if (majorId >= 0)
            {
                if (_state.Shield)
                {
                    ConsumeShield(t, majorId, HitKind.Crash);
                    return;
                }

                if (HealthFloorActive)
                {
                    // [ASSUMED 2026-10-09] First-run FTUE window: "hits never end the run" (GDD §16), so a frontal
                    // crash is downgraded to a side-clip (pushed to the free side, −1 segment, floored at 1).
                    ObstacleBox crashed = _path.GetObstacle(majorId);
                    Mark(_resolved, majorId);
                    ApplySideClip(t, crashed);
                    ApplyMinorHit(t, majorId, HitKind.SideClip);
                    return;
                }

                _state.LastHitObstacle = majorId;
                _state.LastHitKind = HitKind.Crash;
                _state.Hits++;
                Emit(RunEventType.Hit, majorId, (byte)HitKind.Crash, 0f);
                Die(t, DeathCause.Crash, majorId);
                return;
            }

            if (minorId < 0)
            {
                return;
            }

            if (minorKind == HitKind.SideClip)
            {
                ApplySideClip(t, minorBox);
            }

            ApplyMinorHit(t, minorId, minorKind);
        }

        /// <summary>A minor hit (spec 101 §4.2): shield, else −1 segment (FTUE floor), stumble and i-frames.</summary>
        private void ApplyMinorHit(long t, int id, HitKind kind)
        {
            if (t <= _state.InvulnerableUntilTick)
            {
                return;
            }

            if (_state.Shield)
            {
                ConsumeShield(t, id, kind);
                return;
            }

            int health = _state.Health - 1;
            if (HealthFloorActive && health < 1)
            {
                health = 1;
            }

            _state.Health = health;
            _state.Hits++;
            _state.LastHitObstacle = id;
            _state.LastHitKind = kind;
            _state.StumbleTick = t;
            _state.InvulnerableUntilTick = t + _invulnerableTicks;
            _state.RegenProgress = 0f;
            _state.Speed *= _config.Speed.StumbleSpeedFactor;
            Emit(RunEventType.Hit, id, (byte)kind, 0f);
            if (health <= 0)
            {
                Die(t, DeathCause.Health, id);
            }
        }

        private HitKind Classify(in ObstacleBox box, float ex0, float ex1)
        {
            switch (box.Class)
            {
                case ObstacleClass.Low:
                    return HitKind.Trip;
                case ObstacleClass.High:
                    return HitKind.HeadClip;
                case ObstacleClass.Thorns:
                    return HitKind.Thorns;
                default:
                    HitboxConfig hb = _config.Hitbox;
                    float c0 = ex0 + hb.CrashCoreInset;
                    float c1 = ex1 - hb.CrashCoreInset;
                    if (c1 - c0 < hb.CrashCoreMinWidth)
                    {
                        float c = (ex0 + ex1) * 0.5f;
                        c0 = c - (hb.CrashCoreMinWidth * 0.5f);
                        c1 = c + (hb.CrashCoreMinWidth * 0.5f);
                    }

                    return _state.X > c0 && _state.X < c1 ? HitKind.Crash : HitKind.SideClip;
            }
        }

        private void ApplySideClip(long t, in ObstacleBox box)
        {
            float clearance = _config.Hitbox.SideClipClearance;
            _path.GetLateralBounds(_state.S, _state.X, out float bMin, out float bMax);
            float xMin = bMin + _config.Lateral.EdgeMargin;
            float xMax = bMax - _config.Lateral.EdgeMargin;
            float right = box.XMax + clearance;
            float left = box.XMin - clearance;
            bool preferRight = _state.X >= box.CenterX;
            float target;
            if (preferRight)
            {
                target = right <= xMax ? right : left >= xMin ? left : right;
            }
            else
            {
                target = left >= xMin ? left : right <= xMax ? right : left;
            }

            _state.XTarget = Clamp(target, xMin, Math.Max(xMin, xMax));
            _state.DodgeBoostUntilTick = t + _dodgeBoostTicks - 1;
        }

        private void ConsumeShield(long t, int id, HitKind kind)
        {
            _state.Shield = false;
            _state.ShieldUntilTick = 0;
            _state.InvulnerableUntilTick = t + _shieldTicks;
            Emit(RunEventType.ShieldConsumed, id, (byte)kind, 0f);
        }

        private void Die(long t, DeathCause cause, int obstacleId)
        {
            _state.Dead = true;
            _state.Cause = cause;
            _state.DeathObstacle = obstacleId;
            _state.DeathTick = t;
            _state.DeathSpeed = _state.Speed;
            _state.Buffered = InputCommand.None;
            Emit(RunEventType.Died, obstacleId, (byte)cause, 0f);
        }

        private void StepDying(long t)
        {
            long k = t - _state.DeathTick;
            if (_state.Cause == DeathCause.Fall)
            {
                float y = _state.Y;
                float vy = _state.Vy;
                VerticalMotion.Integrate(_config.JumpSlide, ref y, ref vy, false, _dt);
                _state.Y = y;
                _state.Vy = vy;
            }
            else
            {
                float p = Clamp01((float)k / _crashStopTicks);
                _state.Speed = _state.DeathSpeed * (1f - p);
                if (!_state.Grounded)
                {
                    float y = _state.Y;
                    float vy = _state.Vy;
                    VerticalMotion.Integrate(_config.JumpSlide, ref y, ref vy, false, _dt);
                    if (_path.TryGetFloor(_state.S, _state.X, out float floor) && y <= floor && _state.Y >= floor - _config.JumpSlide.LedgeAssistDrop)
                    {
                        y = floor;
                        vy = 0f;
                        _state.Grounded = true;
                    }

                    _state.Y = y;
                    _state.Vy = vy;
                }
            }

            _state.S += _state.Speed * _dt;
        }

        private void StepCoins()
        {

            HitboxConfig hb = _config.Hitbox;
            float r = hb.CoinPickupRadius;
            float halfDepth = (HitboxDepth * 0.5f) + r;
            float halfWidth = (hb.Width * 0.5f) + r;
            float y0 = _state.Y - r;
            float y1 = _state.Y + HitboxHeight + r;
            int n = _path.FindCoins(_state.S - halfDepth, _state.S + halfDepth, _ids);
            for (int i = 0; i < n; i++)
            {
                int id = _ids[i];
                if (Has(_collected, id))
                {
                    continue;
                }

                CoinPoint coin = _path.GetCoin(id);
                if (Math.Abs(coin.X - _state.X) <= halfWidth && coin.Y >= y0 && coin.Y <= y1)
                {
                    Mark(_collected, id);
                    _state.Coins++;
                    Emit(RunEventType.Coin, id, 0, 0f);
                }
            }
        }

        private void StepTimers(long t, float ds)
        {
            if (_state.Buffered != InputCommand.None)
            {
                int limit = _state.BufferedKind == DropReason.Ceiling ? _ceilingHoldTicks : _bufferTicks;
                if (t - _state.BufferedTick >= limit)
                {
                    InputCommand dropped = _state.Buffered;
                    DropReason reason = _state.BufferedKind;
                    _state.Buffered = InputCommand.None;
                    _state.DroppedInputs++;
                    Emit(RunEventType.InputDropped, -1, (byte)reason, (float)dropped);
                }
            }

            if (_state.Shield && _state.ShieldUntilTick > 0 && t >= _state.ShieldUntilTick)
            {
                _state.Shield = false;
                _state.ShieldUntilTick = 0;
                Emit(RunEventType.ShieldExpired, -1, 0, 0f);
            }

            HealthConfig health = _config.Health;
            if (_state.Health < health.MaxHealth)
            {
                _state.RegenProgress += ds;
                if (_state.RegenProgress >= health.RegenDistance)
                {
                    _state.RegenProgress = 0f;
                    _state.Health++;
                    Emit(RunEventType.Regen, -1, 0, 0f);
                }
            }

            RecordSafePoint(false);

            if (!_state.Finished && _state.S >= _path.FinishS)
            {
                _state.Finished = true;
                _state.FinishTick = t;
                Emit(RunEventType.Finished, -1, 0, 0f);
            }
        }

        private void RecordSafePoint(bool force)
        {
            if (!_state.Grounded)
            {
                return;
            }

            if (!force && _safeCount > 0)
            {
                int last = (_safeHead - 1 + SafeRingSize) % SafeRingSize;
                if (_state.S - _safeS[last] < _config.Health.SafePointSpacing)
                {
                    return;
                }
            }

            _safeS[_safeHead] = _state.S;
            _safeX[_safeHead] = _state.X;
            _safeY[_safeHead] = _state.Y;
            _safeHead = (_safeHead + 1) % SafeRingSize;
            if (_safeCount < SafeRingSize)
            {
                _safeCount++;
            }
        }

        private float LastGroundedS()
        {
            if (_safeCount == 0)
            {
                return _state.S;
            }

            return _safeS[(_safeHead - 1 + SafeRingSize) % SafeRingSize];
        }

        private void Emit(RunEventType type, int id, byte reason, float value)
        {
            if (!MuteEvents)
            {
                Events.Add(new RunEvent(type, _state.Tick, id, reason, value));
            }
        }

        private float ClampToPath(float s, float x)
        {
            _path.GetLateralBounds(s, x, out float bMin, out float bMax);
            float xMin = bMin + _config.Lateral.EdgeMargin;
            float xMax = bMax - _config.Lateral.EdgeMargin;
            return Clamp(x, xMin, Math.Max(xMin, xMax));
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float d = target - current;
            if (d > maxDelta)
            {
                return current + maxDelta;
            }

            if (d < -maxDelta)
            {
                return current - maxDelta;
            }

            return target;
        }

        private static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
