using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Touch-layer tuning (spec 001 section 3.3), plain C#. Real time, not simulation. Field defaults are the spec
    /// start values. <see cref="JungleBooze.Gameplay.Config.InputConfigAsset"/> copies its serialized fields into one.
    /// </summary>
    public sealed class InputConfig
    {
        public float SwipeThresholdPt = 28f;
        public float SwipeWindowMs = 250f;
        public float SwipeRearmDistancePt = 28f;
        public bool DiagonalTieGoesHorizontal = true;
        public float TapMaxMovementPt = 12f;
        public float TapMaxDurationMs = 200f;
        public float DoubleTapWindowMs = 300f;
        public int GestureQueueCapacity = 8;

        public static InputConfig CreateDefault()
        {
            return new InputConfig();
        }

        /// <summary>Range checks. Appends one message per problem; returns true when there are none. Allocates.</summary>
        public bool Validate(List<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            int before = errors.Count;
            if (!(SwipeThresholdPt > 0f))
            {
                errors.Add("swipeThresholdPt must be positive.");
            }

            if (!(SwipeWindowMs > 0f))
            {
                errors.Add("swipeWindowMs must be positive.");
            }

            if (!(SwipeRearmDistancePt > 0f))
            {
                errors.Add("swipeRearmDistancePt must be positive.");
            }

            if (!(TapMaxMovementPt >= 0f) || !(TapMaxDurationMs >= 0f) || !(DoubleTapWindowMs >= 0f))
            {
                errors.Add("Tap values must not be negative.");
            }

            if (GestureQueueCapacity < 1 || GestureQueueCapacity > 64)
            {
                errors.Add("gestureQueueCapacity must be in 1..64.");
            }

            return errors.Count == before;
        }
    }
}
