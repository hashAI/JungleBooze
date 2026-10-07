using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// <c>EconomyConfig</c> (GDD 16) in designer units. For now it holds the Continue rules of GDD 14.4; prices,
    /// mission rewards and the daily calendar join it later. <c>EconomyConfigAsset</c> serializes one of these.
    /// Values the GDD does not give are marked [ASSUMED].
    /// </summary>
    [Serializable]
    public sealed class EconomyDesignValues
    {
        /// <summary>Coin price of the 1st, 2nd, ... continue in a run (GDD 14.4: 300, then 600).</summary>
        public int[] ContinueCosts = { 300, 600 };

        /// <summary>GDD 14.4: at most 2 continues per run.</summary>
        public int MaxContinuesPerRun = 2;

        /// <summary>The Continue screen stays up this long (GDD 14.4: 5 s), then Game Over.</summary>
        public float ContinueOfferSeconds = 5f;

        /// <summary>GDD 14.4: one free continue from the companion in the player's first session.</summary>
        public bool FreeContinueInFirstSession = true;

        /// <summary>GDD 14.4: 2 s invulnerability after a continue.</summary>
        public float ContinueInvulnerableMs = 2000f;

        /// <summary>GDD 14.4: 1.5 s of clear track after a continue.</summary>
        public float ContinueClearStretchSeconds = 1.5f;

        /// <summary>Continue buttons ignore input this long after the screen opens [ASSUMED, like Game Over's 400 ms].</summary>
        public float ContinueInputLockMs = 400f;

        public static EconomyDesignValues CreateDefault()
        {
            return new EconomyDesignValues();
        }

        public EconomyDesignValues Clone()
        {
            var copy = (EconomyDesignValues)MemberwiseClone();
            copy.ContinueCosts = ContinueCosts == null ? null : (int[])ContinueCosts.Clone();
            return copy;
        }

        /// <summary>Sanity ranges [ASSUMED]. Appends one message per problem; returns true when there are none.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            if (ContinueCosts == null || ContinueCosts.Length == 0)
            {
                errors.Add("continueCosts needs at least one value.");
            }
            else
            {
                for (int i = 0; i < ContinueCosts.Length; i++)
                {
                    ConfigChecks.Range(errors, "continueCosts[" + i + "]", ContinueCosts[i], 0, 1000000);
                }
            }

            ConfigChecks.Range(errors, "maxContinuesPerRun", MaxContinuesPerRun, 0, 10);
            ConfigChecks.Range(errors, "continueOfferSeconds", ContinueOfferSeconds, 1, 30);
            ConfigChecks.Range(errors, "continueInvulnerableMs", ContinueInvulnerableMs, 0, 10000);
            ConfigChecks.Range(errors, "continueClearStretchSeconds", ContinueClearStretchSeconds, 0, 10);
            ConfigChecks.Range(errors, "continueInputLockMs", ContinueInputLockMs, 0, 2000);
            return errors.Count == before;
        }
    }
}
