using UnityEngine;

namespace JungleBooze.UI.Common
{
    /// <summary>
    /// A supported iPhone screen: native pixels (portrait), point scale and the safe-area insets iOS reports with
    /// the status bar hidden (points). Used by layout tests and the screenshot tool to check every screen at every
    /// size. Floor device: iPhone 12 (owner decision 2026-10-09); the SE 3 (16:9, no notch) is the narrowest shape.
    /// </summary>
    public readonly struct DeviceProfile
    {
        public readonly string Name;
        public readonly int WidthPx;
        public readonly int HeightPx;
        public readonly float Scale;
        public readonly float PortraitTop;
        public readonly float PortraitBottom;
        public readonly float LandscapeSide;
        public readonly float LandscapeBottom;

        public DeviceProfile(string name, int widthPx, int heightPx, float scale, float portraitTop, float portraitBottom, float landscapeSide, float landscapeBottom)
        {
            Name = name;
            WidthPx = widthPx;
            HeightPx = heightPx;
            Scale = scale;
            PortraitTop = portraitTop;
            PortraitBottom = portraitBottom;
            LandscapeSide = landscapeSide;
            LandscapeBottom = landscapeBottom;
        }

        public static readonly DeviceProfile[] All =
        {
            new DeviceProfile("iPhone SE 3", 750, 1334, 2f, 0f, 0f, 0f, 0f),
            new DeviceProfile("iPhone 12 mini", 1080, 2340, 3f, 50f, 34f, 50f, 21f),
            new DeviceProfile("iPhone 12-14", 1170, 2532, 3f, 47f, 34f, 47f, 21f),
            new DeviceProfile("iPhone 14 Plus", 1284, 2778, 3f, 47f, 34f, 47f, 21f),
            new DeviceProfile("iPhone 15 Pro", 1179, 2556, 3f, 59f, 34f, 59f, 21f),
            new DeviceProfile("iPhone 15 Pro Max", 1290, 2796, 3f, 59f, 34f, 59f, 21f),
            new DeviceProfile("iPhone 16 Pro", 1206, 2622, 3f, 62f, 34f, 62f, 21f),
            new DeviceProfile("iPhone 16 Pro Max", 1320, 2868, 3f, 62f, 34f, 62f, 21f),
        };

        /// <summary>Screen size in pixels for an orientation.</summary>
        public Vector2Int Pixels(bool landscape)
        {
            return landscape ? new Vector2Int(HeightPx, WidthPx) : new Vector2Int(WidthPx, HeightPx);
        }

        /// <summary>Safe area in pixels (Unity convention: origin bottom-left).</summary>
        public Rect SafeAreaPixels(bool landscape)
        {
            Vector2Int px = Pixels(landscape);
            if (landscape)
            {
                float side = LandscapeSide * Scale;
                float bottom = LandscapeBottom * Scale;
                return new Rect(side, bottom, px.x - (2f * side), px.y - bottom);
            }

            float top = PortraitTop * Scale;
            float bot = PortraitBottom * Scale;
            return new Rect(0f, bot, px.x, px.y - top - bot);
        }

        public ScreenFrame Frame(bool landscape)
        {
            Vector2Int px = Pixels(landscape);
            return ScreenFrame.FromPixels(px.x, px.y, SafeAreaPixels(landscape));
        }
    }
}
