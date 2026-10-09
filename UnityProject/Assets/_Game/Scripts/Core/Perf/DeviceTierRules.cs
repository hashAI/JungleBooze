using System;

namespace JungleBooze.Core.Perf
{
    /// <summary>
    /// Picks the <see cref="DeviceTier"/> from the device model identifier (for example <c>iPhone13,2</c> for an
    /// iPhone 12) and the system memory. Apple model identifiers carry a generation number that grows with every chip:
    /// iPhone13,x is the A14 family (iPhone 12), iPhone12,x the A13 family (iPhone 11, SE 2nd gen). Desktop, editor
    /// and simulator identifiers count as High. Plain C#; thresholds come from the quality-tier config.
    /// </summary>
    public static class DeviceTierRules
    {
        public const string IphonePrefix = "iPhone";
        public const string IpadPrefix = "iPad";

        /// <param name="deviceModel">SystemInfo.deviceModel.</param>
        /// <param name="systemMemoryMb">SystemInfo.systemMemorySize (MB); 0 or less = unknown (not used).</param>
        /// <param name="minHighIphoneGeneration">Lowest iPhone generation number for High (13 = iPhone 12, A14).</param>
        /// <param name="minHighIpadGeneration">Lowest iPad generation number for High (13 = iPad Air 4, A14).</param>
        /// <param name="minHighMemoryMb">Below this memory a device is Low even if the chip qualifies.</param>
        public static DeviceTier Classify(string deviceModel, int systemMemoryMb, int minHighIphoneGeneration, int minHighIpadGeneration, int minHighMemoryMb)
        {
            if (systemMemoryMb > 0 && systemMemoryMb < minHighMemoryMb)
            {
                return DeviceTier.Low;
            }

            if (TryParseGeneration(deviceModel, IphonePrefix, out int iphone))
            {
                return iphone >= minHighIphoneGeneration ? DeviceTier.High : DeviceTier.Low;
            }

            if (TryParseGeneration(deviceModel, IpadPrefix, out int ipad))
            {
                return ipad >= minHighIpadGeneration ? DeviceTier.High : DeviceTier.Low;
            }

            // Editor, Mac, simulator, unknown future names: High (the reviewed look).
            return DeviceTier.High;
        }

        /// <summary>Reads N from "&lt;prefix&gt;N,M". False if the text does not have that shape.</summary>
        public static bool TryParseGeneration(string deviceModel, string prefix, out int generation)
        {
            generation = 0;
            if (string.IsNullOrEmpty(deviceModel) || !deviceModel.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            int i = prefix.Length;
            int digits = 0;
            while (i < deviceModel.Length && deviceModel[i] >= '0' && deviceModel[i] <= '9' && digits < 4)
            {
                generation = generation * 10 + (deviceModel[i] - '0');
                i++;
                digits++;
            }

            return digits > 0 && i < deviceModel.Length && deviceModel[i] == ',';
        }

        /// <summary>
        /// Render scale that gives <paramref name="targetMegapixels"/> internal pixels on a screen of the given size,
        /// clamped to [min, max]. Same GPU load per pixel count on every device (ADR 0004 Decision 2).
        /// </summary>
        public static float RenderScaleFor(int screenWidth, int screenHeight, float targetMegapixels, float minScale, float maxScale)
        {
            double pixels = (double)screenWidth * screenHeight;
            if (pixels <= 0.0 || targetMegapixels <= 0f)
            {
                return maxScale;
            }

            double scale = Math.Sqrt(targetMegapixels * 1000000.0 / pixels);
            if (scale < minScale)
            {
                return minScale;
            }

            return scale > maxScale ? maxScale : (float)scale;
        }
    }
}
