using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Gameplay.Views;
using JungleBooze.Services.Audio;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// Plays run sounds and music from the runner's events and the session phase. Menu bed on the main menu,
    /// bed of the current world while a run is going (crossfaded over 2 s when the world changes, see
    /// <see cref="WorldThemeView.SegmentChanged"/>; a Continue keeps the bed of the world HERO is in), Game Over sting
    /// when the run ends. No allocations per frame.
    /// </summary>
    public sealed class RunAudioView : IRunView
    {
        private readonly AudioPlayback _audio;
        private SessionPhase _lastPhase = (SessionPhase)255;
        private WorldScheduleConfig _worlds;
        private AudioClipId _bed = AudioClipId.JungleThemeA;
        private bool _inRun;

        public RunAudioView(AudioPlayback audio)
        {
            _audio = audio;
        }

        /// <summary>Crossfades the music when HERO enters another world. Call once at setup.</summary>
        public void BindWorlds(WorldThemeView worldView)
        {
            if (worldView != null)
            {
                worldView.SegmentChanged += OnSegmentChanged;
            }
        }

        public void BeginRun(GameSession session)
        {
            _lastPhase = (SessionPhase)255;
            _audio.ResetMusicMemory();
            var world = session.World as TrackRunWorld;
            _worlds = world?.Track?.Worlds;
            _bed = world != null && _worlds != null ? BedForSegment(world.Track.WorldSegmentAt(session.Runner.Current.Z)) : AudioClipId.JungleThemeA;
        }

        private AudioClipId BedForSegment(int segment)
        {
            return RunAudioCues.WorldMusic((int)_worlds.KindOfSegment(segment), _worlds.IsDuskSegment(segment));
        }

        private void OnSegmentChanged(int segment)
        {
            if (_worlds == null)
            {
                return;
            }

            _bed = BedForSegment(segment);
            if (_inRun)
            {
                _audio.PlayWorldMusic(_bed);
            }
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
                    _inRun = false;
                    _audio.ResetMusicMemory();
                    _audio.PlayMenuMusic();
                    break;
                case SessionPhase.Ready:
                case SessionPhase.Running:
                case SessionPhase.Countdown:
                    _inRun = true;
                    _audio.PlayWorldMusic(_bed);
                    break;
                case SessionPhase.GameOver:
                    _inRun = false;
                    if (previous != SessionPhase.GameOver)
                    {
                        _audio.PlayGameOverSting();
                    }

                    break;
            }
        }
    }
}
