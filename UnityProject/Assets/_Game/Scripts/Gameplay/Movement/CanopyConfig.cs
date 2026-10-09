using System;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Canopy beams (spec 103 §6, §14 <c>Movement/CanopyConfig</c>).</summary>
    [Serializable]
    public sealed class CanopyConfig
    {
        /// <summary>Feet landing up to this far outside a beam edge snap onto it, m.</summary>
        public float BeamLandingAssist = 0.25f;

        /// <summary>The snap pulls x in over this, s (4 ticks).</summary>
        public float SnapTime = 4f / 60f;

        public float MinBeamWidth = 2.0f;

        public float MaxBeamWidth = 2.4f;

        public float MinGap = 3.0f;

        public float MaxGap = 3.5f;

        /// <summary>Next beam centre may be offset by up to this, m.</summary>
        public float MaxBeamOffset = 1.0f;

        /// <summary>Free corridor on beams, m (V14).</summary>
        public float MinCorridor = 1.6f;

        /// <summary>Camera stops following down after a canopy fall, holds, then fades, s.</summary>
        public float FallHoldTime = 0.8f;

        public CanopyConfig Clone()
        {
            return (CanopyConfig)MemberwiseClone();
        }
    }
}
