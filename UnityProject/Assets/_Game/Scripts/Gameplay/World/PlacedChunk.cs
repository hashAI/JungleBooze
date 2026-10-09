namespace JungleBooze.Gameplay.World
{
    /// <summary>A chunk instance on the <see cref="WorldPath"/>: the variant, its pick and the id ranges of its items.</summary>
    public struct PlacedChunk
    {
        public ChunkRuntime Chunk;
        public ChunkPick Pick;
        public int Serial;
        public float StartS;
        public int ObstacleBase;
        public int CoinBase;
        public int CoinCount;
        public int ForkBase;
        public int CrystalBase;
        public int CrystalCount;
        public int PowerUpBase;
        public int PowerUpCount;
        public int DiscoveryBase;
        public int HelpBase;

        /// <summary>View-side pose of the entry seam (world x/z, heading in radians; spec 102 §2.1).</summary>
        public PathFrame Start;

        public float EndS => StartS + Chunk.Length;
    }
}
