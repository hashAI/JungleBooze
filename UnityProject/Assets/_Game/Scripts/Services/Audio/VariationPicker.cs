namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Picks clip variations and per-play jitter without allocating (xorshift32). Presentation only, never simulation:
    /// the sequence doesn't need to be replayable.
    /// </summary>
    public sealed class VariationPicker
    {
        private uint _state;

        public VariationPicker(uint seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Next01()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x >> 8) * (1f / 16777216f);
        }

        /// <summary>Uniform in [−1, 1).</summary>
        public float NextSigned()
        {
            return (Next01() * 2f) - 1f;
        }

        /// <summary>An index in [0, count) that differs from <paramref name="last"/> when count ≥ 2.</summary>
        public int Pick(int count, int last)
        {
            if (count <= 1)
            {
                return 0;
            }

            if (last < 0 || last >= count)
            {
                return (int)(Next01() * count) % count;
            }

            int i = (int)(Next01() * (count - 1)) % (count - 1);
            return i >= last ? i + 1 : i;
        }
    }
}
