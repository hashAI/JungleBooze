using System;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The camera math of spec 003 section 6 as plain C# (no scene, no transform): smoothing of the lateral and
    /// vertical follow, yaw lead with a low-pass and rate cap, bank roll, pitch follow, bend and boost FOV, the swing
    /// camera (pull-back, side offset, roll, landing punch), Reduce Motion, and the screen-edge safety clamp that adds
    /// yaw lead or FOV when the next 40 m of path get close to the portrait viewport edge.
    /// <see cref="CameraRouteRig"/> feeds it the route; <see cref="FollowCameraView"/> applies the result.
    /// Call <see cref="Advance"/> once per frame, then <see cref="ApplySafety"/>. The safety yaw takes effect on the
    /// next frame (one frame of latency, smoothed anyway). No allocations.
    /// </summary>
    public sealed class CameraRouteModel
    {
        private readonly RunnerPresentationConfig _config;
        private readonly CameraRouteTuning _tuning;

        private float _camX;
        private float _camY;
        private float _velX;
        private float _velY;
        private float _yaw;
        private float _velYaw;
        private float _bankRoll;
        private float _velRoll;
        private float _followPitch;
        private float _velPitch;
        private float _bendFov;
        private float _velBend;
        private float _boostFov;
        private float _velBoost;
        private float _swingBlend;
        private float _swingEase;
        private float _swingRoll;
        private float _bump;
        private int _openSide = 1;
        private float _aimYaw;
        private float _safetyYawDelta;
        private float _safetyFov;
        private float _velSafetyFov;
        private float _fovNoSafety;
        private float _dt;
        private bool _reduceMotion;

        public CameraRouteModel(RunnerPresentationConfig config, CameraRouteTuning tuning)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _fovNoSafety = config.CameraFovDeg;
            RecomputeOutputs();
        }

        /// <summary>Smoothed lateral follow (path space, without shake and swing side offset).</summary>
        public float CamX => _camX;

        /// <summary>Smoothed vertical follow (path space).</summary>
        public float CamY => _camY;

        /// <summary>Compass yaw of the camera in radians (0 = along +z, positive toward +x).</summary>
        public float YawRad => _yaw;

        /// <summary>Total pitch in degrees, positive = looking up.</summary>
        public float PitchDeg { get; private set; }

        /// <summary>Pitch-follow component in degrees (the part limited to the spec's 15 deg/s).</summary>
        public float FollowPitchDeg => _followPitch;

        /// <summary>Swing up-tilt component in degrees.</summary>
        public float SwingTiltDeg { get; private set; }

        /// <summary>Total roll in degrees, positive = right side down (banking into a right turn).</summary>
        public float RollDeg => _bankRoll + _swingRoll;

        /// <summary>Bank roll component in degrees (spec cap 5 deg, 20 deg/s).</summary>
        public float BankRollDeg => _bankRoll;

        /// <summary>Swing roll component in degrees.</summary>
        public float SwingRollDeg => _swingRoll;

        /// <summary>Vertical field of view in degrees, safety included.</summary>
        public float FovDeg => _fovNoSafety + _safetyFov;

        /// <summary>FOV without the safety clamp.</summary>
        public float FovNoSafetyDeg => _fovNoSafety;

        /// <summary>FOV added by the screen-edge safety clamp.</summary>
        public float SafetyFovDeg => _safetyFov;

        /// <summary>Yaw lead added by the screen-edge safety clamp, in degrees (target, before smoothing).</summary>
        public float SafetyYawDeg => _safetyYawDelta * Mathf.Rad2Deg;

        /// <summary>Swing blend 0..1 (linear, before the smoothstep ease).</summary>
        public float SwingBlend => _swingBlend;

        /// <summary>Eased swing blend 0..1 that scales pull-back, side offset, roll, tilt and swing FOV.</summary>
        public float SwingEase => _swingEase;

        /// <summary>Extra distance behind HERO from the swing pull-back.</summary>
        public float PullBackM => _tuning.SwingPullBackM * _swingEase;

        /// <summary>Total distance behind HERO.</summary>
        public float BehindM => _config.CameraOffsetBehindM + PullBackM;

        /// <summary>Extra camera height from the swing pull-back.</summary>
        public float UpExtraM => _tuning.SwingUpM * _swingEase;

        /// <summary>Sideways offset toward the open side (path space, signed).</summary>
        public float SideM => _openSide * _tuning.SwingSideOffsetM * _swingEase;

        /// <summary>Snaps every smoothed value to its target for <paramref name="input"/> (run start).</summary>
        public void Reset(in CameraRouteInput input)
        {
            _reduceMotion = input.ReduceMotion;
            _camX = _config.CameraLateralFollow * input.HeroX;
            _camY = _config.CameraVerticalFollow * (input.HeroY > 0f ? input.HeroY : 0f);
            _velX = 0f;
            _velY = 0f;
            _velYaw = 0f;
            _velRoll = 0f;
            _velPitch = 0f;
            _velBend = 0f;
            _velBoost = 0f;
            _velSafetyFov = 0f;
            _swingBlend = input.Swinging && !input.ReduceMotion ? 1f : 0f;
            _bump = 0f;
            _safetyYawDelta = 0f;
            _safetyFov = 0f;
            _bankRoll = input.ReduceMotion ? 0f : Mathf.Clamp(_tuning.RollGain * input.BankDeg, -_tuning.RollMaxDeg, _tuning.RollMaxDeg);
            _followPitch = _tuning.PitchGain * input.RoutePitchRad * Mathf.Rad2Deg;
            _bendFov = input.ReduceMotion ? 0f : BendTarget(input.CurvatureAbs);
            _boostFov = input.ReduceMotion || !input.Boost ? 0f : _tuning.BoostFovDeg;
            if (input.OpenSide != 0)
            {
                _openSide = input.OpenSide;
            }

            UpdateSwingValues(input);
            _yaw = LeadYaw(input) - SideYaw();
            _aimYaw = _yaw;
            Advance(0f, input);
        }

        /// <summary>
        /// One frame. <paramref name="dt"/> is the snapped real frame time; with 0 nothing is smoothed (pause, hit-pause)
        /// and only the outputs are recomputed.
        /// </summary>
        public void Advance(float dt, in CameraRouteInput input)
        {
            _dt = dt > 0f ? dt : 0f;
            bool rm = input.ReduceMotion;
            _reduceMotion = rm;

            // Lateral and vertical follow: unchanged from the straight-route camera (70 percent, 90 ms; 25 percent, 150 ms).
            float targetX = _config.CameraLateralFollow * input.HeroX;
            float targetY = _config.CameraVerticalFollow * (input.HeroY > 0f ? input.HeroY : 0f);
            if (_dt > 0f)
            {
                _camX = Mathf.SmoothDamp(_camX, targetX, ref _velX, _config.CameraLateralSmoothMs / 1000f, Mathf.Infinity, _dt);
                _camY = Mathf.SmoothDamp(_camY, targetY, ref _velY, _config.CameraVerticalSmoothMs / 1000f, Mathf.Infinity, _dt);
            }

            if (input.OpenSide != 0)
            {
                _openSide = input.OpenSide;
            }

            // Swing blend: 250 ms in (the config's existing blend), 400 ms back. Reduce Motion: none at all.
            float swingTarget = input.Swinging && !rm ? 1f : 0f;
            if (rm)
            {
                _swingBlend = 0f;
            }
            else if (_dt > 0f)
            {
                float seconds = (swingTarget > _swingBlend ? _config.SwingCameraBlendMs : _tuning.SwingReturnMs) / 1000f;
                _swingBlend = seconds > 0f ? Mathf.MoveTowards(_swingBlend, swingTarget, _dt / seconds) : swingTarget;
            }

            bool returning = !rm && swingTarget < 0.5f && _swingBlend > 0f && _swingBlend < 1f;
            _bump = returning ? -_tuning.LandFovOvershootDeg * Mathf.Sin(Mathf.PI * (1f - _swingBlend)) : 0f;
            UpdateSwingValues(input);

            // Bank roll: 0.8 x bank, capped at 5 deg and 20 deg/s.
            if (rm)
            {
                _bankRoll = 0f;
                _velRoll = 0f;
            }
            else if (_dt > 0f)
            {
                float rollTarget = Mathf.Clamp(_tuning.RollGain * input.BankDeg, -_tuning.RollMaxDeg, _tuning.RollMaxDeg);
                _bankRoll = DampLimited(_bankRoll, rollTarget, ref _velRoll, _tuning.RollTimeMs / 1000f, _tuning.RollRateMaxDegS, _dt);
            }

            // Pitch follow: 0.6 x the route pitch, 250 ms, 15 deg/s.
            if (_dt > 0f)
            {
                float pitchTarget = _tuning.PitchGain * input.RoutePitchRad * Mathf.Rad2Deg;
                _followPitch = DampLimited(_followPitch, pitchTarget, ref _velPitch, _tuning.PitchTimeMs / 1000f, _tuning.PitchRateMaxDegS, _dt);
            }

            // FOV: bend bonus (300 ms) and boost bonus (400 ms); none with Reduce Motion.
            if (rm)
            {
                _bendFov = 0f;
                _velBend = 0f;
                _boostFov = 0f;
                _velBoost = 0f;
                _safetyFov = 0f;
                _velSafetyFov = 0f;
            }
            else if (_dt > 0f)
            {
                _bendFov = Mathf.SmoothDamp(_bendFov, BendTarget(input.CurvatureAbs), ref _velBend, _tuning.BendFovBlendMs / 1000f, Mathf.Infinity, _dt);
                _boostFov = Mathf.SmoothDamp(_boostFov, input.Boost ? _tuning.BoostFovDeg : 0f, ref _velBoost, _tuning.BoostFovBlendMs / 1000f, Mathf.Infinity, _dt);
            }

            float swingFov = rm ? 0f : (_config.SwingCameraFovDeg - _config.CameraFovDeg) * _swingEase;
            _fovNoSafety = _config.CameraFovDeg + _bendFov + _boostFov + swingFov + (rm ? 0f : _bump);

            // Yaw lead (aim between the headings 8 and 22 m ahead), swing side look-back, safety lead; low-pass, rate cap.
            float target = LeadYaw(input) - SideYaw();
            _aimYaw = target;
            float maxRate = (rm ? _tuning.MaxYawRateReducedDegS : _tuning.MaxYawRateDegS) * Mathf.Deg2Rad;
            if (_dt > 0f)
            {
                float goal = target + _safetyYawDelta;
                float unwrapped = _yaw + WrapPi(goal - _yaw);
                _yaw = DampLimited(_yaw, unwrapped, ref _velYaw, _tuning.YawTimeMs / 1000f, maxRate, _dt);
            }

            RecomputeOutputs();
        }

        /// <summary>
        /// Screen-edge safety (spec 003 6.1 on-screen rule). <paramref name="points"/> holds <paramref name="count"/>
        /// world (x, z) pairs of the probed path points (10, 25 and 40 m ahead, outer lane centers); the camera is at
        /// (<paramref name="camWorldX"/>, <paramref name="camWorldZ"/>). When a point would come closer to the screen
        /// edge than the margin, the yaw lead grows by the smallest amount that fixes it; if the points are further
        /// apart than the viewport allows, the lead centers them and the FOV grows (not with Reduce Motion).
        /// </summary>
        public void ApplySafety(float camWorldX, float camWorldZ, float[] points, int count)
        {
            float minRel = float.MaxValue;
            float maxRel = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float dx = points[2 * i] - camWorldX;
                float dz = points[(2 * i) + 1] - camWorldZ;
                if ((dx * dx) + (dz * dz) < 1e-4f)
                {
                    continue;
                }

                float rel = WrapPi(Mathf.Atan2(dx, dz) - _aimYaw);
                if (rel < minRel)
                {
                    minRel = rel;
                }

                if (rel > maxRel)
                {
                    maxRel = rel;
                }
            }

            if (maxRel < minRel)
            {
                _safetyYawDelta = 0f;
                SmoothSafetyFov(0f);
                return;
            }

            float aspect = _tuning.AspectWidthOverHeight;
            float keep = 1f - _tuning.SafetyMargin;
            float limit = Mathf.Atan(Mathf.Tan(_fovNoSafety * 0.5f * Mathf.Deg2Rad) * aspect * keep);
            float lag = Mathf.Abs(WrapPi(_aimYaw + _safetyYawDelta - _yaw));
            float lagAllow = Mathf.Min(lag, 0.35f * limit);
            float limitEff = Mathf.Max(0.05f, limit - lagAllow);

            float lo = maxRel - limitEff;
            float hi = minRel + limitEff;
            float delta;
            float fovTarget = 0f;
            if (lo <= hi)
            {
                delta = Mathf.Clamp(0f, lo, hi);
            }
            else
            {
                delta = 0.5f * (lo + hi);
                float half = (0.5f * (maxRel - minRel)) + lagAllow;
                if (half < 1.5f)
                {
                    float needFov = 2f * Mathf.Atan(Mathf.Tan(half) / (aspect * keep)) * Mathf.Rad2Deg;
                    fovTarget = Mathf.Clamp(needFov - _fovNoSafety, 0f, _tuning.SafetyMaxFovExtraDeg);
                }
                else
                {
                    fovTarget = _tuning.SafetyMaxFovExtraDeg;
                }
            }

            float maxYaw = _tuning.SafetyMaxYawExtraDeg * Mathf.Deg2Rad;
            _safetyYawDelta = Mathf.Clamp(delta, -maxYaw, maxYaw);
            SmoothSafetyFov(_reduceMotion ? 0f : fovTarget);
        }

        /// <summary>Wraps an angle to [-pi, pi].</summary>
        public static float WrapPi(float a)
        {
            while (a > Mathf.PI)
            {
                a -= 2f * Mathf.PI;
            }

            while (a < -Mathf.PI)
            {
                a += 2f * Mathf.PI;
            }

            return a;
        }

        private float LeadYaw(in CameraRouteInput input)
        {
            return input.AimNearYawRad + (_tuning.YawLeadWeight * WrapPi(input.AimFarYawRad - input.AimNearYawRad));
        }

        private float SideYaw()
        {
            float side = _openSide * _tuning.SwingSideOffsetM * _swingEase;
            return Mathf.Atan2(side, BehindM + _config.CameraLookAheadM);
        }

        private float BendTarget(float curvatureAbs)
        {
            float full = _tuning.BendFullRadiusM;
            float share = Mathf.Min(1f, curvatureAbs * full);
            return _tuning.BendFovBonusDeg * share;
        }

        private void UpdateSwingValues(in CameraRouteInput input)
        {
            float b = _swingBlend;
            _swingEase = b * b * (3f - (2f * b));
            float roll = _tuning.SwingRollGain * input.SwingAngleRad * Mathf.Rad2Deg * _swingEase;
            _swingRoll = Mathf.Clamp(roll, -_tuning.SwingRollMaxDeg, _tuning.SwingRollMaxDeg);
        }

        private void RecomputeOutputs()
        {
            float up = _config.CameraOffsetUpM + UpExtraM;
            float basePitchDeg = -Mathf.Atan2(up - _config.CameraLookAtHeightM, BehindM + _config.CameraLookAheadM) * Mathf.Rad2Deg;
            SwingTiltDeg = _config.SwingCameraTiltDeg * _swingEase;
            PitchDeg = basePitchDeg + _followPitch + SwingTiltDeg;
        }

        private void SmoothSafetyFov(float target)
        {
            if (_dt > 0f)
            {
                _safetyFov = Mathf.SmoothDamp(_safetyFov, target, ref _velSafetyFov, _tuning.SafetyFovBlendMs / 1000f, Mathf.Infinity, _dt);
            }
        }

        /// <summary>
        /// Critically damped follow with a hard cap on the speed: both the step and the stored velocity are clamped,
        /// so the rate limits of the spec hold exactly (AC-314). Units are whatever the caller uses (deg or rad).
        /// </summary>
        private static float DampLimited(float current, float target, ref float velocity, float smoothTime, float maxRate, float dt)
        {
            float next = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, maxRate, dt);
            float maxStep = maxRate * dt;
            float step = next - current;
            if (step > maxStep)
            {
                next = current + maxStep;
            }
            else if (step < -maxStep)
            {
                next = current - maxStep;
            }

            velocity = Mathf.Clamp(velocity, -maxRate, maxRate);
            return next;
        }
    }
}
