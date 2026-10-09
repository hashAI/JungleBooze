using System;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.App.FeelTest;
using JungleBooze.Core;
using JungleBooze.Core.Save;
using JungleBooze.Core.Settings;
using JungleBooze.Gameplay.Analytics;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.CameraRig;
using JungleBooze.Gameplay.Config;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Feedback;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.Views;
using JungleBooze.Gameplay.Views.World;
using JungleBooze.Gameplay.World;
using JungleBooze.Services.Haptics;
using JungleBooze.Services.Save;
using JungleBooze.Services.Settings;
using JungleBooze.UI.Expedition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JungleBooze.App.Expedition
{
    /// <summary>
    /// Composition root and frame driver of the vertical-slice Expedition scene (spec 102–103, Part A): the save
    /// (<see cref="SaveService"/>), the deterministic <see cref="ExpeditionSession"/> (Expedition 1 script on the first
    /// run, the World Director after it and on every later run), input (touch, mouse, keyboard or the Perfect bot),
    /// the streamed gray-box world, the real Pista, the camera rig, first-run slow-time help, discovery toasts, the
    /// results with the next objective, and the Deep Breath purchase. Update is the only per-frame entry point:
    /// input → fixed-step simulation (scaled while help is shown) → views → camera → HUD. Keys: Esc pause, R restart,
    /// Enter run again (results), F1 debug, O camera profile, B bot, M reduced motion.
    /// </summary>
    public sealed class ExpeditionRoot : MonoBehaviour, IRunFeedbackListener
    {
        public const int TargetFrameRate = 60;
        private const int StepsPerSecond = 60;
        private const int MaxStepsPerFrame = 5;
        private const string ReadyText = "READY";
        private const string PausedText = "PAUSED";
        private const string NewDiscoveryTitle = "NEW DISCOVERY";

        private static readonly string[] Countdown = { string.Empty, "1", "2", "3" };
        private static readonly string[] HelpGlyphs = { "←  →", "↑", "↓", "«  »", "↑", "↓", "↑", "↑" };

        [SerializeField] private MovementConfigAssets _movement = new MovementConfigAssets();
        [SerializeField] private GestureConfigAsset _gestures;
        [SerializeField] private CameraProfileAsset _landscape;
        [SerializeField] private CameraProfileAsset _portrait;
        [SerializeField] private ExpeditionContentAsset _content;
        [SerializeField] private CameraModifiersAsset _cameraModifiers;
        [SerializeField] private WorldPalette _palette;
        [SerializeField] private Camera _camera;

        [Tooltip("Optional rigged Pista prefab with AnimatedRunnerAvatar. Empty = gray-box capsule.")]
        [SerializeField] private RunnerAvatar _avatarPrefab;

        [SerializeField] private bool _startWithBot;
        [SerializeField] private bool _reducedMotion;

        [Tooltip("Tools and tests: keep the profile in memory (never touch the player's save).")]
        [SerializeField] private bool _memorySave;

        private MovementConfig _config;
        private ExpeditionContent _expedition;
        private FixedStepTimeSource _time;
        private RunEventBuffer _events;
        private ExpeditionSession _session;
        private GestureRecognizer _gestureRecognizer;
        private FrameInputDispatcher _dispatcher;
        private UnityPointerInput _pointer;
        private PerfectBot _bot;
        private WorldRoutePreference _botRoutes;
        private InputRecording _recording;
        private WorldView _worldView;
        private RunnerView _runnerView;
        private CameraRigModel _rig;
        private ExpeditionHud _hud;
        private FeelSettings _settings;
        private RunFeedbackRouter _feedback;
        private EdgeBrushView _edgeBrush;
        private HelpTracker _help;
        private ToastQueue _toasts;
        private SaveService _save;
        private SaveData _profile;
        private Func<string, bool> _discovered;
        private RunResults _results;
        private readonly StringBuilder _debugText = new StringBuilder(512);
        private bool _built;
        private bool _paused;
        private float _resumeCountdown;
        private bool _botDriving;
        private bool _debugVisible;
        private int _orientationMode;
        private bool _landscapeActive = true;
        private readonly ScreenShapeWatcher _screenShape = new ScreenShapeWatcher();
        private bool _resultsShown;
        private float _establishing;
        private float _unlockMoment;
        private AbilityDefinition _upgradeAbility;
        private float _debugRefresh;
        private int _runsThisSession;
        private float _resultsShownAt;
        private AnalyticsRecorder _analytics;
        private LocalAnalyticsLog _analyticsLog;
        private float _reviveOffer;
        private long _reviveHandledTick = -1;
        private float _curtainFlash;
        private static readonly Color UnderwaterTint = new Color(0.08f, 0.32f, 0.5f, 1f);
        private static readonly Color CurtainTint = new Color(0.8f, 0.92f, 1f, 1f);

        public ExpeditionSession Session => _session;

        public RunnerSimulation Simulation => _session?.Simulation;

        public ExpeditionHud Hud => _hud;

        public WorldView WorldView => _worldView;

        public SaveData Profile => _profile;

        public SaveService SaveService => _save;

        public RunResults LastResults => _results;

        public HelpTracker Help => _help;

        public ToastQueue Toasts => _toasts;

        public Camera Camera => _camera;

        public CameraRigModel CameraRig => _rig;

        public RunnerAvatar Avatar => _runnerView?.Avatar;

        public bool BotDriving => _botDriving;

        public PerfectBot Bot => _bot;

        public bool Paused => _paused;

        /// <summary>Seconds left of the first-run establishing shot (0 = playing).</summary>
        public float EstablishingSeconds => _establishing;

        /// <summary>Unscaled seconds the results have been visible for (tests).</summary>
        public float ResultsShownAt => _resultsShownAt;

        /// <summary>On-device analytics (AC-103-50).</summary>
        public AnalyticsRecorder Analytics => _analytics;

        public LocalAnalyticsLog AnalyticsLog => _analyticsLog;

        /// <summary>Seconds left on the revive offer (0 = none).</summary>
        public float ReviveOfferSeconds => _reviveOffer;

        public void Configure(MovementConfigAssets movement, GestureConfigAsset gestures, CameraProfileAsset landscape, CameraProfileAsset portrait, ExpeditionContentAsset content, WorldPalette palette, Camera targetCamera)
        {
            _movement = movement;
            _gestures = gestures;
            _landscape = landscape;
            _portrait = portrait;
            _content = content;
            _palette = palette;
            _camera = targetCamera;
        }

        public void SetCameraModifiers(CameraModifiersAsset modifiers)
        {
            _cameraModifiers = modifiers;
        }

        public void SetAvatarPrefab(RunnerAvatar prefab)
        {
            _avatarPrefab = prefab;
        }

        /// <summary>Tools/tests: use an in-memory save (call before <see cref="Build"/>).</summary>
        public void UseMemorySave(SaveData initial)
        {
            _memorySave = true;
            _profile = initial;
        }

        public void Build()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            Application.targetFrameRate = TargetFrameRate;
            _config = _movement.Build();
            _expedition = _content.Build();
            _time = new FixedStepTimeSource(StepsPerSecond, MaxStepsPerFrame);
            _events = new RunEventBuffer(512);
            _session = new ExpeditionSession(_expedition, _config, _time, _events, true);
            _help = new HelpTracker(_session.Path, _session.Simulation);
            _toasts = new ToastQueue(8, 2f, 0.5f);
            ulong configHash = MovementConfigAssets.Mix(_movement.ComputeHash(), _content.ComputeHash().ToString(CultureInfo.InvariantCulture));
            _recording = new InputRecording(0UL, configHash, Application.version, 60 * 60 * 10);

            // Save: on device in play mode, memory for tools and tests.
            SaveData seeded = _profile;
            ISaveStorage storage = _memorySave || !Application.isPlaying ? new MemorySaveStorage() : (ISaveStorage)new FileSaveStorage(Application.persistentDataPath);
            float startSkill = _expedition.Director.StartSkill;
            _save = new SaveService(storage, () => SaveData.CreateDefault(NewWorldSeed(), startSkill));
            _profile = seeded ?? _save.Load();
            if (_save.LastOutcome == SaveLoadOutcome.Backup || _save.LastOutcome == SaveLoadOutcome.Corrupt || _save.LastOutcome == SaveLoadOutcome.FutureVersion)
            {
                Debug.LogWarning("[JungleBooze] Save loaded with outcome " + _save.LastOutcome + (_save.LastError != null ? ": " + _save.LastError : string.Empty));
            }

            _discovered = id => _profile.IsDiscovered(id);

            _gestureRecognizer = new GestureRecognizer(_gestures.Values);
            _dispatcher = new FrameInputDispatcher(_gestureRecognizer, _gestures.Values.CommandQueueSize);
            _pointer = new UnityPointerInput(_gestureRecognizer, _dispatcher, _gestures.Values);
            _bot = new PerfectBot(_session.Simulation, false);
            _botRoutes = new WorldRoutePreference(_session.Path, RouteType.Secret, RouteType.Risky, RouteType.Safe);
            _bot.ForkPreference = _botRoutes;
            _botDriving = _startWithBot;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var world = new GameObject("World");
            world.transform.SetParent(transform, false);
            _worldView = world.AddComponent<WorldView>();
            _worldView.Build(_expedition.Library, _session.Path, _palette, font);

            RunnerAvatar avatar = _avatarPrefab != null ? Instantiate(_avatarPrefab, transform) : CapsuleRunnerAvatar.Create(transform, _palette.Runner, _palette.RunnerAccent);
            avatar.Bind(_config);
            _runnerView = new RunnerView(_session.Simulation, avatar) { Frames = _session.Path };

            var brush = new GameObject("EdgeBrushLeaves");
            brush.transform.SetParent(transform, false);
            _edgeBrush = brush.AddComponent<EdgeBrushView>();
            _edgeBrush.Build(_palette.Leaf != null ? _palette.Leaf : _palette.Hedge);

            ISettingsStore settingsStore = Application.isPlaying && !_memorySave ? new PlayerPrefsSettingsStore() : (ISettingsStore)new MemorySettingsStore();
            _settings = new FeelSettings(settingsStore, _reducedMotion);
            _feedback = new RunFeedbackRouter(IosHaptics.Create(), this, _config.JumpSlide.SoftLandingFall);
            _gestureRecognizer.SensitivityMultiplier = _settings.Sensitivity;
            _feedback.HapticsEnabled = _settings.HapticsEnabled;
            _reducedMotion = _settings.ReducedMotion;

            _landscapeActive = Screen.width >= Screen.height;
            _rig = new CameraRigModel(_landscapeActive ? _landscape.Values : _portrait.Values, _config.Speed.V0, _config.Speed.VMax, _config.Lateral.VLatMax)
            {
                ReducedMotion = _reducedMotion,
                Modifiers = _cameraModifiers != null ? _cameraModifiers.Values.Clone() : new CameraModifiers(),
            };

            // Analytics: on-device log only (memory for tools and tests); no network.
            _analytics = new AnalyticsRecorder(_expedition) { Build = Application.version, SessionId = Guid.NewGuid().ToString("N") };
            _analyticsLog = new LocalAnalyticsLog(_memorySave || !Application.isPlaying ? null : Application.persistentDataPath);

            if (Application.isPlaying)
            {
                EnsureEventSystem();
                var hudObject = new GameObject("ExpeditionHud");
                hudObject.transform.SetParent(transform, false);
                _hud = hudObject.AddComponent<ExpeditionHud>();
                _hud.Build(font, _config.Health.MaxHealth, TogglePause, RunAgain, OpenObjective, () => Learn(), CloseUpgrade, () => AcceptRevive(), DeclineRevive);
                _hud.SetHint(Application.isMobilePlatform ? string.Empty : "drag/A D steer · W/Space jump · S slide · Q/E dodge · Esc pause · R restart · F1 debug · O camera · B bot");
                _pointer.IsOverUi = (id, pixel) => _hud != null && _hud.HitsControl(pixel);
                _pointer.Enable();
            }

            StartRun();
        }

        /// <summary>New run from the current profile (instant restart; Expedition 1 when no run was finished yet).</summary>
        public void StartRun()
        {
            bool first = _profile.runsCompleted == 0;
            var setup = new ExpeditionRunSetup
            {
                FirstExpedition = first,
                Seed = ExpeditionRunSetup.RunSeed(_profile.worldSeed, _profile.runsCompleted),
                Owned = (AbilityFlags)_profile.abilities,
                PendingShowcase = (AbilityFlags)_profile.pendingShowcase,
                Skill = _profile.skill,
                Discovered = _discovered,
            };
            BeginRun(setup);
            _establishing = first && _runsThisSession == 0 && Application.isPlaying ? _expedition.Results.EstablishingShotTime : 0f;
            _runsThisSession++;
        }

        /// <summary>Tools and tests: start a run with an explicit setup (forced speed, seeds).</summary>
        public void BeginRun(in ExpeditionRunSetup setup)
        {
            _session.BeginRun(setup);
            _time.Reset();
            _dispatcher.Clear();
            _gestureRecognizer.Reset();
            _bot.Reset();
            _recording.Clear();
            _worldView.ResetRun();
            _runnerView.ResetRun();
            _feedback.Reset();
            _edgeBrush.Clear();
            _help.BeginRun(setup.FirstExpedition && !_botDriving);
            _toasts.Clear();
            _paused = false;
            _resumeCountdown = 0f;
            _resultsShown = false;
            _results = null;
            _unlockMoment = 0f;
            _establishing = 0f;
            _reviveOffer = 0f;
            _reviveHandledTick = -1;
            _curtainFlash = 0f;
            _analytics.BeginRun(_profile.runsCompleted, setup.Seed, (int)setup.Owned, setup.Skill, _landscapeActive);
            _runnerView.Sync(1f, 0f);
            _rig.Snap(CameraTarget(_runnerView.Interpolated));
            ApplyCamera(_rig.Pose);
            if (_hud != null)
            {
                _hud.HideResults();
                _hud.HideRevive();
                _hud.SetOverlay(Color.clear, 0f);
                _hud.HideToast();
                _hud.SetHelp(null);
                _hud.SetCenter(ReadyText);
                _hud.SetDistance(0f);
                _hud.SetWallet(0, 0);
            }
        }

        public void RunAgain()
        {
            StartRun();
        }

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

        public void SetBotDriving(bool driving)
        {
            _botDriving = driving;
            if (driving)
            {
                _help.Disable();
            }
        }

        public void SetBotRoutes(params RouteType[] order)
        {
            _botRoutes.Order = order;
        }

        public void SetDebugVisible(bool visible)
        {
            _debugVisible = visible;
            if (_hud != null)
            {
                _hud.SetDebugVisible(visible);
            }
        }

        public void SetCameraProfile(bool landscape, bool instant)
        {
            _orientationMode = landscape ? 1 : 2;
            _landscapeActive = landscape;
            _rig.SetProfile(landscape ? _landscape.Values : _portrait.Values, instant);
        }

        /// <summary>Skips the establishing shot (any touch does the same).</summary>
        public void SkipEstablishingShot()
        {
            _establishing = 0f;
        }

        /// <summary>Steps whole ticks with the active input, bypassing real time (tests and tools).</summary>
        public void StepTicks(int ticks)
        {
            _establishing = 0f;
            _dispatcher.BeginFrame(ticks);
            for (int i = 0; i < ticks; i++)
            {
                StepOnce();
                MaybeOfferRevive();
                if (_reviveOffer > 0f)
                {
                    break;
                }
            }

            Present(1f, ticks / (float)StepsPerSecond);
        }

        /// <summary>Opens the objective card (the ability card when the objective is an ability).</summary>
        public void OpenObjective()
        {
            if (_results == null || _hud == null)
            {
                return;
            }

            _hud.CompleteCountUp();
            AbilityFlags ability = _results.Objective.Ability;
            if (ability == AbilityFlags.None)
            {
                ability = _expedition.NextAbility((AbilityFlags)_profile.abilities)?.Ability ?? AbilityFlags.None;
            }

            _upgradeAbility = _expedition.FindAbility(ability);
            if (_upgradeAbility == null)
            {
                return;
            }

            bool owned = ProgressionRules.Owns(_profile, _upgradeAbility.Ability);
            _hud.ShowUpgrade(_upgradeAbility, _profile.coins, _profile.crystals, ProgressionRules.CanAfford(_profile, _upgradeAbility) && _upgradeAbility.Implemented, owned);
        }

        /// <summary>LEARN: deducts the cost once, saves, plays the unlock moment, then returns to the results.</summary>
        public bool Learn()
        {
            if (_upgradeAbility == null || !ProgressionRules.TryLearn(_profile, _upgradeAbility))
            {
                return false;
            }

            _save.Save(_profile);
            _analytics.RecordAbilityUnlocked(_upgradeAbility, _profile.runsCompleted);
            _analytics.Flush(_analyticsLog);
            Debug.Log("[JungleBooze] Learned " + _upgradeAbility.Name + "; wallet " + _profile.coins + " coins, " + _profile.crystals + " crystals.");
            if (_hud != null)
            {
                _hud.ShowLearned(_upgradeAbility.Name);
                _hud.SetWallet(_profile.coins, _profile.crystals);
            }

            _unlockMoment = _expedition.Results.UnlockMomentTime;
            return true;
        }

        public void CloseUpgrade()
        {
            _hud?.HideUpgrade(true);
        }

        /// <summary>Revive "Continue?" (GDD §11): pays crystals and continues from the safe point. False if not possible.</summary>
        public bool AcceptRevive()
        {
            if (_reviveOffer <= 0f)
            {
                return false;
            }

            int cost = ReviveRules.Cost(_expedition.Results, _session.Stats.Revives);
            int index = _session.Stats.Revives;
            if (!ReviveRules.TryRevive(_session, _expedition.Results, _profile))
            {
                return false;
            }

            _analytics.RecordRevive(true, cost, index, _session.Simulation.State.Tick * 1000L / StepsPerSecond);
            _reviveOffer = 0f;
            _time.ClearAccumulator();
            _dispatcher.Clear();
            _gestureRecognizer.IgnoreActiveTouches();
            _hud?.HideRevive();
            Debug.Log("[JungleBooze] Revived for " + cost + " crystal(s) (" + _session.Stats.Revives + "/" + _expedition.Results.MaxRevives + ").");
            return true;
        }

        public void DeclineRevive()
        {
            _reviveOffer = 0f;
            _hud?.HideRevive();
        }

        private void MaybeOfferRevive()
        {
            ref readonly RunnerState s = ref _session.Simulation.State;
            if (!s.Dead || s.DeathTick == _reviveHandledTick)
            {
                return;
            }

            _reviveHandledTick = s.DeathTick;
            RunStats stats = _session.Stats;
            ResultsConfig cfg = _expedition.Results;
            if (_botDriving || !Application.isPlaying || !ReviveRules.CanOffer(cfg, _session.FirstExpedition, stats) || !ReviveRules.CanAfford(cfg, _profile, stats))
            {
                return;
            }

            _reviveOffer = cfg.ReviveOfferTime;
            _analytics.RecordRevive(false, ReviveRules.Cost(cfg, stats.Revives), stats.Revives, s.Tick * 1000L / StepsPerSecond);
            _hud?.ShowRevive(ReviveRules.Cost(cfg, stats.Revives), true, _reviveOffer);
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
            if (pauseStatus)
            {
                HandleInterruption();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                HandleInterruption();
            }
        }

        private void HandleInterruption()
        {
            if (!_built)
            {
                return;
            }

            _gestureRecognizer.CancelAll(Time.realtimeSinceStartupAsDouble);
            _dispatcher.Clear();
            if (_session.Phase != RunPhase.Results)
            {
                _paused = true;
                _resumeCountdown = 0f;
            }
        }

        private void Update()
        {
            if (_built)
            {
                Tick(Time.unscaledDeltaTime, Time.realtimeSinceStartupAsDouble);
            }
        }

        /// <summary>One rendered frame (Update's body; PlayMode tests call it directly).</summary>
        public void Tick(float frame, double now)
        {
            HandleKeys();
            UpdateOrientation(now);
            _pointer.Enabled = !_botDriving && !_paused && _establishing <= 0f;
            _pointer.PollFrame(frame, now);

            if (_establishing > 0f)
            {
                _dispatcher.BeginFrame(0);
                _establishing -= frame;
                if (AnyPress())
                {
                    _establishing = 0f;
                    _gestureRecognizer.IgnoreActiveTouches();
                }

                PresentEstablishing(frame);
                return;
            }

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
            else if (_reviveOffer > 0f)
            {
                // The run waits on the "Continue?" offer (4 s, skip always visible).
                _dispatcher.BeginFrame(0);
                _reviveOffer -= frame;
                if (_hud != null)
                {
                    ResultsConfig cfg = _expedition.Results;
                    _hud.ShowRevive(ReviveRules.Cost(cfg, _session.Stats.Revives), ReviveRules.CanAfford(cfg, _profile, _session.Stats), _reviveOffer);
                }

                if (_reviveOffer <= 0f)
                {
                    DeclineRevive();
                }
            }
            else
            {
                float scale = _help.Active ? _help.Scale : 1f;
                int steps = _time.Accumulate(frame * scale);
                _dispatcher.BeginFrame(steps);
                for (int i = 0; i < steps && _reviveOffer <= 0f; i++)
                {
                    StepOnce();
                    MaybeOfferRevive();
                }
            }

            if (_unlockMoment > 0f)
            {
                _unlockMoment -= frame;
                if (_unlockMoment <= 0f && _hud != null)
                {
                    _hud.HideUpgrade(true);
                    _hud.SetObjective(_upgradeAbility.Name + " learned · " + _upgradeAbility.ObjectiveLine, true);
                }
            }

            Present(_paused ? 1f : _time.InterpolationAlpha, frame);
        }

        private void StepOnce()
        {
            InputFrame player = _dispatcher.ReadInput(_session.Run.SessionTick);
            InputFrame frame = _botDriving ? _bot.ReadInput(_session.Run.SessionTick) : player;
            RunPhase phase = _session.Phase;
            if (phase == RunPhase.Running || phase == RunPhase.Finishing)
            {
                _recording.Add(_session.Run.SessionTick, frame);
            }

            _session.Step(frame);
            _time.Step();
            _help.Update();
            DrainEvents();
        }

        private void DrainEvents()
        {
            for (int i = 0; i < _events.Count; i++)
            {
                RunEvent e = _events[i];
                _runnerView.OnRunEvent(e);
                _worldView.OnRunEvent(e);
                _feedback.OnRunEvent(e);
                _analytics.OnRunEvent(e, _session);
                switch (e.Type)
                {
                    case RunEventType.Vista:
                        _rig.TriggerVista();
                        break;
                    case RunEventType.CurtainPass:
                        _curtainFlash = 0.25f;
                        break;
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

                        break;
                    case RunEventType.Discovery:
                        if (e.Reason == 1)
                        {
                            _toasts.Enqueue(e.Id);
                        }

                        break;
                }
            }

            _events.Clear();
        }

        private void Present(float alpha, float frameSeconds)
        {
            _runnerView.Sync(alpha, frameSeconds);
            RunnerState shown = _runnerView.Interpolated;
            _worldView.Sync(_session.Simulation, _session.Tracker, shown.S, _paused ? 0f : frameSeconds);
            _worldView.SyncTraversal(_session.Simulation, _session.Creatures, _session.Simulation.Options.DeepBreath, _paused ? 0f : frameSeconds);
            if (!Application.isPlaying)
            {
                _edgeBrush.ManualTick(frameSeconds);
            }

            _rig.ReducedMotion = _reducedMotion;
            CameraPose pose = _rig.Update(CameraTarget(shown), frameSeconds);
            ApplyCamera(pose);
            if (!_paused)
            {
                _toasts.Update(frameSeconds);
            }

            if (_session.Phase == RunPhase.Results && !_resultsShown)
            {
                _resultsShown = true;
                FinishRun();
            }

            if (_hud == null)
            {
                return;
            }

            ref readonly RunnerState s = ref _session.Simulation.State;
            RunStats stats = _session.Stats;
            if (_curtainFlash > 0f)
            {
                _curtainFlash -= frameSeconds;
            }

            float underwater = s.Mode == MoveMode.DeepDive ? 0.32f : s.Submerged ? 0.12f : 0f;
            if (_curtainFlash > 0f)
            {
                _hud.SetOverlay(CurtainTint, 0.35f * (_curtainFlash / 0.25f));
            }
            else
            {
                _hud.SetOverlay(UnderwaterTint, underwater);
            }

            if (_reviveOffer <= 0f && _hud.ReviveVisible)
            {
                _hud.HideRevive();
            }

            _hud.SetDistance(s.Distance);
            if (_results == null)
            {
                _hud.SetWallet(stats.TotalCoins, stats.TotalCrystals);
            }

            float shieldLeft = s.Shield && s.ShieldUntilTick > 0 ? (s.ShieldUntilTick - s.Tick) / (_expedition.Pickups.ShieldDuration * StepsPerSecond) : 1f;
            _hud.SetHealth(s.Health, s.Shield, shieldLeft);
            if (_toasts.Current >= 0)
            {
                DiscoveryEntry entry = _expedition.Discoveries[_toasts.Current];
                _hud.ShowToast(NewDiscoveryTitle, entry.ToastText);
            }
            else
            {
                _hud.HideToast();
            }

            _hud.SetHelp(_help.Active ? HelpGlyphs[Mathf.Clamp((int)_help.ActiveMove, 0, HelpGlyphs.Length - 1)] : null);
            if (_paused)
            {
                _hud.SetCenter(_resumeCountdown > 0f ? Countdown[Mathf.Clamp(Mathf.CeilToInt(_resumeCountdown * 3f), 0, 3)] : PausedText);
            }
            else
            {
                _hud.SetCenter(_session.Phase == RunPhase.Ready ? ReadyText : null);
            }

            _hud.Tick(frameSeconds);
            if (_hud.ResultsVisible && _hud.CountingUp && AnyPress())
            {
                _hud.CompleteCountUp();
            }

            if (_debugVisible)
            {
                _debugRefresh -= frameSeconds;
                if (_debugRefresh <= 0f)
                {
                    _debugRefresh = 0.1f;
                    _hud.SetDebugText(BuildDebugText(s));
                }
            }
        }

        private void FinishRun()
        {
            RunStats stats = _session.Stats;
            bool first = _session.FirstExpedition;
            _results = ProgressionRules.ApplyRun(_profile, stats, _expedition, first, _session.Director.ShowcasedAbilities);
            bool saved = _save.Save(_profile);
            _analytics.RecordRunEnded(stats, _session.Run.RunSeconds, stats.Revives);
            _analytics.Flush(_analyticsLog);
            _resultsShownAt = Time.realtimeSinceStartup;
            if (_hud != null)
            {
                _hud.ShowResults(_results, _expedition.Results.CountUpTime);
                _hud.SetWallet(_profile.coins, _profile.crystals);
                _hud.HideToast();
                _hud.SetHelp(null);
            }

            string replay = SaveReplay();
            Debug.Log("[JungleBooze] Expedition run " + _profile.runsCompleted + (first ? " (Expedition 1)" : string.Empty) +
                      ": " + ((int)stats.Distance) + " m, coins " + stats.TotalCoins + " (clean line " + stats.CleanLineCoins + ", discovery " + stats.DiscoveryCoins +
                      "), crystals " + stats.TotalCrystals + ", hits " + stats.Hits + ", discoveries " + stats.NewDiscoveryCount + ", routes risky/safe/secret " +
                      stats.RiskyRoutes + "/" + stats.SafeRoutes + "/" + stats.SecretRoutes + ", cause " + stats.Cause + " " + stats.DeathLabel +
                      ", objective \"" + ExpeditionHud.ObjectiveLine(_results.Objective).Replace('\n', ' ') + "\", skill " +
                      _profile.skill.ToString("0.00", CultureInfo.InvariantCulture) + (saved ? string.Empty : " (save read-only)") +
                      (_botDriving ? ", bot" : string.Empty) + (replay != null ? ", replay " + replay : string.Empty));
        }

        private string SaveReplay()
        {
            if (!Application.isPlaying || (!Debug.isDebugBuild && !Application.isEditor))
            {
                return null;
            }

            try
            {
                string folder = Path.Combine(Application.persistentDataPath, "replays");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "expedition-last.jbr");
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

        private void PresentEstablishing(float frame)
        {
            // A 4 s crane from above the root arch down to the follow camera (spec 103 §11 beat 0).
            _runnerView.Sync(1f, frame);
            CameraPose target = _rig.Update(CameraTarget(_runnerView.Interpolated), frame);
            float total = _expedition.Results.EstablishingShotTime;
            float t = total > 0f ? Mathf.Clamp01(1f - (_establishing / total)) : 1f;
            float e = t * t * (3f - (2f * t));
            PathFrame f = _session.Path.GetFrame(target.S);
            f.Offset(target.X, out float ex, out float ez);
            var endPos = new Vector3(ex, target.Y, ez);
            Quaternion endRot = CameraMath.Rotation(target);
            var startPos = new Vector3(6f, 14f, -18f);
            Quaternion startRot = Quaternion.LookRotation(new Vector3(0f, 3f, 60f) - startPos);
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, e), Quaternion.Slerp(startRot, endRot, e));
                _camera.fieldOfView = target.FovDeg;
            }

            _worldView.Sync(_session.Simulation, _session.Tracker, 0f, frame);
            if (_hud != null)
            {
                _hud.SetCenter(null);
            }
        }

        private static bool AnyPress()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                return true;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        private void ApplyCamera(in CameraPose pose)
        {
            if (_camera == null)
            {
                return;
            }

            // Path space → the curved world (spec 102 §2.1): position through the centreline frame at the camera's s,
            // yaw from the rig (it follows the path heading at Pista).
            PathFrame f = _session.Path.GetFrame(pose.S);
            f.Offset(pose.X + pose.ShakeX, out float wx, out float wz);
            _camera.transform.SetPositionAndRotation(new Vector3(wx, pose.Y + pose.ShakeY, wz), CameraMath.Rotation(pose));
            _camera.fieldOfView = pose.FovDeg;
            _camera.nearClipPlane = _rig.Profile.NearClip;
            _camera.farClipPlane = _rig.Profile.FarClip;
        }

        private CameraTargetInput CameraTarget(in RunnerState s)
        {
            CameraMode mode = CameraMode.Run;
            switch (s.Mode)
            {
                case MoveMode.Swim:
                    mode = CameraMode.Swim;
                    break;
                case MoveMode.DeepDive:
                    mode = CameraMode.DeepDive;
                    break;
                case MoveMode.Swing:
                    mode = CameraMode.Swing;
                    break;
                default:
                    if (s.VineAir)
                    {
                        mode = CameraMode.Swing;
                    }
                    else if (_session != null && _session.Path.IsCanopy(s.S))
                    {
                        mode = CameraMode.Canopy;
                    }

                    break;
            }

            bool canopyFall = s.Dead && s.Cause == DeathCause.Fall && _session != null && _session.Path.IsCanopy(s.S);
            return new CameraTargetInput
            {
                S = s.S,
                X = s.X,
                Y = s.Y,
                GroundY = s.Mode == MoveMode.Swing ? s.LastGroundY : s.GroundY,
                VLat = s.VLat,
                Speed = s.Speed,
                Sliding = s.Sliding,
                PathYawDeg = _session != null ? _session.Path.GetFrame(s.S).HeadingDeg : 0f,
                Mode = mode,
                VineAir = s.VineAir,
                FallHold = canopyFall,
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
                StartRun();
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
                SetBotDriving(!_botDriving);
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                _settings.SetReducedMotion(!_settings.ReducedMotion);
                _reducedMotion = _settings.ReducedMotion;
            }
        }

        private void UpdateOrientation(double now)
        {
            if (_screenShape.Update(Screen.width, Screen.height, (int)Screen.orientation))
            {
                _gestureRecognizer.CancelAll(now);
                if (_hud != null)
                {
                    _hud.SetOrientation(Screen.width >= Screen.height);
                }
            }

            bool landscape = _orientationMode == 1 || (_orientationMode == 0 && Screen.width >= Screen.height);
            if (landscape == _landscapeActive)
            {
                return;
            }

            _landscapeActive = landscape;
            _rig.SetProfile(landscape ? _landscape.Values : _portrait.Values, false);
        }

        private string BuildDebugText(in RunnerState s)
        {
            StringBuilder b = _debugText;
            b.Length = 0;
            b.Append("tick ");
            DebugFormat.Int(b, s.Tick);
            b.Append("  ").Append(_session.Stats.CurrentChunk).Append('\n');
            b.Append("s ");
            DebugFormat.Fixed(b, s.S, 1);
            b.Append("  x ");
            DebugFormat.Fixed(b, s.X, 2);
            b.Append("  v ");
            DebugFormat.Fixed(b, s.Speed, 2);
            b.Append('\n');
            b.Append("chunks placed ");
            DebugFormat.Int(b, _session.Path.ChunkCount);
            b.Append("  picks ");
            DebugFormat.Int(b, _session.Director.Serial);
            b.Append("  fallback ");
            DebugFormat.Int(b, _session.Director.FallbackPicks);
            b.Append("  emergency ");
            DebugFormat.Int(b, _session.Director.EmergencyPicks);
            b.Append('\n');
            b.Append("skill S ");
            DebugFormat.Fixed(b, _session.Director.Skill, 2);
            b.Append("  band ");
            DebugFormat.Int(b, _session.Director.BandShift);
            b.Append(_botDriving ? "  BOT" : string.Empty);
            return b.ToString();
        }

        private static long NewWorldSeed()
        {
            // Not simulation code: the install seed only has to differ between installs.
            return DateTime.UtcNow.Ticks ^ ((long)Environment.TickCount << 32);
        }

        // ---- IRunFeedbackListener ----

        public void OnEdgeBrush(int side, bool started)
        {
            RunnerState s = _runnerView.Interpolated;
            _session.Path.GetLateralBounds(s.S, s.X, out float xMin, out float xMax);
            float edge = side > 0 ? xMax : xMin;
            _edgeBrush.Burst(_runnerView.WorldPoint(s.S, edge, s.GroundY), side, s.Speed, started ? 6 : 3);
        }

        public void OnJump()
        {
        }

        public void OnLand(LandingKind kind, float fallHeight)
        {
        }

        public void OnSlide()
        {
        }

        public void OnDodge(int direction)
        {
        }

        public void OnHit(HitKind kind)
        {
        }

        public void OnDied(DeathCause cause)
        {
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
