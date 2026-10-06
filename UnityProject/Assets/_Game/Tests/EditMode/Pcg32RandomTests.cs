using System;
using JungleBooze.Core;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode
{
    public sealed class Pcg32RandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new Pcg32Random(12345UL);
            var b = new Pcg32Random(12345UL);

            for (int i = 0; i < 10000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"Sequences diverged at index {i}.");
            }
        }

        [Test]
        public void SameSeed_ProducesSameMixedSequence()
        {
            var a = new Pcg32Random(777UL);
            var b = new Pcg32Random(777UL);

            for (int i = 0; i < 2000; i++)
            {
                Assert.AreEqual(a.NextInt(-5, 17), b.NextInt(-5, 17));
                Assert.AreEqual(a.NextFloat(), b.NextFloat());
                Assert.AreEqual(a.NextFloat(2.5f, 9f), b.NextFloat(2.5f, 9f));
                Assert.AreEqual(a.Chance(0.3f), b.Chance(0.3f));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new Pcg32Random(1UL);
            var b = new Pcg32Random(2UL);

            int equal = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (a.NextUInt() == b.NextUInt())
                {
                    equal++;
                }
            }

            Assert.Less(equal, 5);
        }

        [Test]
        public void MatchesReferenceImplementationVector()
        {
            // Reference output of the PCG32 C demo: pcg32_srandom_r(&rng, 42u, 54u).
            var rng = new Pcg32Random(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };

            foreach (uint value in expected)
            {
                Assert.AreEqual(value, rng.NextUInt());
            }
        }

        [Test]
        public void NextInt_StaysWithinBounds()
        {
            var rng = new Pcg32Random(99UL);
            for (int i = 0; i < 10000; i++)
            {
                int value = rng.NextInt(-3, 4);
                Assert.GreaterOrEqual(value, -3);
                Assert.Less(value, 4);
            }
        }

        [Test]
        public void NextInt_HitsEveryValueInSmallRange()
        {
            var rng = new Pcg32Random(5UL);
            var seen = new bool[3];
            for (int i = 0; i < 300; i++)
            {
                seen[rng.NextInt(0, 3)] = true;
            }

            CollectionAssert.AreEqual(new[] { true, true, true }, seen);
        }

        [Test]
        public void NextInt_FullIntRange_DoesNotThrow()
        {
            var rng = new Pcg32Random(8UL);
            for (int i = 0; i < 100; i++)
            {
                rng.NextInt(int.MinValue, int.MaxValue);
            }
        }

        [Test]
        public void NextInt_InvalidRange_Throws()
        {
            var rng = new Pcg32Random(1UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(6, 5));
        }

        [Test]
        public void NextFloat_IsInUnitInterval()
        {
            var rng = new Pcg32Random(3UL);
            for (int i = 0; i < 10000; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void Chance_ExtremesAreExact()
        {
            var rng = new Pcg32Random(4UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
                Assert.IsTrue(rng.Chance(1f));
            }
        }

        [Test]
        public void Fork_IsDeterministic()
        {
            IRandom childA = new Pcg32Random(2024UL).Fork(7UL);
            IRandom childB = new Pcg32Random(2024UL).Fork(7UL);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(childA.NextUInt(), childB.NextUInt());
            }
        }

        [Test]
        public void Fork_DifferentStreams_Differ()
        {
            IRandom childA = new Pcg32Random(2024UL).Fork(1UL);
            IRandom childB = new Pcg32Random(2024UL).Fork(2UL);

            int equal = 0;
            for (int i = 0; i < 1000; i++)
            {
                if (childA.NextUInt() == childB.NextUInt())
                {
                    equal++;
                }
            }

            Assert.Less(equal, 5);
        }

        [Test]
        public void ChildStream_IsUnaffectedByOtherChildUsage()
        {
            var rootA = new Pcg32Random(31UL);
            IRandom trackA = rootA.Fork(1UL);
            IRandom cosmeticA = rootA.Fork(2UL);

            var rootB = new Pcg32Random(31UL);
            IRandom trackB = rootB.Fork(1UL);
            rootB.Fork(2UL); // created but never used

            for (int i = 0; i < 500; i++)
            {
                cosmeticA.NextUInt(); // extra draws on a sibling stream
                Assert.AreEqual(trackA.NextUInt(), trackB.NextUInt());
            }
        }
    }
}
