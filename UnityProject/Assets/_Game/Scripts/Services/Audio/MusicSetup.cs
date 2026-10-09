using System;
using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Adaptive music (Blueprint Part XXXIV, GDD §19): an intro that flows into synced, bar-locked loops. Layers
    /// play together from one DSP start time; the intensity level sets each layer's volume. Level 0 recovery,
    /// 1 explore (default), 2 risky branch (+1 layer), 3 danger (gauntlet). Layer order:
    /// 0 melody (explore), 1 drums (explore), 2 danger section, 3 percussion.
    /// </summary>
    [Serializable]
    public sealed class MusicSetup
    {
        public const int LayerCount = 4;
        public const int LevelCount = 4;

        public AudioClip Intro;

        [Tooltip("Synced loop layers: melody, drums, danger, percussion. Lengths must be whole bars.")]
        public AudioClip[] Layers = new AudioClip[LayerCount];

        public AudioClip Results;

        [Tooltip("Seconds per bar (120 BPM 4/4 = 2).")]
        public float BarSeconds = 2f;

        [Tooltip("Layer volumes per level, row-major [level * 4 + layer].")]
        public float[] LevelVolumes =
        {
            0.85f, 0f, 0f, 0f,     // 0 recovery: melody only
            1f, 0.5f, 0f, 0f,      // 1 explore (drums −6 dB)
            1f, 0.9f, 0f, 0.7f,    // 2 risky: drums up, percussion in
            0f, 0f, 1f, 0.9f,      // 3 danger: danger section + percussion
        };

        [Tooltip("Seconds for layer volume changes (vertical).")]
        public float LayerFade = 1.5f;

        [Tooltip("Seconds for the switch to/from the danger section; it starts on the next bar line.")]
        public float SectionFade = 0.5f;

        [Tooltip("Music bus ducking while a sting plays (linear gain).")]
        [Range(0f, 1f)]
        public float StingDuck = 0.35f;

        public float StingDuckFade = 0.25f;

        [Tooltip("Soft stop on death (seconds).")]
        public float StopFade = 1.2f;

        public float Volume(int level, int layer)
        {
            if (LevelVolumes == null || level < 0 || layer < 0 || layer >= LayerCount)
            {
                return 0f;
            }

            int i = (Math.Min(level, LevelCount - 1) * LayerCount) + layer;
            return i < LevelVolumes.Length ? LevelVolumes[i] : 0f;
        }
    }
}
