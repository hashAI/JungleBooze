using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Turns one pointer's timestamped positions (in points, y up) into swipe commands (spec 001 section 12,
    /// GDD 5.1). Plain C#, real time, no allocations.
    /// <list type="bullet">
    /// <item>A swipe is recognized on the sample where the pointer is at least the threshold away from its start
    /// point, if that sample is within the swipe window of that point. The window rolls: when it expires without a
    /// swipe, the start point moves to the current sample, so slow movement never blocks a later quick swipe.</item>
    /// <item>Direction = dominant axis; an exact tie goes horizontal (configurable).</item>
    /// <item>After a swipe, the same touch re-arms: the next swipe needs the re-arm distance from the point where
    /// the last one was recognized, in a new direction. Dragging on in the same direction moves that point along.
    /// [ASSUMED] The re-armed swipe has no time window.</item>
    /// </list>
    /// Taps and double taps (CompanionAssist) are not part of First Playable [ASSUMED].
    /// </summary>
    public sealed class SwipeRecognizer
    {
        private const double TimeEpsilon = 1e-9;

        private readonly float _thresholdPt;
        private readonly double _windowSeconds;
        private readonly float _rearmPt;
        private readonly bool _tieGoesHorizontal;

        private float _originX;
        private float _originY;
        private double _originTime;
        private InputCommand _lastSwipe;

        public SwipeRecognizer(InputConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _thresholdPt = config.SwipeThresholdPt;
            _windowSeconds = config.SwipeWindowMs / 1000.0;
            _rearmPt = config.SwipeRearmDistancePt;
            _tieGoesHorizontal = config.DiagonalTieGoesHorizontal;
        }

        /// <summary>True between <see cref="Begin"/> and <see cref="End"/> / <see cref="Cancel"/>.</summary>
        public bool IsTracking { get; private set; }

        /// <summary>Touch-down at (<paramref name="xPt"/>, <paramref name="yPt"/>). Replaces any tracked pointer.</summary>
        public void Begin(float xPt, float yPt, double timeSeconds)
        {
            IsTracking = true;
            _originX = xPt;
            _originY = yPt;
            _originTime = timeSeconds;
            _lastSwipe = InputCommand.None;
        }

        /// <summary>
        /// A new sample of the tracked pointer. Returns the recognized swipe command, or
        /// <see cref="InputCommand.None"/>.
        /// </summary>
        public InputCommand Move(float xPt, float yPt, double timeSeconds)
        {
            if (!IsTracking)
            {
                return InputCommand.None;
            }

            bool rearmed = _lastSwipe != InputCommand.None;
            if (!rearmed && timeSeconds - _originTime > _windowSeconds + TimeEpsilon)
            {
                // Rolling window: a touch that has gone on longer than the swipe window starts measuring again from
                // here, so a slow drag followed by a quick flick still swipes (the flick is judged on its own).
                _originX = xPt;
                _originY = yPt;
                _originTime = timeSeconds;
                return InputCommand.None;
            }

            float dx = xPt - _originX;
            float dy = yPt - _originY;
            float needed = rearmed ? _rearmPt : _thresholdPt;

            if (dx * dx + dy * dy < needed * needed)
            {
                return InputCommand.None;
            }

            InputCommand swipe = DirectionOf(dx, dy, _tieGoesHorizontal);

            _originX = xPt;
            _originY = yPt;
            _originTime = timeSeconds;

            if (swipe == _lastSwipe)
            {
                // Same direction again: slide the re-arm point along, no new command.
                return InputCommand.None;
            }

            _lastSwipe = swipe;
            return swipe;
        }

        /// <summary>Finger lifted.</summary>
        public void End()
        {
            IsTracking = false;
            _lastSwipe = InputCommand.None;
        }

        /// <summary>Touch cancelled (pause, focus loss). The current touch can no longer produce a swipe.</summary>
        public void Cancel()
        {
            End();
        }

        /// <summary>Dominant-axis direction of a movement; y up. Exact tie: horizontal if <paramref name="tieGoesHorizontal"/>.</summary>
        public static InputCommand DirectionOf(float dx, float dy, bool tieGoesHorizontal)
        {
            float ax = dx < 0f ? -dx : dx;
            float ay = dy < 0f ? -dy : dy;
            bool horizontal = ax > ay || (ax == ay && tieGoesHorizontal);
            if (horizontal)
            {
                return dx < 0f ? InputCommand.MoveLeft : InputCommand.MoveRight;
            }

            return dy > 0f ? InputCommand.Jump : InputCommand.Slide;
        }
    }
}
