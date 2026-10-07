using System.Collections.Generic;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// <c>DifficultyTiers</c> in designer units (spec 002 section 3.3). Defaults are the FP1 start values and the
    /// tier weights of section 7.3 (from <see cref="JungleChunkLibraryDefaults"/>). Validation that needs the chunk
    /// library and the speed curve happens in <see cref="DifficultyTiersConfig.Build"/>.
    /// </summary>
    public sealed class DifficultyTiersDesignValues
    {
        /// <summary>
        /// Minimum distance between consecutive tier starts so density never rises by more than one tier step
        /// within 200 m (GDD 11.2 "no spike" rule) [ASSUMED check value].
        /// </summary>
        public const float MinTierSpacingM = 200f;

        public DifficultyTierDesign[] Tiers = JungleChunkLibraryDefaults.CreateTierDesigns();

        public static DifficultyTiersDesignValues CreateDefault()
        {
            return new DifficultyTiersDesignValues();
        }

        public DifficultyTiersDesignValues Clone()
        {
            var copy = new DifficultyTiersDesignValues();
            copy.Tiers = new DifficultyTierDesign[Tiers == null ? 0 : Tiers.Length];
            for (int i = 0; i < copy.Tiers.Length; i++)
            {
                copy.Tiers[i] = Tiers[i]?.Clone();
            }

            return copy;
        }

        /// <summary>Checks that do not need the library: count, ordering, spacing, ranges.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            if (Tiers == null || Tiers.Length == 0)
            {
                errors.Add("DifficultyTiers needs at least one tier.");
                return false;
            }

            for (int i = 0; i < Tiers.Length; i++)
            {
                DifficultyTierDesign t = Tiers[i];
                string name = "tier " + (i + 1);
                if (t == null)
                {
                    errors.Add(name + " is missing.");
                    continue;
                }

                if (i == 0 && t.FromM != 0f)
                {
                    errors.Add("tier 1 fromM must be 0.");
                }

                if (i > 0 && Tiers[i - 1] != null)
                {
                    float prev = Tiers[i - 1].FromM;
                    if (!(t.FromM > prev))
                    {
                        errors.Add(name + ": fromM must be strictly increasing.");
                    }
                    else if (t.FromM - prev < MinTierSpacingM)
                    {
                        errors.Add(name + ": fromM must be at least " + MinTierSpacingM + " m after the previous tier (no spike rule).");
                    }
                }

                ConfigChecks.Positive(errors, name + " targetRowsPer100M", t.TargetRowsPer100M);
                ConfigChecks.Positive(errors, name + " minActionS", t.MinActionS);

                if (t.Weights == null)
                {
                    errors.Add(name + ": weights missing.");
                    continue;
                }

                for (int w = 0; w < t.Weights.Length; w++)
                {
                    if (t.Weights[w].Weight < 0)
                    {
                        errors.Add(name + ": weight of " + t.Weights[w].ChunkId + " must not be negative.");
                    }
                }
            }

            return errors.Count == before;
        }
    }
}
