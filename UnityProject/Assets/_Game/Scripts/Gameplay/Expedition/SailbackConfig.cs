using System;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Sailback behaviour and discovery (spec 103 §7, §14 <c>Creatures/SailbackConfig</c>). m, s, m/s.</summary>
    [Serializable]
    public sealed class SailbackConfig
    {
        /// <summary>Head tracks Pista once Δs (creature − Pista) is below this.</summary>
        public float HeadTrackDistance = 30f;

        public float AlertDistance = 22f;

        public float LaunchDistance = 14f;

        /// <summary>Pista within this laterally of a perched animal (and within head-track range) startles it at once.</summary>
        public float StartleLateral = 2f;

        public float AlertTime = 0.4f;

        /// <summary>[ASSUMED 2026-10-09] Drop off the branch before the glide, s and m.</summary>
        public float LaunchTime = 0.3f;

        public float LaunchDrop = 0.6f;

        /// <summary>Glide speed = Pista's speed + this.</summary>
        public float GlideSpeedBonus = 2f;

        /// <summary>Flock members launch this much later each.</summary>
        public float FlockOffsetTime = 0.25f;

        /// <summary>Never below this height over the path, m (AC-103-29).</summary>
        public float MinHeightOverPath = 2.5f;

        /// <summary>Observed = Δs in [min, max] and |Δx| ≤ this (proxy for "on screen").</summary>
        public float ObserveMinDs = 2f;

        public float ObserveMaxDs = 35f;

        public float ObserveMaxDx = 10f;

        /// <summary>Cumulative observed time that makes a discovery, s (48 ticks).</summary>
        public float ObserveTime = 0.8f;

        public SailbackConfig Clone()
        {
            return (SailbackConfig)MemberwiseClone();
        }
    }
}
