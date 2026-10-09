namespace JungleBooze.Gameplay.World
{
    /// <summary>A placed discovery trigger: crossing <see cref="S"/> with x in [XMin, XMax] fires it.</summary>
    public readonly struct DiscoveryPoint
    {
        public DiscoveryPoint(int id, int chunkSerial, int localIndex, float s, float xMin, float xMax)
        {
            Id = id;
            ChunkSerial = chunkSerial;
            LocalIndex = localIndex;
            S = s;
            XMin = xMin;
            XMax = xMax;
        }

        public int Id { get; }

        public int ChunkSerial { get; }

        /// <summary>Index into the chunk variant's <see cref="ChunkVariant.Discoveries"/>.</summary>
        public int LocalIndex { get; }

        public float S { get; }

        public float XMin { get; }

        public float XMax { get; }
    }
}
