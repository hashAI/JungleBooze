using System;
using JungleBooze.Core;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>Spec 004 AC-408 and T401: the polynomial sine, cosine and arcsine used by the swing.</summary>
    public sealed class DeterministicMathTests
    {
        [Test]
        public void AC408_SinAndCosAreWithinTheT401ErrorOnTheWholeSwingRange()
        {
            double maxSin = 0.0;
            double maxCos = 0.0;
            for (int i = 0; i <= 26000; i++)
            {
                double x = -DeterministicMath.MaxArgumentRad + (2.0 * DeterministicMath.MaxArgumentRad * i / 26000.0);
                maxSin = Math.Max(maxSin, Math.Abs(DeterministicMath.Sin(x) - Math.Sin(x)));
                maxCos = Math.Max(maxCos, Math.Abs(DeterministicMath.Cos(x) - Math.Cos(x)));
            }

            Assert.Less(maxSin, 1e-7, "sin");
            Assert.Less(maxCos, 1e-7, "cos");
        }

        [Test]
        public void AsinSmall_IsWithinT401ErrorUpToTenDegrees()
        {
            double limit = Math.Sin(10.0 * Math.PI / 180.0);
            double max = 0.0;
            for (int i = 0; i <= 2000; i++)
            {
                double a = -limit + (2.0 * limit * i / 2000.0);
                max = Math.Max(max, Math.Abs(DeterministicMath.AsinSmall(a) - Math.Asin(a)));
            }

            Assert.Less(max, 1e-8);
        }

        [Test]
        public void SinIsOddAndCosIsEven()
        {
            for (double x = 0.0; x <= 1.3; x += 0.05)
            {
                Assert.AreEqual(-DeterministicMath.Sin(x), DeterministicMath.Sin(-x), 0.0);
                Assert.AreEqual(DeterministicMath.Cos(x), DeterministicMath.Cos(-x), 0.0);
            }

            Assert.AreEqual(0.0, DeterministicMath.Sin(0.0), 0.0);
            Assert.AreEqual(1.0, DeterministicMath.Cos(0.0), 0.0);
        }

        [Test]
        public void SameInputGivesTheSameBitsEveryTime()
        {
            long a = BitConverter.DoubleToInt64Bits(DeterministicMath.Sin(0.7) + DeterministicMath.Cos(0.7) + DeterministicMath.AsinSmall(0.1));
            long b = BitConverter.DoubleToInt64Bits(DeterministicMath.Sin(0.7) + DeterministicMath.Cos(0.7) + DeterministicMath.AsinSmall(0.1));
            Assert.AreEqual(a, b);
        }
    }
}
