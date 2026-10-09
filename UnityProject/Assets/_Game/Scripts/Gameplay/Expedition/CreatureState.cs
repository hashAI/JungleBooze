namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Sailback behaviour states (spec 103 §7.2).</summary>
    public enum CreatureState : byte
    {
        Inactive = 0,
        Perched,
        Alert,
        Launch,
        Glide,
        Gone,
    }
}
