using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Services.Audio;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Plays run sounds and music from the runner's events and the session phase. Menu bed on the main menu,
    /// Jungle bed while a run is going, Game Over sting when the run ends. No allocations per frame.
    /// </summary>
    public sealed class RunAudioView : IRunView
    {
        private readonly AudioPlayback _audio;
        private SessionPhase _lastPhase = (SessionPhase)255;

        public RunAudioView(AudioPlayback audio)
        {
            _audio = audio;
        }

        public void BeginRun(GameSession session)
        {
            _lastPhase = (SessionPhase)255;
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            _audio.PlayRunnerEvent((byte)e.Type, e.Value, e.Flags, e.EntityId);
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            SessionPhase phase = session.Phase;
            if (phase == _lastPhase)
            {
                return;
            }

            SessionPhase previous = _lastPhase;
            _lastPhase = phase;
            switch (phase)
            {
                case SessionPhase.Menu:
                    _audio.PlayMenuMusic();
                    break;
                case SessionPhase.Ready:
                case SessionPhase.Running:
                case SessionPhase.Countdown:
                    _audio.PlayJungleMusic();
                    break;
                case SessionPhase.GameOver:
                    if (previous != SessionPhase.GameOver)
                    {
                        _audio.PlayGameOverSting();
                    }

                    break;
            }
        }
    }
}
