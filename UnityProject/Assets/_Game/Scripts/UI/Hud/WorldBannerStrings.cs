using JungleBooze.Gameplay.Track;

namespace JungleBooze.UI.Hud
{
    /// <summary>English titles of the world banner (GDD 9). Localization swaps this table only.</summary>
    public static class WorldBannerStrings
    {
        public const string Jungle = "Jungle";
        public const string River = "River";
        public const string Mountains = "Mountains";
        public const string Ruins = "Ruins";
        public const string DuskJungle = "Dusk Jungle";

        /// <summary>Title for a world; <paramref name="dusk"/> is the dusk variant of the Jungle after the Ruins. Never allocates.</summary>
        public static string TitleFor(WorldKind kind, bool dusk)
        {
            if (dusk)
            {
                return DuskJungle;
            }

            switch (kind)
            {
                case WorldKind.River:
                    return River;
                case WorldKind.Mountains:
                    return Mountains;
                case WorldKind.Ruins:
                    return Ruins;
                default:
                    return Jungle;
            }
        }
    }
}
