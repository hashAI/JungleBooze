using System;
using JungleBooze.Core;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// "Steer + Flick" gesture recognizer (spec 101 §3.3). Plain C#, presentation side: it turns timestamped touch
    /// samples (points) into a steering drag (metres, accumulated until consumed), discrete commands (Jump, Slide,
    /// DodgeLeft/Right) in a fixed-size queue, and a TouchBegan flag. No allocation after construction.
    ///
    /// Rules: vertical swipes fire when 24 pt is crossed within 0.12 s at ≤ 35° from vertical (not on release); the
    /// latest sample before the window counts as the finger's position at the window start (iOS sends no samples
    /// while a finger rests); the same direction re-fires on one touch only after 0.18 s and a fresh 24 pt.
    /// Swipe vs steering use one angle (35° from vertical) with hysteresis: the touch's direction is classified per
    /// <see cref="GestureConfig.DirectionSegment"/> of travel; a vertical segment latches "vertical" (no steering)
    /// and only a segment beyond <see cref="GestureConfig.SteerResumeAngle"/> from vertical unlatches it (its travel
    /// is then applied, no lost drag). When a swipe fires, any lateral the swipe's own samples already produced is
    /// taken back, so a gesture is a swipe or steering, never both. A 4 pt dead zone holds lateral travel and then
    /// releases all of it; quick flicks (≤ 0.22 s, ≥ 30 pt, ≤ 35° from horizontal, no vertical swipe on the touch)
    /// and fast releases (≥ 1,200 pt/s over the last 0.06 s) dodge on release; touch A (first down) steers, touch B
    /// only swipes/flicks and takes over steering when A lifts; cancelled touches emit nothing more.
    /// </summary>
    public sealed class GestureRecognizer
    {
        private const int HistorySize = 48;

        private readonly GestureConfig _config;
        private readonly Track[] _tracks;
        private readonly float _verticalRatio;
        private readonly float _resumeRatio;
        private readonly float _swipeRatio;
        private readonly float _flickRatio;
        private double _pendingLateralPt;

        public GestureRecognizer(GestureConfig config)
        {
            _config = (config ?? throw new ArgumentNullException(nameof(config))).Clone();
            Commands = new CommandQueue(Math.Max(1, _config.CommandQueueSize));
            _tracks = new Track[Math.Max(1, _config.MaxTrackedTouches)];
            for (int i = 0; i < _tracks.Length; i++)
            {
                _tracks[i] = new Track();
            }

            // One shared angle: a direction is "vertical" when |dx| ≤ |dy|·tan(swipeAngle) (35° → 0.70); it stops
            // being vertical only past |dx| ≥ |dy|·tan(steerResumeAngle) (55° → 1.43). Swipe: |dy| ≥ |dx| / tan(35°).
            _verticalRatio = (float)Math.Tan(_config.SwipeAngleTolerance * Math.PI / 180.0);
            _resumeRatio = (float)Math.Tan(Math.Max(_config.SwipeAngleTolerance, _config.SteerResumeAngle) * Math.PI / 180.0);
            _swipeRatio = 1f / _verticalRatio;
            _flickRatio = 1f / (float)Math.Tan(_config.FlickAngleTolerance * Math.PI / 180.0);
            SensitivityMultiplier = _config.SensitivityMultiplier;
        }

        public GestureConfig Config => _config;

        /// <summary>Recognized discrete commands in recognition order.</summary>
        public CommandQueue Commands { get; }

        /// <summary>Touches that began since the last <see cref="ConsumeTouchBegan"/>.</summary>
        public int PendingTouchBegan { get; private set; }

        /// <summary>Player setting (0.5–2.0) applied on top of <see cref="GestureConfig.DragSensitivity"/>.</summary>
        public float SensitivityMultiplier { get; set; }

        /// <summary>Steering drag not yet consumed, metres.</summary>
        public float PendingLateralM => (float)(_pendingLateralPt * _config.DragSensitivity * SensitivityMultiplier);

        public int ActiveTouchCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _tracks.Length; i++)
                {
                    if (_tracks[i].Active)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        /// <summary>Returns and clears the pending steering drag in metres.</summary>
        public double ConsumeLateralM()
        {
            double metres = _pendingLateralPt * _config.DragSensitivity * SensitivityMultiplier;
            _pendingLateralPt = 0.0;
            return metres;
        }

        public bool ConsumeTouchBegan()
        {
            bool any = PendingTouchBegan > 0;
            PendingTouchBegan = 0;
            return any;
        }

        /// <summary>Forgets everything (new run).</summary>
        public void Reset()
        {
            for (int i = 0; i < _tracks.Length; i++)
            {
                _tracks[i].Active = false;
            }

            Commands.Clear();
            _pendingLateralPt = 0.0;
            PendingTouchBegan = 0;
        }

        /// <summary>
        /// Touches currently down are ignored until they lift (resume from pause, spec 101 §9). Pending output is
        /// discarded.
        /// </summary>
        public void IgnoreActiveTouches()
        {
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (_tracks[i].Active)
                {
                    _tracks[i].Ignored = true;
                    _tracks[i].Steering = false;
                }
            }

            Commands.Clear();
            _pendingLateralPt = 0.0;
            PendingTouchBegan = 0;
        }

        /// <summary>Cancels every active touch (orientation change): no flick, no further delta.</summary>
        public void CancelAll(double time)
        {
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (_tracks[i].Active)
                {
                    Process(new TouchSample(_tracks[i].Id, TouchPhaseKind.Canceled, _tracks[i].LastX, _tracks[i].LastY, time));
                }
            }
        }

        public void Process(in TouchSample sample)
        {
            switch (sample.Phase)
            {
                case TouchPhaseKind.Began:
                    Begin(sample);
                    break;
                case TouchPhaseKind.Moved:
                case TouchPhaseKind.Stationary:
                    {
                        Track track = Find(sample.TouchId);
                        if (track != null && !track.Ignored)
                        {
                            Move(track, sample);
                        }

                        break;
                    }

                case TouchPhaseKind.Ended:
                    {
                        Track track = Find(sample.TouchId);
                        if (track == null)
                        {
                            break;
                        }

                        if (!track.Ignored)
                        {
                            Move(track, sample);
                            DetectFlick(track, sample);
                        }

                        Release(track);
                        break;
                    }

                case TouchPhaseKind.Canceled:
                    {
                        Track track = Find(sample.TouchId);
                        if (track != null)
                        {
                            Release(track);
                        }

                        break;
                    }
            }
        }

        private void Begin(in TouchSample sample)
        {
            Track existing = Find(sample.TouchId);
            if (existing != null)
            {
                Release(existing);
            }

            Track track = null;
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (!_tracks[i].Active)
                {
                    track = _tracks[i];
                    break;
                }
            }

            if (track == null)
            {
                return;
            }

            bool steeringTaken = false;
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (_tracks[i].Active && _tracks[i].Steering)
                {
                    steeringTaken = true;
                }
            }

            track.Start(sample, !steeringTaken);
            PendingTouchBegan++;
        }

        private void Move(Track track, in TouchSample sample)
        {
            float dx = sample.XPt - track.LastX;
            float dy = sample.YPt - track.LastY;
            float emitted = 0f;

            if (track.Steering && (dx != 0f || dy != 0f))
            {
                float lateral = ClassifyLateral(track, sample, dx);
                if (track.DeadZonePassed)
                {
                    emitted = lateral;
                }
                else
                {
                    track.HeldLateralPt += lateral;
                    float ox = sample.XPt - track.StartX;
                    float oy = sample.YPt - track.StartY;
                    if ((ox * ox) + (oy * oy) >= _config.TouchDeadZone * _config.TouchDeadZone)
                    {
                        track.DeadZonePassed = true;
                        emitted = track.HeldLateralPt;
                        track.HeldLateralPt = 0f;
                    }
                }

                _pendingLateralPt += emitted;
            }

            track.Push(sample.XPt, sample.YPt, sample.Time, emitted);
            DetectSwipe(track, sample);
        }

        /// <summary>
        /// Steering share of one sample's horizontal motion under the swipe/steer hysteresis. Direction is judged per
        /// segment of travel (not per sample) so jitter and high sample rates don't flip it.
        /// </summary>
        private float ClassifyLateral(Track track, in TouchSample sample, float dx)
        {
            float sx = sample.XPt - track.AnchorX;
            float sy = sample.YPt - track.AnchorY;
            float segment = _config.DirectionSegment;
            if ((sx * sx) + (sy * sy) < segment * segment)
            {
                if (track.VerticalLatched)
                {
                    track.LatchHeldPt += dx;
                    return 0f;
                }

                return dx;
            }

            track.AnchorX = sample.XPt;
            track.AnchorY = sample.YPt;
            float ax = Math.Abs(sx);
            float ay = Math.Abs(sy);
            if (!track.VerticalLatched)
            {
                if (ax <= ay * _verticalRatio)
                {
                    track.VerticalLatched = true;
                    track.LatchHeldPt = 0f;
                    return 0f;
                }

                return dx;
            }

            if (ax >= ay * _resumeRatio)
            {
                // Clearly horizontal again: steering resumes with this segment's full travel.
                float released = track.LatchHeldPt + dx;
                track.VerticalLatched = false;
                track.LatchHeldPt = 0f;
                return released;
            }

            // Still vertical, or inside the hysteresis band: this segment doesn't steer.
            track.LatchHeldPt = 0f;
            return 0f;
        }

        private void DetectSwipe(Track track, in TouchSample sample)
        {
            double windowStart = sample.Time - _config.SwipeWindow;
            for (int i = track.Count - 2; i >= 0; i--)
            {
                int index = track.Index(i);
                double t = track.HistoryT[index];
                if (t < track.SwipeFloorTime)
                {
                    break;
                }

                // A sample older than the window is still used once as the reference: the finger stayed there until
                // the next sample (iOS sends nothing while a finger rests), so a swipe after a resting thumb counts.
                bool beforeWindow = t < windowStart;
                float dx = sample.XPt - track.HistoryX[index];
                float dy = sample.YPt - track.HistoryY[index];
                if (Math.Abs(dy) >= _config.SwipeDistance && Math.Abs(dy) >= Math.Abs(dx) * _swipeRatio)
                {
                    int dir = dy > 0f ? 1 : -1;
                    if (dir != track.LastSwipeDir || t >= track.LastSwipeTime + _config.SwipeRearmTime)
                    {
                        Fire(track, sample, dir, i);
                        return;
                    }
                }

                if (beforeWindow)
                {
                    break;
                }
            }
        }

        private void Fire(Track track, in TouchSample sample, int dir, int reference)
        {
            Commands.TryEnqueue(dir > 0 ? InputCommand.Jump : InputCommand.Slide);
            track.VerticalFired = true;
            track.LastSwipeDir = dir;
            track.LastSwipeTime = sample.Time;
            track.SwipeFloorTime = sample.Time;

            // A gesture is a swipe or steering, never both: take back what the swipe's own samples steered. That is
            // every sample after the reference, plus the fast samples just before it inside the window (the sideways
            // hook a real thumb swipe often starts with). A slower drag before the swipe keeps its steering.
            double windowStart = sample.Time - _config.SwipeWindow;
            float fast = _config.SwipeDistance / Math.Max(0.001f, _config.SwipeWindow);
            for (int i = track.Count - 1; i >= 1; i--)
            {
                int index = track.Index(i);
                if (i <= reference && (track.HistoryT[index] < windowStart || SpeedAt(track, i) < fast))
                {
                    break;
                }

                _pendingLateralPt -= track.HistoryLat[index];
                track.HistoryLat[index] = 0f;
            }

            track.HeldLateralPt = 0f;
            track.VerticalLatched = track.Steering;
            track.LatchHeldPt = 0f;
            track.AnchorX = sample.XPt;
            track.AnchorY = sample.YPt;
        }

        /// <summary>Finger speed at sample i, pt/s, over at least <see cref="GestureConfig.SpeedSampleTime"/> (robust to jitter).</summary>
        private float SpeedAt(Track track, int i)
        {
            int index = track.Index(i);
            double t = track.HistoryT[index];
            int j = i - 1;
            while (j > 0 && track.HistoryT[track.Index(j)] > t - _config.SpeedSampleTime)
            {
                j--;
            }

            int from = track.Index(j);
            double dt = t - track.HistoryT[from];
            if (dt <= 1e-6)
            {
                return 0f;
            }

            float dx = track.HistoryX[index] - track.HistoryX[from];
            float dy = track.HistoryY[index] - track.HistoryY[from];
            return (float)(Math.Sqrt((dx * dx) + (dy * dy)) / dt);
        }

        private void DetectFlick(Track track, in TouchSample sample)
        {
            double lifetime = sample.Time - track.StartTime;
            float dx = sample.XPt - track.StartX;
            float dy = sample.YPt - track.StartY;
            if (!track.VerticalFired && lifetime <= _config.FlickMaxDuration)
            {
                if (Math.Abs(dx) >= _config.FlickMinDistance && Math.Abs(dx) >= Math.Abs(dy) * _flickRatio)
                {
                    Commands.TryEnqueue(dx > 0f ? InputCommand.DodgeRight : InputCommand.DodgeLeft);
                }

                return;
            }

            if (!_config.ReleaseFlickEnabled || track.Count < 2)
            {
                return;
            }

            // Release speed over the last ReleaseFlickWindow seconds.
            double from = sample.Time - _config.ReleaseFlickWindow;
            int reference = track.Index(0);
            for (int i = track.Count - 2; i >= 0; i--)
            {
                int index = track.Index(i);
                reference = index;
                if (track.HistoryT[index] <= from)
                {
                    break;
                }
            }

            double dt = sample.Time - track.HistoryT[reference];
            if (dt <= 1e-6)
            {
                return;
            }

            float rx = sample.XPt - track.HistoryX[reference];
            float ry = sample.YPt - track.HistoryY[reference];
            double speed = Math.Abs(rx) / dt;
            if (speed >= _config.ReleaseFlickSpeed && Math.Abs(rx) >= Math.Abs(ry) * _flickRatio)
            {
                Commands.TryEnqueue(rx > 0f ? InputCommand.DodgeRight : InputCommand.DodgeLeft);
            }
        }

        private void Release(Track track)
        {
            bool wasSteering = track.Steering && !track.Ignored;
            track.Active = false;
            track.Steering = false;
            if (!wasSteering)
            {
                return;
            }

            // The other touch takes over steering from its current position (no target jump).
            for (int i = 0; i < _tracks.Length; i++)
            {
                Track other = _tracks[i];
                if (other.Active && !other.Ignored)
                {
                    other.Steering = true;
                    other.DeadZonePassed = true;
                    other.HeldLateralPt = 0f;
                    other.VerticalLatched = false;
                    other.LatchHeldPt = 0f;
                    other.AnchorX = other.LastX;
                    other.AnchorY = other.LastY;
                    break;
                }
            }
        }

        private Track Find(int touchId)
        {
            for (int i = 0; i < _tracks.Length; i++)
            {
                if (_tracks[i].Active && _tracks[i].Id == touchId)
                {
                    return _tracks[i];
                }
            }

            return null;
        }

        private sealed class Track
        {
            public readonly float[] HistoryX = new float[HistorySize];
            public readonly float[] HistoryY = new float[HistorySize];
            public readonly double[] HistoryT = new double[HistorySize];

            /// <summary>Steering (pt) each sample added to the pending drag, so a swipe can take it back.</summary>
            public readonly float[] HistoryLat = new float[HistorySize];

            public bool Active;
            public bool Ignored;
            public int Id;
            public bool Steering;
            public bool DeadZonePassed;
            public float HeldLateralPt;
            public float StartX;
            public float StartY;
            public double StartTime;
            public float LastX;
            public float LastY;
            public bool VerticalFired;
            public int LastSwipeDir;
            public double LastSwipeTime;
            public double SwipeFloorTime;
            public float AnchorX;
            public float AnchorY;
            public bool VerticalLatched;
            public float LatchHeldPt;
            public int Count;
            private int _head;

            public void Start(in TouchSample sample, bool steering)
            {
                Active = true;
                Ignored = false;
                Id = sample.TouchId;
                Steering = steering;
                DeadZonePassed = false;
                HeldLateralPt = 0f;
                StartX = sample.XPt;
                StartY = sample.YPt;
                StartTime = sample.Time;
                VerticalFired = false;
                LastSwipeDir = 0;
                LastSwipeTime = double.NegativeInfinity;
                SwipeFloorTime = double.NegativeInfinity;
                AnchorX = sample.XPt;
                AnchorY = sample.YPt;
                VerticalLatched = false;
                LatchHeldPt = 0f;
                Count = 0;
                _head = 0;
                Push(sample.XPt, sample.YPt, sample.Time, 0f);
            }

            public void Push(float x, float y, double t, float lateral)
            {
                HistoryX[_head] = x;
                HistoryY[_head] = y;
                HistoryT[_head] = t;
                HistoryLat[_head] = lateral;
                _head = (_head + 1) % HistorySize;
                if (Count < HistorySize)
                {
                    Count++;
                }

                LastX = x;
                LastY = y;
            }

            /// <summary>Ring index of the i-th sample, 0 = oldest kept.</summary>
            public int Index(int i)
            {
                return (_head - Count + i + (2 * HistorySize)) % HistorySize;
            }
        }
    }
}
