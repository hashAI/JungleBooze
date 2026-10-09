using System;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Pickup and reward rules (spec 103 §9.1, §9.3). Coins use the simulation's coin pad (HitboxConfig).</summary>
    [Serializable]
    public sealed class PickupConfig
    {
        /// <summary>Crystal pad around the runner box, m.</summary>
        public float CrystalPad = 0.40f;

        /// <summary>Power-up pad, m.</summary>
        public float PowerUpPad = 0.50f;

        /// <summary>Clean Line: this fraction of the risky branch's coins on a no-damage finish.</summary>
        public float CleanLineBonus = 0.5f;

        public int PerfectReleaseCoins = 10;
        public int PerfectSpanBonusCoins = 25;

        /// <summary>Shield lifetime if unused, s.</summary>
        public float ShieldDuration = 30f;

        /// <summary>The shield HUD rim fades in the last seconds, s.</summary>
        public float ShieldWarnTime = 3f;

        public PickupConfig Clone()
        {
            return (PickupConfig)MemberwiseClone();
        }
    }
}
