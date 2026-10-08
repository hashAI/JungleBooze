using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Composition root of the AURELIA Phase 0 look test scene (ADR 0004). The editor scene builder
    /// (JungleBooze > Look Test > Build Scene) places this component and fills in its references; at Play it wires
    /// the existing run code to the realistic stretch:
    /// the same <see cref="GameSession"/> / <see cref="RunnerSimulation"/>, touch and keyboard input
    /// (<see cref="PlayerInputAdapter"/>) and <see cref="RunDriver"/> as the Run scene, on a flat
    /// world (no obstacles) at a constant speed; Pista is the existing gray-box <see cref="RunnerView"/> (casting a
    /// real shadow) until the 3D model exists; the existing <see cref="FollowCameraView"/> with landscape camera
    /// values; <see cref="LookTestWorldView"/> loops the stretch; <see cref="FrameStatsOverlay"/> and
    /// <see cref="LookTestQualityPanel"/> measure it. The run starts immediately.
    /// The deterministic simulation is unchanged: only its config inputs (speed curve, presentation) differ.
    /// </summary>
    public sealed class LookTestRoot : MonoBehaviour
    {
        private static readonly float[] ScaleSteps = { 0f, 1f, 0.85f, 0.7f };

        [SerializeField] private LookTestConfigAsset _config;
        [SerializeField] private Camera _camera;
        [SerializeField] private Light _sun;
        [SerializeField] private Transform _stretchRoot;
        [SerializeField] private Transform[] _segments;
        [SerializeField] private GameObject[] _plantGroups;
        [SerializeField] private GameObject[] _waterGroups;

        private RunDriver _driver;
        private FrameStatsOverlay _overlay;
        private UniversalAdditionalCameraData _cameraData;
        private LightShadows _sunShadows = LightShadows.Soft;
        private int _scaleStep;
        private float _autoRenderScale = 1f;

        public LookTestConfigAsset Config => _config;

        public RunDriver Driver => _driver;

        /// <summary>Called by the editor scene builder to fill in the scene references.</summary>
        public void Configure(
            LookTestConfigAsset config,
            Camera targetCamera,
            Light sun,
            Transform stretchRoot,
            Transform[] segments,
            GameObject[] plantGroups,
            GameObject[] waterGroups)
        {
            _config = config;
            _camera = targetCamera;
            _sun = sun;
            _stretchRoot = stretchRoot;
            _segments = segments;
            _plantGroups = plantGroups;
            _waterGroups = waterGroups;
        }

        private void Start()
        {
            if (_config == null || _camera == null || _stretchRoot == null || _segments == null || _segments.Length == 0)
            {
                Debug.LogError("[JungleBooze] Look test scene is incomplete. Run JungleBooze > Look Test > Build Scene again.", this);
                return;
            }

            Application.targetFrameRate = _config.TargetFrameRate;
            AllowLandscape();
            ApplyAutoRenderScale();

            RunConfigSet configs = RunConfigLoader.Load();
            RunnerPresentationConfig presentation = configs.Presentation;
            presentation.CameraFovDeg = _config.CameraFovDeg;
            presentation.CameraOffsetBehindM = _config.CameraOffsetBehindM;
            presentation.CameraOffsetUpM = _config.CameraOffsetUpM;
            presentation.CameraLookAheadM = _config.CameraLookAheadM;
            presentation.CameraLookAtHeightM = _config.CameraLookAtHeightM;

            var input = new PlayerInputAdapter(configs.Input, PlayerInputAdapter.PixelsPerPointForDpi(Screen.dpi));
            var session = new GameSession(
                configs.Runner,
                SpeedCurve.CreateConstant(_config.RunSpeedMps),
                input,
                new FlatRunWorldFactory(),
                presentation.ToSessionTimings(),
                (ulong)(uint)_config.Seed,
                SessionPhase.Ready);
            session.Begin();

            var kit = new GrayBoxKit();
            var runnerObject = new GameObject("Pista (stand-in)");
            runnerObject.transform.SetParent(transform, false);
            RunnerView runnerView = runnerObject.AddComponent<RunnerView>();
            runnerView.Init(kit, configs.Runner, presentation);
            UseRealShadows(runnerObject);

            _camera.farClipPlane = _config.CameraFarClipM;
            FollowCameraView cameraView = _camera.GetComponent<FollowCameraView>();
            if (cameraView == null)
            {
                cameraView = _camera.gameObject.AddComponent<FollowCameraView>();
            }

            cameraView.Init(_camera, presentation);
            _cameraData = _camera.GetUniversalAdditionalCameraData();

            LookTestWorldView worldView = _stretchRoot.GetComponent<LookTestWorldView>();
            if (worldView == null)
            {
                worldView = _stretchRoot.gameObject.AddComponent<LookTestWorldView>();
            }

            worldView.Init(_segments, _config.LoopLengthM, _config.RecycleBehindM);

            _driver = gameObject.AddComponent<RunDriver>();
            _driver.Init(session, input, new IRunView[] { worldView, runnerView, cameraView }, kit);

            if (_sun != null && _sun.shadows != LightShadows.None)
            {
                _sunShadows = _sun.shadows;
            }

            BuildOverlay();
            Debug.Log("[JungleBooze] Look test running. Config: " + configs.Source + ". Speed " + _config.RunSpeedMps + " m/s.");
        }

        private void BuildOverlay()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            var overlayObject = new GameObject("FrameStats", typeof(RectTransform));
            overlayObject.transform.SetParent(transform, false);
            _overlay = overlayObject.AddComponent<FrameStatsOverlay>();
            _overlay.Init(font, _config.StatsRefreshSeconds, Application.targetFrameRate, CurrentRenderScale);

            if (!_config.ShowQualityPanel)
            {
                return;
            }

            var panelObject = new GameObject("QualityPanel", typeof(RectTransform));
            panelObject.transform.SetParent(transform, false);
            LookTestQualityPanel panel = panelObject.AddComponent<LookTestQualityPanel>();
            panel.Init(font, _overlay.ResetStats);

            panel.AddToggle("Post", () => _cameraData != null && _cameraData.renderPostProcessing, on =>
            {
                if (_cameraData != null)
                {
                    _cameraData.renderPostProcessing = on;
                }
            });
            panel.AddToggle("Shadows", () => _sun != null && _sun.shadows != LightShadows.None, on =>
            {
                if (_sun != null)
                {
                    _sun.shadows = on ? _sunShadows : LightShadows.None;
                }
            });
            panel.AddToggle("MSAA", () => _camera.allowMSAA, on => _camera.allowMSAA = on);
            panel.AddToggle("HDR", () => _camera.allowHDR, on => _camera.allowHDR = on);
            panel.AddToggle("Plants", () => GroupActive(_plantGroups), on => SetGroups(_plantGroups, on));
            panel.AddToggle("Water", () => GroupActive(_waterGroups), on => SetGroups(_waterGroups, on));
            panel.AddAction(() => Application.targetFrameRate >= 60 ? "60 fps" : "30 fps", () =>
            {
                Application.targetFrameRate = Application.targetFrameRate >= 60 ? 30 : 60;
                _overlay.SetTargetFrameRate(Application.targetFrameRate);
            });
            panel.AddAction(ScaleLabel, CycleRenderScale);
            panel.AddAction(() => "Reset stats", () => { });
            panel.AddAction(() => _driver != null && _driver.Session.Phase == SessionPhase.Paused ? "Resume" : "Pause", () =>
            {
                if (_driver == null)
                {
                    return;
                }

                if (_driver.Session.Phase == SessionPhase.Paused)
                {
                    _driver.Resume();
                }
                else
                {
                    _driver.Pause();
                }
            });
        }

        /// <summary>Gray-box Pista casts and receives the sun's shadow; the flat blob shadow is hidden.</summary>
        private static void UseRealShadows(GameObject runnerObject)
        {
            MeshRenderer[] renderers = runnerObject.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer meshRenderer = renderers[i];
                if (meshRenderer.gameObject.name == "BlobShadow")
                {
                    meshRenderer.enabled = false;
                    continue;
                }

                meshRenderer.shadowCastingMode = ShadowCastingMode.On;
                meshRenderer.receiveShadows = true;
            }
        }

        private static void AllowLandscape()
        {
            // [ASSUMED] landscape for the look test (blueprint recommendation). Works on device when the player
            // settings allow landscape (JungleBooze > Look Test > Use Look Test iOS Build Settings).
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            if (!Application.isEditor)
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }

        /// <summary>
        /// Device builds only: render scale so the internal resolution is about the configured megapixels. The editor
        /// keeps native resolution (changing the pipeline asset there would change the project file).
        /// </summary>
        private void ApplyAutoRenderScale()
        {
            float pixels = Mathf.Max(1f, (float)Screen.width * Screen.height);
            float target = _config.TargetInternalMegapixels * 1000000f;
            _autoRenderScale = target > 0f
                ? Mathf.Clamp(Mathf.Sqrt(target / pixels), _config.MinRenderScale, _config.MaxRenderScale)
                : 1f;
            SetRenderScale(_autoRenderScale);
        }

        private void CycleRenderScale()
        {
            _scaleStep = (_scaleStep + 1) % ScaleSteps.Length;
            SetRenderScale(ScaleSteps[_scaleStep] > 0f ? ScaleSteps[_scaleStep] : _autoRenderScale);
        }

        private string ScaleLabel()
        {
            if (Application.isEditor)
            {
                return "Scale (device)";
            }

            return ScaleSteps[_scaleStep] > 0f ? "Scale " + ScaleSteps[_scaleStep].ToString("0.00") : "Scale auto";
        }

        private static void SetRenderScale(float scale)
        {
            if (Application.isEditor)
            {
                return;
            }

            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset != null)
            {
                asset.renderScale = scale;
            }
        }

        private static float CurrentRenderScale()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            return asset != null ? asset.renderScale : 1f;
        }

        private static bool GroupActive(GameObject[] groups)
        {
            if (groups == null)
            {
                return false;
            }

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null)
                {
                    return groups[i].activeSelf;
                }
            }

            return false;
        }

        private static void SetGroups(GameObject[] groups, bool active)
        {
            if (groups == null)
            {
                return;
            }

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null)
                {
                    groups[i].SetActive(active);
                }
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null)
            {
                module.AssignDefaultActions();
            }
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
