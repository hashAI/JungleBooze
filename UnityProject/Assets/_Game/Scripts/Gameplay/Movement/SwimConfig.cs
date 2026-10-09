using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Swimming, dive and leap (spec 103 §4.2–4.3, §14 <c>Movement/SwimConfig</c>).</summary>
    [Serializable]
    public sealed class SwimConfig
    {
        /// <summary>Water depth under Pista that starts swimming / ends it (hysteresis), m.</summary>
        public float EnterDepth = 0.9f;

        public float ExitDepth = 0.6f;

        /// <summary>Forward speed blends to / from the swim speed over these, s.</summary>
        public float EnterBlendTime = 0.25f;

        public float ExitBlendTime = 0.30f;

        /// <summary>vSwim = this × v(d) + forward current.</summary>
        public float SpeedFactor = 0.70f;

        /// <summary>Lateral servo in water (floatier than land).</summary>
        public float Tau = 0.14f;

        public float VLatMax = 7f;

        public float AccelLat = 45f;

        public float DecelLat = 60f;

        /// <summary>Swim dodge: a strong stroke, m / m/s / m/s² / s.</summary>
        public float DodgeDistance = 1.8f;

        public float DodgeVLatMax = 9f;

        /// <summary>[ASSUMED 2026-10-09] Not in spec 103 §4.2: land dodge accel × 9/14.</summary>
        public float DodgeAccel = 140f;

        public float DodgeBoostTime = 0.25f;

        /// <summary>Author limits on currents (validator W5), m/s.</summary>
        public float MaxLateralCurrent = 2f;

        public float MaxForwardCurrent = 3f;

        /// <summary>Swim hitbox centred on the body line, m.</summary>
        public float HitboxWidth = 0.60f;

        public float HitboxDepth = 1.00f;

        public float HitboxHeight = 0.70f;

        /// <summary>Leap (airborne) hitbox, tucked, m.</summary>
        public float LeapHitboxWidth = 0.50f;

        public float LeapHitboxDepth = 0.40f;

        public float LeapHitboxHeight = 0.70f;

        /// <summary>
        /// [ASSUMED 2026-10-09] Body line above the water surface while swimming, m. Spec 103 gives obstacle extents
        /// relative to the water surface and dive/leap relative to "the surface line"; 0.35 makes the §4.6 table
        /// hold with the §4.2 hitbox (surface swimmer hits a LowBranch at +0.50 and a Snag at +0.30, a dive clears a
        /// FloatingLog at −0.40).
        /// </summary>
        public float SwimLineHeight = 0.35f;

        /// <summary>Body centre below the swim line during the Under phase, m (negative).</summary>
        public float DiveDepth = -1.10f;

        public float DiveDownTime = 0.10f;

        public float DiveUnderTime = 0.55f;

        public float DiveUpTime = 0.20f;

        /// <summary>Leap: apex 1.20 m above the swim line, airtime 0.55 s with spec 101 gravity.</summary>
        public float LeapVelocity = 8.64f;

        /// <summary>Up phase speed when a leap cancels a dive.</summary>
        public float DiveRiseFactor = 2f;

        /// <summary>Swipe down while leaping: vy = −this, a full dive starts on splash-down, m/s.</summary>
        public float DiveInSpeed = 14f;

        /// <summary>Coins this far below the water surface need Submerged (underwater coins, AC-103-14), m.</summary>
        public float UnderwaterCoinDepth = 0.5f;

        public SwimConfig Clone()
        {
            return (SwimConfig)MemberwiseClone();
        }
    }
}
