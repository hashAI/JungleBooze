using System;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.App.Expedition;
using JungleBooze.App.HeroBasin;
using JungleBooze.Core.Perf;
using JungleBooze.Gameplay.Run;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Device benchmark driver (ADR 0010; built by tools/build/device_bench.sh into the first scene of a bench build).
    /// Runs the phases of <see cref="DeviceBenchConfigAsset"/> in order: loads each scene, sets the orientation,
    /// poses the hero camera (landscape/portrait keyframe pose from the hero config) or lets the Perfect bot play the
    /// Expedition, and measures every frame: frame time (p50/p95/p99/max from a 0.1 ms histogram), CPU main/render and
    /// GPU time (FrameTimingManager), draw/SetPass/triangle counts and GC allocations (development builds), thermal
    /// state, memory footprint and battery (iOS). One CSV row per second plus a per-phase summary go to
    /// Documents/bench; the overlay shows the same numbers. The per-frame path does not allocate; the overlay text is
    /// one string per second (it shows up as one GC frame per row).
    /// </summary>
    public sealed class DeviceBenchRunner : MonoBehaviour
    {
        [SerializeField] private DeviceBenchConfigAsset _config;

        [Tooltip("Hero basin config (painterly): landscape and portrait camera poses.")]
        [SerializeField] private HeroBasinConfigAsset _hero;

        private readonly DeviceBenchStats _row = new DeviceBenchStats();
        private readonly DeviceBenchStats _phase = new DeviceBenchStats();
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly CharLine _line = new CharLine(640);
        private readonly CharLine _text = new CharLine(1024);
        private readonly StringBuilder _summary = new StringBuilder(4096);

        private ProfilerRecorder _draws;
        private ProfilerRecorder _setPass;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _gcAlloc;

        private DeviceBenchOverlay _overlay;
        private DeviceBenchLog _log;
        private string _header = string.Empty;
        private int _phaseIndex = -1;
        private float _phaseClock;
        private float _rowClock;
        private float _flushClock;
        private float _totalClock;
        private float _missMs = 25f;
        private int _poseFrames;
        private bool _botPending;
        private bool _done;
        private ExpeditionRoot _expedition;
        private float _phaseCap = float.MaxValue;

        public bool Done => _done;

        public int PhaseIndex => _phaseIndex;

        public string LogPath => _log != null ? _log.CsvPath : string.Empty;

        /// <summary>Tools and tests: set the script before Start.</summary>
        public void Configure(DeviceBenchConfigAsset config, HeroBasinConfigAsset hero)
        {
            _config = config;
            _hero = hero;
        }

        private void OnEnable()
        {
            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void OnDisable()
        {
            _draws.Dispose();
            _setPass.Dispose();
            _batches.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
        }

        private void Start()
        {
            if (_config == null || _config.Phases == null || _config.Phases.Length == 0)
            {
                Debug.LogError("[JungleBooze] Device bench has no config or no phases.", this);
                enabled = false;
                return;
            }

            DontDestroyOnLoad(gameObject);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = true; // Desktop cross-check runs keep going without window focus.
            string[] args = Environment.GetCommandLineArgs();
            _phaseCap = ReadPhaseCap(args);
            if (Array.IndexOf(args, "-jbUncapped") >= 0)
            {
                // Desktop cross-check: no frame cap and no vsync, so the frame time is the GPU's full-clock cost.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
            _missMs = 1000f / target * _config.MissFactor;

            var overlayObject = new GameObject("BenchOverlay");
            overlayObject.transform.SetParent(transform, false);
            _overlay = overlayObject.AddComponent<DeviceBenchOverlay>();
            _overlay.Init(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            _header = "AURELIA device bench | " + SystemInfo.deviceModel + " | " + SystemInfo.operatingSystem + " | GPU " + SystemInfo.graphicsDeviceName +
                      " | " + SystemInfo.systemMemorySize + " MB | tier " + QualityTiers.Current + ", level " + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                      ", render scale " + QualityTiers.RenderScale.ToString("0.00", CultureInfo.InvariantCulture) + " | screen " + Screen.width + "x" + Screen.height +
                      " | target " + target + " fps | " + (Debug.isDebugBuild ? "development" : "release") + " build | Unity " + Application.unityVersion +
                      " | app " + Application.version + " | started " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture);
            try
            {
                _log = new DeviceBenchLog(Path.Combine(Application.persistentDataPath, _config.LogFolder), stamp, _header);
                Debug.Log("[JungleBooze] Device bench logging to " + _log.CsvPath);
            }
            catch (Exception exception)
            {
                Debug.LogError("[JungleBooze] Device bench cannot write its log: " + exception.Message);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            StartPhase(0);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _log?.Dispose();
            _log = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _log?.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            _log?.Flush();
        }

        private void StartPhase(int index)
        {
            _phaseIndex = index;
            DeviceBenchPhase phase = _config.Phases[index];
            Screen.orientation = phase.Portrait ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            _phaseClock = 0f;
            _rowClock = 0f;
            _row.Clear();
            _phase.Clear();
            _expedition = null;
            _botPending = false;
            Debug.Log("[JungleBooze] Bench phase " + (index + 1) + "/" + _config.Phases.Length + ": " + phase.Name + " (" + phase.Scene + ", " +
                      (phase.Portrait ? "portrait" : "landscape") + ", " + phase.Seconds + " s)");
            SceneManager.LoadScene(phase.Scene, LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_phaseIndex < 0 || _done)
            {
                return;
            }

            DeviceBenchPhase phase = _config.Phases[_phaseIndex];
            _poseFrames = phase.Mode == DeviceBenchMode.HeroView ? 3 : 0;
            if (phase.Mode == DeviceBenchMode.ExpeditionBot)
            {
                // sceneLoaded runs after Awake and before Start: switch to an in-memory save before the root builds.
                _expedition = FindFirstObjectByType<ExpeditionRoot>();
                if (_expedition != null)
                {
                    _expedition.UseMemorySave(null);
                    _botPending = true;
                }
                else
                {
                    Debug.LogWarning("[JungleBooze] Bench: no ExpeditionRoot in " + scene.name + ".");
                }
            }
        }

        private void Update()
        {
            if (_done || _phaseIndex < 0)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;
            _totalClock += dt;
            _phaseClock += dt;
            _rowClock += dt;
            _flushClock += dt;

            DeviceBenchPhase phase = _config.Phases[_phaseIndex];
            bool measuring = _phaseClock > phase.WarmUpSeconds;
            DriveScene(phase);

            _row.AddFrame(ms, _missMs);
            if (measuring)
            {
                _phase.AddFrame(ms, _missMs);
            }

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                FrameTiming timing = _timings[0];
                if (timing.cpuMainThreadFrameTime > 0.0 || timing.gpuFrameTime > 0.0)
                {
                    _row.AddTiming(timing.cpuMainThreadFrameTime, timing.cpuRenderThreadFrameTime, timing.gpuFrameTime);
                    if (measuring)
                    {
                        _phase.AddTiming(timing.cpuMainThreadFrameTime, timing.cpuRenderThreadFrameTime, timing.gpuFrameTime);
                    }
                }
            }

            long gc = _gcAlloc.Valid ? _gcAlloc.LastValue : 0;
            _row.AddGc(gc);
            if (measuring)
            {
                _phase.AddGc(gc);
            }

            if (_rowClock >= _config.LogIntervalSeconds)
            {
                int thermal = DeviceHealth.ThermalState;
                long footprint = DeviceHealth.FootprintBytes;
                long available = DeviceHealth.AvailableBytes;
                _row.AddHealth(thermal, footprint, available);
                if (measuring)
                {
                    _phase.AddHealth(thermal, footprint, available);
                }

                WriteRow(phase, measuring);
                ShowLive(phase, measuring);
                _row.Clear();
                _rowClock = 0f;
            }

            if (_flushClock >= _config.FlushSeconds)
            {
                _log?.Flush();
                _flushClock = 0f;
            }

            if (_phaseClock >= phase.WarmUpSeconds + Mathf.Min(phase.Seconds, _phaseCap))
            {
                EndPhase(phase);
            }
        }

        private void DriveScene(DeviceBenchPhase phase)
        {
            if (_poseFrames > 0)
            {
                _poseFrames--;
                PoseHeroCamera(phase.Portrait);
            }

            if (_expedition == null)
            {
                return;
            }

            if (_botPending && _expedition.Session != null)
            {
                _expedition.SetBotDriving(true);
                _botPending = false;
            }

            if (_expedition.Session == null)
            {
                return;
            }

            if (_expedition.ReviveOfferSeconds > 0f)
            {
                _expedition.DeclineRevive();
            }

            if (_expedition.Session.Phase == RunPhase.Results && _expedition.ResultsShownAt > 0f &&
                Time.realtimeSinceStartup - _expedition.ResultsShownAt > _config.RunAgainSeconds)
            {
                _expedition.RunAgain();
                _expedition.SetBotDriving(true);
            }
        }

        private void PoseHeroCamera(bool portrait)
        {
            if (_hero == null)
            {
                return;
            }

            Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(portrait ? _hero.PortraitPosition : _hero.LandscapePosition,
                Quaternion.Euler(portrait ? _hero.PortraitEuler : _hero.LandscapeEuler));
            camera.fieldOfView = portrait ? _hero.PortraitFovDeg : _hero.LandscapeFovDeg;
            camera.farClipPlane = _hero.FarClipM;
        }

        private void WriteRow(DeviceBenchPhase phase, bool measuring)
        {
            if (_log == null)
            {
                return;
            }

            FrameTimeHistogram f = _row.Frames;
            float scale = QualityTiers.RenderScale;
            _line.Clear();
            _line.Append(_totalClock, 1).Append(',').Append(phase.Name).Append(',').Append(measuring ? 1L : 0L).Append(',')
                .Append(_row.Fps, 1).Append(',').Append(f.MeanMs, 2).Append(',').Append(f.Percentile(0.5), 1).Append(',')
                .Append(f.Percentile(0.95), 1).Append(',').Append(f.Percentile(0.99), 1).Append(',').Append(f.MaxMs, 1).Append(',')
                .Append(_row.Missed).Append(',').Append(_row.CpuMainMs, 2).Append(',').Append(_row.CpuRenderMs, 2).Append(',')
                .Append(_row.GpuMs, 2).Append(',').Append(_row.ThermalMax).Append(',').Append(Mb(_row.FootprintMaxBytes)).Append(',')
                .Append(Mb(_row.AvailableMinBytes)).Append(',').Append(Battery()).Append(',').Append(DeviceHealth.LowPowerMode ? 1L : 0L).Append(',')
                .Append(Counter(_draws)).Append(',').Append(Counter(_setPass)).Append(',').Append(Counter(_batches)).Append(',')
                .Append(Counter(_triangles)).Append(',').Append(_row.GcFrames).Append(',').Append(_row.GcMaxBytes).Append(',')
                .Append((long)(Screen.width * scale)).Append(',').Append((long)(Screen.height * scale));
            _log.WriteRow(_line);
        }

        private void ShowLive(DeviceBenchPhase phase, bool measuring)
        {
            if (_overlay == null)
            {
                return;
            }

            FrameTimeHistogram f = _row.Frames;
            FrameTimeHistogram p = _phase.Frames;
            int thermal = _row.ThermalMax;
            _text.Clear();
            _text.Append("BENCH ").Append(_phaseIndex + 1L).Append('/').Append(_config.Phases.Length).Append(' ').Append(phase.Name).Append("  ")
                .Append((long)_phaseClock).Append('/').Append((long)(phase.WarmUpSeconds + Mathf.Min(phase.Seconds, _phaseCap))).Append(" s").Append(measuring ? string.Empty : " (warm-up)").Append('\n');
            _text.Append("FPS ").Append(_row.Fps, 1).Append("  p50 ").Append(f.Percentile(0.5), 1).Append("  p95 ").Append(f.Percentile(0.95), 1)
                .Append("  p99 ").Append(f.Percentile(0.99), 1).Append("  max ").Append(f.MaxMs, 1).Append(" ms\n");
            _text.Append("phase p95 ").Append(p.Percentile(0.95), 1).Append("  p99 ").Append(p.Percentile(0.99), 1).Append("  missed ").Append(_phase.Missed).Append('\n');
            _text.Append("CPU main ").Append(_row.CpuMainMs, 1).Append("  render ").Append(_row.CpuRenderMs, 1).Append("  GPU ").Append(_row.GpuMs, 1).Append(" ms\n");
            _text.Append("thermal ").Append(DeviceHealth.ThermalName(thermal)).Append("  mem ").Append(Mb(_row.FootprintMaxBytes)).Append(" MB (free ")
                .Append(Mb(_row.AvailableMinBytes)).Append(")  bat ").Append(Battery()).Append("%").Append(DeviceHealth.LowPowerMode ? " LOW POWER" : string.Empty).Append('\n');
            _text.Append("draws ").Append(Counter(_draws)).Append("  setpass ").Append(Counter(_setPass)).Append("  tris ").Append(Counter(_triangles))
                .Append("  GC ").Append(_row.GcTotalBytes).Append(" B\n");
            _text.Append((long)(Screen.width * QualityTiers.RenderScale)).Append('x').Append((long)(Screen.height * QualityTiers.RenderScale)).Append(" (x")
                .Append(QualityTiers.RenderScale, 2).Append(")  ").Append(QualityTiers.Current == DeviceTier.High ? "High" : "Low").Append("  ").Append(SystemInfo.deviceModel);
            _overlay.Show(_text.ToString(), false);
        }

        private void EndPhase(DeviceBenchPhase phase)
        {
            FrameTimeHistogram p = _phase.Frames;
            var line = new StringBuilder(256);
            line.Append(phase.Name.PadRight(22)).Append(' ').Append(_phase.Seconds.ToString("0", CultureInfo.InvariantCulture)).Append(" s")
                .Append("  fps ").Append(_phase.Fps.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  p50 ").Append(p.Percentile(0.5).ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  p95 ").Append(p.Percentile(0.95).ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  p99 ").Append(p.Percentile(0.99).ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  max ").Append(p.MaxMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms")
                .Append("  missed ").Append((p.Count > 0 ? 100.0 * _phase.Missed / p.Count : 0.0).ToString("0.00", CultureInfo.InvariantCulture)).Append('%')
                .Append("  CPU ").Append(_phase.CpuMainMs.ToString("0.0", CultureInfo.InvariantCulture)).Append('/').Append(_phase.CpuRenderMs.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("  GPU ").Append(_phase.GpuMs.ToString("0.0", CultureInfo.InvariantCulture)).Append(" ms")
                .Append("  thermal max ").Append(DeviceHealth.ThermalName(_phase.ThermalMax))
                .Append("  mem max ").Append(Mb(_phase.FootprintMaxBytes)).Append(" MB")
                .Append("  GC frames ").Append(_phase.GcFrames);
            string text = line.ToString();
            _summary.AppendLine(text);
            _log?.WriteSummary(text);
            _log?.Flush();
            Debug.Log("[JungleBooze] Bench " + text);

            if (_phaseIndex + 1 < _config.Phases.Length)
            {
                StartPhase(_phaseIndex + 1);
                return;
            }

            _done = true;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            string done = "BENCH DONE in " + (_totalClock / 60f).ToString("0.0", CultureInfo.InvariantCulture) + " min. Logs: Documents/" + _config.LogFolder + "\n" + _summary;
            _log?.WriteSummary("done after " + _totalClock.ToString("0", CultureInfo.InvariantCulture) + " s");
            _overlay?.Show(done, true);
            Debug.Log("[JungleBooze] " + done);
        }

        /// <summary>Desktop cross-check runs: "-jbBenchSeconds N" caps every phase at N measured seconds.</summary>
        private static float ReadPhaseCap(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-jbBenchSeconds" && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds > 0f)
                {
                    return seconds;
                }
            }

            return float.MaxValue;
        }

        private static long Counter(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : -1;
        }

        private static long Mb(long bytes)
        {
            return bytes >= 0 ? bytes / (1024 * 1024) : -1;
        }

        private static long Battery()
        {
            float level = SystemInfo.batteryLevel;
            return level >= 0f ? (long)Mathf.Round(level * 100f) : -1;
        }
    }
}
