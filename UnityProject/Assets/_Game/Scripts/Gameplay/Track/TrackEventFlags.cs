namespace JungleBooze.Gameplay.Track
{
    /// <summary>Bit values for <see cref="TrackEvent.Flags"/>. Meaning depends on the event type.</summary>
    public static class TrackEventFlags
    {
        /// <summary>ChunkSpawned, ChunkEntered: the chunk is mirrored.</summary>
        public const byte Mirrored = 1 << 0;

        /// <summary>ChunkSpawned: the chunk is the seam fallback breather.</summary>
        public const byte SeamFallback = 1 << 1;

        /// <summary>CoinDespawned: the coin had been collected.</summary>
        public const byte Collected = 1 << 0;

        /// <summary>ScoreBonus: from a near-miss.</summary>
        public const byte NearMissBonus = 1 << 0;

        /// <summary>ScoreBonus: from a completed coin streak.</summary>
        public const byte StreakBonus = 1 << 1;
    }
}
