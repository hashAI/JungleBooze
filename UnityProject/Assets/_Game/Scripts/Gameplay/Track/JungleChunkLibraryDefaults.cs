using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// The FP1 chunk library (spec 002 section 7.2) and tier weights (section 7.3) as code. There is no Unity
    /// editor in the build environment to author <c>.asset</c> files, so this is the default; a
    /// <c>ChunkLibraryConfigAsset</c> with chunk assets overrides it once authored [ASSUMED].
    /// <b>Order is part of determinism</b> (section 4.3): append new chunks at the end, never reorder.
    /// Notation: <c>Low[lanes]@zc</c> etc.; lines and trails use the default 2 m spacing.
    /// </summary>
    public static class JungleChunkLibraryDefaults
    {
        public const string StartId = "S-01";
        public const string FallbackBreatherId = "B-01";

        private const byte L0 = LaneMasks.Lane0;
        private const byte L1 = LaneMasks.Lane1;
        private const byte L2 = LaneMasks.Lane2;
        private const byte L01 = LaneMasks.Lane0 | LaneMasks.Lane1;
        private const byte L12 = LaneMasks.Lane1 | LaneMasks.Lane2;
        private const byte All = LaneMasks.All;

        /// <summary>Builds the 16-chunk library (no seam table: every pair compatible until stage B3 builds one).</summary>
        public static ChunkLibrary CreateLibrary()
        {
            return new ChunkLibrary(CreateChunks());
        }

        /// <summary>The 16 chunks in library order.</summary>
        public static ChunkData[] CreateChunks()
        {
            return new[]
            {
                Start(),
                Normal("T1-01", "Log Step", 1, "One log in the middle lane; coins show the free side lanes.",
                    new[] { ObstaclePlacement.Low(L1, 16f) },
                    new[] { CoinPattern.Line(0, 6f, 20f), CoinPattern.Line(2, 26f, 36f) }),
                Normal("T1-02", "Low Branch", 1, "One branch in the middle lane; slide under or step aside.",
                    new[] { ObstaclePlacement.High(L1, 16f) },
                    new[] { CoinPattern.Line(2, 4f, 16f), CoinPattern.Line(0, 22f, 34f) }),
                Normal("T1-03", "Trunk Gate", 1, "Two trunks in the side lanes; the middle stays open.",
                    new[] { ObstaclePlacement.Full(L0, 8f), ObstaclePlacement.Full(L2, 22f) },
                    new[] { CoinPattern.Line(1, 4f, 32f) }),
                Normal("T1-04", "Log, then Branch", 1, "A log right, then a branch left; the middle line is safe.",
                    new[] { ObstaclePlacement.Low(L2, 8f), ObstaclePlacement.High(L0, 22f) },
                    new[] { CoinPattern.Line(1, 4f, 18f), CoinPattern.Line(2, 24f, 34f) }),
                Normal("T1-05", "Short Ravine", 1, "A short full-width ravine teaches the jump.",
                    new[] { ObstaclePlacement.Gap(All, 16f, 3.0f) },
                    new[] { CoinPattern.Line(1, 4f, 12f), CoinPattern.Line(1, 22f, 36f) }),
                Normal("T2-01", "Thorn Wall", 2, "Two-lane wall, then a log under an arc, then a two-lane branch.",
                    new[] { ObstaclePlacement.Full(L01, 8f), ObstaclePlacement.Low(L2, 20f), ObstaclePlacement.High(L12, 30f) },
                    new[] { CoinPattern.Line(2, 2f, 14f), CoinPattern.Arc(2, 20.3f) }),
                Normal("T2-02", "Switchback", 2, "Two staggered two-lane walls force a weave; a full-width log ends it.",
                    new[] { ObstaclePlacement.Full(L12, 6f), ObstaclePlacement.Full(L01, 18f), ObstaclePlacement.Low(All, 30f) },
                    new[] { CoinPattern.Trail(0, 2, 10f, 18f), CoinPattern.Arc(2, 30.3f) }),
                Normal("T2-03", "Ravine Run", 2, "A long ravine with an arc, then a branch and a log.",
                    new[] { ObstaclePlacement.Gap(All, 8f, 4.0f), ObstaclePlacement.High(L01, 22f), ObstaclePlacement.Low(L2, 32f) },
                    new[] { CoinPattern.Arc(1, 10.0f), CoinPattern.Line(2, 16f, 26f) }),

                // Spec 002 writes the last row as High[0,2]@28. Lane masks must be contiguous (section 4.1), so it
                // is authored as two placements in one row, like the two trunks of T3-03.
                Normal("T2-04", "Branch Tunnel", 2, "Branches overhead: slide, dodge the trunk, slide in the middle.",
                    new[]
                    {
                        ObstaclePlacement.High(All, 8f),
                        ObstaclePlacement.Full(L1, 18f),
                        ObstaclePlacement.High(L0, 28f),
                        ObstaclePlacement.High(L2, 28f),
                    },
                    new[] { CoinPattern.Line(1, 2f, 10f), CoinPattern.Line(0, 16f, 30f) }),
                Normal("T3-01", "Boulder Roll", 3, "The boulder leaves the middle lane; coins reward staying.",
                    new[] { ObstaclePlacement.Mover(1, 0, 10f), ObstaclePlacement.Low(L12, 21f), ObstaclePlacement.High(L0, 31f) },
                    new[] { CoinPattern.Line(1, 2f, 6f), CoinPattern.Line(1, 13f, 17f), CoinPattern.Arc(1, 21.3f) }),
                Normal("T3-02", "Boulder Cross", 3, "A trunk left, then a boulder rolling in from the right.",
                    new[] { ObstaclePlacement.Full(L0, 6f), ObstaclePlacement.Mover(2, 1, 16f), ObstaclePlacement.Low(L01, 26f) },
                    new[] { CoinPattern.Line(2, 2f, 12f), CoinPattern.Line(2, 20f, 32f) }),
                Normal("T3-03", "Pinch", 3, "Two trunks pinch the middle; the boulder then rolls right.",
                    new[]
                    {
                        ObstaclePlacement.Full(L0, 8f),
                        ObstaclePlacement.Full(L2, 8f),
                        ObstaclePlacement.Mover(1, 2, 19f),
                        ObstaclePlacement.Low(L01, 29f),
                    },
                    new[] { CoinPattern.Line(1, 4f, 14f), CoinPattern.Arc(1, 29.3f) }),
                Normal("T3-04", "Ravine Boulder", 3, "A two-lane ravine, a boulder rolling in, a full-width branch.",
                    new[] { ObstaclePlacement.Gap(L01, 6f, 3.0f), ObstaclePlacement.Mover(2, 1, 17f), ObstaclePlacement.High(All, 28f) },
                    new[] { CoinPattern.Arc(0, 7.5f), CoinPattern.Line(0, 16f, 28f) }),
                Breather("B-01", "Coin Straight", "A straight line of coins to breathe.",
                    new[] { CoinPattern.Line(1, 2f, 28f) }),
                Breather("B-02", "Coin Weave", "Coins weave across the lanes.",
                    new[] { CoinPattern.Trail(1, 0, 2f, 10f), CoinPattern.Trail(0, 2, 12f, 22f), CoinPattern.Trail(2, 1, 24f, 30f) }),
            };
        }

        /// <summary>Spec 002 section 3.3 rows with the section 7.3 weights (tiers 4–6 copy tier 3 in FP1).</summary>
        public static DifficultyTierDesign[] CreateTierDesigns()
        {
            ChunkWeight[] tier1 =
            {
                new ChunkWeight("T1-01", 2), new ChunkWeight("T1-02", 2), new ChunkWeight("T1-03", 3),
                new ChunkWeight("T1-04", 3), new ChunkWeight("T1-05", 2),
            };

            ChunkWeight[] tier2 =
            {
                new ChunkWeight("T1-01", 1), new ChunkWeight("T1-02", 1), new ChunkWeight("T1-03", 1),
                new ChunkWeight("T1-04", 1), new ChunkWeight("T1-05", 1),
                new ChunkWeight("T2-01", 3), new ChunkWeight("T2-02", 3), new ChunkWeight("T2-03", 3),
                new ChunkWeight("T2-04", 3),
            };

            return new[]
            {
                new DifficultyTierDesign(0f, 4f, 0.90f, tier1),
                new DifficultyTierDesign(300f, 6f, 0.75f, tier2),
                new DifficultyTierDesign(600f, 7f, 0.65f, Tier3Weights()),
                new DifficultyTierDesign(1500f, 8f, 0.55f, Tier3Weights()),
                new DifficultyTierDesign(3000f, 9f, 0.50f, Tier3Weights()),
                new DifficultyTierDesign(5000f, 10f, 0.45f, Tier3Weights()),
            };
        }

        /// <summary>Tier configuration for the default library and the given speed curve.</summary>
        public static DifficultyTiersConfig CreateTiers(SpeedCurve curve, ChunkLibrary library)
        {
            return DifficultyTiersConfig.FromDesignValues(DifficultyTiersDesignValues.CreateDefault(), curve, library);
        }

        private static ChunkWeight[] Tier3Weights()
        {
            return new[]
            {
                new ChunkWeight("T1-01", 1), new ChunkWeight("T1-02", 1), new ChunkWeight("T1-03", 1),
                new ChunkWeight("T1-04", 1), new ChunkWeight("T1-05", 1),
                new ChunkWeight("T2-01", 2), new ChunkWeight("T2-02", 2), new ChunkWeight("T2-03", 2),
                new ChunkWeight("T2-04", 2),
                new ChunkWeight("T3-01", 4), new ChunkWeight("T3-02", 4), new ChunkWeight("T3-03", 4),
                new ChunkWeight("T3-04", 4),
            };
        }

        private static ChunkData Start()
        {
            return new ChunkData(
                StartId,
                ChunkKind.Start,
                50f,
                1,
                6,
                new ObstaclePlacement[0],
                new[] { CoinPattern.Line(1, 20f, 48f) },
                allowMirror: false,
                displayName: "Trailhead",
                designNote: "Open path to get running; a coin line in the middle.");
        }

        private static ChunkData Normal(string id, string name, int minTier, string note, ObstaclePlacement[] obstacles, CoinPattern[] coins)
        {
            return new ChunkData(id, ChunkKind.Normal, 40f, minTier, 6, obstacles, coins, displayName: name, designNote: note);
        }

        private static ChunkData Breather(string id, string name, string note, CoinPattern[] coins)
        {
            return new ChunkData(id, ChunkKind.Breather, 30f, 1, 6, new ObstaclePlacement[0], coins, displayName: name, designNote: note);
        }
    }
}
