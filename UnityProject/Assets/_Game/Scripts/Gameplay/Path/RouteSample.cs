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

        /// <summary>Beat this sample belongs to (spec 003 section 4.2). Presentation hint and validator input.</summary>
        public RouteBeatKind Beat;

        /// <summary>Running number of the beat (0 for the straight route); consecutive beats of one kind differ here.</summary>
        public int BeatId;

        /// <summary>True when the emergency ease (spec 003 section 4.1) shaped this sample.</summary>
        public bool Emergency;
    }
}
