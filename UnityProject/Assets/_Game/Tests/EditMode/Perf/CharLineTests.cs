using JungleBooze.Core.Perf;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Perf
{
    public sealed class CharLineTests
    {
        [Test]
        public void AppendsTextIntegersAndFixedPoint()
        {
            var line = new CharLine(128);
            line.Append("fps ").Append(59.94, 1).Append(',').Append(-3L).Append(',').Append(0L).Append(',').Append(16.666, 2).Append(',').Append(-0.004, 2)
                .Append(',').Append(1234567890123L).Append(',').Append(2.5, 0).Append(',').Append(0.05, 1);
            Assert.AreEqual("fps 59.9,-3,0,16.67,0.00,1234567890123,3,0.1", line.ToString());
        }

        [Test]
        public void NaN_AndOverflow_AreSafe()
        {
            var line = new CharLine(6);
            line.Append(double.NaN, 2).Append("abcdef");
            Assert.AreEqual("nanabc", line.ToString());
            line.Clear();
            Assert.AreEqual(0, line.Length);
            Assert.AreEqual("-9223372036854775808", new CharLine(32).Append(long.MinValue).ToString());
        }
    }
}
