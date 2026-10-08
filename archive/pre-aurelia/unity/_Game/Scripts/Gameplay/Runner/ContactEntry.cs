namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// How HERO's box first came to overlap an obstacle box inside a tick: the axis that started overlapping last
    /// (spec 001 section 9.3). <see cref="CollisionRules"/> maps it to lethal or stumble.
    /// </summary>
    public enum ContactEntry : byte
    {
        None = 0,

        /// <summary>z axis: HERO ran into the front face. Lethal.</summary>
        Front = 1,

        /// <summary>y axis, HERO's top rose through the box's bottom (jumping into a high barrier). Lethal.</summary>
        FromBelow = 2,

        /// <summary>y axis, HERO's bottom came down through the box's top (onto a low barrier). Stumble.</summary>
        FromAbove = 3,

        /// <summary>x axis: a lane move (or a mover) pushed the boxes together sideways. Stumble.</summary>
        Side = 4,

        /// <summary>Two or more axes started overlapping at the same moment. Stumble (ties go to the player).</summary>
        Tie = 5,

        /// <summary>
        /// The boxes already overlapped at the start of the tick (only possible if a box appeared on top of HERO).
        /// Treated as a side stumble so it can never be an unreadable death. [ASSUMED]
        /// </summary>
        Inside = 6,
    }
}
