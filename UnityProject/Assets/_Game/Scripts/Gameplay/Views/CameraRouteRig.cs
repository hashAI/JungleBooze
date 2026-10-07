using System;
using JungleBooze.Gameplay.Path;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Puts <see cref="CameraRouteModel"/> on a <see cref="PathFrame"/>: samples the route for the headings, pitch, bank
    /// and curvature the model needs, builds the camera position (on the route behind HERO, in the banked frame) and
    /// rotation (yaw, pitch, roll), and feeds the model the probed path points for the screen-edge safety clamp.
    /// Not a MonoBehaviour, so EditMode tests drive it directly (AC-305/307/314/315/316). No allocations per frame.
    /// </summary>
    public sealed class CameraRouteRig
    {
        private static readonly float[] ProbeDistancesM = { 10f, 25f, 40f };

        private readonly RunnerPresentationConfig _config;
        private readonly CameraRouteTuning _tuning;
        private readonly CameraRouteModel _model;
        private readonly float[] _points = new float[12];

        public CameraRouteRig(RunnerPresentationConfig config, CameraRouteTuning tuning)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _model = new CameraRouteModel(config, tuning);
            Rotation = Quaternion.identity;
            FovDeg = config.CameraFovDeg;
        }

        public CameraRouteModel Model => _model;

        public CameraRouteTuning Tuning => _tuning;

        /// <summary>Camera position in WorldRoot space.</summary>
        public Vector3 Position { get; private set; }

        /// <summary>Camera rotation in WorldRoot space.</summary>
        public Quaternion Rotation { get; private set; }

        /// <summary>Vertical field of view in degrees.</summary>
        public float FovDeg { get; private set; }

        /// <summary>Snaps the camera to the hero (run start, restart).</summary>
        public void Reset(PathFrame frame, in CameraRouteInput input)
        {
            CameraRouteInput work = Sample(frame, input);
            _model.Reset(work);
            Place(frame, work, 0f);
            FovDeg = _model.FovDeg;
        }

        /// <summary>One frame. <paramref name="dt"/> is the snapped real delta; <paramref name="shakeX"/> is the stumble shake (0 with Reduce Motion).</summary>
        public void Update(PathFrame frame, float dt, in CameraRouteInput input, float shakeX)
        {
            CameraRouteInput work = Sample(frame, input);
            _model.Advance(dt, work);
            Place(frame, work, shakeX);
            FillProbePoints(frame, input.HeroS);
            _model.ApplySafety(Position.x, Position.z, _points, 6);
            FovDeg = _model.FovDeg;
        }

        /// <summary>The probed points (world x, z pairs; 10, 25 and 40 m ahead, outer lane centers) of the last update. Tests only.</summary>
        public float[] ProbePoints => _points;

        private CameraRouteInput Sample(PathFrame frame, in CameraRouteInput input)
        {
            CameraRouteInput work = input;
            double s = input.HeroS;
            frame.Sample(s + _tuning.YawNearM, out PathPose near);
            frame.Sample(s + _tuning.YawFarM, out PathPose far);
            frame.Sample(s, out PathPose hero);
            work.AimNearYawRad = Mathf.Atan2(near.Tangent.x, near.Tangent.z);
            work.AimFarYawRad = Mathf.Atan2(far.Tangent.x, far.Tangent.z);
            work.RoutePitchRad = Mathf.Atan(near.GradePct * 0.01f);
            work.BankDeg = hero.BankDeg;
            work.CurvatureAbs = Mathf.Max(Mathf.Abs(hero.Curvature), Mathf.Abs(far.Curvature));
            return work;
        }

        private void Place(PathFrame frame, in CameraRouteInput work, float shakeX)
        {
            frame.Sample(work.HeroS - _model.BehindM, out PathPose behind);
            float lateral = _model.CamX + shakeX + _model.SideM;
            float up = _config.CameraOffsetUpM + _model.UpExtraM + _model.CamY;
            Position = PathPlacement.Point(behind, lateral, up);
            Rotation = Quaternion.Euler(-_model.PitchDeg, _model.YawRad * Mathf.Rad2Deg, -_model.RollDeg);
        }

        private void FillProbePoints(PathFrame frame, double heroS)
        {
            int n = 0;
            for (int i = 0; i < ProbeDistancesM.Length; i++)
            {
                frame.Sample(heroS + ProbeDistancesM[i], out PathPose pose);
                Vector3 left = pose.Center - (pose.Right * _tuning.SafetyLaneOffsetM);
                Vector3 right = pose.Center + (pose.Right * _tuning.SafetyLaneOffsetM);
                _points[n++] = left.x;
                _points[n++] = left.z;
                _points[n++] = right.x;
                _points[n++] = right.z;
            }
        }
    }
}
