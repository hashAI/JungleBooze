namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Swept axis-aligned box test with relative motion (spec 001 section 9.1, spec 002 section 5.2).
    /// Over one tick, HERO's bounds and the obstacle's lateral bounds move linearly from their Prev values
    /// (t = 0) to their current values (t = 1). Overlap is strict: boxes that only touch do not overlap.
    /// The time of entry is the moment the last axis starts overlapping; that axis decides the
    /// <see cref="ContactEntry"/>. Pure maths, no state, no allocations.
    /// </summary>
    public static class SweptAabb
    {
        /// <summary>Entry times closer than this (in fractions of a tick) count as an exact tie.</summary>
        public const double TieEpsilon = 1e-7;

        /// <summary>
        /// True if the boxes overlap at some moment of the tick. <paramref name="time"/> is the time of entry in
        /// [0, 1); <paramref name="lateral"/> is true when the x axis is the entry axis or part of a tie (a side
        /// contact), false for front, top and bottom contacts.
        /// </summary>
        public static bool TrySweep(in HeroSweep hero, in ObstacleBox box, out double time, out ContactEntry entry, out bool lateral)
        {
            Axis(
                hero.XMinPrev,
                hero.XMaxPrev,
                hero.XMin,
                hero.XMax,
                box.XMinPrev,
                box.XMaxPrev,
                box.XMin,
                box.XMax,
                out double xLo,
                out double xHi,
                out bool _);
            Axis(
                hero.YMinPrev,
                hero.YMaxPrev,
                hero.YMin,
                hero.YMax,
                box.YMin,
                box.YMax,
                box.YMin,
                box.YMax,
                out double yLo,
                out double yHi,
                out bool yTopLeads);
            Axis(
                hero.ZMinPrev,
                hero.ZMaxPrev,
                hero.ZMin,
                hero.ZMax,
                box.ZMin,
                box.ZMax,
                box.ZMin,
                box.ZMax,
                out double zLo,
                out double zHi,
                out bool _);

            double enter = Max(xLo, Max(yLo, zLo));
            double exit = Min(xHi, Min(yHi, zHi));
            if (!(enter < exit) || !(enter < 1.0) || !(exit > 0.0))
            {
                time = 0.0;
                entry = ContactEntry.None;
                lateral = false;
                return false;
            }

            if (double.IsNegativeInfinity(enter))
            {
                time = 0.0;
                entry = ContactEntry.Inside;
                lateral = true;
                return true;
            }

            time = enter;
            bool xAt = IsAt(xLo, enter);
            bool yAt = IsAt(yLo, enter);
            bool zAt = IsAt(zLo, enter);
            int axes = (xAt ? 1 : 0) + (yAt ? 1 : 0) + (zAt ? 1 : 0);
            lateral = xAt;
            if (axes >= 2)
            {
                entry = ContactEntry.Tie;
            }
            else if (zAt)
            {
                entry = ContactEntry.Front;
            }
            else if (yAt)
            {
                entry = yTopLeads ? ContactEntry.FromBelow : ContactEntry.FromAbove;
            }
            else
            {
                entry = ContactEntry.Side;
            }

            return true;
        }

        /// <summary>
        /// Smallest distance between the two boxes in the x-y plane at the end of the tick (0 if they overlap
        /// on both axes). Used for near-miss (spec 001 section 9.7).
        /// </summary>
        public static double GapXY(in HeroSweep hero, in ObstacleBox box)
        {
            double dx = Max(0.0, Max(box.XMin - hero.XMax, hero.XMin - box.XMax));
            double dy = Max(0.0, Max(box.YMin - hero.YMax, hero.YMin - box.YMax));
            if (dx == 0.0)
            {
                return dy;
            }

            if (dy == 0.0)
            {
                return dx;
            }

            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Overlap interval (lo, hi) in tick time of one axis. lo = −∞ if the axis already overlaps at t = 0;
        /// an empty interval has lo = +∞. <paramref name="heroMaxLeads"/> is true when the binding condition is
        /// "HERO's max passes the obstacle's min" (for y: HERO's top rising into the box).
        /// </summary>
        private static void Axis(
            double hMin0,
            double hMax0,
            double hMin1,
            double hMax1,
            double oMin0,
            double oMax0,
            double oMin1,
            double oMax1,
            out double lo,
            out double hi,
            out bool heroMaxLeads)
        {
            // Condition 1: hero max > obstacle min. Condition 2: obstacle max > hero min.
            double p1 = hMax0 - oMin0;
            double q1 = (hMax1 - oMin1) - p1;
            double p2 = oMax0 - hMin0;
            double q2 = (oMax1 - hMin1) - p2;
            Solve(p1, q1, out double lo1, out double hi1);
            Solve(p2, q2, out double lo2, out double hi2);
            lo = Max(lo1, lo2);
            hi = Min(hi1, hi2);
            heroMaxLeads = lo1 >= lo2;
        }

        /// <summary>Interval of t where p + q·t &gt; 0, open, unbounded outside [0, 1].</summary>
        private static void Solve(double p, double q, out double lo, out double hi)
        {
            if (p > 0.0)
            {
                lo = double.NegativeInfinity;
                hi = q >= 0.0 ? double.PositiveInfinity : -p / q;
                return;
            }

            if (q > 0.0)
            {
                lo = -p / q;
                hi = double.PositiveInfinity;
                return;
            }

            lo = double.PositiveInfinity;
            hi = double.NegativeInfinity;
        }

        private static bool IsAt(double lo, double enter)
        {
            return !double.IsInfinity(lo) && lo >= enter - TieEpsilon;
        }

        private static double Max(double a, double b)
        {
            return a > b ? a : b;
        }

        private static double Min(double a, double b)
        {
            return a < b ? a : b;
        }
    }
}
