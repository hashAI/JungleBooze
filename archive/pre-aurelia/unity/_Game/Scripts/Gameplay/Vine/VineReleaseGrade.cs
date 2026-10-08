namespace JungleBooze.Gameplay.Vine
{
    /// <summary>
    /// Result of letting go of a vine (GDD 7.3 step 4). Stored in <c>RunnerEvent.Value</c> of
    /// <c>VineReleased</c>: append only, never renumber.
    /// </summary>
    public enum VineReleaseGrade : byte
    {
        /// <summary>No release (or a swipe in the "too early" band, which is buffered instead).</summary>
        None = 0,

        /// <summary>No swipe by phase 1.00: short, safe launch.</summary>
        Auto = 1,

        /// <summary>Phase 0.45–0.66 or 0.80–1.00.</summary>
        Good = 2,

        /// <summary>Phase 0.66–0.80.</summary>
        Perfect = 3,
    }
}
