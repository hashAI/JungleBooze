namespace JungleBooze.Gameplay.World
{
    /// <summary>Per-run inputs of the World Director (spec 102 §6.1).</summary>
    public struct DirectorRunSetup
    {
        /// <summary>Run seed (ignored when <see cref="Script"/> is set: the script has its own fixed seed).</summary>
        public ulong Seed;

        /// <summary>Expedition 1 script, or null for a directed run.</summary>
        public ExpeditionScript Script;

        /// <summary>Open with the 60 m <c>Short</c> start variant (run 2+).</summary>
        public bool ShortStart;

        public AbilityFlags Owned;

        /// <summary>Abilities unlocked since the last run whose Showcase chunk has not appeared yet.</summary>
        public AbilityFlags PendingShowcase;

        /// <summary>Skill estimate S at run start (spec 102 §6.4).</summary>
        public float Skill;
    }
}
