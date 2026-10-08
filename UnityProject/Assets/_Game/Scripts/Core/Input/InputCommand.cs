using System;

namespace JungleBooze.Core
{
    /// <summary>
    /// Discrete player intents for one simulation tick (spec 101 §3.2, ADR 0006). Context (ground, air, slide) is
    /// resolved by the simulation. At most one of <see cref="Jump"/>, <see cref="Slide"/>, <see cref="DodgeLeft"/>,
    /// <see cref="DodgeRight"/> is set per tick; <see cref="TouchBegan"/> may accompany any of them.
    /// The numeric values are stored in replays (format version 2): never renumber, only append.
    /// </summary>
    [Flags]
    public enum InputCommand : byte
    {
        None = 0,

        /// <summary>Swipe up / W / Up / Space.</summary>
        Jump = 1 << 0,

        /// <summary>Swipe down / S / Down. In the air: fast-fall.</summary>
        Slide = 1 << 1,

        /// <summary>Quick flick left / Q: the 2.2 m dodge.</summary>
        DodgeLeft = 1 << 2,

        /// <summary>Quick flick right / E.</summary>
        DodgeRight = 1 << 3,

        /// <summary>
        /// A touch began this tick. The simulation stores the lateral target as the dodge origin (spec 101 §2.3), so a
        /// dodge never pulls back drag motion made by the same gesture. Keyboard dodges send it with the dodge.
        /// </summary>
        TouchBegan = 1 << 4,
    }
}
