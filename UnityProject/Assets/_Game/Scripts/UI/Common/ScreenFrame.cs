using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// The canvas in layout units: the short screen side is always <see cref="ShortSide"/> units (390 = 1 pt on an
    /// iPhone 12–14), so layouts are the same on every phone, in both orientations. Insets are the safe area.
    /// </summary>
    public readonly struct ScreenFrame
    {
        public const float ShortSide = 390f;

        public readonly float Width;
        public readonly float Height;
        public readonly float Left;
        public readonly float Right;
        public readonly float Top;
        public readonly float Bottom;

        public ScreenFrame(float width, float height, float left, float right, float top, float bottom)
        {
            Width = width;
            Height = height;
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public bool Landscape => Width >= Height;

        public float SafeWidth => Width - Left - Right;

        public float SafeHeight => Height - Top - Bottom;

        /// <summary>Units per pixel for a screen.</summary>
        public static float UnitsPerPixel(int widthPx, int heightPx)
        {
            int shortPx = widthPx < heightPx ? widthPx : heightPx;
            return shortPx > 0 ? ShortSide / shortPx : 1f;
        }

        public static ScreenFrame FromPixels(int widthPx, int heightPx, Rect safePx)
        {
            float u = UnitsPerPixel(widthPx, heightPx);
            return new ScreenFrame(
                widthPx * u,
                heightPx * u,
                safePx.xMin * u,
                (widthPx - safePx.xMax) * u,
                (heightPx - safePx.yMax) * u,
                safePx.yMin * u);
        }
    }
}
