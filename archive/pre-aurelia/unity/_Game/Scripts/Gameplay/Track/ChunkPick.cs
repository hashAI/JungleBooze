namespace JungleBooze.Gameplay.Track
{
    /// <summary>Result of one <see cref="TrackGenerator.NextChunk"/> call.</summary>
    public readonly struct ChunkPick
    {
        public ChunkPick(int chunkIndex, bool mirrored, int tier, bool isSeamFallback)
        {
            ChunkIndex = chunkIndex;
            Mirrored = mirrored;
            Tier = tier;
            IsSeamFallback = isSeamFallback;
        }

        /// <summary>Library index.</summary>
        public int ChunkIndex { get; }

        public bool Mirrored { get; }

        /// <summary>1-based tier by the chunk's start z.</summary>
        public int Tier { get; }

        /// <summary>No attempt passed the seam table; this is the fallback breather.</summary>
        public bool IsSeamFallback { get; }
    }
}
