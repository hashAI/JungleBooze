using System;
using UnityEngine;

namespace JungleBooze.App.HeroBasin
{
    /// <summary>
    /// Painterly v4 "cheap tricks" (ADR 0011): painted matte cards and impostors replace brute-force geometry where
    /// the camera cannot see parallax, and painted atlases replace procedural white foam and cascade slabs. Painterly
    /// style only. Screen values use the landscape (F4) camera: u 0 = left, v 0 = top.
    /// </summary>
    [Serializable]
    public sealed class HeroCards
    {
        [Header("Arch matte card (projection-matched to the F4 camera)")]
        [Tooltip("Replace the 3D hero arch (both instances and its vine curtains) with the painted arch card.")]
        public bool ArchCard = true;
        public string ArchTexture = "BD_F4f_ArchCard";
        [Tooltip("Card rectangle on the landscape frame: (u left, v top, u right, v bottom), v 0 = top. Keep the painting's "
            + "aspect (1536 x 987 px): at 2532 x 1170 that is width = height x 0.719. Default = F4_f's own arch size.")]
        public Vector4 ArchScreenRect = new Vector4(0.154f, -0.07f, 1.01f, 1.12f);
        [Tooltip("Depth of the card plane along the camera's forward axis (m): the 3D arch's own depth, so fog, mist "
            + "and the plunge sort as before. At 215 m a 5 m sideways camera move shifts it 1.3 degrees.")]
        public float ArchDistanceM = 215f;
        [Tooltip("Card UV (v up) where the tall fall leaves the crown, inside the window.")]
        public Vector2 ArchMouthUv = new Vector2(0.545f, 0.69f);
        [Tooltip("The fall pours this far behind the card plane (m), so the window frames it.")]
        public float ArchMouthSetbackM = 14f;
        [Tooltip("Screen rectangle of the arch window (u left, v top, u right, v bottom): 3D trees and canopy whose "
            + "tops would rise into it are left out, so the painted fall and sky stay framed (F4_f).")]
        public Vector4 ArchWindowRect = new Vector4(0.50f, 0.22f, 0.73f, 0.62f);
        public float ArchExposure = 1.25f;
        public float ArchFogShare = 0.08f;

        [Header("Far fall card (painted plunge and its cliff, seen through the arch window)")]
        [Tooltip("Replace the 3D tall plunge (5 layered columns, spray and plume cards) with the painted fall card.")]
        public bool FallCard = true;
        public string FallTexture = "BD_F4f_FarFalls";
        [Tooltip("Card rectangle on the landscape frame (u left, v top, u right, v bottom); keep the painting's aspect "
            + "(795 x 1453 px: width = height x 0.253 at 2532 x 1170).")]
        public Vector4 FallScreenRect = new Vector4(0.506f, 0.05f, 0.734f, 0.95f);
        public float FallDistanceM = 330f;
        [Tooltip("Water flow on the card: (cycles per s, UV slide per cycle).")]
        public Vector2 FallFlow = new Vector2(0.45f, 0.035f);
        public float FallFogShare = 0.0f;
        public float FallExposure = 1.15f;

        [Header("Distant pillar impostors (painted, camera-facing, at their true positions)")]
        public string PillarTexture = "BD_F4f_PillarCards";
        [Tooltip("UV rectangles of the pillars in the texture (u0, v0, u1, v1; v up).")]
        public Vector4[] PillarRects =
        {
            new Vector4(0.0358f, 0.0146f, 0.1921f, 0.8105f), new Vector4(0.2435f, 0.0127f, 0.4570f, 0.9248f),
            new Vector4(0.5059f, 0.0049f, 0.6979f, 0.9355f), new Vector4(0.7279f, 0.0049f, 0.9714f, 0.8584f),
        };
        [Tooltip("Impostors: (x, z, height m, rect index). The foot sits on the terrain, below the mist line.")]
        public Vector4[] PillarCards =
        {
            new Vector4(-122f, 335f, 120f, 0f), new Vector4(300f, 330f, 200f, 3f), new Vector4(-210f, 380f, 160f, 2f),
            new Vector4(-95f, 420f, 150f, 1f), new Vector4(185f, 215f, 150f, 2f),
        };
        public float PillarFogShare = 0.35f;

        [Header("Near pillars (stacked-slab meshes with painted crowns)")]
        [Tooltip("(x, z, yaw, height m) of RS_PillarA_P / RS_PillarB_P, alternating; a crown on each top.")]
        public Vector4[] NearPillars = { new Vector4(-92f, 175f, 20f, 70f) };
        [Tooltip("Crown height as a share of the pillar height (FP_CanopyCrown_P).")]
        public float NearPillarCrown = 0.3f;

        [Header("Painted foam (Water shader, painterly)")]
        public bool PaintedFoam = true;
        public string FoamTexture = "FX_Foam_P";
        [Tooltip("Metres per foam atlas cell (lace, streaks).")]
        public Vector2 FoamCellM = new Vector2(3.2f, 6f);
        [Tooltip("Foam opacity cap: no large opaque white areas.")]
        public float FoamMaxAlpha = 0.85f;

        [Header("Painted cascade sheets (short falls only)")]
        public bool PaintedCascades = true;
        public string CascadeTexture = "FX_Cascade_P";
        [Tooltip("UV rectangles of the painted cascade rows (u0, v0, u1, v1; v up).")]
        public Vector4[] CascadeRows =
        {
            new Vector4(0.0156f, 0.6758f, 0.9697f, 0.9264f), new Vector4(0.0215f, 0.3639f, 0.9814f, 0.6146f),
            new Vector4(0.0254f, 0.0449f, 0.9736f, 0.2891f),
        };
        [Tooltip("Falls up to this height (m) use a painted sheet; taller ones keep the layered procedural sheets.")]
        public float CascadeMaxHeightM = 14f;
        [Tooltip("Sheet width vs the fall width, and how far the painted spray reaches below the foot (share of height).")]
        public Vector2 CascadeOverscan = new Vector2(1.12f, 0.22f);

        [Header("Quality tiers (ADR 0011; tier selection: Scripts/App/Perf/QualityTiers)")]
        [Tooltip("Renderers (name prefix) drawn only on the High tier; Low switches them off at load. High is unchanged.")]
        public string[] LowTierDrops = { "W Shafts", "L5 Layer 04", "L5 Layer 07" };

        [Header("Transparent layer diet")]
        [Tooltip("Share of the ambient mist billows kept (basin haze, arch-feet billows).")]
        public float MistShare = 0.5f;
        [Tooltip("Spray / plume card counts on the tall fall (was 12 / 16).")]
        public Vector2Int TallFallSprayPlume = new Vector2Int(6, 8);
    }
}
