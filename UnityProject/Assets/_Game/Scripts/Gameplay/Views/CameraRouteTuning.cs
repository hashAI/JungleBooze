using System;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Camera tuning for the curved route (spec 003 sections 6.1 and 6.2, 14.1), plain C#, presentation only.
    /// The base follow numbers (6 m behind, 3.2 m up, FOV 60, lateral follow 70 percent, swing FOV 70, swing tilt,
    /// swing blend 250 ms) stay in <see cref="RunnerPresentationConfig"/>; this class only adds the route fields.
    /// Every default is a spec start value and so [ASSUMED] until the owner feel-tests it.
    /// </summary>
    [Serializable]
    public sealed class CameraRouteTuning
    {
        // ---- Yaw lead (6.1) ----

        /// <summary>Aim yaw = lerp(heading(sHero + YawNearM), heading(sHero + YawFarM), this).</summary>
        public float YawLeadWeight = 0.5f;

        public float YawNearM = 8f;
        public float YawFarM = 22f;

        /// <summary>Critically damped yaw smoothing time.</summary>
        public float YawTimeMs = 180f;

        public float MaxYawRateDegS = 40f;

        /// <summary>Yaw rate cap while Reduce Motion is on (6.2).</summary>
        public float MaxYawRateReducedDegS = 20f;

        // ---- Bank roll (6.1) ----

        public float RollGain = 0.8f;
        public float RollMaxDeg = 5f;
        public float RollRateMaxDegS = 20f;
        public float RollTimeMs = 200f;

        // ---- Pitch follow (6.1) ----

        public float PitchGain = 0.6f;
        public float PitchTimeMs = 250f;
        public float PitchRateMaxDegS = 15f;

        // ---- FOV (6.1) ----

        /// <summary>Extra FOV at a bend of radius <see cref="BendFullRadiusM"/> or tighter.</summary>
        public float BendFovBonusDeg = 4f;

        public float BendFullRadiusM = 75f;
        public float BendFovBlendMs = 300f;
        public float BoostFovDeg = 3f;
        public float BoostFovBlendMs = 400f;

        // ---- Swing camera (6.2) ----

        /// <summary>Extra distance behind HERO at full swing blend (6.0 to 7.5 m).</summary>
        public float SwingPullBackM = 1.5f;

        public float SwingUpM = 1.2f;

        /// <summary>Sideways offset toward the open side of the canyon at full swing blend.</summary>
        public float SwingSideOffsetM = 1.5f;

        /// <summary>Roll in degrees per degree of pendulum angle (about +-8 deg at +-55 deg).</summary>
        public float SwingRollGain = 0.15f;

        public float SwingRollMaxDeg = 10f;

        /// <summary>Blend back to the follow camera after landing.</summary>
        public float SwingReturnMs = 400f;

        /// <summary>FOV dips this far below the base FOV in the middle of the return (the "land" punch).</summary>
        public float LandFovOvershootDeg = 2f;

        // ---- Screen-edge safety (6.1 on-screen rule) ----

        /// <summary>Portrait 9:19.5 (the narrowest supported iPhone), width over height.</summary>
        public float AspectWidthOverHeight = 9f / 19.5f;

        /// <summary>Share of the half-FOV kept free at each edge.</summary>
        public float SafetyMargin = 0.06f;

        /// <summary>Largest yaw lead the safety clamp may add.</summary>
        public float SafetyMaxYawExtraDeg = 20f;

        /// <summary>Largest FOV the safety clamp may add (not applied with Reduce Motion).</summary>
        public float SafetyMaxFovExtraDeg = 8f;

        public float SafetyFovBlendMs = 250f;

        /// <summary>Lateral offset of the probed points (outer lane centers).</summary>
        public float SafetyLaneOffsetM = 2.4f;
    }
}
