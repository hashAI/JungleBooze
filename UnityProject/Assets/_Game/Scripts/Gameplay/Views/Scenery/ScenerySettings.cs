using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Tuning of the stateless scenery system (spec 003 section 11), plain C#, presentation only, never in the
    /// simulation hash. Field defaults are the [ASSUMED] first values from the spec tables.
    /// <para>Switch: <see cref="Enabled"/> (PlayerPrefs int <see cref="EnabledPrefKey"/>, default 1) turns the whole
    /// system off; <see cref="ReplaceGroundDressing"/> decides whether it also replaces the old jungle wall dressing
    /// in <see cref="GroundView"/>.</para>
    /// </summary>
    public sealed class ScenerySettings
    {
        /// <summary>PlayerPrefs key: 0 = old wall dressing only, anything else (or missing) = the new scenery.</summary>
        public const string EnabledPrefKey = "JungleBooze.Scenery";

        /// <summary>Number of <see cref="SceneryModel"/> values; every per-model table has this length.</summary>
        public const int ModelCount = 16;

        /// <summary>Number of worlds (Jungle, River, Mountains, Ruins).</summary>
        public const int WorldCount = 4;

        public bool Enabled = true;

        /// <summary>When the system is on, <see cref="GroundView"/> skips its own walls and foliage (they would double the verge).</summary>
        public bool ReplaceGroundDressing = true;

        // Cells and the view window (11.2, 11.5).

        public float CellLengthM = 10f;

        /// <summary>Scenery is kept this far behind HERO (m).</summary>
        public float WindowBehindM = 10f;

        /// <summary>Scenery is kept this far ahead of HERO (m); the fog ends at 90 m.</summary>
        public float WindowAheadM = 95f;

        // Hard caps. Dense corridor (owner direction 2026-10-07): raised from 60k triangles / 60 draws to about 90k
        // triangles and about 50 draw calls for the scenery. Draw calls are the number of (model part, LOD) pairs that
        // have at least one piece in view; the model list below keeps that at about 35 to 45, see TargetDrawCalls.

        public int MaxTriangles = 90000;

        /// <summary>Also the instance capacity of every part (RenderMeshInstanced takes 1023 matrices at most).</summary>
        public int MaxActivePieces = 1000;

        /// <summary>Size of the per-cell placement buffer (a dense cell holds about 35 pieces, the worst case about 70).</summary>
        public int MaxPiecesPerCell = 96;

        /// <summary>Target for the dev log: scenery draw calls.</summary>
        public int TargetDrawCalls = 50;

        /// <summary>Audit: few light shafts on screen (overdraw). Raised gently from 4 to 6; they are the only alpha-blended pieces.</summary>
        public int MaxLightShaftsInView = 6;

        /// <summary>Pieces closer than this to HERO use the full mesh; farther ones the simple one (audit: near 40 m or less).</summary>
        public float LodNearM = 40f;

        /// <summary>Big trees and trunks use the full mesh only within this distance (they are the heaviest meshes).</summary>
        public float LodNearTreeM = 26f;

        /// <summary>Small props (bush, fern, rock, root, vine) use the full mesh only within this distance.</summary>
        public float LodNearSmallM = 24f;

        /// <summary>0 = props lean with the bank and grade, 1 = always upright; in between blends (trees look planted, not tilted).</summary>
        public float UprightBlend = 0.75f;

        /// <summary>Props sit this far below the banked path plane, like the floor strips.</summary>
        public float GroundDropM = 0.02f;

        // Sight corridor (11.1): nothing of any prop may reach inside it.

        public float CorridorEdgeM = 5f;

        public float CorridorBendInnerM = 7f;

        /// <summary>Corridor on both sides during the 60 m of a swing zone (11.3).</summary>
        public float CorridorSwingM = 9f;

        /// <summary>Curvature (rad/m) at which a bend counts as a bend for the inner-side corridor (R of 250 m or less).</summary>
        public float BendCurvature = 1f / 250f;

        /// <summary>Extra clearance on top of the footprint (m).</summary>
        public float ClearanceMarginM = 0.3f;

        /// <summary>
        /// Framed vine section (swing zone, 50 m before and 40 m after it): the anchor tree stands 6 to 9 m beside the
        /// path with a trunk radius of up to 1.75 m, so nothing solid keeps closer than this (11 m) to the centerline.
        /// Dense growth starts right behind the tree and frames it; it cannot hide it from a camera on the path.
        /// </summary>
        public float SwingFrameInnerM = 11f;

        // Bands and density (11.1, 11.3).

        public float NearMaxM = 9f;

        public float MidMinM = 9f;

        public float MidMaxM = 25f;

        /// <summary>Clearing: the mid ring moves out to at least this lateral distance (11.3).</summary>
        public float ClearingMidMinM = 14f;

        /// <summary>Chance per near slot (4 per cell per side, about one prop per 2.7 m at 100 percent density).</summary>
        public float NearChance = 0.95f;

        /// <summary>Chance per mid slot (2 per cell per side, about one big prop per 6 m at 100 percent density).</summary>
        public float MidChance = 0.85f;

        /// <summary>Chance of a light shaft in a cell that passes the gate below.</summary>
        public float ShaftChance = 0.85f;

        /// <summary>Only every n-th cell can hold a shaft (6 shafts or fewer in a 105 m window).</summary>
        public int ShaftEveryCells = 2;

        // Dense-corridor rings.

        /// <summary>Wall ring: chance per trunk slot (3 per cell per side, a trunk every 4 m).</summary>
        public float WallChance = 0.85f;

        /// <summary>Wall ring: trunks stand between the corridor edge and this much farther out (m).</summary>
        public float WallDepthM = 4.5f;

        /// <summary>Wall ring: chance that a trunk carries a crown mass (big leaf clump wrapped round it, 3.5 to 8.5 m up).</summary>
        public float WallCrownChance = 0.85f;

        /// <summary>Wall ring: chance per low filler mass slot (2 per cell per side, ground-up undergrowth blocks between the trunks).</summary>
        public float WallFillerChance = 0.9f;

        /// <summary>Wall ring: chance that a trunk grows a branch stub over the corridor edge.</summary>
        public float StubChance = 0.55f;

        /// <summary>A stub tip (and its tuft) may reach this far inside the corridor edge (m), above 10 m only; 0 in swing frames and on the inner side of a bend.</summary>
        public float StubOverhangM = 1f;

        /// <summary>Stubs start at least this high so the vine hanging from the tip stays at 7 m or more.</summary>
        public float StubMinHeightM = 10.8f;

        /// <summary>Chance of a hanging vine at a stub tip.</summary>
        public float StubVineChance = 0.85f;

        /// <summary>No hanging vine tip is lower than this above the path (spec: nothing below 7 m inside the corridor).</summary>
        public float VineTipMinM = 7f;

        /// <summary>Ground cover: chance per slot (3 cover props and 3 litter decals per cell per side).</summary>
        public float CoverChance = 0.8f;

        public float LitterChance = 0.9f;

        /// <summary>Leaf litter decals lie outside this lateral distance (lane band 3.6 m plus the shoulder).</summary>
        public float LitterInnerM = 4.3f;

        public float LitterOuterM = 14f;

        /// <summary>Leaf litter lies this high above the path plane (prevents z-fighting with the ground).</summary>
        public float LitterHeightM = 0.07f;

        /// <summary>Far ring: silhouettes between these lateral distances (m).</summary>
        public float FarMinM = 26f;

        public float FarMaxM = 55f;

        public float FarChance = 0.8f;

        /// <summary>The far ring never thins below this world factor, so every world stays enclosed.</summary>
        public float FarDensityFloor = 0.7f;

        /// <summary>Canopy: chance per slot (2 slots per cell across the whole width).</summary>
        public float CanopyChance = 0.65f;

        /// <summary>Canopy masses start at least this high above the path: above the 8.64 m swing span and out of the camera's reach.</summary>
        public float CanopyMinHeightM = 12f;

        public float CanopyMaxHeightM = 17f;

        /// <summary>Chance of a vine per canopy vine slot (2 per canopy mass).</summary>
        public float CanopyVineChance = 0.7f;

        /// <summary>Per-stretch density wobble (stretch = 8 cells = 80 m, seeded per world): the factor runs from min to max.</summary>
        public float StretchDensityMin = 0.8f;

        public float StretchDensityMax = 1.15f;

        /// <summary>Density factor per world index (Jungle, River, Mountains, Ruins). [ASSUMED]</summary>
        public float[] WorldDensity = { 1f, 0.85f, 0.55f, 0.7f };

        /// <summary>Horizontal reach of each model at scale 1 (m), by <see cref="SceneryModel"/>; includes buttress roots, canopy, lean. [ASSUMED] for the art.</summary>
        public float[] FootprintUnitM = { 2.2f, 2.2f, 3.6f, 0.9f, 0.7f, 0.85f, 1.0f, 0.3f, 0.5f, 0.95f, 1.7f, 1.7f, 1.7f, 0.2f, 4f, 1f };

        /// <summary>Triangles of the full mesh per model (nominal, for the budget tests; the run counts real meshes). [ASSUMED]</summary>
        public int[] NominalNearTriangles = { 1500, 1500, 2200, 500, 300, 150, 150, 420, 2, 48, 84, 84, 84, 12, 36, 6 };

        /// <summary>Triangles of the far mesh per model (nominal).</summary>
        public int[] NominalFarTriangles = { 60, 60, 80, 30, 20, 24, 24, 12, 2, 10, 24, 24, 24, 8, 36, 6 };

        /// <summary>Density factor of a world (1 for an unknown index).</summary>
        public float DensityOfWorld(int worldIndex)
        {
            return WorldDensity != null && worldIndex >= 0 && worldIndex < WorldDensity.Length ? WorldDensity[worldIndex] : 1f;
        }

        /// <summary>Distance from HERO within which <paramref name="model"/> draws its full mesh.</summary>
        public float LodNearOf(SceneryModel model)
        {
            switch (model)
            {
                case SceneryModel.TreeA:
                case SceneryModel.TreeB:
                case SceneryModel.GiantTrunk:
                    return LodNearTreeM;
                case SceneryModel.Bush:
                case SceneryModel.Fern:
                case SceneryModel.Rock:
                case SceneryModel.Root:
                case SceneryModel.HangingVine:
                    return LodNearSmallM;
                default:
                    return LodNearM;
            }
        }

        public float FootprintOf(SceneryModel model, float scaleXz)
        {
            return FootprintUnitM[(int)model] * scaleXz;
        }

        /// <summary>Reads the switch from PlayerPrefs; everything else is the default. Setup time only.</summary>
        public static ScenerySettings LoadOrDefault()
        {
            var settings = new ScenerySettings();
            settings.Enabled = PlayerPrefs.GetInt(EnabledPrefKey, 1) != 0;
            return settings;
        }
    }
}
