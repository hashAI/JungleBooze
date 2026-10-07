namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// One stored route sample (spec 003 section 3.1, one every 1.0 m of arc length). Positions are doubles so a
    /// long run keeps millimetre precision before the floating origin is applied; angles are unwrapped radians.
    /// Yaw 0 looks along +z and positive yaw turns toward +x.
    /// </summary>
    public struct RouteSample
    {
        public double X;
        public double Y;
        public double Z;
        public float YawRad;
        public float PitchRad;
        public float BankRad;
        public float Curvature;
        public float HalfWidthM;
        public PathLayer Layer;
        public PathSurface Surface;
    }
}
