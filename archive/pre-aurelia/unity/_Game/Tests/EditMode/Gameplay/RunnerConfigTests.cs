using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Runner;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Spec 001 section 3 config conversion and validation (AC-60, AC-61), plus the event buffer and bot input.
    /// </summary>
    public sealed class RunnerConfigTests
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

        [Test]
        public void AC61_ToConfigProducesSpecTickAndDerivedValues()
        {
            RunnerConfig c = Create<RunnerConfigAsset>().ToConfig();

            Assert.AreEqual(7, c.LaneSwitchTicks);
            Assert.AreEqual(4, c.LaneQueueStartTick);
            Assert.AreEqual(36, c.JumpAirtimeTicks);
            Assert.AreEqual(6, c.FastFallMaxTicks);
            Assert.AreEqual(39, c.SlideTicks);
            Assert.AreEqual(9, c.InputBufferTicks);
            Assert.AreEqual(5, c.CoyoteTicks);
            Assert.AreEqual(30, c.RunStartRampTicks);
            Assert.AreEqual(9, c.StumbleBounceTicks);
            Assert.AreEqual(180, c.StumbleDazeTicks);
            Assert.AreEqual(33.33, c.GravityMps2, 0.005);
            Assert.AreEqual(10.0, c.JumpVelocityMps, 1e-9);
            Assert.AreEqual(18, c.JumpApexTick);
            Assert.AreEqual(-2.4f, c.LaneCenterX(0));
            Assert.AreEqual(0f, c.LaneCenterX(1));
            Assert.AreEqual(2.4f, c.LaneCenterX(2));
        }

        [Test]
        public void AC61_PlainDefaultsMatchTheAssetDefaults()
        {
            RunnerConfig fromAsset = Create<RunnerConfigAsset>().ToConfig();
            RunnerConfig plain = RunnerConfig.CreateDefault();

            Assert.AreEqual(plain.LaneSwitchTicks, fromAsset.LaneSwitchTicks);
            Assert.AreEqual(plain.SlideTicks, fromAsset.SlideTicks);
            Assert.AreEqual(plain.GravityMps2, fromAsset.GravityMps2);
            Assert.AreEqual(plain.LaneWidthM, fromAsset.LaneWidthM);
            Assert.AreEqual(plain.EventBufferCapacity, fromAsset.EventBufferCapacity);
        }

        [TestCase(120f, 7)]
        [TestCase(80f, 5)]
        [TestCase(150f, 9)]
        [TestCase(600f, 36)]
        [TestCase(650f, 39)]
        [TestCase(1f, 1)]
        public void MsToTicksRoundsToNearestTickWithMinimumOne(float ms, int ticks)
        {
            Assert.AreEqual(ticks, RunnerConfig.MsToTicks(ms));
        }

        [Test]
        public void ZeroCoyoteAndZeroRampMeanOff()
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            values.CoyoteMs = 0f;
            values.RunStartRampMs = 0f;
            RunnerConfig c = RunnerConfig.FromDesignValues(values);

            Assert.AreEqual(0, c.CoyoteTicks);
            Assert.AreEqual(0, c.RunStartRampTicks);
        }

        [Test]
        public void AC60_DefaultAssetsPassValidation()
        {
            var errors = new List<string>();
            Assert.IsTrue(Create<RunnerConfigAsset>().Validate(errors), string.Join(" ", errors));
            Assert.IsTrue(Create<SpeedCurveAsset>().Validate(errors), string.Join(" ", errors));
        }

        [Test]
        public void AC60_RunnerTuningRejectsOutOfRangeValues()
        {
            AssertRejected(v => v.SlidingHeightM = v.StandingHeightM);
            AssertRejected(v => v.LaneSwitchMs = 200f);
            AssertRejected(v => v.LaneWidthM = 1.5f);
            AssertRejected(v => v.StartLane = 3);
            AssertRejected(v => v.JumpAirtimeMs = float.NaN);
            AssertRejected(v => v.EventBufferCapacity = 8);
            AssertRejected(v => v.LaneCount = 4);

            RunnerDesignValues bad = RunnerDesignValues.CreateDefault();
            bad.SlidingHeightM = 2.0f;
            RunnerConfigAsset asset = Create<RunnerConfigAsset>();
            asset.SetDesignValues(bad);
            Assert.IsFalse(asset.Validate(new List<string>()));
            Assert.Throws<ArgumentException>(() => asset.ToConfig());
        }

        [Test]
        public void AC60_SpeedCurveRejectsUnsortedAndDecreasingRows()
        {
            SpeedCurveAsset unsorted = Create<SpeedCurveAsset>();
            unsorted.SetValues(new[] { new SpeedCurveRow(0f, 10f), new SpeedCurveRow(800f, 12f), new SpeedCurveRow(500f, 13f) }, 8f);
            Assert.IsFalse(unsorted.Validate(new List<string>()));
            Assert.Throws<ArgumentException>(() => unsorted.ToSpeedCurve());

            SpeedCurveAsset decreasing = Create<SpeedCurveAsset>();
            decreasing.SetValues(new[] { new SpeedCurveRow(0f, 10f), new SpeedCurveRow(500f, 9f) }, 8f);
            Assert.IsFalse(decreasing.Validate(new List<string>()));

            SpeedCurveAsset empty = Create<SpeedCurveAsset>();
            empty.SetValues(new SpeedCurveRow[0], 8f);
            Assert.IsFalse(empty.Validate(new List<string>()));
        }

        [Test]
        public void AC60_DefaultSpeedCurveAssetMatchesSpecRows()
        {
            SpeedCurve fromAsset = Create<SpeedCurveAsset>().ToSpeedCurve();
            SpeedCurve plain = SpeedCurve.CreateDefault();

            Assert.AreEqual(plain.RowCount, fromAsset.RowCount);
            for (int i = 0; i < plain.RowCount; i++)
            {
                Assert.AreEqual(plain.GetRowDistance(i), fromAsset.GetRowDistance(i), 1e-6);
                Assert.AreEqual(plain.GetRowSpeed(i), fromAsset.GetRowSpeed(i), 1e-6);
            }

            Assert.AreEqual(8.0, fromAsset.TutorialSpeedMps, 1e-6);
        }

        [Test]
        public void EventBufferKeepsOrderAndCountsOverflow()
        {
            var buffer = new RunnerEventBuffer(4);
            for (int i = 0; i < 6; i++)
            {
                buffer.Add(new RunnerEvent { Type = RunnerEventType.LaneBlocked, Tick = i });
            }

            Assert.AreEqual(4, buffer.Count);
            Assert.AreEqual(2, buffer.OverflowCount);
            Assert.AreEqual(2L, buffer[0].Tick, "oldest kept event");
            Assert.AreEqual(5L, buffer[3].Tick);

            buffer.Clear();
            Assert.AreEqual(0, buffer.Count);
            buffer.Add(new RunnerEvent { Type = RunnerEventType.JumpApex, Tick = 9 });
            Assert.AreEqual(9L, buffer[0].Tick);
        }

        [Test]
        public void BotInputProviderCombinesCommandsPerTick()
        {
            BotInputProvider bot = new BotInputProvider()
                .At(3, InputCommand.Jump)
                .At(3, InputCommand.MoveLeft)
                .At(7, InputCommand.Slide);

            Assert.AreEqual(InputCommand.None, bot.ReadCommands(0));
            Assert.AreEqual(InputCommand.Jump | InputCommand.MoveLeft, bot.ReadCommands(3));
            Assert.AreEqual(InputCommand.Slide, bot.ReadCommands(7));
            Assert.AreEqual(2, bot.ScriptedTickCount);
        }

        [Test]
        public void SimulationAcceptsRecordedFramesOnlyForTheNextTick()
        {
            var sim = new RunnerSimulation(RunnerConfig.CreateDefault(), SpeedCurve.CreateDefault());
            sim.Step(new InputFrame(0, InputCommand.MoveRight));
            Assert.AreEqual(1L, sim.NextTick);
            Assert.Throws<ArgumentException>(() => sim.Step(new InputFrame(5, InputCommand.Jump)));
        }

        private static void AssertRejected(Action<RunnerDesignValues> change)
        {
            RunnerDesignValues values = RunnerDesignValues.CreateDefault();
            change(values);
            Assert.IsFalse(values.Validate(new List<string>()));
            Assert.Throws<ArgumentException>(() => RunnerConfig.FromDesignValues(values));
        }
    }
}
