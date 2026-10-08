using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Immutable runtime <c>CoinTuning</c> (spec 002 section 3.4).</summary>
    public sealed class CoinConfig
    {
        private CoinConfig(CoinDesignValues v)
        {
            CoinValue = v.CoinValue;
            PickupRadiusM = v.PickupRadiusM;
            CoinHeightM = v.CoinHeightM;
            CoinVisualRadiusM = v.CoinVisualRadiusM;
            CoinClearanceM = v.CoinClearanceM;
            LineSpacingM = v.LineSpacingM;
            ArcCoinCount = v.ArcCoinCount;
            ArcSpanFraction = v.ArcSpanFraction;
            TrailMinLengthM = v.TrailMinLengthM;
            CoinBlockClearS = v.CoinBlockClearS;
            StreakLength = v.StreakLength;
        }

        public int CoinValue { get; }

        public float PickupRadiusM { get; }

        public float CoinHeightM { get; }

        public float CoinVisualRadiusM { get; }

        public float CoinClearanceM { get; }

        public float LineSpacingM { get; }

        public int ArcCoinCount { get; }

        public float ArcSpanFraction { get; }

        public float TrailMinLengthM { get; }

        public float CoinBlockClearS { get; }

        public int StreakLength { get; }

        public static CoinConfig FromDesignValues(CoinDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid coin tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new CoinConfig(values);
        }

        public static CoinConfig CreateDefault()
        {
            return FromDesignValues(CoinDesignValues.CreateDefault());
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, CoinValue);
            h = StableHash.Mix(h, PickupRadiusM);
            h = StableHash.Mix(h, CoinHeightM);
            h = StableHash.Mix(h, CoinVisualRadiusM);
            h = StableHash.Mix(h, CoinClearanceM);
            h = StableHash.Mix(h, LineSpacingM);
            h = StableHash.Mix(h, ArcCoinCount);
            h = StableHash.Mix(h, ArcSpanFraction);
            h = StableHash.Mix(h, TrailMinLengthM);
            h = StableHash.Mix(h, CoinBlockClearS);
            h = StableHash.Mix(h, StreakLength);
            return h;
        }
    }
}
