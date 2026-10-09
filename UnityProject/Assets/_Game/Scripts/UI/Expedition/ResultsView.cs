using System.Text;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.UI.Common;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleBooze.UI.Expedition
{
    /// <summary>
    /// "EXPEDITION COMPLETE" (GDD §17, spec 103 §9.2): distance (big) and best, coins with bonus lines, crystals,
    /// discoveries with category counts, NEW RECORD, exactly one next goal (card → ability card), RUN AGAIN (primary,
    /// thumb reach) and Camp. Placement from <see cref="ResultsLayout"/>: two columns in landscape (nothing clips),
    /// one in portrait. Count-ups ≤ CountUpTime; any tap completes them.
    /// </summary>
    public sealed class ResultsView
    {
        private readonly UiFactory _f;
        private readonly ExpeditionContent _content;
        private readonly NumberText _metres;
        private readonly NumberText _numbers;
        private readonly Text _title;
        private readonly RectTransform _record;
        private readonly Image _recordGlow;
        private readonly RectTransform _distanceRow;
        private readonly Text _distanceValue;
        private readonly Text _best;
        private readonly RectTransform _coinsRow;
        private readonly Text _coinsValue;
        private readonly RectTransform _crystalsRow;
        private readonly Text _crystalsValue;
        private readonly Text _bonus;
        private readonly RectTransform _discRow;
        private readonly Text _discValue;
        private readonly Text _discLine;
        private readonly UiButton _objective;
        private readonly Text _objectiveHeader;
        private readonly Text _objectiveTitle;
        private readonly Text _objectiveDetail;
        private readonly Image _objectiveIcon;
        private readonly RectTransform _progressRoot;
        private readonly Image _progressFill;
        private readonly Text _tapToSkip;
        private RunResults _data;
        private float _countUp;
        private float _countUpTime = 1.2f;
        private int _shownDistance = -1;
        private int _shownCoins = -1;
        private int _shownCrystals = -1;
        private ResultsLayout _layout;

        public ResultsView(UiFactory f, Transform canvas, ExpeditionContent content, UnityAction onRunAgain, UnityAction onObjective, UnityAction onCamp)
        {
            _f = f;
            _content = content;
            _metres = new NumberText(5001, 100000, f.S("unit.metres"));
            _numbers = new NumberText(3001, 100000, null);
            Shell = new ScreenShell(f, canvas, "Results", new Vector2(400f, 720f), new Vector2(760f, 380f));
            Transform body = Shell.Body.transform;

            _title = f.Label(body, "Title", f.S("results.title"), FontKind.Display, 26, UiColors.Gold, TextAnchor.MiddleCenter);
            _record = UiFactory.Rect(body, "NewRecord");
            _recordGlow = f.Image(_record, "Glow", f.Theme.Glow, new Color(1f, 0.82f, 0.4f, 0.55f));
            _recordGlow.preserveAspect = false;
            UiFactory.Stretch(_recordGlow.rectTransform, -30f, -30f, -12f, -12f);
            Image recordIcon = f.Icon(_record, "Icon", f.Theme.Record, 26f);
            UiFactory.Anchor(recordIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 26f), Vector2.zero);
            Text recordText = f.Label(_record, "Text", f.S("results.newRecord"), FontKind.Heavy, 18, UiColors.Gold, TextAnchor.MiddleLeft, false, true, true);
            UiFactory.Stretch(recordText.rectTransform, 32f, 0f, 0f, 0f);

            _distanceRow = StatRow(body, "Distance", f.Theme.Distance, "results.distance", 34f, 30, out _distanceValue);
            _best = f.Label(body, "Best", string.Empty, FontKind.Body, 15, UiColors.Mist, TextAnchor.MiddleRight);
            _coinsRow = StatRow(body, "Coins", f.Theme.Coin, "results.coins", 26f, 21, out _coinsValue);
            _crystalsRow = StatRow(body, "Crystals", f.Theme.Crystal, "results.crystals", 26f, 21, out _crystalsValue);
            _bonus = f.Label(body, "Bonus", string.Empty, FontKind.Heavy, 14, UiColors.Turquoise, TextAnchor.MiddleRight);
            _discRow = StatRow(body, "Discoveries", f.Theme.Discovery, "results.discoveries", 26f, 21, out _discValue);
            _discRow.GetComponentInChildren<Text>().alignment = TextAnchor.UpperLeft;
            _discValue.alignment = TextAnchor.UpperRight;
            _discLine = f.Label(_discRow, "Line", string.Empty, FontKind.Body, 13, UiColors.Mist, TextAnchor.LowerLeft);
            UiFactory.Stretch(_discLine.rectTransform, 34f, 0f, 22f, 0f);

            // Next goal: a parchment card (tap → ability card).
            _objective = f.Button(body, "Objective", ButtonStyle.Ghost, null, null, 0, new Vector2(300f, 78f), onObjective);
            _objective.Background.sprite = f.Theme.Card;
            _objectiveIcon = f.Icon(_objective.Root, "Icon", f.Theme.Abilities, 44f);
            UiFactory.Anchor(_objectiveIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 44f), new Vector2(10f, 0f));
            _objectiveHeader = f.Label(_objective.Root, "Header", f.S("results.next"), FontKind.Heavy, 13, UiColors.Emerald, TextAnchor.UpperLeft);
            UiFactory.Stretch(_objectiveHeader.rectTransform, 62f, 34f, 6f, 0f);
            _objectiveHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
            _objectiveHeader.rectTransform.sizeDelta = new Vector2(_objectiveHeader.rectTransform.sizeDelta.x, 18f);
            _objectiveTitle = f.Label(_objective.Root, "Title", string.Empty, FontKind.Heavy, 17, UiColors.Ink, TextAnchor.UpperLeft);
            _objectiveDetail = f.Label(_objective.Root, "Detail", string.Empty, FontKind.Body, 13, UiColors.InkSoft, TextAnchor.UpperLeft);
            Image chevron = f.Icon(_objective.Root, "Chevron", f.Theme.Next, 22f);
            chevron.color = UiColors.InkSoft;
            UiFactory.Anchor(chevron.rectTransform, new Vector2(1f, 0.5f), new Vector2(22f, 22f), new Vector2(-8f, 0f));
            _progressRoot = UiFactory.Rect(_objective.Root, "Progress");
            Image track = f.Image(_progressRoot, "Track", f.Theme.Track, Color.white);
            UiFactory.Stretch(track.rectTransform);
            _progressFill = f.Image(_progressRoot, "Fill", f.Theme.TrackFill, Color.white);
            _progressFill.rectTransform.anchorMin = Vector2.zero;
            _progressFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            _progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _progressFill.rectTransform.offsetMin = Vector2.zero;
            _progressFill.rectTransform.offsetMax = Vector2.zero;

            RunAgain = f.Button(body, "RunAgain", ButtonStyle.Primary, f.S("results.runAgain"), f.Theme.Play, 24, new Vector2(280f, 62f), onRunAgain);
            Camp = f.Button(body, "Camp", ButtonStyle.Ghost, f.S("results.home"), f.Theme.Camp, 16, new Vector2(150f, 44f), onCamp);
            _tapToSkip = f.Label(Shell.Safe, "TapToSkip", f.S("results.tapToSkip"), FontKind.Body, 14, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerCenter, false, true, true);
            UiFactory.Anchor(_tapToSkip.rectTransform, new Vector2(0.5f, 0f), new Vector2(300f, 24f), new Vector2(0f, 2f));
        }

        public ScreenShell Shell { get; }

        public UiButton RunAgain { get; }

        public UiButton Camp { get; }

        public bool Visible => Shell.Visible;

        public bool CountingUp { get; private set; }

        public bool RecordVisible => _record.gameObject.activeSelf;

        public string ObjectiveText { get; private set; } = string.Empty;

        public ResultsLayout CurrentLayout => _layout;

        public void Layout(in ScreenFrame frame, float scale)
        {
            _layout = ResultsLayout.Compute(frame, scale);
            ResultsLayout l = _layout;
            Shell.PanelRoot.sizeDelta = new Vector2(l.PanelWidth, l.PanelHeight);
            float pad = ResultsLayout.Pad * (l.Spacing < 4f ? 0.6f : 1f);
            float inner = l.PanelWidth - (2f * ResultsLayout.Pad);
            float y = pad;
            UiFactory.Place(_title.rectTransform, new UiRect(ResultsLayout.Pad, y, inner, l.TitleHeight));
            y += l.TitleHeight;
            float recordW = 190f * Mathf.Min(scale, 1.3f);
            UiFactory.Place(_record, new UiRect((l.PanelWidth - recordW) * 0.5f, y, recordW, l.RecordHeight));
            y += l.RecordHeight + l.Spacing;

            float colW = l.ColumnWidth;
            float x = ResultsLayout.Pad;
            float sy = y;
            UiFactory.Place(_distanceRow, new UiRect(x, sy, colW, l.BigRowHeight));
            sy += l.BigRowHeight + l.Spacing;
            UiFactory.Place(_best.rectTransform, new UiRect(x, sy, colW, l.StatRowHeight));
            sy += l.StatRowHeight + l.Spacing;
            if (l.TwoColumns)
            {
                float half = (colW - 12f) * 0.5f;
                UiFactory.Place(_coinsRow, new UiRect(x, sy, half, l.StatRowHeight));
                UiFactory.Place(_crystalsRow, new UiRect(x + half + 12f, sy, half, l.StatRowHeight));
                sy += l.StatRowHeight + l.Spacing;
                UiFactory.Place(_bonus.rectTransform, new UiRect(x, sy, colW, l.StatRowHeight));
                sy += l.StatRowHeight + l.Spacing;
            }
            else
            {
                UiFactory.Place(_coinsRow, new UiRect(x, sy, colW, l.StatRowHeight));
                sy += l.StatRowHeight + l.Spacing;
                UiFactory.Place(_bonus.rectTransform, new UiRect(x, sy, colW, l.StatRowHeight));
                sy += l.StatRowHeight + l.Spacing;
                UiFactory.Place(_crystalsRow, new UiRect(x, sy, colW, l.StatRowHeight));
                sy += l.StatRowHeight + l.Spacing;
            }

            UiFactory.Place(_discRow, new UiRect(x, sy, colW, l.DiscoveryHeight));
            sy += l.DiscoveryHeight + l.Spacing;

            float gx = l.TwoColumns ? x + colW + ResultsLayout.Pad : x;
            float gy = l.TwoColumns ? y : sy;
            if (l.TwoColumns)
            {
                // Bottom-align the goal column so RUN AGAIN sits low (thumb reach).
                float goalH = l.ObjectiveHeight + l.Spacing + l.ButtonHeight + l.Spacing + l.SmallButtonHeight;
                gy = Mathf.Max(y, l.PanelHeight - pad - goalH);
            }

            UiFactory.Place(_objective.Root, new UiRect(gx, gy, colW, l.ObjectiveHeight));
            LayoutObjective(l.ObjectiveHeight, scale);
            gy += l.ObjectiveHeight + l.Spacing;
            float runW = Mathf.Min(colW, 300f);
            UiFactory.Place(RunAgain.Root, new UiRect(gx + ((colW - runW) * 0.5f), gy, runW, l.ButtonHeight));
            gy += l.ButtonHeight + l.Spacing;
            float campW = 160f;
            UiFactory.Place(Camp.Root, new UiRect(gx + ((colW - campW) * 0.5f), gy, campW, l.SmallButtonHeight));
        }

        public void Show(RunResults results, float countUpTime, bool reducedMotion)
        {
            _data = results;
            _countUpTime = Mathf.Max(0.01f, countUpTime);
            _countUp = 0f;
            CountingUp = true;
            _shownDistance = _shownCoins = _shownCrystals = -1;
            _record.gameObject.SetActive(results.NewRecord);
            StringTable s = _f.Strings;
            int best = (int)results.Best;
            _best.text = results.FirstExpedition ? s.Format("results.bestFirst", _metres.Get(best)) : s.Format("results.best", _metres.Get(best));
            _bonus.text = BonusLine(s, results);
            _bonus.gameObject.SetActive(_bonus.text.Length > 0);
            int newCount = results.NewDiscoveryNames.Count;
            _discValue.text = newCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var b = new StringBuilder();
            for (int i = 0; i < newCount; i++)
            {
                b.Append(i > 0 ? ", " : string.Empty).Append(results.NewDiscoveryNames[i]);
            }

            if (newCount == 0)
            {
                b.Append(s.Get("results.discoveriesNone"));
            }

            if (!string.IsNullOrEmpty(results.CategoryCounts))
            {
                b.Append("  ·  ").Append(results.CategoryCounts);
            }

            _discLine.text = b.ToString();
            SetObjective(results.Objective);
            Shell.Show(true, reducedMotion);
            _tapToSkip.gameObject.SetActive(true);
            Refresh(0f);
        }

        public void SetObjective(NextObjective o)
        {
            bool show = o != null && o.Kind != ObjectiveKind.None;
            _objective.SetActive(show);
            if (!show)
            {
                ObjectiveText = string.Empty;
                return;
            }

            StringTable s = _f.Strings;
            _objectiveTitle.text = JungleBooze.UI.Expedition.ObjectiveText.Title(s, o, _content);
            _objectiveDetail.text = JungleBooze.UI.Expedition.ObjectiveText.Detail(s, o, _content);
            ObjectiveText = JungleBooze.UI.Expedition.ObjectiveText.Line(s, o, _content);
            bool progress = o.Target > 0 && (o.Kind == ObjectiveKind.AbilityReady || o.Kind == ObjectiveKind.AbilityProgress || o.Kind == ObjectiveKind.NearBest || o.Kind == ObjectiveKind.Secrets);
            _progressRoot.gameObject.SetActive(progress);
            if (progress)
            {
                float t = Mathf.Clamp01(o.Progress / (float)o.Target);
                _progressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.08f, t), 1f);
            }

            _objectiveIcon.sprite = o.Kind == ObjectiveKind.AbilityReady || o.Kind == ObjectiveKind.AbilityProgress ? _f.Theme.Abilities
                : o.Kind == ObjectiveKind.NearBest || o.Kind == ObjectiveKind.BeatBest ? _f.Theme.Record : _f.Theme.Discovery;
        }

        /// <summary>After LEARN: the card shows the learned line and RUN AGAIN is highlighted.</summary>
        public void SetObjectiveLearned(string text, bool highlightRunAgain)
        {
            _objectiveTitle.text = text;
            _objectiveDetail.text = string.Empty;
            _progressRoot.gameObject.SetActive(false);
            _objectiveIcon.sprite = _f.Theme.Check;
            ObjectiveText = text;
            RunAgain.Root.localScale = highlightRunAgain ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
        }

        public void Hide()
        {
            Shell.Show(false, true);
            CountingUp = false;
            RunAgain.Root.localScale = Vector3.one;
        }

        public void CompleteCountUp()
        {
            if (CountingUp)
            {
                CountingUp = false;
                Refresh(1f);
            }
        }

        public void Tick(float seconds)
        {
            if (!CountingUp || _data == null)
            {
                return;
            }

            _countUp += seconds;
            float t = Mathf.Clamp01(_countUp / _countUpTime);
            Refresh(t);
            if (t >= 1f)
            {
                CountingUp = false;
            }
        }

        /// <summary>All stats as plain text (debug log, tests).</summary>
        public string Summary()
        {
            return _distanceValue.text + " | " + _best.text + " | " + _coinsValue.text + " " + _bonus.text + " | " + _crystalsValue.text + " | " + _discValue.text + " " + _discLine.text;
        }

        public static string BonusLine(StringTable s, RunResults r)
        {
            var b = new StringBuilder();
            if (r.CleanLineCoins > 0)
            {
                b.Append(s.Format("results.bonus.cleanLine", NumberText.Group(r.CleanLineCoins)));
            }

            if (r.PerfectCoins > 0)
            {
                b.Append(b.Length > 0 ? "   " : string.Empty).Append(s.Format("results.bonus.perfect", NumberText.Group(r.PerfectCoins)));
            }

            if (r.DiscoveryCoins > 0)
            {
                b.Append(b.Length > 0 ? "   " : string.Empty).Append(s.Format("results.bonus.discovery", NumberText.Group(r.DiscoveryCoins)));
            }

            return b.ToString();
        }

        private void Refresh(float t)
        {
            float e = 1f - ((1f - t) * (1f - t));
            int d = (int)(_data.Distance * e);
            int c = (int)(_data.TotalCoins * e);
            int k = (int)(_data.Crystals * e);
            if (d != _shownDistance)
            {
                _shownDistance = d;
                _distanceValue.text = _metres.Get(d);
            }

            if (c != _shownCoins)
            {
                _shownCoins = c;
                _coinsValue.text = _numbers.Get(c);
            }

            if (k != _shownCrystals)
            {
                _shownCrystals = k;
                _crystalsValue.text = _numbers.Get(k);
            }

            if (t >= 1f)
            {
                _tapToSkip.gameObject.SetActive(false);
            }
        }

        private void LayoutObjective(float h, float scale)
        {
            float header = 18f * scale;
            float title = 24f * scale;
            UiFactory.Stretch(_objectiveHeader.rectTransform, 62f, 36f, 6f, h - 6f - header);
            UiFactory.Stretch(_objectiveTitle.rectTransform, 62f, 36f, 6f + header, h - 6f - header - title);
            bool bar = _progressRoot.gameObject.activeSelf;
            UiFactory.Stretch(_objectiveDetail.rectTransform, 62f, 36f, 6f + header + title, bar ? 16f : 6f);
            _progressRoot.anchorMin = new Vector2(0f, 0f);
            _progressRoot.anchorMax = new Vector2(1f, 0f);
            _progressRoot.pivot = new Vector2(0.5f, 0f);
            _progressRoot.offsetMin = new Vector2(62f, 7f);
            _progressRoot.offsetMax = new Vector2(-36f, 15f);
        }

        private RectTransform StatRow(Transform parent, string name, Sprite icon, string labelKey, float iconSize, int valueSize, out Text value)
        {
            RectTransform row = UiFactory.Rect(parent, name);
            Image i = _f.Icon(row, "Icon", icon, iconSize);
            i.rectTransform.anchorMin = new Vector2(0f, 1f);
            i.rectTransform.anchorMax = new Vector2(0f, 1f);
            i.rectTransform.pivot = new Vector2(0f, 1f);
            i.rectTransform.anchoredPosition = new Vector2(0f, -1f);
            Text label = _f.Label(row, "Label", _f.S(labelKey), FontKind.Body, 16, UiColors.Mist, TextAnchor.MiddleLeft);
            UiFactory.Stretch(label.rectTransform, iconSize + 8f, 0f, 0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.78f, 1f);
            value = _f.Label(row, "Value", string.Empty, FontKind.Heavy, valueSize, UiColors.Cream, TextAnchor.MiddleRight);
            UiFactory.Stretch(value.rectTransform);
            value.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            return row;
        }
    }
}
