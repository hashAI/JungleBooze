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
    /// (JungleBooze > Look Test > Build Scene) places this component and fills in its references. At Play a stand-in
    /// Pista runs forward at the configured speed with a slow side-to-side weave (free horizontal movement, no lanes),
    /// a smoothed third-person camera follows her, <see cref="LookTestWorldView"/> loops the stretch, and
    /// <see cref="FrameStatsOverlay"/> / <see cref="LookTestQualityPanel"/> measure it. Presentation only: the look
    /// test has no gameplay simulation, so it reads frame time directly.
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

        private const float StandInHeightM = 1.65f;
        private const float WeaveAmplitudeM = 1.6f;
        private const float WeavePeriodS = 7f;
        private const float CameraSmoothingS = 0.12f;

        private LookTestWorldView _worldView;
        private Transform _runner;
        private double _distanceM;
        private float _runTimeS;
        private bool _paused;
        private Vector3 _cameraVelocity;
        private FrameStatsOverlay _overlay;
        private UniversalAdditionalCameraData _cameraData;
        private LightShadows _sunShadows = LightShadows.Soft;
        private int _scaleStep;
        private float _autoRenderScale = 1f;

        public LookTestConfigAsset Config => _config;

        /// <summary>Distance the stand-in has run, in meters.</summary>
        public double DistanceM => _distanceM;

        public bool Paused => _paused;

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

            _runner = CreateStandIn(transform);
            _camera.farClipPlane = _config.CameraFarClipM;
            _camera.fieldOfView = _config.CameraFovDeg;
            _cameraData = _camera.GetUniversalAdditionalCameraData();

            _worldView = _stretchRoot.GetComponent<LookTestWorldView>();
            if (_worldView == null)
            {
                _worldView = _stretchRoot.gameObject.AddComponent<LookTestWorldView>();
            }

            _worldView.Init(_segments, _config.LoopLengthM, _config.RecycleBehindM);
            PlaceRunnerAndCamera(true);

            if (_sun != null && _sun.shadows != LightShadows.None)
            {
                _sunShadows = _sun.shadows;
            }

            BuildOverlay();
            Debug.Log("[JungleBooze] Look test running. Speed " + _config.RunSpeedMps + " m/s.");
        }

        private void Update()
        {
            if (_runner == null || _paused)
            {
                return;
            }

            float dt = Time.deltaTime;
            _runTimeS += dt;
            _distanceM += _config.RunSpeedMps * dt;
            PlaceRunnerAndCamera(false);
        }

        private void PlaceRunnerAndCamera(bool snap)
        {
            float x = WeaveAmplitudeM * Mathf.Sin(_runTimeS * (2f * Mathf.PI / WeavePeriodS));
            float z = (float)_distanceM;
            _runner.localPosition = new Vector3(x, 0f, z);
            _worldView.Render(_distanceM);

            // The camera follows the run line (half the weave) so the runner visibly moves across the screen.
            Vector3 target = new Vector3(x * 0.5f, _config.CameraOffsetUpM, z - _config.CameraOffsetBehindM);
            Transform cam = _camera.transform;
            cam.position = snap
                ? target
                : Vector3.SmoothDamp(cam.position, target, ref _cameraVelocity, CameraSmoothingS);
            cam.LookAt(new Vector3(x * 0.5f, _config.CameraLookAtHeightM, z + _config.CameraLookAheadM));
        }

        /// <summary>Capsule stand-in for Pista until the rigged model is imported; casts and receives real shadows.</summary>
        private static Transform CreateStandIn(Transform parent)
        {
            var root = new GameObject("Pista (stand-in)");
            root.transform.SetParent(parent, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.45f, StandInHeightM * 0.5f, 0.45f);
            body.transform.localPosition = new Vector3(0f, StandInHeightM * 0.5f, 0f);
            MeshRenderer meshRenderer = body.GetComponent<MeshRenderer>();
            meshRenderer.material.color = new Color(0.93f, 0.52f, 0.18f, 1f);
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            return root.transform;
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
            panel.AddAction(() => _paused ? "Resume" : "Pause", () => _paused = !_paused);
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
