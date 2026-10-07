namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// The beats a route is made of (spec 003 section 4.2). Values index the tables in <see cref="RouteTuning"/>:
    /// append only. <see cref="SwingZone"/> and <see cref="Gateway"/> are forced by the track's chunk kinds and are
    /// never drawn from the weight table.
    /// </summary>
    public enum RouteBeatKind : byte
    {
        Straight = 0,
        GentleBend = 1,
        Bend = 2,
        SBend = 3,
        Roll = 4,
        RiseFall = 5,
        Clearing = 6,
        Crossing = 7,
        Bridge = 8,

        /// <summary>Layer change up (T5): scheduled by the generator, never drawn from the weight table.</summary>
        Ascent = 9,

        /// <summary>Layer change down (T5): scheduled by the generator, never drawn from the weight table.</summary>
        Descent = 10,

        /// <summary>A vine chunk: straight, near level.</summary>
        SwingZone = 11,

        /// <summary>A world gateway chunk: straight and flat.</summary>
        Gateway = 12,
    }
}
