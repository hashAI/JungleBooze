using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// What happened in one run (spec 103 §9): distance, coins and bonuses, crystals, discoveries, routes, hits and
    /// the death. Filled per tick by <see cref="RunTracker"/> with fixed-size storage (no allocation per tick).
    /// </summary>
    public sealed class RunStats
    {
        public const int MaxNewDiscoveries = 16;

        private readonly int[] _newDiscoveries = new int[MaxNewDiscoveries];

        public float Distance;
        public long Ticks;

        /// <summary>Coins picked up (the simulation's count).</summary>
        public int Coins;

        public int CleanLineCoins;
        public int PerfectCoins;
        public int DiscoveryCoins;
        public int Crystals;
        public int DiscoveryCrystals;
        public int Sightings;
        public int PowerUps;
        public int ShieldsAbsorbed;
        public int Hits;
        public int RiskyRoutes;
        public int SafeRoutes;
        public int SecretRoutes;
        public int CleanLines;
        public int ChunksEntered;
        public int TraversalAttempts;
        public int TraversalSuccesses;

        public bool Dead;
        public DeathCause Cause;
        public string DeathLabel = string.Empty;
        public string DeathChunk = string.Empty;

        /// <summary>Id of the chunk the runner is in (for the debug overlay and reports).</summary>
        public string CurrentChunk = string.Empty;

        public int NewDiscoveryCount { get; private set; }

        /// <summary>Discovery entries passed in reached territory but not found (bit per entry index &lt; 32).</summary>
        public int MissedDiscoveries;

        public int TotalCoins => Coins + CleanLineCoins + PerfectCoins + DiscoveryCoins;

        public int TotalCrystals => Crystals + DiscoveryCrystals;

        public int NewDiscovery(int index) => _newDiscoveries[index];

        public void AddNewDiscovery(int entryIndex)
        {
            if (NewDiscoveryCount < MaxNewDiscoveries)
            {
                _newDiscoveries[NewDiscoveryCount++] = entryIndex;
            }
        }

        public void Reset()
        {
            Distance = 0f;
            Ticks = 0;
            Coins = CleanLineCoins = PerfectCoins = DiscoveryCoins = 0;
            Crystals = DiscoveryCrystals = 0;
            Sightings = PowerUps = ShieldsAbsorbed = Hits = 0;
            RiskyRoutes = SafeRoutes = SecretRoutes = CleanLines = 0;
            ChunksEntered = 0;
            TraversalAttempts = TraversalSuccesses = 0;
            Dead = false;
            Cause = DeathCause.None;
            DeathLabel = string.Empty;
            DeathChunk = string.Empty;
            CurrentChunk = string.Empty;
            NewDiscoveryCount = 0;
            MissedDiscoveries = 0;
        }

        public void CountRoute(RouteType route)
        {
            switch (route)
            {
                case RouteType.Risky:
                    RiskyRoutes++;
                    break;
                case RouteType.Secret:
                    SecretRoutes++;
                    break;
                case RouteType.Safe:
                    SafeRoutes++;
                    break;
            }
        }
    }
}
