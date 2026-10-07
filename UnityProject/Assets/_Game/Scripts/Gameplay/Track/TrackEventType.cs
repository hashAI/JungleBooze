namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Track, coin and score events (spec 002 section 13.1, plus spawn/despawn and score events for views).
    /// Values are append-only. Field use per type is documented on each member (see <see cref="TrackEvent"/>).
    /// </summary>
    public enum TrackEventType : byte
    {
        None = 0,

        /// <summary>A chunk was generated. EntityId = chunk serial, Value = library index, Flags: Mirrored, SeamFallback; Kind; Lane = tier.</summary>
        ChunkSpawned = 1,

        /// <summary>A chunk left the ring. EntityId = chunk serial, Value = library index.</summary>
        ChunkDespawned = 2,

        /// <summary>HERO's centre crossed the chunk start. EntityId = chunk serial, Value = library index, Flags: Mirrored; Kind; Lane = tier.</summary>
        ChunkEntered = 3,

        /// <summary>HERO entered the first chunk of a new tier. Value = tier (1-based).</summary>
        TierChanged = 4,

        /// <summary>An obstacle was generated. EntityId = obstacle id, Archetype, Value = lane mask, Lane = from lane, ToLane = to lane.</summary>
        ObstacleSpawned = 5,

        /// <summary>An obstacle left the ring. EntityId = obstacle id, Archetype.</summary>
        ObstacleDespawned = 6,

        /// <summary>A mover started. EntityId = obstacle id, Lane = from, ToLane = to (Value = to as well).</summary>
        MoverStarted = 7,

        /// <summary>A mover reached its end lane. EntityId = obstacle id, Lane = end lane.</summary>
        MoverSettled = 8,

        /// <summary>A coin was generated. EntityId = coin id, Lane = nearest lane.</summary>
        CoinSpawned = 9,

        /// <summary>A coin left the ring (collected or not). EntityId = coin id, Flags: Collected.</summary>
        CoinDespawned = 10,

        /// <summary>HERO collected a coin. EntityId = coin id, Lane, Value = coin value.</summary>
        CoinCollected = 11,

        /// <summary>A coin streak completed. Value = streak length.</summary>
        CoinStreak = 12,

        /// <summary>Bonus points. Value = points, Flags: NearMissBonus or StreakBonus; EntityId = obstacle id for near-misses.</summary>
        ScoreBonus = 13,

        /// <summary>The run score changed this tick. Value = new score clamped to int (read <see cref="RunTotals.Score"/> for the full value).</summary>
        ScoreChanged = 14,

        /// <summary>The generator used the seam fallback breather. EntityId = chunk serial.</summary>
        SeamFallback = 15,

        /// <summary>A coin left HERO's lane uncollected and reset the streak. EntityId = coin id.</summary>
        CoinMissed = 16,
    }
}
