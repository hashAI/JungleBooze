namespace JungleBooze.Core
{
    /// <summary>
    /// Stream ids for <see cref="IRandom.Fork(ulong)"/> (ARCHITECTURE 5.1, spec 002 section 8.1). The run's root
    /// generator is seeded with the run seed and forked once per subsystem at run setup, in the order listed here.
    /// Ids are stored implicitly in every replay: never renumber or reuse one, only append. [ASSUMED values from
    /// spec 002; tech-architect owns this file.]
    /// </summary>
    public static class RandomStreamIds
    {
        /// <summary>Chunk selection, mirroring and breather timing (the track generator is the only user).</summary>
        public const ulong TrackGeneration = 1;

        /// <summary>Cosmetic obstacle variants that do not change hitboxes (later).</summary>
        public const ulong ObstacleVariants = 2;

        /// <summary>Power-up placement (week 3).</summary>
        public const ulong Pickups = 3;

        /// <summary>Bot decisions (reaction delay, errors).</summary>
        public const ulong Bot = 4;

        /// <summary>Cosmetic simulation-side randomness that must still replay identically.</summary>
        public const ulong Cosmetic = 5;

        /// <summary>
        /// Vine section timing and choice (GDD 7.5). Forked after <see cref="TrackGeneration"/>, so the track
        /// stream is unchanged by vines.
        /// </summary>
        public const ulong VineSchedule = 6;
    }
}
