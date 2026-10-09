#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace JungleBooze.App.Perf
{
    /// <summary>
    /// Thermal state, low-power mode and memory footprint from iOS (Plugins/iOS/JBPerfNative.mm). Returns -1 / false
    /// where the platform has no reading (editor, Mac). Each call is a cheap system query with no allocation.
    /// </summary>
    public static class DeviceHealth
    {
        private static readonly string[] ThermalNames = { "nominal", "fair", "serious", "critical" };

        /// <summary>0 nominal, 1 fair, 2 serious (iOS starts throttling), 3 critical; -1 unknown.</summary>
        public static int ThermalState
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return JBPerf_ThermalState();
#else
                return -1;
#endif
            }
        }

        public static bool LowPowerMode
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return JBPerf_LowPowerMode() != 0;
#else
                return false;
#endif
            }
        }

        /// <summary>Physical footprint in bytes (the figure iOS terminates apps on); -1 unknown.</summary>
        public static long FootprintBytes
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return JBPerf_FootprintBytes();
#else
                return -1;
#endif
            }
        }

        /// <summary>Bytes the app can still allocate before termination; -1 unknown.</summary>
        public static long AvailableBytes
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return JBPerf_AvailableBytes();
#else
                return -1;
#endif
            }
        }

        public static string ThermalName(int state)
        {
            return state >= 0 && state < ThermalNames.Length ? ThermalNames[state] : "n/a";
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int JBPerf_ThermalState();

        [DllImport("__Internal")]
        private static extern int JBPerf_LowPowerMode();

        [DllImport("__Internal")]
        private static extern long JBPerf_FootprintBytes();

        [DllImport("__Internal")]
        private static extern long JBPerf_AvailableBytes();
#endif
    }
}
