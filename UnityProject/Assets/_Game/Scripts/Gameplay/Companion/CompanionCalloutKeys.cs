namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// The language-neutral call-out keys of GDD 15.1. One audio clip (and, while there is no audio, one display
    /// word) per key and language; a missing language falls back to English. Never allocates.
    /// </summary>
    public static class CompanionCalloutKeys
    {
        public const string Vine = "call.vine";
        public const string Danger = "call.danger";
        public const string Cheer1 = "call.cheer.1";
        public const string Cheer2 = "call.cheer.2";
        public const string Cheer3 = "call.cheer.3";

        /// <summary>Number of cheer variants (<see cref="CompanionCalloutId.Cheer1"/> onward).</summary>
        public const int CheerVariantCount = 3;

        public static string KeyFor(CompanionCalloutId id)
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

        public static bool IsCheer(CompanionCalloutId id)
        {
            return id >= CompanionCalloutId.Cheer1 && id <= CompanionCalloutId.Cheer3;
        }
    }
}
