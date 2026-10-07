using JungleBooze.Gameplay.Path;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Density modifiers of one cell (spec 003 section 11.3): per beat, per layer and per world. This is the
    /// layer hook: T5 refines the <see cref="PathLayer.High"/> case (canopy platform dressing) here, in one place.
    /// Dense-corridor rings (owner direction): near, mid, wall, cover, far and canopy each have their own factor, so a
    /// clearing thins the near rings and the floor but keeps the far ring (still enclosed).
    /// </summary>
    public struct SceneryCellProfile
    {
        public float NearDensity;

        public float MidDensity;

        /// <summary>Wall ring: trunks, leaf masses, branch stubs and the vines hanging from the stubs.</summary>
        public float WallDensity;

        /// <summary>Ground cover: ferns, rocks, roots and leaf litter.</summary>
        public float CoverDensity;

        /// <summary>Far ring silhouettes (stays high in a clearing).</summary>
        public float FarDensity;

        /// <summary>Overhead leaf masses and the vines hanging from them.</summary>
        public float CanopyDensity;

        /// <summary>Smallest lateral distance of the mid ring (the nearest part of a prop).</summary>
        public float MidMinM;

        public float ShaftChance;

        /// <summary>Every cell may hold a shaft (Clearing) instead of every n-th.</summary>
        public bool ShaftEveryCell;

        /// <summary>High layer: near ring is leaf tufts (ferns) only.</summary>
        public bool NearTuftsOnly;

        /// <summary>High layer: mid ring is trunk columns only.</summary>
        public bool MidColumnsOnly;

        /// <summary>High layer: the forest floor is far below, so no ground cover and no litter.</summary>
        public bool NoGroundCover;

        /// <summary>Clearing: the canopy exists only at the sides (sky stays open over the lanes) and no canopy vines hang.</summary>
        public bool CanopyAtSidesOnly;

        public static SceneryCellProfile For(RouteBeatKind beat, PathLayer layer, float worldDensity, ScenerySettings s)
        {
            float near = 1f;
            float mid = 1f;
            float wall = 1f;
            float cover = 1f;
            float far = 1f;
            float canopy = 1f;
            float midMin = s.MidMinM;
            float shaft = s.ShaftChance;
            bool everyCell = false;
            bool sidesOnly = false;

            switch (beat)
            {
                case RouteBeatKind.Clearing:
                    // A breather: thin the near rings and the floor, open the sky over the lanes, keep the far ring and
                    // a half wall so the clearing is still enclosed.
                    near = 0.4f;
                    mid = 0.6f;
                    wall = 0.55f;
                    cover = 0.6f;
                    canopy = 0.4f;
                    midMin = s.ClearingMidMinM;
                    shaft = 0.3f;
                    everyCell = true;
                    sidesOnly = true;
                    break;
                case RouteBeatKind.Crossing:
                    near = 0.7f;
                    mid = 0.7f;
                    wall = 0.8f;
                    cover = 0.7f;
                    canopy = 0.8f;
                    break;
                case RouteBeatKind.Bridge:
                    near = 0.3f;
                    mid = 0.3f;
                    wall = 0.45f;
                    cover = 0.1f;
                    canopy = 0.3f;
                    break;
                case RouteBeatKind.Ascent:
                case RouteBeatKind.Descent:
                    near = 0.7f;
                    mid = 0.7f;
                    wall = 0.8f;
                    cover = 0.7f;
                    canopy = 0.8f;
                    break;
                case RouteBeatKind.Gateway:
                    // [ASSUMED] the world gateway frames stand here; keep the sides calm.
                    near = 0.5f;
                    mid = 0.5f;
                    wall = 0.6f;
                    cover = 0.5f;
                    canopy = 0.5f;
                    break;
                default:
                    break;
            }

            var profile = new SceneryCellProfile
            {
                NearDensity = near * worldDensity,
                MidDensity = mid * worldDensity,
                WallDensity = wall * worldDensity,
                CoverDensity = cover * worldDensity,
                FarDensity = far * Mathf.Max(worldDensity, s.FarDensityFloor),
                CanopyDensity = canopy * worldDensity,
                MidMinM = midMin,
                ShaftChance = shaft,
                ShaftEveryCell = everyCell,
                CanopyAtSidesOnly = sidesOnly,
            };

            if (layer == PathLayer.High)
            {
                // Spec 11.3 High layer: near ring leaf tufts and vines only; mid ring trunk columns every 14 to 20 m.
                profile.NearTuftsOnly = true;
                profile.MidColumnsOnly = true;
                profile.NoGroundCover = true;
                profile.MidDensity *= 0.6f;
                profile.WallDensity *= 0.8f;
            }

            return profile;
        }

        /// <summary>Per-stretch variation: scales the rings that make up the wall (not the far ring, not the shafts).</summary>
        public void ScaleDensities(float factor)
        {
            NearDensity *= factor;
            MidDensity *= factor;
            WallDensity *= factor;
            CoverDensity *= factor;
            CanopyDensity *= factor;
        }
    }
}
