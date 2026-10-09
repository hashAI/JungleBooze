using System;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// One-shot playback rules over a pooled voice set (plain C#, no allocation per play): per-cue cooldown and voice
    /// limit, no-repeat variation choice, pitch/volume jitter (ear-fatigue guard), and voice stealing by priority then
    /// age when the pool is full.
    /// </summary>
    public sealed class SfxEngine
    {
        private const float SemitoneRatio = 1.0594631f;

        private readonly AudioCatalog _catalog;
        private readonly IAudioVoices _voices;
        private readonly VariationPicker _picker;
        private readonly int[] _voiceCue;
        private readonly int[] _voicePriority;
        private readonly double[] _voiceStart;
        private readonly double[] _lastPlay;
        private readonly int[] _lastClip;

        public SfxEngine(AudioCatalog catalog, IAudioVoices voices, uint seed)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _voices = voices ?? throw new ArgumentNullException(nameof(voices));
            _picker = new VariationPicker(seed);
            int n = voices.Count;
            _voiceCue = new int[n];
            _voicePriority = new int[n];
            _voiceStart = new double[n];
            for (int i = 0; i < n; i++)
            {
                _voiceCue[i] = -1;
            }

            _lastPlay = new double[catalog.Count];
            _lastClip = new int[catalog.Count];
            for (int i = 0; i < catalog.Count; i++)
            {
                _lastPlay[i] = double.NegativeInfinity;
                _lastClip[i] = -1;
            }
        }

        /// <summary>Linear gain per bus (player volumes when there is no mixer); set by the service.</summary>
        public Func<AudioBus, float> BusGain { get; set; }

        /// <summary>Last clip index chosen per cue (tests).</summary>
        public int LastClip(int cue) => cue >= 0 && cue < _lastClip.Length ? _lastClip[cue] : -1;

        /// <summary>
        /// Plays cue <paramref name="cue"/> at time <paramref name="now"/> (seconds). <paramref name="pitchSemitones"/>
        /// offsets the pitch (e.g. coin streaks), <paramref name="gain"/> scales the volume. Returns the voice, or −1
        /// when dropped (unknown cue, cooldown, no clip).
        /// </summary>
        public int Play(int cue, double now, float pitchSemitones = 0f, float gain = 1f)
        {
            if (cue < 0 || cue >= _catalog.Count)
            {
                return -1;
            }

            AudioCue c = _catalog[cue];
            if (c.ClipCount == 0 || now - _lastPlay[cue] < c.Cooldown)
            {
                return -1;
            }

            int clipIndex = _picker.Pick(c.ClipCount, _lastClip[cue]);
            UnityEngine.AudioClip clip = c.Clips[clipIndex];
            if (clip == null)
            {
                return -1;
            }

            int voice = ChooseVoice(cue, c, now);
            if (voice < 0)
            {
                return -1;
            }

            float semis = pitchSemitones + (c.PitchJitter * _picker.NextSigned());
            float pitch = (float)Math.Pow(SemitoneRatio, semis);
            float db = c.VolumeJitterDb * _picker.NextSigned();
            float volume = c.Volume * gain * (float)Math.Pow(10.0, db / 20.0);
            if (BusGain != null)
            {
                volume *= BusGain(c.Bus);
            }

            volume = volume < 0f ? 0f : volume > 1f ? 1f : volume;
            _voices.Start(voice, clip, c.Bus, volume, pitch);
            _voiceCue[voice] = cue;
            _voicePriority[voice] = c.Priority;
            _voiceStart[voice] = now;
            _lastPlay[cue] = now;
            _lastClip[cue] = clipIndex;
            return voice;
        }

        /// <summary>Voices currently playing <paramref name="cue"/>.</summary>
        public int ActiveVoices(int cue)
        {
            int n = 0;
            for (int i = 0; i < _voiceCue.Length; i++)
            {
                if (_voiceCue[i] == cue && _voices.IsPlaying(i))
                {
                    n++;
                }
            }

            return n;
        }

        public void StopAll()
        {
            for (int i = 0; i < _voiceCue.Length; i++)
            {
                _voices.Stop(i);
                _voiceCue[i] = -1;
            }
        }

        private int ChooseVoice(int cue, AudioCue c, double now)
        {
            // Per-cue limit: reuse this cue's oldest voice.
            int same = 0;
            int oldestSame = -1;
            for (int i = 0; i < _voiceCue.Length; i++)
            {
                if (_voiceCue[i] == cue && _voices.IsPlaying(i))
                {
                    same++;
                    if (oldestSame < 0 || _voiceStart[i] < _voiceStart[oldestSame])
                    {
                        oldestSame = i;
                    }
                }
            }

            if (same >= c.MaxVoices)
            {
                return oldestSame;
            }

            // A free voice.
            for (int i = 0; i < _voiceCue.Length; i++)
            {
                if (!_voices.IsPlaying(i))
                {
                    return i;
                }
            }

            // Steal: lowest priority, then oldest; never a higher-priority voice.
            int steal = -1;
            for (int i = 0; i < _voiceCue.Length; i++)
            {
                if (_voicePriority[i] > c.Priority)
                {
                    continue;
                }

                if (steal < 0 || _voicePriority[i] < _voicePriority[steal] ||
                    (_voicePriority[i] == _voicePriority[steal] && _voiceStart[i] < _voiceStart[steal]))
                {
                    steal = i;
                }
            }

            return steal;
        }
    }
}
