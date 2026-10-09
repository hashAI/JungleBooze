using JungleBooze.Core.Feedback;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace JungleBooze.Services.Haptics
{
    /// <summary>
    /// iOS impact haptics through UIImpactFeedbackGenerator (tiny plugin <c>Assets/Plugins/iOS/JBHaptics.mm</c>;
    /// Unity has no built-in light/medium impact, only the long <c>Handheld.Vibrate</c>). Generators are created
    /// once and prepared, so calls are cheap. Everywhere else <see cref="Create"/> returns <see cref="NullHaptics"/>.
    /// </summary>
    public sealed class IosHaptics : IHaptics
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void JBHaptics_Impact(int style);

        [DllImport("__Internal")]
        private static extern int JBHaptics_Supported();
#endif

        private IosHaptics()
        {
        }

        public bool Supported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return JBHaptics_Supported() != 0;
#else
                return false;
#endif
            }
        }

        public static IHaptics Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosHaptics();
#else
            return new NullHaptics();
#endif
        }

        public void Impact(HapticImpact strength)
        {
#if UNITY_IOS && !UNITY_EDITOR
            JBHaptics_Impact((int)strength);
#endif
        }
    }
}
