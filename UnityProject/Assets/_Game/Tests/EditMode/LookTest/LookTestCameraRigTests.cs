using JungleBooze.App.LookTest;
using JungleBooze.Editor.LookTest;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Look test v2: the shared runner/camera pose (Play and screenshots), the framing rules of ART_DIRECTION 9,
    /// the benchmark command line and the CC0 LOD-mesh filter.
    /// </summary>
    public sealed class LookTestCameraRigTests
    {
        private LookTestConfigAsset _config;
        private LookTestPath _path;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookTestConfigAsset>();
            _path = _config.CreatePath();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void RunnerWeaveStaysOnTheRunnableWidthAndRepeats()
        {
            for (float t = 0f; t < 30f; t += 0.37f)
            {
                float x = LookTestCameraRig.RunnerX(t);
                Assert.LessOrEqual(Mathf.Abs(x), _config.RunnableHalfWidthM, "t " + t);
                Assert.AreEqual(x, LookTestCameraRig.RunnerX(t + LookTestCameraRig.WeavePeriodS), 1e-3f, "t " + t);
            }
        }

        [Test]
        public void RunnerStandsOnTheTrail()
        {
            Vector3 p = LookTestCameraRig.RunnerPosition(_path, 42.0, 1.2f);
            Assert.AreEqual(_path.Height(42.0), p.y, 1e-4f);
            Assert.AreEqual(1.2f, Vector3.Dot(p - _path.Position(42.0), _path.Right(42.0)), 1e-3f);
        }

        [Test]
        public void CameraIsBehindAndAboveTheRunnerWithTheProfilePitch()
        {
            LookTestCameraProfile profile = _config.LandscapeCamera;
            for (double s = 0.0; s < 450.0; s += 17.3)
            {
                LookTestCameraRig.Pose(_path, profile, s, 0f, out Vector3 camera, out Quaternion rotation);
                Vector3 runner = _path.Position(s);
                Vector3 toRunner = runner - camera;
                Vector3 flat = new Vector3(toRunner.x, 0f, toRunner.z);
                Assert.AreEqual(profile.BackM, flat.magnitude, 0.6f, "s " + s);
                Assert.Greater(Vector3.Dot(rotation * Vector3.forward, toRunner), 0f, "the runner is in front of the camera, s " + s);
                Assert.AreEqual(profile.PitchDeg, rotation.eulerAngles.x, 1e-3f, "s " + s);
                Assert.AreEqual(profile.UpM, camera.y - _path.Height(s - profile.FollowLagM), 1e-3f, "s " + s);
            }
        }

        [Test]
        public void ProfileFollowsTheAspect()
        {
            Assert.AreEqual(_config.LandscapeCamera.VerticalFovDeg, LookTestCameraRig.Profile(_config, 2532f / 1170f).VerticalFovDeg);
            Assert.AreEqual(_config.PortraitCamera.VerticalFovDeg, LookTestCameraRig.Profile(_config, 1170f / 2532f).VerticalFovDeg);
        }

        [Test]
        public void LandscapeFramesPistaAsArtDirectionSection9()
        {
            // 14-18% of the screen height, feet about 28% from the bottom, horizon in the upper third.
            Vector2 figure = LookTestCameraRig.FigureOnScreen(_config.LandscapeCamera, 1.65f);
            Assert.That(figure.x, Is.InRange(0.14f, 0.18f));
            Assert.That(figure.y, Is.InRange(0.25f, 0.31f));
            Assert.That(LookTestCameraRig.HorizonScreenY(_config.LandscapeCamera), Is.InRange(0.62f, 0.72f));
        }

        [Test]
        public void PortraitFramesPistaAsArtDirectionSection9()
        {
            // About 12% of the screen height, feet about 22% from the bottom.
            Vector2 figure = LookTestCameraRig.FigureOnScreen(_config.PortraitCamera, 1.65f);
            Assert.That(figure.x, Is.InRange(0.10f, 0.14f));
            Assert.That(figure.y, Is.InRange(0.19f, 0.26f));
        }

        [Test]
        public void Spec101CameraWouldFramePistaTooLarge()
        {
            // Documents the open question for game-designer: spec 101's 5.5 m / 2.4 m / 9° / 55° camera.
            var spec = new LookTestCameraProfile(5.5f, 2.4f, 9f, 55f, 0.7f, 3.2f);
            Assert.Greater(LookTestCameraRig.FigureOnScreen(spec, 1.65f).x, 0.25f);
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
    }
}
