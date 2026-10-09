namespace JungleBooze.UI.Common
{
    /// <summary>Output of <see cref="HudLayout.Compute"/> (safe-area units, top-left origin).</summary>
    public struct HudRects
    {
        public bool OneRow;
        public UiRect Distance;
        public UiRect Health;
        public UiRect Coins;
        public UiRect Crystals;
        public UiRect Pause;
        public UiRect Toast;
        public UiRect Hint;
    }
}
