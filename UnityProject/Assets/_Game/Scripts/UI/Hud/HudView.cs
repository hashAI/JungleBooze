using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// First Playable HUD, built from code (style guide section 8): distance top-left, coins and the pause button
    /// top-right, all inside the safe area; a pause menu with Resume, the 3-2-1 resume countdown, and a Game Over
    /// panel with Restart. Portrait canvas scaler with a 390 × 844 pt reference, so layout units are points.
    /// Text comes from <see cref="HudStrings"/>. Updates only what changed; no allocations per frame.
    /// </summary>
    public sealed class HudView : MonoBehaviour, IRunView
    {
        public static readonly Vector2 ReferenceResolutionPt = new Vector2(390f, 844f);

        private const float MarginPt = 12f;
        private const float TopBarHeightPt = 52f;
        private const int HudNumberFontSize = 28;
        private const int TitleFontSize = 34;
        private const int ButtonFontSize = 26;
        private const int CountdownFontSize = 120;
        private const int MaxDistanceDigits = 6;
        private const int MaxCoinDigits = 5;
        private const SessionPhase NoPhase = (SessionPhase)255;

        private static readonly string[] CountdownStrings = { "1", "2", "3" };

        private Canvas _canvas;
        private DigitCounter _distance;
        private DigitCounter _coins;
        private Button _pauseButton;
        private Image _dim;
        private GameObject _pausePanel;
        private Text _countdownText;
        private GameObject _gameOverPanel;
        private DigitCounter _gameOverDistance;
        private Button _restartButton;
        private Button _resumeButton;
        private SessionPhase _shownPhase = NoPhase;
        private int _shownCountdown = -1;

        public Canvas Canvas => _canvas;

        public Button PauseButton => _pauseButton;

        public Button ResumeButton => _resumeButton;

        public Button RestartButton => _restartButton;

        public bool GameOverVisible => _gameOverPanel != null && _gameOverPanel.activeSelf;

        public bool PauseMenuVisible => _pausePanel != null && _pausePanel.activeSelf;

        /// <summary>Builds the canvas and every element. Call once, right after AddComponent on a RectTransform object.</summary>
        public void Build(IRunCommands commands, Font font)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _canvas.pixelPerfect = false;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolutionPt;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem(transform);

            Color none = new Color(0f, 0f, 0f, 0f);

            // Full-screen dim behind menus (outside the safe area on purpose).
            _dim = HudFactory.CreateImage(transform, "Dim", StylePalette.Dim, true);
            HudFactory.Stretch(_dim.rectTransform, 0f);

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Distance, top-left.
            Image distancePanel = HudFactory.CreatePanel(
                safe, "DistancePanel", StylePalette.Parchment, new Vector2(0f, 1f), new Vector2(150f, TopBarHeightPt), new Vector2(MarginPt, -MarginPt));
            _distance = CreateCounter(distancePanel.transform, "Distance", font, HudNumberFontSize, MaxDistanceDigits, HudStrings.DistanceUnit, false);
            RectTransform distanceRect = (RectTransform)_distance.transform;
            HudFactory.Stretch(distanceRect, 0f);
            distanceRect.offsetMin = new Vector2(10f, 0f);

            // Pause button, top-right corner.
            _pauseButton = HudFactory.CreateButton(
                safe,
                "PauseButton",
                StylePalette.Parchment,
                new Vector2(1f, 1f),
                new Vector2(TopBarHeightPt, TopBarHeightPt),
                new Vector2(-MarginPt, -MarginPt),
                font,
                null,
                0,
                StylePalette.Ink,
                none,
                commands.Pause);
            for (int side = -1; side <= 1; side += 2)
            {
                Image bar = HudFactory.CreateImage(_pauseButton.transform, side < 0 ? "BarLeft" : "BarRight", StylePalette.Ink, false);
                HudFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(7f, 20f), new Vector2(side * 6f, 0f));
            }

            // Coins, left of the pause button: gold coin with the turquoise gem, then the count.
            Image coinPanel = HudFactory.CreatePanel(
                safe,
                "CoinPanel",
                StylePalette.Parchment,
                new Vector2(1f, 1f),
                new Vector2(130f, TopBarHeightPt),
                new Vector2(-(MarginPt + TopBarHeightPt + MarginPt), -MarginPt));
            Image coinRim = HudFactory.CreateImage(coinPanel.transform, "CoinIcon", StylePalette.CoinRim, false);
            HudFactory.Place(coinRim.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 26f), new Vector2(9f, 0f));
            Image coinFace = HudFactory.CreateImage(coinRim.transform, "Face", StylePalette.CoinGold, false);
            HudFactory.Stretch(coinFace.rectTransform, 3f);
            Image coinGem = HudFactory.CreateImage(coinFace.transform, "Gem", StylePalette.CoinGem, false);
            HudFactory.Place(coinGem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(9f, 9f), Vector2.zero);
            coinGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _coins = CreateCounter(coinPanel.transform, "Coins", font, HudNumberFontSize, MaxCoinDigits, null, false);
            RectTransform coinsRect = (RectTransform)_coins.transform;
            HudFactory.Stretch(coinsRect, 0f);
            coinsRect.offsetMin = new Vector2(42f, 0f);

            // Pause menu.
            Image pauseFill = HudFactory.CreatePanel(
                safe, "PauseMenu", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(280f, 220f), Vector2.zero);
            _pausePanel = pauseFill.transform.parent.gameObject;
            Text pauseTitle = HudFactory.CreateText(pauseFill.transform, "Title", font, TitleFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(pauseTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(240f, 60f), new Vector2(0f, -16f));
            pauseTitle.text = HudStrings.Paused;
            _resumeButton = HudFactory.CreateButton(
                pauseFill.transform,
                "ResumeButton",
                StylePalette.PulpOrange,
                new Vector2(0.5f, 0f),
                new Vector2(220f, 88f),
                new Vector2(0f, 24f),
                font,
                HudStrings.Resume,
                ButtonFontSize,
                StylePalette.Parchment,
                StylePalette.Ink,
                commands.Resume);

            // Countdown 3-2-1.
            _countdownText = HudFactory.CreateText(safe, "Countdown", font, CountdownFontSize, StylePalette.Parchment, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 160f), new Vector2(0f, 60f));

            // Game Over.
            Image overFill = HudFactory.CreatePanel(
                safe, "GameOverPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(300f, 320f), Vector2.zero);
            _gameOverPanel = overFill.transform.parent.gameObject;
            Text overTitle = HudFactory.CreateText(overFill.transform, "Title", font, TitleFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(overTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(260f, 60f), new Vector2(0f, -14f));
            overTitle.text = HudStrings.GameOver;
            Text distanceLabel = HudFactory.CreateText(overFill.transform, "DistanceLabel", font, 20, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(distanceLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(260f, 30f), new Vector2(0f, -80f));
            distanceLabel.text = HudStrings.Distance;
            _gameOverDistance = CreateCounter(overFill.transform, "FinalDistance", font, 44, MaxDistanceDigits, HudStrings.DistanceUnit, true);
            HudFactory.Place((RectTransform)_gameOverDistance.transform, new Vector2(0.5f, 1f), new Vector2(260f, 60f), new Vector2(0f, -112f));
            _restartButton = HudFactory.CreateButton(
                overFill.transform,
                "RestartButton",
                StylePalette.PulpOrange,
                new Vector2(0.5f, 0f),
                new Vector2(240f, 88f),
                new Vector2(0f, 40f),
                font,
                HudStrings.Restart,
                ButtonFontSize,
                StylePalette.Parchment,
                StylePalette.Ink,
                commands.Restart);
#if UNITY_EDITOR || UNITY_STANDALONE
            Text hint = HudFactory.CreateText(overFill.transform, "KeyHint", font, 15, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(260f, 24f), new Vector2(0f, 10f));
            hint.text = HudStrings.RestartKeyHint;
#endif

            ApplyPhase(SessionPhase.Running, 0);
            _shownPhase = NoPhase;
        }

        public void BeginRun(GameSession session)
        {
            _shownPhase = NoPhase;
            _shownCountdown = -1;
            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_canvas == null)
            {
                return;
            }

            int meters = MetersOf(session.DistanceM);
            _distance.SetValue(meters);
            _coins.SetValue(session.Coins);

            SessionPhase phase = session.Phase;
            if (phase != _shownPhase)
            {
                ApplyPhase(phase, meters);
                _shownPhase = phase;
                _shownCountdown = -1;
            }

            if (phase == SessionPhase.Countdown)
            {
                int number = CountdownNumber(session.CountdownSecondsLeft, session.Timings.ResumeCountdownSeconds);
                if (number != _shownCountdown)
                {
                    _shownCountdown = number;
                    _countdownText.text = CountdownStrings[number - 1];
                }
            }
        }

        /// <summary>3, 2, 1 over the countdown (spec 001 8.3: 0.5 s per number at 1.5 s).</summary>
        public static int CountdownNumber(double secondsLeft, double totalSeconds)
        {
            if (!(totalSeconds > 0.0) || !(secondsLeft > 0.0))
            {
                return 1;
            }

            int n = (int)Math.Ceiling(secondsLeft / (totalSeconds / 3.0) - 1e-9);
            return n < 1 ? 1 : (n > 3 ? 3 : n);
        }

        private static int MetersOf(double distanceM)
        {
            if (!(distanceM > 0.0))
            {
                return 0;
            }

            return distanceM >= int.MaxValue ? int.MaxValue : (int)distanceM;
        }

        private static DigitCounter CreateCounter(Transform parent, string name, Font font, int fontSize, int digits, string suffix, bool centered)
        {
            RectTransform rt = HudFactory.CreateRect(parent, name);
            DigitCounter counter = rt.gameObject.AddComponent<DigitCounter>();
            counter.Build(font, fontSize, digits, StylePalette.Ink, new Color(0f, 0f, 0f, 0f), suffix, centered);
            return counter;
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
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

        private void ApplyPhase(SessionPhase phase, int meters)
        {
            bool paused = phase == SessionPhase.Paused;
            bool over = phase == SessionPhase.GameOver;

            _dim.enabled = paused || over;
            _pausePanel.SetActive(paused);
            _countdownText.gameObject.SetActive(phase == SessionPhase.Countdown);
            _gameOverPanel.SetActive(over);
            _pauseButton.interactable = phase == SessionPhase.Running || phase == SessionPhase.Countdown;

            if (over)
            {
                _gameOverDistance.SetValue(meters);
            }
        }
    }
}
