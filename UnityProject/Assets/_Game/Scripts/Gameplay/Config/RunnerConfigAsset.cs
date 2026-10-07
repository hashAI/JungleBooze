using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Designer-facing runner tuning (spec 001 section 3.1), saved as <c>Assets/_Game/Config/RunnerTuning.asset</c>.
    /// Values are in designer units; <see cref="ToConfig"/> converts them into the tick-based <see cref="RunnerConfig"/>
    /// the simulation runs on. Defaults are the spec start values.
    /// </summary>
    [CreateAssetMenu(fileName = "RunnerTuning", menuName = "JungleBooze/Config/Runner Tuning")]
    public sealed class RunnerConfigAsset : ScriptableObject
    {
        [Header("Lanes")]
        [SerializeField] private int _laneCount = 3;
        [SerializeField] private float _laneWidthM = 2.4f;
        [SerializeField] private int _startLane = 1;
        [SerializeField] private float _laneSwitchMs = 120f;
        [SerializeField] private float _laneSwitchEaseExponent = 2f;
        [SerializeField] private float _laneQueueStartFraction = 0.5f;
        [SerializeField] private float _edgeForgivenessFraction = 0.6f;

        [Header("Jump, fast-fall, slide")]
        [SerializeField] private float _jumpApexHeightM = 1.5f;
        [SerializeField] private float _jumpAirtimeMs = 600f;
        [SerializeField] private float _fastFallMinSpeedMps = 15f;
        [SerializeField] private float _fastFallMaxMs = 100f;
        [SerializeField] private float _slideMs = 650f;

        [Header("Forgiveness")]
        [SerializeField] private float _inputBufferMs = 150f;
        [SerializeField] private float _coyoteMs = 80f;

        [Header("Hitbox")]
        [SerializeField] private float _playerHitboxWidthM = 0.7f;
        [SerializeField] private float _playerHitboxDepthM = 0.5f;
        [SerializeField] private float _standingHeightM = 1.8f;
        [SerializeField] private float _slidingHeightM = 0.8f;
        [SerializeField] private float _fallDeathDepthM = 1.0f;

        [Header("Run start")]
        [SerializeField] private float _runStartRampMs = 500f;
        [SerializeField] private float _runStartSpeedFraction = 0.5f;

        [Header("Stumble and near-miss")]
        [SerializeField] private float _stumbleBounceMs = 150f;
        [SerializeField] private float _stumbleDazeMs = 3000f;
        [SerializeField] private float _nearMissDistanceM = 0.35f;

        [Header("Events")]
        [SerializeField] private int _eventBufferCapacity = 64;

        /// <summary>Copies the serialized fields into a plain C# value object.</summary>
        public RunnerDesignValues ToDesignValues()
        {
            return new RunnerDesignValues
            {
                LaneCount = _laneCount,
                LaneWidthM = _laneWidthM,
                StartLane = _startLane,
                LaneSwitchMs = _laneSwitchMs,
                LaneSwitchEaseExponent = _laneSwitchEaseExponent,
                LaneQueueStartFraction = _laneQueueStartFraction,
                EdgeForgivenessFraction = _edgeForgivenessFraction,
                JumpApexHeightM = _jumpApexHeightM,
                JumpAirtimeMs = _jumpAirtimeMs,
                FastFallMinSpeedMps = _fastFallMinSpeedMps,
                FastFallMaxMs = _fastFallMaxMs,
                SlideMs = _slideMs,
                InputBufferMs = _inputBufferMs,
                CoyoteMs = _coyoteMs,
                PlayerHitboxWidthM = _playerHitboxWidthM,
                PlayerHitboxDepthM = _playerHitboxDepthM,
                StandingHeightM = _standingHeightM,
                SlidingHeightM = _slidingHeightM,
                FallDeathDepthM = _fallDeathDepthM,
                RunStartRampMs = _runStartRampMs,
                RunStartSpeedFraction = _runStartSpeedFraction,
                StumbleBounceMs = _stumbleBounceMs,
                StumbleDazeMs = _stumbleDazeMs,
                NearMissDistanceM = _nearMissDistanceM,
                EventBufferCapacity = _eventBufferCapacity,
            };
        }

        /// <summary>Validates and converts to ticks. Throws <see cref="System.ArgumentException"/> when out of range.</summary>
        public RunnerConfig ToConfig()
        {
            return RunnerConfig.FromDesignValues(ToDesignValues());
        }

        /// <summary>Range checks from spec 001 section 3.1. Appends one message per problem.</summary>
        public bool Validate(List<string> errors)
        {
            return ToDesignValues().Validate(errors);
        }

        /// <summary>Overwrites every serialized field. For tests and editor tooling.</summary>
        internal void SetDesignValues(RunnerDesignValues v)
        {
            _laneCount = v.LaneCount;
            _laneWidthM = v.LaneWidthM;
            _startLane = v.StartLane;
            _laneSwitchMs = v.LaneSwitchMs;
            _laneSwitchEaseExponent = v.LaneSwitchEaseExponent;
            _laneQueueStartFraction = v.LaneQueueStartFraction;
            _edgeForgivenessFraction = v.EdgeForgivenessFraction;
            _jumpApexHeightM = v.JumpApexHeightM;
            _jumpAirtimeMs = v.JumpAirtimeMs;
            _fastFallMinSpeedMps = v.FastFallMinSpeedMps;
            _fastFallMaxMs = v.FastFallMaxMs;
            _slideMs = v.SlideMs;
            _inputBufferMs = v.InputBufferMs;
            _coyoteMs = v.CoyoteMs;
            _playerHitboxWidthM = v.PlayerHitboxWidthM;
            _playerHitboxDepthM = v.PlayerHitboxDepthM;
            _standingHeightM = v.StandingHeightM;
            _slidingHeightM = v.SlidingHeightM;
            _fallDeathDepthM = v.FallDeathDepthM;
            _runStartRampMs = v.RunStartRampMs;
            _runStartSpeedFraction = v.RunStartSpeedFraction;
            _stumbleBounceMs = v.StumbleBounceMs;
            _stumbleDazeMs = v.StumbleDazeMs;
            _nearMissDistanceM = v.NearMissDistanceM;
            _eventBufferCapacity = v.EventBufferCapacity;
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (RunnerConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
