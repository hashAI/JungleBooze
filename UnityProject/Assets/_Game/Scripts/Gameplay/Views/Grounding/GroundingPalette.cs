using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Colors of the gray-box obstacle rigs, all derived from <see cref="StylePalette"/>. Hazard red appears only as
    /// <see cref="Ochre"/> marks next to <see cref="Ink"/> (spec 005 G8).
    /// </summary>
    public static class GroundingPalette
    {
        public static readonly Color Bark = StylePalette.HazardWood;
        public static readonly Color BarkDark = Color.Lerp(StylePalette.HazardWood, StylePalette.Ink, 0.35f);
        public static readonly Color Soil = Color.Lerp(StylePalette.HazardWood, StylePalette.Ink, 0.55f);
        public static readonly Color Moss = Color.Lerp(StylePalette.JungleGreen, StylePalette.DeepCanopyTeal, 0.55f);
        public static readonly Color Leaf = Color.Lerp(StylePalette.JungleGreen, StylePalette.Ink, 0.25f);
        public static readonly Color Heartwood = Color.Lerp(StylePalette.CreamPath, StylePalette.HazardWood, 0.3f);
        public static readonly Color Stone = StylePalette.HazardStone;
        public static readonly Color StoneLight = Color.Lerp(StylePalette.HazardStone, StylePalette.Parchment, 0.25f);
        public static readonly Color StoneDark = Color.Lerp(StylePalette.HazardStone, StylePalette.Ink, 0.4f);
        public static readonly Color Sand = Color.Lerp(StylePalette.CreamPath, StylePalette.HazardWood, 0.25f);
        public static readonly Color ThornDark = Color.Lerp(StylePalette.HazardThorn, StylePalette.Ink, 0.3f);
        public static readonly Color Thorn = StylePalette.HazardThorn;
        public static readonly Color Ink = StylePalette.Ink;

        /// <summary>Red-ochre trail mark (always flanked by ink).</summary>
        public static readonly Color Ochre = StylePalette.HazardRed;

        /// <summary>Every color above, so a rig never makes a material during a run.</summary>
        public static readonly Color[] All =
        {
            Bark, BarkDark, Soil, Moss, Leaf, Heartwood, Stone, StoneLight, StoneDark, Sand, ThornDark, Thorn, Ink, Ochre,
        };
    }
}
