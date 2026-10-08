using System.Globalization;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Continue screen (GDD 14.4; placeholder look matching the other panels): "Continue?" title, the seconds left
    /// in big digits with a shrinking teal timer bar, then the options: the free first-session continue from the
    /// companion, or Continue for coins with the price and a coin icon (disabled if the wallet is short); a disabled
    /// "Watch ad (soon)" placeholder (rewarded ads arrive with monetization, week 5); Skip. The player's coin total
    /// is shown under the buttons. Buttons ignore input during the screen's input lock. Texts are formatted when the
    /// panel opens; per frame only the countdown digit (cached strings) and the bar change.
    /// </summary>
    public sealed class ContinuePanel
    {
        private const float WidthPt = 320f;
        private const float HeightPt = 470f;
        private const float RowWidthPt = 260f;
        private const int TitleFontSize = 34;
        private const int SecondsFontSize = 56;
        private const int NoteFontSize = 18;
        private const int PrimaryFontSize = 24;
        private const int ButtonFontSize = 20;
        private const int SmallFontSize = 15;
        private const float BarHeightPt = 12f;

        private static readonly string[] SecondStrings = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private readonly GameObject _root;
        private readonly Text _seconds;
        private readonly RectTransform _barFill;
        private readonly Text _note;
        private readonly Text _primaryLabel;
        private readonly GameObject _priceCoin;
        private readonly Text _wallet;
        private int _shownSeconds = -1;
        private float _shownBar = -1f;
        private bool _canContinue;

        public ContinuePanel(Transform safeArea, Font font, IContinueCommands commands)
        {
            Image fill = HudFactory.CreatePanel(
                safeArea, "ContinuePanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 50f), new Vector2(0f, -10f), MenuStrings.ContinueTitle);
            _seconds = MenuFactory.Label(panel, "Seconds", font, SecondsFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 64f), new Vector2(0f, -60f), string.Empty);

            Image bar = HudFactory.CreateImage(panel, "TimerBar", StylePalette.Ink, false);
            HudFactory.Place(bar.rectTransform, top, new Vector2(RowWidthPt, BarHeightPt), new Vector2(0f, -132f));
            Image barInner = HudFactory.CreateImage(bar.transform, "Inner", StylePalette.CreamPath, false);
            HudFactory.Stretch(barInner.rectTransform, 2f);
            Image barFill = HudFactory.CreateImage(barInner.transform, "Fill", StylePalette.PistaTealSash, false);
            _barFill = barFill.rectTransform;
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = Vector2.one;
            _barFill.offsetMin = Vector2.zero;
            _barFill.offsetMax = Vector2.zero;

            _note = MenuFactory.Label(panel, "Note", font, NoteFontSize, TextAnchor.MiddleCenter, top, new Vector2(RowWidthPt, 26f), new Vector2(0f, -156f), string.Empty);

            ContinueButton = MenuFactory.PrimaryButton(
                panel, "ContinueButton", font, MenuStrings.ContinuePaid, PrimaryFontSize, top, new Vector2(RowWidthPt, 72f), new Vector2(0f, -192f), () => commands.ContinueRun());
            _primaryLabel = ContinueButton.transform.Find("Label").GetComponent<Text>();
            RectTransform coinHolder = HudFactory.CreateRect(ContinueButton.transform, "PriceCoin");
            HudFactory.Place(coinHolder, new Vector2(1f, 0.5f), new Vector2(26f, 26f), new Vector2(-16f, 0f));
            MenuFactory.CoinIcon(coinHolder, new Vector2(0.5f, 0.5f), Vector2.zero);
            _priceCoin = coinHolder.gameObject;

            WatchAdButton = MenuFactory.SecondaryButton(
                panel, "WatchAdButton", font, MenuStrings.ContinueWatchAd, ButtonFontSize, top, new Vector2(RowWidthPt, 52f), new Vector2(0f, -276f), null);
            WatchAdButton.interactable = false;

            SkipButton = MenuFactory.NeutralButton(
                panel, "SkipButton", font, MenuStrings.ContinueSkip, ButtonFontSize, top, new Vector2(RowWidthPt, 52f), new Vector2(0f, -340f), commands.SkipContinue);

            _wallet = MenuFactory.Label(panel, "Wallet", font, SmallFontSize, TextAnchor.MiddleCenter, bottom, new Vector2(RowWidthPt, 22f), new Vector2(0f, 30f), string.Empty);
#if UNITY_EDITOR || UNITY_STANDALONE
            MenuFactory.Label(panel, "KeyHint", font, SmallFontSize, TextAnchor.MiddleCenter, bottom, new Vector2(RowWidthPt, 22f), new Vector2(0f, 8f), MenuStrings.ContinueKeyHint);
#endif

            _root.SetActive(false);
        }

        public Button ContinueButton { get; }

        /// <summary>Rewarded-ad placeholder; always disabled until ads exist.</summary>
        public Button WatchAdButton { get; }

        public Button SkipButton { get; }

        public bool Visible => _root.activeSelf;

        /// <summary>Fills and shows the panel (allocates a few strings; once per offer).</summary>
        public void Show(GameSession session, IContinueCommands commands, PlayerSave save)
        {
            bool free = commands.FreeContinueAvailable;
            int cost = commands.NextContinueCost;
            _canContinue = free || commands.CanAffordContinue;
            if (free)
            {
                _primaryLabel.text = MenuStrings.ContinueFree;
                _note.text = MenuStrings.ContinueFreeNote;
            }
            else
            {
                _primaryLabel.text = MenuStrings.ContinuePaid + "  " + cost.ToString(CultureInfo.InvariantCulture) + "      ";
                _note.text = string.Empty;
            }

            _priceCoin.SetActive(!free);
            long coins = save != null ? save.TotalCoins : 0L;
            _wallet.text = MenuStrings.ContinueWallet + ": " + coins.ToString(CultureInfo.InvariantCulture);
            _shownSeconds = -1;
            _shownBar = -1f;
            UpdateTimer(session);
            SetLocked(session.ContinueInputLocked);
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root.activeSelf)
            {
                _root.SetActive(false);
            }
        }

        /// <summary>Countdown digit and bar. No allocation.</summary>
        public void UpdateTimer(GameSession session)
        {
            double left = session.ContinueSecondsLeft;
            int seconds = (int)System.Math.Ceiling(left - 1e-9);
            if (seconds < 0)
            {
                seconds = 0;
            }
            else if (seconds >= SecondStrings.Length)
            {
                seconds = SecondStrings.Length - 1;
            }

            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _seconds.text = SecondStrings[seconds];
            }

            double total = session.ContinueRules.OfferSeconds;
            float bar = total > 0.0 ? Mathf.Clamp01((float)(left / total)) : 0f;
            if (Mathf.Abs(bar - _shownBar) > 0.002f)
            {
                _shownBar = bar;
                _barFill.anchorMax = new Vector2(bar, 1f);
            }
        }

        public void SetLocked(bool locked)
        {
            ContinueButton.interactable = !locked && _canContinue;
            SkipButton.interactable = !locked;
        }
    }
}
