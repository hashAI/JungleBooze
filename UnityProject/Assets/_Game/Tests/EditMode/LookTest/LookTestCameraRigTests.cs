using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// ADR 0004 look test: the shared runner/camera pose (used by Play and by the screenshot tool), the benchmark
    /// command line, the CC0 LOD-mesh filter and the cliff ramp.
    /// </summary>
    public sealed class LookTestCameraRigTests
    {
        private LookTestConfigAsset _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void RunnerWeaveStaysOnThePathAndRepeats()
        {
            for (float t = 0f; t < 30f; t += 0.37f)
            {
                float x = LookTestCameraRig.RunnerX(t);
                Assert.LessOrEqual(Mathf.Abs(x), _config.PathHalfWidthM, "t " + t);
                Assert.AreEqual(x, LookTestCameraRig.RunnerX(t + LookTestCameraRig.WeavePeriodS), 1e-3f, "t " + t);
            }
        }

        [Test]
        public void RunnerStandsOnTheGroundAtTheRunDistance()
        {
            Vector3 p = LookTestCameraRig.RunnerPosition(1.3f, 42f);
            Assert.AreEqual(0f, p.y);
            Assert.AreEqual(42f, p.z);
        }

        [Test]
        public void CameraIsBehindAndAboveTheRunnerAndLooksAhead()
        {
            Vector3 camera = LookTestCameraRig.CameraTarget(_config, 1f, 100f);
            Vector3 look = LookTestCameraRig.LookAtPoint(_config, 1f, 100f);
            Assert.AreEqual(100f - _config.CameraOffsetBehindM, camera.z, 1e-4f);
            Assert.AreEqual(_config.CameraOffsetUpM, camera.y, 1e-4f);
            Assert.Greater(look.z, 100f);
            Assert.AreEqual(camera.x, look.x, 1e-4f, "camera and aim follow the same share of the weave");
        }

        [Test]
        public void LandscapeUsesTheConfiguredVerticalFov()
        {
            Assert.AreEqual(_config.CameraFovDeg, LookTestCameraRig.VerticalFov(_config, 2532f / 1170f), 1e-4f);
        }

        [Test]
        public void PortraitKeepsTheConfiguredHorizontalFov()
        {
            float aspect = 1170f / 2532f;
            float vertical = LookTestCameraRig.VerticalFov(_config, aspect);
            float horizontal = 2f * Mathf.Atan(Mathf.Tan(vertical * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
            Assert.AreEqual(_config.CameraPortraitHorizontalFovDeg, horizontal, 0.05f);
            Assert.Greater(vertical, _config.CameraFovDeg);
            Assert.LessOrEqual(vertical, 100f);
        }

        [Test]
        public void PortraitAimsHigherThanLandscape()
        {
            float landscape = LookTestCameraRig.LookAtPoint(_config, 0f, 10f, 2.16f).y;
            float portrait = LookTestCameraRig.LookAtPoint(_config, 0f, 10f, 0.46f).y;
            Assert.AreEqual(_config.CameraLookAtHeightM, landscape, 1e-4f);
            Assert.AreEqual(_config.CameraPortraitLookAtHeightM, portrait, 1e-4f);
            Assert.Greater(portrait, landscape);
        }

        [TestCase(new[] { "Player", "-jbBenchmark", "30" }, 30f)]
        [TestCase(new[] { "Player", "-jbBenchmark", "12.5" }, 12.5f)]
        [TestCase(new[] { "Player", "-jbBenchmark" }, 0f)]
        [TestCase(new[] { "Player", "-jbBenchmark", "soon" }, 0f)]
        [TestCase(new[] { "Player", "-jbBenchmark", "-5" }, 0f)]
        [TestCase(new[] { "Player" }, 0f)]
        public void BenchmarkSecondsFromTheCommandLine(string[] args, float expected)
        {
            Assert.AreEqual(expected, LookTestRoot.ReadBenchmarkSeconds(args), 1e-4f);
        }

        [Test]
        public void BenchmarkSecondsToleratesNull()
        {
            Assert.AreEqual(0f, LookTestRoot.ReadBenchmarkSeconds(null));
        }

        [TestCase("rock_07_LOD0", false)]
        [TestCase("rock_07_LOD1", true)]
        [TestCase("rock_07_LOD3", true)]
        [TestCase("shrub_03_a_LOD0", false)]
        [TestCase("fern_02_a", false)]
        [TestCase("rock_LOD", false)]
        public void OnlyLod0MeshesOfAScanBecomeVariants(string meshName, bool lower)
        {
            Assert.AreEqual(lower, LookTestSceneBuilder.IsLowerLod(meshName));
        }

        [Test]
        public void CliffRisesOverTheConfiguredRamp()
        {
            var shape = new LookTestTerrainShape(_config);
            float start = _config.CliffStartZM;
            Assert.AreEqual(0f, shape.CliffPresence(start - 1f));
            Assert.Less(shape.CliffPresence(start + _config.CliffRampM * 0.5f), 1f);
            Assert.Greater(shape.CliffPresence(start + _config.CliffRampM * 0.5f), 0f);
            Assert.AreEqual(1f, shape.CliffPresence(start + _config.CliffRampM + 0.01f), 1e-4f);
            Assert.AreEqual(1f, shape.CliffPresence(_config.WaterfallZM), 1e-4f, "the waterfall sits on the full-height cliff");
        }
    }
}
