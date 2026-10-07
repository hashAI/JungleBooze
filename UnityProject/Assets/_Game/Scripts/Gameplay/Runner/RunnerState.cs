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

        /// <summary>Collision stage hook; always 0 until stumbles exist.</summary>
        public int DazeTicksLeft { get; internal set; }

        public int InvulnerableTicks { get; internal set; }

        /// <summary>Hitbox height for this tick: sliding height while sliding, otherwise standing height.</summary>
        public float HitboxHeight { get; internal set; }

        public bool IsDead { get; internal set; }
    }
}
