using System.Collections.Generic;
using JungleBooze.Gameplay.Controls;
using UnityEngine;

namespace JungleBooze.Gameplay.Config
{
    /// <summary>
    /// Designer-facing touch tuning (spec 001 section 3.3), saved as
    /// <c>Assets/_Game/Config/Resources/InputTuning.asset</c>. Defaults are the spec start values.
    /// </summary>
    [CreateAssetMenu(fileName = "InputTuning", menuName = "JungleBooze/Config/Input Tuning")]
    public sealed class InputConfigAsset : ScriptableObject
    {
        [Header("Swipe")]
        [SerializeField] private float _swipeThresholdPt = 28f;
        [SerializeField] private float _swipeWindowMs = 250f;
        [SerializeField] private float _swipeRearmDistancePt = 28f;
        [SerializeField] private bool _diagonalTieGoesHorizontal = true;

        [Header("Tap")]
        [SerializeField] private float _tapMaxMovementPt = 12f;
        [SerializeField] private float _tapMaxDurationMs = 200f;
        [SerializeField] private float _doubleTapWindowMs = 300f;

        [Header("Queue")]
        [SerializeField] private int _gestureQueueCapacity = 8;

        public InputConfig ToConfig()
        {
            return new InputConfig
            {
                SwipeThresholdPt = _swipeThresholdPt,
                SwipeWindowMs = _swipeWindowMs,
                SwipeRearmDistancePt = _swipeRearmDistancePt,
                DiagonalTieGoesHorizontal = _diagonalTieGoesHorizontal,
                TapMaxMovementPt = _tapMaxMovementPt,
                TapMaxDurationMs = _tapMaxDurationMs,
                DoubleTapWindowMs = _doubleTapWindowMs,
                GestureQueueCapacity = _gestureQueueCapacity,
            };
        }

        public bool Validate(List<string> errors)
        {
            return ToConfig().Validate(errors);
        }

        private void OnValidate()
        {
            var errors = new List<string>();
            if (!Validate(errors))
            {
                Debug.LogWarning(name + " (InputConfigAsset) is out of range: " + string.Join(" ", errors), this);
            }
        }
    }
}
