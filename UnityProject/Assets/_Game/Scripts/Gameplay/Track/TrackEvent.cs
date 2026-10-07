using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// One track/coin/score event (see <see cref="TrackEventType"/> for field use per type). Plain struct written
    /// into <see cref="TrackEventBuffer"/>; no allocations.
    /// </summary>
    public struct TrackEvent
    {
        public TrackEventType Type;
        public long Tick;

        /// <summary>Obstacle id, coin id or chunk serial, depending on <see cref="Type"/>.</summary>
        public int EntityId;

        public int Value;
        public byte Lane;
        public byte ToLane;
        public byte Flags;
        public ObstacleArchetype Archetype;
        public ChunkKind Kind;

        public bool HasFlag(byte flag)
        {
            return (Flags & flag) != 0;
        }

        public override string ToString()
        {
            return Type + "@" + Tick + " id=" + EntityId + " value=" + Value + " lane=" + Lane + " flags=" + Flags;
        }
    }
}
