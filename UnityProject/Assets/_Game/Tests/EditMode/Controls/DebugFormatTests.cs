using System.Text;
using JungleBooze.App.FeelTest;
using JungleBooze.Gameplay.Run;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.EditMode.Controls
{
    /// <summary>Review nit 2: the debug overlay formats numbers and enum names without allocating.</summary>
    public sealed class DebugFormatTests
    {
        [TestCase(0.0, 2, "0.00")]
        [TestCase(3.14159, 2, "3.14")]
        [TestCase(-0.004, 2, "0.00")]
        [TestCase(-12.25, 1, "-12.3")]
        [TestCase(640.0, 1, "640.0")]
        [TestCase(1234.5, 0, "1235")]
        public void FixedMatchesInvariantFormatting(double value, int decimals, string expected)
        {
            var b = new StringBuilder();
            DebugFormat.Fixed(b, value, decimals);
            Assert.AreEqual(expected, b.ToString());
        }

        [Test]
        public void IntAndNames()
        {
            var b = new StringBuilder();
            DebugFormat.Int(b, -305);
            b.Append(' ');
            DebugFormat.Int(b, 0);
            string[] names = DebugFormat.Names(typeof(RunPhase));
            b.Append(' ').Append(DebugFormat.Name(names, (int)RunPhase.Running)).Append(' ').Append(DebugFormat.Name(names, 99));
            Assert.AreEqual("-305 0 Running ?", b.ToString());
        }

        [Test]
        public void FormattingDoesNotAllocate()
        {
            var b = new StringBuilder(256);
            string[] names = DebugFormat.Names(typeof(RunPhase));
            void Format()
            {
                b.Length = 0;
                DebugFormat.Fixed(b, 123.456, 2);
                DebugFormat.Int(b, 98765);
                b.Append(DebugFormat.Name(names, 1));
            }

            Format(); // warm-up (JIT)
            Assert.That(Format, Is.Not.AllocatingGCMemory());
        }
    }
}
