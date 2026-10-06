namespace JungleBooze.Core
{
    /// <summary>
    /// Deterministic random number source for simulation code.
    /// Simulation code must never use UnityEngine.Random or System.Random; it receives an IRandom instead.
    /// The same seed always produces the same sequence on the same build and platform.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns the next uniformly distributed 32-bit value.</summary>
        uint NextUInt();

        /// <summary>Returns an integer in [minInclusive, maxExclusive). Unbiased.</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a float in [0, 1) with 24 bits of precision.</summary>
        float NextFloat();

        /// <summary>
        /// Returns a float between minInclusive and maxExclusive.
        /// Float rounding can make the result equal maxExclusive for some ranges.
        /// </summary>
        float NextFloat(float minInclusive, float maxExclusive);

        /// <summary>Returns true with the given probability (0 = never, 1 = always).</summary>
        bool Chance(float probability);

        /// <summary>
        /// Creates an independent child stream derived from this generator.
        /// Use one stream per subsystem (track generation, cosmetic effects, bots) so that
        /// adding randomness in one subsystem never shifts the sequence of another.
        /// Allocates: call at setup time only, never per frame.
        /// </summary>
        IRandom Fork(ulong streamId);
    }
}
