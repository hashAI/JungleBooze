using System;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Per-run inputs from the player's profile (spec 102 §6.1, spec 103 §10).</summary>
    public struct ExpeditionRunSetup
    {
        /// <summary>Run the Expedition 1 script (the first-ever run).</summary>
        public bool FirstExpedition;

        /// <summary>Seed of a directed run (derive it from the install seed and the run index).</summary>
        public ulong Seed;

        public AbilityFlags Owned;
        public AbilityFlags PendingShowcase;
        public float Skill;

        /// <summary>Profile lookup: is this journal entry already discovered?</summary>
        public Func<string, bool> Discovered;

        /// <summary>Tests/tools: constant speed instead of the speed curve (0 = curve).</summary>
        public float ForcedSpeed;

        public static ulong RunSeed(long worldSeed, int runIndex)
        {
            // SplitMix64 of (seed, run): stable across builds and platforms.
            ulong z = unchecked((ulong)worldSeed + ((ulong)(uint)runIndex * 0x9E3779B97F4A7C15UL));
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }
    }
}
