using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>
    /// Third-person follow camera model (spec 101 §5). Plain C#, EditMode-testable with an explicit dt. It reads
    /// Pista's interpolated state and frame time (it is presentation, not simulation) and never writes simulation
    /// state. All smoothing uses <see cref="CriticalSpring"/> (half-life, frame-rate independent). Shake only on
    /// hard landings, minor hits and crashes, hard-capped; Reduced Motion removes shake and bank and halves FOV gain
    /// and slide dip. Orientation changes blend the profile parameters over the target profile's blend time.
    /// </summary>
    public sealed class CameraRigModel
    {
        private readonly ShakeChannel _shake = new ShakeChannel();
        private CameraProfile _from;
        private CameraProfile _to;
        private float _blend = 1f;
        private CriticalSpring _ground;
        private CriticalSpring _air;
        private bool _wasAirborne;
        private CriticalSpring _dip;
        private CriticalSpring _lateral;
        private CriticalSpring _fov;
        private CriticalSpring _bank;
        private CriticalSpring _yaw;
        private CriticalSpring _lookAhead;
        private CameraPose _pose;
        private readonly float[] _weights = new float[5];
        private float _vistaLeft;
        private float _vistaWeight;
        private float _clock;

        public CameraRigModel(CameraProfile profile, float v0, float vMax, float vLatMax)
        {
            _to = (profile ?? throw new ArgumentNullException(nameof(profile))).Clone();
            _from = _to;
            V0 = v0;
            VMax = vMax;
            VLatMax = vLatMax;
        }

        public float V0 { get; }

        public float VMax { get; }

        public float VLatMax { get; }

        public bool ReducedMotion { get; set; }

        public CameraProfile Profile => _to;

        /// <summary>0 → 1 while blending to a new profile.</summary>
        public float BlendProgress => _blend;

        public CameraPose Pose => _pose;

        /// <summary>Beat modifiers (spec 103 §11); null = plain spec 101 camera.</summary>
        public CameraModifiers Modifiers { get; set; }

        /// <summary>Current blend weight of a modifier (tests).</summary>
        public float ModifierWeight(CameraMode mode) => _weights[(int)mode];

        public float VistaWeight => _vistaWeight;

        /// <summary>Starts the vista beat (FOV up, pitch up for the configured duration).</summary>
        public void TriggerVista()
        {
            if (Modifiers != null)
            {
                _vistaLeft = Modifiers.VistaDuration;
            }
        }

        /// <summary>Switches profile (orientation change). Blends unless <paramref name="instant"/>.</summary>
        public void SetProfile(CameraProfile profile, bool instant)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _from = instant ? profile.Clone() : Snapshot();
            _to = profile.Clone();
            _blend = instant || _to.BlendTime <= 0f ? 1f : 0f;
        }

        /// <summary>Jumps every smoothed value to its target (run start, restart).</summary>
        public void Snap(in CameraTargetInput target)
        {
            _ground.Initialized = false;
            _air.Initialized = false;
            _wasAirborne = false;
            _dip.Initialized = false;
            _lateral.Initialized = false;
            _fov.Initialized = false;
            _bank.Initialized = false;
            _yaw.Initialized = false;
            _lookAhead.Initialized = false;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = i == (int)target.Mode && i != 0 ? 1f : 0f;
            }

            _vistaLeft = 0f;
            _vistaWeight = 0f;
            _shake.Clear();
            Update(target, 0f);
        }

        public void ShakeHardLanding()
        {
            CameraProfile p = _to;
            _shake.Trigger(p.ShakeHardLandingAmplitude, p.ShakeHardLandingTime);
        }

        public void ShakeMinorHit()
        {
            CameraProfile p = _to;
            _shake.Trigger(p.ShakeMinorHitAmplitude, p.ShakeMinorHitTime);
        }

        public void ShakeCrash()
        {
            CameraProfile p = _to;
            _shake.Trigger(p.ShakeCrashAmplitude, p.ShakeCrashTime);
        }

        public CameraPose Update(in CameraTargetInput target, float dt)
        {
            if (_blend < 1f && dt > 0f)
            {
                _blend = Math.Min(1f, _blend + (dt / Math.Max(0.0001f, _to.BlendTime)));
            }

            float w = _blend * _blend * (3f - (2f * _blend));
            CameraProfile a = _from;
            CameraProfile b = _to;
            float offsetBack = Lerp(a.OffsetBack, b.OffsetBack, w);
            float height = Lerp(a.Height, b.Height, w);
            float pitch = Lerp(a.PitchDeg, b.PitchDeg, w);
            float fovBase = Lerp(a.FovBaseDeg, b.FovBaseDeg, w);
            float fovGain = Lerp(a.FovGainDeg, b.FovGainDeg, w);
            float lateralFollow = Lerp(a.LateralFollow, b.LateralFollow, w);
            float airFollow = Lerp(a.AirFollow, b.AirFollow, w);
            float slideDip = Lerp(a.SlideDip, b.SlideDip, w);
            float bankMax = Lerp(a.BankMaxDeg, b.BankMaxDeg, w);

            if (ReducedMotion)
            {
                fovGain *= b.ReducedMotionFovGainFactor;
                slideDip *= b.ReducedMotionSlideDipFactor;
                bankMax = 0f;
            }

            // Beat modifiers (spec 103 §11), blended linearly over BlendTime.
            float bob = 0f;
            float under = 0f;
            float lookAheadTarget = 0f;
            CameraModifiers mods = Modifiers;
            if (mods != null)
            {
                _clock += dt;
                float rate = mods.BlendTime > 0f ? dt / mods.BlendTime : 1f;
                for (int m = 1; m < _weights.Length; m++)
                {
                    float want = (int)target.Mode == m ? 1f : 0f;
                    _weights[m] = MoveTowards(_weights[m], want, rate);
                }

                if (_vistaLeft > 0f)
                {
                    _vistaLeft -= dt;
                }

                _vistaWeight = MoveTowards(_vistaWeight, _vistaLeft > 0f ? 1f : 0f, rate);
                for (int m = 1; m < _weights.Length; m++)
                {
                    // Smoothstep over the linear blend: no velocity step (camera pop) at the blend's start and end.
                    float mw = Smooth(_weights[m]);
                    if (mw <= 0f)
                    {
                        continue;
                    }

                    CameraModifier mod = ModifierFor(mods, (CameraMode)m);
                    offsetBack += mw * mod.Back;
                    height += mw * mod.Height;
                    pitch += mw * mod.PitchDeg;
                    fovBase += mw * mod.FovDeg;
                    if (mod.LateralFollow >= 0f)
                    {
                        lateralFollow += mw * (mod.LateralFollow - lateralFollow);
                    }

                    if (mod.AirFollow >= 0f)
                    {
                        airFollow += mw * (mod.AirFollow - airFollow);
                    }

                    if (!ReducedMotion && mod.BobAmplitude > 0f)
                    {
                        bob += mw * mod.BobAmplitude * (float)Math.Sin(2.0 * Math.PI * mod.BobHz * _clock);
                    }
                }

                if (_vistaWeight > 0f)
                {
                    float vw = Smooth(_vistaWeight);
                    pitch += vw * mods.Vista.PitchDeg;
                    fovBase += vw * mods.Vista.FovDeg * (ReducedMotion ? 0.5f : 1f);
                }

                under = Smooth(_weights[(int)CameraMode.DeepDive]) * mods.DeepDiveFollow * Math.Min(0f, target.Y - target.GroundY);
                lookAheadTarget = target.VineAir ? 1f : 0f;
            }

            float speedT = VMax > V0 ? Clamp01((target.Speed - V0) / (VMax - V0)) : 0f;
            float vLat = VLatMax > 0f ? Clamp(target.VLat / VLatMax, -1f, 1f) : 0f;
            float airHeight = Math.Max(0f, target.Y - target.GroundY);

            // Landing on higher ground (a log) moves ground level up and air height down at once. Rebase that step
            // from the ground spring onto the air spring so their sum stays continuous and rises on the air spring
            // alone, instead of sagging then rising (two springs pulling opposite ways).
            if (_wasAirborne && _ground.Initialized && _air.Initialized)
            {
                float step = target.GroundY - _ground.LastTarget;
                _ground.Value += step;
                _ground.LastTarget += step;
                _air.Value -= step;
            }

            _wasAirborne = airHeight > 0f;
            float ground;
            float air;
            if (target.FallHold && _ground.Initialized && _air.Initialized)
            {
                // Canopy fall: the camera stops following down (spec 103 §6).
                ground = _ground.Value;
                air = _air.Value;
            }
            else
            {
                // Vine air: lead the air target by vy × lead so the spring's lag doesn't keep the camera rising
                // while Pista is already falling toward the landing.
                float airTarget = airHeight;
                if (target.VineAir && mods != null)
                {
                    airTarget = Math.Max(0f, airHeight + (target.Vy * mods.VineAirLead));
                }

                ground = _ground.Update(target.GroundY, b.GroundHalfLife, dt);
                air = _air.Update(airTarget * airFollow, b.AirHalfLife, dt);
            }

            // Release look-ahead: aim further ahead (pitch up), keeping the follow distance (spec 103 §11).
            float lookAhead = _lookAhead.Update(lookAheadTarget, mods != null ? mods.LookAheadHalfLife : 0.25f, dt);
            if (mods != null && lookAhead > 0f && offsetBack > 0f)
            {
                double aim = Math.Atan2(height, offsetBack) - Math.Atan2(height, offsetBack + mods.ReleaseLookAhead);
                pitch -= lookAhead * Math.Min(mods.ReleaseLookAheadMaxPitchDeg, (float)(aim * (180.0 / Math.PI)));
            }

            float dip = _dip.Update(target.Sliding ? -slideDip : 0f, b.SlideDipHalfLife, dt);
            float lateral = _lateral.Update(target.X * lateralFollow, b.LateralHalfLife, dt);
            float fov = _fov.Update(fovBase + (fovGain * speedT), b.FovHalfLife, dt);
            float bank = _bank.Update(-bankMax * vLat, b.BankHalfLife, dt);
            float yaw = _yaw.Update(target.PathYawDeg, b.YawHalfLife, dt);

            _pose.S = target.S - offsetBack;
            _pose.X = lateral;
            _pose.Y = ground + height + air + dip + bob + under;
            _pose.PitchDeg = pitch;
            _pose.YawDeg = yaw;
            _pose.RollDeg = ReducedMotion ? 0f : bank;
            _pose.FovDeg = fov;
            if (mods != null)
            {
                float guard = Smooth(_weights[(int)CameraMode.Swing]);
                if (guard > 0f)
                {
                    _pose.Y += guard * (FrameGuardY(target, _pose, mods) - _pose.Y);
                }
            }

            float strength = _shake.Update(dt, b.ShakeFrequency, out float nx, out float ny, out float nr);
            if (ReducedMotion || strength <= 0f)
            {
                _pose.ShakeX = 0f;
                _pose.ShakeY = 0f;
                _pose.ShakeRollDeg = 0f;
            }
            else
            {
                float maxPos = b.ShakeMaxPosition;
                float sx = strength * nx;
                float sy = strength * ny;
                float length = (float)Math.Sqrt((sx * sx) + (sy * sy));
                if (length > maxPos && length > 0f)
                {
                    float k = maxPos / length;
                    sx *= k;
                    sy *= k;
                }

                float rotation = maxPos > 0f ? b.ShakeMaxRotationDeg * (strength / maxPos) * nr : 0f;
                _pose.ShakeX = sx;
                _pose.ShakeY = sy;
                _pose.ShakeRollDeg = Clamp(rotation, -b.ShakeMaxRotationDeg, b.ShakeMaxRotationDeg);
            }

            return _pose;
        }

        private CameraProfile Snapshot()
        {
            float w = _blend * _blend * (3f - (2f * _blend));
            CameraProfile p = _to.Clone();
            p.OffsetBack = Lerp(_from.OffsetBack, _to.OffsetBack, w);
            p.Height = Lerp(_from.Height, _to.Height, w);
            p.PitchDeg = Lerp(_from.PitchDeg, _to.PitchDeg, w);
            p.FovBaseDeg = Lerp(_from.FovBaseDeg, _to.FovBaseDeg, w);
            p.FovGainDeg = Lerp(_from.FovGainDeg, _to.FovGainDeg, w);
            p.LateralFollow = Lerp(_from.LateralFollow, _to.LateralFollow, w);
            p.AirFollow = Lerp(_from.AirFollow, _to.AirFollow, w);
            p.SlideDip = Lerp(_from.SlideDip, _to.SlideDip, w);
            p.BankMaxDeg = Lerp(_from.BankMaxDeg, _to.BankMaxDeg, w);
            return p;
        }

        /// <summary>
        /// Camera height that keeps Pista's feet and raised hands inside the safe band (path space, straight-path
        /// approximation over the follow distance): unchanged if already inside; if both edges bind, centred.
        /// </summary>
        private static float FrameGuardY(in CameraTargetInput target, in CameraPose pose, CameraModifiers mods)
        {
            float dz = target.S - pose.S;
            if (dz <= 0.1f)
            {
                return pose.Y;
            }

            double rad = Math.PI / 180.0;
            double tanHalf = Math.Tan(pose.FovDeg * 0.5 * rad);
            double inner = (1.0 - (2.0 * Clamp01(mods.FrameGuardMargin))) * tanHalf;
            double pitch = pose.PitchDeg * rad;

            // Screen NDC y of a point = tan(atan2(dy, dz) + pitch) / tanHalf, dy = point − camera.
            double lowest = Math.Atan(-inner) - pitch;
            double highest = Math.Atan(inner) - pitch;
            float maxY = target.Y - (float)(dz * Math.Tan(lowest));
            float minY = target.Y + mods.FrameGuardBodyTop - (float)(dz * Math.Tan(highest));
            if (minY > maxY)
            {
                return (minY + maxY) * 0.5f;
            }

            return pose.Y > maxY ? maxY : pose.Y < minY ? minY : pose.Y;
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - (2f * t));
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            return current < target ? Math.Min(target, current + maxDelta) : Math.Max(target, current - maxDelta);
        }

        private static CameraModifier ModifierFor(CameraModifiers mods, CameraMode mode)
        {
            switch (mode)
            {
                case CameraMode.Swim:
                    return mods.Swim;
                case CameraMode.DeepDive:
                    return mods.DeepDive;
                case CameraMode.Swing:
                    return mods.Swing;
                default:
                    return mods.Canopy;
            }
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
