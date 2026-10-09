using JungleBooze.Core.Perf;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Perf
{
    public sealed class DeviceTierRulesTests
    {
        private static DeviceTier Classify(string model, int memoryMb = 4096)
        {
            return DeviceTierRules.Classify(model, memoryMb, 13, 13, 3500);
        }

        [TestCase("iPhone13,1")] // iPhone 12 mini (A14)
        [TestCase("iPhone13,2")] // iPhone 12
        [TestCase("iPhone14,6")] // iPhone SE 3rd gen (A15)
        [TestCase("iPhone17,3")] // iPhone 16
        [TestCase("iPad13,1")] // iPad Air 4 (A14)
        public void A14AndNewer_AreHigh(string model)
        {
            Assert.AreEqual(DeviceTier.High, Classify(model));
        }

        [TestCase("iPhone12,1")] // iPhone 11 (A13)
        [TestCase("iPhone12,8")] // iPhone SE 2nd gen (A13)
        [TestCase("iPhone11,8")] // iPhone XR (A12)
        [TestCase("iPad12,1")] // iPad 9th gen (A13)
        public void OlderThanA14_AreLow(string model)
        {
            Assert.AreEqual(DeviceTier.Low, Classify(model));
        }

        [TestCase("Mac16,10")]
        [TestCase("MacBookPro18,3")]
        [TestCase("arm64")]
        [TestCase("")]
        [TestCase(null)]
        public void DesktopEditorAndUnknown_AreHigh(string model)
        {
            Assert.AreEqual(DeviceTier.High, Classify(model));
        }

        [Test]
        public void LowMemory_IsLowEvenOnANewChip()
        {
            Assert.AreEqual(DeviceTier.Low, Classify("iPhone13,2", 3000));
            Assert.AreEqual(DeviceTier.High, Classify("iPhone13,2", 0), "Unknown memory is ignored.");
        }

        [TestCase("iPhone13,2", 13)]
        [TestCase("iPhone9,4", 9)]
        public void TryParseGeneration_ReadsTheNumberBeforeTheComma(string model, int expected)
        {
            Assert.IsTrue(DeviceTierRules.TryParseGeneration(model, DeviceTierRules.IphonePrefix, out int generation));
            Assert.AreEqual(expected, generation);
        }

        [TestCase("iPhone")]
        [TestCase("iPhone13")]
        [TestCase("iPhoneX,1")]
        [TestCase("iPad13,1")]
        public void TryParseGeneration_RejectsOtherShapes(string model)
        {
            Assert.IsFalse(DeviceTierRules.TryParseGeneration(model, DeviceTierRules.IphonePrefix, out _));
        }

        [Test]
        public void RenderScale_HoldsThePixelCount()
        {
            // iPhone 12: 2532 x 1170 = 2.96 MP; 1.7 MP -> 0.758.
            float scale = DeviceTierRules.RenderScaleFor(2532, 1170, 1.7f, 0.5f, 1f);
            Assert.AreEqual(0.7577f, scale, 0.001f);
            Assert.AreEqual(1f, DeviceTierRules.RenderScaleFor(1334, 750, 1.7f, 0.5f, 1f), "Small screens render at native size.");
            Assert.AreEqual(0.5f, DeviceTierRules.RenderScaleFor(8000, 4000, 1.7f, 0.5f, 1f), "Clamped at the minimum.");
            Assert.AreEqual(1f, DeviceTierRules.RenderScaleFor(0, 0, 1.7f, 0.5f, 1f));
        }
    }
}
