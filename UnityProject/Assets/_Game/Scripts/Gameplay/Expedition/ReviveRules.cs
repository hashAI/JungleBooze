using System;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Run;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// Revive "Continue?" (GDD §11, spec 103 §9.2): never in Expedition 1; from run 2 up to
    /// <see cref="ResultsConfig.MaxRevives"/> per run at 1, 2, 4 crystals, paid from this run's crystals and the
    /// wallet (banked at the results). The revive itself is the simulation's (a validated safe point with a clear
    /// run-in, 30 m cleared, 2 s i-frames that also catch a fall, swim/vine/canopy placements per spec 103 §16), after
    /// a 1.0 s ready beat. Plain C#.
    /// </summary>
    public static class ReviveRules
    {
        public static int Cost(ResultsConfig config, int index)
        {
            int[] costs = config.ReviveCosts;
            if (costs == null || costs.Length == 0)
            {
                return 1;
            }

            return costs[Math.Min(Math.Max(0, index), costs.Length - 1)];
        }

        /// <summary>Crystals available to spend now (wallet + this run − already spent).</summary>
        public static int Available(SaveData profile, RunStats stats)
        {
            return (profile != null ? profile.crystals : 0) + stats.TotalCrystals - stats.ReviveCrystals;
        }

        /// <summary>The offer may show (run 2+, under the limit, dead). Affordability is shown on the button.</summary>
        public static bool CanOffer(ResultsConfig config, bool firstExpedition, RunStats stats)
        {
            return !firstExpedition && stats.Revives < config.MaxRevives;
        }

        public static bool CanAfford(ResultsConfig config, SaveData profile, RunStats stats)
        {
            return Available(profile, stats) >= Cost(config, stats.Revives);
        }

        /// <summary>Pays and revives. False when not allowed, not affordable or not dead.</summary>
        public static bool TryRevive(ExpeditionSession session, ResultsConfig config, SaveData profile)
        {
            RunStats stats = session.Stats;
            if (!CanOffer(config, session.FirstExpedition, stats) || !CanAfford(config, profile, stats))
            {
                return false;
            }

            return session.Revive(Cost(config, stats.Revives));
        }
    }
}
