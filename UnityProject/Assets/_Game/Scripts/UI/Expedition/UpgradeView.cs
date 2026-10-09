using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// The ability card (spec 103 §9.4): exactly what the player gets (name + one line), the exact price in coins
    /// (and crystals) from the content, the wallet, and LEARN (one tap) → the 1.2 s unlock banner. Coins are an
    /// in-game currency: no real-money price appears here.
    /// </summary>
    public sealed class UpgradeView
    {
        private readonly UiFactory _f;
        private readonly Text _name;
        private readonly Text _line;
        private readonly Text _cost;
        private readonly Text _wallet;
        private readonly Text _learned;
        private readonly Image _glow;

        public UpgradeView(UiFactory f, Transform canvas, UnityAction onLearn, UnityAction onBack)
        {
            _f = f;
            Shell = new ScreenShell(f, canvas, "Upgrade", new Vector2(370f, 470f), new Vector2(560f, 340f));
            Shell.AddHeader(f.S("abilities.title"), onBack);
            Transform body = Shell.Body.transform;
            RectTransform content = UiFactory.Rect(body, "Content");
            UiFactory.Stretch(content, 22f, 22f, ScreenShell.HeaderHeight + 10f, 16f);
            _glow = f.Image(content, "Glow", f.Theme.Glow, new Color(0.95f, 0.8f, 0.4f, 0.45f));
            UiFactory.Anchor(_glow.rectTransform, new Vector2(0f, 1f), new Vector2(110f, 110f), new Vector2(-14f, 12f));
            Image icon = f.Icon(content, "Icon", f.Theme.Abilities, 76f);
            UiFactory.Anchor(icon.rectTransform, new Vector2(0f, 1f), new Vector2(76f, 76f), new Vector2(4f, -4f));
            _name = f.Label(content, "Name", string.Empty, FontKind.Display, 24, UiColors.Gold, TextAnchor.MiddleLeft);
            _name.rectTransform.anchorMin = new Vector2(0f, 1f);
            _name.rectTransform.anchorMax = new Vector2(1f, 1f);
            _name.rectTransform.pivot = new Vector2(0.5f, 1f);
            _name.rectTransform.offsetMin = new Vector2(92f, -40f);
            _name.rectTransform.offsetMax = new Vector2(0f, 0f);
            _line = f.Label(content, "Line", string.Empty, FontKind.Body, 16, UiColors.Cream, TextAnchor.UpperLeft);
            _line.rectTransform.anchorMin = new Vector2(0f, 1f);
            _line.rectTransform.anchorMax = new Vector2(1f, 1f);
            _line.rectTransform.pivot = new Vector2(0.5f, 1f);
            _line.rectTransform.offsetMin = new Vector2(92f, -104f);
            _line.rectTransform.offsetMax = new Vector2(0f, -44f);
            Image coin = f.Icon(content, "CoinIcon", f.Theme.Coin, 28f);
            UiFactory.Anchor(coin.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 28f), new Vector2(0f, 110f));
            _cost = f.Label(content, "Cost", string.Empty, FontKind.Heavy, 20, UiColors.Gold, TextAnchor.MiddleLeft);
            UiFactory.Anchor(_cost.rectTransform, new Vector2(0f, 0f), new Vector2(280f, 32f), new Vector2(36f, 108f));
            _wallet = f.Label(content, "Wallet", string.Empty, FontKind.Body, 14, UiColors.Mist, TextAnchor.MiddleLeft);
            UiFactory.Anchor(_wallet.rectTransform, new Vector2(0f, 0f), new Vector2(320f, 24f), new Vector2(0f, 82f));
            Learn = f.Button(content, "Learn", ButtonStyle.Primary, string.Empty, f.Theme.Coin, 22, new Vector2(260f, 62f), onLearn);
            UiFactory.Anchor(Learn.Root, new Vector2(0.5f, 0f), new Vector2(260f, 62f), new Vector2(0f, 14f));
            _learned = f.Label(body, "Learned", string.Empty, FontKind.Display, 30, UiColors.Gold, TextAnchor.MiddleCenter, false, true, true);
            UiFactory.Anchor(_learned.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(340f, 70f), Vector2.zero);
            _learned.gameObject.SetActive(false);
        }

        public ScreenShell Shell { get; }

        public UiButton Learn { get; }

        public bool LearnInteractable => Learn.Interactable;

        public void Fill(AbilityDefinition ability, int coins, int crystals, bool canLearn, bool owned)
        {
            _name.text = ability.Name;
            _line.text = ability.Line;
            _cost.text = ability.CostCrystals > 0
                ? _f.Strings.Format("upgrade.costBoth", NumberText.Group(ability.CostCoins), NumberText.Group(ability.CostCrystals))
                : _f.Strings.Format("upgrade.cost", NumberText.Group(ability.CostCoins));
            _wallet.text = _f.Strings.Format("upgrade.wallet", NumberText.Group(coins), NumberText.Group(crystals));
            Learn.Interactable = canLearn && !owned;
            if (owned)
            {
                Learn.SetLabel(_f.S("upgrade.learned"));
            }
            else if (!canLearn && coins < ability.CostCoins)
            {
                Learn.SetLabel(_f.Strings.Format("upgrade.short", NumberText.Group(ability.CostCoins - coins)));
            }
            else
            {
                Learn.SetLabel(_f.Strings.Format("upgrade.learn", NumberText.Group(ability.CostCoins)));
            }

            _learned.gameObject.SetActive(false);
            _glow.gameObject.SetActive(canLearn && !owned);
        }

        public void ShowLearned(string name)
        {
            _learned.text = _f.Strings.Format("upgrade.learnedBanner", name.ToUpperInvariant());
            _learned.gameObject.SetActive(true);
            Learn.Interactable = false;
            Learn.SetLabel(_f.S("upgrade.learned"));
        }
    }
}
