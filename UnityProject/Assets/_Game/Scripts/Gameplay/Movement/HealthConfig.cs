using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Health, i-frames, regeneration, shield, revive and stumble (spec 101 §4.2–4.3).</summary>
    [Serializable]
    public sealed class HealthConfig
    {
        public int MaxHealth = 3;

        /// <summary>I-frames after a minor hit, s.</summary>
        public float InvulnerableTime = 1.2f;

        /// <summary>Metres without damage for +1 segment.</summary>
        public float RegenDistance = 350f;

        /// <summary>First run only: health cannot drop below 1 for this long, s.</summary>
        public float FtueHealthFloorTime = 60f;

        /// <summary>I-frames after the shield absorbs a hit, s.</summary>
        public float ShieldInvulnerableTime = 1f;

        /// <summary>I-frames after a revive, s.</summary>
        public float ReviveInvulnerableTime = 2f;

        /// <summary>Revive point is the last grounded position at least this far before the hazard, m.</summary>
        public float ReviveBackDistance = 6f;

        /// <summary>Obstacles in this distance after the revive point are removed, m.</summary>
        public float ReviveClearDistance = 30f;

        /// <summary>Stumble animation layered on the run, s (presentation hint).</summary>
        public float StumbleAnimationTime = 0.35f;

        /// <summary>Forward speed reaches 0 this fast after a crash, s.</summary>
        public float CrashStopTime = 0.1f;

        /// <summary>Revive safe points are sampled this often while grounded, m.</summary>
        public float SafePointSpacing = 1f;

        /// <summary>The view blinks Pista at this rate during i-frames, Hz (spec 101 §4.1).</summary>
        public float InvulnerableBlinkHz = 8f;

        public HealthConfig Clone()
        {
            return (HealthConfig)MemberwiseClone();
        }
    }
}
