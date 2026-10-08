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

        /// <summary>A jump started up to this long before HERO reaches the grab zone still grabs.</summary>
        public float GrabEarlinessMs = 450f;

        /// <summary>Time HERO takes to settle from the grab position onto the swing arc [ASSUMED].</summary>
        public float GrabBlendMs = 100f;

        // ---- Swing (GDD 7.3 step 3) ----

        public float SwingMs = 1400f;

        /// <summary>Visual pendulum radius (m).</summary>
        public float SwingRadiusM = 6f;

        /// <summary>Pendulum angle at phase 0 and phase 1 (deg; positive = swung forward) [ASSUMED].</summary>
        public float SwingStartAngleDeg = -20f;
        public float SwingEndAngleDeg = 55f;

        /// <summary>Feet height at the bottom of the arc (angle 0) [ASSUMED].</summary>
        public float SwingLowestFeetM = 0.9f;

        /// <summary>Presentation runs at <see cref="HangTimeScale"/> for the first <see cref="HangMs"/> of a swing.</summary>
        public float HangMs = 300f;
        public float HangTimeScale = 0.8f;

        // ---- Release (GDD 7.3 step 4) ----

        public float GoodStartPhase = 0.45f;
        public float PerfectStartPhase = 0.66f;
        public float PerfectEndPhase = 0.80f;

        /// <summary>A too-early release swipe is kept this long.</summary>
        public float ReleaseBufferMs = 150f;

        // ---- Launch [ASSUMED numbers, tuned so every release clears a chasm at the slowest chasm speed] ----

        public float LaunchGravityMps2 = 16f;
        public float GoodLaunchSpeedMps = 8f;
        public float PerfectLaunchSpeedMps = 10f;
        public float AutoLaunchSpeedMps = 4f;

        /// <summary>A chained launch is aimed so HERO's feet are at this height at the next vine's grab point.</summary>
        public float ChainArriveFeetM = 1.8f;

        /// <summary>Gravity limits for a chained (guided) launch.</summary>
        public float ChainGravityMinMps2 = 6f;
        public float ChainGravityMaxMps2 = 40f;

        /// <summary>The next vine of a chain must be within this distance ahead to be aimed at.</summary>
        public float ChainMaxReachM = 70f;

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
            ConfigChecks.Range(errors, "swingMs", SwingMs, 500, 4000);
            ConfigChecks.Range(errors, "swingRadiusM", SwingRadiusM, 1, 20);
            ConfigChecks.Range(errors, "swingStartAngleDeg", SwingStartAngleDeg, -80, 80);
            ConfigChecks.Range(errors, "swingEndAngleDeg", SwingEndAngleDeg, SwingStartAngleDeg, 85);
            ConfigChecks.Range(errors, "swingLowestFeetM", SwingLowestFeetM, 0.2, 4);
            ConfigChecks.Range(errors, "hangMs", HangMs, 0, 1000);
            ConfigChecks.Range(errors, "hangTimeScale", HangTimeScale, 0.25, 1);
            ConfigChecks.Range(errors, "goodStartPhase", GoodStartPhase, 0.05, 0.95);
            ConfigChecks.Range(errors, "perfectStartPhase", PerfectStartPhase, GoodStartPhase, 0.99);
            ConfigChecks.Range(errors, "perfectEndPhase", PerfectEndPhase, PerfectStartPhase + 0.01, 1);
            ConfigChecks.Range(errors, "releaseBufferMs", ReleaseBufferMs, 0, 500);
            ConfigChecks.Range(errors, "launchGravityMps2", LaunchGravityMps2, 1, 60);
            ConfigChecks.Range(errors, "goodLaunchSpeedMps", GoodLaunchSpeedMps, 0, 30);
            ConfigChecks.Range(errors, "perfectLaunchSpeedMps", PerfectLaunchSpeedMps, 0, 30);
            ConfigChecks.Range(errors, "autoLaunchSpeedMps", AutoLaunchSpeedMps, 0, 30);
            ConfigChecks.Range(errors, "chainArriveFeetM", ChainArriveFeetM, 0.2, 5);
            ConfigChecks.Range(errors, "chainGravityMinMps2", ChainGravityMinMps2, 1, 60);
            ConfigChecks.Range(errors, "chainGravityMaxMps2", ChainGravityMaxMps2, ChainGravityMinMps2, 100);
            ConfigChecks.Range(errors, "chainMaxReachM", ChainMaxReachM, 10, 200);
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
    }
}
