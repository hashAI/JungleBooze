using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Menus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// The run's whole screen UI, built from code (style guide section 8). In-run HUD: distance and score top-left,
    /// coins and the pause button top-right, all inside the safe area; the Ready prompt; stumble / near-miss
    /// call-outs; the 3-2-1 resume countdown. Menus (GDD section 19): the main menu in
    /// <see cref="SessionPhase.Menu"/> with Settings, the pause menu (Resume, Restart, Home) and the Game Over
    /// panel (score, distance, coins, best score, "New best!", Play again, Same track, Home).
    /// Portrait canvas scaler with a 390 × 844 pt reference, so layout units are points.
    /// Text comes from <see cref="HudStrings"/> and <see cref="MenuStrings"/>. Updates only what changed; no
    /// allocations per frame (menu texts are formatted once when a menu opens).
    /// </summary>
    public sealed class HudView : MonoBehaviour, IRunView
    {
        public static readonly Vector2 ReferenceResolutionPt = new Vector2(390f, 844f);

        private const float MarginPt = 12f;
        private const float TopBarHeightPt = 52f;
        private const int HudNumberFontSize = 28;
        private const int CountdownFontSize = 120;
        private const int PromptFontSize = 24;
        private const int SmallFontSize = 15;
        private const int MaxDistanceDigits = 6;
        private const int MaxCoinDigits = 5;
        private const int MaxScoreDigits = 8;
        private const float FlashSeconds = 0.25f;
        private const float FlashAlpha = 0.3f;
        private const float ToastSeconds = 0.8f;
        private const SessionPhase NoPhase = (SessionPhase)255;

        private static readonly string[] CountdownStrings = { "1", "2", "3" };

        private Canvas _canvas;
        private PlayerSave _save;
        private GameObject _topBar;
        private DigitCounter _distance;
        private DigitCounter _coins;
        private DigitCounter _score;
        private Button _pauseButton;
        private Image _dim;
        private Text _readyPrompt;
        private Text _countdownText;
        private Image _flash;
        private Text _toast;
        private MainMenuPanel _mainMenu;
        private PausePanel _pauseMenu;
        private GameOverPanel _gameOver;
        private SettingsPanel _settings;
        private float _flashLeft;
        private float _toastLeft;
        private SessionPhase _shownPhase = NoPhase;
        private int _shownCountdown = -1;
        private bool _shownLock = true;

        public Canvas Canvas => _canvas;

        public Button PauseButton => _pauseButton;

        public Button ResumeButton => _pauseMenu?.ResumeButton;

        /// <summary>Pause menu "Restart".</summary>
        public Button PauseRestartButton => _pauseMenu?.RestartButton;

        /// <summary>Pause menu "Home".</summary>
        public Button PauseHomeButton => _pauseMenu?.HomeButton;

        /// <summary>Game Over primary button "Play again" (new seed).</summary>
        public Button RunAgainButton => _gameOver?.PlayAgainButton;

        /// <summary>Game Over secondary button (same seed).</summary>
        public Button SameTrackButton => _gameOver?.SameTrackButton;

        /// <summary>Game Over "Home".</summary>
        public Button GameOverHomeButton => _gameOver?.HomeButton;

        public Button PlayButton => _mainMenu?.PlayButton;

        public Button SettingsButton => _mainMenu?.SettingsButton;

        public SettingsPanel Settings => _settings;

        public MainMenuPanel MainMenu => _mainMenu;

        public bool GameOverVisible => _gameOver != null && _gameOver.Visible;

        public bool PauseMenuVisible => _pauseMenu != null && _pauseMenu.Visible;

        public bool MainMenuVisible => _mainMenu != null && _mainMenu.Visible;

        /// <summary>True while Settings is open (the run driver ignores the keyboard Play then).</summary>
        public bool SettingsVisible => _settings != null && _settings.Visible;

        public bool NewBestVisible => _gameOver != null && _gameOver.Visible && _gameOver.NewBestVisible;

        public bool ReadyPromptVisible => _readyPrompt != null && _readyPrompt.gameObject.activeSelf;

        public bool PauseButtonVisible => _pauseButton != null && _pauseButton.gameObject.activeInHierarchy;

        /// <summary>Cause line shown on the Game Over panel (tests).</summary>
        public string CauseText => _gameOver?.CauseText;

        /// <summary>
        /// Builds the canvas and every element. Call once, right after AddComponent on a RectTransform object.
        /// <paramref name="save"/> feeds the main menu, Game Over best score and Settings; null shows zeros and
        /// disables storing settings.
        /// </summary>
        public void Build(IRunCommands commands, Font font, PlayerSave save)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            _save = save;

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

            // Stumble flash: a brief pulp-orange wash (never hazard red, style guide 2.1).
            _flash = HudFactory.CreateImage(transform, "Flash", new Color(StylePalette.PulpOrange.r, StylePalette.PulpOrange.g, StylePalette.PulpOrange.b, 0f), false);
            HudFactory.Stretch(_flash.rectTransform, 0f);

            RectTransform safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(safe, 0f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // In-run top bar (hidden behind the main menu).
            RectTransform topBar = HudFactory.CreateRect(safe, "TopBar");
            HudFactory.Stretch(topBar, 0f);
            _topBar = topBar.gameObject;

            // Distance, top-left.
            Image distancePanel = HudFactory.CreatePanel(
                topBar, "DistancePanel", StylePalette.Parchment, new Vector2(0f, 1f), new Vector2(150f, TopBarHeightPt), new Vector2(MarginPt, -MarginPt));
            _distance = MenuFactory.Counter(distancePanel.transform, "Distance", font, HudNumberFontSize, MaxDistanceDigits, HudStrings.DistanceUnit, false);
            RectTransform distanceRect = (RectTransform)_distance.transform;
            HudFactory.Stretch(distanceRect, 0f);
            distanceRect.offsetMin = new Vector2(10f, 0f);

            // Score, under the distance (shown for worlds that keep score).
            Image scorePanel = HudFactory.CreatePanel(
                topBar, "ScorePanel", StylePalette.Parchment, new Vector2(0f, 1f), new Vector2(150f, 36f), new Vector2(MarginPt, -(MarginPt + TopBarHeightPt + 6f)));
            Text scoreLabel = HudFactory.CreateText(scorePanel.transform, "Label", font, SmallFontSize, StylePalette.Ink, none, TextAnchor.MiddleLeft);
            HudFactory.Stretch(scoreLabel.rectTransform, 0f);
            scoreLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
            scoreLabel.text = HudStrings.Score;
            _score = MenuFactory.Counter(scorePanel.transform, "Score", font, 20, MaxScoreDigits, null, false);
            RectTransform scoreRect = (RectTransform)_score.transform;
            HudFactory.Stretch(scoreRect, 0f);
            scoreRect.offsetMin = new Vector2(62f, 0f);

            // Pause button, top-right corner.
            _pauseButton = HudFactory.CreateButton(
                topBar,
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
                topBar,
                "CoinPanel",
                StylePalette.Parchment,
                new Vector2(1f, 1f),
                new Vector2(130f, TopBarHeightPt),
                new Vector2(-(MarginPt + TopBarHeightPt + MarginPt), -MarginPt));
            MenuFactory.CoinIcon(coinPanel.transform, new Vector2(0f, 0.5f), new Vector2(9f, 0f));
            _coins = MenuFactory.Counter(coinPanel.transform, "Coins", font, HudNumberFontSize, MaxCoinDigits, null, false);
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

            // Stumble / near-miss call-out, below the upper-center area.
            _toast = HudFactory.CreateText(safe, "Toast", font, 30, StylePalette.PulpOrange, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_toast.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(300f, 50f), Vector2.zero);
            _toast.gameObject.SetActive(false);

            _mainMenu = new MainMenuPanel(safe, font, commands.Play, OpenSettings);
            _pauseMenu = new PausePanel(safe, font, commands);

            // Countdown 3-2-1.
            _countdownText = HudFactory.CreateText(safe, "Countdown", font, CountdownFontSize, StylePalette.Parchment, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 160f), new Vector2(0f, 60f));

            _gameOver = new GameOverPanel(safe, font, commands);
            _settings = new SettingsPanel(safe, font, save, OnSettingsClosed);

            ApplyPhase(SessionPhase.Running, null);
            _shownPhase = NoPhase;
        }

        /// <summary>Main menu "Settings": hides the menu and opens the Settings panel.</summary>
        public void OpenSettings()
        {
            if (_settings == null || _settings.Visible)
            {
                return;
            }

            _mainMenu.Hide();
            _dim.enabled = true;
            _settings.Open();
        }

        public void BeginRun(GameSession session)
        {
            _shownPhase = NoPhase;
            _shownCountdown = -1;
            _shownLock = true;
            _flashLeft = 0f;
            _toastLeft = 0f;
            if (_toast != null)
            {
                _toast.gameObject.SetActive(false);
                _flash.color = new Color(_flash.color.r, _flash.color.g, _flash.color.b, 0f);
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (_toast == null)
            {
                return;
            }

            if (e.Type == RunnerEventType.Stumbled)
            {
                _flashLeft = FlashSeconds;
                _toastLeft = ToastSeconds;
                _toast.text = HudStrings.Stumble;
            }
            else if (e.Type == RunnerEventType.NearMiss)
            {
                _toastLeft = ToastSeconds;
                _toast.text = HudStrings.NearMiss;
            }
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
            if (session.World is IRunWorldSummary summary)
            {
                _score.SetValue(ClampToInt(summary.Score));
            }

            UpdateCallouts(session.Phase == SessionPhase.Paused ? 0f : realDeltaSeconds);

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
                _gameOver.SetLocked(locked);
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

        /// <summary>Whole meters for display (never negative).</summary>
        internal static int MetersOf(double distanceM)
        {
            if (!(distanceM > 0.0))
            {
                return 0;
            }

            return distanceM >= int.MaxValue ? int.MaxValue : (int)distanceM;
        }

        private static int ClampToInt(long value)
        {
            return value < 0L ? 0 : (value > int.MaxValue ? int.MaxValue : (int)value);
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

        private void UpdateCallouts(float dt)
        {
            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                float a = Mathf.Clamp01(_flashLeft / FlashSeconds) * FlashAlpha;
                Color c = _flash.color;
                c.a = a;
                _flash.color = c;
            }

            if (_toastLeft > 0f)
            {
                _toastLeft -= dt;
                bool on = _toastLeft > 0f;
                if (_toast.gameObject.activeSelf != on)
                {
                    _toast.gameObject.SetActive(on);
                }
            }
        }

        private void OnSettingsClosed()
        {
            _dim.enabled = _shownPhase == SessionPhase.Paused || _shownPhase == SessionPhase.GameOver;
            if (_shownPhase == SessionPhase.Menu)
            {
                _mainMenu.Show(_save);
            }
        }

        private void ApplyPhase(SessionPhase phase, GameSession session)
        {
            bool menu = phase == SessionPhase.Menu;
            bool paused = phase == SessionPhase.Paused;
            bool over = phase == SessionPhase.GameOver;

            // Settings belongs to the main menu; leaving the menu closes it (and saves).
            if (!menu && _settings.Visible)
            {
                _settings.Close();
            }

            if (_topBar.activeSelf == menu)
            {
                _topBar.SetActive(!menu);
            }

            if (menu && !_settings.Visible)
            {
                _mainMenu.Show(_save);
            }
            else if (!menu)
            {
                _mainMenu.Hide();
            }

            _dim.enabled = paused || over || _settings.Visible;
            _readyPrompt.gameObject.SetActive(phase == SessionPhase.Ready);
            _pauseMenu.SetVisible(paused);
            _countdownText.gameObject.SetActive(phase == SessionPhase.Countdown);

            if (over && session != null)
            {
                _gameOver.Show(session, _save);
                _shownLock = session.GameOverInputLocked;
            }
            else
            {
                _gameOver.Hide();
            }

            // Spec 002 12.1: no pause in Ready or Game Over; the pause button is hidden while dying.
            bool pauseVisible = phase == SessionPhase.Running || phase == SessionPhase.Countdown || paused;
            if (_pauseButton.gameObject.activeSelf != pauseVisible)
            {
                _pauseButton.gameObject.SetActive(pauseVisible);
            }

            _pauseButton.interactable = phase == SessionPhase.Running || phase == SessionPhase.Countdown;
        }
    }
}
