using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>Immutable runtime <c>ScoreTuning</c> (spec 002 section 3.5).</summary>
    public sealed class ScoreConfig
    {
        private ScoreConfig(ScoreDesignValues v)
        {
            PointsPerMeter = v.PointsPerMeter;
            NearMissBonus = v.NearMissBonus;
            CoinStreakBonus = v.CoinStreakBonus;
            ScoreMultiplier = v.ScoreMultiplier;
        }

        public int PointsPerMeter { get; }

        public int NearMissBonus { get; }

        public int CoinStreakBonus { get; }

        public int ScoreMultiplier { get; }

        public static ScoreConfig FromDesignValues(ScoreDesignValues values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var errors = new List<string>();
            if (!values.Validate(errors))
            {
                throw new ArgumentException("Invalid score tuning: " + string.Join(" ", errors), nameof(values));
            }

            return new ScoreConfig(values);
        }

        public static ScoreConfig CreateDefault()
        {
            return FromDesignValues(ScoreDesignValues.CreateDefault());
        }

        /// <summary>
        /// Spec 002 section 10.6: <c>(floor(distanceM × pointsPerMeter) + bonusScore) × scoreMultiplier</c>.
        /// </summary>
        public long ComputeScore(double distanceM, long bonusScore)
        {
            return ComputeScore(distanceM, bonusScore, ScoreMultiplier);
        }

        /// <summary>Same with the player's multiplier (GDD 13.1: +1 per completed mission set) instead of the config's.</summary>
        public long ComputeScore(double distanceM, long bonusScore, int multiplier)
        {
            double d = distanceM > 0.0 ? distanceM : 0.0;
            long distancePoints = (long)Math.Floor(d * PointsPerMeter);
            return (distancePoints + bonusScore) * multiplier;
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            h = StableHash.Mix(h, PointsPerMeter);
            h = StableHash.Mix(h, NearMissBonus);
            h = StableHash.Mix(h, CoinStreakBonus);
            h = StableHash.Mix(h, ScoreMultiplier);
            return h;
        }
    }
}
