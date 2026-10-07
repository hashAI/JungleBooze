namespace JungleBooze.Gameplay.Track
{
    /// <summary>A live vine in the track's vine ring (GDD 7.2). Plain value; views read it, never write it.</summary>
    public struct VineInstance
    {
        /// <summary>Per-run id starting at 1 (own counter).</summary>
        public int Id;

        public byte Lane;

        public byte Row;

        /// <summary>World z of the grab point.</summary>
        public double Z;

        public bool OverChasm;

        /// <summary>Serial of the chunk that spawned it (the vine group).</summary>
        public int ChunkSerial;
    }
}
