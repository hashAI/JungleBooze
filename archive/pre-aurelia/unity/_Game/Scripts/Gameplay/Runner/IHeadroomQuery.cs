namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Hook for spec 001 rule 6.4.5: when the slide timer runs out, HERO only stands up if the standing hitbox
    /// would not overlap an obstacle. Optional override: when <see cref="RunnerSimulation"/> is built without one
    /// (null), it checks the standing box against <see cref="ITrackQuery.GetBoxes"/> itself, which is what the real
    /// track uses. <see cref="OpenHeadroomQuery"/> always allows standing (flat preview world, tests).
    /// Must be deterministic and must not allocate.
    /// </summary>
    public interface IHeadroomQuery
    {
        /// <summary>
        /// True if a standing box (bottom at the track surface, height <paramref name="standingHeightM"/>) over the
        /// footprint [xMin, xMax] × [zMin, zMax] overlaps nothing.
        /// </summary>
        bool CanStand(float xMin, float xMax, double zMin, double zMax, float standingHeightM);
    }
}
