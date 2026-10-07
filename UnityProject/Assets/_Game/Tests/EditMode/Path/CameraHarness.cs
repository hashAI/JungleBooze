using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Views;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.RouteCamera
{
    /// <summary>Drives a <see cref="CameraRouteRig"/> along a route without any scene: the hero advances at a set speed.</summary>
    public sealed class CameraHarness
    {
        public const float Dt = 1f / 60f;

        private readonly RunnerPresentationConfig _config;

        public CameraHarness(IRouteSource source, bool reduceMotion, double startS)
            : this(source, reduceMotion, startS, new CameraRouteTuning())
        {
        }

        public CameraHarness(IRouteSource source, bool reduceMotion, double startS, CameraRouteTuning tuning)
        {
            _config = RunnerPresentationConfig.CreateDefault();
            Tuning = tuning;
            ReduceMotion = reduceMotion;
            Frame = new PathFrame(RouteTuning.CreateDefault(), source);
            Frame.BeginRun(1UL, startS - Frame.Tuning.BehindM);
            Rig = new CameraRouteRig(_config, tuning);
            S = startS;
            Frame.Extend(S + Frame.Tuning.AheadM);
            Rig.Reset(Frame, Input(0f, false, 0f, false));
        }

        public PathFrame Frame { get; }

        public CameraRouteRig Rig { get; }

        public CameraRouteTuning Tuning { get; }

        public RunnerPresentationConfig Config => _config;

        public bool ReduceMotion { get; set; }

        /// <summary>Hero distance along the route.</summary>
        public double S { get; private set; }

        /// <summary>Hero lateral position of the last step.</summary>
        public float HeroX { get; private set; }

        public CameraRouteInput Input(float heroX, bool swinging, float swingAngleRad, bool boost)
        {
            return new CameraRouteInput
            {
                HeroX = heroX,
                HeroY = 0f,
                HeroS = S,
                Swinging = swinging,
                SwingAngleRad = swingAngleRad,
                OpenSide = swinging ? 1 : 0,
                Boost = boost,
                ReduceMotion = ReduceMotion,
            };
        }

        /// <summary>Advances the hero by one frame at <paramref name="speedMps"/> and updates the camera.</summary>
        public void Step(double speedMps, float heroX, bool swinging, float swingAngleRad, bool boost)
        {
            S += speedMps * Dt;
            HeroX = heroX;
            Frame.Extend(S + Frame.Tuning.AheadM);
            Rig.Update(Frame, Dt, Input(heroX, swinging, swingAngleRad, boost), 0f);
        }

        /// <summary>
        /// Ratio of the horizontal screen position of a path point to the half-width of the viewport (1 = at the edge),
        /// seen through the actual camera position, rotation and FOV. Points behind the camera give a huge value.
        /// </summary>
        public float EdgeRatio(double s, float lateral)
        {
            Vector3 p = Frame.ToWorld(s, lateral, 0f);
            Vector3 local = Quaternion.Inverse(Rig.Rotation) * (p - Rig.Position);
            if (local.z <= 0.01f)
            {
                return float.MaxValue;
            }

            float halfTan = Mathf.Tan(Rig.FovDeg * 0.5f * Mathf.Deg2Rad) * Tuning.AspectWidthOverHeight;
            return Mathf.Abs(local.x / local.z) / halfTan;
        }

        /// <summary>Worst <see cref="EdgeRatio"/> over 10, 25 and 40 m ahead and the lanes -2.4, 0 and 2.4.</summary>
        public float WorstEdgeRatio()
        {
            float worst = 0f;
            float[] distances = { 10f, 25f, 40f };
            float[] lanes = { -2.4f, 0f, 2.4f };
            for (int i = 0; i < distances.Length; i++)
            {
                for (int j = 0; j < lanes.Length; j++)
                {
                    worst = Mathf.Max(worst, EdgeRatio(S + distances[i], lanes[j]));
                }
            }

            return worst;
        }

        /// <summary>Horizontal bearing (rad) of a path point in the camera frame.</summary>
        public float Bearing(double s, float lateral)
        {
            Vector3 p = Frame.ToWorld(s, lateral, 0f);
            Vector3 local = Quaternion.Inverse(Rig.Rotation) * (p - Rig.Position);
            return Mathf.Atan2(local.x, local.z);
        }
    }
}
