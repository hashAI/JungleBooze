namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Stateless hash for scenery placement (spec 003 section 11.2): the same inputs always give the same bits, in any
    /// call order, so a cell rebuilt after streaming is identical and nothing the player does can change it.
    /// </summary>
    public static class SceneryHash
    {
        /// <summary>Mixes (run seed, stream id, cell index, band, slot, salt) into 64 bits.</summary>
        public static ulong Mix(ulong runSeed, ulong streamId, long cell, uint band, uint slot, uint salt)
        {
            unchecked
            {
                ulong h = runSeed ^ (streamId * 0x9E3779B97F4A7C15UL);
                h = Finalize(h + ((ulong)cell * 0xBF58476D1CE4E5B9UL) + 0x632BE59BD9B4E019UL);
                ulong tag = ((ulong)band << 40) | ((ulong)slot << 20) | (ulong)salt;
                return Finalize(h ^ (tag * 0x94D049BB133111EBUL));
            }
        }

        /// <summary>Uniform value in [0, 1) from the top 24 bits of a hash.</summary>
        public static float Unit(ulong hash)
        {
            return (float)(hash >> 40) * (1f / 16777216f);
        }

        /// <summary>Shorthand for <c>Unit(Mix(...))</c>.</summary>
        public static float Roll(ulong runSeed, ulong streamId, long cell, SceneryBand band, uint slot, uint salt)
        {
            return Unit(Mix(runSeed, streamId, cell, (uint)band, slot, salt));
        }

        private static ulong Finalize(ulong x)
        {
            unchecked
            {
                x ^= x >> 33;
                x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33;
                x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return x;
            }
        }
    }
}
