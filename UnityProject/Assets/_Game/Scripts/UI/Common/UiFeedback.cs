using System;

namespace JungleBooze.UI.Common
{
    /// <summary>Hook for button feedback (light haptic + click sound); the App wires it (respects the toggles).</summary>
    public static class UiFeedback
    {
        public static Action Tap;

        public static void OnTap()
        {
            Tap?.Invoke();
        }
    }
}
