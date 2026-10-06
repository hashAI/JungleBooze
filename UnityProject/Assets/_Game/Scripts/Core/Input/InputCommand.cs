using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// Player intents for one simulation tick, matching the gestures in GDD section 5.1
    /// (swipe left/right/up/down, double tap). Context (ground, air, vine) is resolved by the simulation.
    /// Flags, because one tick can carry more than one
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

        /// <summary>Double tap: activate the companion assist (GDD section 5.1).</summary>
        CompanionAssist = 1 << 4,
    }
}
