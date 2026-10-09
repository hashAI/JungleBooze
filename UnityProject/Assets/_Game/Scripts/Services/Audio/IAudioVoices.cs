using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>A fixed pool of one-shot voices (AudioSources on device, a fake in tests).</summary>
    public interface IAudioVoices
    {
        int Count { get; }

        bool IsPlaying(int voice);

        void Start(int voice, AudioClip clip, AudioBus bus, float volume, float pitch);

        void Stop(int voice);
    }
}
