namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Helpers for the 3-bit lane masks of obstacle placements (spec 002 section 4.1). Bit <c>l</c> set = lane
    /// <c>l</c> occupied; lane 0 is left. FP1 has exactly 3 lanes (validated by the runner config).
    /// </summary>
    public static class LaneMasks
    {
        public const int LaneCount = 3;

        public const byte None = 0;

        public const byte Lane0 = 1 << 0;

        public const byte Lane1 = 1 << 1;

        public const byte Lane2 = 1 << 2;

        public const byte All = Lane0 | Lane1 | Lane2;

        /// <summary>Mask with only <paramref name="lane"/> set.</summary>
        public static byte Of(int lane)
        {
            return (byte)(1 << lane);
        }

        /// <summary>Mask with every lane from <paramref name="a"/> to <paramref name="b"/> (either order) set.</summary>
        public static byte Range(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            int mask = 0;
            for (int l = lo; l <= hi; l++)
            {
                mask |= 1 << l;
            }

            return (byte)mask;
        }

        public static bool Contains(byte mask, int lane)
        {
            return lane >= 0 && lane < LaneCount && (mask & (1 << lane)) != 0;
        }

        public static int Count(byte mask)
        {
            int count = 0;
            for (int l = 0; l < LaneCount; l++)
            {
                if ((mask & (1 << l)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Lowest occupied lane, or -1 for an empty mask.</summary>
        public static int Lowest(byte mask)
        {
            for (int l = 0; l < LaneCount; l++)
            {
                if ((mask & (1 << l)) != 0)
                {
                    return l;
                }
            }

            return -1;
        }

        /// <summary>Highest occupied lane, or -1 for an empty mask.</summary>
        public static int Highest(byte mask)
        {
            for (int l = LaneCount - 1; l >= 0; l--)
            {
                if ((mask & (1 << l)) != 0)
                {
                    return l;
                }
            }

            return -1;
        }

        /// <summary>True for a non-empty mask with no hole, inside the 3 lanes.</summary>
        public static bool IsContiguous(byte mask)
        {
            if (mask == 0 || (mask & ~All) != 0)
            {
                return false;
            }

            int lo = Lowest(mask);
            int hi = Highest(mask);
            return Count(mask) == hi - lo + 1;
        }

        /// <summary>Mirror (spec 002 section 4.2): lane l becomes lane 2 − l.</summary>
        public static byte Mirror(byte mask)
        {
            int result = 0;
            for (int l = 0; l < LaneCount; l++)
            {
                if ((mask & (1 << l)) != 0)
                {
                    result |= 1 << MirrorLane(l);
                }
            }

            return (byte)result;
        }

        public static int MirrorLane(int lane)
        {
            return LaneCount - 1 - lane;
        }
    }
}
