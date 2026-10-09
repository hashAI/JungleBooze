namespace JungleBooze.Gameplay.World
{
    /// <summary>First-run slow-time help moves (GDD §16, spec 103 [ST:…] markers).</summary>
    public enum HelpMove : byte
    {
        Steer = 0,
        Jump,
        Slide,
        Dodge,
        Gap,
        Dive,
        Leap,
        Release,
    }
}
