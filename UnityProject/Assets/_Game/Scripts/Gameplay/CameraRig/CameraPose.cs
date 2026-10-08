namespace JungleBooze.Gameplay.CameraRig
{
    /// <summary>Rig output in path space: S along the path, X lateral, Y height; angles in degrees.</summary>
    public struct CameraPose
    {
        public float S;
        public float X;
        public float Y;
        public float PitchDeg;
        public float YawDeg;
        public float RollDeg;
        public float FovDeg;

        /// <summary>Shake offset (already capped), path-space metres.</summary>
        public float ShakeX;
        public float ShakeY;
        public float ShakeRollDeg;
    }
}
