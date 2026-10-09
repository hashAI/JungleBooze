namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>What the rig follows: Pista's interpolated state in path space.</summary>
    public struct CameraTargetInput
    {
        public float S;
        public float X;
        public float Y;

        /// <summary>Floor under her, or the last ground while airborne.</summary>
        public float GroundY;

        /// <summary>Vertical velocity, m/s (vine air follow lead).</summary>
        public float Vy;

        public float VLat;
        public float Speed;
        public bool Sliding;

        /// <summary>Path tangent yaw at S, degrees (0 on a straight path).</summary>
        public float PathYawDeg;

        /// <summary>Beat modifier to blend toward (spec 103 §11).</summary>
        public CameraMode Mode;

        /// <summary>Airborne after a vine release (look-ahead).</summary>
        public bool VineAir;

        /// <summary>Stop following down (canopy fall: hold, then fade).</summary>
        public bool FallHold;
    }
}
