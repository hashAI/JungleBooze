using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Vine grab, swing and release (spec 103 §5, §14 <c>Movement/VineConfig</c>). Angles in degrees.</summary>
    [Serializable]
    public sealed class VineConfig
    {
        /// <summary>Grab zone: s ∈ [sLip − before, sLip + after], |x − xVine| ≤ half width, feet in [−below, +above] of the takeoff floor.</summary>
        public float GrabBefore = 1.5f;

        public float GrabAfter = 1.0f;

        public float GrabHalfWidth = 1.2f;

        public float GrabFeetBelow = 1.0f;

        public float GrabFeetAbove = 2.0f;

        public float HandToFeet = 1.90f;

        public float Theta0Deg = -35f;

        public float ThetaEndDeg = 55f;

        /// <summary>Swing length, s (60 ticks; independent of run speed).</summary>
        public float SwingTime = 1.0f;

        public float GrabSnapTime = 0.10f;

        /// <summary>Release window opens at this swing phase; earlier swipes are held until then (Good).</summary>
        public float ReleaseOpen = 0.60f;

        /// <summary>Perfect window (inclusive), swing phase.</summary>
        public float PerfectStart = 0.75f;

        public float PerfectEnd = 0.90f;

        public float ReleaseSpeed = 10f;

        public float PerfectBoost = 1.35f;

        /// <summary>Forward speed after the landing blends from the launch speed to v(d) over this, s.</summary>
        public float LandBlendTime = 0.30f;

        /// <summary>Authoring defaults per vine and the validator's limits (V13).</summary>
        public float DefaultLength = 6f;

        public float DefaultAnchorHeight = 8f;

        public float MinLandingOffset = 5f;

        public float FunnelMaxWidth = 2.4f;

        public float SwingCorridor = 1.0f;

        /// <summary>Revive after a death near a vine: this far before the lip (spec 103 §5.5), m.</summary>
        public float ReviveBeforeLip = 8f;

        public VineConfig Clone()
        {
            return (VineConfig)MemberwiseClone();
        }
    }
}
