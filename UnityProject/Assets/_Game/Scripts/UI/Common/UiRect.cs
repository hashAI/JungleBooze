namespace JungleBooze.UI.Common
{
    /// <summary>A layout rectangle in safe-area units, measured from the safe area's top-left corner (y down).</summary>
    public readonly struct UiRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public UiRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;

        public float Bottom => Y + H;

        public bool IsEmpty => W <= 0f || H <= 0f;

        public bool Overlaps(UiRect o)
        {
            return !IsEmpty && !o.IsEmpty && X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
        }

        public bool Inside(float width, float height)
        {
            return X >= -0.01f && Y >= -0.01f && Right <= width + 0.01f && Bottom <= height + 0.01f;
        }

        /// <summary>Mirrored left↔right inside a width (left-handed layout).</summary>
        public UiRect Mirror(float width)
        {
            return new UiRect(width - X - W, Y, W, H);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0") + "," + Y.ToString("0") + " " + W.ToString("0") + "x" + H.ToString("0") + ")";
        }
    }
}
