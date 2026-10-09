using JungleBooze.Services.Audio;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Audio
{
    /// <summary>Voice pool double: records starts; a voice "plays" until <see cref="Finish"/>.</summary>
    internal sealed class FakeVoices : IAudioVoices
    {
        public readonly bool[] Playing;
        public readonly AudioClip[] Clip;
        public readonly float[] Volume;
        public readonly float[] Pitch;
        public int Starts;

        public FakeVoices(int count)
        {
            Playing = new bool[count];
            Clip = new AudioClip[count];
            Volume = new float[count];
            Pitch = new float[count];
        }

        public int Count => Playing.Length;

        public bool IsPlaying(int voice) => Playing[voice];

        public void Start(int voice, AudioClip clip, AudioBus bus, float volume, float pitch)
        {
            Playing[voice] = true;
            Clip[voice] = clip;
            Volume[voice] = volume;
            Pitch[voice] = pitch;
            Starts++;
        }

        public void Stop(int voice)
        {
            Playing[voice] = false;
        }

        public void Finish(int voice)
        {
            Playing[voice] = false;
        }
    }
}
