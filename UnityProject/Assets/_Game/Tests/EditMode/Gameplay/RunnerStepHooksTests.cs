using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 002 section 13.3 (step order with hook points 7a, 11a, 11b) and 16.1.6 (speed-source hook, cheap state
    /// copies), plus determinism with obstacles in play.
    /// </summary>
    public sealed class RunnerStepHooksTests
    {
        private sealed class RecordingHooks : IRunnerStepHooks
        {
            public readonly List<string> Calls = new List<string>();
            public readonly List<RunnerTickInfo> TrackInfos = new List<RunnerTickInfo>();
            public readonly List<RunnerTickInfo> ScoreInfos = new List<RunnerTickInfo>();
            public long EmitOnTick = -1;

            public void OnTrackUpdate(RunnerSimulation runner, in RunnerTickInfo info)
            {
                Calls.Add("7a@" + info.Tick);
                TrackInfos.Add(info);
                if (info.Tick == EmitOnTick)
                {
                    runner.EmitExternal(new RunnerEvent { Type = RunnerEventType.ChunkEntered, Tick = info.Tick, Value = 3 });
                }
            }

            public void OnCoinPickups(RunnerSimulation runner, in RunnerTickInfo info)
            {
                Calls.Add("11a@" + info.Tick);
            }

            public void OnScore(RunnerSimulation runner, in RunnerTickInfo info)
            {
                Calls.Add("11b@" + info.Tick);
                ScoreInfos.Add(info);
            }
        }

        private static RunnerTestHarness Harness(double speedMps, TestTrackQuery track)
        {
            RunnerDesignValues v = RunnerDesignValues.CreateDefault();
            v.RunStartRampMs = 0f;
            return new RunnerTestHarness(v, SpeedCurve.CreateConstant(speedMps), track);
        }

        [Test]
        public void HooksRunInOrder_7a_11a_11b_EveryTick()
        {
            RunnerTestHarness h = Harness(10.0, new TestTrackQuery());
            var hooks = new RecordingHooks();
            h.Sim.StepHooks = hooks;

            h.Step();
            h.Step();

            CollectionAssert.AreEqual(new[] { "7a@0", "11a@0", "11b@0", "7a@1", "11a@1", "11b@1" }, hooks.Calls);
        }

        [Test]
        public void TrackHook_SeesNewZ_ButLaneMoveNotYetApplied()
        {
            RunnerTestHarness h = Harness(12.0, new TestTrackQuery());
            var hooks = new RecordingHooks();
            h.Sim.StepHooks = hooks;

            h.Step(InputCommand.MoveRight); // tick 0
            RunnerTickInfo track = hooks.TrackInfos[0];
            Assert.AreEqual(12.0 / 60.0, track.Z, 1e-12, "step 7 already moved z");
            Assert.AreEqual(0.0, track.ZPrev, 1e-12);
            Assert.AreEqual(12.0, track.Speed, 1e-12);
            Assert.AreEqual(0f, track.X, "step 8 (lane tween) runs after the track hook");
            Assert.AreEqual(track.Z + 0.25, track.FrontZ, 1e-6);

            RunnerTickInfo score = hooks.ScoreInfos[0];
            Assert.Greater(score.X, 0f, "coin and score hooks see the moved X");
            Assert.AreEqual(0f, score.XPrev);
            Assert.AreEqual(1, score.OccupiedLane);
            Assert.AreEqual(1.8f, score.HitboxHeight);
            Assert.AreEqual(0.35f, score.HalfWidth, 1e-6f);
            Assert.AreEqual(0.25f, score.HalfDepth, 1e-6f);
        }

        [Test]
        public void DeathTick_SkipsCoinHook_ScoreStillRuns_ThenNoMoreHooks()
        {
            var track = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(1, 1, 5.0));
            RunnerTestHarness h = Harness(10.0, track);
            var hooks = new RecordingHooks();
            h.Sim.StepHooks = hooks;

            h.RunThrough(32); // dies on tick 28

            Assert.Contains("7a@28", hooks.Calls);
            Assert.Contains("11b@28", hooks.Calls);
            Assert.IsFalse(hooks.Calls.Contains("11a@28"), "no coin pickups on the death tick");
            Assert.IsFalse(hooks.Calls.Contains("7a@29"), "no hooks once dead");
            Assert.IsTrue(hooks.ScoreInfos[hooks.ScoreInfos.Count - 1].IsDead);
        }

        [Test]
        public void HookInfo_ReportsStumbleAndNearMissOfThisTick()
        {
            var stumbleTrack = new TestTrackQuery().AddBox(ObstacleFixtures.FullBlock(5, 2, 2.0));
            RunnerTestHarness s = Harness(6.0, stumbleTrack);
            var sHooks = new RecordingHooks();
            s.Sim.StepHooks = sHooks;
            s.RunThrough(19);
            s.Step(InputCommand.MoveRight);
            s.Step(); // tick 21: side stumble
            Assert.IsTrue(sHooks.ScoreInfos[21].StumbledThisTick);
            Assert.IsFalse(sHooks.ScoreInfos[20].StumbledThisTick);
            Assert.IsFalse(sHooks.ScoreInfos[21].IsDead);

            var nearTrack = new TestTrackQuery().AddBox(ObstacleFixtures.LowBarrier(3, 1, 4.0));
            RunnerTestHarness n = Harness(10.0, nearTrack);
            var nHooks = new RecordingHooks();
            n.Sim.StepHooks = nHooks;
            n.Step(InputCommand.Jump);
            n.RunThrough(40);
            Assert.AreEqual(1, nHooks.ScoreInfos[29].NearMissesThisTick);
            Assert.AreEqual(0, nHooks.ScoreInfos[28].NearMissesThisTick);
        }

        [Test]
        public void EmitExternal_LandsInTheSameEventStream()
        {
            RunnerTestHarness h = Harness(10.0, new TestTrackQuery());
            var hooks = new RecordingHooks { EmitOnTick = 2 };
            h.Sim.StepHooks = hooks;
            h.RunThrough(4);

            List<RunnerEvent> chunk = h.EventsOf(RunnerEventType.ChunkEntered);
            Assert.AreEqual(1, chunk.Count);
            Assert.AreEqual(2L, chunk[0].Tick);
            Assert.AreEqual(3, chunk[0].Value);
        }

        [Test]
        public void SpeedSource_ReplacesCurve_RampAndMultiplierStillApply()
        {
            var h = new RunnerTestHarness(); // default values: 30-tick ramp from 50%
            h.Sim.SpeedSource = new ConstantSpeedSource(12.0);

            h.Step();
            Assert.AreEqual(6.0f, h.State.Speed, 1e-5f, "tick 0: 50% of the source speed");
            h.RunThrough(30);
            Assert.AreEqual(12.0f, h.State.Speed, 1e-5f);

            h.Sim.SpeedMultiplier = 2.0;
            h.Step();
            Assert.AreEqual(24.0f, h.State.Speed, 1e-5f);
        }

        [Test]
        public void ConstantSpeedSource_RejectsNonPositive()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ConstantSpeedSource(0.0));
            Assert.AreEqual(9.5, new ConstantSpeedSource(9.5).GetBaseSpeedMps(1234.0));
        }

        /// <summary>
        /// 6 m/s gauntlet: side stumble on block 1 (tick 21), jump over the low barrier (overlap ticks 137 to 148),
        /// slide under the high barrier (297 to 307), detour through lane 0 and back before block 4 (lane 0 at
        /// 40 m), jump over the 3 m gap at 60 m.
        /// </summary>
        private static TestTrackQuery Gauntlet()
        {
            return new TestTrackQuery()
                .AddBox(ObstacleFixtures.FullBlock(1, 2, 2.0))
                .AddBox(ObstacleFixtures.LowBarrier(2, 1, 14.0))
                .AddBox(ObstacleFixtures.HighBarrier(3, 1, 30.0))
                .AddBox(ObstacleFixtures.FullBlock(4, 0, 40.0))
                .AddGap(60.0, 63.0);
        }

        private static InputCommand Script(long tick)
        {
            switch (tick)
            {
                case 20: return InputCommand.MoveRight;
                case 130: return InputCommand.Jump;
                case 290: return InputCommand.Slide;
                case 320: return InputCommand.MoveLeft;
                case 360: return InputCommand.MoveRight;
                case 595: return InputCommand.Jump;
                default: return InputCommand.None;
            }
        }

        [Test]
        public void SameCommandsWithObstacles_IdenticalHashEveryTick()
        {
            RunnerTestHarness a = Harness(6.0, Gauntlet());
            RunnerTestHarness b = Harness(6.0, Gauntlet());
            for (long tick = 0; tick < 700; tick++)
            {
                a.Step(Script(tick));
                b.Step(Script(tick));
                Assert.AreEqual(a.Sim.ComputeStateHash(), b.Sim.ComputeStateHash(), "tick " + tick);
            }

            Assert.AreEqual(1, a.CountOf(RunnerEventType.Stumbled), "the gauntlet exercises a stumble");
            Assert.IsFalse(a.State.IsDead, "the scripted line survives the gauntlet");
        }

        [Test]
        public void CopyStateFrom_MidRunWithDaze_ContinuesIdentically()
        {
            RunnerConfig config = RunnerConfig.FromDesignValues(NoRamp());
            SpeedCurve curve = SpeedCurve.CreateConstant(6.0);
            TestTrackQuery track = Gauntlet();
            var source = new RunnerSimulation(config, curve, track);
            for (long tick = 0; tick < 25; tick++)
            {
                source.Step(Script(tick));
            }

            Assert.Greater(source.DazeTicksLeft, 0, "copied while dazed and bouncing");
            var copy = new RunnerSimulation(config, curve, track);
            copy.CopyStateFrom(source);
            Assert.AreEqual(source.ComputeStateHash(), copy.ComputeStateHash());
            Assert.AreEqual(source.NextTick, copy.NextTick);

            for (long tick = 25; tick < 400; tick++)
            {
                source.Step(Script(tick));
                copy.Step(Script(tick));
                Assert.AreEqual(source.ComputeStateHash(), copy.ComputeStateHash(), "tick " + tick);
            }
        }

        [Test]
        public void CopyStateFrom_RequiresSameConfig()
        {
            var a = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            var b = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            Assert.Throws<System.ArgumentException>(() => b.CopyStateFrom(a));
        }

        private static RunnerDesignValues NoRamp()
        {
            RunnerDesignValues v = RunnerDesignValues.CreateDefault();
            v.RunStartRampMs = 0f;
            return v;
        }
    }
}
