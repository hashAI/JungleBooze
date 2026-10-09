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
        [SerializeField] private Transform _backdrop;
        [SerializeField] private GameObject[] _detailGroups;

        private const float StandInHeightM = 1.65f;
        private const float CameraSmoothingS = 0.12f;

        private LookTestWorldView _worldView;
        private LookTestPath _path;
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
        private float _benchmarkSeconds;
        private float _nextBenchmarkLogS = BenchmarkLogIntervalS;

        private const float BenchmarkLogIntervalS = 2f;

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
            GameObject[] waterGroups,
            Transform backdrop,
            GameObject[] detailGroups)
        {
            _config = config;
            _camera = targetCamera;
            _sun = sun;
            _stretchRoot = stretchRoot;
            _segments = segments;
            _plantGroups = plantGroups;
            _waterGroups = waterGroups;
            _backdrop = backdrop;
            _detailGroups = detailGroups;
        }

        private void Start()
        {
            if (_config == null || _camera == null || _stretchRoot == null || _segments == null || _segments.Length == 0)
            {
                Debug.LogError("[JungleBooze] Look test scene is incomplete. Run JungleBooze > Look Test > Build Scene again.", this);
                return;
            }

            Application.targetFrameRate = _config.TargetFrameRate;
            AllowBothOrientations();
            ApplyAutoRenderScale();

            _runner = CreateStandIn(transform);
            _camera.farClipPlane = _config.CameraFarClipM;
            _path = _config.CreatePath();
            _cameraData = _camera.GetUniversalAdditionalCameraData();

            _worldView = _stretchRoot.GetComponent<LookTestWorldView>();
            if (_worldView == null)
            {
                _worldView = _stretchRoot.gameObject.AddComponent<LookTestWorldView>();
            }

            _worldView.Init(_segments, _config.LoopLengthM, _path.LoopOffset, _config.RecycleBehindM, _backdrop);
            _worldView.InitDetail(_detailGroups, _config.DetailRangeM);
            PlaceRunnerAndCamera(true);

            if (_sun != null && _sun.shadows != LightShadows.None)
            {
                _sunShadows = _sun.shadows;
            }

            BuildOverlay();
            _benchmarkSeconds = ReadBenchmarkSeconds(System.Environment.GetCommandLineArgs());
            if (_benchmarkSeconds > 0f)
            {
                Application.runInBackground = true;
            }

            Debug.Log("[JungleBooze] Look test running. Speed " + _config.RunSpeedMps + " m/s." +
                      (_benchmarkSeconds > 0f ? " Benchmark for " + _benchmarkSeconds + " s." : string.Empty));
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

            if (_benchmarkSeconds > 0f && _runTimeS >= _nextBenchmarkLogS)
            {
                _nextBenchmarkLogS += BenchmarkLogIntervalS;
                Debug.Log("[JungleBooze] Benchmark t=" + _runTimeS.ToString("0") + " s, z=" + _distanceM.ToString("0") + " m\n" + _overlay.LastReport);
                if (_runTimeS >= _benchmarkSeconds)
                {
                    Application.Quit();
                }
            }
        }

        /// <summary>
        /// "-jbBenchmark &lt;seconds&gt;" on the player command line: log the overlay every 2 s, then quit
        /// (used to read draw calls and triangles from a development player). Returns 0 when absent or invalid.
        /// </summary>
        public static float ReadBenchmarkSeconds(string[] args)
        {
            if (args == null)
            {
                return 0f;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-jbBenchmark" &&
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds) &&
                    seconds > 0f)
                {
                    return seconds;
                }
            }

            return 0f;
        }

        private void PlaceRunnerAndCamera(bool snap)
        {
            float x = LookTestCameraRig.RunnerX(_runTimeS);
            _runner.localPosition = LookTestCameraRig.RunnerPosition(_path, _distanceM, x);
            _runner.localRotation = Quaternion.Euler(0f, _path.HeadingRad(_distanceM) * Mathf.Rad2Deg, 0f);
            _worldView.Render(_distanceM);

            LookTestCameraProfile profile = LookTestCameraRig.Profile(_config, _camera.aspect);
            LookTestCameraRig.Pose(_path, profile, _distanceM, x, out Vector3 target, out Quaternion rotation);
            Transform cam = _camera.transform;
            cam.position = snap
                ? target
                : Vector3.SmoothDamp(cam.position, target, ref _cameraVelocity, CameraSmoothingS);
            cam.rotation = rotation;
            _camera.fieldOfView = profile.VerticalFovDeg;
        }

        /// <summary>Capsule stand-in for Pista until the rigged model is imported; casts and receives real shadows. Also used by the editor screenshot tool.</summary>
        public static Transform CreateStandIn(Transform parent)
        {
            var root = new GameObject("Pista (stand-in)");
            root.transform.SetParent(parent, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "FX Pista stand-in";
            Collider collider = body.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }

            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.45f, StandInHeightM * 0.5f, 0.45f);
            body.transform.localPosition = new Vector3(0f, StandInHeightM * 0.5f, 0f);
            MeshRenderer meshRenderer = body.GetComponent<MeshRenderer>();
            var material = new Material(meshRenderer.sharedMaterial) { name = "Pista stand-in" };
            material.color = new Color(0.93f, 0.52f, 0.18f, 1f);
            meshRenderer.sharedMaterial = material;
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
            panel.AddToggle("Plants", () => GroupActive(_plantGroups), on =>
            {
                SetGroups(_plantGroups, on);
                _worldView.InitDetail(on ? _detailGroups : null, _config.DetailRangeM);
                SetGroups(_detailGroups, on);
            });
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

        private static void AllowBothOrientations()
        {
            // Landscape and portrait: the owner rotates the phone to compare (ADR 0004 Decision 8). The camera
            // adapts its field of view and aim to the aspect every frame (LookTestCameraRig).
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = true;
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
