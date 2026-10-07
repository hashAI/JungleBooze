using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Tutorial;
using JungleBooze.Services.Audio;

namespace JungleBooze.App
{
    /// <summary>
    /// Duko's idle chatter (GDD 15.1): after 18 to 35 s of calm running he says one of his ambient lines
    /// (<see cref="AudioPlayback.PlayChatter"/>, which keeps its own cooldown and stays silent at voice volume 0;
    /// call-outs fall back to squawks at 0, chatter just does not play). Never while HERO swings on a vine, is lifted
    /// or dead, while the tutorial is teaching, or within 3 s of a call-out, near-miss, stumble, vine grab or revive.
    /// The time it counts is run time (only while the run is running). The interval comes from a small seeded
    /// generator (run seed), so a replay of a seed chats at the same moments; this is cosmetic and not part of the
    /// simulation. No allocations.
    /// </summary>
    public sealed class DukoChatterView : IRunView
    {
        public const float MinIntervalSeconds = 18f;
        public const float MaxIntervalSeconds = 35f;
        public const float QuietAfterBusySeconds = 3f;
        private const float RetrySeconds = 1f;

        private readonly AudioPlayback _audio;
        private readonly TutorialDirector _tutorial;
        private float _clock;
        private float _nextAt;
        private float _lastBusyAt = -1000f;
        private int _lineIndex;
        private uint _state = 1u;

        public DukoChatterView(AudioPlayback audio, TutorialDirector tutorial)
        {
            _audio = audio;
            _tutorial = tutorial;
        }

        public void BeginRun(GameSession session)
        {
            _state = (uint)(session.RunSeed ^ (session.RunSeed >> 32)) | 1u;
            _clock = 0f;
            _lastBusyAt = -1000f;
            _lineIndex = (int)(NextUnit() * 3f);
            _nextAt = NextInterval();
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            switch (e.Type)
            {
                case RunnerEventType.CompanionCallout:
                case RunnerEventType.NearMiss:
                case RunnerEventType.Stumbled:
                case RunnerEventType.VineGrabbed:
                case RunnerEventType.Revived:
                case RunnerEventType.Died:
                    _lastBusyAt = _clock;
                    break;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (session.Phase != SessionPhase.Running)
            {
                return;
            }

            _clock += realDeltaSeconds;
            if (_clock < _nextAt)
            {
                return;
            }

            Locomotion locomotion = session.Runner.Current.Locomotion;
            bool calm = locomotion != Locomotion.Carried
                && locomotion != Locomotion.Lifted
                && locomotion != Locomotion.Dead
                && (_tutorial == null || !_tutorial.Active)
                && _clock - _lastBusyAt >= QuietAfterBusySeconds;
            if (!calm)
            {
                _nextAt = _clock + RetrySeconds;
                return;
            }

            if (_audio.PlayChatter(_lineIndex))
            {
                _lineIndex++;
                _nextAt = _clock + NextInterval();
            }
            else
            {
                _nextAt = _clock + RetrySeconds;
            }
        }

        private float NextInterval()
        {
            return MinIntervalSeconds + ((MaxIntervalSeconds - MinIntervalSeconds) * NextUnit());
        }

        /// <summary>xorshift32 in [0, 1).</summary>
        private float NextUnit()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x & 0xFFFFFFu) / 16777216f;
        }
    }
}
