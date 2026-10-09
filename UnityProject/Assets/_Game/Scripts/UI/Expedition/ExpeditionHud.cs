using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Feedback;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// The Expedition scene's whole UI (uGUI, built from code with the painterly <see cref="UiTheme"/>): in-run HUD,
    /// toast, gesture hints, revive, pause, settings, home, journal, abilities, credits, results and the ability card.
    /// One <see cref="NavigationStack"/> decides what shows; Back always returns one level. The canvas keeps the short
    /// screen side at 390 units in both orientations; layouts react to rotation, safe-area and text-size changes.
    /// HUD and menus live on separate sub-canvases so per-frame HUD changes never rebuild menu meshes. Per-frame
    /// setters only touch the UI on changes and never allocate.
    /// Why uGUI over UI Toolkit: code-built 9-slice painted art, world/camera-space capture for the video and
    /// screenshot tools, proven mobile batching, and the existing tests/tools already drive uGUI.
    /// </summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private readonly NavigationStack _nav = new NavigationStack(UiScreen.Hud);
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _rootRect;
        private UiTheme _theme;
        private StringTable _strings;
        private TextScaleRegistry _texts;
        private UiFactory _f;
        private IExpeditionUiHost _host;
        private UiPreferences _prefs;
        private FeelSettings _feel;
        private ExpeditionContent _content;
        private System.Func<SaveData> _profile;
        private Image _overlay;
        private Color _overlayColor;
        private HudView _hud;
        private HomeView _home;
        private PauseView _pause;
        private SettingsView _settings;
        private CreditsView _credits;
        private JournalView _journal;
        private AbilitiesView _abilities;
        private ResultsView _results;
        private UpgradeView _upgrade;
        private ReviveView _revive;
        private Text _debug;
        private Text _desktopHint;
        private Vector2 _laidOutSize;
        private Rect _laidOutSafe;
        private float _laidOutScale = -1f;
        private bool _laidOutHand;
        private ScreenFrame _frame;

        public Canvas Canvas => _canvas;

        public StringTable Strings => _strings;

        public UiTheme Theme => _theme;

        public UiScreen CurrentScreen => _nav.Current;

        public NavigationStack Navigation => _nav;

        public ScreenFrame Frame => _frame;

        public bool ReducedMotion => _feel != null && _feel.ReducedMotion;

        public HudView HudView => _hud;

        public ResultsView ResultsView => _results;

        public HomeView HomeView => _home;

        public PauseView PauseView => _pause;

        public SettingsView SettingsView => _settings;

        public JournalView JournalView => _journal;

        public AbilitiesView AbilitiesView => _abilities;

        public CreditsView CreditsView => _credits;

        public UpgradeView UpgradeView => _upgrade;

        public ReviveView ReviveView => _revive;

        public bool ResultsVisible => _results != null && _nav.Current == UiScreen.Results;

        public bool UpgradeVisible => _upgrade != null && _nav.Current == UiScreen.Upgrade;

        public bool HomeVisible => _home != null && _nav.Root == UiScreen.Home;

        public bool PauseMenuVisible => _nav.Current == UiScreen.Pause;

        public bool ToastVisible => _hud != null && _hud.ToastVisible;

        public bool HelpVisible => _hud != null && _hud.HelpVisible;

        public string ResultsText => _results != null ? _results.Summary() : string.Empty;

        public string ObjectiveText => _results != null ? _results.ObjectiveText : string.Empty;

        public string ToastText => _hud != null ? _hud.ToastLine : string.Empty;

        public bool RecordVisible => _results != null && _results.RecordVisible;

        public bool LearnInteractable => _upgrade != null && _upgrade.LearnInteractable;

        public bool CountingUp => _results != null && _results.CountingUp;

        public bool ReviveVisible => _revive != null && _revive.Visible;

        public bool ReviveInteractable => _revive != null && _revive.Button.Interactable;

        public float OverlayAlpha => _overlay != null && _overlay.gameObject.activeSelf ? _overlay.color.a : 0f;

        public bool DebugVisible => _debug != null && _debug.gameObject.activeSelf;

        /// <summary>Builds every screen. <paramref name="profile"/> supplies the current save for home/journal/abilities.</summary>
        public void Build(UiTheme theme, int maxHealth, IExpeditionUiHost host, FeelSettings feel, UiPreferences prefs, ExpeditionContent content, System.Func<SaveData> profile, string version)
        {
            _theme = theme != null ? theme : UiTheme.Load();
            _strings = _theme != null ? _theme.LoadStrings() : new StringTable("en");
            _texts = new TextScaleRegistry();
            _f = new UiFactory(_theme, _strings, _texts);
            _host = host;
            _feel = feel;
            _prefs = prefs;
            _content = content;
            _profile = profile;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _canvas.pixelPerfect = false;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(ScreenFrame.ShortSide, ScreenFrame.ShortSide);
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.referencePixelsPerUnit = 100f;
            gameObject.AddComponent<GraphicRaycaster>();
            _rootRect = (RectTransform)transform;
            SetOrientation(Screen.width >= Screen.height);

            // Full-screen tint (under water, curtain droplets), below everything.
            _overlay = _f.Image(transform, "Overlay", null, Color.clear);
            _overlay.type = Image.Type.Simple;
            UiFactory.Stretch(_overlay.rectTransform);
            _overlay.gameObject.SetActive(false);

            RectTransform hudCanvas = SubCanvas("HudCanvas", 0);
            RectTransform safe = UiFactory.Rect(hudCanvas, "SafeArea");
            UiFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            _hud = new HudView(_f, safe, maxHealth, () => _host?.TogglePause());

            _desktopHint = _f.Label(safe, "DesktopHint", string.Empty, FontKind.Body, 13, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerCenter, true, false, true);
            UiFactory.Anchor(_desktopHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(760f, 22f), new Vector2(0f, 4f));
            _debug = _f.Label(safe, "Debug", string.Empty, FontKind.Body, 13, Color.white, TextAnchor.UpperLeft, true, false, true);
            UiFactory.Anchor(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(460f, 300f), new Vector2(16f, -150f));
            _debug.gameObject.SetActive(false);

            RectTransform menus = SubCanvas("MenuCanvas", 1);
            _f.CappedScale = true; // fixed layouts
            _home = new HomeView(_f, menus, () => _host?.StartExpedition(), () => Push(UiScreen.Journal), () => Push(UiScreen.Abilities), () => Push(UiScreen.Settings));
            _revive = new ReviveView(_f, menus, () => _host?.Revive(), () => _host?.DeclineRevive());
            _results = new ResultsView(_f, menus, content, () => _host?.RunAgain(), () => _host?.OpenObjective(), () => _host?.ReturnToCamp());
            _upgrade = new UpgradeView(_f, menus, () => _host?.LearnSelected(), () => _host?.CloseUpgrade());
            _pause = new PauseView(_f, menus, () => _host?.TogglePause(), () => Push(UiScreen.Settings), () => _host?.ReturnToCamp());
            _f.CappedScale = false; // scrolling lists take the full text size
            _journal = new JournalView(_f, menus, content, () => Back());
            _abilities = new AbilitiesView(_f, menus, content, () => Back(), a => _host?.OpenAbility(a.Ability));
            _settings = new SettingsView(_f, menus, feel, prefs, Application.version, _theme.PrivacyPolicyUrl, () => Back(), () => Push(UiScreen.Credits), () =>
            {
                ApplyTextScale();
                _host?.SettingsChanged();
            });
            _credits = new CreditsView(_f, menus, () => Back());
            ApplyTextScale();
            Relayout(true);
            SyncScreens(true);
        }

        // ---- navigation ----

        /// <summary>Home (camp) as the base screen; the in-run HUD hides.</summary>
        public void ShowHome()
        {
            _nav.Reset(UiScreen.Home);
            _home.Refresh(_profile?.Invoke());
            HideRevive();
            SyncScreens(false);
        }

        /// <summary>The run's HUD only (START EXPEDITION, RUN AGAIN, resume).</summary>
        public void ShowHudOnly()
        {
            _nav.Reset(UiScreen.Hud);
            SyncScreens(false);
        }

        public void OpenPauseMenu()
        {
            if (_nav.Root == UiScreen.Hud && _nav.Current == UiScreen.Hud)
            {
                _nav.Push(UiScreen.Pause);
                SyncScreens(false);
            }
        }

        public void ClosePauseMenu()
        {
            if (_nav.Root == UiScreen.Hud && _nav.Depth > 1)
            {
                _nav.Reset(UiScreen.Hud);
                SyncScreens(false);
            }
        }

        public void Push(UiScreen screen)
        {
            switch (screen)
            {
                case UiScreen.Journal:
                    _journal.Refresh(_profile?.Invoke());
                    break;
                case UiScreen.Abilities:
                    _abilities.Refresh(_profile?.Invoke());
                    break;
                case UiScreen.Settings:
                    _settings.Refresh();
                    break;
            }

            _nav.Push(screen);
            SyncScreens(false);
        }

        /// <summary>Back one level (header back buttons, Esc). Pause → resume; ability card → where it came from.</summary>
        public bool Back()
        {
            switch (_nav.Current)
            {
                case UiScreen.Pause:
                    _host?.TogglePause();
                    return true;
                case UiScreen.Upgrade:
                    _host?.CloseUpgrade();
                    return true;
            }

            if (!_nav.Back())
            {
                return false;
            }

            if (_nav.Current == UiScreen.Abilities)
            {
                _abilities.Refresh(_profile?.Invoke());
            }

            SyncScreens(false);
            return true;
        }

        // ---- HUD ----

        public void SetDistance(float metres)
        {
            _hud.SetDistance(metres < 0f ? 0 : (int)metres);
        }

        public void SetWallet(int coins, int crystals)
        {
            _hud.SetWallet(coins, crystals);
        }

        public void SetHealth(int health, bool shield, float shieldFraction)
        {
            _hud.SetHealth(health, shield, shieldFraction);
        }

        public void SetCenter(string message)
        {
            _hud.SetCenter(message);
        }

        public void ShowToast(string title, string line)
        {
            _hud.ShowToast(title, line);
        }

        public void HideToast()
        {
            _hud.HideToast();
        }

        /// <summary>First-run gesture hint (HelpMove value), −1 hides it.</summary>
        public void SetHelp(int move)
        {
            _hud.SetHelp(move);
        }

        public void SetOrientation(bool landscape)
        {
            if (_scaler != null)
            {
                _scaler.matchWidthOrHeight = landscape ? 1f : 0f;
            }
        }

        public void SetOverlay(Color color, float alpha)
        {
            if (_overlay == null)
            {
                return;
            }

            bool show = alpha > 0.005f;
            if (_overlay.gameObject.activeSelf != show)
            {
                _overlay.gameObject.SetActive(show);
            }

            if (show)
            {
                color.a = alpha;
                if (color != _overlayColor)
                {
                    _overlayColor = color;
                    _overlay.color = color;
                }
            }
        }

        public void ShowRevive(int cost, bool affordable, float secondsLeft)
        {
            ShowRevive(cost, affordable, secondsLeft, 4f);
        }

        public void ShowRevive(int cost, bool affordable, float secondsLeft, float total)
        {
            _revive.Show(cost, affordable, secondsLeft, total, ReducedMotion);
        }

        public void HideRevive()
        {
            _revive?.Hide();
        }

        // ---- results and ability card ----

        public void ShowResults(RunResults results, float countUpTime)
        {
            _nav.Reset(UiScreen.Results);
            HideRevive();
            _results.Show(results, countUpTime, ReducedMotion);
            SyncScreens(false);
        }

        public void HideResults()
        {
            if (_nav.Root == UiScreen.Results)
            {
                ShowHudOnly();
            }

            _results.Hide();
        }

        public void CompleteCountUp()
        {
            _results.CompleteCountUp();
        }

        public void ShowUpgrade(AbilityDefinition ability, int coins, int crystals, bool canLearn, bool owned)
        {
            _results.CompleteCountUp();
            _upgrade.Fill(ability, coins, crystals, canLearn, owned);
            _nav.Push(UiScreen.Upgrade);
            SyncScreens(false);
        }

        public void ShowLearned(string name)
        {
            _upgrade.ShowLearned(name);
        }

        /// <summary>Closes the ability card back to the screen under it (results or abilities).</summary>
        public void HideUpgrade(bool backToPrevious)
        {
            if (_nav.Current == UiScreen.Upgrade)
            {
                _nav.Back();
                if (_nav.Current == UiScreen.Abilities)
                {
                    _abilities.Refresh(_profile?.Invoke());
                }

                SyncScreens(false);
            }
        }

        /// <summary>After LEARN: the objective card shows the learned line and RUN AGAIN is highlighted.</summary>
        public void SetObjective(string text, bool highlightRunAgain)
        {
            _results.SetObjectiveLearned(text, highlightRunAgain);
        }

        // ---- debug ----

        public void SetDebugVisible(bool visible)
        {
            _debug.gameObject.SetActive(visible);
        }

        public void SetDebugText(string text)
        {
            _debug.text = text;
        }

        public string DebugText => _debug != null ? _debug.text : string.Empty;

        public void SetHint(string text)
        {
            _desktopHint.text = text ?? string.Empty;
            _desktopHint.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>True if a screen pixel is over a UI control (touches there belong to the UI, not steering).</summary>
        public bool HitsControl(Vector2 screenPixel)
        {
            if (_nav.Current != UiScreen.Hud || ReviveVisible)
            {
                return true;
            }

            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(_hud.PauseRect, screenPixel, cam);
        }

        /// <summary>Per frame: transitions, count-ups, toast/hint motion, relayout on rotation or safe-area change.</summary>
        public void Tick(float seconds)
        {
            Relayout(false);
            bool reduced = ReducedMotion;
            _hud.Tick(seconds, reduced);
            _results.Tick(seconds);
            _home.Fader.Tick(seconds);
            _pause.Shell.Fader.Tick(seconds);
            _settings.Shell.Fader.Tick(seconds);
            _credits.Shell.Fader.Tick(seconds);
            _journal.Shell.Fader.Tick(seconds);
            _abilities.Shell.Fader.Tick(seconds);
            _results.Shell.Fader.Tick(seconds);
            _upgrade.Shell.Fader.Tick(seconds);
            _revive.Shell.Fader.Tick(seconds);
        }

        /// <summary>
        /// Tools: render the UI through <paramref name="cam"/> into a <paramref name="widthPx"/>×<paramref name="heightPx"/>
        /// target (screenshots, video). Sets the scale directly (the scaler doesn't tick outside Play mode).
        /// </summary>
        public void ConfigureForCapture(Camera cam, int widthPx, int heightPx)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = cam;
            _canvas.planeDistance = cam.nearClipPlane + 0.05f;
            _scaler.enabled = false;
            _canvas.scaleFactor = Mathf.Min(widthPx, heightPx) / ScreenFrame.ShortSide;
            foreach (SafeAreaFitter fitter in GetComponentsInChildren<SafeAreaFitter>(true))
            {
                fitter.Refresh();
            }

            ForceLayout();
        }

        /// <summary>Forces a layout pass (tools after changing the simulated device).</summary>
        public void ForceLayout()
        {
            Canvas.ForceUpdateCanvases();
            Relayout(true);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>Called by the settings screen and the App after any preference change.</summary>
        public void ApplyTextScale()
        {
            if (_prefs == null)
            {
                return;
            }

            _texts.SetScales(_prefs.TextScale, _prefs.HudTextScale);
            float s = _prefs.TextScale;
            _settings?.Shell.ScaleRows(s);
            _journal?.Shell.ScaleRows(s);
            _abilities?.Shell.ScaleRows(s);
            Relayout(true);
        }

        private void Relayout(bool force)
        {
            if (_rootRect == null)
            {
                return;
            }

            Vector2 size = _rootRect.rect.size;
            if (size.x <= 1f || size.y <= 1f)
            {
                return;
            }

            bool landscape = size.x >= size.y;
            if (_scaler != null && Mathf.Abs(_scaler.matchWidthOrHeight - (landscape ? 1f : 0f)) > 0.01f)
            {
                SetOrientation(landscape);
            }

            Rect safe = SafeAreaFitter.CurrentNormalized();
            float scale = _prefs != null ? _prefs.TextScale : 1f;
            bool hand = _prefs != null && _prefs.LeftHanded;
            if (!force && size == _laidOutSize && safe == _laidOutSafe && scale == _laidOutScale && hand == _laidOutHand)
            {
                return;
            }

            _laidOutSize = size;
            _laidOutSafe = safe;
            _laidOutScale = scale;
            _laidOutHand = hand;
            _frame = new ScreenFrame(size.x, size.y, safe.xMin * size.x, (1f - safe.xMax) * size.x, (1f - safe.yMax) * size.y, safe.yMin * size.y);
            float hudScale = _prefs != null ? _prefs.HudTextScale : 1f;
            _hud.Layout(_frame, hand, hudScale);
            _home.Layout(_frame, scale);
            _results.Layout(_frame, Mathf.Min(scale, UiPreferences.HudTextScaleMax));
            _pause.Shell.Layout(_frame);
            _settings.Shell.Layout(_frame);
            _credits.Shell.Layout(_frame);
            _journal.Shell.Layout(_frame);
            _abilities.Shell.Layout(_frame);
            _upgrade.Shell.Layout(_frame);
            _revive.Shell.Layout(_frame);
        }

        private void SyncScreens(bool instant)
        {
            bool fast = instant || ReducedMotion;
            UiScreen current = _nav.Current;
            UiScreen root = _nav.Root;
            _hud.SetVisible(root == UiScreen.Hud);
            _home.Show(root == UiScreen.Home, fast);
            _home.SetContentVisible(current == UiScreen.Home);
            _pause.Shell.Show(current == UiScreen.Pause, fast);
            _settings.Shell.Show(current == UiScreen.Settings, fast);
            _credits.Shell.Show(current == UiScreen.Credits, fast);
            _journal.Shell.Show(current == UiScreen.Journal, fast);
            _abilities.Shell.Show(current == UiScreen.Abilities, fast);
            _results.Shell.Show(current == UiScreen.Results, fast);
            _upgrade.Shell.Show(current == UiScreen.Upgrade, fast);
            if (current == UiScreen.Results && root == UiScreen.Results)
            {
                _results.Shell.Root.gameObject.SetActive(true);
            }
        }

        private RectTransform SubCanvas(string name, int order)
        {
            RectTransform rt = UiFactory.Rect(transform, name);
            UiFactory.Stretch(rt);
            Canvas c = rt.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = _canvas.sortingOrder + 1 + order;
            rt.gameObject.AddComponent<GraphicRaycaster>();
            return rt;
        }
    }
}
