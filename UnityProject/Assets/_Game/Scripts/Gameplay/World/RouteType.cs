namespace JungleBooze.Gameplay.World
{
    /// <summary>Route of a chunk lane (spec 102 §3.1). Main = the single line of a chunk without a split.</summary>
    public enum RouteType : byte
    {
        Main = 0,
        Safe,
        Risky,
        Secret,
    }
}
