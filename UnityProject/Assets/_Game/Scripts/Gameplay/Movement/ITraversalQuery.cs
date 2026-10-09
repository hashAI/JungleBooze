namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Traversal data of a path (spec 103 §10.3): water volumes and currents, Deep Breath zones, vines and canopy
    /// sections. Implemented by <c>WorldPath</c>; a path without it (the feel course, unit-test courses) has no
    /// traversal. Implementations must not allocate.
    /// </summary>
    public interface ITraversalQuery
    {
        /// <summary>Water surface height at (s, x), or false outside water volumes.</summary>
        bool TryGetWater(float s, float x, out float surfaceY);

        /// <summary>Current at s: lateral (+ = right) and forward, m/s (0 outside currents).</summary>
        void GetCurrent(float s, float x, out float lateral, out float forward);

        /// <summary>The vine whose grab window [lip − before, lip + after] contains s, if any.</summary>
        bool TryFindVine(float s, float before, float after, out VinePoint vine);

        /// <summary>A live vine by id.</summary>
        bool TryGetVine(int id, out VinePoint vine);

        /// <summary>The Deep Breath zone containing (s, x), if any.</summary>
        bool TryFindDeepDive(float s, float x, out DeepDivePoint zone);

        /// <summary>Inside a canopy section (beam landing assist, airborne lateral envelope).</summary>
        bool IsCanopy(float s);
    }
}
