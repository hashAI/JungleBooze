using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// World Director tuning (spec 102 §5–8, spec 103 §10): phases, repetition rules, weights, dynamic difficulty,
    /// mercy, showcase, reward placement and streaming distances. Authored in <c>WorldDirectorConfig.asset</c>.
    /// </summary>
    [Serializable]
    public sealed class WorldDirectorConfig
    {
        public List<PhaseRule> Phases = DefaultPhases();

        /// <summary>R1: no chunk id within the last N picks.</summary>
        public int NoRepeatWindow = 6;

        /// <summary>R2: same category at most N in a row (Recovery never twice).</summary>
        public int MaxSameCategory = 2;

        /// <summary>R3: same environment set at most N in a row unless SetPiece.</summary>
        public int MaxSameEnvironment = 3;

        /// <summary>R4: same variant signature not within the last N chunks of that family.</summary>
        public int VariantSignatureWindow = 3;

        /// <summary>Dimension mix (§5): at most N consecutive chunks with Reaction ≥ 2 (and Navigation ≥ 2).</summary>
        public int MaxConsecutiveHighDimension = 2;

        public int FreshnessWindow = 12;
        public float FreshnessFactor = 0.5f;
        public float DimensionFitFactor = 1.2f;
        public float SkillBiasFactor = 1.3f;

        /// <summary>Mercy (§6.3): this many hits within the window → the next unplanned chunk is Recovery.</summary>
        public int MercyHits = 2;

        public float MercyWindowSeconds = 15f;

        /// <summary>Showcase rule (spec 103 §10.2): forced within this many picks after the start chunk.</summary>
        public int ShowcaseWindow = 5;

        /// <summary>Validator human margins (spec 102 V3/V14/W5): lateral rate 8.0 m/s (73% of vLatMax) after a 0.10 s reaction.</summary>
        public float HumanLateralRate = 8.0f;

        public float HumanReactionTime = 0.10f;

        /// <summary>W5: fraction of (swimVLatMax − |cx|) a human is expected to use.</summary>
        public float HumanSwimMargin = 0.73f;

        /// <summary>W4: no obstacle this close after water entry / before water exit, m.</summary>
        public float WaterEntryClearance = 10f;

        /// <summary>View-side curvature limit (radius ≥ 60 m, spec 102 §2.1), 1/m.</summary>
        public float MaxCurvature = 1f / 60f;

        // Dynamic difficulty (§6.4).
        public float StartSkill = -0.3f;
        public float SkillRate = 0.3f;
        public float SkillMaxStep = 0.2f;
        public float SkillLow = -0.5f;
        public float SkillHigh = 0.5f;
        public float DistanceReference = 1500f;
        public float DistanceLogBase = 4f;
        public float TraversalTarget = 0.85f;
        public float TraversalScale = 0.15f;
        public float CueIntensityLowSkill = 1.30f;
        public float CueIntensityHighSkill = 0.85f;

        // Rewards (§7).
        public float LowCoinDensity = 0.7f;
        public float RiskyCrystalChance = 0.2f;
        public float RichCrystalFactor = 1.3f;
        public float FlowCrystalMinSpacing = 600f;

        /// <summary>After the minimum spacing, chance per chunk = chunk length / this (≈ 1 crystal per 1,000 m).</summary>
        public float FlowCrystalChanceLength = 400f;

        public float PowerUpSpacingMin = 600f;
        public float PowerUpSpacingMax = 900f;
        public float RiskyPowerUpPreference = 0.7f;

        // Streaming.
        /// <summary>Keep at least this much path placed ahead of the runner, m.</summary>
        public float StreamAheadDistance = 220f;

        /// <summary>Plan at least this many chunks ahead of the one the runner is in (§6.2).</summary>
        public int StreamAheadChunks = 2;

        /// <summary>Retire chunks that ended this far behind the runner, m (revive and camera need some).</summary>
        public float RetireBehindDistance = 80f;

        public static List<PhaseRule> DefaultPhases()
        {
            return new List<PhaseRule>
            {
                new PhaseRule(DifficultyPhase.Learning, 0f, 1, 2, 1.20f, 0.8f, 2.0f, 3, 4, 1, 3),
                new PhaseRule(DifficultyPhase.Rhythm, 500f, 2, 4, 0.80f, 1.2f, 1.5f, 4, 5, -1, 4),
                new PhaseRule(DifficultyPhase.Decision, 1500f, 3, 5, 0.65f, 1.5f, 1.5f, 2, 3, -1, 5),
                new PhaseRule(DifficultyPhase.Challenge, 3000f, 4, 7, 0.55f, 1.8f, 1.5f, 2, 3, -1, 5),
                new PhaseRule(DifficultyPhase.Danger, 5000f, 6, 8, 0.50f, 2.0f, 1.5f, 2, 2, -1, 6),
                new PhaseRule(DifficultyPhase.Mastery, 8000f, 7, 10, 0.45f, 2.2f, 1.5f, 2, 2, -1, 6),
            };
        }

        public int PhaseIndexAt(float distance)
        {
            int index = 0;
            for (int i = 0; i < Phases.Count; i++)
            {
                if (distance >= Phases[i].StartDistance)
                {
                    index = i;
                }
            }

            return index;
        }

        public PhaseRule RuleFor(DifficultyPhase phase)
        {
            for (int i = 0; i < Phases.Count; i++)
            {
                if (Phases[i].Phase == phase)
                {
                    return Phases[i];
                }
            }

            return Phases[Phases.Count - 1];
        }

        public WorldDirectorConfig Clone()
        {
            var copy = (WorldDirectorConfig)MemberwiseClone();
            copy.Phases = new List<PhaseRule>(Phases);
            return copy;
        }
    }
}
