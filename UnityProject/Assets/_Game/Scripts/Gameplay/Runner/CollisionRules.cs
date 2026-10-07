namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Collision categories, spec 001 section 9.3 and spec 002 table 5.2. The table is the same for every archetype
    /// (cells marked "n/a" there cannot happen geometrically), so the category depends on the entry only:
    /// front and from-below are lethal; side, from-above, exact ties and "already inside" are stumbles.
    /// </summary>
    public static class CollisionRules
    {
        public static bool IsLethal(ContactEntry entry)
        {
            return entry == ContactEntry.Front || entry == ContactEntry.FromBelow;
        }

        /// <summary>
        /// Stumble flavour for <see cref="RunnerEventType.Stumbled"/>: side (with bounce) when the x axis is part
        /// of the entry, top (no bounce) otherwise.
        /// </summary>
        public static byte StumbleFlag(bool lateral)
        {
            return lateral ? RunnerEventFlags.Side : RunnerEventFlags.Top;
        }
    }
}
