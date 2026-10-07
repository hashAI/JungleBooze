using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Session;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Presentation-only tuning (spec 001 section 3.4), plain C#. Field defaults are the spec start values.
    /// <see cref="JungleBooze.Gameplay.Config.RunnerPresentationConfigAsset"/> copies its serialized fields into one.
    /// </summary>
    public sealed class RunnerPresentationConfig
    {
        public float CameraOffsetBehindM = 6.0f;
        public float CameraOffsetUpM = 3.2f;
        public float CameraLookAheadM = 8.0f;
        public float CameraLookAtHeightM = 1.0f;
        public float CameraFovDeg = 60f;
        public float CameraLateralFollow = 0.70f;
        public float CameraLateralSmoothMs = 90f;
        public float CameraVerticalFollow = 0.25f;
        public float CameraVerticalSmoothMs = 150f;
        public float FogStartM = 45f;

        /// <summary>Style guide section 5: fog ends at 90 m.</summary>
        public float FogEndM = 90f;

        public float MinVisibleTrackS = 1.6f;
        public float HitPauseMs = 350f;
        public float DeathCameraHoldMs = 800f;
        public float LaneBumpWobbleMs = 40f;
        public float LaneBumpWobbleM = 0.12f;
        public float StumbleShakeMs = 200f;
        public float StumbleShakeM = 0.08f;
        public float RunAnimReferenceSpeedMps = 10.0f;
        public float RunAnimRateMin = 0.8f;
        public float RunAnimRateMax = 1.6f;

        /// <summary>Spec 001 section 8.3: 3-2-1 countdown of 1.5 s real time. [ASSUMED] lives in this asset.</summary>
        public float ResumeCountdownMs = 1500f;

        /// <summary>Reduce Motion setting (no settings screen yet): turns off the stumble camera shake.</summary>
        public bool ReduceMotion;

        public static RunnerPresentationConfig CreateDefault()
        {
            return new RunnerPresentationConfig();
        }

        public SessionTimings ToSessionTimings()
        {
            return new SessionTimings(HitPauseMs / 1000.0, DeathCameraHoldMs / 1000.0, ResumeCountdownMs / 1000.0);
        }

        /// <summary>Range checks. Appends one message per problem; returns true when there are none. Allocates.</summary>
        public bool Validate(List<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            int before = errors.Count;
            CheckRange(errors, "cameraOffsetBehindM", CameraOffsetBehindM, 1f, 20f);
            CheckRange(errors, "cameraOffsetUpM", CameraOffsetUpM, 0.5f, 10f);
            CheckRange(errors, "cameraLookAheadM", CameraLookAheadM, 0f, 30f);
            CheckRange(errors, "cameraLookAtHeightM", CameraLookAtHeightM, 0f, 5f);
            CheckRange(errors, "cameraFovDeg", CameraFovDeg, 30f, 100f);
            CheckRange(errors, "cameraLateralFollow", CameraLateralFollow, 0f, 1f);
            CheckRange(errors, "cameraLateralSmoothMs", CameraLateralSmoothMs, 0f, 1000f);
            CheckRange(errors, "cameraVerticalFollow", CameraVerticalFollow, 0f, 1f);
            CheckRange(errors, "cameraVerticalSmoothMs", CameraVerticalSmoothMs, 0f, 1000f);
            CheckRange(errors, "fogStartM", FogStartM, 1f, 500f);
            CheckRange(errors, "fogEndM", FogEndM, FogStartM, 1000f);
            CheckRange(errors, "hitPauseMs", HitPauseMs, 0f, 2000f);
            CheckRange(errors, "deathCameraHoldMs", DeathCameraHoldMs, 0f, 5000f);
            CheckRange(errors, "laneBumpWobbleMs", LaneBumpWobbleMs, 0f, 500f);
            CheckRange(errors, "laneBumpWobbleM", LaneBumpWobbleM, 0f, 1f);
            CheckRange(errors, "stumbleShakeMs", StumbleShakeMs, 0f, 1000f);
            CheckRange(errors, "stumbleShakeM", StumbleShakeM, 0f, 1f);
            CheckRange(errors, "runAnimReferenceSpeedMps", RunAnimReferenceSpeedMps, 1f, 50f);
            CheckRange(errors, "runAnimRateMin", RunAnimRateMin, 0.1f, RunAnimRateMax);
            CheckRange(errors, "resumeCountdownMs", ResumeCountdownMs, 0f, 5000f);
            return errors.Count == before;
        }

        private static void CheckRange(List<string> errors, string name, float value, float min, float max)
        {
            if (!(value >= min && value <= max))
            {
                errors.Add(name + " must be in " + min + ".." + max + " (is " + value + ").");
            }
        }
    }
}
