using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// The five gray-box themes: the four worlds and the Jungle's dusk variant (GDD 9, style guide section 5), all from
    /// <see cref="StylePalette"/>. Built once; <see cref="Get"/> does not allocate.
    /// </summary>
    public static class WorldThemes
    {
        private const float DuskPathKeyMix = 0.35f;
        private const float DuskGroundShade = 0.45f;

        private static readonly WorldTheme[] Themes = Build();

        /// <summary>The theme of <paramref name="kind"/>; <paramref name="dusk"/> picks the Jungle's dusk variant.</summary>
        public static WorldTheme Get(WorldKind kind, bool dusk)
        {
            if (dusk && kind == WorldKind.Jungle)
            {
                return Themes[4];
            }

            return Themes[(int)kind];
        }

        private static WorldTheme[] Build()
        {
            var jungle = new WorldTheme
            {
                KeyLight = StylePalette.JungleKeyLight,
                KeyYawDeg = -25f,
                KeyPitchDeg = 25f,
                ShadowTint = StylePalette.JungleShadowTint,
                SkyHorizon = StylePalette.JungleSkyHorizon,
                Fog = StylePalette.JungleFog,
                Path = StylePalette.CreamPath,
                PathAlternate = StylePalette.PathAlternate,
                Verge = StylePalette.JungleGreen,
                ObstacleBody = StylePalette.HazardWood,
                MoverBody = StylePalette.HazardStone,
                Accent = StylePalette.DeepCanopyTeal,
            };

            var river = new WorldTheme
            {
                KeyLight = StylePalette.RiverKeyLight,
                KeyYawDeg = -15f,
                KeyPitchDeg = 30f,
                ShadowTint = StylePalette.RiverShadowTint,
                SkyHorizon = StylePalette.RiverSkyHorizon,
                Fog = StylePalette.RiverFog,
                Path = StylePalette.RiverPath,
                PathAlternate = StylePalette.RiverPathAlternate,
                Verge = StylePalette.RiverBank,
                ObstacleBody = StylePalette.HazardDriftwood,
                MoverBody = StylePalette.HazardRaft,
                Accent = StylePalette.PistaTealSash,
            };

            var mountains = new WorldTheme
            {
                KeyLight = StylePalette.MountainsKeyLight,
                KeyYawDeg = -30f,
                KeyPitchDeg = 15f,
                ShadowTint = StylePalette.MountainsShadowTint,
                SkyHorizon = StylePalette.MountainsSkyHorizon,
                Fog = StylePalette.MountainsFog,
                Path = StylePalette.MountainsPath,
                PathAlternate = StylePalette.MountainsPathAlternate,
                Verge = StylePalette.MountainsSlope,
                ObstacleBody = StylePalette.HazardIce,
                MoverBody = StylePalette.HazardSnowball,
                Accent = StylePalette.MountainsShadowTint,
            };

            var ruins = new WorldTheme
            {
                KeyLight = StylePalette.RuinsKeyLight,
                KeyYawDeg = 20f,
                KeyPitchDeg = 20f,
                ShadowTint = StylePalette.RuinsShadowTint,
                SkyHorizon = StylePalette.RuinsSkyHorizon,
                Fog = StylePalette.RuinsFog,
                Path = StylePalette.RuinsPath,
                PathAlternate = StylePalette.RuinsPathAlternate,
                Verge = StylePalette.RuinsScenery,
                ObstacleBody = StylePalette.HazardRuinStone,
                MoverBody = StylePalette.HazardStoneDisc,
                Accent = StylePalette.RuinsScenery,
            };

            var dusk = new WorldTheme
            {
                KeyLight = StylePalette.DuskKeyLight,
                KeyYawDeg = -25f,
                KeyPitchDeg = 10f,
                ShadowTint = StylePalette.DuskShadowTint,
                SkyHorizon = StylePalette.DuskSkyHorizon,
                Fog = StylePalette.DuskFog,
                Path = Color.Lerp(StylePalette.CreamPath, StylePalette.DuskKeyLight, DuskPathKeyMix),
                PathAlternate = Color.Lerp(StylePalette.PathAlternate, StylePalette.DuskKeyLight, DuskPathKeyMix),
                Verge = Color.Lerp(StylePalette.JungleGreen, StylePalette.DuskShadowTint, DuskGroundShade),
                ObstacleBody = Color.Lerp(StylePalette.HazardWood, StylePalette.DuskShadowTint, 0.25f),
                MoverBody = Color.Lerp(StylePalette.HazardStone, StylePalette.DuskShadowTint, 0.25f),
                Accent = StylePalette.DuskShadowTint,
            };

            return new[] { jungle, river, mountains, ruins, dusk };
        }
    }
}
