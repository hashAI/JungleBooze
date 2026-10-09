using System;
using JungleBooze.Core.Feedback;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Wraps the device haptics with the GDD §19 rules: at most one haptic per 100 ms and the player toggle. Shared by
    /// the run feedback router and the audio cue pairing so the two never stack. No allocation per call.
    /// </summary>
    public sealed class HapticGate : IHaptics
    {
        public const double MinInterval = 0.1;

        private readonly IHaptics _inner;
        private readonly Func<double> _clock;
        private double _last = double.NegativeInfinity;

        public HapticGate(IHaptics inner, Func<double> clock)
        {
            _inner = inner ?? new NullHaptics();
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public bool Enabled { get; set; } = true;

        public bool Supported => _inner.Supported;

        /// <summary>Haptics dropped by the 100 ms rule (debug, tests).</summary>
        public int Dropped { get; private set; }

        public void Impact(HapticImpact strength)
        {
            if (!Enabled)
            {
                return;
            }

            double now = _clock();
            if (now - _last < MinInterval)
            {
                Dropped++;
                return;
            }

            _last = now;
            _inner.Impact(strength);
        }

        public void Play(CueHaptic haptic)
        {
            switch (haptic)
            {
                case CueHaptic.Light:
                    Impact(HapticImpact.Light);
                    break;
                case CueHaptic.Medium:
                case CueHaptic.Success:
                    Impact(HapticImpact.Medium);
                    break;
                case CueHaptic.Heavy:
                    Impact(HapticImpact.Heavy);
                    break;
            }
        }
    }
}
