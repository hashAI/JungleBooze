using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.World
{
    /// <summary>
    /// A hand-authored chunk (spec 102 §2): identity, classification, difficulty, gates and its variants. Authored in
    /// a <c>ChunkDefinitionAsset</c> under <c>Assets/_Game/Config/Chunks</c>; the simulation reads only this data
    /// (scenery comes later as a prefab). <see cref="Id"/> is never renamed once shipped (analytics, saves).
    /// </summary>
    [Serializable]
    public sealed class ChunkDefinition
    {
        public string Id = "F_Straight_New_01";

        /// <summary>Short label for signs and logs (e.g. "C3").</summary>
        public string Label = string.Empty;

        public Biome Biome = Biome.VerdantForest;
        public EnvironmentSet Environment = EnvironmentSet.Forest;
        public ChunkCategory Category = ChunkCategory.Straight;

        /// <summary>m, 60–320, multiple of 10.</summary>
        public float Length = 200f;

        public SeamType Entry = SeamType.Ground;
        public SeamType Exit = SeamType.Ground;

        /// <summary>Authored rating 1–10 and the range it spans across its phases (spec 102 §9).</summary>
        public int Rating = 2;

        public int RatingMin = 1;
        public int RatingMax = 3;

        public ChunkDimensions Dimensions;

        /// <summary>Phases where the director may pick it; the validator checks both speed extremes of this range.</summary>
        public DifficultyPhase PhaseMin = DifficultyPhase.Learning;

        public DifficultyPhase PhaseMax = DifficultyPhase.Mastery;

        /// <summary>Needed to pass the main line (MVP: always none).</summary>
        public AbilityFlags RequiredAbilities = AbilityFlags.None;

        /// <summary>Abilities this chunk shows off (a locked zone or branch that needs them): the Showcase rule.</summary>
        public AbilityFlags ShowcaseAbilities = AbilityFlags.None;

        public RewardProfile Reward = RewardProfile.Standard;

        /// <summary>Allowed to break the environment-repetition rule R3.</summary>
        public bool SetPiece;

        /// <summary>Only used to open runs (<c>F_Start_RootGate_01</c>); never picked by weight.</summary>
        public bool RunOpener;

        /// <summary>In the director's pool (Phase 3 chunks are authored but kept out of the Phase 2 pool, spec 103 §10.2).</summary>
        public bool PoolEnabled = true;

        /// <summary>Base selection weight (spec 102 §6.3 step 3).</summary>
        public float BaseWeight = 1f;

        /// <summary>Post-MVP event flags.</summary>
        public int EventCompatibility;

        public List<ChunkVariant> Variants = new List<ChunkVariant>();

        /// <summary>
        /// Bit per variant: passed the offline validator (spec 102 §4.1). Written by the editor validator; the
        /// director only picks validated variants.
        /// </summary>
        public int ValidatedMask;

        public int FindVariant(string name)
        {
            for (int i = 0; i < Variants.Count; i++)
            {
                if (Variants[i].Name == name)
                {
                    return i;
                }
            }

            return -1;
        }

        public float LengthOf(int variant)
        {
            float length = variant >= 0 && variant < Variants.Count ? Variants[variant].Length : 0f;
            return length > 0f ? length : Length;
        }
    }
}
