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

        /// <summary>
        /// The 16 FP1 chunks followed by the vine sections (GDD 7.2). This is the library the game plays;
        /// <see cref="CreateLibrary"/> stays the FP1 set so spec 002 checks keep their counts. Vine chunks are appended,
        /// so every FP1 chunk keeps its index.
        /// </summary>
        public static ChunkLibrary CreateLibraryWithVines()
        {
            ChunkData[] fp1 = CreateChunks();
            ChunkData[] vines = CreateVineChunks();
            var all = new ChunkData[fp1.Length + vines.Length];
            fp1.CopyTo(all, 0);
            vines.CopyTo(all, fp1.Length);
            return new ChunkLibrary(all);
        }

        /// <summary>
        /// The library the game plays: the 16 FP1 chunks, the vine sections, then the signature hazard chunks
        /// (GDD 8.3). Appended in that order, so every earlier chunk keeps its index.
        /// </summary>
        public static ChunkLibrary CreateGameLibrary()
        {
            ChunkData[] fp1 = CreateChunks();
            ChunkData[] vines = CreateVineChunks();
            ChunkData[] signature = CreateSignatureChunks();
            var all = new ChunkData[fp1.Length + vines.Length + signature.Length];
            fp1.CopyTo(all, 0);
            vines.CopyTo(all, fp1.Length);
            signature.CopyTo(all, fp1.Length + vines.Length);
            return new ChunkLibrary(all);
        }

        /// <summary>
        /// Signature hazard chunks (GDD 8.3), gray-box: lane denial (thorn patch across 2 lanes, forces the third) and
        /// the telegraphed lane strike (warning in a lane, then the strike). Picked by tier weight
        /// (<see cref="CreateGameTierDesigns"/>) from tier 2 (300 m, so never in the first 150 m, GDD 8.3); more
        /// often from tier 4 ("signature hazards everywhere", GDD 11.2). Fair placement: a free lane always stays
        /// open next to every hazard, coins lead into it, and consecutive hazards leave at least 16 m (≥ 0.75 s at
        /// the tier's top speed) to change lanes. Lane strikes are in every world's chunk set in the gray-box build
        /// [ASSUMED]; once worlds have skins, the Jungle keeps the thorn patch and the other worlds their strike.
        /// </summary>
        public static ChunkData[] CreateSignatureChunks()
        {
            return new[]
            {
                Signature("SG-01", "Thorn Patch", 2, "A thorn patch blocks two lanes; coins show the free one.",
                    new[] { ObstaclePlacement.LaneDenial(L01, 16f) },
                    new[] { CoinPattern.Line(2, 6f, 34f) }),
                Signature("SG-02", "Thorn Gate", 2, "Thorns force the left lane, then a log in it: jump.",
                    new[] { ObstaclePlacement.LaneDenial(L12, 8f), ObstaclePlacement.Low(L0, 28f) },
                    new[] { CoinPattern.Line(0, 2f, 20f), CoinPattern.Arc(0, 28.3f) }),
                Signature("SG-03", "Thorn Slalom", 3, "Thorns force the left lane, then the right lane.",
                    new[] { ObstaclePlacement.LaneDenial(L12, 6f), ObstaclePlacement.LaneDenial(L01, 26f) },
                    new[] { CoinPattern.Line(0, 2f, 12f), CoinPattern.Trail(0, 2, 13f, 22f), CoinPattern.Line(2, 24f, 36f) }),
                Signature("SG-04", "Falling Rocks", 2, "The middle lane flashes a warning, then rocks fall there.",
                    new[] { ObstaclePlacement.LaneStrike(1, 20f) },
                    new[] { CoinPattern.Line(0, 10f, 30f) }),
                Signature("SG-05", "Rockfall Pinch", 3, "Rocks fall on both sides, then in the middle.",
                    new[] { ObstaclePlacement.LaneStrike(0, 12f), ObstaclePlacement.LaneStrike(2, 12f), ObstaclePlacement.LaneStrike(1, 32f) },
                    new[] { CoinPattern.Line(1, 4f, 18f), CoinPattern.Trail(1, 0, 20f, 26f), CoinPattern.Line(0, 28f, 38f) }),
                Signature("SG-06", "Thorns and Rocks", 4, "Thorns force the right lane, then rocks fall in it.",
                    new[] { ObstaclePlacement.LaneDenial(L01, 8f), ObstaclePlacement.LaneStrike(2, 30f) },
                    new[] { CoinPattern.Line(2, 2f, 14f), CoinPattern.Trail(2, 1, 16f, 24f) }),
            };
        }

        /// <summary>
        /// Vine sections (GDD 7.2): a 30 m approach with coins leading into the vine lane and no obstacles, the
        /// vine row(s), then an empty landing pad long enough for the longest launch at top speed plus 1.0 s.
        /// Picked by the vine schedule (<see cref="TrackGenerator"/>), never by tier weights. Tier gating uses
        /// MinTier (GDD 11.2: chasm vines and 2-vine chains from tier 3, 3-vine chains from tier 4); chasm vines also
        /// need <c>VineConfig.ChasmVinesFromM</c>. Chasms are 18 m: longer than any plain jump at top speed (about
        /// 14.3 m with coyote time), shorter than the shortest vine release at the slowest chasm speed. They are a
        /// vine-only gap size, outside the kit's 3–4 m ravines [ASSUMED]. Chains are always over ground: an auto
        /// release only chains into safe vines (GDD 7.3 step 5), so a chasm under a chained vine could not be fair.
        /// </summary>
        public static ChunkData[] CreateVineChunks()
        {
            return new[]
            {
                VineSection("V-01", "Vine Clearing", 1, 110f, "One safe vine in the middle lane; coins lead the way.",
                    new ObstaclePlacement[0],
                    new[] { CoinPattern.Line(1, 6f, 28f), CoinPattern.Line(1, 76f, 104f) },
                    new[] { VinePlacement.Safe(1, 34f) }),
                VineSection("V-02", "Side Vine", 1, 110f, "A coin trail moves you to the side lane for a safe vine.",
                    new ObstaclePlacement[0],
                    new[] { CoinPattern.Trail(1, 0, 4f, 16f), CoinPattern.Line(0, 18f, 28f), CoinPattern.Line(0, 76f, 104f) },
                    new[] { VinePlacement.Safe(0, 34f) }),
                VineSection("V-03", "Chasm Vine", 3, 110f, "A chasm under the middle vine: grab it or fall.",
                    new[] { ObstaclePlacement.Gap(All, 31f, 18f) },
                    new[] { CoinPattern.Line(1, 6f, 26f), CoinPattern.Line(1, 76f, 104f) },
                    new[] { VinePlacement.Chasm(1, 34f) }),
                VineSection("V-04", "Chasm Side Vine", 3, 110f, "Coins lead to a side vine over a chasm.",
                    new[] { ObstaclePlacement.Gap(All, 31f, 18f) },
                    new[] { CoinPattern.Trail(1, 0, 4f, 16f), CoinPattern.Line(0, 18f, 26f), CoinPattern.Line(0, 76f, 104f) },
                    new[] { VinePlacement.Chasm(0, 34f) }),
                VineSection("V-05", "Twin Vines", 3, 150f, "Two safe vines; aim left during the first swing to chain.",
                    new ObstaclePlacement[0],
                    new[] { CoinPattern.Line(1, 6f, 28f), CoinPattern.Line(0, 120f, 146f) },
                    new[] { VinePlacement.Safe(1, 34f, 0), VinePlacement.Safe(0, 78f, 1) }),
                VineSection("V-06", "Vine Ladder", 4, 200f, "Three safe vines weaving across the lanes.",
                    new ObstaclePlacement[0],
                    new[] { CoinPattern.Line(1, 6f, 28f), CoinPattern.Line(1, 166f, 196f) },
                    new[] { VinePlacement.Safe(1, 34f, 0), VinePlacement.Safe(2, 78f, 1), VinePlacement.Safe(1, 122f, 2) }),
            };
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

        /// <summary>
        /// The tier rows the game plays with <see cref="CreateGameLibrary"/>: <see cref="CreateTierDesigns"/> plus the
        /// signature hazard chunks (rare in tiers 2–3, common from tier 4).
        /// </summary>
        public static DifficultyTierDesign[] CreateGameTierDesigns()
        {
            DifficultyTierDesign[] tiers = CreateTierDesigns();
            ChunkWeight[] tier2 = { new ChunkWeight("SG-01", 1), new ChunkWeight("SG-02", 1), new ChunkWeight("SG-04", 1) };
            ChunkWeight[] tier3 =
            {
                new ChunkWeight("SG-01", 1), new ChunkWeight("SG-02", 1), new ChunkWeight("SG-03", 2),
                new ChunkWeight("SG-04", 1), new ChunkWeight("SG-05", 2),
            };
            ChunkWeight[] tier4 =
            {
                new ChunkWeight("SG-01", 2), new ChunkWeight("SG-02", 2), new ChunkWeight("SG-03", 3),
                new ChunkWeight("SG-04", 2), new ChunkWeight("SG-05", 3), new ChunkWeight("SG-06", 3),
            };

            for (int t = 1; t < tiers.Length; t++)
            {
                ChunkWeight[] extra = t == 1 ? tier2 : (t == 2 ? tier3 : tier4);
                ChunkWeight[] old = tiers[t].Weights;
                var merged = new ChunkWeight[old.Length + extra.Length];
                old.CopyTo(merged, 0);
                extra.CopyTo(merged, old.Length);
                tiers[t].Weights = merged;
            }

            return tiers;
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

        private static ChunkData VineSection(
            string id, string name, int minTier, float lengthM, string note, ObstaclePlacement[] obstacles, CoinPattern[] coins, VinePlacement[] vines)
        {
            return new ChunkData(
                id, ChunkKind.Vine, lengthM, minTier, 6, obstacles, coins, displayName: name, designNote: note, vines: vines);
        }

        private static ChunkData Signature(string id, string name, int minTier, string note, ObstaclePlacement[] obstacles, CoinPattern[] coins)
        {
            return new ChunkData(id, ChunkKind.Signature, 40f, minTier, 6, obstacles, coins, displayName: name, designNote: note);
        }

        private static ChunkData Breather(string id, string name, string note, CoinPattern[] coins)
        {
            return new ChunkData(id, ChunkKind.Breather, 30f, 1, 6, new ObstaclePlacement[0], coins, displayName: name, designNote: note);
        }
    }
}
