using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// <c>ChunkValidatorTuning</c> (spec 002 section 3.9), editor and tests only. Plain values for the chunk
    /// validator (stage B3). The quick validation speeds are a rule (band min, every tier's <c>vMaxMps</c> inside
    /// the band, band max), computed by the validator from <see cref="DifficultyTiersConfig"/>, so they are not a field.
    /// </summary>
    public sealed class ChunkValidatorConfig
    {
        public float FullSpeedStepMps = 0.25f;
        public float[] PhaseOffsets = { 0f, 1f / 3f, 2f / 3f };
        public int MaxStatesPerTick = 50000;
        public float PositionQuantumM = 0.001f;
        public float VisibilitySampleOffsetM = 0.7f;
        public int VisibilityMinPoints = 2;
        public float SurvivingLaneSampleM = 0.5f;

        public static ChunkValidatorConfig CreateDefault()
        {
            return new ChunkValidatorConfig();
        }

        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            ConfigChecks.Positive(errors, "fullSpeedStepMps", FullSpeedStepMps);
            if (PhaseOffsets == null || PhaseOffsets.Length == 0)
            {
                errors.Add("phaseOffsets needs at least one value.");
            }
            else
            {
                for (int i = 0; i < PhaseOffsets.Length; i++)
                {
                    if (!(PhaseOffsets[i] >= 0f && PhaseOffsets[i] < 1f))
                    {
                        errors.Add("phaseOffsets[" + i + "] must be in [0, 1).");
                    }
                }
            }

            ConfigChecks.Range(errors, "maxStatesPerTick", MaxStatesPerTick, 1, 10000000);
            ConfigChecks.Positive(errors, "positionQuantumM", PositionQuantumM);
            ConfigChecks.Positive(errors, "visibilitySampleOffsetM", VisibilitySampleOffsetM);
            ConfigChecks.Range(errors, "visibilityMinPoints", VisibilityMinPoints, 1, 3);
            ConfigChecks.Positive(errors, "survivingLaneSampleM", SurvivingLaneSampleM);
            return errors.Count == before;
        }
    }
}
