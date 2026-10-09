using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// One looping ambience layer with an A/B pair of sources: <see cref="Set"/> cross-fades to a new bed (GDD §19:
    /// forest layers cross-fade by chunk, the river layer comes in near water). Idle sources are stopped (no decode).
    /// </summary>
    public sealed class AmbienceChannel
    {
        private readonly AudioSource[] _src = new AudioSource[2];
        private readonly float[] _vol = new float[2];
        private readonly float[] _target = new float[2];
        private int _active;
        private float _fade = 2f;

        public AmbienceChannel(Transform parent, string name, AudioMixerGroup group)
        {
            Name = name;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int i = 0; i < 2; i++)
            {
                AudioSource s = root.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.priority = 64;
                s.outputAudioMixerGroup = group;
                s.volume = 0f;
                _src[i] = s;
            }

            LowPass = root.AddComponent<AudioLowPassFilter>();
            LowPass.cutoffFrequency = 22000f;
            LowPass.enabled = false;
        }

        public AudioLowPassFilter LowPass { get; }

        public string Name { get; }

        public AudioClip Current => _target[_active] > 0f ? _src[_active].clip : null;

        /// <summary>Cross-fades to <paramref name="clip"/> at <paramref name="volume"/> over <paramref name="fade"/> s (null = fade out).</summary>
        public void Set(AudioClip clip, float volume, float fade)
        {
            _fade = fade > 0.01f ? fade : 0.01f;
            AudioSource cur = _src[_active];
            if (clip != null && cur.clip == clip && cur.isPlaying)
            {
                _target[_active] = volume;
                return;
            }

            _target[_active] = 0f;
            if (clip == null)
            {
                return;
            }

            _active ^= 1;
            AudioSource next = _src[_active];
            next.clip = clip;
            next.volume = 0f;
            _vol[_active] = 0f;
            _target[_active] = volume;

            // Start the new bed somewhere along its loop so repeated visits don't always sound the same.
            if (clip.samples > 0)
            {
                next.timeSamples = (int)((Time.realtimeSinceStartup * 7919f) % clip.samples);
            }

            next.Play();
        }

        public void Update(float dt, float gain)
        {
            for (int i = 0; i < 2; i++)
            {
                float step = dt / _fade;
                float v = _vol[i];
                float t = _target[i];
                v = v < t ? Mathf.Min(t, v + step) : Mathf.Max(t, v - step);
                _vol[i] = v;
                AudioSource s = _src[i];
                s.volume = v * gain;
                if (v <= 0f && t <= 0f && s.isPlaying)
                {
                    s.Stop();
                }
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < 2; i++)
            {
                _src[i].Stop();
                _vol[i] = 0f;
                _target[i] = 0f;
            }
        }
    }
}
