using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>
    /// Camera modifiers per beat (spec 103 §11, §14 <c>Camera/CameraModifiers</c>): swim, deep dive, swing, canopy and
    /// the timed vista beat, plus the vine release look-ahead and the canopy fall hold. Blend over
    /// <see cref="BlendTime"/>; Reduced Motion removes the bob and halves the vista FOV.
    /// </summary>
    [Serializable]
    public sealed class CameraModifiers
    {
        public float BlendTime = 0.4f;

        /// <summary>Swim: height −0.6 m, pitch −3°, back −0.5 m, lateral follow 0.75, bob ±0.05 m at 0.8 Hz.</summary>
        public CameraModifier Swim = new CameraModifier { Back = -0.5f, Height = -0.6f, PitchDeg = -3f, LateralFollow = 0.75f, BobAmplitude = 0.05f, BobHz = 0.8f };

        /// <summary>Deep dive: follows under water along the passage, FOV −4°.</summary>
        public CameraModifier DeepDive = new CameraModifier { FovDeg = -4f, Height = -0.4f, PitchDeg = -4f, Back = -1.0f };

        /// <summary>Swing: back +1.0 m, up +0.8 m, FOV +4°, vertical follow 40 % of the arc.</summary>
        public CameraModifier Swing = new CameraModifier { Back = 1.0f, Height = 0.8f, FovDeg = 4f, AirFollow = 0.4f };

        /// <summary>Canopy: height +0.4 m, lateral follow 0.85.</summary>
        public CameraModifier Canopy = new CameraModifier { Height = 0.4f, LateralFollow = 0.85f };

        /// <summary>Vista beat: FOV +4°, pitch up 3°, for <see cref="VistaDuration"/>.</summary>
        public CameraModifier Vista = new CameraModifier { FovDeg = 4f, PitchDeg = -3f };

        public float VistaDuration = 2.0f;

        /// <summary>How much of the body's depth below the surface the deep-dive camera follows (1 = all).</summary>
        public float DeepDiveFollow = 1f;

        /// <summary>Look-ahead after a vine release, m, and its smoothing.</summary>
        public float ReleaseLookAhead = 4f;

        public float LookAheadHalfLife = 0.25f;

        public CameraModifiers Clone()
        {
            var copy = (CameraModifiers)MemberwiseClone();
            copy.Swim = Swim.Clone();
            copy.DeepDive = DeepDive.Clone();
            copy.Swing = Swing.Clone();
            copy.Canopy = Canopy.Clone();
            copy.Vista = Vista.Clone();
            return copy;
        }
    }
}
