namespace JungleBooze.Gameplay.World
{
    /// <summary>Reward profile (spec 102 §7): multiplies coin density and crystal chance.</summary>
    public enum RewardProfile : byte
    {
        Low = 0,
        Standard,
        Rich,
    }
}
