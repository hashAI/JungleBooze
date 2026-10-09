using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Vine swing (spec 103 §5): automatic grab in the grab zone, a scripted 60-tick pendulum
    /// θ(p) = θc − θa·cos(πp), held early release, Good / Perfect release along the arc tangent, safe auto-release.
    /// Steering is discarded during the swing; swipe down and flicks are dropped with InputDropped(Swing).
    /// </summary>
    public sealed partial class RunnerSimulation
    {
        /// <summary>Swing ticks since the grab (0 on the grab tick), or −1 when not swinging.</summary>
        public int SwingTick => _state.Mode == MoveMode.Swing ? (int)(_state.Tick - _state.GrabTick) : -1;

        /// <summary>Swing angle at swing tick <paramref name="k"/>, degrees (positive = forward).</summary>
        public float SwingThetaDeg(int k)
        {
            VineConfig v = _config.Vine;
            float p = Clamp01((float)k / _swingTicks);
            float c = (v.Theta0Deg + v.ThetaEndDeg) * 0.5f;
            float a = (v.ThetaEndDeg - v.Theta0Deg) * 0.5f;
            return c - (a * (float)Math.Cos(Math.PI * p));
        }

        /// <summary>The vine being swung and the current angle (views, camera, animation).</summary>
        public bool TryGetSwing(out VinePoint vine, out float thetaDeg)
        {
            thetaDeg = 0f;
            vine = default;
            if (_state.Mode != MoveMode.Swing || _traversal == null || !_traversal.TryGetVine(_state.VineId, out vine))
            {
                return false;
            }

            thetaDeg = SwingThetaDeg(Math.Min(SwingTick, _swingTicks));
            return true;
        }

        /// <summary>True if a release on swing tick <paramref name="k"/> is Perfect.</summary>
        public bool IsPerfectTick(int k)
        {
            return k >= _perfectStartTick && k <= _perfectEndTick;
        }

        private void TryGrabVine(long t)
        {
            if (_traversal == null || _state.Dead)
            {
                return;
            }

            VineConfig vc = _config.Vine;
            if (!_traversal.TryFindVine(_state.S, vc.GrabBefore, vc.GrabAfter, out VinePoint vine) || vine.Id == _state.VineId)
            {
                return;
            }

            float feet = _state.Y - vine.TakeoffY;
            if (Math.Abs(_state.X - vine.X) > vc.GrabHalfWidth || feet < -vc.GrabFeetBelow || feet > vc.GrabFeetAbove)
            {
                return;
            }

            if (_state.Sliding)
            {
                EndSlide();
            }

            // Commands queued or buffered before this tick are cleared (spec 103 §5.2).
            _state.Mode = MoveMode.Swing;
            _state.VineId = vine.Id;
            _state.GrabTick = t;
            _state.GrabS = _state.S;
            _state.GrabX = _state.X;
            _state.GrabY = _state.Y;
            _state.XTarget = vine.X;
            _state.VLat = 0f;
            _state.Vy = 0f;
            _state.Grounded = false;
            _state.Jumped = true;
            _state.FastFalling = false;
            _state.BelowLip = false;
            _state.Buffered = InputCommand.None;
            _state.ReleaseHeld = false;
            _state.VineAir = false;
            _state.LastGroundY = vine.TakeoffY;
            _state.AirPeakY = _state.Y;
            _state.ActionHits = _state.Hits;
            Emit(RunEventType.VineGrab, vine.Id, 0, 0f);
        }

        private void StepSwing(long t, InputCommand discrete)
        {
            VineConfig vc = _config.Vine;
            int k = (int)(t - _state.GrabTick);
            if (!_traversal.TryGetVine(_state.VineId, out VinePoint vine))
            {
                // The vine left the path (should not happen): let go safely.
                _state.Mode = MoveMode.Run;
                _state.Grounded = false;
                return;
            }

            bool release = false;
            if (discrete == InputCommand.Jump)
            {
                if (k < _releaseOpenTick)
                {
                    _state.ReleaseHeld = true;
                }
                else
                {
                    release = true;
                }
            }
            else if (discrete != InputCommand.None)
            {
                _state.DroppedInputs++;
                Emit(RunEventType.InputDropped, -1, (byte)DropReason.Swing, (float)discrete);
            }

            if (_state.ReleaseHeld && k >= _releaseOpenTick)
            {
                release = true;
            }

            int kk = Math.Min(k, _swingTicks);
            if (k >= _swingTicks)
            {
                release = true;
            }

            float theta = SwingThetaDeg(kk);
            double rad = theta * (Math.PI / 180.0);
            float s = vine.AnchorS + (vine.Length * (float)Math.Sin(rad));
            float y = vine.AnchorY - (vine.Length * (float)Math.Cos(rad)) - vc.HandToFeet;
            float x = vine.X;
            if (_grabSnapTicks > 0 && kk < _grabSnapTicks)
            {
                float w = (float)kk / _grabSnapTicks;
                s = _state.GrabS + ((s - _state.GrabS) * w);
                y = _state.GrabY + ((y - _state.GrabY) * w);
                x = _state.GrabX + ((vine.X - _state.GrabX) * w);
            }

            // s never runs backwards (the grab can happen just past the arc's start point).
            if (s < _state.S)
            {
                s = _state.S;
            }

            float ds = s - _state.S;
            _state.S = s;
            _state.Distance += ds;
            _state.Speed = ds / _dt;
            _state.X = x;
            _state.Y = y;
            _state.VLat = 0f;
            _state.XTarget = vine.X;
            if (y > _state.AirPeakY)
            {
                _state.AirPeakY = y;
            }

            if (release)
            {
                Release(t, vine, kk, rad);
            }

            StepCollisions(t);
            StepCoins();
            if (_state.Dead)
            {
                return;
            }

            StepTimers(t, ds);
        }

        private void Release(long t, in VinePoint vine, int k, double rad)
        {
            VineConfig vc = _config.Vine;
            bool perfect = IsPerfectTick(k);
            float speed = vc.ReleaseSpeed * (perfect ? vc.PerfectBoost : 1f);
            _state.Mode = MoveMode.Run;
            _state.Grounded = false;
            _state.Jumped = true;
            _state.BelowLip = false;
            _state.VineAir = true;
            _state.LaunchSpeed = speed * (float)Math.Cos(rad);
            _state.Vy = speed * (float)Math.Sin(rad);
            _state.AirborneSinceTick = t;
            _state.LastGroundY = vine.TakeoffY;
            _state.ReleaseHeld = false;
            _state.LastReleasePerfect = perfect;
            _state.XTarget = _state.X;
            _state.Releases++;
            if (perfect)
            {
                _state.Perfects++;
            }

            Emit(RunEventType.VineRelease, vine.Id, (byte)(perfect ? 1 : 0), k);
            EmitTraversal(perfect ? TraversalKind.VinePerfect : TraversalKind.VineGood, true);
        }
    }
}
