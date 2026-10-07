using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// <c>TrackPresentationTuning</c> (spec 002 section 3.6), presentation only. Plain values for the track views
    /// (stage C2); the simulation never reads it. Mutable on purpose like the other design-value classes; views
    /// should treat it as read-only. Defaults are the spec start values and the pool sizes of section 8.6.
    /// </summary>
    public sealed class TrackPresentationConfig
    {
        public float ViewSpawnAheadM = 95f;
        public float FogStartM = 45f;
        public float FogEndM = 90f;
        public float TelegraphMinS = 1.2f;
        public float GrayBoxVisualMarginM = 0.08f;
        public float MoverMarkerWidthM = 0.5f;
        public float GapFlagLeadS = 1.0f;

        public int PrewarmGroundSegments = 80;
        public int PrewarmLowBarriers = 12;
        public int PrewarmHighBarriers = 12;
        public int PrewarmFullBlocks = 12;
        public int PrewarmMovers = 4;
        public int PrewarmGapFlags = 6;
        public int PrewarmCoins = 120;
        public int PrewarmCoinPickupVfx = 16;

        public static TrackPresentationConfig CreateDefault()
        {
            return new TrackPresentationConfig();
        }

        public TrackPresentationConfig Clone()
        {
            return (TrackPresentationConfig)MemberwiseClone();
        }

        /// <summary>
        /// Range checks. <paramref name="runnerFogStartM"/> is <c>RunnerPresentationTuning.fogStartM</c>; pass
        /// <see cref="float.NaN"/> to skip the equality rule.
        /// </summary>
        public bool Validate(List<string> errors, float runnerFogStartM)
        {
            int before = errors.Count;
            ConfigChecks.Positive(errors, "viewSpawnAheadM", ViewSpawnAheadM);
            ConfigChecks.Positive(errors, "fogStartM", FogStartM);
            if (!(FogEndM > FogStartM))
            {
                errors.Add("fogEndM must be greater than fogStartM.");
            }

            if (!(ViewSpawnAheadM >= FogEndM))
            {
                errors.Add("viewSpawnAheadM must be at least fogEndM (no pop-in).");
            }

            if (!float.IsNaN(runnerFogStartM) && FogStartM != runnerFogStartM)
            {
                errors.Add("fogStartM must equal RunnerPresentationTuning.fogStartM (" + runnerFogStartM + ").");
            }

            ConfigChecks.Positive(errors, "telegraphMinS", TelegraphMinS);
            ConfigChecks.Range(errors, "grayBoxVisualMarginM", GrayBoxVisualMarginM, 0, 0.1);
            ConfigChecks.Positive(errors, "moverMarkerWidthM", MoverMarkerWidthM);
            ConfigChecks.Positive(errors, "gapFlagLeadS", GapFlagLeadS);
            ConfigChecks.Range(errors, "poolPrewarm ground", PrewarmGroundSegments, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm low", PrewarmLowBarriers, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm high", PrewarmHighBarriers, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm full", PrewarmFullBlocks, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm mover", PrewarmMovers, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm gap flags", PrewarmGapFlags, 1, 1000);
            ConfigChecks.Range(errors, "poolPrewarm coin", PrewarmCoins, 1, 4000);
            ConfigChecks.Range(errors, "poolPrewarm coin vfx", PrewarmCoinPickupVfx, 1, 1000);
            return errors.Count == before;
        }
    }
}
