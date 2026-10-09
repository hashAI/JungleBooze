using System;
using JungleBooze.Core;
using JungleBooze.Editor.Expedition;
using JungleBooze.Editor.FeelTest;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.Tests.EditMode.Movement;
using JungleBooze.Tests.EditMode.World;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.CameraRig
{
    /// <summary>
    /// Vine framing (spec 103 §11, slice follow-up 2026-10-09): through grab, swing, release, the air arc and the
    /// landing, Pista (feet to raised hands) stays inside a safe frame region in both orientations, on the shipped
    /// Expedition 1 course with the shipped camera assets and the scene's camera-target mapping, for a Perfect
    /// release and for the late auto-release (the steepest launch). The camera stays smooth: bounded per-frame
    /// acceleration, no pops.
    /// </summary>
    public sealed class VineFramingTests
    {
        // Safe region [ASSUMED 2026-10-09]: 6 % margin top/bottom, 8 % left/right.
        private const float SafeTop = 0.94f;
        private const float SafeBottom = 0.06f;
        private const float SafeSide = 0.08f;

        /// <summary>Raised hands while hanging (vine HandToFeet 1.9 m) and standing height.</summary>
        private const float BodyTop = 1.95f;

        /// <summary>Max camera acceleration, m/s² (no pops; a 0.2 s spring on a 10 m arc stays well under).</summary>
        private const float MaxAccel = 60f;

        /// <summary>Max pitch rate, deg/s.</summary>
        private const float MaxPitchRate = 45f;

        private sealed class LateRelease : IInputProvider
        {
            private readonly IInputProvider _inner;
            private readonly RunnerSimulation _sim;

            public LateRelease(IInputProvider inner, RunnerSimulation sim)
            {
                _inner = inner;
                _sim = sim;
            }

            public InputFrame ReadInput(long tick)
            {
                InputFrame f = _inner.ReadInput(tick);
                return _sim.State.Mode == MoveMode.Swing ? InputFrame.Empty : f;
            }
        }

        [TestCase(2532, 1170, false)]
        [TestCase(1170, 2532, false)]
        [TestCase(2532, 1170, true)]
        [TestCase(1170, 2532, true)]
        [TestCase(1920, 1080, false)]
        [TestCase(1080, 1920, false)]
        public void PistaStaysInTheSafeFrame_ThroughGrabSwingReleaseAirAndLanding(int width, int height, bool lateRelease)
        {
            bool landscape = width >= height;
            CameraProfile profile = ShippedAssets.Load<CameraProfileAsset>(landscape ? FeelTestPaths.CameraLandscape : FeelTestPaths.CameraPortrait).Values;
            CameraModifiers mods = ShippedAssets.Load<CameraModifiersAsset>(ExpeditionPaths.CameraModifiers).Values.Clone();
            MovementConfig config = ShippedAssets.Config();
            float aspect = (float)width / height;

            ExpeditionSession session = ShippedContent.Session();
            session.BeginRun(ShippedContent.FirstRun());
            PerfectBot bot = ShippedContent.Bot(session, RouteType.Secret, RouteType.Risky, RouteType.Safe);
            IInputProvider input = lateRelease ? new LateRelease(bot, session.Simulation) : (IInputProvider)bot;
            var rig = new CameraRigModel(profile, config.Speed.V0, config.Speed.VMax, config.Lateral.VLatMax) { Modifiers = mods };
            WorldPath path = session.Path;
            rig.Snap(ExpeditionCameraTarget.From(session.Simulation.State, path));

            const float dt = 1f / 60f;
            int grabs = 0;
            int framesChecked = 0;
            int settle = 0;
            bool inSequence = false;
            float minBottom = 1f;
            float maxTop = 0f;
            float maxSide = 0f;
            float maxAccel = 0f;
            float maxPitchRate = 0f;
            float minFollow = float.MaxValue;
            float prevY = 0f;
            float prevVy = 0f;
            float prevPitch = 0f;
            int seqFrame = 0;
            string worst = string.Empty;
            for (int t = 0; t < 60 * 60 * 4 && session.Phase != RunPhase.Results && grabs < 3; t++)
            {
                session.Step(input.ReadInput(session.Run.SessionTick));
                for (int i = 0; i < session.Events.Count; i++)
                {
                    if (session.Events[i].Type == RunEventType.VineGrab)
                    {
                        grabs++;
                        inSequence = true;
                        settle = 0;
                        seqFrame = 0;
                    }
                }

                session.Events.Clear();
                RunnerState s = session.Simulation.State;
                CameraPose pose = rig.Update(ExpeditionCameraTarget.From(s, path), dt);
                if (s.Dead)
                {
                    break;
                }

                if (!inSequence)
                {
                    continue;
                }

                // Sequence: grab → swing → release → air → landing + 0.5 s.
                if (s.Mode == MoveMode.Run && s.Grounded && !s.VineAir && ++settle > 30)
                {
                    inSequence = false;
                    continue;
                }

                Matrix4x4 vp = ViewProjection(path, pose, aspect, profile);
                PathFrame at = path.GetFrame(s.S);
                at.Offset(s.X, out float px, out float pz);
                Vector3 feet = new Vector3(px, s.Y, pz);
                Vector3 top = new Vector3(px, s.Y + BodyTop, pz);
                Vector2 f = ToScreen(vp, feet);
                Vector2 h = ToScreen(vp, top);
                float bottom = Mathf.Min(f.y, h.y);
                float upper = Mathf.Max(f.y, h.y);
                float side = Mathf.Max(Mathf.Abs(f.x - 0.5f), Mathf.Abs(h.x - 0.5f));
                if (upper > maxTop)
                {
                    maxTop = upper;
                    worst = "top " + upper.ToString("0.000") + " at s " + s.S.ToString("0.0") + " y " + s.Y.ToString("0.00") + " mode " + s.Mode + " vineAir " + s.VineAir;
                }

                minBottom = Mathf.Min(minBottom, bottom);
                minFollow = Mathf.Min(minFollow, s.S - pose.S);
                maxSide = Mathf.Max(maxSide, side);
                if (seqFrame >= 2)
                {
                    float vy = (pose.Y - prevY) / dt;
                    maxAccel = Mathf.Max(maxAccel, Mathf.Abs(vy - prevVy) / dt);
                    prevVy = vy;
                    maxPitchRate = Mathf.Max(maxPitchRate, Mathf.Abs(pose.PitchDeg - prevPitch) / dt);
                }
                else if (seqFrame == 1)
                {
                    prevVy = (pose.Y - prevY) / dt;
                }

                prevY = pose.Y;
                prevPitch = pose.PitchDeg;
                seqFrame++;
                framesChecked++;
            }

            Debug.Log("[JungleBooze] vine framing " + width + "x" + height + (lateRelease ? " late release" : " perfect") + ": " + grabs + " grabs, " + framesChecked +
                      " frames, screen y " + minBottom.ToString("0.000") + "…" + maxTop.ToString("0.000") + ", side " + (0.5f + maxSide).ToString("0.000") +
                      ", camera accel " + maxAccel.ToString("0.0") + " m/s², pitch rate " + maxPitchRate.ToString("0.0") + "°/s, min follow " + minFollow.ToString("0.00") + " m; worst " + worst);
            Assert.GreaterOrEqual(grabs, 2, "V1 and V2");
            Assert.IsFalse(session.Simulation.State.Dead, "the sequence completes");
            Assert.LessOrEqual(maxTop, SafeTop, "Pista's hands stay below the top safe edge; " + worst);
            Assert.GreaterOrEqual(minBottom, SafeBottom, "Pista's feet stay above the bottom safe edge");
            Assert.LessOrEqual(0.5f + maxSide, 1f - SafeSide, "Pista stays inside the side safe edges");
            Assert.GreaterOrEqual(minFollow, profile.OffsetBack - 0.25f, "the release look-ahead aims ahead, it never pulls the camera in");
            Assert.LessOrEqual(maxAccel, MaxAccel, "camera height has no pops");
            Assert.LessOrEqual(maxPitchRate, MaxPitchRate, "camera pitch moves smoothly");
        }

        private static Matrix4x4 ViewProjection(WorldPath path, in CameraPose pose, float aspect, CameraProfile profile)
        {
            // Same mapping as ExpeditionRoot.ApplyCamera: path space → world through the frame at the camera's s.
            PathFrame f = path.GetFrame(pose.S);
            f.Offset(pose.X + pose.ShakeX, out float wx, out float wz);
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(new Vector3(wx, pose.Y + pose.ShakeY, wz), CameraMath.Rotation(pose), Vector3.one).inverse;
            return Matrix4x4.Perspective(pose.FovDeg, aspect, profile.NearClip, profile.FarClip) * view;
        }

        private static Vector2 ToScreen(in Matrix4x4 vp, Vector3 p)
        {
            Vector4 c = vp * new Vector4(p.x, p.y, p.z, 1f);
            if (c.w <= 0f)
            {
                return new Vector2(float.NaN, 2f);
            }

            return new Vector2(((c.x / c.w) + 1f) * 0.5f, ((c.y / c.w) + 1f) * 0.5f);
        }
    }
}
