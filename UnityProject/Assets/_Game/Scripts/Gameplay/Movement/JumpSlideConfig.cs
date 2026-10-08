using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Jump, gravity, coyote, buffer, ground/ledge handling, slide and fast-fall (spec 101 §2.4–2.5).</summary>
    [Serializable]
    public sealed class JumpSlideConfig
    {
        public float JumpVelocity = 9.33f;

        /// <summary>Gravity while rising, m/s².</summary>
        public float GUp = 31.1f;

        /// <summary>Gravity while falling, m/s².</summary>
        public float GDown = 42f;

        /// <summary>|vy| below this gets the apex hang factor, m/s.</summary>
        public float ApexHangThreshold = 1f;

        public float ApexHangFactor = 0.6f;

        public float MaxFallSpeed = 24f;

        /// <summary>Jump still allowed this long after walking off an edge, s.</summary>
        public float CoyoteTime = 0.10f;

        /// <summary>A jump that can't execute yet is held this long, s.</summary>
        public float InputBufferTime = 0.15f;

        /// <summary>Floor rises up to this are walked up, m.</summary>
        public float StepUpHeight = 0.35f;

        /// <summary>Floor drops up to this keep the runner grounded, m.</summary>
        public float StepDownSnap = 0.35f;

        /// <summary>Below the lip she left by this much over a gap = major fall, m.</summary>
        public float FallKillDepth = 1.2f;

        /// <summary>Far lip within this distance ahead can be snapped onto, m.</summary>
        public float LedgeAssistReach = 0.30f;

        /// <summary>… if the feet are no lower than this under the lip, m.</summary>
        public float LedgeAssistDrop = 0.25f;

        /// <summary>Fall height of a hard landing (camera/haptic event), m.</summary>
        public float HardLandingFall = 2.5f;

        /// <summary>Minimum fall height for the landing haptic, m.</summary>
        public float SoftLandingFall = 1f;

        public float SlideDuration = 0.65f;

        /// <summary>Swipe down in the air: vy = min(vy, −this), m/s.</summary>
        public float FastFallSpeed = 14f;

        public float FastFallGravityFactor = 2f;

        /// <summary>A jump under a High obstacle waits this long for clearance, s.</summary>
        public float CeilingHoldTime = 0.35f;

        public JumpSlideConfig Clone()
        {
            return (JumpSlideConfig)MemberwiseClone();
        }
    }
}
