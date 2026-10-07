using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Vine
{
    /// <summary>
    /// <c>VineTuning</c> in designer units (GDD 7.5 and 16). <c>VineConfigAsset</c> copies its serialized fields
    /// into one of these. Field defaults are the GDD start values; values the GDD does not give are marked
    /// [ASSUMED] and are first guesses for the owner's feel check.
    /// </summary>
    [Serializable]
    public sealed class VineDesignValues
    {
        /// <summary>Master switch: false = the generator never places vine sections.</summary>
        public bool Enabled = true;

        // ---- Generator (GDD 7.5) ----

        /// <summary>First vine section 20–30 s of planned run time after the run start.</summary>
        public float FirstSectionMinS = 20f;
        public float FirstSectionMaxS = 30f;

        /// <summary>35–70 s of planned run time between vine sections.</summary>
        public float SectionIntervalMinS = 35f;
        public float SectionIntervalMaxS = 70f;

        /// <summary>Vines over a chasm only from this distance (m).</summary>
        public float ChasmVinesFromM = 600f;

        // ---- Grab (GDD 7.2, 7.3 step 2) ----

        public float GrabPointHeightM = 3.0f;
        public float GrabZoneLengthM = 2.0f;
        public float GrabZoneWidthM = 1.6f;
        public float GrabZoneBottomM = 1.6f;
        public float GrabZoneTopM = 3.6f;

        /// <summary>A jump started up to this long before HERO reaches the grab zone still grabs (spec 004 4.1: 550).</summary>
        public float GrabEarlinessMs = 550f;

        /// <summary>Time HERO takes to settle onto the pendulum (y and x only; z is never blended) [ASSUMED].</summary>
        public float GrabBlendMs = 100f;

        // ---- Pendulum (spec 004 sections 3, 4.2, 4.3 and 5) ----

        /// <summary>Rope length L: pivot to the hand. The pivot height is <see cref="GrabPointHeightM"/> + L (17.0 m).</summary>
        public float RopeLengthM = 14f;

        /// <summary>Swing gravity (arcade pendulum) [ASSUMED].</summary>
        public float SwingGravityMps2 = 22f;

        /// <summary>The catch speed is clamp(entry speed, min, max) (spec 004 4.2).</summary>
        public float CatchMinSpeedMps = 13f;
        public float CatchMaxSpeedMps = 16f;

        /// <summary>Validation only: the pendulum must never swing wider than this.</summary>
        public float MaxSwingAngleDeg = 62f;

        /// <summary>The start angle (taken from where HERO grabs) is clamped to this.</summary>
        public float GrabMaxAngleDeg = 10f;

        /// <summary>Hand to feet distance while hanging.</summary>
        public float HandToFeetM = 1.75f;

        /// <summary>Failsafe automatic release; normally the apex ends the swing first.</summary>
        public float SwingMaxMs = 1600f;

        /// <summary>Presentation runs at <see cref="HangTimeScale"/> for the first <see cref="HangMs"/> of a swing.</summary>
        public float HangMs = 300f;
        public float HangTimeScale = 0.8f;

        // ---- Release windows (spec 004 4.4), in ms since the grab ----

        public float GoodStartMs = 450f;
        public float PerfectStartMs = 700f;

        /// <summary>Length of the Perfect window; it ends (exclusive) at PerfectStart + width (183 ms = 11 ticks).</summary>
        public float PerfectWidthMs = 183f;

        /// <summary>A too-early release swipe is kept this long.</summary>
        public float ReleaseBufferMs = 150f;

        // ---- Release velocity and flight (spec 004 4.5) ----

        public float LaunchGravityMps2 = 16f;

        /// <summary>Impulse added to the pendulum velocity at <see cref="ImpulseAngleDeg"/>.</summary>
        public float PerfectImpulseMps = 3f;
        public float GoodImpulseMps = 1.5f;
        public float PoorImpulseMps = 0f;
        public float ImpulseAngleDeg = 30f;

        /// <summary>Floors that make the swing unfailable (a release at the apex has no tangential speed).</summary>
        public float ReleaseMinForwardMps = 6f;
        public float ReleaseMinUpMps = 2f;

        /// <summary>Speed blend from the flight speed to the run speed after landing from a vine.</summary>
        public float LandingSpeedBlendMs = 500f;

        // ---- Chains (spec 004 4.8) ----

        /// <summary>A chained launch is aimed so HERO's feet are at this height at the next vine's grab point.</summary>
        public float ChainArriveFeetM = 1.5f;

        /// <summary>Flight time limits of a guided chain launch.</summary>
        public float ChainFlightMinS = 0.85f;
        public float ChainFlightMaxS = 1.5f;

        /// <summary>The next vine of a chain must be within this distance ahead to be aimed at.</summary>
        public float ChainMaxReachM = 40f;

        // ---- Score and coins (GDD 7.5) ----

        public int GoodScore = 150;
        public int PerfectScore = 400;
        public int AutoScore = 50;

        /// <summary>Score multiplier after 0, 1, 2 Perfects earlier in the same chain.</summary>
        public float[] ChainMultipliers = { 1f, 1.5f, 2f };

        public int GoodCoins = 10;
        public int PerfectCoins = 25;

        /// <summary>How many of the Perfect coins form the bonus ring (the rest trail along the launch arc).</summary>
        public int PerfectRingCoins = 12;
        public float RingRadiusM = 0.7f;

        /// <summary>COMPANION meter gain for a Perfect (percent). Read by the companion system (later).</summary>
        public float PerfectCompanionMeterPercent = 25f;

        // ---- Capacity ----

        public int MaxActiveVines = 32;
        public int MaxBonusCoins = 128;

        public static VineDesignValues CreateDefault()
        {
            return new VineDesignValues();
        }

        public VineDesignValues Clone()
        {
            var copy = (VineDesignValues)MemberwiseClone();
            copy.ChainMultipliers = ChainMultipliers == null ? null : (float[])ChainMultipliers.Clone();
            return copy;
        }

        /// <summary>Sanity ranges [ASSUMED]. Appends one message per problem; returns true when there are none.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Range(errors, "firstSectionMinS", FirstSectionMinS, 5, 120);
            ConfigChecks.Range(errors, "firstSectionMaxS", FirstSectionMaxS, FirstSectionMinS, 180);
            ConfigChecks.Range(errors, "sectionIntervalMinS", SectionIntervalMinS, 10, 300);
            ConfigChecks.Range(errors, "sectionIntervalMaxS", SectionIntervalMaxS, SectionIntervalMinS, 600);
            ConfigChecks.Range(errors, "chasmVinesFromM", ChasmVinesFromM, 0, 100000);
            ConfigChecks.Range(errors, "grabPointHeightM", GrabPointHeightM, 1.5, 6);
            ConfigChecks.Range(errors, "grabZoneLengthM", GrabZoneLengthM, 0.5, 6);
            ConfigChecks.Range(errors, "grabZoneWidthM", GrabZoneWidthM, 0.5, 2.4);
            ConfigChecks.Range(errors, "grabZoneBottomM", GrabZoneBottomM, 0, 5);
            ConfigChecks.Range(errors, "grabZoneTopM", GrabZoneTopM, GrabZoneBottomM + 0.1, 8);
            ConfigChecks.Range(errors, "grabEarlinessMs", GrabEarlinessMs, 0, 1000);
            ConfigChecks.Range(errors, "grabBlendMs", GrabBlendMs, 0, 500);
            ConfigChecks.Range(errors, "ropeLengthM", RopeLengthM, 6, 20);
            ConfigChecks.Range(errors, "swingGravityMps2", SwingGravityMps2, 8, 40);
            ConfigChecks.Range(errors, "catchMinSpeedMps", CatchMinSpeedMps, 1, 40);
            ConfigChecks.Range(errors, "catchMaxSpeedMps", CatchMaxSpeedMps, CatchMinSpeedMps, 40);
            ConfigChecks.Range(errors, "maxSwingAngleDeg", MaxSwingAngleDeg, 20, 75);
            ConfigChecks.Range(errors, "grabMaxAngleDeg", GrabMaxAngleDeg, 0, 20);
            ConfigChecks.Range(errors, "handToFeetM", HandToFeetM, 0.5, 3);
            ConfigChecks.Range(errors, "swingMaxMs", SwingMaxMs, 500, 4000);
            ConfigChecks.Range(errors, "hangMs", HangMs, 0, 1000);
            ConfigChecks.Range(errors, "hangTimeScale", HangTimeScale, 0.25, 1);
            ConfigChecks.Range(errors, "goodStartMs", GoodStartMs, 50, 3000);
            ConfigChecks.Range(errors, "perfectStartMs", PerfectStartMs, GoodStartMs, 3000);
            ConfigChecks.Range(errors, "perfectWidthMs", PerfectWidthMs, 17, 1000);
            ConfigChecks.Range(errors, "releaseBufferMs", ReleaseBufferMs, 0, 500);
            ConfigChecks.Range(errors, "launchGravityMps2", LaunchGravityMps2, 1, 60);
            ConfigChecks.Range(errors, "perfectImpulseMps", PerfectImpulseMps, 0, 30);
            ConfigChecks.Range(errors, "goodImpulseMps", GoodImpulseMps, 0, 30);
            ConfigChecks.Range(errors, "poorImpulseMps", PoorImpulseMps, 0, 30);
            ConfigChecks.Range(errors, "impulseAngleDeg", ImpulseAngleDeg, 0, 80);
            ConfigChecks.Range(errors, "releaseMinForwardMps", ReleaseMinForwardMps, 0, 30);
            ConfigChecks.Range(errors, "releaseMinUpMps", ReleaseMinUpMps, 0, 30);
            ConfigChecks.Range(errors, "landingSpeedBlendMs", LandingSpeedBlendMs, 0, 2000);
            ConfigChecks.Range(errors, "chainArriveFeetM", ChainArriveFeetM, 0.2, 5);
            ConfigChecks.Range(errors, "chainFlightMinS", ChainFlightMinS, 0.2, 5);
            ConfigChecks.Range(errors, "chainFlightMaxS", ChainFlightMaxS, ChainFlightMinS, 5);
            ConfigChecks.Range(errors, "chainMaxReachM", ChainMaxReachM, 10, 200);
            ValidateSwingAmplitude(errors);
            ConfigChecks.Range(errors, "goodScore", GoodScore, 0, 100000);
            ConfigChecks.Range(errors, "perfectScore", PerfectScore, 0, 100000);
            ConfigChecks.Range(errors, "autoScore", AutoScore, 0, 100000);
            if (ChainMultipliers == null || ChainMultipliers.Length == 0)
            {
                errors.Add("chainMultipliers needs at least one value.");
            }
            else
            {
                for (int i = 0; i < ChainMultipliers.Length; i++)
                {
                    ConfigChecks.Range(errors, "chainMultipliers[" + i + "]", ChainMultipliers[i], 0, 100);
                }
            }

            ConfigChecks.Range(errors, "goodCoins", GoodCoins, 0, 64);
            ConfigChecks.Range(errors, "perfectCoins", PerfectCoins, 0, 64);
            ConfigChecks.Range(errors, "perfectRingCoins", PerfectRingCoins, 0, PerfectCoins);
            ConfigChecks.Range(errors, "ringRadiusM", RingRadiusM, 0, 2);
            ConfigChecks.Range(errors, "perfectCompanionMeterPercent", PerfectCompanionMeterPercent, 0, 100);
            ConfigChecks.Range(errors, "maxActiveVines", MaxActiveVines, 4, 256);
            ConfigChecks.Range(errors, "maxBonusCoins", MaxBonusCoins, PerfectCoins, 1024);
            return errors.Count == before;
        }

        /// <summary>
        /// Spec 004 4.3 and T401: the widest swing (catch speed max, start angle at its clamp) must stay within
        /// <see cref="MaxSwingAngleDeg"/>: <c>cos(theta0max) - vCmax^2 / (2 g L) &gt;= cos(MaxSwingAngleDeg)</c>
        /// (default: <c>vCmax &lt;= 17.8</c>). Setup time only, so <c>System.Math</c> is fine here.
        /// </summary>
        private void ValidateSwingAmplitude(List<string> errors)
        {
            if (!(RopeLengthM > 0f) || !(SwingGravityMps2 > 0f))
            {
                return;
            }

            const double degToRad = Math.PI / 180.0;
            double energy = (double)CatchMaxSpeedMps * CatchMaxSpeedMps / (2.0 * SwingGravityMps2 * RopeLengthM);
            double cosTop = Math.Cos(GrabMaxAngleDeg * degToRad) - energy;
            if (cosTop < Math.Cos(MaxSwingAngleDeg * degToRad))
            {
                errors.Add("catchMaxSpeedMps = " + CatchMaxSpeedMps + " swings wider than maxSwingAngleDeg = " + MaxSwingAngleDeg + " deg.");
            }
        }
    }
}
