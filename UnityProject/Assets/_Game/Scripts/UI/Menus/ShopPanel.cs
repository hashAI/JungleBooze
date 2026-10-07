using System.Globalization;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Meta;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// The coin shop (GDD 13.4; placeholder look): power-up upgrades (levels 2 to 5) and the two start boosts, Head
    /// Start and Shield start, which can be armed for the next run. Prices come from <see cref="MetaConfig"/>. A
    /// button the player cannot afford is greyed out and still shows its price. Characters and outfits are not for
    /// sale yet (they are the owner's call), and there are no real-money purchases yet, so no Restore Purchases.
    /// </summary>
    public sealed class ShopPanel
    {
        private const float WidthPt = 330f;
        private const float HeightPt = 650f;
        private const float ContentWidthPt = 290f;
        private const float RowStepPt = 58f;
        private const int TitleFontSize = 34;
        private const int HeaderFontSize = 20;
        private const int RowFontSize = 18;
        private const int ButtonFontSize = 18;
        private const int InfoFontSize = 15;
        private const int BackFontSize = 24;

        /// <summary>Buttons need a non-empty label to get a text object; the real text is set when the panel opens.</summary>
        private const string Placeholder = " ";

        private static readonly UpgradeTrack[] Tracks = { UpgradeTrack.Magnet, UpgradeTrack.Shield, UpgradeTrack.SpeedBoost };

        private readonly MetaProgress _meta;
        private readonly UnityAction _onClosed;
        private readonly GameObject _root;
        private readonly Text _wallet;
        private readonly Text[] _upgradeLabels = new Text[3];
        private readonly Button[] _upgradeButtons = new Button[3];
        private readonly Text[] _upgradeButtonLabels = new Text[3];
        private readonly Text _headStartLabel;
        private readonly Text _shieldStartLabel;
        private readonly Text _headStartBuyLabel;
        private readonly Text _shieldStartBuyLabel;
        private readonly Text _headStartUseLabel;
        private readonly Text _shieldStartUseLabel;

        public ShopPanel(Transform safeArea, Font font, MetaProgress meta, UnityAction onClosed)
        {
            _meta = meta;
            _onClosed = onClosed;

            Image fill = HudFactory.CreatePanel(
                safeArea, "ShopPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 50f), new Vector2(0f, -8f), MetaStrings.ShopTitle);

            // Wallet: coin icon and the number, centered under the title.
            RectTransform walletRow = HudFactory.CreateRect(panel, "WalletRow");
            HudFactory.Place(walletRow, top, new Vector2(ContentWidthPt, 30f), new Vector2(0f, -60f));
            MenuFactory.CoinIcon(walletRow, new Vector2(0.5f, 0.5f), new Vector2(-60f, 0f));
            _wallet = MenuFactory.Label(walletRow, "Wallet", font, HeaderFontSize, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(110f, 30f), new Vector2(15f, 0f), string.Empty);

            MenuFactory.Label(panel, "UpgradesHeader", font, InfoFontSize + 1, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 24f), new Vector2(0f, -96f), MetaStrings.UpgradesHeader);
            for (int i = 0; i < Tracks.Length; i++)
            {
                float y = -124f - i * RowStepPt;
                _upgradeLabels[i] = MenuFactory.Label(panel, "Upgrade" + i, font, RowFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt - 120f, 48f), new Vector2(-60f, y), string.Empty);
                UpgradeTrack track = Tracks[i];
                _upgradeButtons[i] = MenuFactory.SecondaryButton(
                    panel, "UpgradeButton" + i, font, Placeholder, ButtonFontSize, top, new Vector2(110f, 48f), new Vector2(ContentWidthPt * 0.5f - 55f, y), () => BuyUpgrade(track));
                _upgradeButtonLabels[i] = _upgradeButtons[i].GetComponentInChildren<Text>();
            }

            float boostsY = -124f - Tracks.Length * RowStepPt - 4f;
            MenuFactory.Label(panel, "BoostsHeader", font, InfoFontSize + 1, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 24f), new Vector2(0f, boostsY), MetaStrings.StartBoostsHeader);

            float headY = boostsY - 28f;
            _headStartLabel = MenuFactory.Label(panel, "HeadStartLabel", font, RowFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 24f), new Vector2(0f, headY), string.Empty);
            HeadStartBuyButton = MenuFactory.SecondaryButton(
                panel, "HeadStartBuyButton", font, Placeholder, ButtonFontSize, top, new Vector2(120f, 48f), new Vector2(-85f, headY - 30f), BuyHeadStart);
            _headStartBuyLabel = HeadStartBuyButton.GetComponentInChildren<Text>();
            HeadStartUseButton = MenuFactory.NeutralButton(
                panel, "HeadStartUseButton", font, Placeholder, ButtonFontSize - 3, top, new Vector2(160f, 48f), new Vector2(65f, headY - 30f), ToggleHeadStart);
            _headStartUseLabel = HeadStartUseButton.GetComponentInChildren<Text>();

            float shieldY = headY - 92f;
            _shieldStartLabel = MenuFactory.Label(panel, "ShieldStartLabel", font, RowFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 24f), new Vector2(0f, shieldY), string.Empty);
            ShieldStartBuyButton = MenuFactory.SecondaryButton(
                panel, "ShieldStartBuyButton", font, Placeholder, ButtonFontSize, top, new Vector2(120f, 48f), new Vector2(-85f, shieldY - 30f), BuyShieldStart);
            _shieldStartBuyLabel = ShieldStartBuyButton.GetComponentInChildren<Text>();
            ShieldStartUseButton = MenuFactory.NeutralButton(
                panel, "ShieldStartUseButton", font, Placeholder, ButtonFontSize - 3, top, new Vector2(160f, 48f), new Vector2(65f, shieldY - 30f), ToggleShieldStart);
            _shieldStartUseLabel = ShieldStartUseButton.GetComponentInChildren<Text>();

            MenuFactory.Label(panel, "ComingSoon", font, InfoFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 22f), new Vector2(0f, shieldY - 86f), MetaStrings.ComingSoon);

            BackButton = MenuFactory.NeutralButton(
                panel, "BackButton", font, MenuStrings.Back, BackFontSize, new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 20f), Close);

            _root.SetActive(false);
        }

        public Button BackButton { get; }

        public Button HeadStartBuyButton { get; }

        public Button HeadStartUseButton { get; }

        public Button ShieldStartBuyButton { get; }

        public Button ShieldStartUseButton { get; }

        /// <summary>Upgrade button for Magnet (0), Shield (1) or Speed Boost (2).</summary>
        public Button UpgradeButton(int index)
        {
            return _upgradeButtons[index];
        }

        public bool Visible => _root.activeSelf;

        /// <summary>Shows the panel with the current wallet and inventory.</summary>
        public void Open()
        {
            Refresh();
            _root.SetActive(true);
        }

        /// <summary>Back: hides the panel, writes the save if needed, then notifies the owner.</summary>
        public void Close()
        {
            if (!_root.activeSelf)
            {
                return;
            }

            _root.SetActive(false);
            _meta?.Save.SaveIfDirty();
            _onClosed?.Invoke();
        }

        private static void ShowToggle(Button button, Text label, bool on, bool available)
        {
            label.text = MetaStrings.Format(MetaStrings.UseNextRunFormat, on ? MenuStrings.On : MenuStrings.Off);
            ((Image)button.targetGraphic).color = on ? StylePalette.PistaTealSash : StylePalette.Parchment;
            label.color = on ? StylePalette.Parchment : StylePalette.Ink;
            button.interactable = available;
        }

        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private void BuyUpgrade(UpgradeTrack track)
        {
            if (_meta != null && _meta.Shop.TryBuyUpgrade(track))
            {
                _meta.Save.SaveIfDirty();
                Refresh();
            }
        }

        private void BuyHeadStart()
        {
            if (_meta != null && _meta.Shop.TryBuyHeadStart())
            {
                _meta.Save.SaveIfDirty();
                Refresh();
            }
        }

        private void BuyShieldStart()
        {
            if (_meta != null && _meta.Shop.TryBuyShieldStart())
            {
                _meta.Save.SaveIfDirty();
                Refresh();
            }
        }

        private void ToggleHeadStart()
        {
            if (_meta != null)
            {
                _meta.Shop.SetHeadStartArmed(!_meta.Shop.HeadStartArmed);
                Refresh();
            }
        }

        private void ToggleShieldStart()
        {
            if (_meta != null)
            {
                _meta.Shop.SetShieldStartArmed(!_meta.Shop.ShieldStartArmed);
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_meta == null)
            {
                return;
            }

            ShopService shop = _meta.Shop;
            _wallet.text = Number(shop.Coins);
            for (int i = 0; i < Tracks.Length; i++)
            {
                UpgradeTrack track = Tracks[i];
                _upgradeLabels[i].text = MetaStrings.Format(MetaStrings.UpgradeFormat, MetaStrings.UpgradeName(track), shop.GetLevel(track), ShopService.MaxUpgradeLevel);
                if (shop.IsMaxLevel(track))
                {
                    _upgradeButtonLabels[i].text = MetaStrings.MaxLevel;
                    _upgradeButtons[i].interactable = false;
                }
                else
                {
                    _upgradeButtonLabels[i].text = MetaStrings.Format(MetaStrings.BuyFormat, shop.NextUpgradePrice(track));
                    _upgradeButtons[i].interactable = shop.CanBuyUpgrade(track);
                }
            }

            _headStartLabel.text = MetaStrings.Format(MetaStrings.HeadStartFormat, shop.HeadStartDistanceM, shop.HeadStarts);
            _headStartBuyLabel.text = MetaStrings.Format(MetaStrings.BuyFormat, shop.HeadStartPrice);
            HeadStartBuyButton.interactable = shop.CanBuyHeadStart;
            ShowToggle(HeadStartUseButton, _headStartUseLabel, shop.HeadStartArmed, shop.HeadStarts > 0);

            _shieldStartLabel.text = MetaStrings.Format(MetaStrings.ShieldStartFormat, shop.ShieldStarts);
            _shieldStartBuyLabel.text = MetaStrings.Format(MetaStrings.BuyFormat, shop.ShieldStartPrice);
            ShieldStartBuyButton.interactable = shop.CanBuyShieldStart;
            ShowToggle(ShieldStartUseButton, _shieldStartUseLabel, shop.ShieldStartArmed, shop.ShieldStarts > 0);
        }
    }
}
