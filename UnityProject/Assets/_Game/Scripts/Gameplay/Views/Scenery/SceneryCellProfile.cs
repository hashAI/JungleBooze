using JungleBooze.Gameplay.Path;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Density modifiers of one cell (spec 003 section 11.3): per beat, per layer and per world. This is the
    /// layer hook: T5 refines the <see cref="PathLayer.High"/> case (canopy platform dressing) here, in one place.
    /// </summary>
    public struct SceneryCellProfile
    {
        public float NearDensity;

        public float MidDensity;

        /// <summary>Hanging vines.</summary>
        public float OverheadDensity;

        /// <summary>Smallest lateral distance of the mid ring (the nearest part of a prop).</summary>
        public float MidMinM;

        public float ShaftChance;

        /// <summary>Every cell may hold a shaft (Clearing) instead of every n-th.</summary>
        public bool ShaftEveryCell;

        /// <summary>High layer: near ring is leaf tufts (ferns) only.</summary>
        public bool NearTuftsOnly;

        /// <summary>High layer: mid ring is trunk columns only.</summary>
        public bool MidColumnsOnly;

        public static SceneryCellProfile For(RouteBeatKind beat, PathLayer layer, float worldDensity, ScenerySettings s)
        {
            float near = 1f;
            float mid = 1f;
            float overhead = 1f;
            float midMin = s.MidMinM;
            float shaft = s.ShaftChance;
            bool everyCell = false;

            switch (beat)
            {
                case RouteBeatKind.Clearing:
                    near = 0.4f;
                    mid = 0.6f;
                    overhead = 0f;
                    midMin = s.ClearingMidMinM;
                    shaft = 0.3f;
                    everyCell = true;
                    break;
                case RouteBeatKind.Crossing:
                    near = 0.7f;
                    mid = 0.7f;
                    break;
                case RouteBeatKind.Bridge:
                    near = 0.3f;
                    mid = 0.3f;
                    break;
                case RouteBeatKind.Ascent:
                case RouteBeatKind.Descent:
                    near = 0.7f;
                    mid = 0.7f;
                    break;
                case RouteBeatKind.Gateway:
                    // [ASSUMED] the world gateway frames stand here; keep the sides calm.
                    near = 0.5f;
                    mid = 0.5f;
                    overhead = 0.5f;
                    break;
                default:
                    break;
            }

            var profile = new SceneryCellProfile
            {
                NearDensity = near * worldDensity,
                MidDensity = mid * worldDensity,
                OverheadDensity = overhead * worldDensity,
                MidMinM = midMin,
                ShaftChance = shaft,
                ShaftEveryCell = everyCell,
            };

            if (layer == PathLayer.High)
            {
                // Spec 11.3 High layer: near ring leaf tufts and vines only; mid ring trunk columns every 14 to 20 m.
                profile.NearTuftsOnly = true;
                profile.MidColumnsOnly = true;
                profile.MidDensity *= 0.6f;
            }

            return profile;
        }
    }
}
