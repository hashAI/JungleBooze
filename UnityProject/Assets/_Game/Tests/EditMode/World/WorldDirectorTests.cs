using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>Spec 102 §5–8 and spec 103 §10: the World Director (AC-102-03 … 11, AC-103-39, 42, 43).</summary>
    public sealed class WorldDirectorTests
    {
        private const int FuzzSeeds = 10000;
        private const int PicksPerRun = 60;

        private static WorldDirector Director(ExpeditionContent content = null, WorldDirectorConfig config = null)
        {
            content = content ?? ShippedContent.Shared;
            return new WorldDirector(content.Library, config ?? content.Director, ShippedAssets.Config().Speed, 1f / 60f, true);
        }

        private static List<ChunkPick> Plan(WorldDirector director, in DirectorRunSetup setup, int count, out List<float> starts)
        {
            director.BeginRun(setup);
            var picks = new List<ChunkPick>(count);
            starts = new List<float>(count);
            float s = 0f;
            for (int i = 0; i < count; i++)
            {
                ChunkPick pick = director.PlanNext(s);
                picks.Add(pick);
                starts.Add(s);
                s += director.Library.GetEntry(pick.Entry).Length;
            }

            return picks;
        }

        [Test]
        public void AC102_05_06_11_Fuzz10000Seeds_NeverBreaksRules_NeverImpossible()
        {
            ExpeditionContent content = ShippedContent.Shared;
            WorldDirector director = Director(content);
            WorldDirectorConfig cfg = content.Director;
            var speed = new SpeedCurve(ShippedAssets.Config().Speed);
            var rng = new Pcg32Random(424242UL);
            int[] perPhase = new int[6];
            int fallback = 0;
            int forks = 0;
            int maxSinceRecovery = 0;
            var violations = new List<string>();
            for (int seed = 0; seed < FuzzSeeds && violations.Count < 20; seed++)
            {
                AbilityFlags owned = rng.Chance(0.5f) ? AbilityFlags.DeepBreath : AbilityFlags.None;
                var setup = new DirectorRunSetup
                {
                    Seed = (ulong)seed + 1000UL,
                    ShortStart = true,
                    Owned = owned,
                    PendingShowcase = owned != AbilityFlags.None && rng.Chance(0.5f) ? AbilityFlags.DeepBreath : AbilityFlags.None,
                    Skill = rng.NextFloat(-1f, 1f),
                };
                director.BeginRun(setup);
                var history = new List<ChunkRuntime>();
                float s = 0f;
                int sinceRecovery = 0;
                for (int i = 0; i < PicksPerRun; i++)
                {
                    if (rng.Chance(0.05f))
                    {
                        director.OnHit(i * 600L);
                        director.OnHit((i * 600L) + 120L);
                    }

                    ChunkPick pick = director.PlanNext(s);
                    ChunkRuntime c = content.Library.GetEntry(pick.Entry);
                    ChunkDefinition d = c.Definition;
                    PhaseRule rule = cfg.Phases[cfg.PhaseIndexAt(s)];
                    string where = "seed " + seed + " pick " + i + " " + d.Id + " @" + s.ToString("0");
                    if (i == 0)
                    {
                        if (!d.RunOpener || c.VariantName != "Short")
                        {
                            violations.Add(where + ": run must open with the Short start");
                        }
                    }
                    else
                    {
                        perPhase[(int)rule.Phase]++;
                        Check(violations, where, !d.RunOpener && d.PoolEnabled, "opener or non-pool chunk picked");
                        Check(violations, where, !c.Variant.ScriptOnly, "script-only variant picked");
                        Check(violations, where, (d.ValidatedMask & (1 << c.VariantIndex)) != 0, "unvalidated variant");
                        Check(violations, where, rule.Phase >= d.PhaseMin && rule.Phase <= d.PhaseMax, "outside its phase range");
                        Check(violations, where, (d.RequiredAbilities & ~owned) == 0, "needs an unowned ability (AC-102-06)");
                        Check(violations, where, pick.Reason != PickReason.Emergency, "emergency pick (content gap)");
                        fallback += pick.RelaxLevel > 0 ? 1 : 0;
                        forks += d.Category == ChunkCategory.Branch ? 1 : 0;

                        // V11 seam rule.
                        ChunkRuntime prev = history[history.Count - 1];
                        float gap = (prev.ExitMarginM + c.EntryMarginM) / speed.Evaluate(s);
                        Check(violations, where, gap + 1e-4f >= rule.MinActionGap, "V11 seam gap " + gap.ToString("0.00") + " s");

                        // R1: no id within the last 6.
                        for (int k = 1; k <= cfg.NoRepeatWindow && k <= history.Count; k++)
                        {
                            Check(violations, where, history[history.Count - k].Definition != d, "R1 repeat");
                        }

                        // R2: category.
                        if (d.Category == ChunkCategory.Recovery)
                        {
                            Check(violations, where, prev.Definition.Category != ChunkCategory.Recovery, "R2 Recovery twice");
                        }
                        else if (history.Count >= 2)
                        {
                            bool same = history[history.Count - 1].Definition.Category == d.Category && history[history.Count - 2].Definition.Category == d.Category;
                            Check(violations, where, !same, "R2 category 3 in a row");
                        }

                        // R3: environment (set pieces exempt).
                        if (!d.SetPiece && history.Count >= 3)
                        {
                            bool same = true;
                            for (int k = 1; k <= 3; k++)
                            {
                                same &= history[history.Count - k].Definition.Environment == d.Environment;
                            }

                            Check(violations, where, !same, "R3 environment 4 in a row");
                        }

                        // R4: same variant signature not within the last 3 of its family.
                        for (int k = 1; k <= cfg.VariantSignatureWindow && k <= history.Count; k++)
                        {
                            Check(violations, where, history[history.Count - k] != c, "R4 variant signature");
                        }

                        // R5 / AC-102-07: Recovery cadence.
                        sinceRecovery = d.Category == ChunkCategory.Recovery ? 0 : sinceRecovery + 1;
                        maxSinceRecovery = Mathf.Max(maxSinceRecovery, sinceRecovery);
                        Check(violations, where, sinceRecovery <= 7, "R5 no Recovery for " + sinceRecovery + " picks");
                    }

                    history.Add(c);
                    s += c.Length;
                }
            }

            Debug.Log("[JungleBooze] Director fuzz: " + FuzzSeeds + " seeds × " + PicksPerRun + " picks; per phase " + string.Join("/", perPhase) +
                      "; fallback picks " + fallback + "; forks " + forks + "; longest run without Recovery " + maxSinceRecovery);
            CollectionAssert.IsEmpty(violations);
            for (int p = 0; p < 6; p++)
            {
                Assert.Greater(perPhase[p], 10000, "≥ 10,000 generated chunks in phase " + (DifficultyPhase)p + " (AC-102-05)");
            }
        }

        [Test]
        public void AC102_03_SameSeedSameInputs_SamePicks_DifferentSeedsDiffer()
        {
            var setup = new DirectorRunSetup { Seed = 77UL, ShortStart = true, Skill = 0.1f };
            List<ChunkPick> a = Plan(Director(), setup, 40, out _);
            List<ChunkPick> b = Plan(Director(), setup, 40, out _);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Entry, b[i].Entry, "pick " + i);
                Assert.AreEqual(a[i].CrystalMask, b[i].CrystalMask);
                Assert.AreEqual(a[i].FlowCrystalCoin, b[i].FlowCrystalCoin);
                Assert.AreEqual(a[i].PowerUpSlot, b[i].PowerUpSlot);
            }

            setup.Seed = 78UL;
            List<ChunkPick> c = Plan(Director(), setup, 40, out _);
            bool differs = false;
            for (int i = 0; i < a.Count; i++)
            {
                differs |= a[i].Entry != c[i].Entry;
            }

            Assert.IsTrue(differs);
        }

        [Test]
        public void AC102_04_PickupRandomnessDoesNotChangeTheChunkSequence()
        {
            ExpeditionContent content = ShippedContent.Build();
            WorldDirectorConfig noFlow = content.Director.Clone();
            noFlow.FlowCrystalChanceLength = 1e9f;
            noFlow.RiskyCrystalChance = 1f;
            var setup = new DirectorRunSetup { Seed = 5UL, ShortStart = true };
            List<ChunkPick> a = Plan(Director(content), setup, 40, out _);
            List<ChunkPick> b = Plan(Director(content, noFlow), setup, 40, out _);
            bool pickupsDiffer = false;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Entry, b[i].Entry, "chunk sequence must not depend on the Pickups stream");
                pickupsDiffer |= a[i].FlowCrystalCoin != b[i].FlowCrystalCoin || a[i].CrystalMask != b[i].CrystalMask;
            }

            Assert.IsTrue(pickupsDiffer, "the pickup draws did change");
        }

        [Test]
        public void AC103_39_42_Expedition1_ExactSequence_ThenForcedRecovery_ThenPoolOnly()
        {
            ExpeditionContent content = ShippedContent.Shared;
            var setup = new DirectorRunSetup { Script = content.Script, Seed = 999UL };
            List<ChunkPick> picks = Plan(Director(), setup, 30, out List<float> starts);
            for (int i = 0; i < content.Script.Entries.Count; i++)
            {
                ExpeditionScriptEntry e = content.Script.Entries[i];
                ChunkRuntime c = content.Library.GetEntry(picks[i].Entry);
                Assert.AreEqual(e.ChunkId + "/" + e.Variant, c.Id + "/" + c.VariantName);
                Assert.AreEqual(PickReason.Script, picks[i].Reason);
            }

            Assert.AreEqual(2820f, starts[13], 1e-3f);
            ChunkRuntime after = content.Library.GetEntry(picks[13].Entry);
            Assert.AreEqual(ChunkCategory.Recovery, after.Definition.Category, "AC-103-42");
            Assert.AreEqual("F_Recovery_Riverbank_01", after.Id, "the Meadow was C11, so R1 leaves the Riverbank");
            for (int i = 13; i < picks.Count; i++)
            {
                Assert.IsFalse(content.Library.GetEntry(picks[i].Entry).Variant.ScriptOnly);
            }

            // The script's seed is fixed: the run seed does not matter.
            setup.Seed = 1UL;
            List<ChunkPick> other = Plan(Director(), setup, 30, out _);
            for (int i = 0; i < picks.Count; i++)
            {
                Assert.AreEqual(picks[i].Entry, other[i].Entry);
            }
        }

        [Test]
        public void AC103_43_Showcase_DeepBreathWithinFivePicks_1000Seeds()
        {
            ExpeditionContent content = ShippedContent.Shared;
            WorldDirector director = Director();
            int swim = content.Library.FindDefinition("R_Swim_Pool_01");
            for (int seed = 0; seed < 1000; seed++)
            {
                var setup = new DirectorRunSetup { Seed = (ulong)seed, ShortStart = true, Owned = AbilityFlags.DeepBreath, PendingShowcase = AbilityFlags.DeepBreath, Skill = -0.3f };
                List<ChunkPick> picks = Plan(director, setup, 6, out _);
                int found = -1;
                for (int i = 1; i <= 5; i++)
                {
                    if (content.Library.GetEntry(picks[i].Entry).DefinitionIndex == swim)
                    {
                        found = i;
                        break;
                    }
                }

                Assert.GreaterOrEqual(found, 1, "seed " + seed);
                Assert.AreEqual(AbilityFlags.DeepBreath, director.ShowcasedAbilities);
            }

            // Once shown (not pending any more), never forced again.
            for (int seed = 0; seed < 200; seed++)
            {
                var setup = new DirectorRunSetup { Seed = (ulong)seed, ShortStart = true, Owned = AbilityFlags.DeepBreath, Skill = -0.3f };
                List<ChunkPick> picks = Plan(director, setup, 20, out _);
                foreach (ChunkPick p in picks)
                {
                    Assert.AreNotEqual(PickReason.Showcase, p.Reason);
                }
            }
        }

        [Test]
        public void AC102_07_Mercy_TwoHitsIn15s_NextPlaceableRecovery()
        {
            // Mercy forces the next unplanned pick to be a Recovery. When R1/R3 block both Recovery chunks (e.g. both
            // within the last 6), the easiest chunk goes first and the Recovery follows as soon as the rules allow it.
            ExpeditionContent content = ShippedContent.Shared;
            WorldDirector director = Director();
            int immediate = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                director.BeginRun(new DirectorRunSetup { Seed = (ulong)seed, ShortStart = true });
                var history = new List<ChunkDefinition>();
                float s = 0f;
                int warmup = 3 + (seed % 12);
                for (int i = 0; i < warmup; i++)
                {
                    ChunkPick p = director.PlanNext(s);
                    history.Add(content.Library.GetEntry(p.Entry).Definition);
                    s += content.Library.GetEntry(p.Entry).Length;
                }

                director.OnHit(100000);
                director.OnHit(100000 + (14 * 60));
                bool placed = false;
                for (int k = 0; k < 8 && !placed; k++)
                {
                    bool allowed = history[history.Count - 1].Category != ChunkCategory.Recovery && RecoveryFree(content, history);
                    ChunkPick p = director.PlanNext(s);
                    ChunkDefinition def = content.Library.GetEntry(p.Entry).Definition;
                    if (allowed)
                    {
                        Assert.AreEqual(ChunkCategory.Recovery, def.Category, "seed " + seed + " pick " + k);
                        immediate += k == 0 ? 1 : 0;
                        placed = true;
                    }

                    placed |= def.Category == ChunkCategory.Recovery;
                    history.Add(def);
                    s += content.Library.GetEntry(p.Entry).Length;
                }

                Assert.IsTrue(placed, "seed " + seed);
            }

            Assert.Greater(immediate, 150, "usually the very next pick");

            // Two hits 16 s apart are not mercy.
            director.BeginRun(new DirectorRunSetup { Seed = 3UL, ShortStart = true });
            director.PlanNext(0f);
            director.OnHit(100);
            director.OnHit(100 + (16 * 60));
            Assert.AreNotEqual(PickReason.Mercy, director.PlanNext(60f).Reason);
        }

        private static bool RecoveryFree(ExpeditionContent content, List<ChunkDefinition> history)
        {
            for (int d = 0; d < content.Library.DefinitionCount; d++)
            {
                ChunkDefinition def = content.Library.GetDefinition(d);
                if (def.Category != ChunkCategory.Recovery)
                {
                    continue;
                }

                bool recent = false;
                for (int k = 1; k <= 6 && k <= history.Count; k++)
                {
                    recent |= history[history.Count - k] == def;
                }

                // R3: a fourth chunk of the same environment in a row is not allowed either.
                bool sameEnvironment = history.Count >= 3;
                for (int k = 1; k <= 3 && k <= history.Count; k++)
                {
                    sameEnvironment &= history[history.Count - k].Environment == def.Environment;
                }

                if (!recent && !sameEnvironment)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void AC102_09_SkillUpdate_FollowsTheFormula_AndIsClamped()
        {
            WorldDirectorConfig cfg = new WorldDirectorConfig();
            float run = DifficultyModel.RunScore(cfg, 1500f, 0, 0, 0);
            Assert.AreEqual(0.5f * 0f + 0.3f * 1f, run, 1e-4f, "1,500 m, no hits: distScore 0, hitScore 1");
            Assert.AreEqual(0.5f * 1f + 0.3f * 1f, DifficultyModel.RunScore(cfg, 6000f, 0, 0, 0), 1e-4f, "log4(4) = 1");
            Assert.AreEqual((0.5f * -1f) + (0.3f * -1f), DifficultyModel.RunScore(cfg, 300f, 3, 0, 0), 1e-4f, "short run, 10 hits/km → both −1");
            Assert.AreEqual((0.5f * 0f) + (0.3f * 1f) + (0.2f * 1f), DifficultyModel.RunScore(cfg, 1500f, 0, 10, 10), 1e-4f);

            float s = -0.3f;
            float next = DifficultyModel.UpdateSkill(cfg, s, 0.3f);
            Assert.AreEqual(-0.3f + 0.18f, next, 1e-4f);
            Assert.AreEqual(-0.3f + 0.2f, DifficultyModel.UpdateSkill(cfg, s, 1f), 1e-4f, "|ΔS| ≤ 0.2");
            Assert.AreEqual(-0.3f - 0.2f, DifficultyModel.UpdateSkill(cfg, s, -1f), 1e-4f);

            Assert.AreEqual(-1, DifficultyModel.BandShift(cfg, -0.6f));
            Assert.AreEqual(0, DifficultyModel.BandShift(cfg, 0f));
            Assert.AreEqual(1, DifficultyModel.BandShift(cfg, 0.6f));
            Assert.AreEqual(1.30f, DifficultyModel.CueIntensity(cfg, -0.6f), 1e-4f);
            Assert.AreEqual(0.85f, DifficultyModel.CueIntensity(cfg, 0.6f), 1e-4f);
            Assert.AreEqual(4, DifficultyModel.RecoveryEvery(cfg, -0.6f, 5));
            Assert.AreEqual(3, DifficultyModel.RecoveryEvery(cfg, -0.6f, 3));
            Assert.AreEqual(6, DifficultyModel.RecoveryEvery(cfg, 0.6f, 5));
        }

        [Test]
        public void AC102_09_DdaNeverChangesSpeed()
        {
            // Two directed runs at opposite skill: the simulation's speed at the same distance is identical.
            ExpeditionSession low = ShippedContent.Session();
            ExpeditionSession high = ShippedContent.Session();
            low.BeginRun(ShippedContent.Directed(11UL, skill: -1f));
            high.BeginRun(ShippedContent.Directed(11UL, skill: 1f));
            var botLow = ShippedContent.Bot(low);
            var botHigh = ShippedContent.Bot(high);
            for (int i = 0; i < 1200; i++)
            {
                low.Step(botLow.ReadInput(i));
                high.Step(botHigh.ReadInput(i));
            }

            Assert.AreEqual(0, low.Simulation.State.Hits);
            Assert.AreEqual(0, high.Simulation.State.Hits);
            var curve = new SpeedCurve(ShippedAssets.Config().Speed);
            Assert.AreEqual(curve.Evaluate(low.Simulation.State.Distance), low.Simulation.State.Speed, 1e-3f);
            Assert.AreEqual(curve.Evaluate(high.Simulation.State.Distance), high.Simulation.State.Speed, 1e-3f);
        }

        private static void Check(List<string> violations, string where, bool ok, string rule)
        {
            if (!ok && violations.Count < 20)
            {
                violations.Add(where + ": " + rule);
            }
        }
    }
}
