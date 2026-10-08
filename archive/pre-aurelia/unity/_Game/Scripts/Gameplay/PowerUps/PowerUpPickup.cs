namespace JungleBooze.Gameplay.PowerUps
{
    /// <summary>A floating power-up pickup on the track (GDD 10). Plain value; views read it, never write it.</summary>
    public struct PowerUpPickup
    {
        /// <summary>Per-run id starting at 1 (own counter).</summary>
        public int Id;

        public PowerUpType Type;

        public byte Lane;

        /// <summary>World position of the pickup centre.</summary>
        public float X;

        public float Y;

        public double Z;

        public bool Collected;

        /// <summary>HERO has passed it (collected or not).</summary>
        public bool Resolved;

        /// <summary>Serial of the chunk it was placed in.</summary>
        public int ChunkSerial;
    }
}
