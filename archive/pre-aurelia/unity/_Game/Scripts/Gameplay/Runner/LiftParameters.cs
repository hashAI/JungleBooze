namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Shape of one companion Lift (GDD 15.1, 15.2) in simulation ticks, passed to
    /// <see cref="RunnerSimulation.TryStartLift"/>. Built by the companion from its tuning; the runner owns no
    /// companion tuning itself. Value type.
    /// </summary>
    public struct LiftParameters
    {
        /// <summary>Planned length of the whole lift including the descent (GDD: 4.0 s = 240 ticks).</summary>
        public int TotalTicks;

        /// <summary>Ticks to rise from the start height to <see cref="GlideHeightM"/>.</summary>
        public int RiseTicks;

        /// <summary>Ticks of the descent at the end (GDD: last 0.6 s = 36 ticks).</summary>
        public int DescentTicks;

        /// <summary>Glide height of HERO's feet above the track (GDD: 2.5 m).</summary>
        public double GlideHeightM;

        /// <summary>Invulnerability after touchdown (GDD: 0.5 s = 30 ticks).</summary>
        public int LandingInvulnerableTicks;
    }
}
