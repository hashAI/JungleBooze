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
        /// Edge forgiveness (spec 001 section 9.5, applied per box): a box cannot hit HERO while HERO is moving
        /// away from it this tick (<paramref name="x"/> − <paramref name="xPrev"/> points away from the box center)
        /// and HERO's center is at least <paramref name="thresholdM"/> (0.6 × lane width) from the box center.
        /// Moving toward the box, or not moving sideways, never forgives.
        /// </summary>
        public static bool IsEdgeForgiven(double xPrev, double x, double boxCenterX, double thresholdM, double toleranceM)
        {
            double motion = x - xPrev;
            double offset = x - boxCenterX;
            bool movingAway = (motion > 0.0 && offset > 0.0) || (motion < 0.0 && offset < 0.0);
            if (!movingAway)
            {
                return false;
            }

            double distance = offset < 0.0 ? -offset : offset;
            return distance >= thresholdM - toleranceM;
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
