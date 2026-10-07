namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Hook for spec 001 rule 6.4.5: when the slide timer runs out, HERO only stands up if the standing hitbox
    /// would not overlap an obstacle. The collision stage implements this from the obstacle boxes; the default
    /// (<see cref="OpenHeadroomQuery"/>) always allows standing. Must be deterministic and must not allocate.
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
