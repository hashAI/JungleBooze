using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Runner and obstacle hitboxes with forgiveness (spec 101 §2.6, §4.1).</summary>
    [Serializable]
    public sealed class HitboxConfig
    {
        public float Width = 0.5f;

        public float RunDepth = 0.4f;

        public float RunHeight = 1.5f;

        /// <summary>Airborne (tucked) height from the feet, m.</summary>
        public float AirHeight = 1.3f;

        public float SlideDepth = 0.7f;

        public float SlideHeight = 0.7f;

        /// <summary>Obstacle boxes shrink by this per side in x, m.</summary>
        public float ObstacleShrinkX = 0.10f;

        public float LowTopForgiveness = 0.10f;

        public float HighBottomForgiveness = 0.10f;

        /// <summary>Blocker crash core = hitbox narrowed by this per side, m.</summary>
        public float CrashCoreInset = 0.20f;

        public float CrashCoreMinWidth = 0.20f;

        /// <summary>Walkable top: feet within this below the effective top while not rising = land on it, m.</summary>
        public float WalkableTopTolerance = 0.15f;

        /// <summary>SideClip moves the lateral target to the blocker edge ± this, m.</summary>
        public float SideClipClearance = 0.30f;

        /// <summary>Coin pickup radius around the coin centre, added to the runner box, m.</summary>
        public float CoinPickupRadius = 0.35f;

        public HitboxConfig Clone()
        {
            return (HitboxConfig)MemberwiseClone();
        }
    }
}
