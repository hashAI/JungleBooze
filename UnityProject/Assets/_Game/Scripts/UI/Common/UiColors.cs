using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// UI palette sampled from the painterly art direction (ART_DIRECTION_PAINTERLY §8): warm gold, emerald,
    /// turquoise and cream on deep emerald-teal panels. Text/background pairs meet WCAG AA (4.5:1) or better
    /// (checked in UiContrastTests).
    /// </summary>
    public static class UiColors
    {
        /// <summary>Cream text on dark panels (#FCF0CB).</summary>
        public static readonly Color Cream = Hex(0xFCF0CB);

        /// <summary>Secondary text on dark panels (#CFE3D6).</summary>
        public static readonly Color Mist = Hex(0xCFE3D6);

        /// <summary>Gold accent text/headers on dark panels (#F2CF7A).</summary>
        public static readonly Color Gold = Hex(0xF2CF7A);

        /// <summary>Bright turquoise accent on dark panels (#7FE0CC).</summary>
        public static readonly Color Turquoise = Hex(0x7FE0CC);

        /// <summary>Dark ink text on parchment and gold buttons (#2A1C08).</summary>
        public static readonly Color Ink = Hex(0x2A1C08);

        /// <summary>Muted ink on parchment (#5A4526).</summary>
        public static readonly Color InkSoft = Hex(0x5A4526);

        /// <summary>Emerald accent on parchment (#1E6B47).</summary>
        public static readonly Color Emerald = Hex(0x1E6B47);

        /// <summary>Deep panel colour (approximate mean of the panel sprite), used for contrast checks (#12362F).</summary>
        public static readonly Color PanelDeep = Hex(0x12362F);

        /// <summary>Parchment card mean (#F4E3B9).</summary>
        public static readonly Color Parchment = Hex(0xF4E3B9);

        /// <summary>Gold button mean (#EFBE5E).</summary>
        public static readonly Color ButtonGold = Hex(0xEFBE5E);

        /// <summary>Turquoise button mean (#1E6B61).</summary>
        public static readonly Color ButtonTeal = Hex(0x1E6B61);

        /// <summary>Text shadow for labels drawn over the 3D world.</summary>
        public static readonly Color TextShadow = new Color(0.02f, 0.07f, 0.06f, 0.85f);

        /// <summary>Scrim behind modal screens.</summary>
        public static readonly Color Scrim = new Color(0.02f, 0.06f, 0.05f, 0.62f);

        public static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        /// <summary>WCAG 2.x relative luminance.</summary>
        public static float Luminance(Color c)
        {
            return (0.2126f * Linear(c.r)) + (0.7152f * Linear(c.g)) + (0.0722f * Linear(c.b));
        }

        /// <summary>WCAG contrast ratio (1–21).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a);
            float lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float Linear(float c)
        {
            return c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
    }
}
