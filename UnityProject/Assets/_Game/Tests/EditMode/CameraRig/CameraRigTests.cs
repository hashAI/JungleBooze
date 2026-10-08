using System;
using System.Collections.Generic;
using JungleBooze.Core;
using JungleBooze.Editor.FeelTest;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Tests.EditMode.Movement;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.CameraRig
{
    /// <summary>Spec 101 §5: AC-101-41 … AC-101-44.</summary>
    public sealed class CameraRigTests
    {
        private const float V0 = 10f;
        private const float VMax = 16f;
        private const float VLatMax = 11f;

        private static IEnumerable<CameraProfile> Profiles()
        {
            yield return ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraLandscape).Values;
            yield return ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraPortrait).Values;
        }

        [Test]
        public void ShippedProfilesMatchSpec101()
        {
            CameraProfile land = ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraLandscape).Values;
            CameraProfile port = ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraPortrait).Values;
            Assert.AreEqual(JsonUtility.ToJson(new CameraProfile()), JsonUtility.ToJson(land));
            Assert.AreEqual(JsonUtility.ToJson(CameraProfile.DefaultPortrait()), JsonUtility.ToJson(port));
        }

        [Test]
        public void AC41_ConvergesToProfileOffsetsAtConstantSpeed()
        {
            foreach (CameraProfile p in Profiles())
            {
                foreach (float speed in new[] { 10f, 13f, 16f })
                {
                    var rig = new CameraRigModel(p, V0, VMax, VLatMax);
                    var target = new CameraTargetInput { S = 0f, X = 1f, Y = 0.5f, GroundY = 0.5f, Speed = speed };
                    rig.Snap(target);
                    target.Speed = speed;
                    CameraPose pose = default;
                    for (int i = 0; i < 600; i++)
                    {
                        target.S += speed / 60f;
                        pose = rig.Update(target, 1f / 60f);
                    }

                    Assert.AreEqual(target.S - p.OffsetBack, pose.S, 0.01f, p.Name);
                    Assert.AreEqual(p.LateralFollow * 1f, pose.X, 0.01f, p.Name);
                    Assert.AreEqual(0.5f + p.Height, pose.Y, 0.01f, p.Name);
                    Assert.AreEqual(p.PitchDeg, pose.PitchDeg, 0.1f, p.Name);
                    float fov = p.FovBaseDeg + (p.FovGainDeg * (speed - V0) / (VMax - V0));
                    Assert.AreEqual(fov, pose.FovDeg, 0.1f, p.Name + " FOV at " + speed);
                    if (speed == VMax)
                    {
                        Assert.AreEqual(p.FovBaseDeg + p.FovGainDeg, pose.FovDeg, 0.1f, "base + gain at vMax");
                    }
                }
            }
        }

        private static CameraTargetInput Trajectory(double t)
        {
            var target = new CameraTargetInput();
            double speed = 12.0 + (2.0 * Math.Sin(t * 0.7));
            target.S = (float)(12.0 * t);
            target.Speed = (float)speed;
            target.X = (float)(2.5 * Math.Sin(2.0 * Math.PI * t / 1.5));
            target.VLat = (float)(2.5 * 2.0 * Math.PI / 1.5 * Math.Cos(2.0 * Math.PI * t / 1.5));
            double phase = t % 1.2;
            target.GroundY = (float)(0.2 * Math.Sin(t * 0.9));
            target.Y = target.GroundY + (phase < 0.6 ? (float)((9.33 * phase) - (15.55 * phase * phase)) : 0f);
            return target;
        }

        private static List<CameraPose> Drive(int hz, double seconds)
        {
            var rig = new CameraRigModel(new CameraProfile(), V0, VMax, VLatMax);
            rig.Snap(Trajectory(0.0));
            var samples = new List<CameraPose>();
            int frames = (int)Math.Round(seconds * hz);
            int every = hz / 30;
            for (int i = 1; i <= frames; i++)
            {
                CameraPose pose = rig.Update(Trajectory((double)i / hz), 1f / hz);
                if (i % every == 0)
                {
                    samples.Add(pose);
                }
            }

            return samples;
        }

        [Test]
        public void AC42_SameResultAt30_60_120Hz()
        {
            List<CameraPose> a = Drive(30, 6.0);
            List<CameraPose> b = Drive(60, 6.0);
            List<CameraPose> c = Drive(120, 6.0);
            Assert.AreEqual(a.Count, c.Count);
            float worst = 0f;
            for (int i = 0; i < a.Count; i++)
            {
                worst = Math.Max(worst, Math.Abs(a[i].X - c[i].X));
                worst = Math.Max(worst, Math.Abs(a[i].Y - c[i].Y));
                worst = Math.Max(worst, Math.Abs(b[i].X - c[i].X));
                worst = Math.Max(worst, Math.Abs(b[i].Y - c[i].Y));
                Assert.AreEqual(c[i].FovDeg, a[i].FovDeg, 0.1f);
                Assert.AreEqual(c[i].RollDeg, a[i].RollDeg, 0.1f);
            }

            Debug.Log("[JungleBooze] AC-101-42 worst 30/60 vs 120 Hz position difference: " + worst.ToString("0.0000") + " m");
            Assert.LessOrEqual(worst, 0.01f);
        }

        [Test]
        public void AC43_ShakeIsCappedAndReducedMotionRemovesShakeAndBank()
        {
            var rig = new CameraRigModel(new CameraProfile(), V0, VMax, VLatMax);
            var target = new CameraTargetInput { Speed = 16f, VLat = 11f };
            rig.Snap(target);
            float maxPos = 0f;
            float maxRot = 0f;
            for (int i = 0; i < 600; i++)
            {
                if (i % 7 == 0)
                {
                    rig.ShakeCrash();
                }

                if (i % 5 == 0)
                {
                    rig.ShakeMinorHit();
                }

                CameraPose pose = rig.Update(target, i % 3 == 0 ? 1f / 30f : 1f / 120f);
                maxPos = Math.Max(maxPos, (float)Math.Sqrt((pose.ShakeX * pose.ShakeX) + (pose.ShakeY * pose.ShakeY)));
                maxRot = Math.Max(maxRot, Math.Abs(pose.ShakeRollDeg));
            }

            Assert.LessOrEqual(maxPos, 0.12f + 1e-6f);
            Assert.LessOrEqual(maxRot, 1.0f + 1e-6f);
            Assert.Greater(maxPos, 0.02f, "shake actually happened");

            rig.ReducedMotion = true;
            for (int i = 0; i < 120; i++)
            {
                rig.ShakeCrash();
                CameraPose pose = rig.Update(target, 1f / 60f);
                if (i > 60)
                {
                    Assert.AreEqual(0f, pose.ShakeX);
                    Assert.AreEqual(0f, pose.ShakeY);
                    Assert.AreEqual(0f, pose.ShakeRollDeg);
                    Assert.AreEqual(0f, pose.RollDeg, "bank exactly 0");
                }
            }
        }

        [Test]
        public void ReducedMotionHalvesFovGainAndSlideDip()
        {
            CameraProfile p = new CameraProfile();
            var rig = new CameraRigModel(p, V0, VMax, VLatMax) { ReducedMotion = true };
            var target = new CameraTargetInput { Speed = 16f, Sliding = true };
            rig.Snap(target);
            CameraPose pose = default;
            for (int i = 0; i < 600; i++)
            {
                pose = rig.Update(target, 1f / 60f);
            }

            Assert.AreEqual(p.FovBaseDeg + (p.FovGainDeg * 0.5f), pose.FovDeg, 0.05f);
            Assert.AreEqual(p.Height - (p.SlideDip * 0.5f), pose.Y, 0.01f);
        }

        [Test]
        public void OrientationChangeBlendsOverBlendTime()
        {
            CameraProfile land = new CameraProfile();
            CameraProfile port = CameraProfile.DefaultPortrait();
            var rig = new CameraRigModel(land, V0, VMax, VLatMax);
            var target = new CameraTargetInput { Speed = 10f };
            rig.Snap(target);
            rig.SetProfile(port, false);
            CameraPose mid = rig.Update(target, 0.2f);
            Assert.Greater(mid.PitchDeg, land.PitchDeg);
            Assert.Less(mid.PitchDeg, port.PitchDeg);
            CameraPose end = rig.Update(target, 0.25f);
            Assert.AreEqual(port.PitchDeg, end.PitchDeg, 1e-4f);
        }

        [TestCase(2532, 1170)]
        [TestCase(1170, 2532)]
        public void AC44_EveryObstacleVisible15sBeforeContactAtVMax(int width, int height)
        {
            bool landscape = width >= height;
            CameraProfile profile = landscape
                ? ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraLandscape).Values
                : ShippedAssets.Load<CameraProfileAsset>(FeelTestPaths.CameraPortrait).Values;
            float aspect = (float)width / height;
            MovementConfig config = ShippedAssets.Config();
            var course = new CoursePath(ShippedAssets.Course());
            var sim = new RunnerSimulation(config, course, 1f / 60f, new RunEventBuffer(256));
            sim.Reset(new RunOptions { ForcedSpeed = VMax, SkipStartRamp = true });
            var bot = new PerfectBot(sim, false);
            var rig = new CameraRigModel(profile, config.Speed.V0, config.Speed.VMax, config.Lateral.VLatMax);
            rig.Snap(Target(sim.State));
            var checkedIds = new bool[course.ObstacleCount];
            var failures = new List<string>();
            float lead = 1.5f * VMax;
            float halfDepth = config.Hitbox.RunDepth * 0.5f;
            while (!sim.State.Finished && !sim.State.Dead)
            {
                sim.Step(bot.ReadInput(sim.State.Tick));
                sim.Events.Clear();
                CameraPose pose = rig.Update(Target(sim.State), 1f / 60f);
                Matrix4x4 vp = CameraMath.ViewProjection(pose, aspect, profile.NearClip, profile.FarClip);
                for (int id = 0; id < course.ObstacleCount; id++)
                {
                    ObstacleBox box = course.GetObstacle(id);
                    if (checkedIds[id] || sim.State.S + halfDepth < box.SMin - lead)
                    {
                        continue;
                    }

                    checkedIds[id] = true;
                    var front = new Vector3(Mathf.Clamp(box.CenterX, -3.5f, 3.5f), (box.YMin + Math.Min(box.YMax, box.YMin + 1.5f)) * 0.5f, box.SMin);
                    if (!CameraMath.Contains(vp, front))
                    {
                        failures.Add(course.GetObstacleLabel(id));
                    }
                }
            }

            Assert.IsTrue(sim.State.Finished);
            CollectionAssert.IsEmpty(failures, "not on screen 1.5 s before contact (" + (landscape ? "landscape" : "portrait") + ")");
        }

        [TestCase(2532, 1170)]
        [TestCase(1170, 2532)]
        public void PistaScreenFraction_Report(int width, int height)
        {
            bool landscape = width >= height;
            CameraProfile profile = landscape ? new CameraProfile() : CameraProfile.DefaultPortrait();
            var rig = new CameraRigModel(profile, V0, VMax, VLatMax);
            var target = new CameraTargetInput { S = 100f, Speed = V0 };
            rig.Snap(target);
            CameraPose pose = rig.Update(target, 1f / 60f);
            Matrix4x4 vp = CameraMath.ViewProjection(pose, (float)width / height, profile.NearClip, profile.FarClip);
            float feet = CameraMath.ScreenY(vp, new Vector3(0f, 0f, 100f));
            float head = CameraMath.ScreenY(vp, new Vector3(0f, 1.65f, 100f));
            float ahead = CameraMath.ScreenY(vp, new Vector3(0f, 0f, 100f + (landscape ? 35f : 45f)));
            Debug.Log("[JungleBooze] camera " + profile.Name + ": Pista " + ((head - feet) * 100f).ToString("0") + "% of screen height, feet at " +
                      (feet * 100f).ToString("0") + "% from bottom, path " + (landscape ? 35 : 45) + " m ahead at " + (ahead * 100f).ToString("0") + "%");
            Assert.IsTrue(ahead > 0f && ahead < 1f, "path visible far ahead");
        }

        private static CameraTargetInput Target(in RunnerState s)
        {
            return new CameraTargetInput { S = s.S, X = s.X, Y = s.Y, GroundY = s.GroundY, VLat = s.VLat, Speed = s.Speed, Sliding = s.Sliding };
        }
    }
}
