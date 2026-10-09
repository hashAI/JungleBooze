namespace JungleBooze.UI.Common
{
    /// <summary>
    /// "EXPEDITION COMPLETE" layout (plain C#, EditMode-tested on every device): one column in portrait; two columns
    /// in landscape (stats left, next goal + RUN AGAIN right) so nothing clips the top of a 390-unit-high screen
    /// (the old landscape bug). Row heights grow with the text scale; the panel is clamped into the safe area.
    /// </summary>
    public readonly struct ResultsLayout
    {
        public const float Margin = 10f;
        public const float Pad = 18f;

        public readonly bool TwoColumns;
        public readonly float PanelWidth;
        public readonly float PanelHeight;
        public readonly float TitleHeight;
        public readonly float RecordHeight;
        public readonly float StatRowHeight;
        public readonly float BigRowHeight;
        public readonly float DiscoveryHeight;
        public readonly float ObjectiveHeight;
        public readonly float ButtonHeight;
        public readonly float SmallButtonHeight;
        public readonly float Spacing;
        public readonly float LeftColumnHeight;
        public readonly float RightColumnHeight;
        public readonly int StatRows;

        private ResultsLayout(bool two, float pw, float ph, float title, float record, float row, float big, float disc, float obj, float button, float small, float spacing, float left, float right, int rows)
        {
            StatRows = rows;
            TwoColumns = two;
            PanelWidth = pw;
            PanelHeight = ph;
            TitleHeight = title;
            RecordHeight = record;
            StatRowHeight = row;
            BigRowHeight = big;
            DiscoveryHeight = disc;
            ObjectiveHeight = obj;
            ButtonHeight = button;
            SmallButtonHeight = small;
            Spacing = spacing;
            LeftColumnHeight = left;
            RightColumnHeight = right;
        }

        /// <summary>Content fits the panel (no clipping) in both columns.</summary>
        public bool Fits => LeftColumnHeight <= PanelHeight + 0.01f && RightColumnHeight <= PanelHeight + 0.01f;

        public float ColumnWidth => TwoColumns ? (PanelWidth - (3f * Pad)) * 0.5f : PanelWidth - (2f * Pad);

        public static ResultsLayout Compute(in ScreenFrame frame, float textScale)
        {
            float s = textScale < 1f ? 1f : textScale;
            bool two = frame.Landscape;
            float availW = frame.SafeWidth - (2f * Margin);
            float availH = frame.SafeHeight - (2f * Margin);
            float pw = two ? Min(availW, 760f) : Min(availW, 400f);
            float spacing = two ? 4f : 8f;
            float title = 30f * s;
            float record = 26f * s;
            float big = 40f * s;
            float row = 28f * s;
            float disc = 48f * s;
            float obj = 92f * s;
            float button = 60f * (s > 1.15f ? 1.15f : s);
            float small = 44f;

            // Header (title + record) spans the panel; then the columns.
            float header = Pad + title + record + spacing;
            // Portrait: best, coins, bonus, crystals. Landscape: best, coins | crystals, bonus.
            int rows = two ? 3 : 4;
            float stats = big + (rows * row) + disc + ((rows + 1) * spacing);
            float goal = obj + spacing + button + spacing + small;
            float left;
            float right;
            if (two)
            {
                left = header + stats + Pad;
                right = header + goal + Pad;
            }
            else
            {
                left = header + stats + spacing + goal + Pad;
                right = 0f;
            }

            float need = left > right ? left : right;
            float ph = Min(availH, need);
            if (need > availH)
            {
                // Compress spacing first (never the text): recompute with tight spacing.
                float tight = 2f;
                header = (Pad * 0.6f) + title + record + tight;
                stats = big + (rows * row) + disc + ((rows + 1) * tight);
                goal = obj + tight + button + tight + small;
                left = two ? header + stats + (Pad * 0.6f) : header + stats + tight + goal + (Pad * 0.6f);
                right = two ? header + goal + (Pad * 0.6f) : 0f;
                spacing = tight;
                ph = availH;
            }

            return new ResultsLayout(two, pw, ph, title, record, row, big, disc, obj, button, small, spacing, left, right, rows);
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }
    }
}
