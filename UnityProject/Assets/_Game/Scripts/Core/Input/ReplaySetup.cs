using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// The per-run inputs that are not in the input stream (replay format 3, ADR 0006 amendment): which run it was
    /// (scripted first expedition or directed), the profile state the director and tracker read (owned abilities,
    /// pending showcase, skill S for the difficulty model, discovered journal ids) and the forced test speed. Plain
    /// values so Core does not depend on gameplay types; gameplay converts it to and from its own run setup.
    /// </summary>
    public sealed class ReplaySetup
    {
        /// <summary>Scripted Expedition 1 (the director replaces the seed with the script seed).</summary>
        public bool FirstExpedition;

        /// <summary>Owned ability flags (bit mask).</summary>
        public int Owned;

        /// <summary>Abilities whose Showcase chunk is still pending (bit mask).</summary>
        public int PendingShowcase;

        /// <summary>Skill S at run start (difficulty model input).</summary>
        public float Skill;

        /// <summary>Tests and tools: constant speed (0 = speed curve).</summary>
        public float ForcedSpeed;

        /// <summary>Journal ids the profile had discovered when the run started.</summary>
        public string[] Discovered = Array.Empty<string>();

        public ReplaySetup Clone()
        {
            var copy = (ReplaySetup)MemberwiseClone();
            copy.Discovered = Discovered != null ? (string[])Discovered.Clone() : Array.Empty<string>();
            return copy;
        }
    }
}
