namespace JungleBooze.Gameplay.World
{
    /// <summary>Entry/exit seam type (spec 102 §2.1). MVP: Ground only; Water and Canopy are reserved for open seams.</summary>
    public enum SeamType : byte
    {
        Ground = 0,
        Water,
        Canopy,
    }
}
