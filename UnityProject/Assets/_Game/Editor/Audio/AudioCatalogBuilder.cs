using System;
using System.Collections.Generic;
using System.IO;
using JungleBooze.Services.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Editor.Audio
{
    /// <summary>
    /// Builds <c>Config/Audio/Resources/AudioCatalog.asset</c> from the clips in <c>Assets/_Game/Audio</c>
    /// (file <c>sfx_land_hard_02.ogg</c> → cue <c>sfx.land.hard</c>, variation 2). New cues get the defaults below;
    /// cues that already exist keep their hand-tuned values and only get their clip lists refreshed.
    /// Menu: JungleBooze/Audio/Build Audio Catalog. Batch: <c>-executeMethod JungleBooze.Editor.Audio.AudioCatalogBuilder.Build</c>.
    /// </summary>
    public static class AudioCatalogBuilder
    {
        public const string AudioRoot = "Assets/_Game/Audio";
        public const string CatalogPath = "Assets/_Game/Config/Audio/Resources/AudioCatalog.asset";
        public const string MixerPath = "Assets/_Game/Audio/AureliaMixer.mixer";

        /// <summary>Clips that belong to the music setup, not to cues.</summary>
        public static readonly string[] MusicSetupIds =
        {
            "mus.theme.intro", "mus.explore.melody", "mus.explore.drums", "mus.danger", "mus.layer.perc", "mus.results",
        };

        [MenuItem("JungleBooze/Audio/Build Audio Catalog")]
        public static void Build()
        {
            var clips = new SortedDictionary<string, List<AudioClip>>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                string id = IdFromFile(Path.GetFileNameWithoutExtension(path));
                if (!clips.TryGetValue(id, out List<AudioClip> list))
                {
                    list = new List<AudioClip>();
                    clips[id] = list;
                }

                list.Add(clip);
            }

            foreach (List<AudioClip> list in clips.Values)
            {
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            bool created = catalog == null;
            if (created)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            }

            var cues = new List<AudioCue>();
            foreach (KeyValuePair<string, List<AudioClip>> kv in clips)
            {
                if (Array.IndexOf(MusicSetupIds, kv.Key) >= 0)
                {
                    continue;
                }

                AudioCue cue = catalog.Find(kv.Key) ?? Defaults(kv.Key);
                cue.Clips = kv.Value.ToArray();
                cues.Add(cue);
            }

            catalog.Cues = cues;
            MusicSetup m = catalog.Music ?? new MusicSetup();
            m.Intro = First(clips, "mus.theme.intro");
            m.Layers = new[] { First(clips, "mus.explore.melody"), First(clips, "mus.explore.drums"), First(clips, "mus.danger"), First(clips, "mus.layer.perc") };
            m.Results = First(clips, "mus.results");
            catalog.Music = m;
            catalog.Mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (created)
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            List<string> problems = catalog.Validate();
            Debug.Log("[JungleBooze] Audio catalog: " + cues.Count + " cues, mixer " + (catalog.Mixer != null ? "ok" : "MISSING") +
                      (problems.Count == 0 ? ", valid." : ", problems: " + string.Join(" ", problems)));
        }

        /// <summary><c>sfx_land_hard_02</c> → <c>sfx.land.hard</c>.</summary>
        public static string IdFromFile(string name)
        {
            int cut = name.Length;
            int u = name.LastIndexOf('_');
            if (u > 0 && u + 1 < name.Length && int.TryParse(name.Substring(u + 1), out _))
            {
                cut = u;
            }

            return name.Substring(0, cut).Replace('_', '.');
        }

        public static AudioCue Defaults(string id)
        {
            var c = new AudioCue { Id = id, Bus = BusFor(id) };
            switch (c.Bus)
            {
                case AudioBus.Ui:
                    c.Volume = 0.8f;
                    c.PitchJitter = 0.2f;
                    c.VolumeJitterDb = 0.5f;
                    c.Cooldown = 0.05f;
                    c.MaxVoices = 2;
                    c.Priority = 160;
                    break;
                case AudioBus.Music:
                    c.Volume = 0.9f;
                    c.PitchJitter = 0f;
                    c.VolumeJitterDb = 0f;
                    c.Cooldown = 0.5f;
                    c.MaxVoices = 1;
                    c.Priority = 0;
                    break;
                case AudioBus.Ambience:
                    c.Volume = 1f;
                    c.PitchJitter = 0f;
                    c.VolumeJitterDb = 0f;
                    c.Cooldown = 1f;
                    c.MaxVoices = 1;
                    c.Priority = 64;
                    c.Loop = !id.StartsWith("amb.cue.", StringComparison.Ordinal);
                    break;
            }

            if (id.StartsWith("sfx.step.", StringComparison.Ordinal))
            {
                c.Volume = 0.55f;
                c.PitchJitter = 0.8f;
                c.VolumeJitterDb = 2f;
                c.Cooldown = 0.12f;
                c.Priority = 60;
            }

            switch (id)
            {
                case "sfx.coin":
                    c.Volume = 0.55f;
                    c.PitchJitter = 0.15f;
                    c.Cooldown = 0.02f;
                    c.MaxVoices = 4;
                    c.Priority = 100;
                    break;
                case "sfx.jump":
                case "sfx.land":
                    c.Volume = 0.8f;
                    break;
                case "sfx.edgeBrush":
                    c.Volume = 0.6f;
                    c.Cooldown = 0.25f;
                    c.MaxVoices = 1;
                    c.PitchJitter = 1f;
                    break;
                case "sfx.swim.stroke":
                    c.Volume = 0.6f;
                    c.Cooldown = 0.3f;
                    break;
                case "sfx.vine.creak":
                    c.Volume = 0.7f;
                    break;
                case "sfx.hit.minor":
                case "sfx.hit.thorns":
                case "sfx.crash":
                case "sfx.bump.water":
                    c.Priority = 200;
                    break;
                case "sfx.fall":
                case "sfx.fail.crash":
                    c.Priority = 220;
                    c.PitchJitter = 0f;
                    break;
                case "sfx.sailback.chirp":
                    c.Volume = 0.85f;
                    c.PitchJitter = 1f;
                    c.Cooldown = 0.25f;
                    break;
                case "sfx.sailback.distant":
                    c.Volume = 0.7f;
                    c.Cooldown = 3f;
                    c.MaxVoices = 1;
                    break;
                case "ui.countUp":
                    c.Volume = 0.5f;
                    c.Cooldown = 0.04f;
                    break;
                case "amb.river":
                case "amb.rapids":
                    c.Volume = 0.9f;
                    break;
                case "amb.falls.roar":
                    c.Volume = 0.8f;
                    break;
                case "amb.secret.dripEcho":
                    c.Volume = 0.6f;
                    break;
                case "amb.cue.risky":
                    c.Volume = 0.7f;
                    break;
            }

            c.Haptic = HapticFor(id);
            return c;
        }

        /// <summary>Spec 103 §11 haptic pairing (landings and hits are owned by the run feedback router).</summary>
        public static CueHaptic HapticFor(string id)
        {
            switch (id)
            {
                case "sfx.crystal":
                case "sfx.cleanLine":
                case "sfx.water.enter":
                case "sfx.splash":
                case "sfx.vine.grab":
                case "sfx.beam.land":
                case "sfx.curtain.pass":
                case "sfx.shield.pickup":
                case "ui.objective":
                    return CueHaptic.Light;
                case "sfx.shield.break":
                    return CueHaptic.Medium;
                case "sfx.perfect":
                case "ui.learn":
                    return CueHaptic.Success;
                default:
                    return CueHaptic.None;
            }
        }

        public static AudioBus BusFor(string id)
        {
            if (id.StartsWith("ui.", StringComparison.Ordinal))
            {
                return AudioBus.Ui;
            }

            if (id.StartsWith("mus.", StringComparison.Ordinal))
            {
                return AudioBus.Music;
            }

            return id.StartsWith("amb.", StringComparison.Ordinal) ? AudioBus.Ambience : AudioBus.Sfx;
        }

        private static AudioClip First(SortedDictionary<string, List<AudioClip>> clips, string id)
        {
            return clips.TryGetValue(id, out List<AudioClip> list) && list.Count > 0 ? list[0] : null;
        }
    }
}
