using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Services.Audio;
using JungleBooze.UI.Hud;

namespace JungleBooze.App
{
    /// <summary>
    /// Run-scene audio adapter. Forwards runner events to <see cref="AudioPlayback"/> (cue mapping lives in
    /// <see cref="RunAudioCues"/>), plays the UI tap on every HUD/menu button, and picks the music from the session
    /// phase: menu loop in the Menu, Jungle theme in a run, silence while dying, the sting on Game Over.
    /// [ASSUMED] Jungle theme A until the owner picks a variation. Thin view only; no allocations per frame.
    /// </summary>
    public sealed class RunAudioView : IRunView, IDisposable
    {
        private const SessionPhase NoPhase = (SessionPhase)255;

        private readonly AudioPlayback _audio;
        private readonly Action _onButtonPressed;
        private SessionPhase _phase = NoPhase;

        public RunAudioView(AudioPlayback audio)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _onButtonPressed = OnButtonPressed;
            HudFactory.ButtonPressed += _onButtonPressed;
        }

        public void Dispose()
        {
            HudFactory.ButtonPressed -= _onButtonPressed;
        }

        private void OnButtonPressed()
        {
            // The audio object is destroyed with the Run scene; drop the static subscription then.
            if (_audio == null)
            {
                Dispose();
                return;
            }

            _audio.PlayUiTap();
        }

        public void BeginRun(GameSession session)
        {
            // A new run (Restart, Home) re-applies the music for whatever phase it starts in.
            _phase = NoPhase;
            ApplyPhase(session.Phase);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            _audio.PlayRunnerEvent((byte)e.Type, e.Value, e.Flags, e.EntityId);
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            ApplyPhase(session.Phase);
        }

        private void ApplyPhase(SessionPhase phase)
        {
            if (phase == _phase)
            {
                return;
            }

            _phase = phase;
            switch (phase)
            {
                case SessionPhase.Menu:
                    _audio.PlayMenuMusic();
                    break;
                case SessionPhase.Dying:
                case SessionPhase.ContinueOffer:
                    _audio.StopMusic();
                    break;
                case SessionPhase.GameOver:
                    _audio.PlayGameOverSting();
                    break;
                default:
                    // Ready, Running, Paused, Countdown: keep the Jungle bed going (no-op if already playing).
                    _audio.PlayJungleMusic();
                    break;
            }
        }
    }
}
