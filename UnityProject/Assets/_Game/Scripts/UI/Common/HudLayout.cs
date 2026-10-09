namespace JungleBooze.UI.Common
{
    /// <summary>
    /// In-run HUD placement (plain C#, EditMode-tested on every <see cref="DeviceProfile"/>): distance, health,
    /// coins, crystals and pause in one top row when it fits, else two rows (portrait always: this fixes the old
    /// portrait overlap of distance and health). Right-handed puts pause top-right (thumb), left-handed mirrors the
    /// HUD. Sizes grow with the HUD text scale; chips are wide enough for the largest expected values
    /// (99,999 m, 99,999 coins, 999 crystals) so numbers never overflow.
    /// </summary>
    public static class HudLayout
    {
        public const float Margin = 12f;
        public const float Gap = 8f;
        public const float ChipHeight = 40f;
        public const float NumberFont = 22f;
        public const float IconSize = 26f;
        public const float PauseSize = 52f;
        public const float LeafSize = 28f;
        public const float LeafGap = 4f;

        /// <summary>Average advance of a digit/comma in Nunito Black, in ems (measured; slightly generous).</summary>
        public const float DigitEm = 0.62f;

        public static float ChipWidth(int chars, float scale)
        {
            return 12f + (IconSize * scale) + 6f + (chars * NumberFont * scale * DigitEm) + 10f;
        }

        public static HudRects Compute(in ScreenFrame frame, int maxHealth, bool leftHanded, float scale)
        {
            float w = frame.SafeWidth;
            float chipH = ChipHeight * scale;
            float pause = PauseSize > chipH ? PauseSize : chipH;
            float distW = ChipWidth(8, scale);   // "99,999 m"
            float coinW = ChipWidth(6, scale);   // "99,999"
            float crysW = ChipWidth(3, scale);   // "999"
            float leaf = LeafSize * scale;
            float healthW = (maxHealth * leaf) + ((maxHealth - 1) * LeafGap * scale) + (leaf * 0.9f); // + shield slot
            float rowW = Margin + distW + Gap + healthW + Gap + coinW + Gap + crysW + Gap + pause + Margin;
            bool oneRow = frame.Landscape && rowW <= w;

            var r = new HudRects { OneRow = oneRow };
            float y0 = Margin * 0.5f;
            r.Pause = new UiRect(w - Margin - pause, y0, pause, pause);
            float rowMid = y0 + (pause * 0.5f);
            r.Distance = new UiRect(Margin, rowMid - (chipH * 0.5f), distW, chipH);
            if (oneRow)
            {
                r.Crystals = new UiRect(r.Pause.X - Gap - crysW, r.Distance.Y, crysW, chipH);
                r.Coins = new UiRect(r.Crystals.X - Gap - coinW, r.Distance.Y, coinW, chipH);
                // Centred on screen when there's room, else centred in the free gap; never closer than Gap to a neighbour.
                float freeStart = r.Distance.Right + Gap;
                float freeEnd = r.Coins.X - Gap;
                float healthX = (w - healthW) * 0.5f;
                if (healthX < freeStart || healthX + healthW > freeEnd)
                {
                    healthX = freeStart + ((freeEnd - freeStart - healthW) * 0.5f);
                }

                r.Health = new UiRect(healthX, rowMid - (leaf * 0.5f), healthW, leaf);
            }
            else
            {
                float row2 = (y0 + pause) + Gap;
                r.Health = new UiRect(Margin, row2 + ((chipH - leaf) * 0.5f), healthW, leaf);
                r.Crystals = new UiRect(w - Margin - crysW, row2, crysW, chipH);
                r.Coins = new UiRect(r.Crystals.X - Gap - coinW, row2, coinW, chipH);
                if (r.Coins.X < r.Health.Right + Gap)
                {
                    // Very narrow + large text: wallet gets its own third row (right aligned).
                    float row3 = row2 + chipH + Gap;
                    r.Crystals = new UiRect(w - Margin - crysW, row3, crysW, chipH);
                    r.Coins = new UiRect(r.Crystals.X - Gap - coinW, row3, coinW, chipH);
                }
            }

            float bottomOfHud = Max(Max(r.Pause.Bottom, r.Health.Bottom), Max(r.Coins.Bottom, r.Distance.Bottom));
            float toastW = Min(w - (2f * Margin), 380f * Min(scale, 1.15f));
            float toastH = 66f * Min(scale, 1.15f);
            r.Toast = new UiRect((w - toastW) * 0.5f, bottomOfHud + Gap, toastW, toastH);
            float hintW = Min(w - (2f * Margin), 420f);
            float hintH = 116f * Min(scale, 1.15f);
            float hintY = Min(frame.SafeHeight * 0.58f, frame.SafeHeight - hintH - Gap);
            r.Hint = new UiRect((w - hintW) * 0.5f, hintY, hintW, hintH);
            if (leftHanded)
            {
                r.Pause = r.Pause.Mirror(w);
                r.Distance = r.Distance.Mirror(w);
                r.Health = r.Health.Mirror(w);
                r.Coins = r.Coins.Mirror(w);
                r.Crystals = r.Crystals.Mirror(w);
            }

            return r;
        }

        private static float Min(float a, float b)
        {
            return a < b ? a : b;
        }

        private static float Max(float a, float b)
        {
            return a > b ? a : b;
        }
    }
}
