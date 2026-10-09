using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.EditMode.World
{
    /// <summary>
    /// Whole runs on the streamed world (spec 102–103 Part A): the Perfect bot through Expedition 1 + 2 km of
    /// endless, determinism and replay, discoveries, crystals, Clean Line, the Shield, the FTUE window, allocations.
    /// </summary>
    public sealed class ExpeditionRunTests
    {
        private const float EndlessExtra = 2000f;

        private sealed class RunLog
        {
            public readonly List<string> Chunks = new List<string>();
            public readonly List<int> Discoveries = new List<int>();
            public int CrystalEvents;
            public int PowerUps;
            public int CleanLines;
            public int Toasts;
            public long Ticks;
            public RunnerState Final;
            public RunStats Stats;
            public long ProbeSteps;
            public double Seconds;
        }

        private static RunLog Drive(ExpeditionSession session, IInputProvider input, float untilDistance, InputRecording record = null, int maxTicks = 60 * 60 * 12)
        {
            var log = new RunLog();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int t = 0; t < maxTicks; t++)
            {
                if (session.Phase == RunPhase.Results || session.Simulation.State.Distance >= untilDistance)
                {
                    break;
                }

                long tick = session.Run.SessionTick;
                InputFrame frame = input.ReadInput(tick);
                record?.Add(tick, frame);
                session.Step(frame);
                RunEventBuffer events = session.Events;
                for (int i = 0; i < events.Count; i++)
                {
                    RunEvent e = events[i];
                    switch (e.Type)
                    {
                        case RunEventType.ChunkEntered:
                            ChunkRuntime c = session.Path.Chunk(e.Id).Chunk;
                            log.Chunks.Add(c.Id + "/" + c.VariantName);
                            break;
                        case RunEventType.Discovery:
                            log.Discoveries.Add(e.Id * 2 + e.Reason);
                            log.Toasts += e.Reason;
                            break;
                        case RunEventType.Crystal:
                            log.CrystalEvents++;
                            break;
                        case RunEventType.PowerUp:
                            log.PowerUps++;
                            break;
                        case RunEventType.CleanLine:
                            log.CleanLines++;
                            break;
                    }
                }

                events.Clear();
                log.Ticks++;
            }

            watch.Stop();
            log.Seconds = watch.Elapsed.TotalSeconds;
            log.Final = session.Simulation.State;
            log.Stats = session.Stats;
            return log;
        }

        [Test]
        public void PerfectBot_PlaysExpedition1_And2kmEndless_WithZeroHits()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Secret, RouteType.Risky, RouteType.Safe);
            RunLog log = Drive(session, bot, 2820f + EndlessExtra);
            log.ProbeSteps = bot.ProbeSteps;
            Report("Expedition 1 + 2 km endless (Perfect bot, routes secret > risky > safe)", session, log, bot);
            Assert.IsFalse(log.Final.Dead, "died: " + log.Stats.DeathLabel);
            Assert.AreEqual(0, log.Final.Hits, "hits");
            Assert.GreaterOrEqual(log.Final.Distance, 2820f + EndlessExtra);
            Assert.AreEqual(0, bot.GaveUp);
            Assert.AreEqual(0, session.Director.EmergencyPicks);
            Assert.GreaterOrEqual(log.Chunks.Count, 13 + 8);
            Assert.AreEqual("F_Recovery_Riverbank_01/Default", log.Chunks[13], "forced Recovery after C13");
            Assert.AreEqual(3, log.Stats.NewDiscoveryCount, "D-01 Falls Basin, D-02 Sailback (observed), D-03 Veil Grotto");
            Assert.GreaterOrEqual(log.Stats.TotalCoins, 150, "the first run affords Deep Breath (spec 103 §13)");
            Assert.GreaterOrEqual(log.Stats.Crystals, 3, "C3 risky crystal + 2 grotto crystals");
            Assert.GreaterOrEqual(log.Stats.SecretRoutes, 1, "Veil Grotto taken");
            Assert.GreaterOrEqual(log.Stats.CleanLines, 1, "C3 risky ridge cleanly");
        }

        [Test]
        public void PerfectBot_SafeRoutes_ClearsExpedition1()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Safe);
            RunLog log = Drive(session, bot, 2830f);
            Report("Expedition 1 (Perfect bot, safe routes)", session, log, bot);
            Assert.AreEqual(0, log.Final.Hits);
            Assert.IsFalse(log.Final.Dead);
            Assert.AreEqual(0, log.Stats.SecretRoutes);
            Assert.AreNotEqual(0, log.Stats.MissedDiscoveries & (1 << ShippedContent.Shared.FindDiscovery("D-03")), "the grotto was passed and missed");
        }

        [Test]
        public void PerfectBot_DirectedRuns_ManySeeds_NoHits()
        {
            // "Never impossible" on generated sequences: the Perfect bot through directed runs of several seeds.
            var summary = new StringBuilder();
            for (int seed = 1; seed <= 6; seed++)
            {
                ExpeditionSession session = ShippedContent.Session();
                session.BeginRun(ShippedContent.Directed((ulong)seed * 7919UL, AbilityFlags.DeepBreath, skill: seed % 2 == 0 ? 0.8f : -0.8f));
                PerfectBot bot = ShippedContent.Bot(session, seed % 3 == 0 ? RouteType.Risky : RouteType.Safe);
                RunLog log = Drive(session, bot, 4000f);
                summary.Append("seed ").Append(seed).Append(": ").Append((int)log.Final.Distance).Append(" m, hits ").Append(log.Final.Hits)
                    .Append(", chunks ").Append(log.Chunks.Count).Append(log.Final.Dead ? ", DEAD " + log.Stats.DeathLabel : string.Empty).Append('\n');
                Assert.AreEqual(0, log.Final.Hits, "seed " + seed + "\n" + string.Join("\n", log.Chunks));
                Assert.IsFalse(log.Final.Dead, "seed " + seed);
            }

            Debug.Log("[JungleBooze] Directed runs:\n" + summary);
        }

        [Test]
        public void AC103_44_FullScriptedRun_ReplaysIdentically()
        {
            ExpeditionSession first = ShippedContent.Session();
            first.BeginRun(ShippedContent.FirstRun());
            var recording = new InputRecording(1UL);
            PerfectBot bot = ShippedContent.Bot(first, RouteType.Secret, RouteType.Risky, RouteType.Safe);
            RunLog a = Drive(first, bot, 2820f + 600f, recording);

            ExpeditionSession second = ShippedContent.Session();
            second.BeginRun(ShippedContent.FirstRun());
            RunLog b = Drive(second, new ReplayInputProvider(recording), 2820f + 600f);

            CollectionAssert.AreEqual(a.Chunks, b.Chunks, "chunks");
            CollectionAssert.AreEqual(a.Discoveries, b.Discoveries, "discoveries");
            Assert.AreEqual(a.Ticks, b.Ticks);
            Assert.AreEqual(a.Final.S, b.Final.S);
            Assert.AreEqual(a.Final.X, b.Final.X);
            Assert.AreEqual(a.Final.Coins, b.Final.Coins);
            Assert.AreEqual(a.Stats.Crystals, b.Stats.Crystals);
            Assert.AreEqual(a.Stats.TotalCoins, b.Stats.TotalCoins);
            Assert.AreEqual(a.CrystalEvents, b.CrystalEvents);
        }

        [Test]
        public void AC103_41_FirstRun_HealthFloor_AndNoCrashDeathInTheFirst60s()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            RunLog log = Drive(session, new NullInput(), 600f);
            Assert.IsFalse(log.Final.Dead, "no input for the first ~60 s never ends Expedition 1 (" + log.Stats.DeathLabel + ")");
            Assert.AreEqual(1, log.Final.Health);
            Assert.Greater(log.Final.Hits, 3, "the C1 boulder (crash → side-clip) and the C2 obstacles");
            Assert.Greater(log.Ticks, 60 * 50);
        }

        [Test]
        public void FirstRunHelp_SlowsBeforeEachFirstMove_OncePerMove_AndClearsOnTheRightAction()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            var help = new HelpTracker(session.Path, session.Simulation);
            help.BeginRun(true);
            var seen = new List<HelpMove>();
            float steerAt = -1f;
            for (int t = 0; t < 60 * 70 && session.Simulation.State.S < 600f; t++)
            {
                // Hands off, except: back to the centre before C2's dodge wall (earlier side-clips pushed her right).
                float st = session.Simulation.State.S;
                session.Step(st > 290f && st < 305f ? new InputFrame(InputCommand.None, ToMm(-session.Simulation.State.XTarget)) : InputFrame.Empty);
                session.Events.Clear();
                help.Update();
                if (help.Active && (seen.Count == 0 || seen[seen.Count - 1] != help.ActiveMove))
                {
                    seen.Add(help.ActiveMove);
                    if (help.ActiveMove == HelpMove.Steer)
                    {
                        steerAt = session.Simulation.State.S;
                    }
                }
            }

            CollectionAssert.AreEqual(new[] { HelpMove.Steer, HelpMove.Jump, HelpMove.Slide, HelpMove.Dodge }, seen);
            Assert.AreEqual(4, help.Prompts, "once per move (C2's second root and branch get no help; C4's gap comes after 600 m)");
            float lead = 140f - steerAt;
            Assert.That(lead, NUnit.Framework.Is.InRange(5.0f, 7.5f), "0.6 s before the C1 boulder at ~10.7 m/s");

            // The right action clears it: jump during the Jump prompt.
            session.BeginRun(ShippedContent.FirstRun());
            help.BeginRun(true);
            bool cleared = false;
            for (int t = 0; t < 60 * 30 && !cleared; t++)
            {
                bool jumpPrompt = help.Active && help.ActiveMove == HelpMove.Jump;
                session.Step(jumpPrompt ? InputFrame.FromCommands(InputCommand.Jump) : InputFrame.Empty);
                session.Events.Clear();
                help.Update();
                cleared = jumpPrompt && !help.Active;
            }

            Assert.IsTrue(cleared);
            help.BeginRun(false);
            help.Update();
            Assert.IsFalse(help.Active, "never outside the first run");
        }

        [Test]
        public void AC103_28_30_Discovery_FirstTimeRewards_ThenSightingsOnly()
        {
            ExpeditionContent content = ShippedContent.Shared;
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            RunLog run1 = Drive(session, ShippedContent.Bot(session, RouteType.Secret, RouteType.Safe), 2100f);
            Assert.AreEqual(3, run1.Stats.NewDiscoveryCount);
            Assert.AreEqual(150, run1.Stats.DiscoveryCoins, "+50 coins each");
            Assert.AreEqual(6, run1.Stats.DiscoveryCrystals, "+2 crystals each");
            Assert.AreEqual(3, run1.Toasts);

            // A profile that already has them: replaying the script gives sightings, no toast, no reward.
            var known = new HashSet<string> { "D-01", "D-02", "D-03" };
            ExpeditionRunSetup setup = ShippedContent.FirstRun();
            setup.Discovered = known.Contains;
            session.BeginRun(setup);
            RunLog run2 = Drive(session, ShippedContent.Bot(session, RouteType.Secret, RouteType.Safe), 2100f);
            Assert.AreEqual(0, run2.Stats.NewDiscoveryCount);
            Assert.AreEqual(0, run2.Stats.DiscoveryCoins);
            Assert.AreEqual(5, run2.Stats.Sightings, "D-01, D-03 and three sailback groups (C8 flock, C9 perch, grotto roost)");
            Assert.AreEqual(0, run2.Toasts);
            Assert.AreEqual(0, content.FindDiscovery("D-01"));
        }

        [Test]
        public void AC103_32_33_34_SecretBranch_CrystalsAndNoPenaltyWhenSkipped()
        {
            ExpeditionSession secret = ShippedContent.Session();
            secret.BeginRun(ShippedContent.FirstRun());
            RunLog a = Drive(secret, ShippedContent.Bot(secret, RouteType.Secret, RouteType.Safe), 2070f);
            ExpeditionSession safe = ShippedContent.Session();
            safe.BeginRun(ShippedContent.FirstRun());
            RunLog b = Drive(safe, ShippedContent.Bot(safe, RouteType.Safe), 2070f);
            Assert.AreEqual(1, a.Stats.SecretRoutes);
            Assert.AreEqual(b.Stats.Crystals + 2, a.Stats.Crystals, "two grotto crystals");
            Assert.AreEqual(0, a.Final.Hits);
            Assert.AreEqual(0, b.Final.Hits, "skipping the secret costs nothing");
            Assert.IsFalse(b.Final.Dead);
        }

        [Test]
        public void AC103_36_CleanLine_HalfTheBranchCoins_OnlyWithoutDamage()
        {
            // C3 risky ridge with the bot (clean).
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            int coinsAtFork = -1;
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Risky);
            int cleanLineBonus = 0;
            for (int t = 0; t < 60 * 60 && session.Simulation.State.S < 600f; t++)
            {
                session.Step(bot.ReadInput(t));
                if (coinsAtFork < 0 && session.Simulation.State.S >= 440f)
                {
                    coinsAtFork = session.Simulation.State.Coins;
                }

                for (int i = 0; i < session.Events.Count; i++)
                {
                    if (session.Events[i].Type == RunEventType.CleanLine)
                    {
                        int branchCoins = session.Simulation.State.Coins - coinsAtFork;
                        cleanLineBonus = (int)session.Events[i].Value;
                        Assert.AreEqual(Mathf.FloorToInt(branchCoins * 0.5f), cleanLineBonus, 1, "≈ 50% of the branch's coins");
                    }
                }

                session.Events.Clear();
            }

            Assert.Greater(cleanLineBonus, 20);
            Assert.AreEqual(cleanLineBonus, session.Stats.CleanLineCoins);

            // Same branch with a hit on it: no bonus.
            session.BeginRun(ShippedContent.FirstRun());
            bot = ShippedContent.Bot(session, RouteType.Risky);
            for (int t = 0; t < 60 * 60 && session.Simulation.State.S < 600f; t++)
            {
                float s = session.Simulation.State.S;
                InputFrame frame = s > 470f && s < 480f ? InputFrame.Empty : bot.ReadInput(t);
                session.Step(frame);
            }

            Assert.Greater(session.Stats.Hits, 0, "the bot let go at the risky Low @95 (s 475)");
            Assert.AreEqual(0, session.Stats.CleanLineCoins);
        }

        [Test]
        public void AC103_37_38_Shield_OnlyByJumping_AbsorbsAHit_Expires()
        {
            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Safe);
            Drive(session, bot, 2300f);
            Assert.AreEqual(0, session.Stats.PowerUps, "a running body passes under the Shield (y 2.3)");

            // Jump so the apex is at the Shield (s 2321).
            session.BeginRun(ShippedContent.FirstRun());
            bot = ShippedContent.Bot(session, RouteType.Safe);
            Drive(session, bot, 2310f);
            float speed = session.Simulation.State.Speed;
            float jumpAt = 2321f - (speed * 0.30f);
            while (session.Simulation.State.S < jumpAt)
            {
                session.Step(new InputFrame(InputCommand.None, ToMm(2.5f - session.Simulation.State.XTarget)));
            }

            session.Step(InputFrame.FromCommands(InputCommand.Jump));
            for (int i = 0; i < 60; i++)
            {
                session.Step(InputFrame.Empty);
            }

            Assert.AreEqual(1, session.Stats.PowerUps, "reachable by a well-timed jump");
            Assert.IsTrue(session.Simulation.State.Shield);
            Assert.AreEqual(1800, session.Simulation.State.ShieldUntilTick - session.Simulation.State.Tick, 70, "30 s shield");

            // No input into C12: the first hit is absorbed (no health lost), the shield is gone.
            int hits = session.Simulation.State.Hits;
            bool consumed = false;
            for (int i = 0; i < 600 && !consumed; i++)
            {
                session.Step(InputFrame.Empty);
                for (int k = 0; k < session.Events.Count; k++)
                {
                    consumed |= session.Events[k].Type == RunEventType.ShieldConsumed;
                }

                session.Events.Clear();
            }

            Assert.IsTrue(consumed);
            Assert.AreEqual(hits, session.Simulation.State.Hits, "absorbed hit costs nothing");
            Assert.IsFalse(session.Simulation.State.Shield);
        }

        [Test]
        public void AC103_38_TimedShield_ExpiresAfter1800Ticks()
        {
            var rig = JungleBooze.Tests.EditMode.Movement.SimRig.Flat(12f);
            rig.Sim.GrantShield(1800);
            rig.Steps(1799);
            Assert.IsTrue(rig.State.Shield);
            rig.Steps(1);
            Assert.IsFalse(rig.State.Shield);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.ShieldExpired));
        }

        [Test]
        public void AC102_10_StreamingAndTrackerAllocateNothingPerTick()
        {
            // Warm-up: a twin session plays the identical ticks first, so every code path has run once (first-use
            // runtime initialisation is not a per-tick allocation). Then the measured session must allocate 0 bytes.
            ExpeditionSession twin = ShippedContent.Session();
            twin.BeginRun(ShippedContent.Directed(31UL));
            PerfectBot twinBot = ShippedContent.Bot(twin, RouteType.Risky);
            for (int i = 0; i < 3600; i++)
            {
                twin.Step(twinBot.ReadInput(i));
                twin.Events.Clear();
            }

            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.Directed(31UL));
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Risky);
            for (int i = 0; i < 600; i++)
            {
                session.Step(bot.ReadInput(i));
                session.Events.Clear();
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 3000; i++)
                {
                    session.Step(bot.ReadInput(600 + i));
                    session.Events.Clear();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.GreaterOrEqual(session.Path.NextChunkSerial, 6, "streamed several chunks while measuring");
            Assert.AreEqual(twin.Simulation.State.S, session.Simulation.State.S, "same ticks");
        }

        private static short ToMm(float metres)
        {
            return InputFrame.ClampMm((long)System.Math.Round(metres * 1000f));
        }

        private static void Report(string title, ExpeditionSession session, RunLog log, PerfectBot bot)
        {
            RunStats s = log.Stats;
            var b = new StringBuilder();
            b.Append("[JungleBooze] ").Append(title).Append('\n');
            b.Append("  distance ").Append(((int)log.Final.Distance).ToString(CultureInfo.InvariantCulture)).Append(" m in ")
                .Append((log.Ticks / 60f).ToString("0.0", CultureInfo.InvariantCulture)).Append(" s sim, hits ").Append(log.Final.Hits)
                .Append(", dead ").Append(log.Final.Dead).Append(log.Final.Dead ? " (" + s.DeathLabel + ")" : string.Empty).Append('\n');
            b.Append("  coins ").Append(s.Coins).Append(" + clean line ").Append(s.CleanLineCoins).Append(" + discovery ").Append(s.DiscoveryCoins)
                .Append(" = ").Append(s.TotalCoins).Append("; crystals ").Append(s.Crystals).Append(" + ").Append(s.DiscoveryCrystals).Append('\n');
            b.Append("  discoveries ").Append(s.NewDiscoveryCount).Append(", routes risky/safe/secret ").Append(s.RiskyRoutes).Append('/').Append(s.SafeRoutes)
                .Append('/').Append(s.SecretRoutes).Append(", clean lines ").Append(s.CleanLines).Append(", chunks ").Append(log.Chunks.Count)
                .Append(", fallback picks ").Append(session.Director.FallbackPicks).Append(", emergency ").Append(session.Director.EmergencyPicks).Append('\n');
            b.Append("  bot probe steps ").Append(bot.ProbeSteps).Append(", gave up ").Append(bot.GaveUp).Append(", wall time ")
                .Append(log.Seconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" s\n  ");
            b.Append(string.Join(" → ", log.Chunks));
            Debug.Log(b.ToString());
        }

        private sealed class NullInput : IInputProvider
        {
            public InputFrame ReadInput(long tick)
            {
                return InputFrame.Empty;
            }
        }
    }
}
