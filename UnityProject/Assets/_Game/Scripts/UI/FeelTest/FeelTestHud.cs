using System;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.FeelTest
{
    /// <summary>
    /// Gray-box HUD for the Phase 1 feel test (ui-engineer polishes later): distance, three health segments (and a
    /// shield marker), a pause button, a centre message (ready / paused / resume countdown), the results panel with
    /// RUN AGAIN, a debug text block (F1) and a controls hint. Built from code with <see cref="HudFactory"/>;
    /// distance and health updates only touch the UI when values change and never allocate.
    /// </summary>
    public sealed class FeelTestHud : MonoBehaviour
    {
        private static readonly Color HealthFull = new Color(0.36f, 0.78f, 0.38f, 1f);
        private static readonly Color HealthEmpty = new Color(0.18f, 0.16f, 0.2f, 0.75f);
        private static readonly Color ShieldColor = new Color(0.35f, 0.75f, 1f, 1f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f, 0.96f);
        private static readonly Color Ink = new Color(0.12f, 0.1f, 0.14f, 1f);
        private static readonly Color Leaf = new Color(0.3f, 0.62f, 0.32f, 1f);

        private readonly IntStringCache _metres = new IntStringCache(2001, " m");
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safe;
        private Text _distance;
        private Image[] _segments;
        private Image _shield;
        private Text _center;
        private RectTransform _pauseButton;
        private GameObject _results;
        private RectTransform _resultsRect;
        private Text _resultsTitle;
        private Text _resultsBody;
        private Text _debug;
        private Text _hint;
        private int _shownDistance = -1;
        private int _shownHealth = -1;
        private bool _shownShield;
        private bool _landscape = true;
        private GameObject _settings;
        private RectTransform _settingsRect;
        private Text _sensitivityValue;
        private Text _hapticsLabel;
        private Text _reducedMotionLabel;
        private float _shownSensitivity = -1f;
        private int _shownHaptics = -1;
        private int _shownReducedMotion = -1;

        public bool ResultsVisible => _results != null && _results.activeSelf;

        public bool SettingsVisible => _settings != null && _settings.activeSelf;

        public string SensitivityText => _sensitivityValue != null ? _sensitivityValue.text : string.Empty;

        public bool DebugVisible => _debug != null && _debug.gameObject.activeSelf;

        public string DebugText => _debug != null ? _debug.text : string.Empty;

        public Canvas Canvas => _canvas;

        public void Build(Font font, int maxHealth, Action onPause, Action onRestart)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            SetOrientation(Screen.width >= Screen.height);

            _safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(_safe, 0f);
            _safe.gameObject.AddComponent<SafeAreaFitter>();

            Image distancePanel = HudFactory.CreatePanel(_safe, "DistancePanel", Parchment, new Vector2(0f, 1f), new Vector2(150f, 52f), new Vector2(16f, -12f));
            _distance = HudFactory.CreateText(distancePanel.transform, "Distance", font, 30, Ink, Color.clear, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_distance.rectTransform, 2f);
            _distance.text = _metres.Get(0);

            RectTransform health = HudFactory.CreateRect(_safe, "Health");
            HudFactory.Place(health, new Vector2(0.5f, 1f), new Vector2(64f * maxHealth, 30f), new Vector2(0f, -22f));
            _segments = new Image[maxHealth];
            for (int i = 0; i < maxHealth; i++)
            {
                Image fill = HudFactory.CreatePanel(health, "Segment" + i, HealthFull, new Vector2(0f, 0.5f), new Vector2(56f, 24f), new Vector2(4f + (i * 64f), 0f));
                _segments[i] = fill;
            }

            _shield = HudFactory.CreateImage(health, "Shield", ShieldColor, false);
            HudFactory.Place(_shield.rectTransform, new Vector2(1f, 0.5f), new Vector2(18f, 18f), new Vector2(26f, 0f));
            _shield.gameObject.SetActive(false);

            Button pause = HudFactory.CreateButton(_safe, "Pause", Parchment, new Vector2(1f, 1f), new Vector2(56f, 52f), new Vector2(-16f, -12f), font, "II", 26, Ink, Color.clear, () => onPause?.Invoke());
            _pauseButton = (RectTransform)pause.transform.parent;

            _center = HudFactory.CreateText(_safe, "Center", font, 54, Color.white, Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_center.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(600f, 80f), Vector2.zero);
            _center.gameObject.SetActive(false);

            Image panel = HudFactory.CreatePanel(_safe, "Results", Parchment, new Vector2(0.5f, 0.5f), new Vector2(380f, 330f), Vector2.zero);
            _results = panel.transform.parent.gameObject;
            _resultsRect = (RectTransform)_results.transform;
            _resultsTitle = HudFactory.CreateText(panel.transform, "Title", font, 30, Ink, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_resultsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(360f, 40f), new Vector2(0f, -14f));
            _resultsBody = HudFactory.CreateText(panel.transform, "Body", font, 19, Ink, Color.clear, TextAnchor.UpperLeft);
            _resultsBody.fontStyle = FontStyle.Normal;
            _resultsBody.verticalOverflow = VerticalWrapMode.Overflow;
            HudFactory.Place(_resultsBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, 180f), new Vector2(0f, -62f));
            HudFactory.CreateButton(panel.transform, "RunAgain", Leaf, new Vector2(0.5f, 0f), new Vector2(240f, 60f), new Vector2(0f, 18f), font, "RUN AGAIN", 28, Color.white, Ink, () => onRestart?.Invoke());
            _results.SetActive(false);

            _debug = HudFactory.CreateText(_safe, "Debug", font, 15, Color.white, Ink, TextAnchor.UpperLeft);
            _debug.fontStyle = FontStyle.Normal;
            HudFactory.Place(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(420f, 300f), new Vector2(16f, -80f));
            _debug.gameObject.SetActive(false);

            _hint = HudFactory.CreateText(_safe, "Hint", font, 14, new Color(1f, 1f, 1f, 0.85f), Ink, TextAnchor.LowerCenter);
            _hint.fontStyle = FontStyle.Normal;
            HudFactory.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(800f, 24f), new Vector2(0f, 8f));
        }

        /// <summary>
        /// Settings panel shown while paused: steering sensitivity (− / +), haptics and reduced motion toggles.
        /// Values are persisted by the caller (FeelSettings).
        /// </summary>
        public void BuildSettings(Font font, Action<int> onSensitivityStep, Action onHapticsToggle, Action onReducedMotionToggle)
        {
            Image panel = HudFactory.CreatePanel(_safe, "Settings", Parchment, new Vector2(0.5f, 0.32f), new Vector2(420f, 170f), Vector2.zero);
            _settings = panel.transform.parent.gameObject;
            _settingsRect = (RectTransform)_settings.transform;

            Text label = HudFactory.CreateText(panel.transform, "SensitivityLabel", font, 20, Ink, Color.clear, TextAnchor.MiddleLeft);
            HudFactory.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(160f, 44f), new Vector2(18f, -12f));
            label.text = "Steering";
            HudFactory.CreateButton(panel.transform, "SensitivityDown", Parchment, new Vector2(0f, 1f), new Vector2(48f, 44f), new Vector2(180f, -10f), font, "-", 26, Ink, Color.clear, () => onSensitivityStep?.Invoke(-1));
            _sensitivityValue = HudFactory.CreateText(panel.transform, "SensitivityValue", font, 22, Ink, Color.clear, TextAnchor.MiddleCenter);
            HudFactory.Place(_sensitivityValue.rectTransform, new Vector2(0f, 1f), new Vector2(84f, 44f), new Vector2(236f, -12f));
            HudFactory.CreateButton(panel.transform, "SensitivityUp", Parchment, new Vector2(0f, 1f), new Vector2(48f, 44f), new Vector2(328f, -10f), font, "+", 26, Ink, Color.clear, () => onSensitivityStep?.Invoke(1));

            Button haptics = HudFactory.CreateButton(panel.transform, "Haptics", Parchment, new Vector2(0f, 0f), new Vector2(180f, 48f), new Vector2(18f, 22f), font, "Haptics", 18, Ink, Color.clear, () => onHapticsToggle?.Invoke());
            _hapticsLabel = haptics.GetComponentInChildren<Text>();
            Button reduced = HudFactory.CreateButton(panel.transform, "ReducedMotion", Parchment, new Vector2(1f, 0f), new Vector2(200f, 48f), new Vector2(-18f, 22f), font, "Motion", 18, Ink, Color.clear, () => onReducedMotionToggle?.Invoke());
            _reducedMotionLabel = reduced.GetComponentInChildren<Text>();
            _settings.SetActive(false);
        }

        public void SetSettingsVisible(bool visible)
        {
            if (_settings != null && _settings.activeSelf != visible)
            {
                _settings.SetActive(visible);
            }
        }

        /// <summary>Updates the settings labels (only when a value changed).</summary>
        public void SetSettingsValues(float sensitivity, bool haptics, bool reducedMotion)
        {
            if (_settings == null)
            {
                return;
            }

            if (!Mathf.Approximately(sensitivity, _shownSensitivity))
            {
                _shownSensitivity = sensitivity;
                _sensitivityValue.text = sensitivity.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "x";
            }

            int h = haptics ? 1 : 0;
            if (h != _shownHaptics && _hapticsLabel != null)
            {
                _shownHaptics = h;
                _hapticsLabel.text = haptics ? "Haptics: ON" : "Haptics: OFF";
            }

            int r = reducedMotion ? 1 : 0;
            if (r != _shownReducedMotion && _reducedMotionLabel != null)
            {
                _shownReducedMotion = r;
                _reducedMotionLabel.text = reducedMotion ? "Reduced motion: ON" : "Reduced motion: OFF";
            }
        }

        public void SetOrientation(bool landscape)
        {
            _landscape = landscape;
            if (_scaler != null)
            {
                _scaler.referenceResolution = landscape ? new Vector2(844f, 390f) : new Vector2(390f, 844f);
            }
        }

        public bool Landscape => _landscape;

        public void SetDistance(float metres)
        {
            int value = metres < 0f ? 0 : (int)metres;
            if (value != _shownDistance)
            {
                _shownDistance = value;
                _distance.text = _metres.Get(value);
            }
        }

        public void SetHealth(int health, bool shield)
        {
            if (health != _shownHealth)
            {
                _shownHealth = health;
                for (int i = 0; i < _segments.Length; i++)
                {
                    _segments[i].color = i < health ? HealthFull : HealthEmpty;
                }
            }

            if (shield != _shownShield)
            {
                _shownShield = shield;
                _shield.gameObject.SetActive(shield);
            }
        }

        /// <summary>Centre message; null or empty hides it. Pass cached strings to avoid allocation.</summary>
        public void SetCenter(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            if (_center.gameObject.activeSelf != show)
            {
                _center.gameObject.SetActive(show);
            }

            if (show && !ReferenceEquals(_center.text, message))
            {
                _center.text = message;
            }
        }

        public void ShowResults(string title, string body)
        {
            _resultsTitle.text = title;
            _resultsBody.text = body;
            _results.SetActive(true);
        }

        public void HideResults()
        {
            _results.SetActive(false);
        }

        public void SetDebugVisible(bool visible)
        {
            _debug.gameObject.SetActive(visible);
        }

        public void SetDebugText(string text)
        {
            _debug.text = text;
        }

        public void SetHint(string text)
        {
            _hint.text = text;
            _hint.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>True if a screen pixel is over a HUD control (touches there belong to the UI).</summary>
        public bool HitsControl(Vector2 screenPixel)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_pauseButton, screenPixel, null))
            {
                return true;
            }

            if (SettingsVisible && RectTransformUtility.RectangleContainsScreenPoint(_settingsRect, screenPixel, null))
            {
                return true;
            }

            return ResultsVisible && RectTransformUtility.RectangleContainsScreenPoint(_resultsRect, screenPixel, null);
        }
    }
}
