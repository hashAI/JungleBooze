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
        private CameraPose _pose;

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
            float ground = _ground.Update(target.GroundY, b.GroundHalfLife, dt);
            float air = _air.Update(airHeight * airFollow, b.AirHalfLife, dt);
            float dip = _dip.Update(target.Sliding ? -slideDip : 0f, b.SlideDipHalfLife, dt);
            float lateral = _lateral.Update(target.X * lateralFollow, b.LateralHalfLife, dt);
            float fov = _fov.Update(fovBase + (fovGain * speedT), b.FovHalfLife, dt);
            float bank = _bank.Update(-bankMax * vLat, b.BankHalfLife, dt);
            float yaw = _yaw.Update(target.PathYawDeg, b.YawHalfLife, dt);

            _pose.S = target.S - offsetBack;
            _pose.X = lateral;
            _pose.Y = ground + height + air + dip;
            _pose.PitchDeg = pitch;
            _pose.YawDeg = yaw;
            _pose.RollDeg = ReducedMotion ? 0f : bank;
            _pose.FovDeg = fov;

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

        private static float Lerp(float a, float b, float t)
        {
            return a + ((b - a) * t);
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
