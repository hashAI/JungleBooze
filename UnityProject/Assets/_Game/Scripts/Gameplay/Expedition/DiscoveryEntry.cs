using System;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// A journal entry (spec 103 §7.3, GDD §15). Names are working labels; lore names are the owner's call.
    /// </summary>
    [Serializable]
    public sealed class DiscoveryEntry
    {
        /// <summary>Stable id (saved): "D-01" …</summary>
        public string Id = "D-00";

        public string Name = "Unknown";
        public DiscoveryCategory Category = DiscoveryCategory.Location;

        /// <summary>common / uncommon / rare.</summary>
        public string Rarity = "common";

        public int RewardCoins = 50;
        public int RewardCrystals = 2;

        /// <summary>Second toast line ("Unknown Species · Added to Journal").</summary>
        public string ToastText = "Added to Journal";

        /// <summary>A secret (results objective priority 5 when missed).</summary>
        public bool Secret;

        /// <summary>Results hint when it exists in reached territory but wasn't found (priority 4/5).</summary>
        public string MissedHint = string.Empty;
    }
}
