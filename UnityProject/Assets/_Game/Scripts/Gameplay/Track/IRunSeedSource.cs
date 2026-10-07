namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Where "Run again" gets its new seed (spec 002 section 12.2). Not simulation code: the App may mix the system
    /// clock into it; tests use a fixed entropy value.
    /// </summary>
    public interface IRunSeedSource
    {
        /// <summary>A new non-zero run seed.</summary>
        ulong NextSeed();
    }
}
