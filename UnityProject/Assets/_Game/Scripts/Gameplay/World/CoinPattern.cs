using System;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A coin pattern (spec 103 §3.0), chunk-local. Heights are above the floor (or the walkable top) under each coin.
    /// Line(S0–S1, X, Step) · Weave(S0–S1, Amp, Period) around X · Arc(S0, X, Count) on the ideal jump arc ·
    /// Under(S0, Count) at y 0.3 under a High · Point(S0, X, Y).
    /// </summary>
    [Serializable]
    public struct CoinPattern
    {
        public CoinPatternKind Kind;
        public float S0;
        public float S1;
        public float X;
        public float Step;
        public float Amp;
        public float Period;
        public int Count;

        /// <summary>Height above the floor; 0 = the kind's default.</summary>
        public float Y;

        public static CoinPattern Line(float s0, float s1, float x, float step = 1.5f)
        {
            return new CoinPattern { Kind = CoinPatternKind.Line, S0 = s0, S1 = s1, X = x, Step = step };
        }

        public static CoinPattern Weave(float s0, float s1, float amp, float period, float x = 0f, float step = 2.5f)
        {
            return new CoinPattern { Kind = CoinPatternKind.Weave, S0 = s0, S1 = s1, X = x, Amp = amp, Period = period, Step = step };
        }

        public static CoinPattern Arc(float s, float x, int count)
        {
            return new CoinPattern { Kind = CoinPatternKind.Arc, S0 = s, S1 = s, X = x, Count = count };
        }

        public static CoinPattern Under(float s, int count, float x = 0f)
        {
            return new CoinPattern { Kind = CoinPatternKind.Under, S0 = s, S1 = s, X = x, Count = count };
        }

        public static CoinPattern Point(float s, float x, float y)
        {
            return new CoinPattern { Kind = CoinPatternKind.Point, S0 = s, S1 = s, X = x, Y = y, Count = 1 };
        }
    }
}
