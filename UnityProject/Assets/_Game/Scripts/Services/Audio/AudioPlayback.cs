using JungleBooze.Services.Persistence;
using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Pooled 2D playback for runs, menus and Duko. Music, sound effects and companion voice are separate
    /// sources so a coin burst cannot cut the music. Volumes follow <see cref="PlayerSave"/> music and
    /// sound-effect sliders. There is no companion-voice slider in the save yet: spoken lines use
    /// <see cref="VoiceVolume"/> (default 1) multiplied by the sound-effect volume, and 0 switches call-outs
    /// to squawks on the sound-effect bus (GDD 15.1).
    /// iOS: leave Player Settings "Mute Other Audio Sources" off (it already is). That keeps Unity on
    /// AVAudioSessionCategoryAmbient, which respects the silent switch and other apps. No native plugin.
    /// </summary>
    public sealed class AudioPlayback : MonoBehaviour
    {
        public const int SfxVoiceCount = 8;
        public const float ChatterCooldownSeconds = 8f;
        private const float VoiceDuck = 0.55f;

        [SerializeField] private AudioCatalog _catalog;

        private readonly AudioSource[] _sfx = new AudioSource[SfxVoiceCount];
        private AudioSource _music;
        private AudioSource _sting;
        private AudioSource _voice;
        private int _sfxCursor;
        private float _musicVolume = SettingsData.DefaultMusicVolume;
        private float _sfxVolume = SettingsData.DefaultSfxVolume;
        private float _voiceVolume = 1f;
        private float _nextChatterTime;
        private bool _ducking;
        private PlayerSave _save;

        /// <summary>Companion voice 0..1. Zero plays squawk call-outs and skips chatter. [ASSUMED] until a slider exists.</summary>
        public float VoiceVolume
        {
            get => _voiceVolume;
            set => _voiceVolume = value < 0f ? 0f : (value > 1f ? 1f : value);
        }

        public AudioCatalog Catalog => _catalog;

        /// <summary>Sets the clip catalog when the component is created in code. Setup-time only.</summary>
        public void SetCatalog(AudioCatalog catalog)
        {
            _catalog = catalog;
        }

        private void Awake()
        {
            _music = CreateSource("Music", true, 64);
            _sting = CreateSource("Sting", false, 32);
            _voice = CreateSource("Voice", false, 48);
            for (int i = 0; i < SfxVoiceCount; i++)
            {
                _sfx[i] = CreateSource("Sfx", false, 128);
            }
        }

        private void OnDestroy()
        {
            if (_save != null)
            {
                _save.SettingsChanged -= ApplySavedVolumes;
            }
        }

        private void Update()
        {
            bool duck = _voice != null && _voice.isPlaying;
            if (duck == _ducking)
            {
                return;
            }

            _ducking = duck;
            ApplyMusicVolume();
        }

        /// <summary>Copies music and sound-effect volumes and keeps them in sync when settings change.</summary>
        public void Bind(PlayerSave save)
        {
            if (_save != null)
            {
                _save.SettingsChanged -= ApplySavedVolumes;
            }

            _save = save;
            if (_save != null)
            {
                _save.SettingsChanged += ApplySavedVolumes;
                ApplySavedVolumes();
            }
        }

        public void ApplySavedVolumes()
        {
            if (_save == null)
            {
                return;
            }

            _musicVolume = _save.MusicVolume;
            _sfxVolume = _save.SfxVolume;
            ApplyMusicVolume();
        }

        /// <summary>Plays the clip for one runner event. Safe to call with event types that have no sound.</summary>
        public void PlayRunnerEvent(byte eventType, short value, byte flags, int entityId)
        {
            if (!RunAudioCues.TryGet(eventType, value, flags, entityId, _voiceVolume, out AudioCue cue))
            {
                return;
            }

            PlayCue(cue);
        }

        public void PlayUiTap()
        {
            PlaySfx(new AudioCue(AudioClipId.UiTap, 1f, 0.7f, false));
        }

        /// <summary>Menu bed. Loops the menu clip.</summary>
        public void PlayMenuMusic()
        {
            PlayMusic(AudioClipId.MenuLoop);
        }

        /// <summary>[ASSUMED] Jungle world bed is theme A until the owner picks a variation.</summary>
        public void PlayJungleMusic()
        {
            PlayMusic(AudioClipId.JungleThemeA);
        }

        public void PlayGameOverSting()
        {
            if (_sting == null || _catalog == null)
            {
                return;
            }

            AudioClip clip = _catalog.Get(AudioClipId.GameOverSting);
            if (clip == null)
            {
                return;
            }

            StopMusic();
            _sting.pitch = 1f;
            _sting.volume = _musicVolume;
            _sting.clip = clip;
            _sting.Play();
        }

        public void StopMusic()
        {
            if (_music != null)
            {
                _music.Stop();
            }

            if (_sting != null)
            {
                _sting.Stop();
            }
        }

        /// <summary>Ambient line, at most once every <see cref="ChatterCooldownSeconds"/>. False when voice is muted.</summary>
        public bool PlayChatter(int index)
        {
            if (_voiceVolume <= 0f)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (now < _nextChatterTime)
            {
                return false;
            }

            _nextChatterTime = now + ChatterCooldownSeconds;
            PlayCue(new AudioCue(RunAudioCues.ChatterClip(index), 1f, 0.85f, true));
            return true;
        }

        private void PlayMusic(AudioClipId id)
        {
            if (_music == null || _catalog == null)
            {
                return;
            }

            AudioClip clip = _catalog.Get(id);
            if (clip == null)
            {
                return;
            }

            if (_sting != null && _sting.isPlaying)
            {
                _sting.Stop();
            }

            if (_music.clip == clip && _music.isPlaying)
            {
                ApplyMusicVolume();
                return;
            }

            _music.clip = clip;
            _music.pitch = 1f;
            ApplyMusicVolume();
            _music.Play();
        }

        private void PlayCue(AudioCue cue)
        {
            if (cue.Spoken)
            {
                PlayVoice(cue);
            }
            else
            {
                PlaySfx(cue);
            }
        }

        private void PlaySfx(AudioCue cue)
        {
            if (_catalog == null || _sfxVolume <= 0f)
            {
                return;
            }

            AudioClip clip = _catalog.Get(cue.Clip);
            if (clip == null)
            {
                return;
            }

            AudioSource source = _sfx[_sfxCursor];
            if (source == null)
            {
                return;
            }

            _sfxCursor++;
            if (_sfxCursor >= SfxVoiceCount)
            {
                _sfxCursor = 0;
            }

            source.pitch = cue.Pitch;
            source.volume = cue.Gain * _sfxVolume;
            source.clip = clip;
            source.Play();
        }

        private void PlayVoice(AudioCue cue)
        {
            if (_voice == null || _catalog == null || _voiceVolume <= 0f || _sfxVolume <= 0f)
            {
                return;
            }

            AudioClip clip = _catalog.Get(cue.Clip);
            if (clip == null)
            {
                return;
            }

            _voice.pitch = cue.Pitch;
            _voice.volume = cue.Gain * _voiceVolume * _sfxVolume;
            _voice.clip = clip;
            _voice.Play();
            if (!_ducking)
            {
                _ducking = true;
                ApplyMusicVolume();
            }
        }

        private void ApplyMusicVolume()
        {
            if (_music == null)
            {
                return;
            }

            float duck = _ducking ? VoiceDuck : 1f;
            _music.volume = _musicVolume * duck;
        }

        private AudioSource CreateSource(string name, bool loop, int priority)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.reverbZoneMix = 0f;
            return source;
        }
    }
}
