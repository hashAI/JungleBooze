using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// Player intents for one simulation tick. Flags, because one tick can carry more than one
    /// command (for example a lane change and a jump buffered in the same step).
    /// The numeric values are stored in replays: never renumber existing members, only append.
    /// </summary>
    [Flags]
    public enum InputCommand : byte
    {
        None = 0,
        MoveLeft = 1 << 0,
        MoveRight = 1 << 1,
        Jump = 1 << 2,
        Slide = 1 << 3,

        /// <summary>Context action (tap), for example grabbing a vine. [ASSUMED] until the GDD fixes the control scheme.</summary>
        Action = 1 << 4,
    }
}
