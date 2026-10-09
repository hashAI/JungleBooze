using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// The seven abilities (GDD §13, order decided 2026-10-09: Deep Breath first). Bit values are saved: never
    /// renumber, only append.
    /// </summary>
    [Flags]
    public enum AbilityFlags
    {
        None = 0,
        DeepBreath = 1 << 0,
        VineGrip = 1 << 1,
        TrailSense = 1 << 2,
        RootVault = 1 << 3,
        CreatureTracking = 1 << 4,
        ShoulderCharge = 1 << 5,
        DoubleJump = 1 << 6,
    }
}
