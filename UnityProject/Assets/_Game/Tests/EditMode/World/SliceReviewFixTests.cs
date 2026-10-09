using System;
using System.Collections.Generic;
using System.IO;
using JungleBooze.Core;
using JungleBooze.Core.Save;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Analytics;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.Services.Analytics;
using JungleBooze.Services.Save;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>
    /// Fixes from the vertical slice review (docs/reviews/2026-10-09-vertical-slice.md): B1 replay format 3, S1
    /// showcase on arrival, S2 sailback group slots, S3/S12 save failures, S4 revive fairness + V15, S5 V7 run-in,
    /// S6 director last resort, S7/S8/N3/N4/N11 analytics, S9 sightings, S11 V14 worst case, N1/N2.
    /// </summary>
    public sealed class SliceReviewFixTests
    {
        private sealed class Sink : ICreatureDiscoverySink
        {
            public readonly List<int> Serials = new List<int>();

            public void OnCreatureObserved(string entryId, int chunkSerial, long tick)
            {
                Serials.Add(chunkSerial);
            }
        }

        private static SaveService Service(MemorySaveStorage storage, Func<double> clock = null)
        {
            return new SaveService(storage, () => SaveData.CreateDefault(1L, -0.3f), clock);
        }

        private static void StepUntil(ExpeditionSession session, IInputProvider input, Func<bool> stop, int maxTicks = 60 * 60 * 10)
        {
            for (int t = 0; t < maxTicks && !stop(); t++)
            {
                session.Step(input != null ? input.ReadInput(session.Run.SessionTick) : InputFrame.Empty);
                session.Events.Clear();
            }
        }

        // ---- B1: replay format 3 ----

        [Test]
        public void B1_Run2WithRevive_RecordsSetupAndRevive_AndReplaysBitExactly()
        {
            ExpeditionContent content = ShippedContent.Shared;
            ulong seed = ExpeditionRunSetup.RunSeed(987654321L, 1);
            var discovered = new HashSet<string> { "D-01" };
            var setup = new ExpeditionRunSetup
            {
                FirstExpedition = false,
                Seed = seed,
                Owned = AbilityFlags.DeepBreath,
                PendingShowcase = AbilityFlags.DeepBreath,
                Skill = 0.17f,
                Discovered = discovered.Contains,
            };

            SaveData profile = SaveData.CreateDefault(987654321L, 0.17f);
            profile.runsCompleted = 1;
            profile.crystals = 5;
            ExpeditionSession live = ShippedContent.Session();
            live.Recording = new InputRecording(0UL, 42UL, "test", 4096);
            live.BeginRun(setup);
            PerfectBot bot = ShippedContent.Bot(live, RouteType.Safe);

            // Bot to 700 m, then no input until she dies, a paid revive, then the bot again to 1400 m, then no input.
            StepUntil(live, bot, () => live.Simulation.State.Distance > 700f);
            StepUntil(live, null, () => live.Simulation.State.Dead);
            Assert.IsTrue(live.Simulation.State.Dead, "dies without input");
            Assert.IsTrue(ReviveRules.TryRevive(live, content.Results, profile));
            Assert.AreEqual(RunPhase.Ready, live.Phase, "revive ready beat");
            bot.Reset();
            StepUntil(live, bot, () => live.Simulation.State.Distance > 1400f || live.Phase == RunPhase.Results);
            StepUntil(live, null, () => live.Phase == RunPhase.Results);
            Assert.AreEqual(RunPhase.Results, live.Phase);

            InputRecording recorded = live.Recording;
            Assert.AreEqual(seed, recorded.Seed, "the run seed, not 0");
            Assert.AreEqual(1, recorded.MarkerCount);
            Assert.AreEqual(ReplayMarkerKind.Revive, recorded.GetMarker(0).Kind);
            Assert.AreEqual((int)AbilityFlags.DeepBreath, recorded.Setup.Owned);
            Assert.AreEqual((int)AbilityFlags.DeepBreath, recorded.Setup.PendingShowcase);
            Assert.AreEqual(0.17f, recorded.Setup.Skill);
            CollectionAssert.AreEqual(new[] { "D-01" }, recorded.Setup.Discovered);

            var stream = new MemoryStream();
            InputRecordingSerializer.Write(recorded, stream);
            stream.Position = 0;
            InputRecording loaded = InputRecordingSerializer.Read(stream);

            ExpeditionSession replay = ShippedContent.Session();
            ExpeditionReplay.Play(replay, loaded);
            Assert.AreEqual(RunPhase.Results, replay.Phase);
            Assert.AreEqual(live.Simulation.State, replay.Simulation.State, "bit-exact final state");
            Assert.AreEqual(live.Director.Serial, replay.Director.Serial);
            Assert.AreEqual(live.Stats.TotalCoins, replay.Stats.TotalCoins);
            Assert.AreEqual(live.Stats.TotalCrystals, replay.Stats.TotalCrystals);
            Assert.AreEqual(live.Stats.Revives, replay.Stats.Revives);
            Assert.AreEqual(1, replay.Stats.Revives);
            Assert.AreEqual(live.Stats.NewDiscoveryCount, replay.Stats.NewDiscoveryCount);
            Assert.AreEqual(live.Stats.Sightings, replay.Stats.Sightings);
            for (int serial = live.Path.FirstChunkSerial; serial < live.Path.NextChunkSerial; serial++)
            {
                Assert.AreEqual(live.Path.Chunk(serial).Chunk.Id, replay.Path.Chunk(serial).Chunk.Id, "chunk " + serial);
            }
        }

        // ---- S1: showcase spent only when reached ----

        [Test]
        public void S1_DieBeforeTheShowcaseChunk_StaysPending_AndIsForcedAgainNextRun()
        {
            ExpeditionContent content = ShippedContent.Shared;
            SaveData profile = SaveData.CreateDefault(31L, -0.3f);
            profile.runsCompleted = 1;
            profile.abilities = (int)AbilityFlags.DeepBreath;
            profile.pendingShowcase = (int)AbilityFlags.DeepBreath;
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.Directed(707UL, AbilityFlags.DeepBreath, showcase: AbilityFlags.DeepBreath));
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Safe);
            float showcaseStart = float.NaN;
            StepUntil(session, bot, () =>
            {
                for (int serial = session.Path.FirstChunkSerial; serial < session.Path.NextChunkSerial; serial++)
                {
                    if (session.Path.Chunk(serial).Pick.Reason == PickReason.Showcase)
                    {
                        showcaseStart = session.Path.Chunk(serial).StartS;
                        return true;
                    }
                }

                return false;
            });
            Assert.IsFalse(float.IsNaN(showcaseStart), "the pool is planned ahead");
            Assert.Less(session.Simulation.State.S, showcaseStart, "the runner is still before it");
            Assert.AreEqual(AbilityFlags.DeepBreath, session.Director.ShowcasedAbilities, "planned (director bookkeeping)");
            Assert.AreEqual(AbilityFlags.None, session.Stats.ShowcaseReached, "but never reached");

            // The run ends here (the stats are what a death at this point banks): the showcase stays owed.
            ProgressionRules.ApplyRun(profile, session.Stats, content, false, session.Stats.ShowcaseReached);
            Assert.AreEqual((int)AbilityFlags.DeepBreath, profile.pendingShowcase, "still owed");

            // Run 3 with the bot: the pool is forced again and reached; then the bit clears.
            session.BeginRun(ShippedContent.Directed(708UL, AbilityFlags.DeepBreath, showcase: (AbilityFlags)profile.pendingShowcase));
            StepUntil(session, ShippedContent.Bot(session, RouteType.Safe), () => session.Stats.ShowcaseReached != AbilityFlags.None || session.Phase == RunPhase.Results);
            Assert.AreEqual(AbilityFlags.DeepBreath, session.Stats.ShowcaseReached);
            ProgressionRules.ApplyRun(profile, session.Stats, content, false, session.Stats.ShowcaseReached);
            Assert.AreEqual(0, profile.pendingShowcase);
        }

        // ---- S2: sailback group slots ----

        [Test]
        public void S2_CanopyAtN_WaterfallAtNPlus2_BothFlocksStillCount()
        {
            ChunkLibrary library = ShippedContent.Shared.Library;
            var path = new WorldPath();
            int[] entries =
            {
                library.Find(SliceChunkLayouts.Canopy, "Default"),
                library.Find(SliceChunkLayouts.Meadow, "Default"),
                library.Find(SliceChunkLayouts.Waterfall, "A"),
            };
            foreach (int e in entries)
            {
                Assert.GreaterOrEqual(e, 0);
                path.Append(library.GetEntry(e), new ChunkPick { Entry = e, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            }

            var sink = new Sink();
            var creatures = new SailbackSystem(path, ShippedContent.Shared.Sailback, 1f / 60f, new RunEventBuffer(512)) { Sink = sink };
            float s = 0f;
            float end = path.Chunk(2).StartS + 200f;
            for (long t = 1; s < end; t++)
            {
                s += 12f / 60f;
                creatures.Step(new RunnerState { Tick = t, S = s, X = 0f, Speed = 12f });
            }

            CollectionAssert.Contains(sink.Serials, 0, "the canopy flock (chunk n) is observed although chunk n + 2 spawned");
            CollectionAssert.Contains(sink.Serials, 2, "and the waterfall's (chunk n + 2)");
        }

        // ---- S3 / S12: save failures ----

        [Test]
        public void S3_SaveFailure_NeverThrows_RetriesWithBackoff()
        {
            var storage = new MemorySaveStorage { FailWith = () => new IOException("disk full") };
            double now = 0.0;
            SaveService save = Service(storage, () => now);
            SaveData data = SaveData.CreateDefault(5L, 0f);
            Assert.IsFalse(save.Save(data));
            Assert.IsTrue(save.HasPendingSave);
            StringAssert.Contains("disk full", save.LastError);
            Assert.IsFalse(save.RetryPending(data, false), "back-off 2 s");
            now = 1.9;
            Assert.IsFalse(save.RetryPending(data, false));
            now = 2.1;
            Assert.IsFalse(save.RetryPending(data, false), "still failing → back-off 4 s");
            Assert.AreEqual(2, save.Failures);
            storage.FailWith = null;
            now = 3.0;
            Assert.IsFalse(save.RetryPending(data, false), "waits for the back-off");
            Assert.IsTrue(save.RetryPending(data, true), "a safe point forces it");
            Assert.IsFalse(save.HasPendingSave);
            Assert.IsNotNull(storage.Primary);
        }

        [Test]
        public void S3_FailingStorage_ResultsStillShow_LearnIsAtomic_AndPersistsLater()
        {
            ExpeditionContent content = ShippedContent.Shared;
            var storage = new MemorySaveStorage { FailWith = () => new UnauthorizedAccessException("denied") };
            SaveService save = Service(storage);
            SaveData profile = SaveData.CreateDefault(77L, -0.3f);
            profile.runsCompleted = 1;
            profile.coins = 400;
            ExpeditionSession session = ShippedContent.Session();
            var log = new LocalAnalyticsLog(null);
            var flow = new ExpeditionFlow(content, session, save, profile, new AnalyticsRecorder(content), log);
            flow.BeginRun(flow.NextRunSetup(), true);
            StepUntil(session, null, () => session.Phase == RunPhase.Results);

            RunResults results = flow.FinishRun();
            Assert.IsNotNull(results, "results show although the save failed");
            Assert.IsFalse(flow.LastSaveOk);
            Assert.IsTrue(save.HasPendingSave);
            Assert.AreEqual(2, profile.runsCompleted);

            AbilityDefinition deepBreath = content.FindAbility(AbilityFlags.DeepBreath);
            int coins = profile.coins;
            Assert.IsTrue(flow.Learn(deepBreath), "LEARN completes in memory");
            Assert.AreEqual(coins - deepBreath.CostCoins, profile.coins);
            Assert.IsTrue(ProgressionRules.Owns(profile, AbilityFlags.DeepBreath), "never coins without the unlock");
            Assert.IsNull(storage.Primary, "nothing reached storage yet");

            storage.FailWith = null;
            Assert.IsTrue(flow.SafePoint(true));
            SaveData back = Service(storage).Load();
            Assert.AreEqual(profile.coins, back.coins);
            Assert.AreEqual(profile.abilities, back.abilities);
            Assert.AreEqual(2, back.runsCompleted);
            StringAssert.Contains("\"event\":\"run_ended\"", string.Join("\n", log.Lines));
        }

        [Test]
        public void S12_KilledBetweenRenames_PromotesTmp_CorruptFilesAreKept()
        {
            string dir = Path.Combine(Path.GetTempPath(), "jb-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(dir);
                SaveService save = new SaveService(storage, () => SaveData.CreateDefault(1L, 0f));
                SaveData old = SaveData.CreateDefault(1L, 0f);
                old.coins = 10;
                Assert.IsTrue(save.Save(old));
                SaveData newer = SaveData.CreateDefault(1L, 0f);
                newer.coins = 99;
                Assert.IsTrue(save.Save(newer));

                // Half-finished replace: primary renamed to .bak, the complete new file still in .tmp.
                File.Copy(storage.PrimaryPath, storage.TempPath, true);
                File.Delete(storage.PrimaryPath);
                SaveData loaded = new SaveService(storage, () => SaveData.CreateDefault(1L, 0f)).Load();
                Assert.AreEqual(99, loaded.coins, "the newest save survives");
                Assert.IsTrue(File.Exists(storage.PrimaryPath), "and is promoted to the main file");

                // Both unreadable: copies are kept for diagnosis before anything replaces them.
                File.WriteAllText(storage.PrimaryPath, "{garbage");
                File.WriteAllText(storage.BackupPath, "also garbage");
                var fresh = new SaveService(storage, () => SaveData.CreateDefault(1L, 0f));
                fresh.Load();
                Assert.AreEqual(SaveLoadOutcome.Corrupt, fresh.LastOutcome);
                Assert.IsTrue(File.Exists(Path.Combine(dir, "save.corrupt-0.json")));
                Assert.IsTrue(File.Exists(Path.Combine(dir, "save.corrupt-1.json")));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        // ---- S4: revive fairness ----

        [Test]
        public void S4_ReviveAtEveryGap_ClearRunIn_ReadyBeat_BotWithReactionDelayPasses()
        {
            MovementConfig config = ShippedAssets.Config();
            ChunkLibrary library = ShippedContent.Shared.Library;
            var tested = new List<string>();
            var failures = new List<string>();
            const float speed = 15f;
            float runIn = RunnerSimulation.ReviveRunIn(config, speed);
            for (int e = 0; e < library.EntryCount; e++)
            {
                ChunkRuntime chunk = library.GetEntry(e);
                List<CourseFloorPatch> gaps = ChunkValidator.EffectiveGaps(chunk);
                foreach (CourseFloorPatch gap in gaps)
                {
                    if (ChunkValidator.IsVineGap(chunk, gap) || gap.SMin < 40f)
                    {
                        continue;
                    }

                    var path = new WorldPath();
                    path.Append(chunk, new ChunkPick { Entry = e, CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
                    var sim = new RunnerSimulation(config, path, 1f / 60f, new RunEventBuffer(64)) { MuteEvents = true };
                    sim.Reset(new RunOptions { ForcedSpeed = speed, SkipStartRamp = true, StartS = gap.SMin - 40f, StartX = (gap.XMin + gap.XMax) * 0.5f > 3f ? 3f : (gap.XMin + gap.XMax) * 0.5f < -3f ? -3f : (gap.XMin + gap.XMax) * 0.5f });
                    var bot = new PerfectBot(sim, false);

                    // The bot runs up, but its jumps are dropped near the gap: she falls in.
                    for (int t = 0; t < 60 * 10 && !sim.State.Dead && sim.State.S < gap.SMax + 2f; t++)
                    {
                        InputFrame f = bot.ReadInput(sim.State.Tick);
                        bool near = sim.State.S > gap.SMin - 12f;
                        sim.Step(near ? new InputFrame(InputCommand.None, 0) : f);
                    }

                    if (!sim.State.Dead || sim.State.Cause != DeathCause.Fall)
                    {
                        continue;
                    }

                    string label = chunk.Id + "/" + chunk.VariantName + " gap " + gap.SMin.ToString("0.#");
                    tested.Add(label);
                    Assert.IsTrue(sim.Revive(), label);
                    float revivedAt = sim.State.S;
                    if (gap.SMin - revivedAt + 0.01f < runIn)
                    {
                        failures.Add(label + ": revived " + (gap.SMin - revivedAt).ToString("0.0") + " m before the lip (< " + runIn.ToString("0.0") + ")");
                        continue;
                    }

                    // 0.15 s reaction after the ready beat, then the Perfect bot.
                    bot.Reset();
                    for (int t = 0; t < 60 * 8 && !sim.State.Dead && sim.State.S < gap.SMax + 3f; t++)
                    {
                        InputFrame f = bot.ReadInput(sim.State.Tick);
                        sim.Step(t < 9 ? InputFrame.Empty : f);
                    }

                    if (sim.State.Dead || sim.State.S < gap.SMax || sim.State.FallRescues > 0)
                    {
                        failures.Add(label + ": " + (sim.State.Dead ? "died again (" + sim.State.Cause + ")" : sim.State.FallRescues > 0 ? "needed the fall rescue" : "did not pass"));
                    }
                }
            }

            UnityEngine.Debug.Log("[JungleBooze] Revive fairness: " + tested.Count + " gaps: " + string.Join(", ", tested));
            Assert.GreaterOrEqual(tested.Count, 8, "the slice's ground and beam gaps");
            CollectionAssert.IsEmpty(failures);
        }

        [Test]
        public void S4_RevivedRunner_FallDuringIFrames_IsCaught_AfterwardsFallsKill()
        {
            var b = new ChunkLayoutBuilder("TwoGaps", 200f);
            b.Gap(60f, 63f).Gap(140f, 143f);
            var def = new ChunkDefinition { Id = "T_Gaps_01", Length = 200f };
            def.Variants.Add(b.Variant);
            var path = new WorldPath();
            path.Append(new ChunkRuntime(def, 0, 0, 0), new ChunkPick { CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var sim = new RunnerSimulation(ShippedAssets.Config(), path, 1f / 60f, new RunEventBuffer(256));
            sim.Reset(new RunOptions { ForcedSpeed = 12f, SkipStartRamp = true, StartS = 10f });
            for (int t = 0; t < 600 && !sim.State.Dead; t++)
            {
                sim.Step(InputFrame.Empty);
            }

            Assert.AreEqual(DeathCause.Fall, sim.State.Cause);
            Assert.IsTrue(sim.Revive());
            Assert.LessOrEqual(sim.State.S, 60f - RunnerSimulation.ReviveRunIn(ShippedAssets.Config(), 12f) + 0.01f, "clear run-in");
            for (int t = 0; t < 240 && sim.State.S < 70f && !sim.State.Dead; t++)
            {
                sim.Step(InputFrame.Empty);
            }

            Assert.IsFalse(sim.State.Dead, "a fall inside the revive i-frames is caught");
            Assert.AreEqual(1, sim.State.FallRescues);
            Assert.GreaterOrEqual(sim.State.S, 63f, "set on the far lip");
            for (int t = 0; t < 900 && !sim.State.Dead; t++)
            {
                sim.Step(InputFrame.Empty);
            }

            Assert.IsTrue(sim.State.Dead, "after the i-frames a fall kills again");
        }

        [Test]
        public void S4_ReviveOnlyWhileDying_N2()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.Directed(55UL));
            StepUntil(session, null, () => session.Phase == RunPhase.Results);
            Assert.IsFalse(session.Revive(1), "not once the results show (the run is banked)");
        }

        [Test]
        public void V15_GapsTooCloseForARevive_Flagged()
        {
            var b = new ChunkLayoutBuilder("V15", 200f);
            b.Gap(60f, 63f).Arc(61.5f).Gap(72f, 75f).Arc(73.5f);
            string issues = string.Join("\n", ValidatePool(b, 200f).Issues);
            StringAssert.Contains("V15", issues);

            var ok = new ChunkLayoutBuilder("V15ok", 200f);
            ok.Gap(60f, 63f).Arc(61.5f).Gap(110f, 113f).Arc(111.5f);
            StringAssert.DoesNotContain("V15", string.Join("\n", ValidatePool(ok, 200f).Issues));
        }

        // ---- S5 / S11: validator ----

        [Test]
        public void S5_V7_LooksFromInsideThePreviousChunk()
        {
            // An obstacle 10 m after the entry: the viewpoint lies in the previous chunk. With an allowed radius of
            // 6 m before the seam zone, the previous chunk can turn the camera away; with the shipped 60 m it can't.
            var b = new ChunkLayoutBuilder("V7", 200f);
            b.Blk(8f, 1.4f, 2.4f, "Entry rock");
            string tight = string.Join("\n", ValidatePool(b, 200f, new WorldDirectorConfig { MaxCurvature = 1f / 6f }, DifficultyPhase.Mastery).Issues);
            StringAssert.Contains("previous chunk curving", tight);
            string shipped = string.Join("\n", ValidatePool(b, 200f, null, DifficultyPhase.Mastery).Issues);
            StringAssert.DoesNotContain("previous chunk curving", shipped);
        }

        [Test]
        public void S11_V14_FarEdgeOfTheBeam_AndAirSteering()
        {
            // Beam B offset by 1.0 m (the limit); a hanging branch right at A's end leaves no ground time to steer.
            var beams = new ChunkLayoutBuilder("V14w", 200f);
            beams.Width(20f, -1.2f, 1.2f).Width(60f, -1.2f, 1.2f).Width(60.01f, -1.2f, 2.2f).Width(63.5f, -1.2f, 2.2f).Width(63.51f, -0.2f, 2.2f).Width(150f, -0.2f, 2.2f).Width(170f, -3.5f, 3.5f)
                .Gap(20f, 150f).Floor(20f, 60f, 0f, 0f, -1.2f, 1.2f).Floor(63.5f, 150f, 0f, 0f, -0.2f, 2.2f)
                .High(59.4f, 1.0f, -1.2f, 1.2f)
                .Zone(TraversalMode.Canopy, 20f, 150f, -1.2f, 2.2f, 0.25f, false, "beams");
            var config = new WorldDirectorConfig();
            MovementConfig movement = ShippedAssets.Config();
            movement.Lateral.AirLateralFactor = 0.3f;
            var lib = new ChunkLibrary(new[] { Def(beams, 200f, DifficultyPhase.Rhythm) });
            var validator = new ChunkValidator(movement, config, ExpeditionSetup.CameraProfiles(), 0.4f);
            var report = new ValidationReport("T", "V14w");
            validator.CheckStatic(lib.GetEntry(0), config.RuleFor(DifficultyPhase.Rhythm), 14f, 16f, report);
            StringAssert.Contains("to the next beam", string.Join("\n", report.Issues));
        }

        // ---- S6: director never throws ----

        [Test]
        public void S6_TwoChunkLibrary_NothingEligible_LastResortInsteadOfThrow()
        {
            var opener = new ChunkDefinition { Id = "T_Start_01", Length = 60f, RunOpener = true, Category = ChunkCategory.Recovery };
            opener.Variants.Add(new ChunkLayoutBuilder("Default", 60f).Variant);
            var late = new ChunkDefinition { Id = "T_Late_01", Length = 200f, Category = ChunkCategory.Straight, PhaseMin = DifficultyPhase.Mastery, PhaseMax = DifficultyPhase.Mastery };
            late.Variants.Add(new ChunkLayoutBuilder("Default", 200f).Variant);
            var lib = new ChunkLibrary(new[] { opener, late });
            var director = new WorldDirector(lib, new WorldDirectorConfig(), ShippedAssets.Config().Speed, 1f / 60f, false);
            director.BeginRun(new DirectorRunSetup { Seed = 9UL, ShortStart = true });
            float s = 0f;
            for (int i = 0; i < 4; i++)
            {
                ChunkPick pick = default;
                Assert.DoesNotThrow(() => pick = director.PlanNext(s));
                s += lib.GetEntry(pick.Entry).Length;
            }

            Assert.Greater(director.LastResortPicks, 0);
            Assert.AreEqual(director.LastResortPicks, director.EmergencyPicks);
        }

        // ---- S7 / S8 / N3 / N4 / N11: analytics ----

        [Test]
        public void S7_AnalyticsLog_OneWritePerFlush_RollsOverAtTheCap()
        {
            string dir = Path.Combine(Path.GetTempPath(), "jb-log-" + Guid.NewGuid().ToString("N"));
            try
            {
                var log = new LocalAnalyticsLog(dir);
                string line = "{\"event\":\"x\",\"pad\":\"" + new string('a', 1000) + "\"}";
                for (int batch = 0; batch < 400; batch++)
                {
                    log.Write(line);
                    log.Write(line);
                    log.Commit();
                }

                Assert.AreEqual(400, log.Commits, "one file append per flush");
                Assert.LessOrEqual(new FileInfo(log.FilePath).Length, LocalAnalyticsLog.MaxBytes);
                Assert.IsTrue(File.Exists(log.RolledPath));
                Assert.LessOrEqual(new FileInfo(log.RolledPath).Length, LocalAnalyticsLog.MaxBytes);
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Test]
        public void S8_N3_N4_N11_Analytics_NoSeed_AbandonedRunsClosed_RunEndedFields()
        {
            ExpeditionContent content = ShippedContent.Shared;
            ExpeditionSession session = ShippedContent.Session();
            var log = new LocalAnalyticsLog(null);
            var analytics = new AnalyticsRecorder(content, 16);
            var flow = new ExpeditionFlow(content, session, Service(new MemorySaveStorage()), SaveData.CreateDefault(3L, -0.3f), analytics, log);
            flow.BeginRun(ShippedContent.Directed(5UL, AbilityFlags.DeepBreath), true);
            for (int t = 0; t < 120; t++)
            {
                session.Step(InputFrame.Empty);
            }

            flow.BeginRun(ShippedContent.Directed(6UL), true);
            string all = string.Join("\n", log.Lines);
            StringAssert.Contains("\"event\":\"run_ended\"", all);
            StringAssert.Contains("\"cause\":\"abandoned\"", all);
            StringAssert.Contains("\"abilities_mask\":" + (int)AbilityFlags.DeepBreath, all, "stored in the event, not read at flush time");
            StringAssert.DoesNotContain("\"seed\"", all);

            // A full ring still closes the run, and run_ended reports what was dropped.
            for (int t = 0; t < 60 * 60 && session.Phase != RunPhase.Results; t++)
            {
                session.Step(InputFrame.Empty);
                for (int i = 0; i < session.Events.Count; i++)
                {
                    analytics.OnRunEvent(session.Events[i], session);
                }

                session.Events.Clear();
            }

            for (int i = 0; i < 40; i++)
            {
                analytics.RecordRevive(false, 1, 0, i);
            }

            flow.FinishRun();
            all = string.Join("\n", log.Lines);
            string last = log.Lines[log.Lines.Count - 1];
            StringAssert.Contains("\"event\":\"run_ended\"", last);
            StringAssert.Contains("\"crystals\":", last);
            StringAssert.Contains("\"hits\":", last);
            StringAssert.Contains("\"revives\":0", last);
            StringAssert.IsMatch("\"dropped\":[1-9]", last);
            StringAssert.Contains("\"phase\":\"", all, "death has the difficulty phase");
        }

        // ---- S9: sightings ----

        [Test]
        public void S9_SightingsOfKnownEntries_AreBankedIntoTheJournal()
        {
            ExpeditionContent content = ShippedContent.Shared;
            SaveData profile = SaveData.CreateDefault(3L, -0.3f);
            profile.runsCompleted = 1;
            profile.journal.Add(new JournalRecord { id = "D-01", sightings = 1, firstRun = 1 });
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(new ExpeditionRunSetup { FirstExpedition = true, Skill = -0.3f, Discovered = profile.IsDiscovered });
            StepUntil(session, ShippedContent.Bot(session, RouteType.Safe), () => session.Stats.Sightings > 0 || session.Simulation.State.Distance > 3000f);
            int d01 = content.FindDiscovery("D-01");
            Assert.AreEqual(1, session.Stats.SightingsOf(d01), "the Falls Basin seen again");
            ProgressionRules.ApplyRun(profile, session.Stats, content, false, AbilityFlags.None);
            Assert.AreEqual(2, profile.FindJournal("D-01").sightings);
        }

        // ---- helpers ----

        private static ChunkDefinition Def(ChunkLayoutBuilder b, float length, DifficultyPhase phase)
        {
            var def = new ChunkDefinition { Id = "T_Review_01", Length = length, PhaseMin = phase, PhaseMax = phase };
            def.Variants.Add(b.Variant);
            return def;
        }

        private static ValidationReport ValidatePool(ChunkLayoutBuilder b, float length, WorldDirectorConfig config = null, DifficultyPhase phase = DifficultyPhase.Rhythm)
        {
            var lib = new ChunkLibrary(new[] { Def(b, length, phase) });
            var validator = new ChunkValidator(ShippedAssets.Config(), config ?? new WorldDirectorConfig(), ExpeditionSetup.CameraProfiles(), 0.4f);
            return validator.ValidatePool(lib.GetEntry(0));
        }
    }
}
