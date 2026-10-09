using System.Collections.Generic;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Everything the "EXPEDITION COMPLETE" screen shows (spec 103 §9.2): distance and best, coins with bonus lines,
    /// crystals, new discoveries with category counts, NEW RECORD, and one next objective. Built once at run end.
    /// </summary>
    public sealed class RunResults
    {
        public string Title = "EXPEDITION COMPLETE";
        public float Distance;
        public float Best;
        public bool FirstExpedition;
        public bool NewRecord;
        public int Coins;
        public int CleanLineCoins;
        public int PerfectCoins;
        public int DiscoveryCoins;
        public int TotalCoins;
        public int Crystals;
        public int WalletCoins;
        public int WalletCrystals;
        public List<string> NewDiscoveryNames = new List<string>();

        /// <summary>"Creatures 1/3 · Locations 2/4".</summary>
        public string CategoryCounts = string.Empty;

        public string DeathLabel = string.Empty;
        public NextObjective Objective = new NextObjective();
    }
}
