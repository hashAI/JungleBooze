using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Plays the adaptive music: the intro, then the bar-locked loop layers (scheduled on one DSP start time so they
    /// stay sample-aligned), the results loop and stings (which duck the music). Volumes come from
    /// <see cref="MusicMix"/>. Music ignores the listener pause, so the pause menu keeps a soft bed.
    /// </summary>
    public sealed class MusicPlayer
    {
        private const double ScheduleLead = 0.1;

        private readonly MusicSetup _setup;
        private readonly AudioSource _intro;
        private readonly AudioSource[] _layers = new AudioSource[MusicSetup.LayerCount];
        private readonly AudioLowPassFilter[] _layerLowPass = new AudioLowPassFilter[MusicSetup.LayerCount];
        private readonly AudioSource _results;
        private readonly AudioSource _sting;
        private float _resultsVol;
        private float _resultsTarget;
        private bool _loopsRunning;
        private bool _underwater;

        public MusicPlayer(Transform parent, MusicSetup setup, AudioMixerGroup group)
        {
            _setup = setup;
            Mix = new MusicMix(setup);
            var root = new GameObject("Music");
            root.transform.SetParent(parent, false);
            _intro = NewSource(root.transform, "Intro", group, false);
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _layers[i] = NewSource(root.transform, "Layer" + i, group, true);
                _layerLowPass[i] = _layers[i].gameObject.AddComponent<AudioLowPassFilter>();
                _layerLowPass[i].cutoffFrequency = 22000f;
                _layerLowPass[i].enabled = false;
            }

            _results = NewSource(root.transform, "Results", group, true);
            _sting = NewSource(root.transform, "Sting", group, false);
        }

        public MusicMix Mix { get; }

        public bool LoopsRunning => _loopsRunning;

        public bool StingPlaying => _sting.isPlaying;

        /// <summary>Extra duck (pause menu), combined with the sting duck.</summary>
        public bool Ducked { get; set; }

        /// <summary>Starts the run music: the intro first when asked, then the loops at intensity 1.</summary>
        public void StartRun(bool withIntro)
        {
            StopAll();
            double dsp = AudioSettings.dspTime + ScheduleLead;
            double loopStart = dsp;
            if (withIntro && _setup.Intro != null)
            {
                _intro.clip = _setup.Intro;
                _intro.volume = 1f;
                _intro.PlayScheduled(dsp);
                loopStart = dsp + ((double)_setup.Intro.samples / _setup.Intro.frequency);
            }

            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                AudioClip clip = _setup.Layers != null && i < _setup.Layers.Length ? _setup.Layers[i] : null;
                if (clip == null)
                {
                    continue;
                }

                _layers[i].clip = clip;
                _layers[i].PlayScheduled(loopStart);
            }

            Mix.LoopStart = loopStart;
            Mix.Snap(1);
            _loopsRunning = true;
            _resultsTarget = 0f;
        }

        public void SetLevel(int level)
        {
            Mix.SetLevel(level, AudioSettings.dspTime);
        }

        public void PlaySting(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            _sting.Stop();
            _sting.clip = clip;
            _sting.volume = volume;
            _sting.Play();
        }

        /// <summary>Death: the loops fade out (spec 103 §11 <c>mus.stop.soft</c>).</summary>
        public void StopSoft()
        {
            Mix.SoftStop();
        }

        /// <summary>Results: the loops stop, the results loop fades in.</summary>
        public void PlayResults()
        {
            Mix.SoftStop();
            if (_setup.Results == null)
            {
                return;
            }

            _results.clip = _setup.Results;
            _results.volume = 0f;
            _resultsVol = 0f;
            _resultsTarget = 1f;
            _results.Play();
        }

        /// <summary>Muffles the music under water and behind the waterfall curtain.</summary>
        public void SetUnderwater(bool on, float cutoff)
        {
            if (_underwater == on)
            {
                return;
            }

            _underwater = on;
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _layerLowPass[i].enabled = on;
                _layerLowPass[i].cutoffFrequency = on ? cutoff : 22000f;
            }
        }

        public void Update(float dt, float busGain)
        {
            Mix.Duck(_sting.isPlaying || Ducked);
            Mix.Update(AudioSettings.dspTime, dt);
            float master = Mix.Master * busGain;
            if (_loopsRunning)
            {
                for (int i = 0; i < MusicSetup.LayerCount; i++)
                {
                    _layers[i].volume = Mix.Layer(i) * master;
                }

                _intro.volume = master;
                if (Mix.Stopped)
                {
                    _intro.Stop();
                    for (int i = 0; i < MusicSetup.LayerCount; i++)
                    {
                        _layers[i].Stop();
                    }

                    _loopsRunning = false;
                }
            }

            float step = dt / 1.5f;
            _resultsVol = _resultsVol < _resultsTarget ? Mathf.Min(_resultsTarget, _resultsVol + step) : Mathf.Max(_resultsTarget, _resultsVol - step);
            _results.volume = _resultsVol * (_sting.isPlaying || Ducked ? _setup.StingDuck : 1f) * busGain;
            if (_resultsVol <= 0f && _resultsTarget <= 0f && _results.isPlaying)
            {
                _results.Stop();
            }
        }

        public void StopAll()
        {
            _intro.Stop();
            for (int i = 0; i < MusicSetup.LayerCount; i++)
            {
                _layers[i].Stop();
            }

            _results.Stop();
            _sting.Stop();
            _resultsVol = 0f;
            _resultsTarget = 0f;
            _loopsRunning = false;
        }

        private static AudioSource NewSource(Transform parent, string name, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.priority = 0;
            s.ignoreListenerPause = true;
            s.outputAudioMixerGroup = group;
            return s;
        }
    }
}
