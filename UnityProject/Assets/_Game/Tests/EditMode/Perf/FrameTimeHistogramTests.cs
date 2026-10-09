using System;
using JungleBooze.Core.Perf;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Perf
{
    public sealed class FrameTimeHistogramTests
    {
        [Test]
        public void Percentiles_AreExactToOneBucket()
        {
            var h = new FrameTimeHistogram(0.1f, 100f);
            for (int i = 0; i < 98; i++)
            {
                h.Add(16.66f);
            }

            h.Add(33.3f);
            h.Add(50.1f);
            Assert.AreEqual(100L, h.Count);
            Assert.AreEqual(16.7f, h.Percentile(0.5), 0.051f);
            Assert.AreEqual(16.7f, h.Percentile(0.95), 0.051f);
            Assert.AreEqual(33.35f, h.Percentile(0.99), 0.06f);
            Assert.AreEqual(50.1f, h.Percentile(1.0), 0.051f);
            Assert.AreEqual(50.1f, h.MaxMs, 1e-4f);
            Assert.AreEqual((98 * 16.66 + 33.3 + 50.1) / 100.0, h.MeanMs, 1e-3);
            Assert.AreEqual(0.02, h.FractionAbove(25f), 1e-9);
        }

        [Test]
        public void SlowFrames_BeyondTheRange_AreKept()
        {
            var h = new FrameTimeHistogram(0.1f, 50f);
            h.Add(10f);
            h.Add(400f);
            Assert.AreEqual(400f, h.Percentile(1.0), 1e-3f);
            Assert.AreEqual(400f, h.MaxMs, 1e-3f);
        }

        [Test]
        public void Empty_ReturnsZero_AndClearResets()
        {
            var h = new FrameTimeHistogram();
            Assert.AreEqual(0f, h.Percentile(0.5));
            h.Add(12f);
            h.Add(-1f);
            h.Add(float.NaN);
            Assert.AreEqual(1L, h.Count, "Negative and NaN samples are ignored.");
            h.Clear();
            Assert.AreEqual(0L, h.Count);
            Assert.AreEqual(0f, h.MaxMs);
        }

        [Test]
        public void InvalidRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimeHistogram(0f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FrameTimeHistogram(1f, 0.5f));
        }
    }
}
