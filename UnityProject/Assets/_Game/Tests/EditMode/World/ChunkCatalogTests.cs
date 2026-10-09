using System.Collections.Generic;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>Spec 102 §2, §4, §9 and spec 103 §2–3: the shipped chunk set, its layouts and the offline validator.</summary>
    public sealed class ChunkCatalogTests
    {
        private static readonly string[] MvpIds =
        {
            "F_Start_RootGate_01", "F_Straight_Glade_01", "F_Straight_Roots_01", "F_Recovery_Meadow_01", "F_Recovery_Riverbank_01",
            "F_Branch_StiltRoots_01", "F_Branch_Ravine_01", "F_Challenge_FallenGiants_01", "F_Challenge_ThornRun_01", "R_Transition_Ford_01",
            "R_Swim_Pool_01", "R_Branch_Waterfall_01", "C_Canopy_VineSpan_01", "D_Discovery_Grotto_01", "D_Discovery_Overlook_01",
        };

        [Test]
        public void Catalog_HasThe15MvpChunks_WithGroundSeams()
        {
            ChunkLibrary lib = ShippedContent.Shared.Library;
            Assert.AreEqual(15, lib.DefinitionCount);
            foreach (string id in MvpIds)
            {
                int d = lib.FindDefinition(id);
                Assert.GreaterOrEqual(d, 0, id);
                ChunkDefinition def = lib.GetDefinition(d);
                Assert.AreEqual(SeamType.Ground, def.Entry, id);
                Assert.AreEqual(SeamType.Ground, def.Exit, id);
                Assert.That(def.Length, Is.InRange(60f, 320f), id);
                Assert.AreEqual(0f, def.Length % 10f, 1e-4f, id);
                Assert.AreEqual(AbilityFlags.None, def.RequiredAbilities, id + ": MVP main lines need no ability");
            }

            Assert.AreEqual(2, CountCategory(lib, ChunkCategory.Recovery), "content rule: ≥ 2 Recovery chunks per entry type");
            Assert.IsFalse(lib.GetDefinition(lib.FindDefinition("F_Branch_Ravine_01")).PoolEnabled, "Phase 3");
            Assert.IsFalse(lib.GetDefinition(lib.FindDefinition("D_Discovery_Grotto_01")).PoolEnabled, "Phase 3");
        }

        [Test]
        public void Script_FollowsSpec103Table_And_Totals2820m()
        {
            ExpeditionContent content = ShippedContent.Shared;
            string[] expected =
            {
                "F_Start_RootGate_01/Full", "F_Straight_Roots_01/Learn", "F_Branch_StiltRoots_01/Slice", "F_Straight_Glade_01/Slice",
                "R_Transition_Ford_01/Default", "R_Swim_Pool_01/Default", "D_Discovery_Overlook_01/Default", "C_Canopy_VineSpan_01/Default",
                "R_Branch_Waterfall_01/Slice", "F_Straight_Roots_01/B", "F_Recovery_Meadow_01/Default", "F_Challenge_FallenGiants_01/Default",
                "F_Challenge_ThornRun_01/Default",
            };
            float[] starts = { 0f, 160f, 380f, 600f, 800f, 1020f, 1340f, 1520f, 1820f, 2060f, 2260f, 2380f, 2620f };
            Assert.AreEqual(expected.Length, content.Script.Entries.Count);
            float d = 0f;
            for (int i = 0; i < expected.Length; i++)
            {
                ExpeditionScriptEntry e = content.Script.Entries[i];
                Assert.AreEqual(expected[i], e.ChunkId + "/" + e.Variant);
                Assert.AreEqual(starts[i], d, 1e-3f, e.Beat);
                d += content.Library.GetEntry(content.Library.Find(e.ChunkId, e.Variant)).Length;
            }

            Assert.AreEqual(2820f, d, 1e-3f);
            Assert.AreEqual(1UL, content.Script.Seed);
            ExpeditionScriptEntry meadow = content.Script.Entries[10];
            Assert.AreEqual(PowerUpKind.Shield, meadow.PowerUp);
        }

        [Test]
        public void ScriptOnlyVariants_AreLearnAndSlice()
        {
            ChunkLibrary lib = ShippedContent.Shared.Library;
            for (int i = 0; i < lib.EntryCount; i++)
            {
                ChunkRuntime e = lib.GetEntry(i);
                bool expected = e.VariantName == "Learn" || e.VariantName == "Slice";
                Assert.AreEqual(expected, e.Variant.ScriptOnly, e.Id + "/" + e.VariantName);
            }
        }

        [Test]
        public void NoPlaceholdersLeft_SwimAndCanopyCarryTheirTraversalData()
        {
            ChunkLibrary lib = ShippedContent.Shared.Library;
            for (int i = 0; i < lib.EntryCount; i++)
            {
                ChunkRuntime e = lib.GetEntry(i);
                Assert.IsFalse(e.Placeholder, e.Id + " is still a stand-in");
            }

            ChunkRuntime swim = Entry("R_Swim_Pool_01", "Default");
            Assert.AreEqual(1, swim.WaterCount);
            Assert.AreEqual(2, swim.CurrentCount);
            Assert.AreEqual(1, swim.DeepDiveCount);
            ChunkRuntime canopy = Entry("C_Canopy_VineSpan_01", "Default");
            Assert.AreEqual(2, canopy.VineCount);
            Assert.AreEqual(1, canopy.CreatureCount);
            Assert.IsTrue(canopy.IsCanopy(180f));
            Assert.IsFalse(canopy.TryGetFloor(176f, 0f, out _), "beam gap is open air");
            Assert.IsTrue(canopy.TryGetFloor(190f, 0.8f, out float top));
            Assert.AreEqual(9f, top, 1e-3f);
        }

        [Test]
        public void C3_FirstRouteChoice_LayoutAndCrystal()
        {
            ChunkRuntime c3 = Entry("F_Branch_StiltRoots_01", "Slice");
            Assert.AreEqual(220f, c3.Length);
            c3.GetLateralBounds(100f, -3f, out float a, out float b);
            Assert.AreEqual(-4f, a, 1e-3f);
            Assert.AreEqual(-1.6f, b, 1e-3f, "risky lane right of the divider");
            c3.GetLateralBounds(100f, 2f, out a, out b);
            Assert.AreEqual(-0.4f, a, 1e-3f);
            Assert.AreEqual(4.5f, b, 1e-3f);
            Assert.AreEqual(RouteType.Risky, c3.RouteAt(100f, -3f));
            Assert.AreEqual(RouteType.Safe, c3.RouteAt(100f, 2f));
            Assert.AreEqual(RouteType.Main, c3.RouteAt(30f, -3f));
            Assert.IsTrue(c3.TryGetFloor(100f, -3f, out float ridge));
            Assert.AreEqual(1.2f, ridge, 1e-3f, "raised root ridge");
            Assert.AreEqual(1, c3.CrystalCount);
            Assert.IsTrue(c3.GetCrystal(0).Always, "script crystal");
            Assert.IsTrue(c3.GetRoute(2).Locked, "Trail Sense teaser is locked (V12)");
            Assert.Greater(CoinsBetween(c3, 75f, 160f, -4.5f, -1.6f), 50, "risky ×2.2 coins");
        }

        [Test]
        public void C9_VeilGrotto_SecretLane()
        {
            ChunkRuntime c9 = Entry("R_Branch_Waterfall_01", "Slice");
            c9.GetLateralBounds(140f, 4.8f, out float a, out float b);
            Assert.AreEqual(3.8f, a, 1e-3f);
            Assert.AreEqual(5.8f, b, 1e-3f, "secret entrance 2.0 m");
            Assert.AreEqual(RouteType.Secret, c9.RouteAt(140f, 4.8f));
            Assert.AreEqual(RouteType.Safe, c9.RouteAt(140f, 0f));
            Assert.AreEqual(RouteType.Risky, c9.RouteAt(140f, -3.4f));
            Assert.AreEqual(RouteType.Safe, c9.RouteAt(100f, 4f), "before D2 the secret lane is still the safe lane");
            Assert.AreEqual(3, c9.CrystalCount, "grotto ×2 + risky");
            Assert.AreEqual("D-03", c9.GetDiscovery(0).EntryId);
        }

        [Test]
        public void Gauntlet_C12PartialGap_And_C13Narrows()
        {
            ChunkRuntime c12 = Entry("F_Challenge_FallenGiants_01", "Default");
            Assert.IsFalse(c12.TryGetFloor(106f, -2f, out _));
            Assert.IsTrue(c12.TryGetFloor(106f, 2f, out _), "floor stays on +1.0…+3.5");
            ChunkRuntime c13 = Entry("F_Challenge_ThornRun_01", "Default");
            c13.GetOuterBounds(50f, out float a, out float b);
            Assert.AreEqual(3f, b - a, 1e-3f);
        }

        [Test]
        public void FirstExpedition_HasNoGapInTheFirst60Seconds_AtScriptedSpeed()
        {
            // AC-103-41: the first gap is C4's at 645 m; at the speed curve (0.8 s start ramp, no hits) that is > 60 s.
            ExpeditionContent content = ShippedContent.Shared;
            float firstGap = float.MaxValue;
            float start = 0f;
            foreach (ExpeditionScriptEntry e in content.Script.Entries)
            {
                ChunkRuntime c = content.Library.GetEntry(content.Library.Find(e.ChunkId, e.Variant));
                for (int i = 0; i < c.FloorCount; i++)
                {
                    if (c.GetFloor(i).Kind == CourseFloorKind.Gap)
                    {
                        firstGap = Mathf.Min(firstGap, start + c.GetFloor(i).SMin);
                    }
                }

                start += c.Length;
            }

            var curve = new SpeedCurve(ShippedAssets.Config().Speed);
            float s = 0f;
            float t = 0f;
            while (s < firstGap)
            {
                s += curve.Evaluate(s) / 60f;
                t += 1f / 60f;
            }

            Assert.AreEqual(645f, firstGap, 1e-3f);
            Assert.Greater(t, content.Script.NoGapSeconds);
        }

        [Test]
        public void AC102_01_40_ShippedCatalog_PassesTheValidator_AndMasksMatch()
        {
            ExpeditionContent content = ShippedContent.Build();
            CatalogValidation.Result result = CatalogValidation.Run(content.Library, content.Script, ShippedAssets.Config(), content.Director,
                JungleBooze.Editor.Expedition.ExpeditionSetup.CameraProfiles(), content.Pickups.CrystalPad);
            var failures = new List<string>();
            foreach (ValidationReport r in result.Reports)
            {
                if (!r.Passed)
                {
                    failures.Add(r.ToString());
                }
            }

            Debug.Log("[JungleBooze] Chunk validation: " + result.Reports.Count + " checks, " + result.BotRuns + " bot runs, failures " + failures.Count);
            CollectionAssert.IsEmpty(failures);
            for (int d = 0; d < content.Library.DefinitionCount; d++)
            {
                ChunkDefinition def = content.Library.GetDefinition(d);
                Assert.AreEqual(result.Masks[d], def.ValidatedMask, def.Id + ": shipped mask is stale (re-run JungleBooze > Expedition > Validate Chunks)");
            }
        }

        [Test]
        public void AC102_01_02_Validator_RejectsImpossibleAndBrokenChunks()
        {
            var def = new ChunkDefinition { Id = "T_Bad_01", Length = 120f, PhaseMin = DifficultyPhase.Rhythm, PhaseMax = DifficultyPhase.Rhythm };
            var v = new ChunkVariant { Name = "Bad" };
            v.Widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            v.Widths.Add(new CourseWidthKey(120f, -3.5f, 3.5f));
            v.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Blocker, SMin = 40f, SMax = 40.6f, XMin = -3.5f, XMax = 3.5f, YMin = 0f, YMax = 2.5f });
            v.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Low, SMin = 2f, SMax = 2.6f, XMin = -3.5f, XMax = 3.5f, YMin = 0f, YMax = 0.5f });
            v.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = 80f, SMax = 88f, XMin = -9f, XMax = 9f });
            def.Variants.Add(v);
            var lib = new ChunkLibrary(new[] { def });
            ValidationReport report = new ChunkValidator(ShippedAssets.Config(), new WorldDirectorConfig()).ValidatePool(lib.GetEntry(0));
            Assert.IsFalse(report.Passed);
            string all = string.Join("\n", report.Issues);
            StringAssert.Contains("V1", all, "the bot crashes into a full-width wall");
            StringAssert.Contains("V6", all, "no free corridor");
            StringAssert.Contains("V8", all, "obstacle in the seam zone");
            StringAssert.Contains("V4", all, "8 m gap");
            StringAssert.Contains("@40", all, "issues carry the s position");
        }

        private static ChunkRuntime Entry(string id, string variant)
        {
            ChunkLibrary lib = ShippedContent.Shared.Library;
            int index = lib.Find(id, variant);
            Assert.GreaterOrEqual(index, 0, id + "/" + variant);
            return lib.GetEntry(index);
        }

        private static int CountCategory(ChunkLibrary lib, ChunkCategory category)
        {
            int n = 0;
            for (int d = 0; d < lib.DefinitionCount; d++)
            {
                n += lib.GetDefinition(d).Category == category ? 1 : 0;
            }

            return n;
        }

        private static int CoinsBetween(ChunkRuntime c, float s0, float s1, float x0, float x1)
        {
            int n = 0;
            for (int i = 0; i < c.CoinCount; i++)
            {
                CoinPoint p = c.GetCoin(i);
                n += p.S >= s0 && p.S <= s1 && p.X >= x0 && p.X <= x1 ? 1 : 0;
            }

            return n;
        }
    }
}
