using System.Globalization;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Main menu (GDD section 19, Home; placeholder look): title banner at the top, a card with the best score and
    /// the coin wallet, the big Play button (88+ pt, style guide 8.2) and a Settings button near the bottom thumb
    /// zone. Shown over the idle run start, inside the safe area. Texts are formatted when shown, never per frame.
    /// </summary>
    public sealed class MainMenuPanel
    {
        private const int TitleFontSize = 40;
        private const int StatFontSize = 22;
        private const int PlayFontSize = 40;
        private const int SettingsFontSize = 24;

        private readonly GameObject _root;
        private readonly Text _bestValue;
        private readonly Text _coinsValue;

        public MainMenuPanel(Transform safeArea, Font font, UnityAction onPlay, UnityAction onSettings)
        {
            RectTransform root = HudFactory.CreateRect(safeArea, "MainMenu");
            HudFactory.Stretch(root, 0f);
            _root = root.gameObject;

            // Title banner (style guide 8.1: pulp orange lettering with ink outline on a parchment panel).
            Image titleFill = HudFactory.CreatePanel(
                root, "TitlePanel", StylePalette.Parchment, new Vector2(0.5f, 1f), new Vector2(340f, 96f), new Vector2(0f, -48f));
            Text title = HudFactory.CreateText(
                titleFill.transform, "Title", font, TitleFontSize, StylePalette.PulpOrange, StylePalette.Ink, TextAnchor.MiddleCenter);
            HudFactory.Stretch(title.rectTransform, 0f);
            title.text = MenuStrings.GameTitle;

            // Stats card: best score and coin wallet.
            Image statsFill = HudFactory.CreatePanel(
                root, "StatsPanel", StylePalette.Parchment, new Vector2(0.5f, 1f), new Vector2(300f, 100f), new Vector2(0f, -172f));
            _bestValue = MenuFactory.Row(statsFill.transform, "BestRow", font, StatFontSize, MenuStrings.BestScore, 260f, -14f, out _);
            _coinsValue = MenuFactory.Row(statsFill.transform, "CoinsRow", font, StatFontSize, MenuStrings.TotalCoins, 260f, -56f, out RectTransform coinsRow);
            Text coinsLabel = coinsRow.Find("Label").GetComponent<Text>();
            coinsLabel.rectTransform.offsetMin = new Vector2(34f, 0f);
            MenuFactory.CoinIcon(coinsRow, new Vector2(0f, 0.5f), Vector2.zero);

            PlayButton = MenuFactory.PrimaryButton(
                root, "PlayButton", font, MenuStrings.Play, PlayFontSize, new Vector2(0.5f, 0f), new Vector2(260f, 100f), new Vector2(0f, 176f), onPlay);
            SettingsButton = MenuFactory.NeutralButton(
                root, "SettingsButton", font, MenuStrings.Settings, SettingsFontSize, new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 88f), onSettings);

            _root.SetActive(false);
        }

        public Button PlayButton { get; }

        public Button SettingsButton { get; }

        public bool Visible => _root.activeSelf;

        /// <summary>Best score shown (tests).</summary>
        public string BestText => _bestValue.text;

        /// <summary>Wallet shown (tests).</summary>
        public string CoinsText => _coinsValue.text;

        /// <summary>Shows the menu with fresh numbers from <paramref name="save"/> (formats two strings).</summary>
        public void Show(PlayerSave save)
        {
            _bestValue.text = (save != null ? save.BestScore : 0L).ToString(CultureInfo.InvariantCulture);
            _coinsValue.text = (save != null ? save.TotalCoins : 0L).ToString(CultureInfo.InvariantCulture);
            if (!_root.activeSelf)
            {
                _root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_root.activeSelf)
            {
                _root.SetActive(false);
            }
        }
    }
}
