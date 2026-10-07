using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// A live obstacle in the simulation ring (spec 002 section 4.4). One instance per placement; it produces one
    /// box per occupied lane (movers: one box at <see cref="MoverX"/>). Gaps have no box.
    /// </summary>
    public struct ObstacleInstance
    {
        /// <summary>Per-run id starting at 1; the <c>ObstacleId</c> of runner events.</summary>
        public int Id;

        public ObstacleArchetype Archetype;

        /// <summary>Occupied lanes after mirroring. Mover: its start lane.</summary>
        public byte LaneMask;

        /// <summary>World z of the front (gap: near edge).</summary>
        public double Z;

        /// <summary>Box depth along z (gap: its length).</summary>
        public float DepthM;

        /// <summary>Gap only.</summary>
        public float GapLengthM;

        /// <summary>Mover: start lane. Others: lowest occupied lane.</summary>
        public byte FromLane;

        /// <summary>Mover: end lane. Others: highest occupied lane.</summary>
        public byte ToLane;

        public MoverPhase Phase;

        /// <summary>Mover: current box centre x after this tick's track update.</summary>
        public float MoverX;

        /// <summary>Mover: box centre x before this tick's track update (relative motion for collisions).</summary>
        public float MoverXPrev;

        /// <summary>Mover: ticks moved so far.</summary>
        public int MoverTicks;

        /// <summary>Serial of the chunk that spawned it.</summary>
        public int ChunkSerial;

        /// <summary>Back end along z (front + depth).</summary>
        public double BackZ => Z + DepthM;
    }
}
