namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Seed source that mixes an entropy value with a counter through SplitMix64 (spec 002 section 12.2: "the system
    /// clock mixed with a counter through a hash"). The App passes clock ticks as <c>entropy</c>; tests pass a
    /// constant, which makes the sequence deterministic. SplitMix64 is a bijection of the counter, so a source never
    /// repeats a seed within 2^64 calls; 0 is skipped (it means "no seed" elsewhere).
    /// </summary>
    public sealed class MixedRunSeedSource : IRunSeedSource
    {
        private const ulong Golden = 0x9E3779B97F4A7C15UL;

        private ulong _state;

        public MixedRunSeedSource(ulong entropy)
        {
            _state = entropy;
        }

        public ulong NextSeed()
        {
            while (true)
            {
                _state = unchecked(_state + Golden);
                ulong z = _state;
                z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
                z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
                z ^= z >> 31;
                if (z != 0UL)
                {
                    return z;
                }
            }
        }
    }
}
