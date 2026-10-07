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

        /// <summary>Music crossfade between worlds (s).</summary>
        public const float MusicCrossfadeSeconds = 2f;

        [SerializeField] private AudioCatalog _catalog;

        private readonly AudioSource[] _sfx = new AudioSource[SfxVoiceCount];
        // Two pooled music sources: one plays, the other takes over during a crossfade. No allocation after Awake.
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly float[] _musicWeight = new float[2];
        private readonly AudioClipId[] _musicId = new AudioClipId[2];
        private readonly float[] _musicResume = new float[256];
        private int _musicCur;
        private bool _fading;
        private float _fadeInRate = 1f;
        private float _fadeOutRate = 1f;
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

        /// <summary>Sets the clip catalog when the component is created in code.</summary>
        public void SetCatalog(AudioCatalog catalog)
        {
            _catalog = catalog;
        }

        private void Awake()
        {
            _music[0] = CreateSource("Music", true, 64);
            _music[1] = CreateSource("Music2", true, 64);
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
            bool changed = duck != _ducking;
            _ducking = duck;
            if (_fading)
            {
                AdvanceCrossfade(Time.unscaledDeltaTime);
                changed = true;
            }

            if (changed)
            {
                ApplyMusicVolume();
            }
        }

        private void AdvanceCrossfade(float dt)
        {
            int cur = _musicCur;
            int prev = 1 - cur;
            float wc = _musicWeight[cur] + (dt * _fadeInRate);
            float wp = _musicWeight[prev] - (dt * _fadeOutRate);
            _musicWeight[cur] = wc > 1f ? 1f : wc;
            _musicWeight[prev] = wp < 0f ? 0f : wp;
            if (_musicWeight[cur] >= 1f && _musicWeight[prev] <= 0f)
            {
                StopSlot(prev);
                _fading = false;
            }
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

        /// <summary>
        /// Plays a world bed: starts it at once when no music is playing, otherwise crossfades from the current bed over
        /// <see cref="MusicCrossfadeSeconds"/>. A bed that was left earlier in the run resumes from where it stopped
        /// (see <see cref="ResetMusicMemory"/>). The same clip again changes nothing. No allocation.
        /// </summary>
        public void PlayWorldMusic(AudioClipId id)
        {
            if (_catalog == null || _music[0] == null || _music[1] == null)
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

            AudioSource current = _music[_musicCur];
            if (current.clip == clip && current.isPlaying)
            {
                return;
            }

            if (!current.isPlaying)
            {
                StartImmediate(id, clip);
                return;
            }

            // Crossfade. A fade that is still running is cut short: its outgoing source stops now, the incoming one
            // (maybe partly faded in) becomes the outgoing one from its current weight.
            int outgoing = _musicCur;
            int incoming = 1 - outgoing;
            if (_fading)
            {
                StopSlot(incoming);
            }

            _musicResume[(int)_musicId[outgoing]] = current.time;
            float outStart = _musicWeight[outgoing] > 0.01f ? _musicWeight[outgoing] : 1f;
            AudioSource next = _music[incoming];
            next.clip = clip;
            next.pitch = 1f;
            float resume = _musicResume[(int)id];
            next.time = resume > 0f && resume < clip.length ? resume : 0f;
            _musicId[incoming] = id;
            _musicWeight[incoming] = 0f;
            _musicCur = incoming;
            _fadeInRate = 1f / MusicCrossfadeSeconds;
            _fadeOutRate = outStart / MusicCrossfadeSeconds;
            _fading = true;
            ApplyMusicVolume();
            next.Play();
        }

        /// <summary>Forgets where each bed stopped. Call when a new run starts or on the menu.</summary>
        public void ResetMusicMemory()
        {
            for (int i = 0; i < _musicResume.Length; i++)
            {
                _musicResume[i] = 0f;
            }
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
            _fading = false;
            StopSlot(0);
            StopSlot(1);
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
            if (_music[0] == null || _music[1] == null || _catalog == null)
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

            AudioSource current = _music[_musicCur];
            if (current.clip == clip && current.isPlaying && !_fading)
            {
                ApplyMusicVolume();
                return;
            }

            StartImmediate(id, clip);
        }

        /// <summary>Starts <paramref name="clip"/> on the current source at full weight and silences the other.</summary>
        private void StartImmediate(AudioClipId id, AudioClip clip)
        {
            _fading = false;
            int other = 1 - _musicCur;
            StopSlot(other);
            AudioSource source = _music[_musicCur];
            source.Stop();
            source.clip = clip;
            source.pitch = 1f;
            float resume = _musicResume[(int)id];
            source.time = resume > 0f && resume < clip.length ? resume : 0f;
            _musicId[_musicCur] = id;
            _musicWeight[_musicCur] = 1f;
            ApplyMusicVolume();
            source.Play();
        }

        private void StopSlot(int slot)
        {
            _musicWeight[slot] = 0f;
            if (_music[slot] != null)
            {
                _music[slot].Stop();
            }
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
            float duck = _ducking ? VoiceDuck : 1f;
            for (int i = 0; i < _music.Length; i++)
            {
                if (_music[i] != null)
                {
                    _music[i].volume = _musicVolume * duck * _musicWeight[i];
                }
            }
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
