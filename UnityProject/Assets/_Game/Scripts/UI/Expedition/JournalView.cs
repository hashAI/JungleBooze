using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.World;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Journal (GDD §15, basic for the slice): every entry of the expedition content, discovered ones with name,
    /// category, rarity and sightings; undiscovered ones as a locked silhouette row. Entry art comes later.
    /// </summary>
    public sealed class JournalView
    {
        private readonly UiFactory _f;
        private readonly ExpeditionContent _content;
        private readonly Text _summary;
        private readonly Image[] _icons;
        private readonly Text[] _names;
        private readonly Text[] _details;

        public JournalView(UiFactory f, Transform canvas, ExpeditionContent content, UnityAction onBack)
        {
            _f = f;
            _content = content;
            Shell = new ScreenShell(f, canvas, "Journal", new Vector2(400f, 720f), new Vector2(640f, 360f));
            Shell.AddHeader(f.S("journal.title"), onBack);
            Shell.AddList(6f);
            RectTransform top = Shell.Row("Summary", 30f);
            _summary = f.Label(top, "Text", string.Empty, FontKind.Heavy, 15, UiColors.Turquoise, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_summary.rectTransform);
            int n = content != null ? content.Discoveries.Count : 0;
            _icons = new Image[n];
            _names = new Text[n];
            _details = new Text[n];
            for (int i = 0; i < n; i++)
            {
                RectTransform row = Shell.Row("Entry" + i, 72f);
                Image card = f.Image(row, "Card", f.Theme.Chip, Color.white);
                UiFactory.Stretch(card.rectTransform);
                _icons[i] = f.Icon(row, "Icon", f.Theme.Lock, 46f);
                UiFactory.Anchor(_icons[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(46f, 46f), new Vector2(14f, 0f));
                _names[i] = f.Label(row, "Name", string.Empty, FontKind.Heavy, 18, UiColors.Cream, TextAnchor.LowerLeft);
                _names[i].rectTransform.anchorMin = new Vector2(0f, 0.5f);
                _names[i].rectTransform.anchorMax = Vector2.one;
                _names[i].rectTransform.offsetMin = new Vector2(72f, 0f);
                _names[i].rectTransform.offsetMax = new Vector2(-12f, -6f);
                _details[i] = f.Label(row, "Detail", string.Empty, FontKind.Body, 14, UiColors.Mist, TextAnchor.UpperLeft);
                _details[i].rectTransform.anchorMin = Vector2.zero;
                _details[i].rectTransform.anchorMax = new Vector2(1f, 0.5f);
                _details[i].rectTransform.offsetMin = new Vector2(72f, 6f);
                _details[i].rectTransform.offsetMax = new Vector2(-12f, 0f);
            }
        }

        public ScreenShell Shell { get; }

        public void Refresh(SaveData profile)
        {
            if (_content == null || profile == null)
            {
                return;
            }

            int found = 0;
            for (int i = 0; i < _names.Length; i++)
            {
                DiscoveryEntry e = _content.Discoveries[i];
                JournalRecord rec = profile.FindJournal(e.Id);
                string category = _f.S(CategoryKey(e.Category));
                if (rec != null)
                {
                    found++;
                    _icons[i].sprite = e.Category == DiscoveryCategory.Location ? _f.Theme.Camp : _f.Theme.Discovery;
                    _icons[i].color = Color.white;
                    _names[i].text = e.Name;
                    _details[i].text = category + " · " + e.Rarity + " · " + _f.Strings.Format("journal.found", rec.sightings);
                }
                else
                {
                    _icons[i].sprite = _f.Theme.Lock;
                    _icons[i].color = new Color(1f, 1f, 1f, 0.7f);
                    _names[i].text = _f.S("journal.unknown");
                    _details[i].text = category;
                }
            }

            _summary.text = _f.Strings.Format("journal.count", found, _names.Length);
        }

        public static string CategoryKey(DiscoveryCategory c)
        {
            switch (c)
            {
                case DiscoveryCategory.Creature:
                    return "journal.cat.creature";
                case DiscoveryCategory.Plant:
                    return "journal.cat.plant";
                case DiscoveryCategory.Mystery:
                    return "journal.cat.mystery";
                default:
                    return "journal.cat.location";
            }
        }
    }
}
