using System.Globalization;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Meta;
using JungleBooze.Services.Persistence;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Missions (GDD 13.2; placeholder look): the score multiplier, the set number, the 3 active missions with a
    /// progress bar and their reward, and what finishing the set pays. Done missions say "Done" in words, so state
    /// never relies on color alone. Texts are formatted when the panel opens.
    /// </summary>
    public sealed class MissionsPanel
    {
        private const float WidthPt = 330f;
        private const float HeightPt = 500f;
        private const float ContentWidthPt = 290f;
        private const float BlockTopPt = -108f;
        private const float BlockStepPt = 84f;
        private const float BarHeightPt = 14f;
        private const int TitleFontSize = 34;
        private const int InfoFontSize = 20;
        private const int DescriptionFontSize = 18;
        private const int StatusFontSize = 16;
        private const int BackFontSize = 24;

        private readonly MetaProgress _meta;
        private readonly UnityAction _onClosed;
        private readonly GameObject _root;
        private readonly Text _multiplier;
        private readonly Text _setNumber;
        private readonly Text _setReward;
        private readonly Text[] _descriptions = new Text[MissionService.SlotCount];
        private readonly Text[] _statuses = new Text[MissionService.SlotCount];
        private readonly RectTransform[] _fills = new RectTransform[MissionService.SlotCount];

        public MissionsPanel(Transform safeArea, Font font, MetaProgress meta, UnityAction onClosed)
        {
            _meta = meta;
            _onClosed = onClosed;

            Image fill = HudFactory.CreatePanel(
                safeArea, "MissionsPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 52f), new Vector2(0f, -8f), MetaStrings.MissionsTitle);
            _multiplier = MenuFactory.Label(panel, "Multiplier", font, InfoFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 26f), new Vector2(0f, -62f), string.Empty);
            _setNumber = MenuFactory.Label(panel, "SetNumber", font, InfoFontSize, TextAnchor.MiddleRight, top, new Vector2(ContentWidthPt, 26f), new Vector2(0f, -62f), string.Empty);

            for (int i = 0; i < MissionService.SlotCount; i++)
            {
                float y = BlockTopPt - i * BlockStepPt;
                _descriptions[i] = MenuFactory.Label(panel, "Description" + i, font, DescriptionFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 26f), new Vector2(0f, y), string.Empty);

                RectTransform bar = HudFactory.CreateRect(panel, "Bar" + i);
                HudFactory.Place(bar, top, new Vector2(ContentWidthPt, BarHeightPt), new Vector2(0f, y - 30f));
                Image track = HudFactory.CreateImage(bar, "Track", StylePalette.Ink, false);
                HudFactory.Stretch(track.rectTransform, 0f);
                Image inner = HudFactory.CreateImage(bar, "Inner", StylePalette.CreamPath, false);
                HudFactory.Stretch(inner.rectTransform, 2f);
                Image bark = HudFactory.CreateImage(inner.transform, "Fill", StylePalette.PistaTealSash, false);
                RectTransform fillRect = bark.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                _fills[i] = fillRect;

                _statuses[i] = MenuFactory.Label(panel, "Status" + i, font, StatusFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 22f), new Vector2(0f, y - 48f), string.Empty);
            }

            _setReward = MenuFactory.Label(panel, "SetReward", font, StatusFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 24f), new Vector2(0f, BlockTopPt - 3f * BlockStepPt + 2f), string.Empty);

            BackButton = MenuFactory.NeutralButton(
                panel, "BackButton", font, MenuStrings.Back, BackFontSize, new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 20f), Close);

            _root.SetActive(false);
        }

        public Button BackButton { get; }

        public bool Visible => _root.activeSelf;

        /// <summary>Shows the panel with the current missions.</summary>
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

        private void Refresh()
        {
            if (_meta == null)
            {
                return;
            }

            MissionService missions = _meta.Missions;
            _multiplier.text = MetaStrings.Format(MetaStrings.ScoreMultiplierFormat, missions.ScoreMultiplier);
            _setNumber.text = MetaStrings.Format(MetaStrings.SetFormat, missions.SetNumber);
            for (int i = 0; i < MissionService.SlotCount; i++)
            {
                MissionSlotData slot = missions.GetSlot(i);
                _descriptions[i].text = MetaStrings.Describe((MissionKind)slot.kind, slot.perRun, slot.target);
                if (slot.completed)
                {
                    _statuses[i].text = MetaStrings.MissionDone;
                }
                else
                {
                    _statuses[i].text = slot.progress.ToString(CultureInfo.InvariantCulture) + " / " +
                        slot.target.ToString(CultureInfo.InvariantCulture) + "   " +
                        MetaStrings.Format(MetaStrings.MissionRewardFormat, slot.reward);
                }

                float fraction = slot.target <= 0 ? 0f : Mathf.Clamp01((float)slot.progress / slot.target);
                _fills[i].anchorMax = new Vector2(slot.completed ? 1f : fraction, 1f);
            }

            _setReward.text = MetaStrings.Format(MetaStrings.SetRewardFormat, missions.CurrentSetReward);
        }
    }
}
