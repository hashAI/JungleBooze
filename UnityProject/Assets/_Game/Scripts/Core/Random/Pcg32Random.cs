using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// PCG32 (XSH RR variant, 64-bit state, 32-bit output) by M. E. O'Neill, pcg-random.org.
    /// Small, fast, allocation-free and statistically strong. Output matches the reference
    /// C implementation (pcg32_srandom_r / pcg32_random_r) for the same seed and stream.
    /// </summary>
    public sealed class Pcg32Random : IRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        /// <summary>Default stream selector (the reference implementation's default sequence constant).</summary>
        public const ulong DefaultStream = 0xda3e39cb94b95bdbUL;

        private const float FloatUnit = 1.0f / 16777216.0f; // 2^-24

        private ulong _state;
        private readonly ulong _increment;

        public Pcg32Random(ulong seed)
            : this(seed, DefaultStream)
        {
        }

        public Pcg32Random(ulong seed, ulong stream)
        {
            Seed = seed;
            Stream = stream;
            _state = 0UL;
            _increment = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        /// <summary>The seed this generator was created with. Stored with replays.</summary>
        public ulong Seed { get; }

        /// <summary>The stream selector this generator was created with.</summary>
        public ulong Stream { get; }

        public uint NextUInt()
        {
            ulong oldState = _state;
            _state = unchecked((oldState * Multiplier) + _increment);
            uint xorShifted = unchecked((uint)(((oldState >> 18) ^ oldState) >> 27));
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            uint bound = unchecked((uint)(maxExclusive - minInclusive));

            // Rejection sampling to remove modulo bias (same approach as pcg32_boundedrand_r).
            uint threshold = (uint)((0x100000000UL - bound) % bound);
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return unchecked(minInclusive + (int)(value % bound));
                }
            }
        }

        public float NextFloat()
        {
            return (NextUInt() >> 8) * FloatUnit;
        }

        public float NextFloat(float minInclusive, float maxExclusive)
        {
            return minInclusive + ((maxExclusive - minInclusive) * NextFloat());
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public IRandom Fork(ulong streamId)
        {
            ulong childSeed = ((ulong)NextUInt() << 32) | NextUInt();
            return new Pcg32Random(childSeed, streamId);
        }
    }
}
