using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Worlds a chunk may appear in (spec 002 section 4.1). Bit n is <see cref="WorldKind"/> n, so
    /// <see cref="WorldScheduleConfig.MaskOf"/> maps a world to its bit. Normal chunks are world-neutral
    /// (<see cref="All"/>, GDD 8.3); signature hazard chunks are limited to the worlds that own the hazard.
    /// </summary>
    [Flags]
    public enum WorldMask : byte
    {
        None = 0,
        Jungle = 1 << 0,
        River = 1 << 1,
        Mountains = 1 << 2,
        Ruins = 1 << 3,
        All = 0xFF,
    }
}
