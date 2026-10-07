namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Read-only description of one generated chunk (spec 003 section 4.1, AC-313):
    /// what <see cref="TrackSimulation.PeekChunk"/> returns. A plain value; <see cref="IsValid"/> is false when the
    /// chunk is not generated yet or has already been despawned.
    /// </summary>
    public struct ChunkPeek
    {
        public bool IsValid;

        /// <summary>Generation serial, starting at 1.</summary>
        public int Serial;

        public ChunkKind Kind;

        /// <summary>World z of the chunk start.</summary>
        public double StartZ;

        public float LengthM;

        public double EndZ => StartZ + LengthM;
    }
}
