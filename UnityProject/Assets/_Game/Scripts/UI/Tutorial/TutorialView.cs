using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Tutorial;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Tutorial
{
    /// <summary>
    /// The tutorial's on-screen help (GDD 12): a hint banner, a ghost hand that demonstrates the swipe (text and
    /// motion, never color alone) and a Skip button (only when the tutorial was completed before). Own overlay
    /// canvas on the HUD's 390 x 844 pt reference, inside the safe area. With Reduce Motion the hand stays still.
    /// Everything is hidden while the tutorial is off, in menus and when paused. No allocations per frame.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour, IRunView
    {
        private const float HintWidthPt = 330f;
        private const float HintHeightPt = 72f;
        private const float HintYPt = 150f;
        private const int HintFontSize = 24;
        private const float HandSizePt = 64f;
        private const float HandBaseYPt = -110f;
        private const float HandTravelPt = 80f;
        private const float HandPeriodSeconds = 1.1f;
        private const int HandFontSize = 28;
        private const float SkipWidthPt = 96f;
        private const float SkipHeightPt = 44f;
        private const float SkipRightPt = 12f;
        private const float SkipTopPt = 76f;
        private const int SkipFontSize = 20;

        private TutorialDirector _director;
        private bool _reduceMotion;
        private GameObject _hintRoot;
        private Text _hintText;
        private GameObject _handRoot;
        private RectTransform _handRect;
        private CanvasGroup _handGroup;
        private Text _handText;
        private GameObject _skipRoot;
        private TutorialHint _shownHint;
        private TutorialGesture _shownGesture;
        private float _clock;

        public bool HintVisible => _hintRoot != null && _hintRoot.activeSelf;

        public string HintText => _hintText != null ? _hintText.text : string.Empty;

        public bool SkipVisible => _skipRoot != null && _skipRoot.activeSelf;

        /// <summary>Builds the canvas. Call once, right after AddComponent on a RectTransform object.</summary>
        public void Build(TutorialDirector director, Font font, bool reduceMotion)
        {
            _director = director;
            _reduceMotion = reduceMotion;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudView.ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            gameObject.AddComponent<GraphicRaycaster>();

            Color none = new Color(0f, 0f, 0f, 0f);
            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Image banner = HudFactory.CreatePanel(
                safe, "Hint", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(HintWidthPt, HintHeightPt), new Vector2(0f, HintYPt));
            _hintRoot = banner.transform.parent.gameObject;
            _hintText = HudFactory.CreateText(banner.transform, "Text", font, HintFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_hintText.rectTransform, 8f);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hintRoot.SetActive(false);

            Image hand = HudFactory.CreatePanel(
                safe, "GhostHand", StylePalette.SunGold, new Vector2(0.5f, 0.5f), new Vector2(HandSizePt, HandSizePt), new Vector2(0f, HandBaseYPt));
            _handRoot = hand.transform.parent.gameObject;
            _handRect = (RectTransform)_handRoot.transform;
            _handGroup = _handRoot.AddComponent<CanvasGroup>();
            _handGroup.blocksRaycasts = false;
            _handGroup.interactable = false;
            _handText = HudFactory.CreateText(hand.transform, "Glyph", font, HandFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_handText.rectTransform, 0f);
            _handRoot.SetActive(false);

            Button skip = HudFactory.CreateButton(
                safe,
                "SkipButton",
                StylePalette.Parchment,
                new Vector2(1f, 1f),
                new Vector2(SkipWidthPt, SkipHeightPt),
                new Vector2(-SkipRightPt, -SkipTopPt),
                font,
                TutorialStrings.Skip,
                SkipFontSize,
                StylePalette.Ink,
                none,
                OnSkipClicked);
            _skipRoot = skip.transform.parent.gameObject;
            _skipRoot.SetActive(false);
        }

        /// <summary>Applies the Reduce Motion setting (the hand stops moving).</summary>
        public void SetReduceMotion(bool reduceMotion)
        {
            _reduceMotion = reduceMotion;
        }

        public void BeginRun(GameSession session)
        {
            _shownHint = TutorialHint.None;
            _shownGesture = TutorialGesture.None;
            _clock = 0f;
            SetVisible(false, false);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_hintRoot == null || _director == null)
            {
                return;
            }

            SessionPhase phase = session.Phase;
            bool playing = phase == SessionPhase.Running || phase == SessionPhase.Countdown || phase == SessionPhase.Dying;
            bool on = _director.Active && playing;
            TutorialHint hint = on ? _director.Hint : TutorialHint.None;
            SetVisible(hint != TutorialHint.None, on && _director.Skippable);
            if (hint == TutorialHint.None)
            {
                return;
            }

            if (hint != _shownHint)
            {
                _shownHint = hint;
                _hintText.text = TutorialStrings.For(hint);
            }

            TutorialGesture gesture = _director.Gesture;
            _handRoot.SetActive(gesture != TutorialGesture.None);
            if (gesture != _shownGesture)
            {
                _shownGesture = gesture;
                _handText.text = TutorialStrings.GlyphFor(gesture);
                _clock = 0f;
            }

            if (gesture != TutorialGesture.None)
            {
                AnimateHand(gesture, realDeltaSeconds);
            }
        }

        private void SetVisible(bool hint, bool skip)
        {
            if (_hintRoot.activeSelf != hint)
            {
                _hintRoot.SetActive(hint);
            }

            if (!hint && _handRoot.activeSelf)
            {
                _handRoot.SetActive(false);
            }

            if (_skipRoot.activeSelf != skip)
            {
                _skipRoot.SetActive(skip);
            }
        }

        private void AnimateHand(TutorialGesture gesture, float realDeltaSeconds)
        {
            if (_reduceMotion)
            {
                _handRect.anchoredPosition = new Vector2(0f, HandBaseYPt);
                _handRect.localScale = Vector3.one;
                _handGroup.alpha = 1f;
                return;
            }

            _clock += realDeltaSeconds;
            if (_clock > 1000f)
            {
                _clock -= 1000f;
            }

            float t = Mathf.Repeat(_clock, HandPeriodSeconds) / HandPeriodSeconds;
            float x = 0f;
            float y = HandBaseYPt;
            float scale = 1f;
            float fade = 1f;
            switch (gesture)
            {
                case TutorialGesture.SwipeSides:
                    x = Mathf.Sin(t * 2f * Mathf.PI) * HandTravelPt;
                    break;
                case TutorialGesture.SwipeUp:
                    y += Mathf.Lerp(-HandTravelPt * 0.5f, HandTravelPt, t);
                    fade = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f);
                    break;
                case TutorialGesture.SwipeDown:
                    y += Mathf.Lerp(HandTravelPt, -HandTravelPt * 0.5f, t);
                    fade = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f);
                    break;
                case TutorialGesture.DoubleTap:
                    scale = 1f + 0.25f * Mathf.Max(0f, Mathf.Sin(t * 4f * Mathf.PI));
                    break;
            }

            _handRect.anchoredPosition = new Vector2(x, y);
            _handRect.localScale = new Vector3(scale, scale, 1f);
            _handGroup.alpha = fade;
        }

        private void OnSkipClicked()
        {
            _director?.Skip();
        }
    }
}
