using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Foreground dressing of one camera frame (landscape F4 or portrait P1): hero framing plants, undergrowth kept to
    /// the frame sides, and a final ground sweep (0% lawn) that covers bare ground with ferns at the sides and mossy
    /// rock in the centre band, so the water past Pista's ledge stays in view (F4_f). Screen regions are
    /// (uMin, vMin, uMax, vMax), v 0 = top.
    /// </summary>
    [Serializable]
    public sealed class HeroDressingPass
    {
        public HeroFramePlant[] Frames = new HeroFramePlant[0];
        [Tooltip("Undergrowth: screen region, clump count and width range (fraction of the frame width).")]
        public Vector4 UndergrowthRegion = new Vector4(0f, 0.6f, 1f, 1f);
        public int UndergrowthClumps = 30;
        public Vector2 UndergrowthSizeU = new Vector2(0.06f, 0.16f);
        [Tooltip("Centre band (uMin, uMax) kept free of ferns: ground there is covered with low mossy rock instead.")]
        public Vector2 ClearBandU = new Vector2(0.3f, 0.82f);
        [Tooltip("Final ground sweep: max items, and the top of the swept band (v).")]
        public int SweepItems = 90;
        public float SweepTopV = 0.5f;
        [Tooltip("Sweep plant width range (fraction of the frame width).")]
        public Vector2 SweepSizeU = new Vector2(0.05f, 0.11f);
        [Tooltip("Items only where the ray lands within this distance (m): the portrait pass keeps to the near ground "
            + "so it never adds plants to the midground the landscape frame shares.")]
        public float MaxDistanceM = 5000f;

        public static HeroDressingPass LandscapeDefault()
        {
            return new HeroDressingPass
            {
                // F4_f: big broadleaf cluster lower left, bell-flower clump lower right, palm / fern accents.
                // Only the left clump and the bell clump carry bells (orange 1-2%).
                Frames = new[]
                {
                    new HeroFramePlant("FrameLeft_P", 0.05f, 0.92f, 0.42f, 0f),
                    new HeroFramePlant("PalmFern_P", 0.19f, 0.99f, 0.16f, 140f),
                    new HeroFramePlant("PalmFern_P", 0.0f, 0.62f, 0.16f, 60f),
                    new HeroFramePlant("Bellflower_P", 0.9f, 0.9f, 0.13f, -20f),
                    new HeroFramePlant("PalmFern_P", 0.98f, 0.99f, 0.22f, 30f),
                    new HeroFramePlant("PalmFern_P", 0.8f, 0.99f, 0.13f, 200f),
                    new HeroFramePlant("Fern_P", 0.26f, 0.99f, 0.09f, 90f),
                },
                UndergrowthRegion = new Vector4(0f, 0.58f, 1f, 1f),
                UndergrowthClumps = 30,
                UndergrowthSizeU = new Vector2(0.06f, 0.15f),
                ClearBandU = new Vector2(0.3f, 0.82f),
                SweepItems = 240,
                SweepTopV = 0.33f,
            };
        }

        public static HeroDressingPass PortraitDefault()
        {
            return new HeroDressingPass
            {
                Frames = new[]
                {
                    // No bell clump here: the portrait's lower right is the landscape's lower centre (orange 1-2%).
                    // The right half looks down past the ledge onto water and a rock island: lens-near plants stand on
                    // the ray (foot below the frame), behind the landscape camera's view.
                    new HeroFramePlant("FrameLeft_P", 0.02f, 0.97f, 0.45f, 20f),
                    new HeroFramePlant("Canopy_P", 0.12f, 1.0f, 0.5f, 100f, 2.0f),
                    new HeroFramePlant("PalmFern_P", 0.06f, 0.62f, 0.4f, 150f, 3.5f),
                    new HeroFramePlant("PalmFern_P", 0.16f, 0.86f, 0.4f, 280f, 2.4f),
                    new HeroFramePlant("Canopy_P", 0.78f, 0.74f, 0.4f, 40f, 4.5f),
                    new HeroFramePlant("PalmFern_P", 0.0f, 0.78f, 0.5f, 60f),
                    new HeroFramePlant("PalmFern_P", 0.97f, 0.93f, 0.95f, 30f, 2.2f),
                    new HeroFramePlant("Canopy_P", 0.9f, 0.78f, 0.45f, 80f, 3.2f),
                    new HeroFramePlant("Fern_P", 0.62f, 0.9f, 0.4f, 10f, 2.6f),
                    new HeroFramePlant("Fern_P", 0.8f, 0.86f, 0.5f, 250f, 3.6f),
                    new HeroFramePlant("PalmFern_P", 0.58f, 0.8f, 0.35f, 330f, 4.8f),
                    new HeroFramePlant("PalmFern_P", 0.72f, 1.0f, 0.6f, 200f, 2.0f),
                    new HeroFramePlant("PalmFern_P", 0.44f, 1.0f, 0.4f, 120f),
                    new HeroFramePlant("Fern_P", 0.38f, 0.93f, 0.5f, 45f, 2.6f),
                    new HeroFramePlant("Fern_P", 0.56f, 1.0f, 0.45f, 300f, 1.8f),
                },
                UndergrowthRegion = new Vector4(0f, 0.6f, 1f, 1f),
                UndergrowthClumps = 45,
                UndergrowthSizeU = new Vector2(0.2f, 0.4f),
                ClearBandU = new Vector2(0.33f, 0.47f),
                SweepItems = 120,
                SweepTopV = 0.6f,
                MaxDistanceM = 16f,
                SweepSizeU = new Vector2(0.18f, 0.32f),
            };
        }
    }
}
