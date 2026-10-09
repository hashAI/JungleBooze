using System;
using JungleBooze.Core.Feedback;
using JungleBooze.Core.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// The game's audio playback (Blueprint XXXIII–XXXV): pooled one-shot voices for SFX and UI, adaptive music,
    /// three ambience channels (bed, water layer, overlay), bus volumes from the player settings (AudioMixer groups
    /// Music / Ambience / SFX / UI with exposed volumes; per-source gains when no mixer is assigned) and haptics paired
    /// with cues through one 100 ms gate. Everything is created once; play calls don't allocate.
    /// iOS: the project keeps "Mute Other Audio Sources" off, so Unity uses the Ambient session category (the silent
    /// switch mutes the game and other apps' audio keeps playing).
    /// </summary>
    public sealed class AudioService
    {
        public const int SfxVoices = 14;

        /// <summary>Headroom on the SFX and UI buses (overlapping one-shots stay under 0 dBFS; measured on a bot run).</summary>
        public const float SfxTrimDb = -2f;

        private const float SfxTrimLinear = 0.7943282f;
        public const int UiVoices = 4;

        /// <summary>Exposed mixer parameters (dB).</summary>
        public static readonly string[] VolumeParams = { "MusicVolume", "AmbienceVolume", "SfxVolume", "UiVolume" };

        /// <summary>Mixer group names, indexed by <see cref="AudioBus"/>.</summary>
        public static readonly string[] GroupNames = { "Music", "Ambience", "SFX", "UI" };

        private readonly Func<double> _clock;
        private readonly AudioMixerGroup[] _groups;
        private readonly Func<AudioBus, float> _busGain;
        private bool _paused;

        public bool Paused => _paused;

        private AudioService(Transform parent, AudioCatalog catalog, ISettingsStore settings, IHaptics haptics, Func<double> clock)
        {
            Catalog = catalog;
            _clock = clock;
            Volumes = new AudioVolumes(settings);
            Haptics = haptics as HapticGate ?? new HapticGate(haptics, clock);
            Root = new GameObject("Audio").transform;
            Root.SetParent(parent, false);

            AudioMixer mixer = catalog.Mixer;
            if (mixer != null)
            {
                _groups = new AudioMixerGroup[GroupNames.Length];
                for (int i = 0; i < GroupNames.Length; i++)
                {
                    AudioMixerGroup[] found = mixer.FindMatchingGroups(GroupNames[i]);
                    _groups[i] = found != null && found.Length > 0 ? found[0] : null;
                }
            }

            _busGain = BusGain;
            var sfxVoices = new AudioSourceVoices(Root, "Sfx", SfxVoices, _groups, false, clock);
            var uiVoices = new AudioSourceVoices(Root, "Ui", UiVoices, _groups, true, clock);
            Sfx = new SfxEngine(catalog, sfxVoices, 0xA5A5F00Du) { BusGain = _busGain };
            Ui = new SfxEngine(catalog, uiVoices, 0x5EEDC0DEu) { BusGain = _busGain };
            Music = new MusicPlayer(Root, catalog.Music, Group(AudioBus.Music));
            Bed = new AmbienceChannel(Root, "AmbienceBed", Group(AudioBus.Ambience));
            Water = new AmbienceChannel(Root, "AmbienceWater", Group(AudioBus.Ambience));
            Overlay = new AmbienceChannel(Root, "AmbienceOverlay", Group(AudioBus.Ambience));
            ApplyVolumes();
        }

        public Transform Root { get; }

        public AudioCatalog Catalog { get; }

        public AudioVolumes Volumes { get; }

        public HapticGate Haptics { get; }

        public SfxEngine Sfx { get; }

        public SfxEngine Ui { get; }

        public MusicPlayer Music { get; }

        public AmbienceChannel Bed { get; }

        public AmbienceChannel Water { get; }

        public AmbienceChannel Overlay { get; }

        public bool UsesMixer => _groups != null && _groups[0] != null;

        /// <summary>Optional decision log (tools).</summary>
        public IAudioLog Log { get; set; }

        public double Now => _clock();

        public static AudioService Create(Transform parent, AudioCatalog catalog, ISettingsStore settings, IHaptics haptics, Func<double> clock)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return new AudioService(parent, catalog, settings, haptics, clock ?? (() => Time.realtimeSinceStartupAsDouble));
        }

        /// <summary>Cue index by id (startup only), −1 when missing.</summary>
        public int Cue(string id) => Catalog.IndexOf(id);

        /// <summary>Plays a cue on its bus and fires its paired haptic. Returns the voice or −1.</summary>
        public int Play(int cue, float pitchSemitones = 0f, float gain = 1f, bool haptic = true)
        {
            if (cue < 0 || cue >= Catalog.Count)
            {
                return -1;
            }

            AudioCue c = Catalog[cue];
            int voice = c.Bus == AudioBus.Ui ? Ui.Play(cue, _clock(), pitchSemitones, gain) : Sfx.Play(cue, _clock(), pitchSemitones, gain);
            if (voice >= 0 && haptic && c.Haptic != CueHaptic.None)
            {
                Haptics.Play(c.Haptic);
            }

            if (voice >= 0 && Log != null)
            {
                Log.Cue(_clock(), c.Id, (c.Bus == AudioBus.Ui ? Ui : Sfx).LastClip(cue), pitchSemitones, gain);
            }

            return voice;
        }

        /// <summary>A music sting (discovery, secret, unlock, vista, run over); ducks the music while it plays.</summary>
        public void PlaySting(int cue)
        {
            if (cue < 0 || cue >= Catalog.Count)
            {
                return;
            }

            AudioCue c = Catalog[cue];
            if (c.ClipCount > 0)
            {
                Music.PlaySting(c.Clips[0], c.Volume * BusGain(AudioBus.Music));
                Log?.Sting(_clock(), c.Id);
            }

            if (c.Haptic != CueHaptic.None)
            {
                Haptics.Play(c.Haptic);
            }
        }

        /// <summary>Sets an ambience channel to a looping cue (−1 = fade out).</summary>
        public void SetAmbience(AmbienceChannel channel, int cue, float fade, float gain = 1f)
        {
            if (cue < 0 || cue >= Catalog.Count || Catalog[cue].ClipCount == 0)
            {
                channel.Set(null, 0f, fade);
                Log?.Ambience(_clock(), channel.Name, null, 0f, fade);
                return;
            }

            AudioCue c = Catalog[cue];
            channel.Set(c.Clips[0], c.Volume * gain, fade);
            Log?.Ambience(_clock(), channel.Name, c.Id, c.Volume * gain, fade);
        }

        public void MusicStart(bool withIntro)
        {
            Music.StartRun(withIntro);
            Log?.Music(_clock(), "start", withIntro ? 1 : 0);
        }

        public void MusicLevel(int level)
        {
            if (level != Music.Mix.Level)
            {
                Log?.Music(_clock(), "level", level);
            }

            Music.SetLevel(level);
        }

        public void MusicStopSoft()
        {
            Music.StopSoft();
            Log?.Music(_clock(), "stopSoft", 0);
        }

        public void MusicResults()
        {
            Music.PlayResults();
            Log?.Music(_clock(), "results", 0);
        }

        public void MusicUnderwater(bool on, float cutoff)
        {
            Music.SetUnderwater(on, cutoff);
            Log?.Music(_clock(), "underwater", on ? 1 : 0);
        }

        /// <summary>Pause menu: SFX and ambience pause (AudioListener.pause); UI sounds and the music bed continue, ducked.</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            AudioListener.pause = paused;
            Music.Ducked = paused;
        }

        /// <summary>Re-reads the player volumes and applies them (mixer parameters, or per-source gains).</summary>
        public void ApplyVolumes()
        {
            Volumes.Reload();
            if (!UsesMixer)
            {
                return;
            }

            AudioMixer m = Catalog.Mixer;
            m.SetFloat(VolumeParams[0], AudioVolumes.ToDecibels(Volumes.Music));
            m.SetFloat(VolumeParams[1], AudioVolumes.ToDecibels(Volumes.Ambience));
            m.SetFloat(VolumeParams[2], Trim(AudioVolumes.ToDecibels(Volumes.Sfx)));
            m.SetFloat(VolumeParams[3], Trim(AudioVolumes.ToDecibels(Volumes.Sfx)));
        }

        /// <summary>Per frame (unscaled seconds): music and ambience fades.</summary>
        public void Tick(float dt)
        {
            Music.Update(dt, BusGain(AudioBus.Music));

            float amb = BusGain(AudioBus.Ambience);
            Bed.Update(dt, amb);
            Water.Update(dt, amb);
            Overlay.Update(dt, amb);
        }

        public void StopAll()
        {
            Sfx.StopAll();
            Ui.StopAll();
            Music.StopAll();
            Bed.StopAll();
            Water.StopAll();
            Overlay.StopAll();
        }

        private static float Trim(float db) => db <= AudioVolumes.MinDb ? db : db + SfxTrimDb;

        private AudioMixerGroup Group(AudioBus bus) => _groups != null ? _groups[(int)bus] : null;

        /// <summary>Linear gain applied per source: 1 with a mixer (the mixer applies the volume), else the setting (squared taper).</summary>
        private float BusGain(AudioBus bus)
        {
            if (UsesMixer)
            {
                return 1f;
            }

            float v = Volumes.Linear(bus);
            float g = v * v;
            return bus == AudioBus.Sfx || bus == AudioBus.Ui ? g * SfxTrimLinear : g;
        }
    }
}
