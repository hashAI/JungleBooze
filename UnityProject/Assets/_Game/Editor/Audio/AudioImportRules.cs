using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Audio
{
    /// <summary>
    /// Mobile import settings for everything under <c>Assets/_Game/Audio</c> (applied on import, all platforms + iOS):
    /// Music: Vorbis, streamed (stereo, 44.1 kHz). Ambience: Vorbis, compressed in memory (loops run all the time;
    /// no streaming file I/O). SFX / UI / footsteps / creatures: ADPCM, decompress on load, mono, preloaded (lowest
    /// CPU and latency for short one-shots).
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Audio/";
        public const float MusicQuality = 0.5f;
        public const float AmbienceQuality = 0.4f;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings s = Settings(assetPath);
            importer.forceToMono = s.compressionFormat == AudioCompressionFormat.ADPCM;
            importer.loadInBackground = s.loadType == AudioClipLoadType.Streaming;
            importer.ambisonic = false;
            importer.defaultSampleSettings = s;
            importer.SetOverrideSampleSettings("iOS", s);
        }

        public static AudioImporterSampleSettings Settings(string path)
        {
            AudioImporterSampleSettings s = default;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.preloadAudioData = true;
            if (path.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = MusicQuality;
                s.preloadAudioData = false;
            }
            else if (path.Contains("/Ambience/") && !path.Contains("amb_cue_"))
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = AmbienceQuality;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                s.quality = 1f;
            }

            return s;
        }
    }
}
