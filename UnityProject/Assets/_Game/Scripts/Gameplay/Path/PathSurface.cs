namespace JungleBooze.Gameplay.Path
{
    /// <summary>Ground kind under the path (spec 003 section 5.2). Cosmetic: gameplay is the same on all of them.</summary>
    public enum PathSurface : byte
    {
        Trail = 0,
        Bough = 1,
        Ledge = 2,
        Planks = 3,
        Ford = 4,
        Mud = 5,
    }
}
