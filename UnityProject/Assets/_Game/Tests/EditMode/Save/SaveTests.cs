using System.IO;
using JungleBooze.Core.Save;
using JungleBooze.Services.Save;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Save
{
    /// <summary>ARCHITECTURE §9: versioned save, migrations, atomic writes, corrupt and future files.</summary>
    public sealed class SaveTests
    {
        /// <summary>A real FP1 (lane game) save, version 1 — fixture, never delete.</summary>
        private const string Fp1Fixture = "{\"version\":1,\"bestScore\":48210,\"bestDistanceM\":1630,\"totalCoins\":912,\"runsPlayed\":14,\"sessionsStarted\":3,\"freeContinueUsed\":true,\"coinsSpentOnContinues\":120,\"settings\":{\"musicVolume\":0.8,\"sfxVolume\":1.0}}";

        private static SaveData Sample()
        {
            SaveData d = SaveData.CreateDefault(123456789L, -0.3f);
            d.coins = 342;
            d.crystals = 7;
            d.bestDistance = 2410.5f;
            d.totalDistance = 9000;
            d.runsCompleted = 3;
            d.abilities = 1;
            d.pendingShowcase = 1;
            d.skill = 0.12f;
            d.journal.Add(new JournalRecord { id = "D-01", sightings = 2, firstRun = 1 });
            d.journal.Add(new JournalRecord { id = "D-03", sightings = 1, firstRun = 2 });
            return d;
        }

        private static SaveService Service(ISaveStorage storage)
        {
            return new SaveService(storage, () => SaveData.CreateDefault(1L, -0.3f));
        }

        [Test]
        public void RoundTrip_KeepsEveryField()
        {
            var storage = new MemorySaveStorage();
            SaveService save = Service(storage);
            Assert.IsTrue(save.Save(Sample()));
            SaveData back = Service(storage).Load();
            SaveData a = Sample();
            Assert.AreEqual(SaveData.CurrentVersion, back.version);
            Assert.AreEqual(a.worldSeed, back.worldSeed);
            Assert.AreEqual(a.coins, back.coins);
            Assert.AreEqual(a.crystals, back.crystals);
            Assert.AreEqual(a.bestDistance, back.bestDistance);
            Assert.AreEqual(a.totalDistance, back.totalDistance);
            Assert.AreEqual(a.runsCompleted, back.runsCompleted);
            Assert.AreEqual(a.abilities, back.abilities);
            Assert.AreEqual(a.pendingShowcase, back.pendingShowcase);
            Assert.AreEqual(a.skill, back.skill);
            Assert.AreEqual(2, back.journal.Count);
            Assert.IsTrue(back.IsDiscovered("D-03"));
            Assert.AreEqual(2, back.FindJournal("D-01").sightings);
        }

        [Test]
        public void FirstLaunch_IsNew_WithDefaults()
        {
            SaveService save = Service(new MemorySaveStorage());
            SaveData d = save.Load();
            Assert.AreEqual(SaveLoadOutcome.New, save.LastOutcome);
            Assert.AreEqual(0, d.runsCompleted);
            Assert.AreEqual(-0.3f, d.skill, 1e-6f, "new players start at S = −0.3 (spec 102 §6.4)");
        }

        [Test]
        public void Migration_V1LaneGameFixture_StartsAureliaFresh()
        {
            var storage = new MemorySaveStorage { Primary = Fp1Fixture };
            SaveService save = Service(storage);
            SaveData d = save.Load();
            Assert.AreEqual(SaveLoadOutcome.Primary, save.LastOutcome);
            Assert.AreEqual(SaveData.CurrentVersion, d.version);
            Assert.AreEqual(0, d.coins, "lane coins are not AURELIA coins");
            Assert.AreEqual(0, d.runsCompleted, "the next run is Expedition 1");
            Assert.IsTrue(save.Save(d));
            StringAssert.Contains("\"version\": 2", storage.Primary);
        }

        [Test]
        public void FutureVersion_IsReadOnly_AndNeverOverwritten()
        {
            string future = "{\"version\":99,\"coins\":500,\"crystals\":3,\"runsCompleted\":9,\"newThing\":true}";
            var storage = new MemorySaveStorage { Primary = future };
            SaveService save = Service(storage);
            SaveData d = save.Load();
            Assert.AreEqual(SaveLoadOutcome.FutureVersion, save.LastOutcome);
            Assert.IsTrue(save.ReadOnly);
            Assert.AreEqual(500, d.coins, "read as far as this build understands");
            Assert.IsFalse(save.Save(d));
            Assert.AreEqual(future, storage.Primary);
            Assert.AreEqual(0, storage.Writes);
        }

        [Test]
        public void CorruptPrimary_FallsBackToBackup_BothCorrupt_GivesDefaultsAndRecovers()
        {
            var storage = new MemorySaveStorage();
            SaveService save = Service(storage);
            save.Save(Sample());
            SaveData second = Sample();
            second.coins = 999;
            save.Save(second);
            storage.Primary = "{\"version\":2,\"coins\":";
            SaveData d = Service(storage).Load();
            Assert.AreEqual(342, d.coins, "previous good file");

            storage.Primary = "garbage";
            storage.Backup = string.Empty;
            SaveService broken = Service(storage);
            d = broken.Load();
            Assert.AreEqual(SaveLoadOutcome.Corrupt, broken.LastOutcome);
            Assert.AreEqual(0, d.coins);
            Assert.IsTrue(broken.Save(d), "a corrupt save never blocks progress");
        }

        [Test]
        public void Repair_ClampsBadValues_AndDropsBrokenJournalRows()
        {
            string json = "{\"version\":2,\"coins\":-5,\"crystals\":-1,\"bestDistance\":-3,\"skill\":7,\"journal\":[{\"id\":\"D-01\",\"sightings\":0},{\"id\":\"\"},{\"id\":\"D-01\",\"sightings\":4}]}";
            Assert.IsTrue(SaveCodec.TryDecode(json, out SaveData d, out bool future, out _));
            Assert.IsFalse(future);
            Assert.AreEqual(0, d.coins);
            Assert.AreEqual(0, d.crystals);
            Assert.AreEqual(0f, d.bestDistance);
            Assert.AreEqual(1f, d.skill);
            Assert.AreEqual(1, d.journal.Count);
            Assert.AreEqual(1, d.journal[0].sightings);
        }

        [Test]
        public void NotASave_IsRejected()
        {
            Assert.IsFalse(SaveCodec.TryDecode(string.Empty, out _, out _, out _));
            Assert.IsFalse(SaveCodec.TryDecode("{}", out _, out _, out _));
            Assert.IsFalse(SaveCodec.TryDecode("not json", out _, out _, out _));
        }

        [Test]
        public void FileStorage_AtomicWrite_KeepsBackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "jb-save-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                SaveService save = Service(storage);
                save.Save(Sample());
                SaveData next = Sample();
                next.coins = 5;
                save.Save(next);
                Assert.IsTrue(File.Exists(storage.PrimaryPath));
                Assert.IsTrue(File.Exists(storage.BackupPath));
                Assert.IsFalse(File.Exists(storage.TempPath));
                Assert.AreEqual(5, Service(new FileSaveStorage(dir)).Load().coins);
                StringAssert.Contains("\"coins\": 342", File.ReadAllText(storage.BackupPath));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}
