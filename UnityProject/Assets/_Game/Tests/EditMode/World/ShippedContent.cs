using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;
using UnityEditor;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>Loads the shipped vertical-slice content (Assets/_Game/Config/World …) and builds sessions on it.</summary>
    internal static class ShippedContent
    {
        private static ExpeditionContent _cached;

        public static ExpeditionContentAsset Asset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ExpeditionContentAsset>(ExpeditionPaths.Content);
            Assert.IsNotNull(asset, "Missing " + ExpeditionPaths.Content + " (run JungleBooze > Expedition > Build Scene).");
            return asset;
        }

        /// <summary>A fresh copy of the content (tests may change tuning on it).</summary>
        public static ExpeditionContent Build()
        {
            return Asset().Build();
        }

        /// <summary>Shared read-only content (don't mutate).</summary>
        public static ExpeditionContent Shared => _cached ?? (_cached = Build());

        public static ExpeditionSession Session(ExpeditionContent content = null, int events = 1024)
        {
            return new ExpeditionSession(content ?? Shared, ShippedAssets.Config(), new FixedStepTimeSource(60, 5), new RunEventBuffer(events), true);
        }

        public static ExpeditionRunSetup FirstRun()
        {
            return new ExpeditionRunSetup { FirstExpedition = true, Skill = -0.3f };
        }

        public static ExpeditionRunSetup Directed(ulong seed, AbilityFlags owned = AbilityFlags.None, float skill = -0.3f, AbilityFlags showcase = AbilityFlags.None)
        {
            return new ExpeditionRunSetup { FirstExpedition = false, Seed = seed, Owned = owned, Skill = skill, PendingShowcase = showcase };
        }

        public static PerfectBot Bot(ExpeditionSession session, params RouteType[] routes)
        {
            return new PerfectBot(session.Simulation, false)
            {
                ForkPreference = new WorldRoutePreference(session.Path, routes.Length > 0 ? routes : new[] { RouteType.Safe }),
            };
        }
    }
}
