namespace JungleBooze.Services.Meta
{
    /// <summary>What a mission counts (GDD 13.2). Stored in the save: append only, never renumber.</summary>
    public enum MissionKind
    {
        Coins = 0,
        Distance = 1,
        Vines = 2,
        PerfectReleases = 3,
        Slides = 4,
        Shields = 5,
        NearMisses = 6,
    }
}
