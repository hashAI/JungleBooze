namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// The shared obstacle kit (spec 001 section 9.1, spec 002 section 5). Stored as a byte in
    /// <see cref="RunnerEvent.Archetype"/> and in <see cref="ObstacleBox.Archetype"/>. Values are append-only:
    /// replays, sim reports and chunk assets store them.
    /// </summary>
    public enum ObstacleArchetype : byte
    {
        /// <summary>No obstacle (for example a death by falling off a plain ledge).</summary>
        None = 0,

        /// <summary>Jump-over. Box 0 → 0.8 m.</summary>
        LowBarrier = 1,

        /// <summary>Slide-under. Box 1.1 → 3.0 m.</summary>
        HighBarrier = 2,

        /// <summary>Lane-block. Box 0 → 3.0 m.</summary>
        FullBlock = 3,

        /// <summary>Moves sideways by one lane (rolling boulder). Box 0 → 1.9 m.</summary>
        Mover = 4,

        /// <summary>Missing ground. Has no box; answered through <see cref="ITrackQuery.HasGround"/>.</summary>
        Gap = 5,
    }
}
