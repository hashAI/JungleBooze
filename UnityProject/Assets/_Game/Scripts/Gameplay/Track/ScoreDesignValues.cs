using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary><c>ScoreTuning</c> (spec 002 section 3.5). Defaults are the spec start values.</summary>
    public sealed class ScoreDesignValues
    {
        public int PointsPerMeter = 1;
        public int NearMissBonus = 20;
        public int CoinStreakBonus = 50;
        public int ScoreMultiplier = 1;

        public static ScoreDesignValues CreateDefault()
        {
            return new ScoreDesignValues();
        }

        public ScoreDesignValues Clone()
        {
            return (ScoreDesignValues)MemberwiseClone();
        }

        /// <summary>Ranges are not given in spec 002; these are sanity bounds [ASSUMED].</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "pointsPerMeter", PointsPerMeter, 1, 100);
            ConfigChecks.Range(errors, "nearMissBonus", NearMissBonus, 0, 10000);
            ConfigChecks.Range(errors, "coinStreakBonus", CoinStreakBonus, 0, 10000);
            ConfigChecks.Range(errors, "scoreMultiplier", ScoreMultiplier, 1, 100);
            return errors.Count == before;
        }
    }
}
