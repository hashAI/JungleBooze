using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// 64-bit FNV-1a mixing helpers for state hashes and data hashes (same scheme as
    /// <see cref="Runner.RunnerSimulation.ComputeStateHash"/>). Stable across runs and platforms for the same
    /// values; never uses <c>GetHashCode</c>. Allocation-free.
    /// </summary>
    public static class StableHash
    {
        public const ulong Seed = 14695981039346656037UL;

        private const ulong Prime = 1099511628211UL;

        public static ulong Mix(ulong hash, long value)
        {
            ulong v = unchecked((ulong)value);
            for (int i = 0; i < 8; i++)
            {
                hash ^= v & 0xFFUL;
                hash = unchecked(hash * Prime);
                v >>= 8;
            }

            return hash;
        }

        public static ulong Mix(ulong hash, ulong value)
        {
            return Mix(hash, unchecked((long)value));
        }

        public static ulong Mix(ulong hash, int value)
        {
            return Mix(hash, (long)value);
        }

        public static ulong Mix(ulong hash, bool value)
        {
            return Mix(hash, value ? 1L : 0L);
        }

        public static ulong Mix(ulong hash, double value)
        {
            return Mix(hash, BitConverter.DoubleToInt64Bits(value));
        }

        public static ulong Mix(ulong hash, float value)
        {
            return Mix(hash, (double)value);
        }

        /// <summary>Mixes the UTF-16 code units of <paramref name="value"/> (null and empty differ).</summary>
        public static ulong Mix(ulong hash, string value)
        {
            if (value == null)
            {
                return Mix(hash, -1L);
            }

            hash = Mix(hash, (long)value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash = unchecked(hash * Prime);
            }

            return hash;
        }
    }
}
