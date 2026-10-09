using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Services.Audio
{
    /// <summary>A pool of 2D AudioSources created once (no per-play allocation), routed to the bus mixer groups.</summary>
    public sealed class AudioSourceVoices : IAudioVoices
    {
        private readonly AudioSource[] _sources;
        private readonly AudioMixerGroup[] _groups;
        private readonly double[] _endTime;
        private readonly System.Func<double> _clock;

        /// <param name="clock">Service clock; outside Play mode (tools) voices end by clip length on it, since sources never advance there.</param>
        public AudioSourceVoices(Transform parent, string name, int count, AudioMixerGroup[] busGroups, bool ignoreListenerPause, System.Func<double> clock = null)
        {
            _groups = busGroups;
            _clock = clock;
            _endTime = new double[count];
            _sources = new AudioSource[count];
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int i = 0; i < count; i++)
            {
                AudioSource s = root.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = ignoreListenerPause;
                s.priority = 128;
                _sources[i] = s;
            }
        }

        public int Count => _sources.Length;

        public bool IsPlaying(int voice)
        {
            if (!Application.isPlaying && _clock != null)
            {
                return _clock() < _endTime[voice];
            }

            return _sources[voice].isPlaying;
        }

        public void Start(int voice, AudioClip clip, AudioBus bus, float volume, float pitch)
        {
            AudioSource s = _sources[voice];
            s.Stop();
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            if (_groups != null)
            {
                s.outputAudioMixerGroup = _groups[(int)bus];
            }

            s.Play();
            if (_clock != null && clip != null)
            {
                _endTime[voice] = _clock() + (clip.length / (pitch > 0.01f ? pitch : 0.01f));
            }
        }

        public void Stop(int voice)
        {
            _sources[voice].Stop();
            _endTime[voice] = 0;
        }
    }
}
