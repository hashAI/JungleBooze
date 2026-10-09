using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.World;
using JungleBooze.Services.Save;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>Spec 103 §9 and GDD §17: results, the next objective, LEARN, NEW RECORD (AC-103-46 … 48).</summary>
    public sealed class ProgressionTests
    {
        private static SaveData NewProfile()
        {
            return SaveData.CreateDefault(42L, -0.3f);
        }

        private static RunStats Stats(float distance, int coins, int crystals = 0, int hits = 0)
        {
            var s = new RunStats();
            s.Distance = distance;
            s.Coins = coins;
            s.Crystals = crystals;
            s.Hits = hits;
            return s;
        }

        private static int Index(string id)
        {
            return ShippedContent.Shared.FindDiscovery(id);
        }

        [Test]
        public void AC103_46_Run1_With150Coins_DeepBreathReady()
        {
            SaveData p = NewProfile();
            RunResults r = ProgressionRules.ApplyRun(p, Stats(2410f, 160), ShippedContent.Shared, true, AbilityFlags.None);
            Assert.AreEqual(ObjectiveKind.AbilityReady, r.Objective.Kind);
            Assert.AreEqual(AbilityFlags.DeepBreath, r.Objective.Ability);
            StringAssert.StartsWith("Deep Breath ready · 150/150", r.Objective.Title);
            Assert.AreEqual("Dive into the Sunken Arch", r.Objective.Detail);
            Assert.AreEqual(160, p.coins);
            Assert.AreEqual(1, p.runsCompleted);
        }

        [TestCase(75)]
        [TestCase(110)]
        [TestCase(149)]
        public void AC103_46_Run1_With75To149Coins_ProgressLine(int coins)
        {
            SaveData p = NewProfile();
            RunResults r = ProgressionRules.ApplyRun(p, Stats(900f, coins), ShippedContent.Shared, true, AbilityFlags.None);
            Assert.AreEqual(ObjectiveKind.AbilityProgress, r.Objective.Kind);
            Assert.AreEqual("Deep Breath " + coins + "/150", r.Objective.Title);
        }

        [Test]
        public void AC103_46_SecretHint_OnlyWhenD03MissedAndNothingElseMatches()
        {
            SaveData p = NewProfile();
            RunStats s = Stats(2000f, 40);
            s.MissedDiscoveries = 1 << Index("D-03");
            RunResults r = ProgressionRules.ApplyRun(p, s, ShippedContent.Shared, true, AbilityFlags.None);
            Assert.AreEqual(ObjectiveKind.Secrets, r.Objective.Kind);
            Assert.AreEqual("Something glinted behind Veil Falls", r.Objective.Title);

            // Higher priorities win: enough coins → Deep Breath, even with the grotto missed.
            SaveData rich = NewProfile();
            RunStats s2 = Stats(2000f, 200);
            s2.MissedDiscoveries = 1 << Index("D-03");
            Assert.AreEqual(ObjectiveKind.AbilityReady, ProgressionRules.ApplyRun(rich, s2, ShippedContent.Shared, true, AbilityFlags.None).Objective.Kind);

            // Found: no grotto line.
            SaveData found = NewProfile();
            found.journal.Add(new JournalRecord { id = "D-03", sightings = 1 });
            RunStats s3 = Stats(2000f, 40);
            Assert.AreNotEqual("Something glinted behind Veil Falls", ProgressionRules.ApplyRun(found, s3, ShippedContent.Shared, true, AbilityFlags.None).Objective.Title);
        }

        [Test]
        public void Priority2_NearBest_And_Priority4_UnseenCreature()
        {
            SaveData p = NewProfile();
            p.runsCompleted = 3;
            p.bestDistance = 5000f;
            p.abilities = (int)AbilityFlags.DeepBreath;
            RunResults r = ProgressionRules.ApplyRun(p, Stats(4870f, 10), ShippedContent.Shared, false, AbilityFlags.None);
            Assert.AreEqual(ObjectiveKind.NearBest, r.Objective.Kind);
            Assert.AreEqual("4,870 m / 5,000 m", r.Objective.Title);

            SaveData q = NewProfile();
            q.runsCompleted = 3;
            q.bestDistance = 9000f;
            q.abilities = (int)AbilityFlags.DeepBreath;
            RunStats s = Stats(2000f, 10);
            s.MissedDiscoveries = 1 << Index("D-02");
            Assert.AreEqual(ObjectiveKind.UnseenEntry, ProgressionRules.ApplyRun(q, s, ShippedContent.Shared, false, AbilityFlags.None).Objective.Kind);
        }

        [Test]
        public void AC103_47_Learn_DeductsOnce_Persists_DisabledBelowCost()
        {
            ExpeditionContent content = ShippedContent.Shared;
            AbilityDefinition deepBreath = content.FindAbility(AbilityFlags.DeepBreath);
            SaveData p = NewProfile();
            p.coins = 149;
            Assert.IsFalse(ProgressionRules.CanAfford(p, deepBreath));
            Assert.IsFalse(ProgressionRules.TryLearn(p, deepBreath));
            Assert.AreEqual(149, p.coins);

            p.coins = 160;
            Assert.IsTrue(ProgressionRules.TryLearn(p, deepBreath));
            Assert.AreEqual(10, p.coins);
            Assert.IsTrue(ProgressionRules.Owns(p, AbilityFlags.DeepBreath));
            Assert.AreEqual((int)AbilityFlags.DeepBreath, p.pendingShowcase, "Showcase in the next run");
            Assert.IsFalse(ProgressionRules.TryLearn(p, deepBreath), "only once");
            Assert.AreEqual(10, p.coins);

            var storage = new MemorySaveStorage();
            var save = new SaveService(storage, () => SaveData.CreateDefault(1L, -0.3f));
            save.Save(p);
            SaveData restarted = new SaveService(storage, () => SaveData.CreateDefault(1L, -0.3f)).Load();
            Assert.IsTrue(ProgressionRules.Owns(restarted, AbilityFlags.DeepBreath), "persists across app restarts");
            Assert.AreEqual(10, restarted.coins);

            AbilityDefinition vineGrip = content.FindAbility(AbilityFlags.VineGrip);
            restarted.coins = 10000;
            restarted.crystals = 100;
            Assert.IsFalse(ProgressionRules.TryLearn(restarted, vineGrip), "not playable in the slice");
        }

        [Test]
        public void AC103_48_NewRecord_OnlyFromRun2_WhenBeaten()
        {
            SaveData p = NewProfile();
            RunResults r1 = ProgressionRules.ApplyRun(p, Stats(2410f, 0), ShippedContent.Shared, true, AbilityFlags.None);
            Assert.IsFalse(r1.NewRecord);
            Assert.AreEqual(2410f, r1.Best);
            Assert.IsTrue(r1.FirstExpedition);

            RunResults r2 = ProgressionRules.ApplyRun(p, Stats(2000f, 0), ShippedContent.Shared, false, AbilityFlags.None);
            Assert.IsFalse(r2.NewRecord);
            Assert.AreEqual(2410f, r2.Best);

            RunResults r3 = ProgressionRules.ApplyRun(p, Stats(2500f, 0), ShippedContent.Shared, false, AbilityFlags.None);
            Assert.IsTrue(r3.NewRecord);
            Assert.AreEqual(2500f, p.bestDistance);
        }

        [Test]
        public void ApplyRun_BanksWallet_Journal_Skill_AndClearsShowcase()
        {
            ExpeditionContent content = ShippedContent.Shared;
            SaveData p = NewProfile();
            p.pendingShowcase = (int)AbilityFlags.DeepBreath;
            RunStats s = Stats(3000f, 200, 3);
            s.CleanLineCoins = 25;
            s.DiscoveryCoins = 50;
            s.DiscoveryCrystals = 2;
            s.AddNewDiscovery(Index("D-01"));
            RunResults r = ProgressionRules.ApplyRun(p, s, content, false, AbilityFlags.DeepBreath);
            Assert.AreEqual(275, p.coins);
            Assert.AreEqual(5, p.crystals);
            Assert.AreEqual(275, r.TotalCoins);
            Assert.IsTrue(p.IsDiscovered("D-01"));
            Assert.AreEqual(0, p.pendingShowcase);
            Assert.AreEqual(3000, p.totalDistance);
            Assert.AreNotEqual(-0.3f, p.skill);
            CollectionAssert.AreEqual(new[] { "Falls Basin" }, r.NewDiscoveryNames);
            StringAssert.Contains("Locations 1/4", r.CategoryCounts);
        }

        [Test]
        public void AC103_31_ToastQueue_QueuesWithGap_NeverBlocks()
        {
            var q = new ToastQueue(8, 2f, 0.5f);
            q.Enqueue(0);
            Assert.AreEqual(0, q.Current);
            q.Update(1f);
            q.Enqueue(1);
            Assert.AreEqual(0, q.Current, "second waits");
            q.Update(1f);
            Assert.AreEqual(-1, q.Current, "first ended at 2.0 s");
            q.Update(0.4f);
            Assert.AreEqual(-1, q.Current);
            q.Update(0.1f);
            Assert.AreEqual(1, q.Current, "shows 0.5 s after the first ends");
            q.Update(2f);
            Assert.AreEqual(-1, q.Current);
            Assert.AreEqual(0, q.Pending);
        }

        [Test]
        public void RunSeed_IsStable_AndDiffersPerRun()
        {
            Assert.AreEqual(ExpeditionRunSetup.RunSeed(42L, 3), ExpeditionRunSetup.RunSeed(42L, 3));
            Assert.AreNotEqual(ExpeditionRunSetup.RunSeed(42L, 3), ExpeditionRunSetup.RunSeed(42L, 4));
            Assert.AreNotEqual(ExpeditionRunSetup.RunSeed(42L, 3), ExpeditionRunSetup.RunSeed(43L, 3));
        }
    }
}
