using System;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// One coin pattern in a chunk (spec 002 section 4.1). Chunk-relative. Field use per type:
    /// <list type="bullet">
    /// <item><c>Line</c>: <see cref="Lane"/>, <see cref="ZStart"/>, <see cref="ZEnd"/>, <see cref="SpacingM"/>.</item>
    /// <item><c>Arc</c>: <see cref="Lane"/>, <see cref="ZStart"/> = arc centre.</item>
    /// <item><c>Trail</c>: <see cref="Lane"/> = from lane, <see cref="ToLane"/>, <see cref="ZStart"/>, <see cref="ZEnd"/>, <see cref="SpacingM"/>.</item>
    /// <item><c>Single</c>: <see cref="X"/>, <see cref="Y"/>, <see cref="ZStart"/>.</item>
    /// </list>
    /// <see cref="SpacingM"/> 0 means the config default (<see cref="CoinConfig.LineSpacingM"/>).
    /// </summary>
    [Serializable]
    public struct CoinPattern
    {
        public CoinPatternType Type;
        public int Lane;
        public int ToLane;
        public float ZStart;
        public float ZEnd;
        public float SpacingM;
        public float X;
        public float Y;

        public static CoinPattern Line(int lane, float zStart, float zEnd, float spacingM = 0f)
        {
            return new CoinPattern
            {
                Type = CoinPatternType.Line,
                Lane = lane,
                ToLane = lane,
                ZStart = zStart,
                ZEnd = zEnd,
                SpacingM = spacingM,
            };
        }

        public static CoinPattern Arc(int lane, float zCenter)
        {
            return new CoinPattern
            {
                Type = CoinPatternType.Arc,
                Lane = lane,
                ToLane = lane,
                ZStart = zCenter,
                ZEnd = zCenter,
            };
        }

        public static CoinPattern Trail(int fromLane, int toLane, float zStart, float zEnd, float spacingM = 0f)
        {
            return new CoinPattern
            {
                Type = CoinPatternType.Trail,
                Lane = fromLane,
                ToLane = toLane,
                ZStart = zStart,
                ZEnd = zEnd,
                SpacingM = spacingM,
            };
        }

        public static CoinPattern Single(float x, float y, float zc)
        {
            return new CoinPattern
            {
                Type = CoinPatternType.Single,
                X = x,
                Y = y,
                ZStart = zc,
                ZEnd = zc,
            };
        }

        /// <summary>Arc centre (same field as <see cref="ZStart"/>).</summary>
        public float ZCenter => ZStart;

        /// <summary>Spec 002 section 4.2: coin lanes l → 2 − l and x → −x.</summary>
        public CoinPattern Mirrored()
        {
            CoinPattern p = this;
            if (Type == CoinPatternType.Single)
            {
                p.X = -X;
            }
            else
            {
                p.Lane = LaneMasks.MirrorLane(Lane);
                p.ToLane = LaneMasks.MirrorLane(ToLane);
            }

            return p;
        }

        public override string ToString()
        {
            return Type + "(" + Lane + "→" + ToLane + ", " + ZStart + "→" + ZEnd + ")";
        }
    }
}
