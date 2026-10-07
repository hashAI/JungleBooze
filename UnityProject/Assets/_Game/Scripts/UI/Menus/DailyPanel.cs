using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Meta;
using JungleBooze.UI.Hud;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// Daily reward and daily challenge (GDD 13.3; placeholder look): the 7-day calendar with each day's reward,
    /// a Claim button, and today's challenge. Claimed, today and next days are marked in words and with a gold
    /// highlight, so state never relies on color alone. The rewarded-ad "double it" option is not built yet.
    /// </summary>
    public sealed class DailyPanel
    {
        private const float WidthPt = 330f;
        private const float HeightPt = 580f;
        private const float ContentWidthPt = 290f;
        private const float RowsTopPt = -62f;
        private const float RowStepPt = 32f;
        private const int TitleFontSize = 34;
        private const int RowFontSize = 18;
        private const int ClaimFontSize = 28;
        private const int InfoFontSize = 16;
        private const int HeaderFontSize = 20;
        private const int BackFontSize = 24;

        private readonly MetaProgress _meta;
        private readonly UnityAction _onClosed;
        private readonly UnityAction _onClaimed;
        private readonly GameObject _root;
        private readonly Image[] _rowBackgrounds = new Image[MetaConfig.CalendarDays];
        private readonly Text[] _dayLabels = new Text[MetaConfig.CalendarDays];
        private readonly Text[] _rewardLabels = new Text[MetaConfig.CalendarDays];
        private readonly Text _claimInfo;
        private readonly Text _challengeText;
        private readonly Text _challengeStatus;
        private readonly Text _claimLabel;

        public DailyPanel(Transform safeArea, Font font, MetaProgress meta, UnityAction onClosed, UnityAction onClaimed)
        {
            _meta = meta;
            _onClosed = onClosed;
            _onClaimed = onClaimed;

            Image fill = HudFactory.CreatePanel(
                safeArea, "DailyPanel", StylePalette.Parchment, new Vector2(0.5f, 0.5f), new Vector2(WidthPt, HeightPt), Vector2.zero);
            _root = fill.transform.parent.gameObject;
            Transform panel = fill.transform;
            Vector2 top = new Vector2(0.5f, 1f);
            Color highlight = new Color(StylePalette.CoinGold.r, StylePalette.CoinGold.g, StylePalette.CoinGold.b, 0.6f);
            Color none = new Color(0f, 0f, 0f, 0f);

            MenuFactory.Label(panel, "Title", font, TitleFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 50f), new Vector2(0f, -8f), MetaStrings.DailyTitle);

            for (int i = 0; i < MetaConfig.CalendarDays; i++)
            {
                float y = RowsTopPt - i * RowStepPt;
                Image background = HudFactory.CreateImage(panel, "RowBackground" + i, none, false);
                HudFactory.Place(background.rectTransform, top, new Vector2(ContentWidthPt + 10f, RowStepPt - 2f), new Vector2(0f, y));
                _rowBackgrounds[i] = background;
                _dayLabels[i] = MenuFactory.Label(panel, "Day" + i, font, RowFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, RowStepPt - 2f), new Vector2(0f, y), string.Empty);
                _rewardLabels[i] = MenuFactory.Label(panel, "Reward" + i, font, RowFontSize, TextAnchor.MiddleRight, top, new Vector2(ContentWidthPt, RowStepPt - 2f), new Vector2(0f, y), string.Empty);
            }

            _highlight = highlight;
            _none = none;

            float claimY = RowsTopPt - MetaConfig.CalendarDays * RowStepPt - 10f;
            ClaimButton = MenuFactory.PrimaryButton(
                panel, "ClaimButton", font, MetaStrings.Claim, ClaimFontSize, top, new Vector2(ContentWidthPt - 30f, 64f), new Vector2(0f, claimY), Claim);
            _claimLabel = ClaimButton.GetComponentInChildren<Text>();
            _claimInfo = MenuFactory.Label(panel, "ClaimInfo", font, InfoFontSize, TextAnchor.MiddleCenter, top, new Vector2(ContentWidthPt, 22f), new Vector2(0f, claimY - 70f), string.Empty);

            float challengeY = claimY - 104f;
            MenuFactory.Label(panel, "ChallengeTitle", font, HeaderFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 26f), new Vector2(0f, challengeY), MetaStrings.ChallengeTitle);
            _challengeText = MenuFactory.Label(panel, "ChallengeText", font, InfoFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 22f), new Vector2(0f, challengeY - 28f), string.Empty);
            _challengeStatus = MenuFactory.Label(panel, "ChallengeStatus", font, InfoFontSize, TextAnchor.MiddleLeft, top, new Vector2(ContentWidthPt, 22f), new Vector2(0f, challengeY - 50f), string.Empty);

            BackButton = MenuFactory.NeutralButton(
                panel, "BackButton", font, MenuStrings.Back, BackFontSize, new Vector2(0.5f, 0f), new Vector2(220f, 60f), new Vector2(0f, 20f), Close);

            _root.SetActive(false);
        }

        private readonly Color _highlight;
        private readonly Color _none;

        public Button ClaimButton { get; }

        public Button BackButton { get; }

        public bool Visible => _root.activeSelf;

        /// <summary>Shows the panel with the current calendar.</summary>
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

        private void Claim()
        {
            if (_meta == null || !_meta.Daily.TryClaim(out DailyReward _))
            {
                return;
            }

            _meta.Save.SaveIfDirty();
            Refresh();
            _onClaimed?.Invoke();
        }

        private void Refresh()
        {
            if (_meta == null)
            {
                return;
            }

            DailyRewardService daily = _meta.Daily;
            bool canClaim = daily.CanClaim;
            int next = daily.NextDayIndex;
            for (int i = 0; i < MetaConfig.CalendarDays; i++)
            {
                // The calendar pauses on a missed day, so "claimed" means earlier in the current week.
                string state;
                if (i < next)
                {
                    state = MetaStrings.DayClaimed;
                }
                else if (i == next)
                {
                    state = canClaim ? MetaStrings.DayToday : MetaStrings.DayNext;
                }
                else
                {
                    state = null;
                }

                string day = MetaStrings.Format(MetaStrings.DayFormat, i + 1);
                _dayLabels[i].text = state == null ? day : day + " - " + state;
                _rewardLabels[i].text = MetaStrings.Describe(daily.RewardFor(i));
                _rowBackgrounds[i].color = i == next ? _highlight : _none;
            }

            ClaimButton.interactable = canClaim;
            _claimLabel.text = MetaStrings.Claim;
            _claimInfo.text = canClaim ? string.Empty : MetaStrings.ComeBackTomorrow;

            DailyChallenge challenge = _meta.Missions.GetDailyChallenge();
            _challengeText.text = MetaStrings.Describe(challenge.Kind, true, challenge.Target);
            _challengeStatus.text = challenge.Done
                ? MetaStrings.ChallengeDone
                : MetaStrings.ChallengeOpen + "   " + MetaStrings.Format(MetaStrings.ChallengeRewardFormat, challenge.Reward);
        }
    }
}
