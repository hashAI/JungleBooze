using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Steering servo, soft edges and dodge (spec 101 §2.3), plus the fork nudge (spec 102 §3.3).</summary>
    [Serializable]
    public sealed class LateralMovementConfig
    {
        /// <summary>Servo time constant, s.</summary>
        public float Tau = 0.075f;

        /// <summary>Max lateral speed, m/s.</summary>
        public float VLatMax = 11f;

        /// <summary>Lateral acceleration when speeding up or keeping direction, m/s².</summary>
        public float AccelLat = 150f;

        /// <summary>Lateral deceleration when slowing or reversing, m/s².</summary>
        public float DecelLat = 180f;

        /// <summary>Runner half-width plus margin: xLim = path edge − this, m.</summary>
        public float EdgeMargin = 0.30f;

        /// <summary>Soft zone inside xLim where outward speed is damped, m.</summary>
        public float SoftZone = 0.35f;

        /// <summary>Outward speed factor at full soft-zone penetration.</summary>
        public float SoftEdgeMinFactor = 0.40f;

        /// <summary>Distance from xLim at which pushing outward emits EdgeBrush, m.</summary>
        public float EdgeBrushDistance = 0.05f;

        /// <summary>Inward push when the path narrows under the runner, m/s.</summary>
        public float NarrowPushSpeed = 6f;

        public float AirLateralFactor = 1f;

        public float SlideLateralFactor = 1f;

        /// <summary>Minimum total shift of a dodge from the gesture origin, m.</summary>
        public float DodgeDistance = 2.2f;

        public float DodgeVLatMax = 14f;

        public float DodgeAccel = 220f;

        /// <summary>Duration of the dodge speed/accel boost, s.</summary>
        public float DodgeBoostTime = 0.22f;

        /// <summary>Fork nudge starts this far before the divider front, m (spec 102 §3.3).</summary>
        public float ForkNudgeLookahead = 8f;

        /// <summary>Extra detection margin around the divider, m (spec 102 §3.3).</summary>
        public float ForkNudgeZone = 0.6f;

        public LateralMovementConfig Clone()
        {
            return (LateralMovementConfig)MemberwiseClone();
        }
    }
}
