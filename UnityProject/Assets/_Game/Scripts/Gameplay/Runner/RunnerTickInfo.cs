namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// HERO's state at a hook point inside one tick (spec 002 section 13.3), passed to
    /// <see cref="IRunnerStepHooks"/>. "Prev" values are the end of the previous tick; the others are the values at
    /// the hook point (after step 7 for <see cref="IRunnerStepHooks.OnTrackUpdate"/>, after step 11 for the
    /// coin and score hooks). Value type; building it does not allocate.
    /// </summary>
    public struct RunnerTickInfo
    {
        public long Tick;

        public float X;

        public float XPrev;

        /// <summary>Height of HERO's feet above the track surface (m).</summary>
        public float Y;

        public float YPrev;

        /// <summary>HERO center z (m).</summary>
        public double Z;

        public double ZPrev;

        /// <summary>Forward speed used on this tick (m/s).</summary>
        public double Speed;

        /// <summary>Hitbox height (standing or sliding) at the hook point.</summary>
        public float HitboxHeight;

        /// <summary><c>RunnerConfig.PlayerHitboxWidthM / 2</c>.</summary>
        public float HalfWidth;

        /// <summary><c>RunnerConfig.PlayerHitboxDepthM / 2</c>.</summary>
        public float HalfDepth;

        /// <summary>Lane whose center is nearest to <see cref="X"/> (spec 001 5.1).</summary>
        public int OccupiedLane;

        /// <summary>HERO is dead (at the coin and score hooks: died on this tick).</summary>
        public bool IsDead;

        /// <summary>HERO stumbled on this tick (a stumble resets the coin streak, spec 001 9.4.6).</summary>
        public bool StumbledThisTick;

        /// <summary>Number of <see cref="RunnerEventType.NearMiss"/> events emitted on this tick (score bonus).</summary>
        public int NearMissesThisTick;

        /// <summary>Grade of a vine release on this tick; None if HERO did not let go of a vine.</summary>
        public JungleBooze.Gameplay.Vine.VineReleaseGrade VineRelease;

        /// <summary>Chain score multiplier of this tick's vine release (GDD 7.3 step 5).</summary>
        public float VineBonusMultiplier;

        /// <summary>HERO grabbed a vine on this tick.</summary>
        public bool VineGrabbedThisTick;

        /// <summary>HERO's front face z (<see cref="Z"/> + <see cref="HalfDepth"/>), used by mover triggers.</summary>
        public double FrontZ => Z + HalfDepth;
    }
}
