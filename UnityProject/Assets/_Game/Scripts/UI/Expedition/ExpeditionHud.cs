using System;
using System.Globalization;
using System.Text;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;
using JungleBooze.UI.FeelTest;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Minimal gray-box HUD and results for the vertical slice (spec 103 §9, GDD §17; ui-engineer polishes later):
    /// distance, coins, crystals, health with the Shield timer, pause, centre message, discovery toast, slow-time
    /// help glyph, the "EXPEDITION COMPLETE" results with count-ups, NEW RECORD and the one next-objective card, and
    /// the ability card with LEARN. Built from code with <see cref="HudFactory"/>; in-run updates only touch the UI
    /// when a value changes and never allocate.
    /// </summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private static readonly Color HealthFull = new Color(0.36f, 0.78f, 0.38f, 1f);
        private static readonly Color HealthEmpty = new Color(0.18f, 0.16f, 0.2f, 0.75f);
        private static readonly Color ShieldColor = new Color(0.35f, 0.75f, 1f, 1f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f, 0.96f);
        private static readonly Color Ink = new Color(0.12f, 0.1f, 0.14f, 1f);
        private static readonly Color Leaf = new Color(0.3f, 0.62f, 0.32f, 1f);
        private static readonly Color Gold = new Color(0.95f, 0.76f, 0.19f, 1f);
        private static readonly Color Cyan = new Color(0.35f, 0.85f, 0.95f, 1f);
        private static readonly Color Violet = new Color(0.55f, 0.42f, 0.85f, 1f);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly IntStringCache _metres = new IntStringCache(5001, " m");
        private readonly IntStringCache _numbers = new IntStringCache(3001, string.Empty);
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safe;
        private Text _distance;
        private Text _coins;
        private Text _crystals;
        private Image[] _segments;
        private Image _shield;
        private Image _shieldTimer;
        private Text _center;
        private RectTransform _pauseButton;
        private GameObject _toast;
        private Text _toastTitle;
        private Text _toastLine;
        private Text _help;
        private GameObject _results;
        private RectTransform _resultsRect;
        private Text _resultsTitle;
        private Text _resultsBody;
        private Text _record;
        private Button _objective;
        private Text _objectiveText;
        private Button _runAgain;
        private GameObject _upgrade;
        private RectTransform _upgradeRect;
        private Text _upgradeName;
        private Text _upgradeBody;
        private Button _learn;
        private Text _learnLabel;
        private Text _learned;
        private Text _debug;
        private Text _hint;
        private Image _overlay;
        private Color _overlayColor;
        private GameObject _revive;
        private RectTransform _reviveRect;
        private Text _reviveTitle;
        private Text _reviveTimer;
        private Button _reviveButton;
        private Text _reviveLabel;
        private int _shownReviveCost = -1;
        private int _shownReviveSeconds = -1;
        private int _shownDistance = -1;
        private int _shownCoins = -1;
        private int _shownCrystals = -1;
        private int _shownHealth = -1;
        private bool _shownShield;
        private float _shownShieldFill = -1f;
        private RunResults _resultsData;
        private float _countUp;
        private float _countUpTime = 1.2f;
        private bool _counting;

        public bool ResultsVisible => _results != null && _results.activeSelf;

        public bool UpgradeVisible => _upgrade != null && _upgrade.activeSelf;

        public bool ToastVisible => _toast != null && _toast.activeSelf;

        public bool HelpVisible => _help != null && _help.gameObject.activeSelf;

        public string ResultsText => _resultsBody != null ? _resultsBody.text : string.Empty;

        public string ObjectiveText => _objectiveText != null ? _objectiveText.text : string.Empty;

        public string ToastText => _toastLine != null ? _toastLine.text : string.Empty;

        public bool RecordVisible => _record != null && _record.gameObject.activeSelf;

        public bool LearnInteractable => _learn != null && _learn.interactable;

        public bool CountingUp => _counting;

        public Canvas Canvas => _canvas;

        public bool ReviveVisible => _revive != null && _revive.activeSelf;

        public bool ReviveInteractable => _reviveButton != null && _reviveButton.interactable;

        public float OverlayAlpha => _overlay != null && _overlay.gameObject.activeSelf ? _overlay.color.a : 0f;

        public void Build(Font font, int maxHealth, Action onPause, Action onRunAgain, Action onObjective, Action onLearn, Action onUpgradeBack)
        {
            Build(font, maxHealth, onPause, onRunAgain, onObjective, onLearn, onUpgradeBack, null, null);
        }

        public void Build(Font font, int maxHealth, Action onPause, Action onRunAgain, Action onObjective, Action onLearn, Action onUpgradeBack, Action onRevive, Action onSkipRevive)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            SetOrientation(Screen.width >= Screen.height);

            // Full-screen tint (under water during a deep dive, droplets at a water curtain), below every control.
            _overlay = HudFactory.CreateImage(transform, "Overlay", Color.clear, false);
            HudFactory.Stretch(_overlay.rectTransform, 0f);
            _overlay.raycastTarget = false;
            _overlay.gameObject.SetActive(false);

            _safe = HudFactory.CreateRect(transform, "SafeArea");
            HudFactory.Stretch(_safe, 0f);
            _safe.gameObject.AddComponent<SafeAreaFitter>();

            Image distancePanel = HudFactory.CreatePanel(_safe, "DistancePanel", Parchment, new Vector2(0f, 1f), new Vector2(150f, 48f), new Vector2(16f, -12f));
            _distance = HudFactory.CreateText(distancePanel.transform, "Distance", font, 28, Ink, Color.clear, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_distance.rectTransform, 2f);
            _distance.text = _metres.Get(0);

            Image wallet = HudFactory.CreatePanel(_safe, "WalletPanel", Parchment, new Vector2(0f, 1f), new Vector2(150f, 40f), new Vector2(16f, -66f));
            Image coinIcon = HudFactory.CreateImage(wallet.transform, "CoinIcon", Gold, false);
            HudFactory.Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 16f), new Vector2(8f, 0f));
            _coins = HudFactory.CreateText(wallet.transform, "Coins", font, 22, Ink, Color.clear, TextAnchor.MiddleLeft);
            HudFactory.Place(_coins.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, 36f), new Vector2(28f, 0f));
            Image crystalIcon = HudFactory.CreateImage(wallet.transform, "CrystalIcon", Cyan, false);
            HudFactory.Place(crystalIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 14f), new Vector2(92f, 0f));
            crystalIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _crystals = HudFactory.CreateText(wallet.transform, "Crystals", font, 22, Ink, Color.clear, TextAnchor.MiddleLeft);
            HudFactory.Place(_crystals.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 36f), new Vector2(110f, 0f));

            RectTransform health = HudFactory.CreateRect(_safe, "Health");
            HudFactory.Place(health, new Vector2(0.5f, 1f), new Vector2(64f * maxHealth, 30f), new Vector2(0f, -22f));
            _segments = new Image[maxHealth];
            for (int i = 0; i < maxHealth; i++)
            {
                _segments[i] = HudFactory.CreatePanel(health, "Segment" + i, HealthFull, new Vector2(0f, 0.5f), new Vector2(56f, 24f), new Vector2(4f + (i * 64f), 0f));
            }

            _shield = HudFactory.CreateImage(health, "Shield", ShieldColor, false);
            HudFactory.Place(_shield.rectTransform, new Vector2(1f, 0.5f), new Vector2(24f, 24f), new Vector2(32f, 0f));
            _shieldTimer = HudFactory.CreateImage(_shield.transform, "Timer", Color.white, false);
            HudFactory.Place(_shieldTimer.rectTransform, new Vector2(0.5f, 0f), new Vector2(24f, 4f), new Vector2(0f, -6f));
            _shield.gameObject.SetActive(false);

            Button pause = HudFactory.CreateButton(_safe, "Pause", Parchment, new Vector2(1f, 1f), new Vector2(56f, 52f), new Vector2(-16f, -12f), font, "II", 26, Ink, Color.clear, () => onPause?.Invoke());
            _pauseButton = (RectTransform)pause.transform.parent;

            _center = HudFactory.CreateText(_safe, "Center", font, 54, Color.white, Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_center.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(600f, 80f), Vector2.zero);
            _center.gameObject.SetActive(false);

            Image toast = HudFactory.CreatePanel(_safe, "Toast", Parchment, new Vector2(0.5f, 1f), new Vector2(360f, 64f), new Vector2(0f, -60f));
            _toast = toast.transform.parent.gameObject;
            _toastTitle = HudFactory.CreateText(toast.transform, "Title", font, 18, Violet, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_toastTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(340f, 24f), new Vector2(0f, -4f));
            _toastTitle.text = "NEW DISCOVERY";
            _toastLine = HudFactory.CreateText(toast.transform, "Line", font, 18, Ink, Color.clear, TextAnchor.LowerCenter);
            _toastLine.fontStyle = FontStyle.Normal;
            HudFactory.Place(_toastLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(340f, 28f), new Vector2(0f, 6f));
            _toast.SetActive(false);

            _help = HudFactory.CreateText(_safe, "HelpGlyph", font, 72, Color.white, Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_help.rectTransform, new Vector2(0.5f, 0.35f), new Vector2(300f, 100f), Vector2.zero);
            _help.gameObject.SetActive(false);

            BuildRevive(font, onRevive, onSkipRevive);
            BuildResults(font, onRunAgain, onObjective);
            BuildUpgrade(font, onLearn, onUpgradeBack);

            _debug = HudFactory.CreateText(_safe, "Debug", font, 15, Color.white, Ink, TextAnchor.UpperLeft);
            _debug.fontStyle = FontStyle.Normal;
            HudFactory.Place(_debug.rectTransform, new Vector2(0f, 1f), new Vector2(460f, 300f), new Vector2(16f, -116f));
            _debug.gameObject.SetActive(false);

            _hint = HudFactory.CreateText(_safe, "Hint", font, 14, new Color(1f, 1f, 1f, 0.85f), Ink, TextAnchor.LowerCenter);
            _hint.fontStyle = FontStyle.Normal;
            HudFactory.Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(800f, 24f), new Vector2(0f, 8f));
        }

        private void BuildRevive(Font font, Action onRevive, Action onSkip)
        {
            // GDD §11 "Continue?": 4 s offer, skip always visible, never mandatory (spec 103 §9.2: from run 2).
            Image panel = HudFactory.CreatePanel(_safe, "Revive", Parchment, new Vector2(0.5f, 0.5f), new Vector2(320f, 200f), Vector2.zero);
            _revive = panel.transform.parent.gameObject;
            _reviveRect = (RectTransform)_revive.transform;
            _reviveTitle = HudFactory.CreateText(panel.transform, "Title", font, 30, Ink, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_reviveTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 40f), new Vector2(0f, -12f));
            _reviveTitle.text = "CONTINUE?";
            _reviveTimer = HudFactory.CreateText(panel.transform, "Timer", font, 20, Violet, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_reviveTimer.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 28f), new Vector2(0f, -52f));
            _reviveButton = HudFactory.CreateButton(panel.transform, "Continue", Cyan, new Vector2(0.5f, 0f), new Vector2(220f, 56f), new Vector2(0f, 70f), font, "Revive", 24, Ink, Color.clear, () => onRevive?.Invoke());
            _reviveLabel = _reviveButton.GetComponentInChildren<Text>();
            HudFactory.CreateButton(panel.transform, "Skip", Parchment, new Vector2(0.5f, 0f), new Vector2(120f, 44f), new Vector2(0f, 16f), font, "Skip", 20, Ink, Color.clear, () => onSkip?.Invoke());
            _revive.SetActive(false);
        }

        /// <summary>Shows or updates the revive offer (cost in crystals, seconds left). Only touches UI on changes.</summary>
        public void ShowRevive(int cost, bool affordable, float secondsLeft)
        {
            if (!_revive.activeSelf)
            {
                _revive.SetActive(true);
                _shownReviveCost = -1;
                _shownReviveSeconds = -1;
            }

            if (cost != _shownReviveCost)
            {
                _shownReviveCost = cost;
                _reviveLabel.text = "Revive · " + cost.ToString(Invariant) + (cost == 1 ? " crystal" : " crystals");
            }

            _reviveButton.interactable = affordable;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));
            if (seconds != _shownReviveSeconds)
            {
                _shownReviveSeconds = seconds;
                _reviveTimer.text = _numbers.Get(seconds);
            }
        }

        public void HideRevive()
        {
            if (_revive != null)
            {
                _revive.SetActive(false);
            }
        }

        /// <summary>Full-screen tint (alpha 0 hides it). No allocation.</summary>
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

        private void BuildResults(Font font, Action onRunAgain, Action onObjective)
        {
            Image panel = HudFactory.CreatePanel(_safe, "Results", Parchment, new Vector2(0.5f, 0.5f), new Vector2(380f, 400f), Vector2.zero);
            _results = panel.transform.parent.gameObject;
            _resultsRect = (RectTransform)_results.transform;
            _resultsTitle = HudFactory.CreateText(panel.transform, "Title", font, 28, Ink, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_resultsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(360f, 36f), new Vector2(0f, -12f));
            _record = HudFactory.CreateText(panel.transform, "NewRecord", font, 22, Gold, Ink, TextAnchor.UpperCenter);
            HudFactory.Place(_record.rectTransform, new Vector2(0.5f, 1f), new Vector2(360f, 28f), new Vector2(0f, -46f));
            _record.text = "NEW RECORD";
            _resultsBody = HudFactory.CreateText(panel.transform, "Body", font, 18, Ink, Color.clear, TextAnchor.UpperLeft);
            _resultsBody.fontStyle = FontStyle.Normal;
            HudFactory.Place(_resultsBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(330f, 170f), new Vector2(0f, -76f));

            _objective = HudFactory.CreateButton(panel.transform, "Objective", new Color(0.86f, 0.93f, 0.98f, 1f), new Vector2(0.5f, 0f), new Vector2(330f, 70f), new Vector2(0f, 92f), font, string.Empty, 18, Ink, Color.clear, () => onObjective?.Invoke());
            _objectiveText = HudFactory.CreateText(_objective.transform, "Text", font, 18, Ink, Color.clear, TextAnchor.MiddleCenter);
            HudFactory.Stretch(_objectiveText.rectTransform, 6f);
            _objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _runAgain = HudFactory.CreateButton(panel.transform, "RunAgain", Leaf, new Vector2(0.5f, 0f), new Vector2(240f, 60f), new Vector2(0f, 16f), font, "RUN AGAIN", 28, Color.white, Ink, () => onRunAgain?.Invoke());
            _results.SetActive(false);
        }

        private void BuildUpgrade(Font font, Action onLearn, Action onBack)
        {
            Image panel = HudFactory.CreatePanel(_safe, "Upgrade", Parchment, new Vector2(0.5f, 0.5f), new Vector2(360f, 300f), Vector2.zero);
            _upgrade = panel.transform.parent.gameObject;
            _upgradeRect = (RectTransform)_upgrade.transform;
            _upgradeName = HudFactory.CreateText(panel.transform, "Name", font, 28, Ink, Color.clear, TextAnchor.UpperCenter);
            HudFactory.Place(_upgradeName.rectTransform, new Vector2(0.5f, 1f), new Vector2(340f, 36f), new Vector2(0f, -14f));
            _upgradeBody = HudFactory.CreateText(panel.transform, "Body", font, 18, Ink, Color.clear, TextAnchor.UpperCenter);
            _upgradeBody.fontStyle = FontStyle.Normal;
            _upgradeBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudFactory.Place(_upgradeBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(320f, 110f), new Vector2(0f, -58f));
            _learn = HudFactory.CreateButton(panel.transform, "Learn", Leaf, new Vector2(0.5f, 0f), new Vector2(200f, 60f), new Vector2(0f, 76f), font, "LEARN", 28, Color.white, Ink, () => onLearn?.Invoke());
            _learnLabel = _learn.GetComponentInChildren<Text>();
            HudFactory.CreateButton(panel.transform, "Back", Parchment, new Vector2(0.5f, 0f), new Vector2(120f, 44f), new Vector2(0f, 18f), font, "Back", 20, Ink, Color.clear, () => onBack?.Invoke());
            _learned = HudFactory.CreateText(panel.transform, "Learned", font, 34, Gold, Ink, TextAnchor.MiddleCenter);
            HudFactory.Place(_learned.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(340f, 60f), new Vector2(0f, 10f));
            _learned.gameObject.SetActive(false);
            _upgrade.SetActive(false);
        }

        public void SetOrientation(bool landscape)
        {
            if (_scaler != null)
            {
                _scaler.referenceResolution = landscape ? new Vector2(844f, 390f) : new Vector2(390f, 844f);
            }
        }

        public void SetDistance(float metres)
        {
            int value = metres < 0f ? 0 : (int)metres;
            if (value != _shownDistance)
            {
                _shownDistance = value;
                _distance.text = _metres.Get(value);
            }
        }

        public void SetWallet(int coins, int crystals)
        {
            if (coins != _shownCoins)
            {
                _shownCoins = coins;
                _coins.text = _numbers.Get(coins);
            }

            if (crystals != _shownCrystals)
            {
                _shownCrystals = crystals;
                _crystals.text = _numbers.Get(crystals);
            }
        }

        /// <summary>Health segments and the Shield icon; <paramref name="shieldFraction"/> is the time left (0–1).</summary>
        public void SetHealth(int health, bool shield, float shieldFraction)
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

            float fill = Mathf.Round(Mathf.Clamp01(shieldFraction) * 24f) / 24f;
            if (shield && fill != _shownShieldFill)
            {
                _shownShieldFill = fill;
                _shieldTimer.rectTransform.sizeDelta = new Vector2(24f * fill, 4f);
                _shield.color = fill < 0.1f ? new Color(ShieldColor.r, ShieldColor.g, ShieldColor.b, 0.5f) : ShieldColor;
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

        public void ShowToast(string title, string line)
        {
            if (!ReferenceEquals(_toastTitle.text, title))
            {
                _toastTitle.text = title;
            }

            if (!ReferenceEquals(_toastLine.text, line))
            {
                _toastLine.text = line;
            }

            if (!_toast.activeSelf)
            {
                _toast.SetActive(true);
            }
        }

        public void HideToast()
        {
            if (_toast.activeSelf)
            {
                _toast.SetActive(false);
            }
        }

        /// <summary>Slow-time help glyph (wordless arrows); null hides it.</summary>
        public void SetHelp(string glyph)
        {
            bool show = !string.IsNullOrEmpty(glyph);
            if (_help.gameObject.activeSelf != show)
            {
                _help.gameObject.SetActive(show);
            }

            if (show && !ReferenceEquals(_help.text, glyph))
            {
                _help.text = glyph;
            }
        }

        /// <summary>Opens the results; numbers count up over <paramref name="countUpTime"/> (any tap completes them).</summary>
        public void ShowResults(RunResults results, float countUpTime)
        {
            _resultsData = results;
            _countUpTime = Mathf.Max(0.01f, countUpTime);
            _countUp = 0f;
            _counting = true;
            _resultsTitle.text = results.Title;
            _record.gameObject.SetActive(results.NewRecord);
            _objectiveText.text = ObjectiveLine(results.Objective);
            _objective.transform.parent.gameObject.SetActive(results.Objective.Kind != ObjectiveKind.None);
            RefreshResults(0f);
            _results.SetActive(true);
            _upgrade.SetActive(false);
        }

        public void HideResults()
        {
            _results.SetActive(false);
            _upgrade.SetActive(false);
            _counting = false;
        }

        /// <summary>Completes the count-up (a tap on the results).</summary>
        public void CompleteCountUp()
        {
            if (_counting)
            {
                _counting = false;
                RefreshResults(1f);
            }
        }

        /// <summary>Per frame (results screen only).</summary>
        public void Tick(float seconds)
        {
            if (!_counting || _resultsData == null)
            {
                return;
            }

            _countUp += seconds;
            float t = Mathf.Clamp01(_countUp / _countUpTime);
            RefreshResults(t);
            if (t >= 1f)
            {
                _counting = false;
            }
        }

        public void ShowUpgrade(AbilityDefinition ability, int coins, int crystals, bool canLearn, bool owned)
        {
            _upgradeName.text = ability.Name;
            var b = new StringBuilder();
            b.Append(ability.Line).Append("\n\n");
            b.Append("Cost  ").Append(ability.CostCoins.ToString(Invariant)).Append(" coins");
            if (ability.CostCrystals > 0)
            {
                b.Append(" · ").Append(ability.CostCrystals.ToString(Invariant)).Append(" crystals");
            }

            b.Append("\nYou have  ").Append(coins.ToString(Invariant)).Append(" coins · ").Append(crystals.ToString(Invariant)).Append(" crystals");
            _upgradeBody.text = b.ToString();
            _learn.interactable = canLearn && !owned;
            _learnLabel.text = owned ? "LEARNED" : "LEARN";
            _learned.gameObject.SetActive(false);
            _upgrade.SetActive(true);
            _results.SetActive(false);
        }

        /// <summary>The 1.2 s unlock moment.</summary>
        public void ShowLearned(string name)
        {
            _learned.text = name.ToUpperInvariant() + " LEARNED";
            _learned.gameObject.SetActive(true);
            _learn.interactable = false;
            _learnLabel.text = "LEARNED";
        }

        public void HideUpgrade(bool backToResults)
        {
            _upgrade.SetActive(false);
            if (backToResults)
            {
                _results.SetActive(true);
            }
        }

        /// <summary>After LEARN: the objective card shows the result and RUN AGAIN is highlighted.</summary>
        public void SetObjective(string text, bool highlightRunAgain)
        {
            _objectiveText.text = text;
            _runAgain.transform.localScale = highlightRunAgain ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
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

            if (UpgradeVisible && RectTransformUtility.RectangleContainsScreenPoint(_upgradeRect, screenPixel, null))
            {
                return true;
            }

            if (ReviveVisible && RectTransformUtility.RectangleContainsScreenPoint(_reviveRect, screenPixel, null))
            {
                return true;
            }

            return ResultsVisible && RectTransformUtility.RectangleContainsScreenPoint(_resultsRect, screenPixel, null);
        }

        public static string ObjectiveLine(NextObjective objective)
        {
            if (objective == null || objective.Kind == ObjectiveKind.None)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(objective.Detail) ? objective.Title : objective.Title + "\n" + objective.Detail;
        }

        private void RefreshResults(float t)
        {
            RunResults r = _resultsData;
            float e = 1f - ((1f - t) * (1f - t));
            var b = new StringBuilder(256);
            b.Append("Distance  ").Append(ProgressionRules.Metres(r.Distance * e)).Append('\n');
            b.Append(r.FirstExpedition ? "Best  " + ProgressionRules.Metres(r.Best) + " · first expedition" : "Best  " + ProgressionRules.Metres(r.Best)).Append('\n');
            b.Append("Coins  ").Append(((int)(r.TotalCoins * e)).ToString(Invariant));
            if (r.CleanLineCoins > 0)
            {
                b.Append("   +").Append(r.CleanLineCoins.ToString(Invariant)).Append(" Clean Line");
            }

            if (r.PerfectCoins > 0)
            {
                b.Append("   +").Append(r.PerfectCoins.ToString(Invariant)).Append(" Perfect");
            }

            if (r.DiscoveryCoins > 0)
            {
                b.Append("   +").Append(r.DiscoveryCoins.ToString(Invariant)).Append(" Discovery");
            }

            b.Append('\n');
            b.Append("Crystals  ").Append(((int)(r.Crystals * e)).ToString(Invariant)).Append('\n');
            if (r.NewDiscoveryNames.Count > 0)
            {
                b.Append("New: ");
                for (int i = 0; i < r.NewDiscoveryNames.Count; i++)
                {
                    b.Append(i > 0 ? ", " : string.Empty).Append(r.NewDiscoveryNames[i]);
                }

                b.Append('\n');
            }

            if (!string.IsNullOrEmpty(r.CategoryCounts))
            {
                b.Append(r.CategoryCounts).Append('\n');
            }

            _resultsBody.text = b.ToString();
        }
    }
}
