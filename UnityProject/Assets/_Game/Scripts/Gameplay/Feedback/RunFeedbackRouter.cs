using JungleBooze.Core.Feedback;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Gameplay.Feedback
{
    /// <summary>
    /// Turns simulation events into haptics and audio/feedback hooks (presentation, plain C#). Haptics (spec 101
    /// §2.4, §4): light impact on landings from at least <c>SoftLandingFall</c>, medium on hard landings and minor
    /// hits, heavy on a crash; edge brushes never vibrate. Edge-brush events arrive every tick while Pista pushes
    /// into the edge, so the listener hears about them at most every <see cref="EdgeBrushRepeatTicks"/> ticks.
    /// </summary>
    public sealed class RunFeedbackRouter
    {
        /// <summary>Re-notify a continuous edge brush this often (ticks, 60 Hz).</summary>
        public const int EdgeBrushRepeatTicks = 9;

        /// <summary>A gap longer than this many ticks starts a new brush (fires <c>started</c>).</summary>
        public const int EdgeBrushGapTicks = 3;

        private readonly float _softLandingFall;
        private long _lastBrushTick = long.MinValue;
        private long _lastBrushNotifyTick = long.MinValue;
        private int _lastBrushSide;

        public RunFeedbackRouter(IHaptics haptics, IRunFeedbackListener listener, float softLandingFall)
        {
            Haptics = haptics ?? new NullHaptics();
            Listener = listener;
            _softLandingFall = softLandingFall;
        }

        public IHaptics Haptics { get; set; }

        public IRunFeedbackListener Listener { get; set; }

        /// <summary>Player setting.</summary>
        public bool HapticsEnabled { get; set; } = true;

        public void Reset()
        {
            _lastBrushTick = long.MinValue;
            _lastBrushNotifyTick = long.MinValue;
            _lastBrushSide = 0;
        }

        public void OnRunEvent(in RunEvent e)
        {
            switch (e.Type)
            {
                case RunEventType.Jump:
                    Listener?.OnJump();
                    break;
                case RunEventType.Land:
                    var kind = (LandingKind)e.Reason;
                    if (kind == LandingKind.Hard)
                    {
                        Vibrate(HapticImpact.Medium);
                    }
                    else if (e.Value >= _softLandingFall)
                    {
                        Vibrate(HapticImpact.Light);
                    }

                    Listener?.OnLand(kind, e.Value);
                    break;
                case RunEventType.SlideStart:
                    Listener?.OnSlide();
                    break;
                case RunEventType.Dodge:
                    Listener?.OnDodge(e.Reason == 1 ? 1 : -1);
                    break;
                case RunEventType.Hit:
                    var hit = (HitKind)e.Reason;
                    Vibrate(hit == HitKind.Crash ? HapticImpact.Heavy : HapticImpact.Medium);
                    Listener?.OnHit(hit);
                    break;
                case RunEventType.Died:
                    Listener?.OnDied((DeathCause)e.Reason);
                    break;
                case RunEventType.EdgeBrush:
                    OnEdgeBrush(e);
                    break;
            }
        }

        private void OnEdgeBrush(in RunEvent e)
        {
            int side = e.Reason == 1 ? 1 : -1;
            bool started = side != _lastBrushSide || e.Tick - _lastBrushTick > EdgeBrushGapTicks;
            _lastBrushTick = e.Tick;
            _lastBrushSide = side;
            if (started || e.Tick - _lastBrushNotifyTick >= EdgeBrushRepeatTicks)
            {
                _lastBrushNotifyTick = e.Tick;
                Listener?.OnEdgeBrush(side, started);
            }
        }

        private void Vibrate(HapticImpact strength)
        {
            if (HapticsEnabled)
            {
                Haptics.Impact(strength);
            }
        }
    }
}
