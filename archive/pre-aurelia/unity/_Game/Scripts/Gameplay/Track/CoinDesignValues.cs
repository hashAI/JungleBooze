using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary><c>CoinTuning</c> in designer units (spec 002 section 3.4). Defaults are the spec start values.</summary>
    [Serializable]
    public sealed class CoinDesignValues
    {
        public int CoinValue = 1;
        public float PickupRadiusM = 0.6f;
        public float CoinHeightM = 0.75f;
        public float CoinVisualRadiusM = 0.25f;
        public float CoinClearanceM = 0.1f;
        public float LineSpacingM = 2.0f;
        public int ArcCoinCount = 7;
        public float ArcSpanFraction = 0.75f;
        public float TrailMinLengthM = 6.0f;
        public float CoinBlockClearS = 0.35f;
        public int StreakLength = 25;

        public static CoinDesignValues CreateDefault()
        {
            return new CoinDesignValues();
        }

        public CoinDesignValues Clone()
        {
            return (CoinDesignValues)MemberwiseClone();
        }

        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "coinValue", CoinValue, 1, 10);
            ConfigChecks.Range(errors, "pickupRadiusM", PickupRadiusM, 0.3, 1.0);
            ConfigChecks.Range(errors, "coinHeightM", CoinHeightM, 0.5, 1.0);
            ConfigChecks.Positive(errors, "coinVisualRadiusM", CoinVisualRadiusM);
            ConfigChecks.Range(errors, "coinClearanceM", CoinClearanceM, 0.05, 0.3);
            ConfigChecks.Range(errors, "lineSpacingM", LineSpacingM, 1.0, 4.0);
            ConfigChecks.Range(errors, "arcCoinCount", ArcCoinCount, 5, 9);
            if (ArcCoinCount % 2 == 0)
            {
                errors.Add("arcCoinCount must be odd.");
            }

            ConfigChecks.Range(errors, "arcSpanFraction", ArcSpanFraction, 0.5, 0.9);
            ConfigChecks.Range(errors, "trailMinLengthM", TrailMinLengthM, 4.0, 12.0);
            ConfigChecks.Range(errors, "coinBlockClearS", CoinBlockClearS, 0.2, 0.6);
            ConfigChecks.Range(errors, "streakLength", StreakLength, 10, 100);
            return errors.Count == before;
        }
    }
}
