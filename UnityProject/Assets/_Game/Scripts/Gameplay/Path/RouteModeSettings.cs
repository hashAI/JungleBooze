using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Which <see cref="RouteMode"/> a session starts in, and the F4 cycle. In the editor and development builds the
    /// choice is remembered in PlayerPrefs; release builds always use <see cref="RouteMode.Generated"/>.
    /// [ASSUMED] Generated is the default everywhere: the game is meant to wind (spec 003).
    /// </summary>
    public static class RouteModeSettings
    {
        /// <summary>PlayerPrefs key (editor and development builds only) holding the saved <see cref="RouteMode"/>.</summary>
        public const string PrefKey = "JungleBooze.RouteMode";

        /// <summary>The next mode in the F4 cycle: Generated, Debug curve, Straight, Generated.</summary>
        public static RouteMode Next(RouteMode mode)
        {
            switch (mode)
            {
                case RouteMode.Generated:
                    return RouteMode.DebugCurve;
                case RouteMode.DebugCurve:
                    return RouteMode.Straight;
                default:
                    return RouteMode.Generated;
            }
        }

        /// <summary>The mode a session starts in. Setup time only.</summary>
        public static RouteMode LoadInitial(RouteTuning tuning)
        {
            RouteMode fallback = tuning != null && tuning.DebugRoute ? RouteMode.DebugCurve : RouteMode.Generated;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            if (saved >= (int)RouteMode.Generated && saved <= (int)RouteMode.Straight)
            {
                return (RouteMode)saved;
            }
#endif
            return fallback;
        }

        /// <summary>Remembers <paramref name="mode"/> for the next session (editor and development builds only).</summary>
        public static void Save(RouteMode mode)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlayerPrefs.SetInt(PrefKey, (int)mode);
            PlayerPrefs.Save();
#endif
        }
    }
}
