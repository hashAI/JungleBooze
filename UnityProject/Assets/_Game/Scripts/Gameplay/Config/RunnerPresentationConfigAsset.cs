using System.Collections.Generic;
using JungleBooze.Gameplay.Views;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Designer-facing presentation tuning (spec 001 section 3.4), saved as
    /// <c>Assets/_Game/Config/Resources/RunnerPresentationTuning.asset</c>. Defaults are the spec start values.
    /// </summary>
    [CreateAssetMenu(fileName = "RunnerPresentationTuning", menuName = "JungleBooze/Config/Runner Presentation Tuning")]
    public sealed class RunnerPresentationConfigAsset : ScriptableObject
    {
        [Header("Camera")]
        [SerializeField] private float _cameraOffsetBehindM = 6.0f;
        [SerializeField] private float _cameraOffsetUpM = 3.2f;
        [SerializeField] private float _cameraLookAheadM = 8.0f;
        [SerializeField] private float _cameraLookAtHeightM = 1.0f;
        [SerializeField] private float _cameraFovDeg = 60f;
        [SerializeField] private float _cameraLateralFollow = 0.70f;
        [SerializeField] private float _cameraLateralSmoothMs = 90f;
        [SerializeField] private float _cameraVerticalFollow = 0.25f;
        [SerializeField] private float _cameraVerticalSmoothMs = 150f;

        [Header("Fog and view distance")]
        [SerializeField] private float _fogStartM = 45f;
        [SerializeField] private float _fogEndM = 90f;
        [SerializeField] private float _minVisibleTrackS = 1.6f;

        [Header("Death, bump, stumble")]
        [SerializeField] private float _hitPauseMs = 350f;
        [SerializeField] private float _deathCameraHoldMs = 800f;
        [SerializeField] private float _laneBumpWobbleMs = 40f;
        [SerializeField] private float _laneBumpWobbleM = 0.12f;
        [SerializeField] private float _stumbleShakeMs = 200f;
        [SerializeField] private float _stumbleShakeM = 0.08f;

        [Header("Run animation")]
        [SerializeField] private float _runAnimReferenceSpeedMps = 10.0f;
        [SerializeField] private float _runAnimRateMin = 0.8f;
        [SerializeField] private float _runAnimRateMax = 1.6f;

        [Header("Pause")]
        [SerializeField] private float _resumeCountdownMs = 1500f;

        public RunnerPresentationConfig ToConfig()
        {
            return new RunnerPresentationConfig
            {
                CameraOffsetBehindM = _cameraOffsetBehindM,
                CameraOffsetUpM = _cameraOffsetUpM,
                CameraLookAheadM = _cameraLookAheadM,
                CameraLookAtHeightM = _cameraLookAtHeightM,
                CameraFovDeg = _cameraFovDeg,
                CameraLateralFollow = _cameraLateralFollow,
                CameraLateralSmoothMs = _cameraLateralSmoothMs,
                CameraVerticalFollow = _cameraVerticalFollow,
                CameraVerticalSmoothMs = _cameraVerticalSmoothMs,
                FogStartM = _fogStartM,
                FogEndM = _fogEndM,
                MinVisibleTrackS = _minVisibleTrackS,
                HitPauseMs = _hitPauseMs,
                DeathCameraHoldMs = _deathCameraHoldMs,
                LaneBumpWobbleMs = _laneBumpWobbleMs,
                LaneBumpWobbleM = _laneBumpWobbleM,
                StumbleShakeMs = _stumbleShakeMs,
                StumbleShakeM = _stumbleShakeM,
                RunAnimReferenceSpeedMps = _runAnimReferenceSpeedMps,
                RunAnimRateMin = _runAnimRateMin,
                RunAnimRateMax = _runAnimRateMax,
                ResumeCountdownMs = _resumeCountdownMs,
            };
        }

        public bool Validate(List<string> errors)
        {
            return ToConfig().Validate(errors);
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (RunnerPresentationConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
