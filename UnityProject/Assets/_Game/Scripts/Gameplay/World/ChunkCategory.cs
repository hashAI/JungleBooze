namespace JungleBooze.Gameplay.World
{
    /// <summary>Chunk category (spec 102 §2, Blueprint 8.2). Append only (saved in analytics).</summary>
    public enum ChunkCategory : byte
    {
        Straight = 0,
        Branch,
        Challenge,
        Discovery,
        Transition,
        Event,
        Recovery,
    }
}
