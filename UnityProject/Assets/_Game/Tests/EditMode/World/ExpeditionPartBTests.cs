using System.Collections.Generic;
using System.Text;
using JungleBooze.Core;
using JungleBooze.Core.Save;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Analytics;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>
    /// Vertical slice Part B on the shipped content (spec 103): the Perfect bot swims, swings (Perfect releases),
    /// crosses the beams and discovers the sailback by observation; run 2 with Deep Breath takes the Sunken Arch;
    /// revive rules; analytics events (AC-103-50); zero allocation in the traversal stretch; camera modifiers;
    /// path curvature; the new validator rules (AC-102-02).
    /// </summary>
    public sealed class ExpeditionPartBTests
    {
        private sealed class Log
        {
            public readonly Dictionary<RunEventType, int> Counts = new Dictionary<RunEventType, int>();
            public readonly List<string> Chunks = new List<string>();
            public readonly List<string> Creatures = new List<string>();

            public int this[RunEventType type] => Counts.TryGetValue(type, out int n) ? n : 0;
        }

        private static Log Drive(ExpeditionSession session, IInputProvider input, float until, AnalyticsRecorder analytics = null, bool creatureTrace = false)
        {
            var log = new Log();
            for (int t = 0; t < 60 * 60 * 12 && session.Phase != RunPhase.Results && session.Simulation.State.Distance < until; t++)
            {
                session.Step(input.ReadInput(session.Run.SessionTick));
                for (int i = 0; i < session.Events.Count; i++)
                {
                    RunEvent e = session.Events[i];
                    log.Counts[e.Type] = log[e.Type] + 1;
                    analytics?.OnRunEvent(e, session);
                    if (e.Type == RunEventType.ChunkEntered)
                    {
                        ChunkRuntime c = session.Path.Chunk(e.Id).Chunk;
                        log.Chunks.Add(c.Id + "/" + c.VariantName);
                    }
                }

                if (creatureTrace)
                {
                    for (int i = 0; i < SailbackSystem.Capacity; i++)
                    {
                        Sailback a = session.Creatures.Get(i);
                        log.Creatures.Add(i + ":" + a.State + "@" + a.S.ToString("0.00") + "," + a.Y.ToString("0.00"));
                    }
                }

                session.Events.Clear();
            }

            return log;
        }

        [Test]
        public void Expedition1_PerfectBot_SwimsSwingsPerfectCrossesBeams_DiscoversTheSailbackByObservation()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Secret, RouteType.Risky, RouteType.Safe);
            Log log = Drive(session, bot, 2820f);
            RunStats s = session.Stats;
            var summary = new StringBuilder();
            foreach (KeyValuePair<RunEventType, int> kv in log.Counts)
            {
                summary.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            }

            Debug.Log("[JungleBooze] Expedition 1 Part B events: " + summary + "\n  perfect coins " + s.PerfectCoins + ", traversals " + s.TraversalSuccesses + "/" + s.TraversalAttempts);
            Assert.AreEqual(0, session.Simulation.State.Hits, "Expedition 1 with 0 hits");
            Assert.IsFalse(session.Simulation.State.Dead);
            Assert.AreEqual(1, log[RunEventType.WaterEnter], "C6");
            Assert.AreEqual(1, log[RunEventType.WaterExit]);
            Assert.GreaterOrEqual(log[RunEventType.Dive], 1, "dives under logs");
            Assert.GreaterOrEqual(log[RunEventType.Leap], 1, "leaps the snag");
            Assert.AreEqual(2, log[RunEventType.VineGrab], "V1 and V2");
            Assert.AreEqual(2, log[RunEventType.PerfectRelease], "the bot hits both Perfect windows");
            Assert.AreEqual(1, log[RunEventType.PerfectSpan]);
            Assert.AreEqual(10 + 10 + 25, s.PerfectCoins, "AC-103-22: 10 per Perfect + 25 Perfect Span");
            Assert.GreaterOrEqual(log[RunEventType.TraversalResult], 6);
            Assert.AreEqual(s.TraversalAttempts, s.TraversalSuccesses, "every traversal clean");
            Assert.AreEqual(3, s.NewDiscoveryCount, "D-01, D-02 (sailback observed), D-03");
            Assert.GreaterOrEqual(log[RunEventType.Vista], 1, "vista beat at D-01");
            Assert.GreaterOrEqual(log[RunEventType.CurtainPass], 1, "through Veil Falls");
            Assert.GreaterOrEqual(s.Crystals, 4, "C3 risky + V2 column + 2 grotto crystals");
        }

        [Test]
        public void Run2_DeepBreath_ShowcasesThePool_AndTheBotTakesTheSunkenArch()
        {
            int found = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                ExpeditionSession session = ShippedContent.Session();
                session.BeginRun(ShippedContent.Directed(seed * 101UL, AbilityFlags.DeepBreath, showcase: AbilityFlags.DeepBreath));
                PerfectBot bot = ShippedContent.Bot(session, RouteType.Secret, RouteType.Safe);
                Log log = Drive(session, bot, 2600f);
                int pool = log.Chunks.FindIndex(c => c.StartsWith("R_Swim_Pool_01"));
                Assert.GreaterOrEqual(pool, 1, "seed " + seed + ": " + string.Join(", ", log.Chunks));
                Assert.LessOrEqual(pool, 5, "within the first 5 picks after the start chunk");
                Assert.GreaterOrEqual(log[RunEventType.DeepDiveStart], 1, "seed " + seed);
                Assert.AreEqual(0, session.Simulation.State.Hits, "seed " + seed);
                int d04 = ShippedContent.Shared.FindDiscovery("D-04");
                found += session.Tracker.IsDiscovered(d04) ? 1 : 0;
            }

            Assert.AreEqual(4, found, "D-04 Sunken Arch found through the deep dive");
        }

        [Test]
        public void DeepBreath_ArchCrystals_OnlyThroughTheDeepDive()
        {
            ExpeditionSession with = ShippedContent.Session();
            with.BeginRun(ShippedContent.Directed(707UL, AbilityFlags.DeepBreath, showcase: AbilityFlags.DeepBreath));
            Drive(with, ShippedContent.Bot(with, RouteType.Safe), 2400f);

            ExpeditionSession without = ShippedContent.Session();
            without.BeginRun(ShippedContent.Directed(707UL, AbilityFlags.None));
            PerfectBot bot = ShippedContent.Bot(without, RouteType.Safe);
            Drive(without, bot, 2400f);
            Assert.GreaterOrEqual(with.Stats.Crystals - without.Stats.Crystals, 2, "the two Sunken Arch crystals");
        }

        [Test]
        public void Revive_CostsOneTwoFour_MaxThree_NotInExpedition1_BankedFromTheWallet()
        {
            ResultsConfig cfg = ShippedContent.Shared.Results;
            Assert.AreEqual(1, ReviveRules.Cost(cfg, 0));
            Assert.AreEqual(2, ReviveRules.Cost(cfg, 1));
            Assert.AreEqual(4, ReviveRules.Cost(cfg, 2));
            Assert.AreEqual(3, cfg.MaxRevives);

            ExpeditionSession first = ShippedContent.Session();
            first.BeginRun(ShippedContent.FirstRun());
            Assert.IsFalse(ReviveRules.CanOffer(cfg, true, first.Stats), "never in Expedition 1");

            SaveData profile = SaveData.CreateDefault(3L, -0.3f);
            profile.runsCompleted = 2;
            profile.crystals = 10;
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.Directed(55UL));
            int revives = 0;
            for (int t = 0; t < 60 * 60 * 6 && revives < 4; t++)
            {
                session.Step(InputFrame.Empty);
                session.Events.Clear();
                if (session.Simulation.State.Dead)
                {
                    if (ReviveRules.TryRevive(session, cfg, profile))
                    {
                        revives++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Assert.AreEqual(3, revives, "max 3 per run");
            Assert.AreEqual(7, session.Stats.ReviveCrystals, "1 + 2 + 4");
            int before = profile.crystals;
            for (int t = 0; t < 600 && session.Phase != RunPhase.Results; t++)
            {
                session.Step(InputFrame.Empty);
            }

            ProgressionRules.ApplyRun(profile, session.Stats, ShippedContent.Shared, false, AbilityFlags.None);
            Assert.AreEqual(System.Math.Max(0, before + session.Stats.TotalCrystals - 7), profile.crystals);
        }

        [Test]
        public void Revive_InWater_ReturnsToSwimming_AtTheLastSurfacePosition()
        {
            var b = new ChunkLayoutBuilder("Pool", 300f);
            b.Width(0f, -4.5f, 4.5f).Width(300f, -4.5f, 4.5f).Floor(0f, 300f, -1.8f, -1.8f).Water(0f, 300f, -4.5f, 4.5f)
                .FloatingLog(60f).FloatingLog(90f).FloatingLog(120f);
            var def = new ChunkDefinition { Id = "T_Pool_01", Length = 300f };
            def.Variants.Add(b.Variant);
            var path = new WorldPath();
            path.Append(new ChunkRuntime(def, 0, 0, 0), new ChunkPick { CoinDensity = 1f, FlowCrystalCoin = -1, PowerUpSlot = -1 });
            var sim = new RunnerSimulation(SpecConfig.Create(), path, 1f / 60f, new RunEventBuffer(1024));
            sim.Reset(new RunOptions { ForcedSpeed = 12f, SkipStartRamp = true });
            for (int t = 0; t < 3000 && !sim.State.Dead; t++)
            {
                sim.Step(InputFrame.Empty);
            }

            Assert.IsTrue(sim.State.Dead);
            float hazard = sim.State.S;
            Assert.IsTrue(sim.Revive());
            sim.Step(InputFrame.Empty);
            Assert.AreEqual(MoveMode.Swim, sim.State.Mode);
            Assert.LessOrEqual(sim.State.S, hazard - 6f + 0.3f);
            Assert.AreEqual(sim.SwimLine, sim.State.Y, 1e-4f);
        }

        [Test]
        public void AC103_50_Analytics_TraversalCreatureSecretPowerUpAbility_WithGddProperties()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            var analytics = new AnalyticsRecorder(ShippedContent.Shared) { Build = "test", SessionId = "s1" };
            analytics.BeginRun(0, 1UL, 0, -0.3f, true);
            Drive(session, ShippedContent.Bot(session, RouteType.Secret, RouteType.Safe), 2100f, analytics);
            analytics.RecordAbilityUnlocked(ShippedContent.Shared.FindAbility(AbilityFlags.DeepBreath), 1);
            var types = new HashSet<string>();
            var lines = new List<string>();
            for (int i = 0; i < analytics.Count; i++)
            {
                AnalyticsEvent e = analytics[i];
                if (e.Type == AnalyticsEventType.TraversalResult)
                {
                    types.Add(e.Text);
                }

                lines.Add(analytics.ToJson(e));
            }

            CollectionAssert.IsSubsetOf(new[] { "swim_dive", "swim_leap", "vine_perfect", "beam_gap" }, types);
            Assert.GreaterOrEqual(analytics.CountOf(AnalyticsEventType.CreatureFound), 1);
            Assert.GreaterOrEqual(analytics.CountOf(AnalyticsEventType.SecretFound), 1);
            Assert.AreEqual(1, analytics.CountOf(AnalyticsEventType.AbilityUnlocked));
            string all = string.Join("\n", lines);
            StringAssert.Contains("\"event\":\"traversal_result\"", all);
            StringAssert.Contains("\"type\":\"vine_perfect\",\"success\":true,\"obstacle_id\":", all);
            StringAssert.Contains("\"event\":\"creature_found\"", all);
            StringAssert.Contains("\"entry_id\":\"D-02\",\"first_time\":true,\"chunk_id\":\"C_Canopy_VineSpan_01\"", all);
            StringAssert.Contains("\"event\":\"secret_found\"", all);
            StringAssert.Contains("\"event\":\"ability_unlocked\"", all);
            StringAssert.Contains("\"cost\":150", all);
            StringAssert.Contains("\"session_id\":\"s1\"", all);
            StringAssert.Contains("\"run_id\":0", all);
            StringAssert.Contains("\"t_ms\":", all);

            // power_up: picked / used / expired from the run events.
            var shield = new AnalyticsRecorder(ShippedContent.Shared);
            shield.OnRunEvent(new RunEvent(RunEventType.PowerUp, 10, 0, (byte)PowerUpKind.Shield, 0f), session);
            shield.OnRunEvent(new RunEvent(RunEventType.ShieldConsumed, 20, 3, 0, 0f), session);
            shield.OnRunEvent(new RunEvent(RunEventType.ShieldExpired, 30, -1, 0, 0f), session);
            Assert.AreEqual(3, shield.CountOf(AnalyticsEventType.PowerUp));
            StringAssert.Contains("\"id\":\"shield\",\"action\":\"picked\"", shield.ToJson(shield[0]));
            StringAssert.Contains("\"action\":\"used\"", shield.ToJson(shield[1]));
            StringAssert.Contains("\"action\":\"expired\"", shield.ToJson(shield[2]));
        }

        [Test]
        public void AC103_49_SwimVineCanopyCreatures_AllocateNothingPerTick()
        {
            ExpeditionSession twin = ShippedContent.Session();
            twin.BeginRun(ShippedContent.FirstRun());
            PerfectBot twinBot = ShippedContent.Bot(twin, RouteType.Secret, RouteType.Safe);
            var recorder = new AnalyticsRecorder(ShippedContent.Shared, 4096);
            for (int i = 0; i < 60 * 200 && twin.Simulation.State.Distance < 2000f; i++)
            {
                twin.Step(twinBot.ReadInput(i));
                for (int k = 0; k < twin.Events.Count; k++)
                {
                    recorder.OnRunEvent(twin.Events[k], twin);
                }

                twin.Events.Clear();
            }

            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Secret, RouteType.Safe);
            var measured = new AnalyticsRecorder(ShippedContent.Shared, 4096);
            int tick = 0;
            while (session.Simulation.State.Distance < 950f && !session.Simulation.State.Dead && tick < 60 * 300)
            {
                session.Step(bot.ReadInput(tick++));
                session.Events.Clear();
            }

            Assert.That(() =>
            {
                while (session.Simulation.State.Distance < 1900f && !session.Simulation.State.Dead && tick < 60 * 600)
                {
                    session.Step(bot.ReadInput(tick++));
                    for (int k = 0; k < session.Events.Count; k++)
                    {
                        measured.OnRunEvent(session.Events[k], session);
                    }

                    session.Events.Clear();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.IsFalse(session.Simulation.State.Dead, session.Stats.DeathLabel);
            Assert.Greater(session.Stats.VineReleases, 1, "measured through the swim, the vines, the beams and the sailbacks");
        }

        [Test]
        public void AC103_44_SameInputs_SameCreatureStatesAndDiscoveries()
        {
            ExpeditionSession a = ShippedContent.Session();
            a.BeginRun(ShippedContent.FirstRun());
            var recording = new InputRecording(1UL);
            PerfectBot bot = ShippedContent.Bot(a, RouteType.Secret, RouteType.Safe);
            var recorder = new RecordingInputProvider(bot, recording);
            Log first = Drive(a, recorder, 2100f, null, true);
            ExpeditionSession b = ShippedContent.Session();
            b.BeginRun(ShippedContent.FirstRun());
            Log second = Drive(b, new ReplayInputProvider(recording), 2100f, null, true);
            CollectionAssert.AreEqual(first.Creatures, second.Creatures);
            Assert.AreEqual(a.Stats.NewDiscoveryCount, b.Stats.NewDiscoveryCount);
            Assert.AreEqual(a.Stats.PerfectCoins, b.Stats.PerfectCoins);
            Assert.AreEqual(a.Simulation.State.S, b.Simulation.State.S);
        }

        // ---- Camera modifiers (spec 103 §11) ----

        private static CameraRigModel Rig()
        {
            return new CameraRigModel(new CameraProfile(), 10f, 16f, 11f) { Modifiers = new CameraModifiers() };
        }

        [Test]
        public void CameraModifiers_BlendOver04s_SwimLowersAndBobs_VistaLasts2s_FallHolds()
        {
            CameraRigModel rig = Rig();
            var target = new CameraTargetInput { S = 0f, Speed = 12f };
            rig.Snap(target);
            float baseY = rig.Pose.Y;
            target.Mode = CameraMode.Swim;
            rig.Update(target, 0.2f);
            Assert.AreEqual(0.5f, rig.ModifierWeight(CameraMode.Swim), 1e-4f, "half way after 0.2 s");
            rig.Update(target, 0.2f);
            Assert.AreEqual(1f, rig.ModifierWeight(CameraMode.Swim), 1e-4f);
            for (int i = 0; i < 60; i++)
            {
                rig.Update(target, 1f / 60f);
            }

            Assert.AreEqual(baseY - 0.6f, rig.Pose.Y, 0.06f, "swim height −0.6 m (± bob)");
            rig.ReducedMotion = true;
            float y0 = rig.Update(target, 1f / 60f).Y;
            float y1 = rig.Update(target, 0.3f).Y;
            Assert.AreEqual(y0, y1, 1e-3f, "Reduced Motion: no bob");

            CameraRigModel vista = Rig();
            var run = new CameraTargetInput { S = 0f, Speed = 12f };
            vista.Snap(run);
            float fov = vista.Pose.FovDeg;
            vista.TriggerVista();
            for (int i = 0; i < 114; i++)
            {
                vista.Update(run, 1f / 60f);
            }

            Assert.AreEqual(1f, vista.VistaWeight, 1e-4f, "full vista weight during the 2 s beat");
            Assert.Greater(vista.Pose.FovDeg, fov + 3.3f, "vista FOV +4° (through the FOV spring)");
            for (int i = 0; i < 300; i++)
            {
                vista.Update(run, 1f / 60f);
            }

            Assert.AreEqual(0f, vista.VistaWeight, 1e-4f);
            Assert.AreEqual(fov, vista.Pose.FovDeg, 0.1f, "back after the beat");

            CameraRigModel fall = Rig();
            var canopy = new CameraTargetInput { S = 0f, Y = 9f, GroundY = 9f, Speed = 12f, Mode = CameraMode.Canopy };
            fall.Snap(canopy);
            float held = fall.Pose.Y;
            canopy.FallHold = true;
            canopy.Y = 2f;
            canopy.GroundY = 2f;
            for (int i = 0; i < 30; i++)
            {
                fall.Update(canopy, 1f / 60f);
            }

            Assert.AreEqual(held, fall.Pose.Y, 1e-3f, "the camera stops following down");
        }

        // ---- Path curvature (spec 102 §2.1) ----

        [Test]
        public void Curvature_ConstantCurveIsACircle_FramesContinuousAcrossSeams()
        {
            const float k = 1f / 80f;
            var curve = new PathCurve(new[] { new CurveKey(0f, k), new CurveKey(100f, k) }, 100f);
            curve.Evaluate(100f, out float x, out float z, out float h);
            Assert.AreEqual(100f * k, h, 1e-3f, "heading = arc length × curvature");
            Assert.AreEqual(80f * (1f - Mathf.Cos(h)), x, 0.02f, "on the 80 m circle");
            Assert.AreEqual(80f * Mathf.Sin(h), z, 0.02f);

            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            WorldPath path = session.Path;
            for (int serial = path.FirstChunkSerial + 1; serial < path.NextChunkSerial; serial++)
            {
                float seam = path.Chunk(serial).StartS;
                PathFrame before = path.GetFrame(seam - 0.01f);
                PathFrame after = path.GetFrame(seam + 0.01f);
                Assert.AreEqual(before.X, after.X, 0.03f, "position continuous at seam " + serial);
                Assert.AreEqual(before.Z, after.Z, 0.03f);
                Assert.AreEqual(before.Heading, after.Heading, 1e-3f, "tangent continuous");
            }

            Assert.AreNotEqual(0f, path.GetFrame(100f).Heading, "C1 curves (keyframe F1)");
        }

        // ---- Validator rules (AC-102-02) ----

        private static ValidationReport Validate(ChunkLayoutBuilder b, float length, DifficultyPhase phase = DifficultyPhase.Rhythm)
        {
            var def = new ChunkDefinition { Id = "T_Rules_01", Length = length, PhaseMin = phase, PhaseMax = phase };
            def.Variants.Add(b.Variant);
            var lib = new ChunkLibrary(new[] { def });
            var validator = new ChunkValidator(ShippedAssets.Config(), new WorldDirectorConfig(), ExpeditionSetup.CameraProfiles(), 0.4f);
            return validator.ValidatePool(lib.GetEntry(0));
        }

        [Test]
        public void AC102_02_NewRules_FlagV3_V13_V14_W1toW5_Curvature()
        {
            // V3: a blocker wall on the left, then 6 m later one on the right.
            var v3 = new ChunkLayoutBuilder("V3", 120f);
            v3.BlkSpan(40f, -3.5f, 0.8f).BlkSpan(43f, -0.8f, 3.5f);
            StringAssert.Contains("V3", string.Join("\n", Validate(v3, 120f).Issues));

            // W2 / W4 / W5: a blocker in water right after the entry, a strong current.
            var water = new ChunkLayoutBuilder("W", 200f);
            water.Width(10f, -4.5f, 4.5f).Width(190f, -4.5f, 4.5f).Floor(10f, 20f, 0f, -1.8f).Floor(20f, 180f, -1.8f, -1.8f).Floor(180f, 190f, -1.8f, 0f)
                .Water(6f, 194f, -4.5f, 4.5f).BlkSpan(25f, -4.5f, -2f).Current(60f, 100f, 2.6f, 0f)
                .FloatingLog(120f).Snag(124f);
            string w = string.Join("\n", Validate(water, 200f).Issues);
            StringAssert.Contains("W2", w);
            StringAssert.Contains("W4", w);
            StringAssert.Contains("W5", w);
            StringAssert.Contains("W1", w, "log → snag 4 m apart at swim speed");

            // V13: a funnel wider than 2.4 m and a platform too close to the anchor.
            var vine = new ChunkLayoutBuilder("V13", 200f);
            vine.Gap(96f, 102f).Vine(99f, 0f, 96f, 102f, 108f, 109f, false);
            StringAssert.Contains("V13", string.Join("\n", Validate(vine, 200f).Issues));

            // V14: a beam offset by 1.6 m and a 4.2 m beam gap.
            var beams = new ChunkLayoutBuilder("V14", 200f);
            beams.Width(20f, -1.2f, 1.2f).Width(60f, -1.2f, 1.2f).Width(60.01f, -1.2f, 2.8f).Width(64.2f, -1.2f, 2.8f).Width(64.21f, 0.4f, 2.8f).Width(150f, 0.4f, 2.8f).Width(170f, -3.5f, 3.5f)
                .Gap(20f, 150f).Floor(20f, 60f, 0f, 0f, -1.2f, 1.2f).Floor(64.2f, 150f, 0f, 0f, 0.4f, 2.8f)
                .Zone(TraversalMode.Canopy, 20f, 150f, -1.2f, 2.8f, 0.25f, false, "beams");
            string v14 = string.Join("\n", Validate(beams, 200f).Issues);
            StringAssert.Contains("V14", v14);
            StringAssert.Contains("offset", v14);

            // Curvature: radius 40 m and a bend in the seam zone.
            var bend = new ChunkLayoutBuilder("Bend", 120f);
            bend.Bend(2f, 1f / 40f).Bend(60f, 1f / 40f).Bend(70f, 0f);
            StringAssert.Contains("curve radius", string.Join("\n", Validate(bend, 120f).Issues));
        }
    }
}
