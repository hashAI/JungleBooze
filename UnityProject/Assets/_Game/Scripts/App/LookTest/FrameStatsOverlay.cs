using System;
using System.Text;
using JungleBooze.UI.Common;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// On-screen performance readout for the look test on iPhone (ADR 0004): frames per second, average and worst
    /// frame time and the slowest 1% over the last window and since the last reset, CPU main/render thread and GPU
    /// time (FrameTimingManager; needs Player setting "Frame Timing Stats", which the scene builder turns on), draw
    /// calls, batches, SetPass calls, triangles and system memory (Profiler counters; shown as "n/a" where a build
    /// type does not provide them), screen and render resolution, device model.
    /// Sampling is allocation-free; the text is rebuilt only every <c>refreshSeconds</c> (a small string allocation
    /// each refresh, a known exception for this debug overlay). Statistics reset automatically after a warm-up and
    /// on <see cref="ResetStats"/>.
    /// </summary>
    public sealed class FrameStatsOverlay : MonoBehaviour
    {
        private const int HistoryFrames = 1200;
        private const float WarmUpSeconds = 3f;
        private const int FontSize = 13;

        private readonly float[] _history = new float[HistoryFrames];
        private readonly float[] _sortScratch = new float[HistoryFrames];
        private readonly FrameTiming[] _timings = new FrameTiming[1];
        private readonly StringBuilder _text = new StringBuilder(512);

        private Text _label;
        private float _refreshSeconds = 0.5f;
        private int _historyCount;
        private int _historyNext;

        private float _windowElapsed;
        private int _windowFrames;
        private float _windowWorstMs;
        private double _windowCpuMainMs;
        private double _windowCpuRenderMs;
        private double _windowGpuMs;
        private int _windowTimingSamples;

        private double _totalSeconds;
        private long _totalFrames;
        private float _totalWorstMs;
        private float _sinceStart;
        private bool _warmUpDone;
        private float _hitchThresholdMs = 20f;

        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPass;
        private ProfilerRecorder _triangles;
        private ProfilerRecorder _systemMemory;

        private Func<float> _renderScale;

        /// <summary>Builds the overlay canvas. <paramref name="renderScale"/> reports the current render scale.</summary>
        public void Init(Font font, float refreshSeconds, int targetFrameRate, Func<float> renderScale)
        {
            _refreshSeconds = Mathf.Max(0.1f, refreshSeconds);
            _renderScale = renderScale;
            SetTargetFrameRate(targetFrameRate);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(844f, 390f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Image panel = HudFactory.CreateImage(safe, "StatsBackground", new Color(0f, 0f, 0f, 0.55f), false);
            HudFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(300f, 158f), new Vector2(6f, -6f));

            _label = HudFactory.CreateText(panel.transform, "Stats", font, FontSize, Color.white, new Color(0f, 0f, 0f, 0f), TextAnchor.UpperLeft);
            _label.fontStyle = FontStyle.Normal;
            HudFactory.Stretch(_label.rectTransform, 6f);
            _label.text = "Measuring...";
        }

        /// <summary>Frames slower than the target frame time + 4 ms count as hitches.</summary>
        public void SetTargetFrameRate(int targetFrameRate)
        {
            _hitchThresholdMs = 1000f / Mathf.Max(1, targetFrameRate) + 4f;
        }

        /// <summary>Clears the "since reset" statistics and the 1% history (after a settings change).</summary>
        public void ResetStats()
        {
            _historyCount = 0;
            _historyNext = 0;
            _totalSeconds = 0.0;
            _totalFrames = 0L;
            _totalWorstMs = 0f;
        }

        private void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _systemMemory.Dispose();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;

            _sinceStart += dt;
            if (!_warmUpDone && _sinceStart >= WarmUpSeconds)
            {
                _warmUpDone = true;
                ResetStats();
            }

            _history[_historyNext] = ms;
            _historyNext = (_historyNext + 1) % HistoryFrames;
            if (_historyCount < HistoryFrames)
            {
                _historyCount++;
            }

            _totalSeconds += dt;
            _totalFrames++;
            if (ms > _totalWorstMs)
            {
                _totalWorstMs = ms;
            }

            _windowElapsed += dt;
            _windowFrames++;
            if (ms > _windowWorstMs)
            {
                _windowWorstMs = ms;
            }

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
            {
                FrameTiming timing = _timings[0];
                if (timing.cpuMainThreadFrameTime > 0.0 || timing.gpuFrameTime > 0.0)
                {
                    _windowCpuMainMs += timing.cpuMainThreadFrameTime;
                    _windowCpuRenderMs += timing.cpuRenderThreadFrameTime;
                    _windowGpuMs += timing.gpuFrameTime;
                    _windowTimingSamples++;
                }
            }

            if (_windowElapsed >= _refreshSeconds && _label != null)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            float windowAvgMs = _windowElapsed * 1000f / Mathf.Max(1, _windowFrames);
            double totalAvgMs = _totalFrames > 0 ? _totalSeconds * 1000.0 / _totalFrames : 0.0;

            _text.Clear();
            _text.Append("FPS ").Append((1000f / Mathf.Max(0.01f, windowAvgMs)).ToString("0.0"))
                .Append("   frame ").Append(windowAvgMs.ToString("0.0")).Append(" ms   worst ")
                .Append(_windowWorstMs.ToString("0.0")).Append(" ms\n");

            if (_windowTimingSamples > 0)
            {
                _text.Append("CPU main ").Append((_windowCpuMainMs / _windowTimingSamples).ToString("0.0"))
                    .Append("  render ").Append((_windowCpuRenderMs / _windowTimingSamples).ToString("0.0"))
                    .Append("  GPU ").Append((_windowGpuMs / _windowTimingSamples).ToString("0.0")).Append(" ms\n");
            }
            else
            {
                _text.Append("CPU/GPU timing n/a (Frame Timing Stats off)\n");
            }

            _text.Append("Since reset: ").Append(_totalSeconds.ToString("0")).Append(" s  avg ")
                .Append((totalAvgMs > 0.0 ? 1000.0 / totalAvgMs : 0.0).ToString("0.0")).Append(" fps\n")
                .Append("  slowest 1% ").Append(SlowestPercentMs().ToString("0.0")).Append(" ms  worst ")
                .Append(_totalWorstMs.ToString("0.0")).Append(" ms  hitches ").Append(CountHitches()).Append('\n');

            _text.Append("Draws ").Append(Counter(_drawCalls)).Append("  batches ").Append(Counter(_batches))
                .Append("  setpass ").Append(Counter(_setPass)).Append('\n')
                .Append("Tris ").Append(Counter(_triangles)).Append("  mem ")
                .Append(_systemMemory.Valid && _systemMemory.LastValue > 0 ? (_systemMemory.LastValue / (1024 * 1024)).ToString() + " MB" : "n/a")
                .Append('\n');

            float scale = _renderScale != null ? _renderScale() : 1f;
            _text.Append(Screen.width).Append('x').Append(Screen.height).Append(" @ ").Append(scale.ToString("0.00"))
                .Append("  ").Append(SystemInfo.deviceModel);

            _label.text = _text.ToString();

            _windowElapsed = 0f;
            _windowFrames = 0;
            _windowWorstMs = 0f;
            _windowCpuMainMs = 0.0;
            _windowCpuRenderMs = 0.0;
            _windowGpuMs = 0.0;
            _windowTimingSamples = 0;
        }

        private static string Counter(ProfilerRecorder recorder)
        {
            return recorder.Valid && recorder.LastValue > 0 ? recorder.LastValue.ToString() : "n/a";
        }

        /// <summary>Average of the slowest 1% of frames in the history (at least one frame).</summary>
        private float SlowestPercentMs()
        {
            if (_historyCount == 0)
            {
                return 0f;
            }

            Array.Copy(_history, _sortScratch, _historyCount);
            Array.Sort(_sortScratch, 0, _historyCount);
            int take = Mathf.Max(1, _historyCount / 100);
            float sum = 0f;
            for (int i = _historyCount - take; i < _historyCount; i++)
            {
                sum += _sortScratch[i];
            }

            return sum / take;
        }

        private int CountHitches()
        {
            int count = 0;
            for (int i = 0; i < _historyCount; i++)
            {
                if (_history[i] > _hitchThresholdMs)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
