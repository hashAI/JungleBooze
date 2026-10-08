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

        /// <summary>
        /// Signature hazard "lane denial" (GDD 8.3, Jungle thorn patch): a static mass across 2 lanes that forces
        /// the remaining lane. Cannot be jumped or slid. Box 0 → 2.2 m.
        /// </summary>
        LaneDenial = 6,

        /// <summary>
        /// Signature hazard "telegraphed lane strike" (GDD 8.3: water spout, falling rocks, darts): a warning in one
        /// lane, then a strike with a box 0 → 3.0 m only while active.
        /// </summary>
        LaneStrike = 7,
    }
}
