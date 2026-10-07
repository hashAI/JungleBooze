using System;
using System.Collections.Generic;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Track
{
    /// <summary>Spec 002 section 3: config loading and validation (AC-201) and derived tier values (AC-202).</summary>
    public sealed class TrackConfigTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject so in _created)
            {
                UnityEngine.Object.DestroyImmediate(so);
            }

            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            T so = ScriptableObject.CreateInstance<T>();
            _created.Add(so);
            return so;
        }

        // ---- AC-201 ----

        [Test]
        public void AC201_EveryConfigAssetLoadsWithStartValuesAndPassesValidation()
        {
            var errors = new List<string>();
            Assert.IsTrue(Create<TrackConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<ObstacleKitConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<CoinConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<ScoreConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<RunFlowConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<DifficultyTiersConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<TrackPresentationConfigAsset>().Validate(errors), string.Join(" ", errors));

            TrackConfig track = Create<TrackConfigAsset>().ToConfig();
            Assert.AreEqual(150f, track.GenerateAheadM);
            Assert.AreEqual(15f, track.DespawnBehindM);
            Assert.AreEqual(3, track.NoRepeatWindow);
            Assert.AreEqual("S-01", track.StartChunkId);

            ObstacleKitConfig kit = Create<ObstacleKitConfigAsset>().ToConfig();
            Assert.AreEqual(1.4f, kit.MoverTriggerLeadS);
            CoinConfig coins = Create<CoinConfigAsset>().ToConfig();
            Assert.AreEqual(25, coins.StreakLength);
            ScoreConfig score = Create<ScoreConfigAsset>().ToConfig();
            Assert.AreEqual(20, score.NearMissBonus);
            RunFlowConfig flow = Create<RunFlowConfigAsset>().ToConfig();
            Assert.AreEqual(0.4, flow.GameOverInputLockSeconds, 1e-9);

            ChunkLibrary library = Create<ChunkLibraryConfigAsset>().ToLibrary();
            Assert.AreEqual(16, library.Count);
            Assert.IsTrue(library.SeamTableIsCurrent);
            DifficultyTiersConfig tiers = Create<DifficultyTiersConfigAsset>().ToConfig(SpeedCurve.CreateDefault(), library);
            Assert.AreEqual(6, tiers.TierCount);
        }

        [Test]
        public void AC201_ChunkAssetRoundTripsTheLibraryChunks()
        {
            ChunkData[] chunks = JungleChunkLibraryDefaults.CreateChunks();
            Assert.AreEqual(16, chunks.Length);
            foreach (ChunkData chunk in chunks)
            {
                ChunkAsset asset = Create<ChunkAsset>();
                asset.SetFrom(chunk);
                Assert.AreEqual(chunk.ComputeHash(), asset.ToChunkData().ComputeHash(), chunk.Id);
            }
        }

        [Test]
        public void AC201_LibraryCountsMatchSpecSection7()
        {
            ChunkLibrary library = JungleChunkLibraryDefaults.CreateLibrary();
            int start = 0, normal = 0, breather = 0;
            for (int i = 0; i < library.Count; i++)
            {
                switch (library[i].Kind)
                {
                    case ChunkKind.Start:
                        start++;
                        break;
                    case ChunkKind.Normal:
                        normal++;
                        Assert.AreEqual(40f, library[i].LengthM, library[i].Id);
                        break;
                    case ChunkKind.Breather:
                        breather++;
                        Assert.AreEqual(30f, library[i].LengthM, library[i].Id);
                        break;
                }
            }

            Assert.AreEqual(1, start);
            Assert.AreEqual(13, normal);
            Assert.AreEqual(2, breather);
            Assert.AreEqual(0, library.IndexOf("S-01"));
            Assert.GreaterOrEqual(library.IndexOf("B-01"), 0);
        }

        [Test]
        public void AC201_BreatherMaxBelowMin_IsRejected()
        {
            TrackDesignValues v = TrackDesignValues.CreateDefault();
            v.BreatherIntervalMaxS = 20f;
            v.BreatherIntervalMinS = 25f;
            var errors = new List<string>();
            Assert.IsFalse(v.Validate(errors));
            Assert.Throws<ArgumentException>(() => TrackConfig.FromDesignValues(v));
        }

        [TestCase("generateAheadM")]
        [TestCase("despawnBehindM")]
        [TestCase("noRepeatWindow")]
        [TestCase("mirrorChance")]
        [TestCase("maxActiveCoins")]
        public void AC201_OutOfRangeTrackValues_AreRejected(string field)
        {
            TrackDesignValues v = TrackDesignValues.CreateDefault();
            switch (field)
            {
                case "generateAheadM":
                    v.GenerateAheadM = 99f;
                    break;
                case "despawnBehindM":
                    v.DespawnBehindM = 41f;
                    break;
                case "noRepeatWindow":
                    v.NoRepeatWindow = 7;
                    break;
                case "mirrorChance":
                    v.MirrorChance = 1.5f;
                    break;
                case "maxActiveCoins":
                    v.MaxActiveCoins = 10;
                    break;
            }

            var errors = new List<string>();
            Assert.IsFalse(v.Validate(errors));
            StringAssert.Contains(field, string.Join(" ", errors));
        }

        [Test]
        public void AC201_OutOfRangeKitCoinAndScoreValues_AreRejected()
        {
            ObstacleKitDesignValues kit = ObstacleKitDesignValues.CreateDefault();
            kit.MoverTriggerLeadS = 1.0f;
            Assert.Throws<ArgumentException>(() => ObstacleKitConfig.FromDesignValues(kit));

            CoinDesignValues coins = CoinDesignValues.CreateDefault();
            coins.ArcCoinCount = 6;
            Assert.Throws<ArgumentException>(() => CoinConfig.FromDesignValues(coins));

            ScoreDesignValues score = ScoreDesignValues.CreateDefault();
            score.ScoreMultiplier = 0;
            Assert.Throws<ArgumentException>(() => ScoreConfig.FromDesignValues(score));
        }

        [Test]
        public void AC201_UnsortedTierFrom_IsRejected()
        {
            DifficultyTiersDesignValues v = DifficultyTiersDesignValues.CreateDefault();
            v.Tiers[2].FromM = 250f;
            var errors = new List<string>();
            Assert.IsFalse(v.Validate(errors));
            Assert.Throws<ArgumentException>(
                () => DifficultyTiersConfig.FromDesignValues(v, SpeedCurve.CreateDefault(), JungleChunkLibraryDefaults.CreateLibrary()));
        }

        [Test]
        public void AC201_TierSpikeRule_TiersCloserThan200m_AreRejected()
        {
            DifficultyTiersDesignValues v = DifficultyTiersDesignValues.CreateDefault();
            v.Tiers[1].FromM = 150f;
            var errors = new List<string>();
            Assert.IsFalse(v.Validate(errors));
        }

        [Test]
        public void AC201_WeightForChunkOutsideItsTierRange_IsRejected()
        {
            DifficultyTiersDesignValues v = DifficultyTiersDesignValues.CreateDefault();
            ChunkWeight[] w = v.Tiers[0].Weights;
            Array.Resize(ref w, w.Length + 1);
            w[w.Length - 1] = new ChunkWeight("T3-01", 1);
            v.Tiers[0].Weights = w;
            var errors = new List<string>();
            Assert.IsNull(DifficultyTiersConfig.Build(v, SpeedCurve.CreateDefault(), JungleChunkLibraryDefaults.CreateLibrary(), errors));
            StringAssert.Contains("T3-01", string.Join(" ", errors));
        }

        [Test]
        public void AC201_EmptyPool_IsRejected()
        {
            DifficultyTiersDesignValues v = DifficultyTiersDesignValues.CreateDefault();
            v.Tiers[1].Weights = new ChunkWeight[0];
            var errors = new List<string>();
            Assert.IsNull(DifficultyTiersConfig.Build(v, SpeedCurve.CreateDefault(), JungleChunkLibraryDefaults.CreateLibrary(), errors));
            StringAssert.Contains("empty", string.Join(" ", errors));
        }

        [Test]
        public void AC201_StaleSeamTableHash_IsReported()
        {
            ChunkLibrary library = JungleChunkLibraryDefaults.CreateLibrary();
            Assert.IsTrue(library.WithSeams(new SeamTable(library.Count, library.DataHash)).SeamTableIsCurrent);
            Assert.IsFalse(library.WithSeams(new SeamTable(library.Count, library.DataHash + 1)).SeamTableIsCurrent);
        }

        [Test]
        public void AC201_FogStartDifferentFromRunnerPresentation_IsRejected()
        {
            TrackPresentationConfig p = TrackPresentationConfig.CreateDefault();
            var errors = new List<string>();
            Assert.IsTrue(p.Validate(errors, 45f), string.Join(" ", errors));
            Assert.IsFalse(p.Validate(errors, 40f));
            StringAssert.Contains("fogStartM", string.Join(" ", errors));
        }

        [Test]
        public void AC201_SetupRejectsLibraryWithoutStartChunk()
        {
            TrackDesignValues v = TrackDesignValues.CreateDefault();
            v.StartChunkId = "NOPE";
            Assert.Throws<ArgumentException>(() => new TrackRunSetup(
                TrackConfig.FromDesignValues(v),
                ObstacleKitConfig.CreateDefault(),
                CoinConfig.CreateDefault(),
                ScoreConfig.CreateDefault(),
                JungleChunkLibraryDefaults.CreateLibrary(),
                DifficultyTiersDesignValues.CreateDefault()));
        }

        // ---- AC-202 ----

        [Test]
        public void AC202_DerivedTierValues_MatchSpec()
        {
            DifficultyTiersConfig tiers = JungleChunkLibraryDefaults.CreateTiers(SpeedCurve.CreateDefault(), JungleChunkLibraryDefaults.CreateLibrary());
            double[] vMax = { 11.20, 12.25, 14.17, 16.58, 19.00, 21.00 };
            double[] spacing = { 10.08, 9.19, 9.21, 9.12, 9.50, 9.45 };
            Assert.AreEqual(6, tiers.TierCount);
            for (int t = 0; t < 6; t++)
            {
                Assert.AreEqual(vMax[t], tiers.GetTier(t).VMaxMps, 0.01, "vMaxMps tier " + (t + 1));
                Assert.AreEqual(spacing[t], tiers.GetTier(t).MinRowSpacingM, 0.01, "minRowSpacingM tier " + (t + 1));
            }
        }
    }
}
