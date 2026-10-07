using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// How the track packs its spec 002 section 13.1 events into <see cref="RunnerEvent"/> fields. The track writes
    /// them into the runner's event buffer (<see cref="RunnerSimulation.EmitExternal"/>), so views read one ordered
    /// stream through <c>IRunView.OnRunnerEvent</c>.
    /// <list type="bullet">
    /// <item><c>ChunkEntered</c>: Value = library index, EntityId = chunk serial, Lane = tier,
    /// Flags = <see cref="RunnerEventFlags.ChunkMirrored"/> | (kind &lt;&lt; <see cref="ChunkKindShift"/>).</item>
    /// <item><c>TierChanged</c>: Value = tier (1-based).</item>
    /// <item><c>MoverStarted</c>: EntityId = obstacle id, Lane = from lane, Value = to lane, Archetype = Mover.</item>
    /// <item><c>MoverSettled</c>: EntityId = obstacle id, Lane = end lane, Archetype = Mover.</item>
    /// <item><c>CoinCollected</c>: EntityId = coin id, Lane = coin lane, Value = coin value.</item>
    /// <item><c>CoinStreak</c>: Value = streak length.</item>
    /// <item><c>ScoreBonus</c>: Value = points, Flags = <see cref="RunnerEventFlags.BonusNearMiss"/> or
    /// <see cref="RunnerEventFlags.BonusStreak"/>.</item>
    /// </list>
    /// </summary>
    public static class TrackEventCodes
    {
        /// <summary>The chunk kind sits in bits 4–7 of a <c>ChunkEntered</c> event's Flags.</summary>
        public const int ChunkKindShift = 4;

        public static byte PackChunkFlags(bool mirrored, ChunkKind kind)
        {
            return (byte)((mirrored ? RunnerEventFlags.ChunkMirrored : 0) | ((int)kind << ChunkKindShift));
        }

        public static ChunkKind UnpackChunkKind(byte flags)
        {
            return (ChunkKind)(flags >> ChunkKindShift);
        }

        public static bool UnpackChunkMirrored(byte flags)
        {
            return (flags & RunnerEventFlags.ChunkMirrored) != 0;
        }
    }
}
