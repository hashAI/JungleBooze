namespace JungleBooze.Gameplay.World
{
    /// <summary>Why the World Director picked a chunk (debug, analytics, tests).</summary>
    public enum PickReason : byte
    {
        Script = 0,
        Start,
        Normal,
        Recovery,
        Mercy,
        Branch,
        Showcase,
        Fallback,
        Emergency,
    }
}
