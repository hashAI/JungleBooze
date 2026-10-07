using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Portrait follow camera (spec 001 section 10): position (camX, camY + up, Z − behind), looking at
    /// (camX, lookAtHeight + camY, Z + lookAhead), both expressed in route space (spec 003 T3: the position is mapped
    /// through the <see cref="PathFrame"/> at s = Z − behind and the look point at s = Z + lookAhead, so on a curved
    /// route the camera sits behind HERO on the route and looks along it; on the straight route this is exactly the
    /// old placement; the look-ahead lead, bank and FOV work is T4). camX smooth-damps toward lateralFollow × X, camY toward
    /// verticalFollow × Y, in real frame time. Slides do not move it; it follows interpolated state; stumble shake
    /// (off with Reduce Motion). Vine swing (GDD 7.3 step 3): while on a vine and in the launch after it, the FOV
    /// eases to the swing FOV and the view tilts up (both off with Reduce Motion).
    /// Freezes during the death hit-pause and hold because the simulation stops.
    /// No allocations per frame.
    /// </summary>
    public sealed class FollowCameraView : MonoBehaviour, IRunView
    {
        private const float ShakeFrequencyHz = 30f;

        private Camera _camera;
        private RunnerPresentationConfig _config;
        private float _camX;
        private float _camY;
        private float _velX;
        private float _velY;
        private float _shakeLeft;
        private float _shakeClock;
        private float _swingBlend;
        private PathFrame _frame;

        public Camera Camera => _camera;

        /// <summary>
        /// Reduce Motion (from the save, live): no stumble shake, no swing tilt and no FOV swing. Starts from the
        /// config's value; the bootstrap overrides it with the save's setting. The shared config asset is never written.
        /// </summary>
        public bool ReduceMotion { get; set; }

        /// <summary>Smoothed lateral follow value (tests: AC-56).</summary>
        public float FollowX => _camX;

        /// <summary>The route the camera follows (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        public void Init(Camera targetCamera, RunnerPresentationConfig config)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _camera = targetCamera;
            _config = config;
            ReduceMotion = config.ReduceMotion;
            _camera.fieldOfView = config.CameraFovDeg;
        }

        public void BeginRun(GameSession session)
        {
            RunnerState state = session.Runner.Current;
            _camX = _config.CameraLateralFollow * state.X;
            _camY = _config.CameraVerticalFollow * state.Y;
            _velX = 0f;
            _velY = 0f;
            _shakeLeft = 0f;
            _shakeClock = 0f;
            _swingBlend = 0f;
            _camera.fieldOfView = _config.CameraFovDeg;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (e.Type == RunnerEventType.Stumbled && !ReduceMotion)
            {
                _shakeLeft = _config.StumbleShakeMs / 1000f;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_camera == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out float x, out float y, out double z);

            float targetX = _config.CameraLateralFollow * x;
            float targetY = _config.CameraVerticalFollow * (y > 0f ? y : 0f);
            if (realDeltaSeconds > 0f)
            {
                _camX = Mathf.SmoothDamp(_camX, targetX, ref _velX, _config.CameraLateralSmoothMs / 1000f, Mathf.Infinity, realDeltaSeconds);
                _camY = Mathf.SmoothDamp(_camY, targetY, ref _velY, _config.CameraVerticalSmoothMs / 1000f, Mathf.Infinity, realDeltaSeconds);
            }

            float shakeX = 0f;
            float shakeSeconds = _config.StumbleShakeMs / 1000f;
            if (ReduceMotion)
            {
                _shakeLeft = 0f;
            }

            if (_shakeLeft > 0f && shakeSeconds > 0f)
            {
                _shakeClock += realDeltaSeconds;
                _shakeLeft -= realDeltaSeconds;
                float fade = Mathf.Clamp01(_shakeLeft / shakeSeconds);
                shakeX = _config.StumbleShakeM * fade * Mathf.Sin(_shakeClock * ShakeFrequencyHz * 2f * Mathf.PI);
            }

            RunnerState state = runner.Current;
            bool swinging = state.Locomotion == Locomotion.Carried || state.InVineFlight;
            float swingTarget = swinging && !ReduceMotion ? 1f : 0f;
            float blendSeconds = _config.SwingCameraBlendMs / 1000f;
            if (realDeltaSeconds > 0f)
            {
                _swingBlend = blendSeconds > 0f
                    ? Mathf.MoveTowards(_swingBlend, swingTarget, realDeltaSeconds / blendSeconds)
                    : swingTarget;
            }

            float ease = _swingBlend * _swingBlend * (3f - 2f * _swingBlend);
            _camera.fieldOfView = Mathf.Lerp(_config.CameraFovDeg, _config.SwingCameraFovDeg, ease);

            Transform t = _camera.transform;
            _frame.Sample(z - _config.CameraOffsetBehindM, out PathPose behindPose);
            _frame.Sample(z + _config.CameraLookAheadM, out PathPose aheadPose);
            t.position = PathPlacement.Point(behindPose, _camX + shakeX, _camY + _config.CameraOffsetUpM);
            t.LookAt(PathPlacement.Point(aheadPose, _camX + shakeX, _config.CameraLookAtHeightM + _camY));
            if (ease > 0f)
            {
                t.rotation = t.rotation * Quaternion.Euler(-_config.SwingCameraTiltDeg * ease, 0f, 0f);
            }
        }
    }
}
