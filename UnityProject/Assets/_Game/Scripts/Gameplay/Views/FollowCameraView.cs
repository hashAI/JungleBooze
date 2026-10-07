using JungleBooze.Core;
using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using UnityEngine;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Portrait follow camera (spec 001 section 10, spec 003 section 6). The math lives in <see cref="CameraRouteModel"/>
    /// and <see cref="CameraRouteRig"/> (plain C#, EditMode-tested); this view feeds them the interpolated run state and
    /// applies the result. On the straight route the camera is exactly the old one: 6 m behind, 3.2 m up, looking
    /// 8 m ahead at 1 m, lateral follow 70 percent (90 ms), vertical 25 percent (150 ms), FOV 60. On a curved route it
    /// leads into the bend (yaw), banks, follows the grade, widens the FOV on tight bends and keeps the next 40 m of
    /// path on screen. On a vine it pulls back (further while the hero swings past the pivot, so the branch tip 17 m up
    /// and the hero up to 8 m high both stay in the portrait frame), moves toward the open side of the canyon (opposite
    /// the anchor tree), rolls with the swing and widens the FOV.
    /// Stumble shake and every roll, FOV and pull-back effect are off with Reduce Motion (the yaw lead stays, at half
    /// the rate). Smoothing uses the snapped real delta the run driver passes in. Freezes during the death hit-pause.
    /// No allocations per frame.
    /// </summary>
    public sealed class FollowCameraView : MonoBehaviour, IRunView
    {
        private const float ShakeFrequencyHz = 30f;

        private Camera _camera;
        private RunnerPresentationConfig _config;
        private CameraRouteTuning _tuning;
        private CameraRouteRig _rig;
        private TrackRunWorld _world;
        private float _shakeLeft;
        private float _shakeClock;
        private PathFrame _frame;
        private int _openSideVineId;
        private int _openSideValue;

        public Camera Camera => _camera;

        /// <summary>
        /// Reduce Motion (from the save, live): no stumble shake, no roll, no FOV change, no swing pull-back or side
        /// offset; yaw lead kept at a lower rate. Starts from the config's value; the bootstrap overrides it with the
        /// save's setting. The shared config asset is never written.
        /// </summary>
        public bool ReduceMotion { get; set; }

        /// <summary>Smoothed lateral follow value (tests: AC-56).</summary>
        public float FollowX => _rig != null ? _rig.Model.CamX : 0f;

        /// <summary>The camera math (tests, tuning). Null before <see cref="Init"/>.</summary>
        public CameraRouteRig Rig => _rig;

        /// <summary>The route the camera follows (spec 003). Call before <see cref="Init"/>; default is the straight route.</summary>
        public void SetFrame(PathFrame frame)
        {
            _frame = frame;
        }

        /// <summary>Replaces the camera tuning (default: the spec start values). Call before <see cref="Init"/>.</summary>
        public void SetTuning(CameraRouteTuning tuning)
        {
            _tuning = tuning;
        }

        public void Init(Camera targetCamera, RunnerPresentationConfig config)
        {
            _frame = PathPlacement.OrIdentity(_frame);
            _tuning = _tuning ?? new CameraRouteTuning();
            _camera = targetCamera;
            _config = config;
            _rig = new CameraRouteRig(config, _tuning);
            ReduceMotion = config.ReduceMotion;
            _camera.fieldOfView = config.CameraFovDeg;
        }

        public void BeginRun(GameSession session)
        {
            _world = session.World as TrackRunWorld;
            _shakeLeft = 0f;
            _shakeClock = 0f;
            _openSideVineId = 0;
            _openSideValue = 0;
            CameraRouteInput input = BuildInput(session, 1f);
            _rig.Reset(_frame, input);
            Apply();
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

            CameraRouteInput input = BuildInput(session, alpha);
            _rig.Update(_frame, realDeltaSeconds, input, shakeX);
            Apply();
        }

        private CameraRouteInput BuildInput(GameSession session, float alpha)
        {
            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out float x, out float y, out double z);
            RunnerState state = runner.Current;
            bool carried = state.Locomotion == Locomotion.Carried;

            var input = new CameraRouteInput
            {
                HeroX = x,
                HeroY = y,
                HeroS = z,
                Swinging = carried || state.InVineFlight,
                SwingAngleRad = carried ? state.SwingAngleRad : 0f,
                OpenSide = OpenSideFor(runner, state, carried),
                FramePivot = carried,
                PivotAheadM = carried ? (float)(state.SwingPivotZ - z) : 0f,
                PivotHeightM = carried ? runner.Vines.PivotHeightM : 0f,
                Boost = _world != null && _world.PowerUps != null && _world.PowerUps.IsActive(PowerUpType.SpeedBoost),
                ReduceMotion = ReduceMotion,
            };
            return input;
        }

        /// <summary>
        /// Open side of the canyon: opposite the anchor tree of the vine (the tree is a pure function of the run seed, the
        /// chunk serial and the row, so this is the same tree <see cref="VineView"/> draws). Kept for the flight after the
        /// release. Without a track (tests) it is away from the vine's lane until a tree is known.
        /// </summary>
        private int OpenSideFor(RunnerSimulation runner, in RunnerState state, bool carried)
        {
            int vineId = carried ? state.VineId : (state.InVineFlight ? runner.ReleasedVineId : 0);
            if (vineId == 0)
            {
                return 0;
            }

            if (vineId == _openSideVineId)
            {
                return _openSideValue;
            }

            TrackSimulation track = _world != null ? _world.Track : null;
            if (track == null)
            {
                return carried ? OpenSideOf(state.VineLane) : 0;
            }

            int count = track.VineCount;
            for (int i = 0; i < count; i++)
            {
                ref readonly VineInstance v = ref track.GetVine(i);
                if (v.Id == vineId)
                {
                    SwingTree tree = SwingRigMath.PlaceTree(_frame.RunSeed, RandomStreamIds.Scenery, v.ChunkSerial, v.Row, runner.Config.LaneCenterX(v.Lane));
                    _openSideVineId = vineId;
                    _openSideValue = tree.OpenSide;
                    return _openSideValue;
                }
            }

            return 0;
        }

        /// <summary>Fallback open side when no track is available: away from the vine's lane (left lane: right; right lane: left; middle: right).</summary>
        private static int OpenSideOf(int vineLane)
        {
            return vineLane == 2 ? -1 : (vineLane >= 0 ? 1 : 0);
        }

        private void Apply()
        {
            _camera.transform.SetPositionAndRotation(_rig.Position, _rig.Rotation);
            _camera.fieldOfView = _rig.FovDeg;
        }
    }
}
