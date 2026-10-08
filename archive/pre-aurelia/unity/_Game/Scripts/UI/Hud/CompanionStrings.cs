using JungleBooze.Gameplay.Companion;

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// English display words for the companion's call-outs and the Lift meter. [ASSUMED] Until audio exists the
    /// call-outs show as a text bubble (the squawk-only fallback of GDD 15.1 is text for now); each word belongs to
    /// a language-neutral key in <see cref="CompanionCalloutKeys"/>, so localization swaps this table only.
    /// Cheer words are the GDD's [ASSUMED] "Shiny!" / "Wow!" plus a third variant.
    /// </summary>
    public static class CompanionStrings
    {
        public const string Vine = "Vine!";
        public const string Danger = "Look out!";
        public const string Cheer1 = "Shiny!";
        public const string Cheer2 = "Wow!";
        public const string Cheer3 = "Yeah!";

        public const string LiftLabel = "LIFT";
        public const string LiftReady = "LIFT!";
#if UNITY_IOS || UNITY_ANDROID
        public const string LiftReadyHint = "Double tap";
#else
        public const string LiftReadyHint = "Double-click / E";
#endif

        /// <summary>Display word for a call-out (key → word). Never allocates.</summary>
        public static string WordFor(CompanionCalloutId id)
        {
            switch (id)
            {
                case CompanionCalloutId.Vine:
                    return Vine;
                case CompanionCalloutId.Danger:
                    return Danger;
                case CompanionCalloutId.Cheer1:
                    return Cheer1;
                case CompanionCalloutId.Cheer2:
                    return Cheer2;
                case CompanionCalloutId.Cheer3:
                    return Cheer3;
                default:
                    return string.Empty;
            }
        }
    }
}
