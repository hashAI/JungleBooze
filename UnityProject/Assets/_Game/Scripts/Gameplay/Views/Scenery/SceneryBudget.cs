namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Running total of the pieces accepted for drawing (spec 003 section 11.5). The view feeds pieces nearest first,
    /// so the cap drops the farthest ones. Pure; the tests use the same type with nominal triangle counts.
    /// </summary>
    public struct SceneryBudget
    {
        public int Triangles;

        public int Pieces;

        public int Shafts;

        /// <summary>Pieces refused because a cap was reached.</summary>
        public int Dropped;

        /// <summary>Accepts the piece (and counts it) unless a cap would be exceeded.</summary>
        public bool TryTake(SceneryModel model, int triangles, ScenerySettings settings)
        {
            bool shaft = model == SceneryModel.LightShaft;
            if (Pieces >= settings.MaxActivePieces
                || Triangles + triangles > settings.MaxTriangles
                || (shaft && Shafts >= settings.MaxLightShaftsInView))
            {
                Dropped++;
                return false;
            }

            Triangles += triangles;
            Pieces++;
            if (shaft)
            {
                Shafts++;
            }

            return true;
        }
    }
}
