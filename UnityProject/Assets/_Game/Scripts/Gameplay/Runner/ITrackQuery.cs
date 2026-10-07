namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// What the track tells movement (spec 001 section 6.5). Week 1 ships <see cref="FlatTrackQuery"/> and
    /// <see cref="TestTrackQuery"/>; the generated track implements the same interface later.
    /// Implementations must be deterministic and must not allocate.
    /// </summary>
    public interface ITrackQuery
    {
        /// <summary>
        /// True if any ground lies under the footprint rectangle [xMin, xMax] × [zMin, zMax]
        /// (HERO's footprint is X ± half hitbox width, Z ± half hitbox depth).
        /// </summary>
        bool HasGround(float xMin, float xMax, double zMin, double zMax);
    }
}
