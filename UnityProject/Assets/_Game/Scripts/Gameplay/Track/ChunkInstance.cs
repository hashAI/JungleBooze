namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// A generated chunk in the simulation ring (spec 002 section 4.4). Plain value; views read it, never write it.
    /// </summary>
    public struct ChunkInstance
    {
        /// <summary>Per-run counter starting at 1 (generation order).</summary>
        public int Serial;

        /// <summary>Index in the <see cref="ChunkLibrary"/>.</summary>
        public int ChunkIndex;

        public bool Mirrored;

        public ChunkKind Kind;

        /// <summary>World z of the chunk start.</summary>
        public double StartZ;

        public float LengthM;

        /// <summary>1-based tier the chunk was picked for.</summary>
        public byte Tier;

        /// <summary>True when this chunk is the seam fallback breather (section 8.3).</summary>
        public bool IsSeamFallback;

        /// <summary>Id of the chunk's first obstacle (0 when it has none).</summary>
        public int FirstObstacleId;

        public int ObstacleCount;

        /// <summary>Id of the chunk's first coin (0 when it has none).</summary>
        public int FirstCoinId;

        public int CoinCount;

        public double EndZ => StartZ + LengthM;
    }
}
