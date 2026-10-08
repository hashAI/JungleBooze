using UnityEngine;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Pixels per iOS point. Unity has no UIScreen.scale API, so on phones it is derived from the screen density:
    /// every iPhone at ≥ 380 ppi is a @3x device and every lower-density iPhone is @2x. Editor and desktop use the
    /// configured mouse scale.
    /// </summary>
    public static class ScreenPointScale
    {
        /// <summary>Density threshold between @2x (326 ppi) and @3x (401–476 ppi) iPhones.</summary>
        public const float Retina3xMinDpi = 380f;

        public static float PixelsPerPoint(float desktopPixelsPerPoint)
        {
            if (!Application.isMobilePlatform)
            {
                return desktopPixelsPerPoint > 0f ? desktopPixelsPerPoint : 1f;
            }

            float dpi = Screen.dpi;
            if (dpi <= 0f)
            {
                return 2f;
            }

            return dpi >= Retina3xMinDpi ? 3f : 2f;
        }
    }
}
