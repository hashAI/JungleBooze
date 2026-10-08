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

        public float VLat;
        public float Speed;
        public bool Sliding;

        /// <summary>Path tangent yaw at S, degrees (0 on a straight path).</summary>
        public float PathYawDeg;
    }
}
