using System.Text;
using JungleBooze.Gameplay.Path;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Route
{
    /// <summary>
    /// Menu items for the generated route (spec 003 sections 14.2 and 16, task T2): switch the generated route on for
    /// the next Play, and validate many seeds against the limits of section 4.3 using the real track generator.
    /// </summary>
    public static class RouteMenu
    {
        private const string UseMenu = "JungleBooze/Route/Use generated route";
        private const string ValidateMenu = "JungleBooze/Route/Validate seeds";
        private const int SeedCount = 200;
        private const double LengthM = 5000.0;

        [MenuItem(UseMenu)]
        private static void ToggleUseGeneratedRoute()
        {
            bool on = IsGenerated();
            RouteModeSettings.Save(on ? RouteMode.Straight : RouteMode.Generated);
            Debug.Log("[JungleBooze] Generated route " + (on ? "OFF (straight route)" : "ON") + " for the next Play.");
        }

        [MenuItem(UseMenu, true)]
        private static bool ToggleUseGeneratedRouteValidate()
        {
            Menu.SetChecked(UseMenu, IsGenerated());
            return true;
        }

        private static bool IsGenerated()
        {
            return RouteModeSettings.LoadInitial(RouteTuning.LoadOrDefault()) == RouteMode.Generated;
        }

        [MenuItem(ValidateMenu)]
        private static void ValidateSeeds()
        {
            RouteTuning tuning = RouteTuning.LoadOrDefault();
            var validator = new RouteValidator(tuning);
            int failed = 0;
            int emergency = 0;
            double worstVisibility = 0.0;
            double minAhead = double.MaxValue;
            var details = new StringBuilder();

            try
            {
                for (int seed = 1; seed <= SeedCount; seed++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Validate routes", "seed " + seed + " of " + SeedCount, seed / (float)SeedCount))
                    {
                        break;
                    }

                    RouteTrackSimulator route = RouteTrackSimulator.Build((ulong)seed, LengthM, 5.0, tuning);
                    RouteReport report = validator.Validate(route.Samples, route.Worlds, route.Count, route.StartS);
                    emergency += report.EmergencySamples;
                    worstVisibility = System.Math.Max(worstVisibility, report.WorstVisibilityRatio);
                    minAhead = System.Math.Min(minAhead, route.MinCommittedAheadM);
                    if (!report.IsValid && failed++ < 5)
                    {
                        details.Append("seed ").Append(seed).Append(": ").Append(report).Append('\n');
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string summary = "[JungleBooze] Route validation: " + failed + " of " + SeedCount + " seeds failed, "
                + emergency + " emergency samples, worst visibility ratio " + worstVisibility.ToString("0.00")
                + " (limit 0.94), smallest committed track ahead " + minAhead.ToString("0.0") + " m (need 175).\n" + details;
            if (failed > 0)
            {
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }
    }
}
