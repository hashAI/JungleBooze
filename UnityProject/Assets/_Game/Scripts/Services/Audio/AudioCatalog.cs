using System;
using System.Collections.Generic;
using UnityEngine;

namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Every sound of the game by id (spec 103 §11 hook ids), plus the adaptive music setup. Built by the editor tool
    /// <c>JungleBooze/Audio/Build Audio Catalog</c> from <c>Assets/_Game/Audio</c>; tuning (volume, jitter, cooldowns,
    /// haptics) is edited on the asset. Lookups by id happen once at startup (<see cref="IndexOf"/>); play calls use
    /// the int index.
    /// </summary>
    [CreateAssetMenu(menuName = "JungleBooze/Audio/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        public List<AudioCue> Cues = new List<AudioCue>();

        public MusicSetup Music = new MusicSetup();

        [Tooltip("Optional mixer; without it bus volumes are applied per source.")]
        public UnityEngine.Audio.AudioMixer Mixer;

        public int Count => Cues.Count;

        public AudioCue this[int index] => Cues[index];

        /// <summary>Index of the cue with <paramref name="id"/>, or −1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Cues.Count; i++)
            {
                if (Cues[i] != null && string.Equals(Cues[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public AudioCue Find(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? Cues[i] : null;
        }

        /// <summary>Data problems (empty = valid): duplicate or empty ids, cues without clips, bad tuning, music gaps.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (AudioCue c in Cues)
            {
                if (c == null || string.IsNullOrEmpty(c.Id))
                {
                    problems.Add("Cue without id.");
                    continue;
                }

                if (!seen.Add(c.Id))
                {
                    problems.Add("Duplicate cue id " + c.Id + ".");
                }

                if (c.ClipCount == 0)
                {
                    problems.Add(c.Id + ": no clips.");
                }
                else
                {
                    foreach (AudioClip clip in c.Clips)
                    {
                        if (clip == null)
                        {
                            problems.Add(c.Id + ": missing clip reference.");
                        }
                    }
                }

                if (c.Volume <= 0f || c.MaxVoices < 1 || c.Cooldown < 0f || c.PitchJitter < 0f)
                {
                    problems.Add(c.Id + ": invalid tuning.");
                }
            }

            MusicSetup m = Music;
            if (m == null || m.Layers == null || m.Layers.Length != MusicSetup.LayerCount)
            {
                problems.Add("Music: need " + MusicSetup.LayerCount + " layers.");
            }
            else
            {
                for (int i = 0; i < m.Layers.Length; i++)
                {
                    if (m.Layers[i] == null)
                    {
                        problems.Add("Music: layer " + i + " missing.");
                    }
                }

                if (m.LevelVolumes == null || m.LevelVolumes.Length != MusicSetup.LayerCount * MusicSetup.LevelCount)
                {
                    problems.Add("Music: level table must have " + (MusicSetup.LayerCount * MusicSetup.LevelCount) + " values.");
                }

                if (m.BarSeconds <= 0f)
                {
                    problems.Add("Music: bar length must be positive.");
                }
            }

            return problems;
        }
    }
}
