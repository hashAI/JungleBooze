using System;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Recognizes a double tap on one pointer (GDD 5.1: double tap = companion Assist; GDD 16 <c>InputTuning</c>:
    /// tap limits and a 300 ms double tap window). A tap is a touch that moved at most <c>tapMaxMovementPt</c> from
    /// its start, lasted at most <c>tapMaxDurationMs</c> and produced no swipe. A double tap is a second tap that
    /// starts within <c>doubleTapWindowMs</c> of the first tap's lift. Plain C#, real time, no allocations.
    /// </summary>
    public sealed class TapRecognizer
    {
        private readonly float _maxMovementPt;
        private readonly double _maxDurationSeconds;
        private readonly double _windowSeconds;

        private bool _tracking;
        private float _startX;
        private float _startY;
        private double _startTime;
        private bool _moved;
        private bool _hasPendingTap;
        private double _pendingTapEnd;

        public TapRecognizer(InputConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _maxMovementPt = config.TapMaxMovementPt;
            _maxDurationSeconds = config.TapMaxDurationMs / 1000.0;
            _windowSeconds = config.DoubleTapWindowMs / 1000.0;
        }

        /// <summary>Touch-down.</summary>
        public void Begin(float xPt, float yPt, double timeSeconds)
        {
            _tracking = true;
            _startX = xPt;
            _startY = yPt;
            _startTime = timeSeconds;
            _moved = false;
            if (_hasPendingTap && timeSeconds - _pendingTapEnd > _windowSeconds)
            {
                _hasPendingTap = false;
            }
        }

        /// <summary>A sample of the tracked pointer. <paramref name="swiped"/>: the swipe recognizer fired on this touch.</summary>
        public void Move(float xPt, float yPt, bool swiped)
        {
            if (!_tracking || _moved)
            {
                return;
            }

            float dx = xPt - _startX;
            float dy = yPt - _startY;
            if (swiped || dx * dx + dy * dy > _maxMovementPt * _maxMovementPt)
            {
                _moved = true;
            }
        }

        /// <summary>Finger lifted. Returns true if this lift completes a double tap.</summary>
        public bool End(double timeSeconds)
        {
            if (!_tracking)
            {
                return false;
            }

            _tracking = false;
            bool isTap = !_moved && timeSeconds - _startTime <= _maxDurationSeconds;
            if (!isTap)
            {
                _hasPendingTap = false;
                return false;
            }

            if (_hasPendingTap && _startTime - _pendingTapEnd <= _windowSeconds)
            {
                _hasPendingTap = false;
                return true;
            }

            _hasPendingTap = true;
            _pendingTapEnd = timeSeconds;
            return false;
        }

        /// <summary>Forget the tracked touch and any first tap (pause, focus loss, gameplay off).</summary>
        public void Cancel()
        {
            _tracking = false;
            _hasPendingTap = false;
        }
    }
}
