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

        /// <summary>Floor rise above <c>stepUpHeight</c> met on the ground (minor; Pista clambers up).</summary>
        Wall,

        /// <summary>Water contact (spec 103 §4.6): −1 health, stumble, i-frames; never Crash or Fall.</summary>
        Bump,
    }
}
