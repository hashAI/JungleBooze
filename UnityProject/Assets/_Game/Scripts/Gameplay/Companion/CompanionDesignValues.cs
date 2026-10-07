using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// <c>CompanionTuning</c> (GDD 15.2 and 16) in designer units. <c>CompanionConfigAsset</c> serializes one of
    /// these. Field defaults are the GDD start values; values the GDD does not give are marked [ASSUMED]. The
    /// Perfect-release meter gain lives in <c>VineTuning</c> (<c>perfectCompanionMeterPercent</c>).
    /// </summary>
    [Serializable]
    public sealed class CompanionDesignValues
    {
        // ---- Assist meter (GDD 15.1 job 2) ----

        public float NearMissMeterPercent = 5f;

        /// <summary>Per completed coin streak (the streak length is <c>CoinTuning.streakLength</c>, 25).</summary>
        public float CoinStreakMeterPercent = 5f;

        public float GoodReleaseMeterPercent = 10f;

        // ---- Lift (GDD 15.2) ----

        public float LiftDurationMs = 4000f;
        public float LiftGlideHeightM = 2.5f;
        public float LiftDescentMs = 600f;

        /// <summary>Time to rise to the glide height [ASSUMED]; HERO leaves the ground on the first lift tick.</summary>
        public float LiftRiseMs = 300f;

        public float LiftClearAfterTouchdownMs = 1000f;
        public float LiftLandingInvulnerableMs = 500f;
        public float LiftCoinPullM = 10f;

        // ---- Call-outs (GDD 15.1 job 1) ----

        public float VineCalloutLeadMs = 2000f;
        public float HazardCalloutLeadMs = 1500f;

        /// <summary>Minimum time between two call-outs, the spoken length limit (GDD: ≤ 0.6 s) [ASSUMED as the gap].</summary>
        public float CalloutMinGapMs = 600f;

        // ---- Presentation (GDD 15.1 placement, style guide 6.2) ----

        public float HomeAheadM = 2.0f;
        public float HomeHeightM = 4.2f;

        /// <summary>Lateral follow smoothing, the camera's lane smoothing (GDD 6: 90 ms).</summary>
        public float FollowSmoothMs = 90f;

        public float SwoopHeightM = 4.0f;
        public float SwoopMs = 500f;

        /// <summary>Wingspan of the gray-box macaw (style guide 6.2 [ASSUMED]: 1.0 m).</summary>
        public float WingspanM = 1.0f;

        /// <summary>Wing beats per second [ASSUMED].</summary>
        public float FlapHz = 3f;

        /// <summary>How long a call-out bubble stays on screen [ASSUMED].</summary>
        public float CalloutBubbleMs = 900f;

        public static CompanionDesignValues CreateDefault()
        {
            return new CompanionDesignValues();
        }

        public CompanionDesignValues Clone()
        {
            return (CompanionDesignValues)MemberwiseClone();
        }

        /// <summary>Sanity ranges [ASSUMED]. Appends one message per problem; returns true when there are none.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "nearMissMeterPercent", NearMissMeterPercent, 0, 100);
            ConfigChecks.Range(errors, "coinStreakMeterPercent", CoinStreakMeterPercent, 0, 100);
            ConfigChecks.Range(errors, "goodReleaseMeterPercent", GoodReleaseMeterPercent, 0, 100);
            ConfigChecks.Range(errors, "liftDurationMs", LiftDurationMs, 500, 15000);
            ConfigChecks.Range(errors, "liftGlideHeightM", LiftGlideHeightM, 0.5, 6);
            ConfigChecks.Range(errors, "liftDescentMs", LiftDescentMs, 50, LiftDurationMs);
            ConfigChecks.Range(errors, "liftRiseMs", LiftRiseMs, 17, LiftDurationMs - LiftDescentMs);
            ConfigChecks.Range(errors, "liftClearAfterTouchdownMs", LiftClearAfterTouchdownMs, 0, 5000);
            ConfigChecks.Range(errors, "liftLandingInvulnerableMs", LiftLandingInvulnerableMs, 0, 5000);
            ConfigChecks.Range(errors, "liftCoinPullM", LiftCoinPullM, 0, 50);
            ConfigChecks.Range(errors, "vineCalloutLeadMs", VineCalloutLeadMs, 0, 6000);
            ConfigChecks.Range(errors, "hazardCalloutLeadMs", HazardCalloutLeadMs, 0, 6000);
            ConfigChecks.Range(errors, "calloutMinGapMs", CalloutMinGapMs, 0, 3000);
            ConfigChecks.Range(errors, "homeAheadM", HomeAheadM, -5, 10);
            ConfigChecks.Range(errors, "homeHeightM", HomeHeightM, 1, 10);
            ConfigChecks.Range(errors, "followSmoothMs", FollowSmoothMs, 0, 1000);
            ConfigChecks.Range(errors, "swoopHeightM", SwoopHeightM, 1, 10);
            ConfigChecks.Range(errors, "swoopMs", SwoopMs, 50, 2000);
            ConfigChecks.Range(errors, "wingspanM", WingspanM, 0.2, 3);
            ConfigChecks.Range(errors, "flapHz", FlapHz, 0.1, 12);
            ConfigChecks.Range(errors, "calloutBubbleMs", CalloutBubbleMs, 100, 5000);
            return errors.Count == before;
        }
    }
}
