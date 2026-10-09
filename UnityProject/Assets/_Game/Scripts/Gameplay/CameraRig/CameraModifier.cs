using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>Offsets one camera modifier adds to the profile at full weight. Negative follow values = unchanged.</summary>
    [Serializable]
    public sealed class CameraModifier
    {
        /// <summary>Added to the distance behind Pista, m.</summary>
        public float Back;

        /// <summary>Added to the height, m.</summary>
        public float Height;

        /// <summary>Added to the downward pitch, degrees (negative looks up).</summary>
        public float PitchDeg;

        public float FovDeg;

        /// <summary>Replaces the lateral follow (≥ 0).</summary>
        public float LateralFollow = -1f;

        /// <summary>Replaces the vertical (air) follow (≥ 0).</summary>
        public float AirFollow = -1f;

        /// <summary>Vertical bob, m and Hz (off with Reduced Motion).</summary>
        public float BobAmplitude;

        public float BobHz;

        public CameraModifier Clone()
        {
            return (CameraModifier)MemberwiseClone();
        }
    }
}
