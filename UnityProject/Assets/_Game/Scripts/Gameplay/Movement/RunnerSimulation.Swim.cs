using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Swimming (spec 103 §4): water entry/exit with hysteresis and speed blends, the swim servo with currents,
    /// dive (Down → Under → Up), leap, dive-in, ceiling hold under surface obstacles, the Deep Breath passage, and
    /// the canopy beam landing assist (§6). Body height in water is the body line (centre of the swim hitbox).
    /// Allocation-free.
    /// </summary>
    public sealed partial class RunnerSimulation
    {
        /// <summary>Swim line height for the current water (body centre at the surface).</summary>
        public float SwimLine => _state.WaterY + _config.Swim.SwimLineHeight;

        private void StepTraversal(long t, InputFrame frame)
        {
            InputCommand discrete = InputCommands.Discrete(frame.Commands);
            switch (_state.Mode)
            {
                case MoveMode.Swing:
                    StepSwing(t, discrete);
                    break;
                case MoveMode.DeepDive:
                    StepDeepDive(t);
                    break;
                default:
                    StepSwim(t, frame, discrete);
                    break;
            }
        }

        private void StepSwim(long t, InputFrame frame, InputCommand discrete)
        {
            // (2) Commands (spec 103 §4.4).
            ResolveSwimHold(t);
            if (discrete == InputCommand.Jump)
            {
                SwimUp(t);
            }
            else if (discrete == InputCommand.Slide)
            {
                SwimDown(t);
            }

            if (_state.Mode == MoveMode.DeepDive)
            {
                StepDeepDive(t);
                return;
            }

            // (3) Lateral with swim values and the current.
            int dodgeDir = discrete == InputCommand.DodgeLeft ? -1 : discrete == InputCommand.DodgeRight ? 1 : 0;
            StepLateral(t, frame.LateralDeltaM, dodgeDir);

            // (4) Vertical: surface line, dive phases, leap.
            StepSwimVertical(t);

            // (5) Forward.
            float speed = CurrentSpeed(t);
            _state.Speed = speed;
            float ds = speed * _dt;
            _state.S += ds;
            _state.Distance += ds;

            // (6) Collisions and pickups.
            StepCollisions(t);
            StepCoins();
            if (_state.Dead)
            {
                return;
            }

            if (_state.Mode == MoveMode.Swim)
            {
                TryExitWater(t);
            }

            // (7) Timers.
            StepTimers(t, ds);
        }

        /// <summary>Water entry from land or air (Run mode, after the vertical step). True when swimming started.</summary>
        private bool TryEnterWater(long t)
        {
            if (_traversal == null || _state.Mode != MoveMode.Run || _state.Dead)
            {
                return false;
            }

            SwimConfig sw = _config.Swim;
            if (!TryWaterDepth(_state.S, _state.X, out float surface, out float depth) || depth < sw.EnterDepth)
            {
                return false;
            }

            if (!_state.Grounded && (_state.Vy > 0f || _state.Y > surface))
            {
                return false;
            }

            bool splash = !_state.Grounded;
            bool fastFall = _state.FastFalling;
            bool bufferedJump = _state.Buffered == InputCommand.Jump && _state.BufferedKind == DropReason.Buffer;
            if (_state.Sliding)
            {
                // A slide in progress ends on the entry tick (spec 103 §4.4).
                EndSlide();
            }

            _state.Mode = MoveMode.Swim;
            _state.WaterY = surface;
            _state.Y = surface + sw.SwimLineHeight;
            _state.Vy = 0f;
            _state.Grounded = true;
            _state.Jumped = false;
            _state.BelowLip = false;
            _state.FastFalling = false;
            _state.Leaping = false;
            _state.VineAir = false;
            _state.Dive = DivePhase.None;
            _state.OnSurface = InputCommand.None;
            _state.GroundY = surface;
            _state.LastGroundY = surface;
            _state.Buffered = InputCommand.None;
            StartBlend(t, _state.Speed, _swimEnterBlendTicks);
            Emit(RunEventType.WaterEnter, -1, (byte)(splash ? 1 : 0), 0f);
            if (splash)
            {
                Emit(RunEventType.Splash, -1, 0, 0f);
            }

            // Entry tick: a buffered jump becomes a leap, a fast-fall becomes a dive.
            if (fastFall)
            {
                StartDive(t, t + 1);
            }
            else if (bufferedJump)
            {
                Leap(t);
            }

            return true;
        }

        private void TryExitWater(long t)
        {
            if (_state.Dive != DivePhase.None || _state.Leaping)
            {
                return;
            }

            if (TryWaterDepth(_state.S, _state.X, out float surface, out float depth) && depth >= _config.Swim.ExitDepth)
            {
                _state.WaterY = surface;
                _state.GroundY = surface;
                return;
            }

            float floor = _path.TryGetFloor(_state.S, _state.X, out float f) ? f : _state.WaterY;
            _state.Mode = MoveMode.Run;
            _state.Grounded = true;
            _state.Jumped = false;
            _state.FastFalling = false;
            _state.Y = floor;
            _state.Vy = 0f;
            _state.GroundY = floor;
            _state.LastGroundY = floor;
            StartBlend(t, _state.Speed, _swimExitBlendTicks);
            Emit(RunEventType.WaterExit, -1, 0, 0f);
        }

        private bool TryWaterDepth(float s, float x, out float surface, out float depth)
        {
            depth = 0f;
            if (_traversal == null || !_traversal.TryGetWater(s, x, out surface))
            {
                surface = 0f;
                return false;
            }

            depth = _path.TryGetFloor(s, x, out float floor) ? surface - floor : 100f;
            return true;
        }

        private void ResolveSwimHold(long t)
        {
            if (_state.Buffered == InputCommand.Jump && _state.BufferedKind == DropReason.Ceiling &&
                (_state.Dive == DivePhase.Down || _state.Dive == DivePhase.Under) && !SurfaceObstacleOverhead())
            {
                _state.Buffered = InputCommand.None;
                DiveCancel(t);
            }
        }

        /// <summary>Swipe up in water (spec 103 §4.4).</summary>
        private void SwimUp(long t)
        {
            if (_state.Leaping)
            {
                BufferCommand(InputCommand.Jump, DropReason.Buffer, t);
                return;
            }

            switch (_state.Dive)
            {
                case DivePhase.None:
                    Leap(t);
                    break;
                case DivePhase.Down:
                case DivePhase.Under:
                    if (SurfaceObstacleOverhead())
                    {
                        // Held like spec 101's ceiling hold; dropped with InputDropped(Ceiling) if still covered.
                        BufferCommand(InputCommand.Jump, DropReason.Ceiling, t);
                    }
                    else
                    {
                        DiveCancel(t);
                    }

                    break;
                default:
                    _state.OnSurface = InputCommand.Jump;
                    break;
            }
        }

        /// <summary>Swipe down in water (spec 103 §4.4).</summary>
        private void SwimDown(long t)
        {
            if (_state.Leaping)
            {
                _state.Buffered = InputCommand.None;
                if (_state.Vy > -_config.Swim.DiveInSpeed)
                {
                    _state.Vy = -_config.Swim.DiveInSpeed;
                }

                _state.FastFalling = true;
                _state.OnSurface = InputCommand.Slide;
                Emit(RunEventType.FastFall, -1, 0, 0f);
                return;
            }

            switch (_state.Dive)
            {
                case DivePhase.None:
                    StartDive(t, t);
                    break;
                case DivePhase.Under:
                    // Restart the Under timer.
                    _state.DivePhaseTick = t;
                    break;
                case DivePhase.Up:
                    _state.OnSurface = InputCommand.Slide;
                    break;
            }
        }

        private void StartDive(long t, long phaseStart)
        {
            if (_options.DeepBreath && _traversal != null && _traversal.TryFindDeepDive(_state.S, _state.X, out DeepDivePoint zone))
            {
                StartDeepDive(t, zone);
                return;
            }

            _state.Dive = DivePhase.Down;
            _state.DivePhaseTick = phaseStart;
            _state.DiveFromY = SwimLine;
            _state.DiveRiseFast = false;
            _state.OnSurface = InputCommand.None;
            _state.ActionHits = _state.Hits;
            Emit(RunEventType.Dive, -1, 0, 0f);
        }

        private void DiveCancel(long t)
        {
            _state.Dive = DivePhase.Up;
            _state.DivePhaseTick = t;
            _state.DiveFromY = _state.Y;
            _state.DiveRiseFast = true;
            _state.OnSurface = InputCommand.Jump;
        }

        private void Leap(long t)
        {
            _state.Leaping = true;
            _state.Grounded = false;
            _state.Jumped = true;
            _state.FastFalling = false;
            _state.Vy = _config.Swim.LeapVelocity;
            _state.AirborneSinceTick = t;
            _state.AirPeakY = _state.Y;
            _state.Buffered = InputCommand.None;
            _state.OnSurface = InputCommand.None;
            _state.ActionHits = _state.Hits;
            Emit(RunEventType.Leap, -1, 0, 0f);
        }

        private void StepSwimVertical(long t)
        {
            SwimConfig sw = _config.Swim;
            if (_state.Leaping)
            {
                float y = _state.Y;
                float vy = _state.Vy;
                VerticalMotion.Integrate(_config.JumpSlide, ref y, ref vy, _state.FastFalling, _dt);
                if (y > _state.AirPeakY)
                {
                    _state.AirPeakY = y;
                }

                if (TryWaterDepth(_state.S, _state.X, out float surface, out float depth) && depth >= sw.ExitDepth)
                {
                    _state.WaterY = surface;
                    float line = surface + sw.SwimLineHeight;
                    if (vy <= 0f && y <= line)
                    {
                        Splash(t, line);
                        return;
                    }

                    _state.Y = y;
                    _state.Vy = vy;
                    return;
                }

                // Leapt out of the water volume: an ordinary airborne runner from here (feet = body line − line height).
                _state.Mode = MoveMode.Run;
                _state.Leaping = false;
                _state.Y = y - sw.SwimLineHeight;
                _state.Vy = vy;
                _state.Jumped = true;
                _state.Grounded = false;
                _state.LastGroundY = _state.WaterY;
                StartBlend(t, _state.Speed, _swimExitBlendTicks);
                Emit(RunEventType.WaterExit, -1, 0, 0f);
                return;
            }

            float lineY = SwimLine;
            float deep = lineY + sw.DiveDepth;
            _state.Vy = 0f;
            for (int guard = 0; guard < 3; guard++)
            {
                long k = t - _state.DivePhaseTick;
                switch (_state.Dive)
                {
                    case DivePhase.None:
                        _state.Y = lineY;
                        return;

                    case DivePhase.Down:
                        if (k < 0)
                        {
                            _state.Y = lineY;
                            return;
                        }

                        if (k >= _diveDownTicks)
                        {
                            _state.Dive = DivePhase.Under;
                            _state.DivePhaseTick = t;
                            continue;
                        }

                        _state.Y = Lerp(_state.DiveFromY, deep, (float)(k + 1) / _diveDownTicks);
                        return;

                    case DivePhase.Under:
                        // [ASSUMED 2026-10-09] Like spec 101's slide ceiling guard, the Under phase lasts while a surface
                        // obstacle is overhead, so a timed dive never surfaces into a log.
                        if (k >= _diveUnderTicks && !SurfaceObstacleOverhead())
                        {
                            _state.Dive = DivePhase.Up;
                            _state.DivePhaseTick = t;
                            _state.DiveFromY = deep;
                            continue;
                        }

                        _state.Y = deep;
                        return;

                    default:
                    {
                        int upTicks = _state.DiveRiseFast ? Math.Max(1, (int)Math.Ceiling(_diveUpTicks / Math.Max(1f, sw.DiveRiseFactor))) : _diveUpTicks;
                        if (k >= upTicks)
                        {
                            SurfaceFromDive(t);
                            if (_state.Dive == DivePhase.Down)
                            {
                                continue;
                            }

                            return;
                        }

                        _state.Y = Lerp(_state.DiveFromY, lineY, (float)(k + 1) / upTicks);
                        return;
                    }
                }
            }
        }

        private void SurfaceFromDive(long t)
        {
            _state.Dive = DivePhase.None;
            _state.Y = SwimLine;
            _state.DiveRiseFast = false;
            Emit(RunEventType.Surface, -1, 0, 0f);
            EmitTraversal(TraversalKind.SwimDive, _state.Hits == _state.ActionHits);
            InputCommand next = _state.OnSurface;
            _state.OnSurface = InputCommand.None;
            if (next == InputCommand.Jump)
            {
                Leap(t);
            }
            else if (next == InputCommand.Slide)
            {
                StartDive(t, t);
            }
        }

        private void Splash(long t, float line)
        {
            float fall = _state.AirPeakY - line;
            _state.Leaping = false;
            _state.Grounded = true;
            _state.Jumped = false;
            _state.Y = line;
            _state.Vy = 0f;
            _state.GroundY = _state.WaterY;
            _state.LastGroundY = _state.WaterY;
            Emit(RunEventType.Splash, -1, 0, fall);
            EmitTraversal(TraversalKind.SwimLeap, _state.Hits == _state.ActionHits);
            bool dive = _state.FastFalling || _state.OnSurface == InputCommand.Slide;
            _state.FastFalling = false;
            _state.OnSurface = InputCommand.None;
            if (dive)
            {
                _state.Buffered = InputCommand.None;
                StartDive(t, t + 1);
            }
            else if (_state.Buffered == InputCommand.Jump && _state.BufferedKind == DropReason.Buffer)
            {
                Leap(t);
            }
        }

        /// <summary>A FloatingLog or LowBranch over the swim hitbox (ceiling hold, Under extension).</summary>
        private bool SurfaceObstacleOverhead()
        {
            SwimConfig sw = _config.Swim;
            float halfDepth = sw.HitboxDepth * 0.5f;
            float halfWidth = sw.HitboxWidth * 0.5f;
            int n = _path.FindObstacles(_state.S - halfDepth, _state.S + halfDepth, _ids);
            for (int i = 0; i < n; i++)
            {
                int id = _ids[i];
                if (Has(_removed, id))
                {
                    continue;
                }

                ObstacleBox box = _path.GetObstacle(id);
                if (box.Class != ObstacleClass.FloatingLog && box.Class != ObstacleClass.LowBranch)
                {
                    continue;
                }

                EffectiveBox(box, out float ex0, out float ex1, out float ey0, out _);
                if (box.SMin < _state.S + halfDepth && box.SMax > _state.S - halfDepth &&
                    ex0 < _state.X + halfWidth && ex1 > _state.X - halfWidth && ey0 > _state.Y)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- Deep Breath (spec 103 §4.5) ----

        private void StartDeepDive(long t, in DeepDivePoint zone)
        {
            _state.Mode = MoveMode.DeepDive;
            _state.Dive = DivePhase.None;
            _state.DeepZone = zone.Id;
            _state.DeepStartTick = t;
            _state.DeepS0 = _state.S;
            _state.DeepX0 = _state.X;
            _state.DeepExitS = zone.ExitS;
            _state.DeepExitX = zone.ExitX;
            _state.WaterY = zone.WaterY;
            _state.OnSurface = InputCommand.None;
            _state.Buffered = InputCommand.None;
            _state.ActionHits = _state.Hits;
            Emit(RunEventType.Dive, -1, 1, 0f);
            Emit(RunEventType.DeepDiveStart, zone.Id, 0, 0f);
        }

        /// <summary>Ticks of the deep dive (spec 103: 2.4 s = 144).</summary>
        public int DeepDiveTicks => Math.Max(1, ToTicks(_options.DeepDiveTime > 0f ? _options.DeepDiveTime : 2.4f));

        private void StepDeepDive(long t)
        {
            int total = DeepDiveTicks;
            long k = t - _state.DeepStartTick + 1;
            float u = Clamp01((float)k / total);
            float depth = _options.DeepDiveDepth < 0f ? _options.DeepDiveDepth : -2.5f;
            float s = _state.DeepS0 + ((_state.DeepExitS - _state.DeepS0) * u);
            // The passage lines up with the exit x early, then runs straight (crystals sit on that line).
            float ux = Clamp01(u / 0.3f);
            float w = ux * ux * (3f - (2f * ux));
            float x = _state.DeepX0 + ((_state.DeepExitX - _state.DeepX0) * w);
            float y = SwimLine + (depth * (float)Math.Sin(Math.PI * u));
            float ds = s - _state.S;
            _state.S = s;
            _state.Distance += ds;
            _state.Speed = ds / _dt;
            _state.X = x;
            _state.XTarget = x;
            _state.VLat = 0f;
            _state.Y = y;
            _state.Vy = 0f;

            // Invulnerable to everything (StepCollisions skips); pickups still count.
            StepCoins();
            if (k >= total)
            {
                _state.Mode = MoveMode.Swim;
                _state.Dive = DivePhase.None;
                _state.Y = SwimLine;
                _state.Grounded = true;
                _state.GroundY = _state.WaterY;
                StartBlend(t, _state.Speed, _swimEnterBlendTicks);
                Emit(RunEventType.DeepDiveEnd, _state.DeepZone, 0, 0f);
                Emit(RunEventType.Surface, -1, 1, 0f);
                EmitTraversal(TraversalKind.SwimDive, true);
            }

            StepTimers(t, ds);
        }

        // ---- Canopy beams (spec 103 §6) ----

        /// <summary>
        /// Feet landing up to <c>BeamLandingAssist</c> outside a beam edge land on the beam and x is pulled onto it
        /// over a few ticks. Further out there is no floor: the fall continues (cause Fall).
        /// </summary>
        private bool TryBeamAssist(long t, float yPrev, float y)
        {
            if (_traversal == null || !_traversal.IsCanopy(_state.S))
            {
                return false;
            }

            JumpSlideConfig js = _config.JumpSlide;
            float assist = _config.Canopy.BeamLandingAssist;
            for (int side = -1; side <= 1; side += 2)
            {
                float probe = _state.X + (side * assist);
                if (!TryGetSupport(_state.S, probe, yPrev, true, out float floor) || y > floor || yPrev < floor - js.LedgeAssistDrop)
                {
                    continue;
                }

                // The beam edge: first floor found stepping out from x.
                float edge = probe;
                for (int k = 1; k <= 5; k++)
                {
                    float xp = _state.X + (side * assist * k / 5f);
                    if (_path.TryGetFloor(_state.S, xp, out _))
                    {
                        edge = xp;
                        break;
                    }
                }

                _path.GetLateralBounds(_state.S, edge + (side * 0.01f), out float bMin, out float bMax);
                float margin = _config.Lateral.EdgeMargin;
                float target = Clamp(_state.X, bMin + margin, Math.Max(bMin + margin, bMax - margin));
                float from = _state.X;
                Land(t, floor);
                _state.SnapFromX = from;
                _state.SnapToX = target;
                _state.SnapTick = t;
                _state.SnapUntilTick = t + _beamSnapTicks;
                _state.XTarget = target;
                _state.VLat = 0f;
                Emit(RunEventType.BeamAssist, -1, (byte)(side > 0 ? 1 : 0), Math.Abs(target - from));
                return true;
            }

            return false;
        }

        // ---- Shared helpers ----

        private void EmitTraversal(TraversalKind kind, bool success)
        {
            _state.TraversalAttempts++;
            if (success)
            {
                _state.TraversalSuccesses++;
            }

            Emit(RunEventType.TraversalResult, success ? -1 : _state.LastHitObstacle, (byte)kind, success ? 1f : 0f);
        }

        private void StartBlend(long t, float from, int ticks)
        {
            _state.BlendFromSpeed = from;
            _state.BlendTick = t;
            _state.BlendTicks = ticks;
        }

        private bool IsUnderwater(float s, float x, float y)
        {
            return _traversal != null && _traversal.TryGetWater(s, x, out float surface) && y < surface - _config.Swim.UnderwaterCoinDepth;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
        }
    }
}
