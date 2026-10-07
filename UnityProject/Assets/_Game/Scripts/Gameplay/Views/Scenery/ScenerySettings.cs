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
        public const int ModelCount = 9;

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

        // Hard caps (11.5: 60k triangles, 60 draw calls for everything in view).

        public int MaxTriangles = 60000;

        public int MaxActivePieces = 256;

        /// <summary>Size of the per-cell placement buffer.</summary>
        public int MaxPiecesPerCell = 24;

        /// <summary>Audit: at most 4 light shafts on screen (overdraw).</summary>
        public int MaxLightShaftsInView = 4;

        /// <summary>Pieces closer than this to HERO use the full mesh; farther ones the simple one (audit: near 40 m or less).</summary>
        public float LodNearM = 40f;

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

        // Bands and density (11.1, 11.3).

        public float NearMaxM = 9f;

        public float MidMinM = 9f;

        public float MidMaxM = 25f;

        /// <summary>Clearing: the mid ring moves out to at least this lateral distance (11.3).</summary>
        public float ClearingMidMinM = 14f;

        /// <summary>Chance per near slot (3 per cell per side, about one prop per 3.3 m at 100 percent density).</summary>
        public float NearChance = 0.95f;

        /// <summary>Chance of one big prop per cell per side (about one per 11.8 m at 100 percent density).</summary>
        public float MidChance = 0.85f;

        /// <summary>Chance per hanging-vine slot (2 per cell per side).</summary>
        public float VineChance = 0.35f;

        /// <summary>Chance of a light shaft in a cell that passes the gate below.</summary>
        public float ShaftChance = 0.85f;

        /// <summary>Only every n-th cell can hold a shaft (4 shafts or fewer in a 105 m window, 3 to 5 per 60 m asked).</summary>
        public int ShaftEveryCells = 3;

        /// <summary>Density factor per world index (Jungle, River, Mountains, Ruins). [ASSUMED]</summary>
        public float[] WorldDensity = { 1f, 0.85f, 0.55f, 0.7f };

        /// <summary>Horizontal reach of each model at scale 1 (m), by <see cref="SceneryModel"/>; includes buttress roots, canopy, lean. [ASSUMED] for the art.</summary>
        public float[] FootprintUnitM = { 2.2f, 2.2f, 3.6f, 0.9f, 0.7f, 0.85f, 1.0f, 0.3f, 0.5f };

        /// <summary>Triangles of the full mesh per model (nominal, for the budget tests; the run counts real meshes). [ASSUMED]</summary>
        public int[] NominalNearTriangles = { 1500, 1500, 2200, 500, 300, 150, 150, 420, 2 };

        /// <summary>Triangles of the far mesh per model (nominal).</summary>
        public int[] NominalFarTriangles = { 60, 60, 80, 30, 20, 24, 24, 12, 2 };

        /// <summary>Density factor of a world (1 for an unknown index).</summary>
        public float DensityOfWorld(int worldIndex)
        {
            return WorldDensity != null && worldIndex >= 0 && worldIndex < WorldDensity.Length ? WorldDensity[worldIndex] : 1f;
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
