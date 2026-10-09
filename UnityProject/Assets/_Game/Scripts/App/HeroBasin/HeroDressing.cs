using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Set-dressing pass of the painterly hero basin (design/aurelia/HERO_BASIN_DRESSING.md). Every item is placed by
    /// casting a ray from the landscape camera through a normalized screen point (u 0 = left, v 0 = top) with a fixed
    /// seed, so the shot repeats exactly. Sizes are given as a fraction of the frame width at the hit distance (the
    /// scene is ~5x the scale the map's metres assume), so a "0.05 u" clump covers 5% of the frame wherever it lands.
    /// Regions are (uMin, vMin, uMax, vMax). Only read when the style is Painterly and <see cref="Enabled"/> is on.
    /// </summary>
    [Serializable]
    public sealed class HeroDressing
    {
        public bool Enabled = true;
        public int Seed = 7331;

        [Header("1. Arch overgrowth")]
        [Tooltip("Foliage clumps on the upward-facing strand tops (map: ~40, 60/30/10 large/medium/small).")]
        public int ArchClumps = 40;
        [Tooltip("Clump width as a fraction of the frame width: large, medium, small.")]
        public Vector3 ArchClumpSizeU = new Vector3(0.07f, 0.045f, 0.028f);
        [Tooltip("Minimum spacing between clump centres on screen (fraction of the frame width).")]
        public float ArchClumpSpacingU = 0.03f;
        [Tooltip("Strand-top normal threshold (normal.y above this counts as a top).")]
        public float ArchTopNormalY = 0.5f;
        [Tooltip("Vine/moss curtains, clumped in 3s (map: ~25).")]
        public int ArchCurtains = 27;
        [Tooltip("Curtain length range as a fraction of the frame height.")]
        public Vector2 ArchCurtainLengthV = new Vector2(0.05f, 0.16f);
        [Tooltip("Screen band kept clear of curtains (the tall fall): uMin, uMax.")]
        public Vector2 FallClearU = new Vector2(0.5f, 0.64f);

        [Header("2. Rock islands in the basin")]
        public int Islands = 16;
        public Vector4 IslandRegion = new Vector4(0.36f, 0.6f, 0.95f, 0.95f);
        [Tooltip("Island width as a fraction of the frame width: large, medium, small.")]
        public Vector3 IslandSizeU = new Vector3(0.11f, 0.07f, 0.045f);
        [Tooltip("Share of islands with a shrub cap.")]
        public float IslandShrubShare = 0.75f;
        public int Spillways = 7;

        [Header("3. Pool rims (travertine lips and the cascade shelf)")]
        [Tooltip("Boulders per metre of lip (scaled pieces), and fern/shrub clumps per boulder.")]
        public float RimBouldersPerM = 0.6f;
        public float RimPlantsPerBoulder = 0.35f;
        public Vector2 RimBoulderSizeM = new Vector2(1.8f, 3.6f);
        [Tooltip("Boulders around the rim of the shelf the wide cascade pours from (hides the disc edge).")]
        public int ShelfBoulders = 60;

        [Header("4. Foreground ground (0% lawn)")]
        public int Cobbles = 26;
        [Tooltip("Radius around Pista's feet kept clear of undergrowth (m).")]
        public float PistaClearRadiusM = 1.1f;
        [Tooltip("Undergrowth coverage passes: screen regions filled with fern / broadleaf clumps where the ray hits ground.")]
        public Vector4 UndergrowthRegion = new Vector4(0f, 0.6f, 1f, 1f);
        public int UndergrowthClumps = 64;
        public Vector2 UndergrowthSizeU = new Vector2(0.08f, 0.17f);
        [Tooltip("Final sweep: fern clumps wherever bare terrain or a smooth mound still shows (max count).")]
        public int LawnSweepClumps = 90;

        [Header("5. Framing plants")]
        public Vector2 FrameLeftUv = new Vector2(0.05f, 0.92f);
        public float FrameLeftSizeU = 0.42f;
        public Vector2 FrameRightUv = new Vector2(0.94f, 0.95f);
        [Tooltip("0 = keep only the config's FrameRight clump (its bells already fill the orange budget).")]
        public float FrameRightSizeU = 0f;
        public Vector2 BellcapUv = new Vector2(0.2f, 0.88f);
        [Tooltip("0 = none: the left framing clump already carries bell stems (orange budget 1-2%).")]
        public float BellcapSizeU = 0f;
        [Tooltip("Backlit leaf clusters hanging from the top-left corner (map: 6-10), keeping the sun 40-60% visible.")]
        public int CornerLeaves = 9;

        [Header("6. Mid canopy puffs")]
        public int CanopyLeft = 30;
        public Vector4 CanopyLeftRegion = new Vector4(0.0f, 0.3f, 0.45f, 0.72f);
        public int CanopyRight = 15;
        public Vector4 CanopyRightRegion = new Vector4(0.7f, 0.42f, 1f, 0.75f);
        public Vector2 CanopySizeU = new Vector2(0.05f, 0.11f);

        [Header("7. Sunburst and god rays")]
        [Tooltip("Ray cards fanning from the sun: end points on screen (u, v), the longest reaching v 0.55.")]
        public Vector2[] GodRayEnds = { new Vector2(0.24f, 0.5f), new Vector2(0.31f, 0.47f), new Vector2(0.19f, 0.55f), new Vector2(0.37f, 0.4f) };
        public Vector2 GodRayWidthU = new Vector2(0.025f, 0.05f);
        public float GodRayDistanceM = 26f;

        [Header("9. Mist at the tall fall")]
        public int PlungeMistCards = 3;
    }
}
