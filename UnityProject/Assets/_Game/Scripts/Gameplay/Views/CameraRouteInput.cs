namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// What the camera needs to know for one frame. The first group comes from the run (the view fills it); the
    /// second group is sampled from the route by <see cref="CameraRouteRig"/> (callers leave it zero).
    /// A plain struct, so passing it never allocates.
    /// </summary>
    public struct CameraRouteInput
    {
        /// <summary>Interpolated hero lateral position (path space).</summary>
        public float HeroX;

        /// <summary>Interpolated hero height (path space).</summary>
        public float HeroY;

        /// <summary>Interpolated hero distance along the route (the simulation z).</summary>
        public double HeroS;

        /// <summary>On a vine or in the flight after it.</summary>
        public bool Swinging;

        /// <summary>Pendulum angle in radians (positive = forward); 0 when not on a vine.</summary>
        public float SwingAngleRad;

        /// <summary>Side the canyon is open on: +1 right, -1 left, 0 unknown (the last non-zero value is kept).</summary>
        public int OpenSide;

        /// <summary>Speed Boost is active.</summary>
        public bool Boost;

        /// <summary>No roll, no FOV shift, no pull-back or side offset (spec 003 6.2).</summary>
        public bool ReduceMotion;

        // ---- Sampled from the route by the rig ----

        /// <summary>Compass heading (rad, positive toward +x) at hero + YawNearM.</summary>
        public float AimNearYawRad;

        /// <summary>Compass heading (rad) at hero + YawFarM.</summary>
        public float AimFarYawRad;

        /// <summary>Route pitch (rad, positive = climbing) at hero + YawNearM.</summary>
        public float RoutePitchRad;

        /// <summary>Route bank in degrees at the hero.</summary>
        public float BankDeg;

        /// <summary>Largest absolute curvature (1/m) at the hero and at hero + YawFarM.</summary>
        public float CurvatureAbs;
    }
}
