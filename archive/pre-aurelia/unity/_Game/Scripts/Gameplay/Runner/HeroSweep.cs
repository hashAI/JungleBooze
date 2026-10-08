namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// HERO's box at the start ("Prev") and end of one tick, for the swept AABB test (spec 001 section 9.1).
    /// Every bound is linearly interpolated between the two over the tick. Value type.
    /// </summary>
    public struct HeroSweep
    {
        public double XMinPrev;
        public double XMaxPrev;
        public double YMinPrev;
        public double YMaxPrev;
        public double ZMinPrev;
        public double ZMaxPrev;

        public double XMin;
        public double XMax;
        public double YMin;
        public double YMax;
        public double ZMin;
        public double ZMax;

        /// <summary>Builds the sweep from HERO's center x, feet y, center z and hitbox height at both ends.</summary>
        public static HeroSweep FromCenters(
            double xPrev,
            double yPrev,
            double zPrev,
            double heightPrev,
            double x,
            double y,
            double z,
            double height,
            double halfWidth,
            double halfDepth)
        {
            return new HeroSweep
            {
                XMinPrev = xPrev - halfWidth,
                XMaxPrev = xPrev + halfWidth,
                YMinPrev = yPrev,
                YMaxPrev = yPrev + heightPrev,
                ZMinPrev = zPrev - halfDepth,
                ZMaxPrev = zPrev + halfDepth,
                XMin = x - halfWidth,
                XMax = x + halfWidth,
                YMin = y,
                YMax = y + height,
                ZMin = z - halfDepth,
                ZMax = z + halfDepth,
            };
        }
    }
}
