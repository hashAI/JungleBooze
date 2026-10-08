using System;

namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>Third-person camera profile for one orientation (spec 101 §5). Distances m, angles degrees, times s.</summary>
    [Serializable]
    public sealed class CameraProfile
    {
        public string Name = "Landscape";

        /// <summary>Distance behind Pista along the path tangent.</summary>
        public float OffsetBack = 5.5f;

        /// <summary>Height above Pista's ground level.</summary>
        public float Height = 2.4f;

        /// <summary>Downward pitch.</summary>
        public float PitchDeg = 9f;

        /// <summary>Vertical FOV at v0.</summary>
        public float FovBaseDeg = 55f;

        /// <summary>FOV added at vMax (linear in (v − v0)/(vMax − v0)).</summary>
        public float FovGainDeg = 6f;

        public float FovHalfLife = 0.5f;

        /// <summary>Camera lateral position = this × Pista's x.</summary>
        public float LateralFollow = 0.70f;

        public float LateralHalfLife = 0.10f;

        public float YawHalfLife = 0.25f;

        public float GroundHalfLife = 0.30f;

        /// <summary>Camera rises by this fraction of the jump height.</summary>
        public float AirFollow = 0.35f;

        public float AirHalfLife = 0.20f;

        /// <summary>Camera dips by this while sliding (positive number = down).</summary>
        public float SlideDip = 0.25f;

        public float SlideDipHalfLife = 0.12f;

        /// <summary>Max roll, proportional to vLat / vLatMax.</summary>
        public float BankMaxDeg = 2f;

        public float BankHalfLife = 0.12f;

        public float ShakeHardLandingAmplitude = 0.04f;

        public float ShakeHardLandingTime = 0.18f;

        public float ShakeMinorHitAmplitude = 0.07f;

        public float ShakeMinorHitTime = 0.25f;

        public float ShakeCrashAmplitude = 0.12f;

        public float ShakeCrashTime = 0.35f;

        /// <summary>Smooth-noise frequency, Hz.</summary>
        public float ShakeFrequency = 18f;

        /// <summary>Hard cap on the shake position offset, m.</summary>
        public float ShakeMaxPosition = 0.12f;

        /// <summary>Hard cap on the shake rotation, degrees.</summary>
        public float ShakeMaxRotationDeg = 1f;

        /// <summary>Reduced Motion: FOV gain multiplier.</summary>
        public float ReducedMotionFovGainFactor = 0.5f;

        /// <summary>Reduced Motion: slide dip multiplier.</summary>
        public float ReducedMotionSlideDipFactor = 0.5f;

        /// <summary>Blend time when switching to this profile (orientation change).</summary>
        public float BlendTime = 0.4f;

        public float NearClip = 0.15f;

        public float FarClip = 220f;

        public CameraProfile Clone()
        {
            return (CameraProfile)MemberwiseClone();
        }

        public static CameraProfile DefaultPortrait()
        {
            return new CameraProfile
            {
                Name = "Portrait",
                OffsetBack = 6.2f,
                Height = 3.0f,
                PitchDeg = 12f,
                FovBaseDeg = 65f,
                FovGainDeg = 5f,
                LateralFollow = 0.80f,
                AirFollow = 0.30f,
                SlideDip = 0.20f,
                BankMaxDeg = 1.5f,
            };
        }
    }
}
