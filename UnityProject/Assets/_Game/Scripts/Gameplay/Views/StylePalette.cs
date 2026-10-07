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
