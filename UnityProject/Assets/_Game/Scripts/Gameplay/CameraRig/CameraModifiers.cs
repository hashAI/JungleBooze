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

        /// <summary>
        /// Look-ahead after a vine release, m: the camera aims this much further ahead (pitch up), it does not move
        /// forward (moving it shortened the follow distance and dropped Pista out of the bottom of the frame).
        /// </summary>
        public float ReleaseLookAhead = 4f;

        /// <summary>Cap on the look-ahead pitch-up, degrees.</summary>
        public float ReleaseLookAheadMaxPitchDeg = 3f;

        public float LookAheadHalfLife = 0.25f;

        /// <summary>
        /// Vertical follow lead in the vine air, s: the air target leads by vy × this, cancelling the air spring's
        /// lag so the camera turns down with Pista instead of still rising while she falls.
        /// </summary>
        public float VineAirLead = 0.22f;

        /// <summary>
        /// Swing and vine air framing guard: Pista (feet to <see cref="FrameGuardBodyTop"/>) stays at least this
        /// fraction of the screen height inside the top and bottom edges. A limit, rarely reached with the lead.
        /// </summary>
        public float FrameGuardMargin = 0.08f;

        /// <summary>Raised hands above the feet while hanging, m.</summary>
        public float FrameGuardBodyTop = 1.95f;

        /// <summary>
        /// Occlusion: a vine Pista is not holding is hidden once its rope is less than this far ahead of the camera
        /// (m along the path). After a release the rope swings back toward the camera, which follows on the same
        /// line, and covered Pista at the top of her arc.
        /// </summary>
        public float VineNearHide = 3.5f;

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
