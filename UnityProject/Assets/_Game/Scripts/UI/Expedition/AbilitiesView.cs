using System;
using System.Collections.Generic;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// Abilities (GDD §13) in unlock order: learned (check), learnable now (VIEW opens the ability card with its exact
    /// coin price), or coming later (lock). Prices come from the content, never from UI code.
    /// </summary>
    public sealed class AbilitiesView
    {
        private readonly UiFactory _f;
        private readonly List<AbilityDefinition> _order = new List<AbilityDefinition>();
        private readonly Image[] _icons;
        private readonly Text[] _details;
        private readonly UiButton[] _views;
        private readonly Text[] _states;

        public AbilitiesView(UiFactory f, Transform canvas, ExpeditionContent content, UnityAction onBack, Action<AbilityDefinition> onOpen)
        {
            _f = f;
            Shell = new ScreenShell(f, canvas, "Abilities", new Vector2(400f, 720f), new Vector2(640f, 360f));
            Shell.AddHeader(f.S("abilities.title"), onBack);
            Shell.AddList(6f);
            if (content != null)
            {
                for (int i = 0; i < content.Abilities.Count; i++)
                {
                    _order.Add(content.Abilities[i]);
                }

                _order.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            int n = _order.Count;
            _icons = new Image[n];
            _details = new Text[n];
            _views = new UiButton[n];
            _states = new Text[n];
            for (int i = 0; i < n; i++)
            {
                AbilityDefinition a = _order[i];
                RectTransform row = Shell.Row("Ability" + i, 84f);
                Image card = f.Image(row, "Card", f.Theme.Chip, Color.white);
                UiFactory.Stretch(card.rectTransform);
                _icons[i] = f.Icon(row, "Icon", f.Theme.Abilities, 48f);
                UiFactory.Anchor(_icons[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(48f, 48f), new Vector2(12f, 0f));
                Text name = f.Label(row, "Name", a.Name, FontKind.Heavy, 18, UiColors.Cream, TextAnchor.LowerLeft);
                name.rectTransform.anchorMin = new Vector2(0f, 0.55f);
                name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.offsetMin = new Vector2(72f, 0f);
                name.rectTransform.offsetMax = new Vector2(-112f, -6f);
                _details[i] = f.Label(row, "Line", a.Line, FontKind.Body, 13, UiColors.Mist, TextAnchor.UpperLeft);
                _details[i].rectTransform.anchorMin = Vector2.zero;
                _details[i].rectTransform.anchorMax = new Vector2(1f, 0.55f);
                _details[i].rectTransform.offsetMin = new Vector2(72f, 6f);
                _details[i].rectTransform.offsetMax = new Vector2(-112f, -2f);
                AbilityDefinition captured = a;
                _views[i] = f.Button(row, "View", ButtonStyle.Secondary, f.S("abilities.open"), null, 16, new Vector2(96f, 44f), () => onOpen?.Invoke(captured));
                UiFactory.Anchor(_views[i].Root, new Vector2(1f, 0.5f), new Vector2(96f, 44f), new Vector2(-10f, 0f));
                _states[i] = f.Label(row, "State", string.Empty, FontKind.Heavy, 14, UiColors.Gold, TextAnchor.MiddleCenter);
                UiFactory.Anchor(_states[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(100f, 44f), new Vector2(-8f, 0f));
            }
        }

        public ScreenShell Shell { get; }

        public void Refresh(SaveData profile)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                AbilityDefinition a = _order[i];
                bool owned = profile != null && ProgressionRules.Owns(profile, a.Ability);
                _views[i].SetActive(!owned && a.Implemented);
                _states[i].gameObject.SetActive(owned || !a.Implemented);
                _states[i].text = owned ? _f.S("abilities.owned") : _f.S("abilities.soon");
                _icons[i].sprite = owned || a.Implemented ? _f.Theme.Abilities : _f.Theme.Lock;
                _icons[i].color = a.Implemented ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }
        }
    }
}
