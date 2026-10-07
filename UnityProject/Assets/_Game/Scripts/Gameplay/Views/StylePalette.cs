using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Colors from design/STYLE_GUIDE.md section 2 (sRGB hex; Unity converts to linear when set on a material).
    /// Gray-box and HUD code takes every color from here, never from inline literals.
    /// </summary>
    public static class StylePalette
    {
        // 2.2 Core style palette.
        public static readonly Color Ink = Hex(0x1E, 0x1A, 0x24);
        public static readonly Color PulpOrange = Hex(0xF2, 0x8C, 0x28);
        public static readonly Color SunGold = Hex(0xFF, 0xC4, 0x3D);
        public static readonly Color CreamPath = Hex(0xF3, 0xDF, 0xB2);
        public static readonly Color JungleGreen = Hex(0x3A, 0x8C, 0x3F);
        public static readonly Color DeepCanopyTeal = Hex(0x1B, 0x4D, 0x4A);
        public static readonly Color Parchment = Hex(0xF7, 0xE9, 0xC6);

        // 2.1 Signal colors (coins only).
        public static readonly Color CoinGold = Hex(0xFF, 0xD2, 0x3F);
        public static readonly Color CoinRim = Hex(0xC9, 0x8A, 0x12);
        public static readonly Color CoinGem = Hex(0x2E, 0xC4, 0xB6);

        // 2.1 Hazard red: hazards only (always paired with ink stripes or an ink edge).
        public static readonly Color HazardRed = Hex(0xD7, 0x26, 0x3D);

        /// <summary>Gray-box hazard bodies: darker and less saturated than the path (section 4.1).</summary>
        public static readonly Color HazardWood = Hex(0x5A, 0x46, 0x36);

        /// <summary>Gray-box mover (rolling boulder) body: dark stone.</summary>
        public static readonly Color HazardStone = Hex(0x6B, 0x5F, 0x52);

        /// <summary>Gray-box thorn patch body (lane denial): dark, desaturated thorny green-brown (section 4.1).</summary>
        public static readonly Color HazardThorn = Hex(0x3E, 0x4A, 0x2E);

        // 2.1 Power-up glows (cool colors only; power-ups and their effects only).
        public static readonly Color MagnetGlow = Hex(0x4F, 0xF0, 0xFF);
        public static readonly Color ShieldGlow = Hex(0x7C, 0xC6, 0xFF);
        public static readonly Color BoostGlow = Hex(0xFF, 0x3F, 0xA4);

        /// <summary>The shield bubble over a chasm: no glow (GDD 10 readability), sky blue dimmed toward ink.</summary>
        public static readonly Color ShieldDim = Color.Lerp(ShieldGlow, Ink, 0.55f);

        /// <summary>Ravine void on the ground: ink.</summary>
        public static readonly Color Void = Ink;

        // 2.3 Pista ("Mapcloth").
        public static readonly Color PistaMapCloth = Hex(0xEF, 0xE0, 0xBD);
        public static readonly Color PistaMapLines = Hex(0x8A, 0x5A, 0x2B);
        public static readonly Color PistaTealSash = Hex(0x17, 0x8F, 0x8A);
        public static readonly Color PistaSatchelStrap = Hex(0xF2, 0xA9, 0x00);
        public static readonly Color PistaSatchel = Hex(0x8C, 0x55, 0x30);
        public static readonly Color PistaSkin = Hex(0x6B, 0x40, 0x29);
        public static readonly Color PistaHair = Hex(0x2B, 0x1B, 0x14);

        // 2.4 Duko (macaw, design "Dusk").
        public static readonly Color MacawViolet = Hex(0x5B, 0x3A, 0x8C);
        public static readonly Color MacawOrange = Hex(0xF2, 0x8C, 0x28);
        public static readonly Color MacawUnderwing = Hex(0x8A, 0x63, 0xC2);
        public static readonly Color MacawTailTip = Hex(0x2E, 0xC4, 0xB6);
        public static readonly Color MacawBeak = Hex(0x3A, 0x35, 0x40);
        public static readonly Color MacawFace = Hex(0xF5, 0xEB, 0xDD);

        /// <summary>Lift meter and Lift effects (2.2 "Lift violet", ties to the macaw).</summary>
        public static readonly Color LiftViolet = Hex(0x8A, 0x63, 0xC2);

        // Section 5, Jungle world column.
        public static readonly Color JungleKeyLight = Hex(0xFF, 0xD2, 0x7A);
        public static readonly Color JungleShadowTint = Hex(0x2E, 0x5B, 0x57);
        public static readonly Color JungleFog = Hex(0xE9, 0xC9, 0x8A);
        public static readonly Color JungleSkyHorizon = Hex(0xFF, 0xE3, 0xA3);

        // Section 5, other worlds (key light, shadow band tint, sky horizon, fog, path). Rim and sky-top values are
        // not used by the gray-box (solid-color camera) and come with the real sky.
        public static readonly Color RiverKeyLight = Hex(0xFF, 0xE6, 0xB0);
        public static readonly Color RiverShadowTint = Hex(0x1F, 0x5A, 0x6B);
        public static readonly Color RiverFog = Hex(0xCF, 0xE6, 0xDF);
        public static readonly Color RiverSkyHorizon = Hex(0xFF, 0xE8, 0xB8);
        public static readonly Color RiverPath = Hex(0xE9, 0xD3, 0xA6);
        public static readonly Color RiverPathAlternate = Hex(0xD9, 0xBF, 0x8E);
        public static readonly Color RiverBank = Hex(0x2F, 0x8F, 0x9D);

        public static readonly Color MountainsKeyLight = Hex(0xFF, 0xF1, 0xDC);
        public static readonly Color MountainsShadowTint = Hex(0x6B, 0x6A, 0x9A);
        public static readonly Color MountainsFog = Hex(0xE8, 0xDC, 0xEB);
        public static readonly Color MountainsSkyHorizon = Hex(0xFF, 0xE6, 0xC7);
        public static readonly Color MountainsPath = Hex(0xF4, 0xF1, 0xF8);
        public static readonly Color MountainsPathAlternate = Hex(0xB9, 0xB6, 0xD3);
        public static readonly Color MountainsSlope = Hex(0xD9, 0xD4, 0xE8);

        public static readonly Color RuinsKeyLight = Hex(0xFF, 0xB0, 0x66);
        public static readonly Color RuinsShadowTint = Hex(0x5A, 0x2E, 0x3A);
        public static readonly Color RuinsFog = Hex(0xE7, 0xB5, 0x8A);
        public static readonly Color RuinsSkyHorizon = Hex(0xFF, 0xD0, 0x8A);
        public static readonly Color RuinsPath = Hex(0xEB, 0xCB, 0x9E);
        public static readonly Color RuinsPathAlternate = Hex(0xD9, 0xB7, 0x85);

        /// <summary>Scenery gold of the Ruins (dull; only coins are bright gold, section 5).</summary>
        public static readonly Color RuinsScenery = Hex(0xB8, 0x89, 0x2E);

        public static readonly Color DuskKeyLight = Hex(0xFF, 0x9A, 0x5A);
        public static readonly Color DuskShadowTint = Hex(0x2B, 0x3A, 0x67);
        public static readonly Color DuskFog = Hex(0xB0, 0x60, 0x7A);
        public static readonly Color DuskSkyHorizon = Hex(0xFF, 0x8C, 0x42);

        /// <summary>Gray-box hazard bodies per world (darker and less saturated than the path, section 4.1).</summary>
        public static readonly Color HazardDriftwood = Hex(0x4E, 0x5A, 0x55);
        public static readonly Color HazardRaft = Hex(0x4A, 0x6A, 0x70);
        public static readonly Color HazardIce = Hex(0x5C, 0x64, 0x78);
        public static readonly Color HazardSnowball = Hex(0x7C, 0x86, 0xA0);
        public static readonly Color HazardRuinStone = Hex(0x5E, 0x4B, 0x42);
        public static readonly Color HazardStoneDisc = Hex(0x6E, 0x5A, 0x4A);

        /// <summary>Vine rope and canopy branch (gray-box; GDD 7.2). Darker than the jungle green so it reads on the sky.</summary>
        public static readonly Color VineRope = Hex(0x4E, 0x6B, 0x2A);

        /// <summary>Vine grab point glow core: near-white (GDD 7.2 "white core with a sun-gold pulse").</summary>
        public static readonly Color VineGlowCore = Hex(0xFF, 0xFB, 0xEE);

        /// <summary>Second path tone so ground tiles visibly scroll (sun-bleached plank, section 5).</summary>
        public static readonly Color PathAlternate = Hex(0xE9, 0xD3, 0xA6);

        /// <summary>Blob shadow: ink at 35% over the cream path, baked into an opaque color (section 3).</summary>
        public static readonly Color BlobShadow = Color.Lerp(CreamPath, Ink, 0.35f);

        /// <summary>Lane marker dashes: ink at 25% over the cream path.</summary>
        public static readonly Color LaneMarker = Color.Lerp(CreamPath, Ink, 0.25f);

        /// <summary>Overlay behind HUD panels: ink at 55% opacity.</summary>
        public static readonly Color Dim = new Color(Ink.r, Ink.g, Ink.b, 0.55f);

        private static Color Hex(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }
    }
}
