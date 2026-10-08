using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Worlds a chunk may appear in (spec 002 section 4.1). FP1 only has the Jungle, and its layouts are
    /// world-neutral (<see cref="All"/>). Bits for the other worlds are added when they get names [ASSUMED].
    /// </summary>
    [Flags]
    public enum WorldMask : byte
    {
        None = 0,
        Jungle = 1 << 0,
        All = 0xFF,
    }
}
