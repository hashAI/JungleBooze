using System.IO;
using JungleBooze.Core;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Generates the two small textures the look test needs and no CC0 library offers in a mobile-ready form:
    /// a seamless water normal map (sum of sine waves with whole-number frequencies, so it tiles exactly) and a soft
    /// round mist particle. Written as PNG files under <see cref="Folder"/> and imported with fixed settings.
    /// Deterministic (seeded); editor only.
    /// </summary>
    public static class LookTestTextureGenerator
    {
        public const string Folder = "Assets/_Game/Art/LookTest/Generated";
        public const string WaterNormalPath = Folder + "/WaterNormal.png";
        public const string MistPath = Folder + "/MistSoft.png";

        private const int WaterSize = 512;
        private const int MistSize = 128;
        private const int Waves = 14;

        public static Texture2D EnsureWaterNormal(int seed)
        {
            if (!File.Exists(WaterNormalPath))
            {
                var rng = new Pcg32Random((ulong)(uint)seed, 0x77A7E5UL);
                var kx = new int[Waves];
                var ky = new int[Waves];
                var amp = new float[Waves];
                var phase = new float[Waves];
                for (int i = 0; i < Waves; i++)
                {
                    kx[i] = rng.NextInt(-9, 10);
                    ky[i] = rng.NextInt(1, 12);
                    amp[i] = rng.NextFloat(0.4f, 1f) / Mathf.Sqrt(kx[i] * kx[i] + ky[i] * ky[i]);
                    phase[i] = rng.NextFloat(0f, 6.283f);
                }

                var texture = new Texture2D(WaterSize, WaterSize, TextureFormat.RGB24, false, true);
                var pixels = new Color32[WaterSize * WaterSize];
                const float strength = 0.9f;
                for (int y = 0; y < WaterSize; y++)
                {
                    for (int x = 0; x < WaterSize; x++)
                    {
                        float u = (float)x / WaterSize;
                        float v = (float)y / WaterSize;
                        float dhdu = 0f;
                        float dhdv = 0f;
                        for (int i = 0; i < Waves; i++)
                        {
                            float arg = 2f * Mathf.PI * (kx[i] * u + ky[i] * v) + phase[i];
                            float c = Mathf.Cos(arg) * amp[i] * 2f * Mathf.PI;
                            dhdu += c * kx[i];
                            dhdv += c * ky[i];
                        }

                        Vector3 n = new Vector3(-dhdu * strength * 0.03f, -dhdv * strength * 0.03f, 1f).normalized;
                        pixels[y * WaterSize + x] = new Color32(
                            (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                            (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                            (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                            255);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                Write(texture, WaterNormalPath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(WaterNormalPath);
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                SetIos(importer, TextureImporterFormat.ASTC_5x5);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaterNormalPath);
        }

        public static Texture2D EnsureMist()
        {
            if (!File.Exists(MistPath))
            {
                var texture = new Texture2D(MistSize, MistSize, TextureFormat.RGBA32, false, false);
                var pixels = new Color32[MistSize * MistSize];
                for (int y = 0; y < MistSize; y++)
                {
                    for (int x = 0; x < MistSize; x++)
                    {
                        float dx = (x + 0.5f) / MistSize * 2f - 1f;
                        float dy = (y + 0.5f) / MistSize * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * (3f - 2f * a);
                        pixels[y * MistSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                Write(texture, MistPath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(MistPath);
            if (importer != null && !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                SetIos(importer, TextureImporterFormat.ASTC_6x6);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(MistPath);
        }

        private static void Write(Texture2D texture, string assetPath)
        {
            LookTestAssets.EnsureFolder(Folder);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void SetIos(TextureImporter importer, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(LookTestAssetImportRules.IosPlatform);
            settings.overridden = true;
            settings.maxTextureSize = 512;
            settings.format = format;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
