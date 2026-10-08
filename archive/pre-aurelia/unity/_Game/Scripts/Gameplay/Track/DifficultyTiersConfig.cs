using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// Immutable runtime <c>DifficultyTiers</c> (spec 002 section 3.3). Built against a speed curve (for the derived
    /// values) and a chunk library (weights resolved to library indices).
    /// </summary>
    public sealed class DifficultyTiersConfig
    {
        private readonly DifficultyTier[] _tiers;

        private DifficultyTiersConfig(DifficultyTier[] tiers)
        {
            _tiers = tiers;
        }

        public int TierCount => _tiers.Length;

        /// <summary>Tier by 0-based index (tier number − 1).</summary>
        public DifficultyTier GetTier(int index)
        {
            return _tiers[index];
        }

        /// <summary>0-based index of the highest tier with <c>fromM ≤ startZ</c> (spec 002 section 8.3).</summary>
        public int TierIndexAt(double startZ)
        {
            int index = 0;
            for (int i = 1; i < _tiers.Length; i++)
            {
                if (_tiers[i].FromM <= startZ)
                {
                    index = i;
                }
                else
                {
                    break;
                }
            }

            return index;
        }

        /// <summary>
        /// Validates <paramref name="values"/> against the library (AC-201) and derives <c>vMaxMps</c> and
        /// <c>minRowSpacingM</c> (AC-202). Appends problems to <paramref name="errors"/>; returns null when invalid.
        /// </summary>
        public static DifficultyTiersConfig Build(DifficultyTiersDesignValues values, SpeedCurve curve, ChunkLibrary library, List<string> errors)
        {
            if (values == null || curve == null || library == null)
            {
                errors.Add("DifficultyTiers needs tier values, a speed curve and a chunk library.");
                return null;
            }

            int before = errors.Count;
            if (!values.Validate(errors))
            {
                return null;
            }

            int n = values.Tiers.Length;
            var tiers = new DifficultyTier[n];
            double cap = curve.GetRowSpeed(curve.RowCount - 1);
            for (int t = 0; t < n; t++)
            {
                DifficultyTierDesign design = values.Tiers[t];
                int number = t + 1;
                var weights = new int[library.Count];
                for (int w = 0; w < design.Weights.Length; w++)
                {
                    ChunkWeight entry = design.Weights[w];
                    int index = library.IndexOf(entry.ChunkId);
                    if (index < 0)
                    {
                        errors.Add("tier " + number + ": unknown chunk id " + entry.ChunkId + ".");
                        continue;
                    }

                    ChunkData chunk = library[index];
                    if (entry.Weight > 0)
                    {
                        if (chunk.Kind != ChunkKind.Normal && chunk.Kind != ChunkKind.Signature)
                        {
                            errors.Add("tier " + number + ": " + chunk.Id + " is not a normal or signature chunk and cannot have a weight.");
                        }

                        if (number < chunk.MinTier || number > chunk.MaxTier)
                        {
                            errors.Add("tier " + number + ": " + chunk.Id + " has a weight outside its tier range " + chunk.MinTier + "–" + chunk.MaxTier + ".");
                        }
                    }

                    weights[index] += entry.Weight;
                }

                double vMax = t + 1 < n ? curve.Evaluate(values.Tiers[t + 1].FromM) : cap;
                tiers[t] = new DifficultyTier(number, design.FromM, design.TargetRowsPer100M, design.MinActionS, vMax, weights);
                if (tiers[t].TotalWeight <= 0)
                {
                    errors.Add("tier " + number + ": the pool is empty (total weight 0).");
                }
            }

            return errors.Count == before ? new DifficultyTiersConfig(tiers) : null;
        }

        /// <summary>Like <see cref="Build"/>, but throws <see cref="ArgumentException"/> when invalid.</summary>
        public static DifficultyTiersConfig FromDesignValues(DifficultyTiersDesignValues values, SpeedCurve curve, ChunkLibrary library)
        {
            var errors = new List<string>();
            DifficultyTiersConfig config = Build(values, curve, library, errors);
            if (config == null)
            {
                throw new ArgumentException("Invalid difficulty tiers: " + string.Join(" ", errors), nameof(values));
            }

            return config;
        }

        public ulong ComputeHash()
        {
            ulong h = StableHash.Seed;
            for (int t = 0; t < _tiers.Length; t++)
            {
                DifficultyTier tier = _tiers[t];
                h = StableHash.Mix(h, tier.FromM);
                h = StableHash.Mix(h, tier.TargetRowsPer100M);
                h = StableHash.Mix(h, tier.MinActionS);
                for (int c = 0; c < tier.ChunkCount; c++)
                {
                    h = StableHash.Mix(h, tier.GetWeight(c));
                }
            }

            return h;
        }
    }
}
