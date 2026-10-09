using System;
using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// One named sound in the <see cref="AudioCatalog"/>: its variations and how it plays. Ids follow the hook ids of
    /// spec 103 §11 (<c>sfx.jump</c>, <c>amb.forest</c>, <c>mus.sting.discovery</c>, …). Tuning lives here, not in code.
    /// </summary>
    [Serializable]
    public sealed class AudioCue
    {
        public string Id = "sfx.new";

        public AudioBus Bus = AudioBus.Sfx;

        [Tooltip("Variations; one is picked per play (never the same one twice in a row when there are several).")]
        public AudioClip[] Clips = Array.Empty<AudioClip>();

        [Range(0f, 2f)]
        public float Volume = 1f;

        [Tooltip("Random pitch per play, ± semitones (ear-fatigue guard for repeated sounds).")]
        [Range(0f, 3f)]
        public float PitchJitter = 0.5f;

        [Tooltip("Random volume per play, ± dB.")]
        [Range(0f, 6f)]
        public float VolumeJitterDb = 1f;

        [Tooltip("Minimum seconds between two plays of this cue (repeats inside it are dropped).")]
        [Range(0f, 2f)]
        public float Cooldown = 0.03f;

        [Tooltip("Most voices this cue may hold at once; the oldest of them is reused beyond it.")]
        [Range(1, 8)]
        public int MaxVoices = 2;

        [Tooltip("Higher wins when all voices are busy.")]
        [Range(0, 255)]
        public int Priority = 128;

        public CueHaptic Haptic = CueHaptic.None;

        [Tooltip("Looping cue (ambience beds, music layers).")]
        public bool Loop;

        public int ClipCount => Clips == null ? 0 : Clips.Length;
    }
}
