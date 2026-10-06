using System;
using JungleBooze.Core;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    public sealed class FixedStepTimeSourceTests
    {
        [Test]
        public void DeltaTime_IsConstantStep()
        {
            var time = new FixedStepTimeSource(60);
            Assert.AreEqual(1f / 60f, time.DeltaTime, 1e-7f);
        }

        [Test]
        public void Step_AdvancesTickAndElapsedWithoutDrift()
        {
            var time = new FixedStepTimeSource(60);
            for (int i = 0; i < 3600; i++)
            {
                time.Step();
            }

            Assert.AreEqual(3600L, time.Tick);
            Assert.AreEqual(60.0, time.ElapsedSeconds, 1e-9);
        }

        [Test]
        public void Accumulate_SmallDelta_ReturnsNoStepUntilFullStepAccumulated()
        {
            var time = new FixedStepTimeSource(50); // 0.02 s steps
            Assert.AreEqual(0, time.Accumulate(0.015));
            Assert.AreEqual(1, time.Accumulate(0.010));
            Assert.That(time.InterpolationAlpha, Is.InRange(0.24f, 0.26f));
        }

        [Test]
        public void Accumulate_DoesNotAdvanceTick()
        {
            var time = new FixedStepTimeSource(60);
            time.Accumulate(1.0);
            Assert.AreEqual(0L, time.Tick);
        }

        [Test]
        public void Accumulate_RespectsMaxStepsPerFrame_AndDropsExcess()
        {
            // 64 Hz keeps every value exact in binary floating point.
            var time = new FixedStepTimeSource(64, maxStepsPerFrame: 4);
            int steps = time.Accumulate(1.0); // 64 steps due, cap is 4

            Assert.AreEqual(4, steps);
            Assert.AreEqual(0.9375, time.DroppedSeconds);
            Assert.AreEqual(0, time.Accumulate(0.0));
        }

        [Test]
        public void Accumulate_InvalidInput_IsIgnored()
        {
            var time = new FixedStepTimeSource(60);
            Assert.AreEqual(0, time.Accumulate(-1.0));
            Assert.AreEqual(0, time.Accumulate(double.NaN));
            Assert.AreEqual(0, time.Accumulate(double.PositiveInfinity));
        }

        [Test]
        public void Reset_ClearsState()
        {
            var time = new FixedStepTimeSource(60);
            time.Accumulate(0.5);
            time.Step();
            time.Reset();

            Assert.AreEqual(0L, time.Tick);
            Assert.AreEqual(0f, time.InterpolationAlpha);
            Assert.AreEqual(0.0, time.DroppedSeconds);
        }

        [Test]
        public void Constructor_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepTimeSource(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepTimeSource(60, 0));
        }
    }
}
