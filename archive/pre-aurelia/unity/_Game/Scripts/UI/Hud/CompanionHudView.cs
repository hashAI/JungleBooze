using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// The companion's small HUD addition, on its own overlay canvas so the main HUD stays untouched:
    /// <list type="bullet">
    /// <item>Lift meter (style guide 8.1: violet gauge under the score, top-left in the safe area). When full it
    /// pulses and shows how to trigger Lift (double tap; double-click or E in the editor).</item>
    /// <item>Call-out bubble: until audio exists, each call-out (GDD 15.1) shows as a short word in a comic speech
    /// bubble just above the macaw (text from <see cref="CompanionStrings"/> by language-neutral id), with a punch-in
    /// stamp. "Look out!" uses pulp orange, cheers sun gold, "Vine!" parchment; never hazard red.</item>
    /// </list>
    /// Same 390 × 844 pt reference as the HUD. Nothing here takes touches. No allocations per frame.
    /// </summary>
    public sealed class CompanionHudView : MonoBehaviour, IRunView
    {
        private const float MarginPt = 12f;
        private const float MeterTopPt = 112f;
        private const float MeterWidthPt = 150f;
        private const float MeterHeightPt = 34f;
        private const float BarLeftPt = 54f;
        private const int LabelFontSize = 15;
        private const int BubbleFontSize = 22;
        private const float BubbleWidthPt = 150f;
        private const float BubbleHeightPt = 44f;
        private const float BubbleLiftPt = 8f;
        private const float PunchSeconds = 0.12f;
        private const float PunchScale = 1.3f;
        private const float PulseHz = 2f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Camera _camera;
        private CompanionView _bird;
        private CompanionConfig _config;
        private GameObject _meterRoot;
        private RectTransform _meterFill;
        private Image _meterFillImage;
        private Text _meterLabel;
        private Text _meterHint;
        private RectTransform _bubbleRoot;
        private Image _bubbleFill;
        private Text _bubbleText;
        private float _bubbleLeft;
        private float _bubbleAge;
        private float _pulseClock;
        private float _shownFill = -1f;
        private bool _shownFull;
        private bool _meterVisible;

        /// <summary>Meter fill shown last frame (0..1; tests).</summary>
        public float ShownMeterFill => _shownFill;

        public bool BubbleVisible => _bubbleRoot != null && _bubbleRoot.gameObject.activeSelf;

        public string BubbleText => _bubbleText != null ? _bubbleText.text : string.Empty;

        /// <summary>Builds the canvas. Call once, right after AddComponent on a RectTransform object.</summary>
        public void Build(Font font, CompanionConfig config, Camera worldCamera, CompanionView bird)
        {
            _config = config ?? CompanionConfig.CreateDefault();
            _camera = worldCamera;
            _bird = bird;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9;
            _canvas.pixelPerfect = false;
            _canvasRect = (RectTransform)transform;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudView.ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            Color none = new Color(0f, 0f, 0f, 0f);

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Lift meter under the score panel.
            Image panel = HudFactory.CreatePanel(
                safe, "LiftMeter", StylePalette.Parchment, new Vector2(0f, 1f), new Vector2(MeterWidthPt, MeterHeightPt), new Vector2(MarginPt, -MeterTopPt));
            _meterRoot = panel.transform.parent.gameObject;
            _meterLabel = HudFactory.CreateText(panel.transform, "Label", font, LabelFontSize, StylePalette.Ink, none, TextAnchor.MiddleLeft);
            HudFactory.Stretch(_meterLabel.rectTransform, 0f);
            _meterLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            _meterLabel.text = CompanionStrings.LiftLabel;

            Image track = HudFactory.CreateImage(panel.transform, "Track", StylePalette.Ink, false);
            RectTransform trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.offsetMin = new Vector2(BarLeftPt, -8f);
            trackRect.offsetMax = new Vector2(-8f, 8f);
            Image inner = HudFactory.CreateImage(track.transform, "Inner", StylePalette.CreamPath, false);
            HudFactory.Stretch(inner.rectTransform, 2f);
            _meterFillImage = HudFactory.CreateImage(inner.transform, "Fill", StylePalette.LiftViolet, false);
            _meterFill = _meterFillImage.rectTransform;
            _meterFill.anchorMin = Vector2.zero;
            _meterFill.anchorMax = new Vector2(0f, 1f);
            _meterFill.offsetMin = Vector2.zero;
            _meterFill.offsetMax = Vector2.zero;

            _meterHint = HudFactory.CreateText(safe, "LiftHint", font, LabelFontSize, StylePalette.Parchment, StylePalette.Ink, TextAnchor.MiddleLeft);
            HudFactory.Place(_meterHint.rectTransform, new Vector2(0f, 1f), new Vector2(MeterWidthPt + 40f, 22f), new Vector2(MarginPt + 4f, -(MeterTopPt + MeterHeightPt + 6f)));
            _meterHint.text = CompanionStrings.LiftReadyHint;
            _meterHint.gameObject.SetActive(false);

            // Call-out bubble (positioned over the macaw every frame).
            _bubbleFill = HudFactory.CreatePanel(
                transform, "CalloutBubble", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(BubbleWidthPt, BubbleHeightPt), Vector2.zero);
            _bubbleRoot = (RectTransform)_bubbleFill.transform.parent;
            _bubbleRoot.pivot = new Vector2(0.5f, 0f);
            _bubbleRoot.localRotation = Quaternion.Euler(0f, 0f, -6f);
            _bubbleText = HudFactory.CreateText(_bubbleFill.transform, "Word", font, BubbleFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_bubbleText.rectTransform, 0f);
            _bubbleRoot.gameObject.SetActive(false);

            _meterVisible = true;
            SetMeterVisible(false);
        }

        public void BeginRun(GameSession session)
        {
            _bubbleLeft = 0f;
            _shownFill = -1f;
            _shownFull = false;
            if (_bubbleRoot != null)
            {
                _bubbleRoot.gameObject.SetActive(false);
                _meterHint.gameObject.SetActive(false);
                _meterLabel.text = CompanionStrings.LiftLabel;
                _meterFillImage.color = StylePalette.LiftViolet;
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (_bubbleRoot == null || e.Type != RunnerEventType.CompanionCallout)
            {
                return;
            }

            var id = (CompanionCalloutId)e.Value;
            string word = CompanionStrings.WordFor(id);
            if (word.Length == 0)
            {
                return;
            }

            _bubbleText.text = word;
            if (id == CompanionCalloutId.Danger)
            {
                _bubbleFill.color = StylePalette.PulpOrange;
                _bubbleText.color = StylePalette.Parchment;
            }
            else if (CompanionCalloutKeys.IsCheer(id))
            {
                _bubbleFill.color = StylePalette.SunGold;
                _bubbleText.color = StylePalette.Ink;
            }
            else
            {
                _bubbleFill.color = StylePalette.Parchment;
                _bubbleText.color = StylePalette.Ink;
            }

            _bubbleLeft = _config.CalloutBubbleSeconds;
            _bubbleAge = 0f;
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_canvas == null)
            {
                return;
            }

            CompanionSimulation companion = session.Companion;
            SessionPhase phase = session.Phase;
            bool showMeter = companion != null && phase != SessionPhase.Menu && phase != SessionPhase.GameOver
                && phase != SessionPhase.ContinueOffer;
            SetMeterVisible(showMeter);
            float dt = phase == SessionPhase.Paused ? 0f : realDeltaSeconds;

            if (showMeter)
            {
                float fill = Mathf.Clamp01(companion.MeterFill);
                if (fill != _shownFill)
                {
                    _shownFill = fill;
                    _meterFill.anchorMax = new Vector2(fill, 1f);
                }

                bool full = companion.MeterFull;
                if (full != _shownFull)
                {
                    _shownFull = full;
                    _meterLabel.text = full ? CompanionStrings.LiftReady : CompanionStrings.LiftLabel;
                    _meterHint.gameObject.SetActive(full);
                    if (!full)
                    {
                        _meterFillImage.color = StylePalette.LiftViolet;
                    }
                }

                if (full)
                {
                    _pulseClock += dt * PulseHz;
                    if (_pulseClock > 1000f)
                    {
                        _pulseClock -= 1000f;
                    }

                    float k = 0.5f + 0.5f * Mathf.Sin(_pulseClock * 2f * Mathf.PI);
                    _meterFillImage.color = Color.Lerp(StylePalette.LiftViolet, StylePalette.SunGold, k * 0.6f);
                }
            }

            UpdateBubble(dt);
        }

        private void UpdateBubble(float dt)
        {
            if (_bubbleLeft <= 0f)
            {
                if (_bubbleRoot.gameObject.activeSelf)
                {
                    _bubbleRoot.gameObject.SetActive(false);
                }

                return;
            }

            _bubbleLeft -= dt;
            _bubbleAge += dt;
            bool placed = false;
            if (_camera != null && _bird != null)
            {
                Vector3 screen = _camera.WorldToScreenPoint(_bird.BubbleAnchor);
                if (screen.z > 0f
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, new Vector2(screen.x, screen.y), null, out Vector2 local))
                {
                    _bubbleRoot.anchoredPosition = local + new Vector2(0f, BubbleLiftPt);
                    placed = true;
                }
            }

            if (!placed)
            {
                // No camera: a fixed spot in the upper third.
                _bubbleRoot.anchoredPosition = new Vector2(0f, HudView.ReferenceResolutionPt.y * 0.22f);
            }

            float punch = _bubbleAge < PunchSeconds ? Mathf.Lerp(PunchScale, 1f, _bubbleAge / PunchSeconds) : 1f;
            _bubbleRoot.localScale = new Vector3(punch, punch, 1f);
            bool on = _bubbleLeft > 0f;
            if (_bubbleRoot.gameObject.activeSelf != on)
            {
                _bubbleRoot.gameObject.SetActive(on);
            }
        }

        private void SetMeterVisible(bool visible)
        {
            if (_meterVisible == visible)
            {
                return;
            }

            _meterVisible = visible;
            _meterRoot.SetActive(visible);
            if (!visible)
            {
                _meterHint.gameObject.SetActive(false);
                _shownFull = false;
                _shownFill = -1f;
                _meterLabel.text = CompanionStrings.LiftLabel;
                _meterFillImage.color = StylePalette.LiftViolet;
            }
        }
    }
}
