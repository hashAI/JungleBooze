using System;
using System.Globalization;
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
    /// top-right, all inside the safe area; the Ready prompt; a pause menu with Resume; the 3-2-1 resume countdown;
    /// and the Game Over panel (spec 002 section 12.1): cause, distance, coins, score (when the world keeps score),
    /// session best, seed (development builds), and the buttons Run again (primary) and Same track (secondary),
    /// which ignore input during the Game Over input lock.
    /// Portrait canvas scaler with a 390 × 844 pt reference, so layout units are points.
    /// Text comes from <see cref="HudStrings"/>. Updates only what changed; no allocations per frame (the Game Over
    /// texts are formatted once when the panel opens).
    /// </summary>
    public sealed class HudView : MonoBehaviour, IRunView
    {
        public static readonly Vector2 ReferenceResolutionPt = new Vector2(390f, 844f);

        private const float MarginPt = 12f;
        private const float TopBarHeightPt = 52f;
        private const int HudNumberFontSize = 28;
        private const int TitleFontSize = 34;
        private const int ButtonFontSize = 26;
        private const int SecondaryButtonFontSize = 22;
        private const int CountdownFontSize = 120;
        private const int PromptFontSize = 24;
        private const int RowFontSize = 20;
        private const int SmallFontSize = 15;
        private const int MaxDistanceDigits = 6;
        private const int MaxCoinDigits = 5;
        private const float GameOverWidthPt = 320f;
        private const float GameOverHeightPt = 520f;
        private const float RowWidthPt = 260f;
        private const float RowHeightPt = 26f;
        private const float RowStepPt = 30f;
        private const float FirstRowYPt = -176f;
        private const SessionPhase NoPhase = (SessionPhase)255;

        private static readonly string[] CountdownStrings = { "1", "2", "3" };

        private Canvas _canvas;
        private DigitCounter _distance;
        private DigitCounter _coins;
        private Button _pauseButton;
        private Image _dim;
        private Text _readyPrompt;
        private GameObject _pausePanel;
        private Text _countdownText;
        private GameObject _gameOverPanel;
        private Text _causeText;
        private DigitCounter _gameOverDistance;
        private Text _coinsValue;
        private RectTransform _scoreRow;
        private Text _scoreValue;
        private RectTransform _bestRow;
        private Text _bestValue;
        private Text _newBestStamp;
        private RectTransform _seedRow;
        private Text _seedText;
        private Button _runAgainButton;
        private Button _sameTrackButton;
        private Button _resumeButton;
        private SessionPhase _shownPhase = NoPhase;
        private int _shownCountdown = -1;
        private bool _shownLock = true;

        public Canvas Canvas => _canvas;

        public Button PauseButton => _pauseButton;

        public Button ResumeButton => _resumeButton;

        /// <summary>Game Over primary button (new seed).</summary>
        public Button RunAgainButton => _runAgainButton;

        /// <summary>Game Over secondary button (same seed).</summary>
        public Button SameTrackButton => _sameTrackButton;

        public bool GameOverVisible => _gameOverPanel != null && _gameOverPanel.activeSelf;

        public bool PauseMenuVisible => _pausePanel != null && _pausePanel.activeSelf;

        public bool ReadyPromptVisible => _readyPrompt != null && _readyPrompt.gameObject.activeSelf;

        public bool PauseButtonVisible => _pauseButton != null && _pauseButton.gameObject.activeInHierarchy;

        /// <summary>Cause line shown on the Game Over panel (tests).</summary>
        public string CauseText => _causeText == null ? null : _causeText.text;

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
            EnsureEventSystem(transform.parent != null ? transform.parent : transform);

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
                HudStrings.PauseButtonName + "Button",
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
            CreateCoinIcon(coinPanel.transform, new Vector2(9f, 0f));
            _coins = CreateCounter(coinPanel.transform, "Coins", font, HudNumberFontSize, MaxCoinDigits, null, false);
            RectTransform coinsRect = (RectTransform)_coins.transform;
            HudFactory.Stretch(coinsRect, 0f);
            coinsRect.offsetMin = new Vector2(42f, 0f);

            // Ready prompt, lower middle (clear of the upper-center area, style guide 8.1).
            _readyPrompt = HudFactory.CreateText(safe, "ReadyPrompt", font, PromptFontSize, StylePalette.Parchment, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_readyPrompt.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(340f, 60f), Vector2.zero);
#if UNITY_IOS || UNITY_ANDROID
            _readyPrompt.text = HudStrings.ReadyPromptTouch;
#else
            _readyPrompt.text = HudStrings.ReadyPromptKeyboard;
#endif

            BuildPauseMenu(safe, font, commands, none);

            // Countdown 3-2-1.
            _countdownText = HudFactory.CreateText(safe, "Countdown", font, CountdownFontSize, StylePalette.Parchment, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 160f), new Vector2(0f, 60f));

            BuildGameOverPanel(safe, font, commands, none);

            ApplyPhase(SessionPhase.Running, null);
            _shownPhase = NoPhase;
        }

        public void BeginRun(GameSession session)
        {
            _shownPhase = NoPhase;
            _shownCountdown = -1;
            _shownLock = true;
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

            // Buttons keep no selection, so keyboard Submit/Space never re-clicks the last pressed button.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }

            _distance.SetValue(MetersOf(session.DistanceM));
            _coins.SetValue(session.Coins);

            SessionPhase phase = session.Phase;
            if (phase != _shownPhase)
            {
                ApplyPhase(phase, session);
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

            // Game Over buttons stay disabled during the input lock (spec 002 12.1); the driver also checks it.
            bool locked = session.GameOverInputLocked;
            if (locked != _shownLock)
            {
                _shownLock = locked;
                _runAgainButton.interactable = !locked;
                _sameTrackButton.interactable = !locked;
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

        /// <summary>
        /// Game Over cause line: the world's named cause when it provides one (spec 002 12.4), otherwise a generic
        /// line from <see cref="HudStrings"/>. A run ended without a death (development key K) says "Run ended".
        /// </summary>
        public static string CauseTextFor(GameSession session)
        {
            RunnerSimulation runner = session.Runner;
            DeathCause cause = runner.Current.IsDead ? runner.DeathCause : DeathCause.None;
            if (cause != DeathCause.None && session.World is IRunWorldSummary summary)
            {
                string named = summary.DescribeDeath(cause, runner.DeathArchetype, runner.DeathAfterStumble);
                if (!string.IsNullOrEmpty(named))
                {
                    return named;
                }
            }

            switch (cause)
            {
                case DeathCause.Hit:
                    return runner.DeathAfterStumble ? HudStrings.CauseTrippedTwice : HudStrings.CauseHit;
                case DeathCause.Fell:
                    return HudStrings.CauseFell;
                default:
                    return HudStrings.CauseEnded;
            }
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

        private static void CreateCoinIcon(Transform parent, Vector2 offset)
        {
            Image coinRim = HudFactory.CreateImage(parent, "CoinIcon", StylePalette.CoinRim, false);
            HudFactory.Place(coinRim.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 26f), offset);
            Image coinFace = HudFactory.CreateImage(coinRim.transform, "Face", StylePalette.CoinGold, false);
            HudFactory.Stretch(coinFace.rectTransform, 3f);
            Image coinGem = HudFactory.CreateImage(coinFace.transform, "Gem", StylePalette.CoinGem, false);
            HudFactory.Place(coinGem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(9f, 9f), Vector2.zero);
            coinGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>A label (left) and value (right) row on the Game Over panel. Returns the value text.</summary>
        private static Text CreateRow(Transform parent, string name, Font font, string label, float y, out RectTransform row)
        {
            row = HudFactory.CreateRect(parent, name);
            HudFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, RowHeightPt), new Vector2(0f, y));
            Color none = new Color(0f, 0f, 0f, 0f);

            Text labelText = HudFactory.CreateText(row, "Label", font, RowFontSize, StylePalette.Ink, none, TextAnchor.MiddleLeft);
            HudFactory.Stretch(labelText.rectTransform, 0f);
            labelText.text = label;

            Text value = HudFactory.CreateText(row, "Value", font, RowFontSize, StylePalette.Ink, none, TextAnchor.MiddleRight);
            HudFactory.Stretch(value.rectTransform, 0f);
            return value;
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

        private void BuildPauseMenu(RectTransform safe, Font font, IRunCommands commands, Color none)
        {
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
        }

        private void BuildGameOverPanel(RectTransform safe, Font font, IRunCommands commands, Color none)
        {
            Image overFill = HudFactory.CreatePanel(
                safe, "GameOverPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(GameOverWidthPt, GameOverHeightPt), Vector2.zero);
            _gameOverPanel = overFill.transform.parent.gameObject;
            Transform panel = overFill.transform;

            Text overTitle = HudFactory.CreateText(panel, "Title", font, TitleFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(overTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, 50f), new Vector2(0f, -12f));
            overTitle.text = HudStrings.GameOver;

            _causeText = HudFactory.CreateText(panel, "Cause", font, 18, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(_causeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, 26f), new Vector2(0f, -62f));

            Text distanceLabel = HudFactory.CreateText(panel, "DistanceLabel", font, 17, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(distanceLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, 22f), new Vector2(0f, -92f));
            distanceLabel.text = HudStrings.Distance;
            _gameOverDistance = CreateCounter(panel, "FinalDistance", font, 44, MaxDistanceDigits, HudStrings.DistanceUnit, true);
            HudFactory.Place((RectTransform)_gameOverDistance.transform, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, 56f), new Vector2(0f, -114f));

            _coinsValue = CreateRow(panel, "CoinsRow", font, HudStrings.Coins, FirstRowYPt, out _);
            _scoreValue = CreateRow(panel, "ScoreRow", font, HudStrings.Score, FirstRowYPt - RowStepPt, out _scoreRow);
            _bestValue = CreateRow(panel, "BestRow", font, HudStrings.Best, FirstRowYPt - 2f * RowStepPt, out _bestRow);

            // "New best!" stamp on the panel's top-right corner (style guide 8.1: pulp orange, ink outline, rotated).
            _newBestStamp = HudFactory.CreateText(panel, "NewBestStamp", font, 22, StylePalette.PulpOrange, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_newBestStamp.rectTransform, new Vector2(1f, 1f), new Vector2(130f, 30f), new Vector2(16f, 22f));
            _newBestStamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            _newBestStamp.text = HudStrings.NewBest;

            _seedText = HudFactory.CreateText(panel, "Seed", font, SmallFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            _seedRow = _seedText.rectTransform;
            HudFactory.Place(_seedRow, new Vector2(0.5f, 1f), new Vector2(RowWidthPt, 22f), new Vector2(0f, FirstRowYPt - 3f * RowStepPt));

            // Primary: Run again (88 pt tall, style guide 8.2). Secondary: Same track (teal).
            _runAgainButton = HudFactory.CreateButton(
                panel,
                "RunAgainButton",
                StylePalette.PulpOrange,
                new Vector2(0.5f, 0f),
                new Vector2(RowWidthPt, 88f),
                new Vector2(0f, 112f),
                font,
                HudStrings.RunAgain,
                ButtonFontSize,
                StylePalette.Parchment,
                StylePalette.Ink,
                commands.Restart);
            _sameTrackButton = HudFactory.CreateButton(
                panel,
                "SameTrackButton",
                StylePalette.PistaTealSash,
                new Vector2(0.5f, 0f),
                new Vector2(RowWidthPt, 56f),
                new Vector2(0f, 44f),
                font,
                HudStrings.SameTrack,
                SecondaryButtonFontSize,
                StylePalette.Parchment,
                StylePalette.Ink,
                commands.RestartSameTrack);
#if UNITY_EDITOR || UNITY_STANDALONE
            Text hint = HudFactory.CreateText(panel, "KeyHint", font, SmallFontSize, StylePalette.Ink, none, TextAnchor.MiddleCenter);
            HudFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(RowWidthPt, 24f), new Vector2(0f, 12f));
            hint.text = HudStrings.GameOverKeyHint;
#endif
        }

        private void ApplyPhase(SessionPhase phase, GameSession session)
        {
            bool paused = phase == SessionPhase.Paused;
            bool over = phase == SessionPhase.GameOver;

            _dim.enabled = paused || over;
            _readyPrompt.gameObject.SetActive(phase == SessionPhase.Ready);
            _pausePanel.SetActive(paused);
            _countdownText.gameObject.SetActive(phase == SessionPhase.Countdown);
            _gameOverPanel.SetActive(over);

            // Spec 002 12.1: no pause in Ready or Game Over; the pause button is hidden while dying.
            bool pauseVisible = phase == SessionPhase.Running || phase == SessionPhase.Countdown || paused;
            if (_pauseButton.gameObject.activeSelf != pauseVisible)
            {
                _pauseButton.gameObject.SetActive(pauseVisible);
            }

            _pauseButton.interactable = phase == SessionPhase.Running || phase == SessionPhase.Countdown;

            if (over && session != null)
            {
                FillGameOver(session);
            }
        }

        /// <summary>Formats the Game Over texts once when the panel opens (allocates a few strings; not per frame).</summary>
        private void FillGameOver(GameSession session)
        {
            _causeText.text = CauseTextFor(session);
            _gameOverDistance.SetValue(MetersOf(session.DistanceM));
            _coinsValue.text = session.Coins.ToString(CultureInfo.InvariantCulture);

            float bestY = FirstRowYPt - RowStepPt;
            if (session.World is IRunWorldSummary summary)
            {
                _scoreRow.gameObject.SetActive(true);
                _scoreValue.text = summary.Score.ToString(CultureInfo.InvariantCulture);
                bestY -= RowStepPt;
            }
            else
            {
                _scoreRow.gameObject.SetActive(false);
            }

            _bestRow.anchoredPosition = new Vector2(0f, bestY);
            _bestValue.text = MetersOf(session.BestDistanceM).ToString(CultureInfo.InvariantCulture) + " " + HudStrings.DistanceUnit;
            _newBestStamp.gameObject.SetActive(session.LastRunWasBest && session.RunNumber > 1);

            // Seed: development builds only (spec 002 12.1).
            bool showSeed = Debug.isDebugBuild;
            _seedRow.gameObject.SetActive(showSeed);
            if (showSeed)
            {
                _seedRow.anchoredPosition = new Vector2(0f, bestY - RowStepPt);
                _seedText.text = HudStrings.Seed + " " + session.RunSeed.ToString(CultureInfo.InvariantCulture);
            }

            _runAgainButton.interactable = !session.GameOverInputLocked;
            _sameTrackButton.interactable = !session.GameOverInputLocked;
            _shownLock = session.GameOverInputLocked;
        }
    }
}
