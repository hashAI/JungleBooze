using System;
using System.IO;
using JungleBooze.App.Audio;
using JungleBooze.Editor.Audio;
using JungleBooze.Services.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace JungleBooze.Tests.EditMode.Audio
{
    /// <summary>
    /// The shipped audio data: catalog validity, every expedition hook has a sound, bar-exact music loops, mobile
    /// import settings, the size budget, the mixer, and the iOS audio session setting.
    /// </summary>
    public sealed class AudioCatalogTests
    {
        public const long SizeBudgetBytes = 15L * 1024 * 1024;

        private static AudioCatalog Load()
        {
            var cat = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioCatalogBuilder.CatalogPath);
            Assert.IsNotNull(cat, "run JungleBooze/Audio/Build Audio Catalog");
            return cat;
        }

        [Test]
        public void CatalogIsValidAndLoadsFromResources()
        {
            AudioCatalog cat = Load();
            Assert.IsEmpty(cat.Validate(), string.Join("\n", cat.Validate()));
            Assert.AreSame(cat, Resources.Load<AudioCatalog>(ExpeditionAudio.CatalogResource));
        }

        [Test]
        public void EveryExpeditionHookHasASound()
        {
            AudioCatalog cat = Load();
            foreach (string id in ExpeditionAudioMap.All)
            {
                AudioCue cue = cat.Find(id);
                Assert.IsNotNull(cue, id);
                Assert.Greater(cue.ClipCount, 0, id);
                Assert.AreEqual(AudioCatalogBuilder.BusFor(id), cue.Bus, id);
            }
        }

        [Test]
        public void RepeatedSoundsHaveVariationOrJitter()
        {
            // Ear-fatigue guard: sounds that repeat a lot need several takes or pitch/volume jitter.
            AudioCatalog cat = Load();
            string[] frequent = { "sfx.coin", "sfx.jump", "sfx.land", "sfx.dodge", "sfx.step.dirt", "sfx.step.moss", "sfx.step.wood", "sfx.step.shallow", "sfx.edgeBrush", "sfx.swim.stroke" };
            foreach (string id in frequent)
            {
                AudioCue c = cat.Find(id);
                Assert.IsTrue(c.ClipCount >= 2 || c.PitchJitter > 0f, id);
                Assert.IsTrue(c.PitchJitter > 0f || c.VolumeJitterDb > 0f, id + " needs jitter");
            }

            Assert.GreaterOrEqual(cat.Find("sfx.step.dirt").ClipCount, 4, "footsteps need several takes");
        }

        [Test]
        public void AmbienceBedsLoopAndOneShotsDont()
        {
            AudioCatalog cat = Load();
            foreach (AudioCue c in cat.Cues)
            {
                bool bed = c.Id.StartsWith("amb.", StringComparison.Ordinal) && !c.Id.StartsWith("amb.cue.", StringComparison.Ordinal);
                Assert.AreEqual(bed, c.Loop, c.Id);
            }
        }

        [Test]
        public void MusicLayersAreWholeBarsAndStayAligned()
        {
            MusicSetup m = Load().Music;
            Assert.AreEqual(2f, m.BarSeconds, 1e-6f, "120 BPM 4/4");
            int rate = m.Layers[0].frequency;
            long bar = (long)(m.BarSeconds * rate);
            foreach (AudioClip c in m.Layers)
            {
                Assert.AreEqual(rate, c.frequency, c.name);
                Assert.AreEqual(0, c.samples % bar, c.name + " is not a whole number of bars");
            }

            Assert.AreEqual(m.Layers[0].samples, m.Layers[1].samples, "explore melody and drums are the same loop");
            Assert.AreEqual(m.Layers[2].samples, m.Layers[3].samples, "danger and percussion are the same loop");
            Assert.AreEqual(0, m.Intro.samples % bar, "the intro ends on a bar line so the loops start on the beat");
            Assert.AreEqual(0, m.Results.samples % bar);
            for (int level = 0; level < MusicSetup.LevelCount; level++)
            {
                float sum = 0f;
                for (int layer = 0; layer < MusicSetup.LayerCount; layer++)
                {
                    float v = m.Volume(level, layer);
                    Assert.That(v, Is.InRange(0f, 1f), "AudioSource volume can't exceed 1");
                    sum += v;
                }

                Assert.Greater(sum, 0f, "level " + level + " is silent");
            }
        }

        [Test]
        public void ImportSettingsFollowTheMobileRules()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioCatalogBuilder.AudioRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings want = AudioImportRules.Settings(path);
                AudioImporterSampleSettings ios = importer.GetOverrideSampleSettings("iOS");
                Assert.AreEqual(want.loadType, ios.loadType, path);
                Assert.AreEqual(want.compressionFormat, ios.compressionFormat, path);
                Assert.AreEqual(want.loadType, importer.defaultSampleSettings.loadType, path);
                if (path.Contains("/Music/"))
                {
                    Assert.AreEqual(AudioClipLoadType.Streaming, ios.loadType, path);
                }
                else if (want.compressionFormat == AudioCompressionFormat.ADPCM)
                {
                    Assert.IsTrue(importer.forceToMono, path + " (SFX are mono)");
                }
            }
        }

        [Test]
        public void AudioStaysInsideTheSizeBudget()
        {
            long total = 0;
            foreach (string f in Directory.GetFiles(AudioCatalogBuilder.AudioRoot, "*.ogg", SearchOption.AllDirectories))
            {
                total += new FileInfo(f).Length;
            }

            Assert.Greater(total, 0);
            Assert.LessOrEqual(total, SizeBudgetBytes, (total / 1024) + " KB");
        }

        [Test]
        public void MixerHasTheBusGroupsAndExposedVolumes()
        {
            AudioMixer mixer = Load().Mixer;
            Assert.IsNotNull(mixer, AudioCatalogBuilder.MixerPath);
            foreach (string g in AudioService.GroupNames)
            {
                Assert.IsNotEmpty(mixer.FindMatchingGroups(g), g);
            }

            foreach (string p in AudioService.VolumeParams)
            {
                Assert.IsTrue(mixer.GetFloat(p, out _), p + " is not exposed");
            }
        }

        [Test]
        public void IosAudioSessionMixesWithOtherAppsAndRespectsTheSilentSwitch()
        {
            // "Mute Other Audio Sources" off → AVAudioSessionCategoryAmbient: other apps' audio keeps playing and the
            // silent switch mutes the game.
            Assert.IsFalse(PlayerSettings.muteOtherAudioSources);
            string audioManager = File.ReadAllText("ProjectSettings/AudioManager.asset");
            StringAssert.Contains("m_DSPBufferSize: 512", audioManager, "low-latency DSP buffer (SFX ≤ 1 frame late)");
        }

        [Test]
        public void FileNamesMapToCueIds()
        {
            Assert.AreEqual("sfx.land.hard", AudioCatalogBuilder.IdFromFile("sfx_land_hard_02"));
            Assert.AreEqual("amb.secret.dripEcho", AudioCatalogBuilder.IdFromFile("amb_secret_dripEcho"));
            Assert.AreEqual("mus.sting.discovery", AudioCatalogBuilder.IdFromFile("mus_sting_discovery"));
            Assert.AreEqual(AudioBus.Ui, AudioCatalogBuilder.BusFor("ui.tap"));
            Assert.AreEqual(CueHaptic.Success, AudioCatalogBuilder.HapticFor("sfx.perfect"));
        }
    }
}
