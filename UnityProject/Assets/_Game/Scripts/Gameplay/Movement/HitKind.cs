namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Contact results (spec 101 §4.1).</summary>
    public enum HitKind : byte
    {
        None = 0,
        Trip,
        HeadClip,
        SideClip,
        Thorns,
        Crash,
    }
}
