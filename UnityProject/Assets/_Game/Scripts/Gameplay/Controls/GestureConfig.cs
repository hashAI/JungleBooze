using System;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>Touch gesture thresholds in iOS points (spec 101 §3.3) plus keyboard/mouse mapping (§3.4).</summary>
    [Serializable]
    public sealed class GestureConfig
    {
        /// <summary>Steering: metres of lateral target per point of finger travel.</summary>
        public float DragSensitivity = 0.040f;

        /// <summary>Player setting multiplier (0.5–2.0).</summary>
        public float SensitivityMultiplier = 1f;

        /// <summary>No lateral output until the touch moved this far; then the full travel is applied, pt.</summary>
        public float TouchDeadZone = 4f;

        /// <summary>Samples within this angle of vertical contribute no lateral delta, degrees.</summary>
        public float VerticalIntentAngle = 30f;

        /// <summary>Vertical swipe threshold, pt …</summary>
        public float SwipeDistance = 24f;

        /// <summary>… reached within this window, s.</summary>
        public float SwipeWindow = 0.12f;

        /// <summary>Swipe direction within this angle of vertical, degrees.</summary>
        public float SwipeAngleTolerance = 35f;

        /// <summary>Same-direction re-fire on one touch needs a fresh swipe distance after this, s.</summary>
        public float SwipeRearmTime = 0.18f;

        /// <summary>Quick flick: touch lifetime ≤ this, s …</summary>
        public float FlickMaxDuration = 0.22f;

        /// <summary>… and |Δx| ≥ this, pt …</summary>
        public float FlickMinDistance = 30f;

        /// <summary>… within this angle of horizontal, degrees.</summary>
        public float FlickAngleTolerance = 35f;

        /// <summary>"Flick to dodge on release" setting.</summary>
        public bool ReleaseFlickEnabled = true;

        /// <summary>Horizontal release speed for a dodge after a longer drag, pt/s.</summary>
        public float ReleaseFlickSpeed = 1200f;

        /// <summary>Release speed is measured over this last part of the touch, s.</summary>
        public float ReleaseFlickWindow = 0.06f;

        public int MaxTrackedTouches = 2;

        public int CommandQueueSize = 16;

        /// <summary>A/D or arrows move the lateral target at this speed, m/s.</summary>
        public float KeyboardLateralSpeed = 10f;

        /// <summary>Editor mouse: screen pixels per emulated touch point.</summary>
        public float EditorPixelsPerPoint = 2f;

        public GestureConfig Clone()
        {
            return (GestureConfig)MemberwiseClone();
        }
    }
}
