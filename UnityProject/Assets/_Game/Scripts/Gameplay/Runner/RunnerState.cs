namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Read-only snapshot of the runner after a simulation tick (spec 001 section 11). Views interpolate between
    /// <see cref="RunnerSimulation.Previous"/> and <see cref="RunnerSimulation.Current"/>. Value type, no allocations.
    /// </summary>
    public struct RunnerState
    {
        /// <summary>The tick this snapshot was taken after; -1 before the first step.</summary>
        public long Tick { get; internal set; }

        /// <summary>Lateral position in m (left negative).</summary>
        public float X { get; internal set; }

        /// <summary>Height of HERO's feet above the track surface in m.</summary>
        public float Y { get; internal set; }

        /// <summary>Distance along the track in m.</summary>
        public double Z { get; internal set; }

        /// <summary>Forward speed used on this tick in m/s.</summary>
        public float Speed { get; internal set; }

        public Locomotion Locomotion { get; internal set; }

        public int TargetLane { get; internal set; }

        public int OccupiedLane { get; internal set; }

        /// <summary>Eased progress (0..1) of the active lane move; 1 when no move is active.</summary>
        public float LaneMoveProgress { get; internal set; }

        /// <summary>Progress (0..1) along the jump arc while <see cref="Locomotion.Airborne"/>; otherwise 0.</summary>
        public float JumpPhase { get; internal set; }

        public int SlideTicksLeft { get; internal set; }

        /// <summary>Ticks left in the daze window after a stumble; 0 when not dazed.</summary>
        public int DazeTicksLeft { get; internal set; }

        /// <summary>A side-stumble bounce back to the origin lane is running.</summary>
        public bool StumbleBounceActive { get; internal set; }

        public int InvulnerableTicks { get; internal set; }

        /// <summary>Hitbox height for this tick: sliding height while sliding, otherwise standing height.</summary>
        public float HitboxHeight { get; internal set; }

        public bool IsDead { get; internal set; }

        // ---- Vine swinging (GDD 7) ----

        /// <summary>Swing phase 0..1 (swing tick over <c>VineConfig.ApexTicksEstimate</c>) while <see cref="Locomotion.Carried"/>; otherwise 0.</summary>
        public float SwingPhase { get; internal set; }

        /// <summary>Ticks since the grab while on a vine; otherwise 0.</summary>
        public int SwingTick { get; internal set; }

        /// <summary>
        /// Real pendulum angle theta in radians about the fixed pivot (0 = rope straight down, positive = swung
        /// forward) while on a vine; otherwise 0.
        /// </summary>
        public float SwingAngleRad { get; internal set; }

        /// <summary>Pendulum angular speed omega in rad/s while on a vine; otherwise 0.</summary>
        public float SwingOmegaRadS { get; internal set; }

        /// <summary>
        /// World z of the fixed pivot (the vine's z) while on a vine; otherwise 0. The pivot is
        /// <c>(SwingPivotZ, lane centre x of VineLane, VineConfig.PivotHeightM)</c> and never moves; the rope end is
        /// <c>(SwingPivotZ + L sin(theta), PivotHeightM - L cos(theta))</c>.
        /// </summary>
        public double SwingPivotZ { get; internal set; }

        /// <summary>
        /// Speed blend after landing from a vine: 0 at the touchdown, 1 when the run speed is back (smoothstep
        /// input, u = ticks since landing / blend ticks); 1 when no blend is running.
        /// </summary>
        public float LandBlend { get; internal set; }

        /// <summary>Id of the vine HERO is swinging on; 0 when not on a vine.</summary>
        public int VineId { get; internal set; }

        /// <summary>Lane of the vine HERO is swinging on; -1 when not on a vine.</summary>
        public int VineLane { get; internal set; }

        /// <summary>Aimed landing / next-vine lane while on a vine; -1 otherwise.</summary>
        public int AimLane { get; internal set; }

        /// <summary>Id of the next vine aimed at (0 = the landing pad) while on a vine.</summary>
        public int AimVineId { get; internal set; }

        /// <summary>In the air after a vine release, until landing or the next grab.</summary>
        public bool InVineFlight { get; internal set; }

        /// <summary>Grade of the most recent vine release this run (None before the first).</summary>
        public JungleBooze.Gameplay.Vine.VineReleaseGrade LastReleaseGrade { get; internal set; }

        /// <summary>A too-early release swipe is buffered.</summary>
        public bool ReleaseBuffered { get; internal set; }
    }
}
