using System;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Results screen timing and next-objective thresholds (spec 103 §9.2, GDD §17).</summary>
    [Serializable]
    public sealed class ResultsConfig
    {
        /// <summary>Count-up duration (≤ 2.0 s; any tap completes it), s.</summary>
        public float CountUpTime = 1.2f;

        /// <summary>The unlock moment after LEARN, s.</summary>
        public float UnlockMomentTime = 1.2f;

        /// <summary>Priority 2: the run ended within this fraction of the best distance.</summary>
        public float NearBestFraction = 0.10f;

        /// <summary>Priority 3: the next ability is at least this funded…</summary>
        public float NextAbilityFunded = 0.60f;

        /// <summary>…or this after run 1 [ASSUMED spec 103 §9.2].</summary>
        public float NextAbilityFundedFirstRun = 0.50f;

        /// <summary>Establishing shot before Expedition 1, s (any touch skips).</summary>
        public float EstablishingShotTime = 4f;

        /// <summary>Journal sizes per category for "Creatures 1/3" (GDD §15).</summary>
        public int CreatureTotal = 3;

        public int PlantTotal = 5;
        public int LocationTotal = 4;
        public int MysteryTotal = 2;

        /// <summary>Revive "Continue?" (GDD §11): crystal cost per revive in a run, the limit and the offer time, s.</summary>
        public int[] ReviveCosts = { 1, 2, 4 };

        public int MaxRevives = 3;

        public float ReviveOfferTime = 4f;

        public ResultsConfig Clone()
        {
            var copy = (ResultsConfig)MemberwiseClone();
            copy.ReviveCosts = ReviveCosts != null ? (int[])ReviveCosts.Clone() : new[] { 1, 2, 4 };
            return copy;
        }
    }
}
