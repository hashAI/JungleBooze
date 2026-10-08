namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// A live coin in the simulation ring (spec 002 section 4.4). Collected coins stay in the ring (flagged) until
    /// they despawn, so ids stay stable for views.
    /// </summary>
    public struct CoinInstance
    {
        /// <summary>Per-run id starting at 1 (counted separately from obstacles).</summary>
        public int Id;

        /// <summary>World position of the coin centre.</summary>
        public float X;

        public float Y;

        public double Z;

        /// <summary>Lane whose centre is nearest to <see cref="X"/>.</summary>
        public byte Lane;

        public bool Collected;

        /// <summary>HERO has passed the coin (collected or not); the streak rule has seen it.</summary>
        public bool Resolved;

        /// <summary>Serial of the chunk that spawned it.</summary>
        public int ChunkSerial;
    }
}
