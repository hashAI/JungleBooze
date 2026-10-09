using System.Collections.Generic;
using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Look test water and falls (ADR 0008): the water FX texture tiles, layered fall sheets carry their layer data
    /// in vertex colors, the river is foamy only where it is shallow, and rock is wet only near falls.
    /// </summary>
    public sealed class LookTestWaterTests
    {
        private LookTestConfigAsset _config;
        private LookTestStretchLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            _layout = new LookTestStretchLayout(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void PeriodicNoiseWrapsAcrossTheTile()
        {
            for (int i = 0; i < 16; i++)
            {
                float v = i / 16f;
                Assert.AreEqual(LookTestTextureGenerator.PeriodicNoise(0f, v, 32, 2, 7u), LookTestTextureGenerator.PeriodicNoise(1f, v, 32, 2, 7u), 1e-5f);
                Assert.AreEqual(LookTestTextureGenerator.PeriodicNoise(v, 0f, 6, 6, 7u), LookTestTextureGenerator.PeriodicNoise(v, 1f, 6, 6, 7u), 1e-5f);
            }
        }

        [Test]
        public void WaterFxValuesAreInRangeAndVaried()
        {
            Color[] values = LookTestTextureGenerator.WaterFxValues(1, 64);
            Assert.AreEqual(64 * 64, values.Length);
            float minR = 1f;
            float maxR = 0f;
            float minG = 1f;
            float maxG = 0f;
            foreach (Color c in values)
            {
                Assert.That(c.r, Is.InRange(0f, 1f));
                Assert.That(c.g, Is.InRange(0f, 1f));
                Assert.That(c.b, Is.InRange(0f, 1f));
                minR = Mathf.Min(minR, c.r);
                maxR = Mathf.Max(maxR, c.r);
                minG = Mathf.Min(minG, c.g);
                maxG = Mathf.Max(maxG, c.g);
            }

            Assert.Greater(maxR - minR, 0.8f, "streaks span dark gaps to white water");
            Assert.Greater(maxG - minG, 0.8f, "foam has lines and holes");
        }

        [Test]
        public void FallLayersCarrySpeedSeedAndCoverageInVertexColors()
        {
            foreach (LookTestMeshFactory.FallLayer layer in LookTestMeshFactory.FallLayers)
            {
                Mesh sheet = LookTestMeshFactory.Waterfall(10f, 30f, 1.5f, layer);
                Color[] colors = sheet.colors;
                Assert.IsNotEmpty(colors);
                float maxA = 0f;
                foreach (Color c in colors)
                {
                    Assert.AreEqual(layer.Speed, c.g, 1e-5f);
                    Assert.AreEqual(layer.Seed, c.b, 1e-5f);
                    Assert.That(c.r, Is.InRange(0f, 1f));
                    Assert.That(c.a, Is.InRange(0f, layer.Coverage + 1e-5f));
                    maxA = Mathf.Max(maxA, c.a);
                }

                Assert.Greater(maxA, layer.Coverage * 0.5f);
                Assert.AreEqual(30f, sheet.bounds.max.y, 1e-3f, "the lip is at the top height");
                Object.DestroyImmediate(sheet);
            }
        }

        [Test]
        public void RiverFoamsAtTheShoreButTheFordStaysClear()
        {
            float s = _config.RiverCrossSM + 8f;
            float rd = _layout.RiverD(s);
            float y = _layout.WaterY(rd);
            float fall = _layout.RiverFallS();
            Color middle = LookTestMeshFactory.RiverColor(_layout, s, rd, y, 0f, fall);
            Color shore = LookTestMeshFactory.RiverColor(_layout, s, rd + _config.RiverHalfWidthM, y, 0.74f, fall);
            Assert.Greater(middle.g, 0.5f, "mid river is deep");
            Assert.Greater(shore.r, middle.r + 0.3f, "foam lines the shore");

            Color ford = LookTestMeshFactory.RiverColor(_layout, _config.RiverCrossSM, 0f, _layout.WaterY(0f), 0f, fall);
            Assert.Less(ford.r, 0.35f, "the ford is clear turquoise, not white");
        }

        [Test]
        public void ImpactFoamIsWhiteInTheMiddleAndFadesToTheRim()
        {
            Mesh disc = LookTestMeshFactory.ImpactFoam(10f);
            Vector3[] vertices = disc.vertices;
            Color[] colors = disc.colors;
            for (int i = 0; i < vertices.Length; i++)
            {
                float r = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                if (r < 0.1f)
                {
                    Assert.Greater(colors[i].r, 0.95f);
                }
                else if (r > 9.9f)
                {
                    Assert.Less(colors[i].a, 0.05f);
                }
            }

            Object.DestroyImmediate(disc);
        }

        [Test]
        public void RockIsWetNearFallsOnly()
        {
            var falls = new List<LookTestBuildContext.FallSpan>
            {
                new LookTestBuildContext.FallSpan { Top = new Vector3(0f, 30f, 0f), Foot = new Vector3(0f, 0f, -1f), Width = 10f },
            };
            Assert.AreEqual(1f, LookTestBuildContext.Wetness(new Vector3(2f, 15f, 1f), falls), 1e-4f, "behind the sheet");
            Assert.Greater(LookTestBuildContext.Wetness(new Vector3(12f, 0f, 0f), falls), 0.5f, "splash zone at the foot");
            Assert.AreEqual(0f, LookTestBuildContext.Wetness(new Vector3(60f, 20f, 40f), falls), 1e-4f, "far away is dry");
        }

        [Test]
        public void BasinArchTopFitsUnderThePortraitFrame()
        {
            // P1: the portrait camera (pitch 12, FOV 65) sees about 20° above the horizon; the arch top must fit
            // under it with room for the pillars behind (ADR 0008).
            LookTestPath path = _config.CreatePath();
            Vector4 feet = _config.BasinArchFeet;
            float valley = _config.ValleyFloorY - 2f;
            float apexY = 0.5f * _config.BasinArchControlY + 0.5f * valley + _config.BasinArchRadiusM.x;
            Vector3 apex = LookTestLandmarks.BasinPoint(path, 0.5f * (feet.x + feet.z), 0.5f * (feet.y + feet.w), apexY);
            LookTestCameraRig.Pose(path, _config.PortraitCamera, 190.0, 0f, out Vector3 camera, out Quaternion _);
            Vector3 toApex = apex - camera;
            float elevation = Mathf.Atan2(toApex.y, new Vector2(toApex.x, toApex.z).magnitude) * Mathf.Rad2Deg;
            float top = 0.5f * _config.PortraitCamera.VerticalFovDeg - _config.PortraitCamera.PitchDeg;
            Assert.Less(elevation, top - 3f);
            Assert.Greater(elevation, 0f, "the arch still towers over the ledge");
        }
    }
}
