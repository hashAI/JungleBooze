using JungleBooze.Editor.Characters;
using JungleBooze.Gameplay.Animation;
using JungleBooze.Gameplay.Config;
using NUnit.Framework;
using UnityEditor;

namespace JungleBooze.Tests.EditMode.Animation
{
    /// <summary>Run-cycle playback rate: no-skate stance rate, flight cap, touch-down skip, frame integration, cadence.</summary>
    public sealed class StrideWarpTests
    {
        private static RunnerAnimationConfig Cfg(float[] table)
        {
            return new RunnerAnimationConfig { StanceSpeedTable = table, SprintBlendMax = 0f };
        }

        // Quarter cycle planted at 5 m/s, a touch-down entry at 0.5 m/s, the rest flight.
        private static float[] Table()
        {
            var t = new float[16];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = -1f;
            }

            t[2] = 0.5f;
            for (int i = 3; i < 7; i++)
            {
                t[i] = 5f;
            }

            return t;
        }

        [Test]
        public void UniformRateMatchesTheNaturalSpeedWithoutWarp()
        {
            var c = new RunnerAnimationConfig { ContactWarp = false };
            Assert.AreEqual(12f / c.RunNaturalSpeed, StrideWarp.Rate(c, 12f, 0f, 0.3f), 1e-5f);
        }

        [Test]
        public void PlantedPhaseKeepsTheFootStill()
        {
            RunnerAnimationConfig c = Cfg(Table());
            float phase = 4.5f / 16f;
            float rate = StrideWarp.Rate(c, 14f, 0f, phase);
            Assert.AreEqual(14f / 5f, rate, 1e-4f, "foot backward speed x rate == ground speed");
        }

        [Test]
        public void FlightIsCappedAndTouchdownIsSkipped()
        {
            RunnerAnimationConfig c = Cfg(Table());
            Assert.AreEqual(c.FlightRateMax, StrideWarp.Rate(c, 14f, 0f, 12.5f / 16f), 1e-4f);
            Assert.AreEqual(c.TouchdownRate, StrideWarp.Rate(c, 14f, 0f, 2f / 16f), 1e-4f);
        }

        [Test]
        public void FrameRateIntegratesAcrossAContactStartingMidFrame()
        {
            RunnerAnimationConfig c = Cfg(Table());
            float start = (2.95f / 16f);
            float sampled = StrideWarp.Rate(c, 16f, 0f, start);
            float integrated = StrideWarp.FrameRate(c, 16f, 0f, start, 1f / 60f);
            Assert.AreNotEqual(sampled, integrated);
            Assert.Greater(integrated, 0f);

            // A constant table gives the same answer either way.
            var flat = new float[8];
            for (int i = 0; i < flat.Length; i++)
            {
                flat[i] = 4f;
            }

            RunnerAnimationConfig f = Cfg(flat);
            Assert.AreEqual(StrideWarp.Rate(f, 12f, 0f, 0.4f), StrideWarp.FrameRate(f, 12f, 0f, 0.4f, 1f / 60f), 1e-4f);
        }

        [Test]
        public void RatesStayWithinLimits()
        {
            RunnerAnimationConfig c = Cfg(Table());
            for (float speed = 0f; speed <= 16f; speed += 2f)
            {
                for (int i = 0; i < 64; i++)
                {
                    float r = StrideWarp.Rate(c, speed, 0f, i / 64f);
                    Assert.GreaterOrEqual(r, c.MinRunRate);
                    Assert.LessOrEqual(r, System.Math.Max(c.MaxRunRate, c.TouchdownRate));
                }
            }
        }

        [Test]
        public void ShippedTableGivesBelievableCadenceAt10To16()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RunnerAnimationConfigAsset>(PistaPaths.AnimationConfig);
            Assert.IsNotNull(asset, "Run JungleBooze > Characters > Build Pista Prefab");
            RunnerAnimationConfig c = asset.Values;
            Assert.GreaterOrEqual(c.StanceSpeedTable.Length, 32, "stance table generated from the Run clip");
            float slow = StrideWarp.StepsPerSecond(c, 10f, 0f);
            float fast = StrideWarp.StepsPerSecond(c, 16f, 0f);
            Assert.That(slow, Is.InRange(4.5f, 7f));
            Assert.That(fast, Is.InRange(slow, 7.5f), "cadence grows a little; stride does the rest");
        }
    }
}
