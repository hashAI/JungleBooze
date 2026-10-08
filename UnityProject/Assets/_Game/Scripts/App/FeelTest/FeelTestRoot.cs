using System;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.FeelTest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JungleBooze.App.FeelTest
{
    /// <summary>
    /// Composition root and frame driver of the Phase 1 feel test (spec 101). Builds the deterministic run
    /// (<see cref="RunSession"/> on the <see cref="FeelCourseAsset"/>), input (touch gestures, mouse, keyboard, or
    /// the Perfect bot), the gray-box course, the stand-in Pista (or the avatar prefab hook), the third-person
    /// camera rig and the HUD. Update is the only per-frame entry point (ARCHITECTURE §4): read input → step the
    /// fixed-step simulation → sync views with interpolation → camera → HUD. Keys: Esc pause, R restart,
    /// F1 debug, O camera profile (auto/landscape/portrait), B bot autoplay, M reduced motion.
    /// </summary>
    public sealed class FeelTestRoot : MonoBehaviour
    {
        public const int TargetFrameRate = 60;
        private const int StepsPerSecond = 60;
        private const int MaxStepsPerFrame = 5;
        private const ulong DefaultSeed = 20261009UL;

        private static readonly string[] Countdown = { string.Empty, "1", "2", "3" };
        private const string PausedText = "PAUSED";
        private const string ReadyText = "READY";

        [SerializeField] private MovementConfigAssets _movement = new MovementConfigAssets();
        [SerializeField] private GestureConfigAsset _gestures;
        [SerializeField] private CameraProfileAsset _landscape;
        [SerializeField] private CameraProfileAsset _portrait;
        [SerializeField] private FeelCourseAsset _course;
        [SerializeField] private FeelTestPalette _palette;
        [SerializeField] private Camera _camera;

        [Tooltip("Optional rigged Pista prefab with AnimatedRunnerAvatar. Empty = gray-box capsule.")]
        [SerializeField] private RunnerAvatar _avatarPrefab;

        [Tooltip("First run of the player's life: health floors at 1 for 60 s (spec 101 §4.2).")]
        [SerializeField] private bool _firstRun;

        [SerializeField] private bool _reducedMotion;

        [SerializeField] private bool _startWithBot;

        [SerializeField] private bool _botPrefersRiskyBranch;

        private MovementConfig _config;
        private CoursePath _path;
        private FixedStepTimeSource _time;
        private RunEventBuffer _events;
        private RunSession _session;
        private GestureRecognizer _gestureRecognizer;
        private FrameInputDispatcher _dispatcher;
        private UnityPointerInput _pointer;
        private PerfectBot _bot;
        private InputRecording _recording;
        private ulong _configHash;
        private CourseView _courseView;
        private RunnerView _runnerView;
        private DebugHitboxView _debugView;
        private CameraRigModel _rig;
        private FeelTestHud _hud;
        private readonly StringBuilder _debugText = new StringBuilder(512);
        private readonly int[] _droppedTicks = new int[5];
        private readonly byte[] _droppedReasons = new byte[5];
        private int _droppedCount;
        private bool _built;
        private bool _paused;
        private float _resumeCountdown;
        private bool _botDriving;
        private bool _debugVisible;
        private int _orientationMode; // 0 auto, 1 landscape, 2 portrait
        private bool _landscapeActive = true;
        private bool _resultsShown;
        private int _edgeBrushTicks;
        private float _debugRefresh;
        private int _hitLogCount;
        private readonly int[] _hitLogIds = new int[8];
        private readonly byte[] _hitLogKinds = new byte[8];

        public RunSession Session => _session;

        public RunnerSimulation Simulation => _session?.Simulation;

        public bool DebugVisible => _debugVisible;

        public bool BotDriving => _botDriving;

        public DebugHitboxView DebugView => _debugView;

        public FeelTestHud Hud => _hud;

        public CameraRigModel CameraRig => _rig;

        public Camera Camera => _camera;

        /// <summary>Called by the editor scene builder to fill in the scene references.</summary>
        public void Configure(MovementConfigAssets movement, GestureConfigAsset gestures, CameraProfileAsset landscape, CameraProfileAsset portrait, FeelCourseAsset course, FeelTestPalette palette, Camera targetCamera)
        {
            _movement = movement;
            _gestures = gestures;
            _landscape = landscape;
            _portrait = portrait;
            _course = course;
            _palette = palette;
            _camera = targetCamera;
        }

        /// <summary>Builds the run, views and HUD. Called from Start, or directly by tools (screenshots, tests).</summary>
        public void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            Application.targetFrameRate = TargetFrameRate;
            _config = _movement.Build();
            _configHash = MovementConfigAssets.Mix(_movement.ComputeHash(), JsonUtility.ToJson(_course.Course));
            _path = new CoursePath(_course.Course);
            _time = new FixedStepTimeSource(StepsPerSecond, MaxStepsPerFrame);
            _events = new RunEventBuffer(256);
            _session = new RunSession(_config, _path, _time, _events);

            _gestureRecognizer = new GestureRecognizer(_gestures.Values);
            _dispatcher = new FrameInputDispatcher(_gestureRecognizer, _gestures.Values.CommandQueueSize);
            _pointer = new UnityPointerInput(_gestureRecognizer, _dispatcher, _gestures.Values);
            _bot = new PerfectBot(_session.Simulation, _botPrefersRiskyBranch);
            _botDriving = _startWithBot;
            _recording = new InputRecording(DefaultSeed, _configHash, Application.version, 4096);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var world = new GameObject("FeelCourse");
            world.transform.SetParent(transform, false);
            _courseView = world.AddComponent<CourseView>();
            _courseView.Build(_path, _palette, font);

            RunnerAvatar avatar = _avatarPrefab != null
                ? Instantiate(_avatarPrefab, transform)
                : CapsuleRunnerAvatar.Create(transform, _palette.Runner, _palette.RunnerAccent);
            _runnerView = new RunnerView(_session.Simulation, avatar);

            var debug = new GameObject("DebugHitboxes");
            debug.transform.SetParent(transform, false);
            _debugView = debug.AddComponent<DebugHitboxView>();
            _debugView.Build(_session.Simulation, _path, _palette);

            _landscapeActive = Screen.width >= Screen.height;
            _rig = new CameraRigModel(_landscapeActive ? _landscape.Values : _portrait.Values, _config.Speed.V0, _config.Speed.VMax, _config.Lateral.VLatMax)
            {
                ReducedMotion = _reducedMotion,
            };

            if (Application.isPlaying)
            {
                EnsureEventSystem();
                var hudObject = new GameObject("FeelTestHud");
                hudObject.transform.SetParent(transform, false);
                _hud = hudObject.AddComponent<FeelTestHud>();
                _hud.Build(font, _config.Health.MaxHealth, TogglePause, Restart);
                _hud.SetHint(Application.isMobilePlatform ? string.Empty : "drag/A D steer · W/Space jump · S slide · Q/E dodge · Esc pause · R restart · F1 debug · O camera · B bot · M reduced motion");
                _pointer.IsOverUi = (id, pixel) => _hud != null && _hud.HitsControl(pixel);
                _pointer.Enable();
            }

            Restart();
        }

        /// <summary>Instant restart: new run at Ready.</summary>
        public void Restart()
        {
            _session.Restart(new RunOptions { FirstRun = _firstRun });
            _time.Reset();
            _dispatcher.Clear();
            _gestureRecognizer.Reset();
            _bot.Reset();
            _recording.Clear();
            _courseView.ResetRun();
            _runnerView.ResetRun();
            _events.Clear();
            _paused = false;
            _resumeCountdown = 0f;
            _resultsShown = false;
            _edgeBrushTicks = 0;
            _droppedCount = 0;
            _hitLogCount = 0;
            _runnerView.Sync(1f, 0f);
            _rig.Snap(CameraTarget(_runnerView.Interpolated));
            ApplyCamera(_rig.Pose);
            if (_hud != null)
            {
                _hud.HideResults();
                _hud.SetCenter(ReadyText);
                _hud.SetDistance(0f);
            }
        }

        /// <summary>Esc / pause button: freeze, or resume after the ready beat (spec 101 §9).</summary>
        public void TogglePause()
        {
            if (_session.Phase == RunPhase.Results)
            {
                return;
            }

            if (!_paused)
            {
                _paused = true;
                _resumeCountdown = 0f;
            }
            else if (_resumeCountdown <= 0f)
            {
                _resumeCountdown = _config.Flow.ResumeReadyTime;
            }
        }

        public void SetDebugVisible(bool visible)
        {
            _debugVisible = visible;
            _debugView.SetVisible(visible);
            if (_hud != null)
            {
                _hud.SetDebugVisible(visible);
            }
        }

        /// <summary>Tools: force a camera profile (no blend when instant).</summary>
        public void SetCameraProfile(bool landscape, bool instant)
        {
            _orientationMode = landscape ? 1 : 2;
            _landscapeActive = landscape;
            _rig.SetProfile(landscape ? _landscape.Values : _portrait.Values, instant);
        }

        public void SetBotDriving(bool driving)
        {
            _botDriving = driving;
        }

        /// <summary>Steps the run by whole ticks with the active input (tests and tools; bypasses real time).</summary>
        public void StepTicks(int ticks)
        {
            _dispatcher.BeginFrame(ticks);
            for (int i = 0; i < ticks; i++)
            {
                StepOnce();
            }

            Present(1f, ticks / (float)StepsPerSecond);
        }

        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            _pointer?.Dispose();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _built && !_paused && _session.Phase != RunPhase.Results)
            {
                TogglePause();
            }
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            float frame = Time.unscaledDeltaTime;
            HandleKeys();
            UpdateOrientation();
            _pointer.Enabled = !_botDriving && !_paused;
            _pointer.PollFrame(frame, Time.realtimeSinceStartupAsDouble);

            if (_paused)
            {
                _dispatcher.BeginFrame(0);
                if (_resumeCountdown > 0f)
                {
                    _resumeCountdown -= frame;
                    if (_resumeCountdown <= 0f)
                    {
                        _paused = false;
                        _time.ClearAccumulator();
                        _gestureRecognizer.IgnoreActiveTouches();
                        _dispatcher.Clear();
                    }
                }
            }
            else
            {
                int steps = _time.Accumulate(frame);
                _dispatcher.BeginFrame(steps);
                for (int i = 0; i < steps; i++)
                {
                    StepOnce();
                }
            }

            Present(_paused ? 1f : _time.InterpolationAlpha, frame);
        }

        private void StepOnce()
        {
            InputFrame player = _dispatcher.ReadInput(_session.SessionTick);
            InputFrame frame = _botDriving ? _bot.ReadInput(_session.SessionTick) : player;
            if (_session.Phase == RunPhase.Running)
            {
                _recording.Add(_session.SessionTick, frame);
            }

            _session.Step(frame);
            _time.Step();
            DrainEvents();
        }

        private void DrainEvents()
        {
            for (int i = 0; i < _events.Count; i++)
            {
                RunEvent e = _events[i];
                _runnerView.OnRunEvent(e);
                _courseView.OnRunEvent(e);
                switch (e.Type)
                {
                    case RunEventType.Land:
                        if (e.Reason == (byte)LandingKind.Hard)
                        {
                            _rig.ShakeHardLanding();
                        }

                        break;
                    case RunEventType.Hit:
                        if (e.Reason == (byte)HitKind.Crash)
                        {
                            _rig.ShakeCrash();
                        }
                        else
                        {
                            _rig.ShakeMinorHit();
                        }

                        if (_hitLogCount < _hitLogIds.Length)
                        {
                            _hitLogIds[_hitLogCount] = e.Id;
                            _hitLogKinds[_hitLogCount] = e.Reason;
                            _hitLogCount++;
                        }

                        break;
                    case RunEventType.EdgeBrush:
                        _edgeBrushTicks++;
                        break;
                    case RunEventType.InputDropped:
                        _droppedTicks[_droppedCount % _droppedTicks.Length] = (int)e.Tick;
                        _droppedReasons[_droppedCount % _droppedReasons.Length] = e.Reason;
                        _droppedCount++;
                        break;
                    case RunEventType.Revived:
                        _courseView.SyncRemoved(_session.Simulation);
                        break;
                }
            }

            _events.Clear();
        }

        private void Present(float alpha, float frameSeconds)
        {
            _runnerView.Sync(alpha, frameSeconds);
            RunnerState shown = _runnerView.Interpolated;
            _rig.ReducedMotion = _reducedMotion;
            CameraPose pose = _rig.Update(CameraTarget(shown), frameSeconds);
            ApplyCamera(pose);
            _debugView.Sync(shown);

            if (_hud == null)
            {
                return;
            }

            ref readonly RunnerState s = ref _session.Simulation.State;
            _hud.SetDistance(s.Distance);
            _hud.SetHealth(s.Health, s.Shield);
            if (_paused)
            {
                _hud.SetCenter(_resumeCountdown > 0f ? Countdown[Mathf.Clamp(Mathf.CeilToInt(_resumeCountdown * 3f), 0, 3)] : PausedText);
            }
            else
            {
                _hud.SetCenter(_session.Phase == RunPhase.Ready ? ReadyText : null);
            }

            if (_session.Phase == RunPhase.Results && !_resultsShown)
            {
                _resultsShown = true;
                ShowResults();
            }

            if (_debugVisible)
            {
                _debugRefresh -= frameSeconds;
                if (_debugRefresh <= 0f)
                {
                    _debugRefresh = 0.1f;
                    _hud.SetDebugText(BuildDebugText(s, frameSeconds));
                }
            }
        }

        private void ApplyCamera(in CameraPose pose)
        {
            if (_camera == null)
            {
                return;
            }

            _camera.transform.SetPositionAndRotation(CameraMath.Position(pose), CameraMath.Rotation(pose));
            _camera.fieldOfView = pose.FovDeg;
            _camera.nearClipPlane = _rig.Profile.NearClip;
            _camera.farClipPlane = _rig.Profile.FarClip;
        }

        private static CameraTargetInput CameraTarget(in RunnerState s)
        {
            return new CameraTargetInput
            {
                S = s.S,
                X = s.X,
                Y = s.Y,
                GroundY = s.GroundY,
                VLat = s.VLat,
                Speed = s.Speed,
                Sliding = s.Sliding,
            };
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (keyboard.rKey.wasPressedThisFrame || (_session.Phase == RunPhase.Results && keyboard.enterKey.wasPressedThisFrame))
            {
                Restart();
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                SetDebugVisible(!_debugVisible);
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                _orientationMode = (_orientationMode + 1) % 3;
            }

            if (keyboard.bKey.wasPressedThisFrame)
            {
                _botDriving = !_botDriving;
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                _reducedMotion = !_reducedMotion;
            }
        }

        private void UpdateOrientation()
        {
            bool landscape = _orientationMode == 1 || (_orientationMode == 0 && Screen.width >= Screen.height);
            if (landscape == _landscapeActive)
            {
                return;
            }

            _landscapeActive = landscape;
            _rig.SetProfile(landscape ? _landscape.Values : _portrait.Values, false);
            _gestureRecognizer.CancelAll(Time.realtimeSinceStartupAsDouble);
            if (_hud != null)
            {
                _hud.SetOrientation(Screen.width >= Screen.height);
            }
        }

        private void ShowResults()
        {
            ref readonly RunnerState s = ref _session.Simulation.State;
            string title = s.Finished ? "COURSE COMPLETE" : "RUN OVER";
            var body = new StringBuilder(256);
            body.Append("Time  ").Append(_session.RunSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s\n");
            body.Append("Distance  ").Append(((int)s.Distance).ToString(CultureInfo.InvariantCulture)).Append(" m\n");
            body.Append("Hits  ").Append(s.Hits).Append("    Coins  ").Append(s.Coins).Append(" / ").Append(_path.CoinCount).Append('\n');
            body.Append("Dropped inputs  ").Append(s.DroppedInputs).Append('\n');
            if (s.Dead)
            {
                body.Append("Cause  ").Append(s.Cause);
                if (s.DeathObstacle >= 0)
                {
                    body.Append(" · ").Append(_path.GetObstacleLabel(s.DeathObstacle));
                }

                body.Append('\n');
            }

            for (int i = 0; i < _hitLogCount && i < 4; i++)
            {
                body.Append("  hit: ").Append((HitKind)_hitLogKinds[i]).Append(" · ").Append(_path.GetObstacleLabel(_hitLogIds[i])).Append('\n');
            }

            _hud.ShowResults(title, body.ToString());
            string replay = SaveReplay();
            Debug.Log("[JungleBooze] Feel test run: " + title + ", time " + _session.RunSeconds.ToString("0.00", CultureInfo.InvariantCulture) +
                      " s, hits " + s.Hits + ", coins " + s.Coins + "/" + _path.CoinCount + ", dropped " + s.DroppedInputs +
                      ", edge-brush ticks " + _edgeBrushTicks + (_botDriving ? ", bot" : string.Empty) +
                      (s.Dead ? ", cause " + s.Cause + " " + _path.GetObstacleLabel(s.DeathObstacle) : string.Empty) +
                      (replay != null ? ", replay " + replay : string.Empty));
        }

        private string SaveReplay()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                return null;
            }

            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "replays");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "feeltest-last.jbr");
                using (FileStream stream = File.Create(file))
                {
                    InputRecordingSerializer.Write(_recording, stream);
                }

                return file;
            }
            catch (IOException exception)
            {
                Debug.LogWarning("[JungleBooze] Could not save the replay: " + exception.Message);
                return null;
            }
        }

        private string BuildDebugText(in RunnerState s, float frameSeconds)
        {
            StringBuilder b = _debugText;
            b.Length = 0;
            b.Append("tick ").Append(s.Tick).Append("  ").Append(_session.Phase).Append("  ").Append(_path.SectionAt(s.S)).Append('\n');
            b.Append("s ").Append(s.S.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  x ").Append(s.X.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("  xT ").Append(s.XTarget.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("  y ").Append(s.Y.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            b.Append("v ").Append(s.Speed.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("  vLat ").Append(s.VLat.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  vy ").Append(s.Vy.ToString("0.0", CultureInfo.InvariantCulture)).Append('\n');
            b.Append(s.Grounded ? "grounded" : s.Jumped ? "jump" : "air");
            if (s.Sliding)
            {
                b.Append(" slide");
            }

            if (s.FastFalling)
            {
                b.Append(" fast-fall");
            }

            if (s.IsInvulnerable)
            {
                b.Append(" i-frames");
            }

            b.Append('\n');
            b.Append("buffered ").Append(s.Buffered == InputCommand.None ? "-" : s.Buffered + " (" + s.BufferedKind + ", " + (s.Tick - s.BufferedTick) + " ticks)").Append('\n');
            b.Append("dropped (last 5):");
            int shown = Math.Min(_droppedCount, _droppedTicks.Length);
            for (int i = 0; i < shown; i++)
            {
                int index = (_droppedCount - 1 - i) % _droppedTicks.Length;
                b.Append(' ').Append((DropReason)_droppedReasons[index]).Append('@').Append(_droppedTicks[index]);
            }

            b.Append('\n');
            b.Append("camera ").Append(_rig.Profile.Name).Append(_orientationMode == 0 ? " (auto)" : " (forced)")
                .Append(_reducedMotion ? "  reduced motion" : string.Empty).Append(_botDriving ? "  BOT" : string.Empty).Append('\n');
            b.Append("fps ").Append((1f / Mathf.Max(0.0001f, frameSeconds)).ToString("0", CultureInfo.InvariantCulture))
                .Append("  dropped sim time ").Append(_time.DroppedSeconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" s");
            return b.ToString();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
