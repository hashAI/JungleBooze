using System.IO;
using JungleBooze.Core;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Generates the small textures the look test needs and no CC0 library offers in a mobile-ready form:
    /// a seamless water normal map, a water effects texture (falling streaks, lacy foam, breakup) (sum of sine waves with whole-number frequencies, so it tiles exactly), a soft
    /// round mist particle, and the canopy dapple pattern (tiling blobs, rank-equalized so a threshold t lights
    /// exactly 1 - t of the area; JBAtmosphere.hlsl). Written as PNG files under <see cref="Folder"/> and imported with fixed settings.
    /// Deterministic (seeded); editor only.
    /// </summary>
    public static class LookTestTextureGenerator
    {
        public const string Folder = "Assets/_Game/Art/LookTest/Generated";
        public const string WaterNormalPath = Folder + "/WaterNormal.png";
        public const string MistPath = Folder + "/MistSoft.png";
        public const string DapplePath = Folder + "/CanopyDapple.png";
        public const string WaterFxPath = Folder + "/WaterFx.png";

        private const int WaterSize = 512;
        private const int MistSize = 128;
        private const int Waves = 14;
        private const int DappleSize = 256;
        private const int DappleBlobs = 90;
        private const int WaterFxSize = 256;

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

        /// <summary>Tileable sun-fleck pattern for the canopy light (R8, values uniformly distributed in 0..1).</summary>
        public static Texture2D EnsureDapple(int seed)
        {
            if (!File.Exists(DapplePath))
            {
                float[] values = DappleValues(seed, DappleSize, DappleBlobs);
                var texture = new Texture2D(DappleSize, DappleSize, TextureFormat.RGB24, false, true);
                var pixels = new Color32[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    byte v = (byte)Mathf.RoundToInt(values[i] * 255f);
                    pixels[i] = new Color32(v, v, v, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                Write(texture, DapplePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(DapplePath);
            if (importer != null && importer.sRGBTexture)
            {
                importer.textureType = TextureImporterType.SingleChannel;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                SetIos(importer, TextureImporterFormat.R8);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(DapplePath);
        }

        /// <summary>
        /// Dapple values (row-major, size²): a sum of soft round blobs of mixed sizes that wraps at the borders, then
        /// replaced by its rank so the values are uniform in [0, 1]. Pure; unit-tested.
        /// </summary>
        public static float[] DappleValues(int seed, int size, int blobs)
        {
            var rng = new Pcg32Random((ulong)(uint)seed, 0xDA991EUL);
            var field = new float[size * size];
            for (int b = 0; b < blobs; b++)
            {
                float cx = rng.NextFloat(0f, size);
                float cy = rng.NextFloat(0f, size);
                float radius = size * Mathf.Lerp(0.015f, 0.07f, rng.NextFloat() * rng.NextFloat());
                float weight = rng.NextFloat(0.5f, 1f);
                int reach = Mathf.CeilToInt(radius * 2.5f);
                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        float r2 = (dx * dx + dy * dy) / (radius * radius);
                        if (r2 > 6.25f)
                        {
                            continue;
                        }

                        int x = ((Mathf.FloorToInt(cx) + dx) % size + size) % size;
                        int y = ((Mathf.FloorToInt(cy) + dy) % size + size) % size;
                        field[y * size + x] += weight * Mathf.Exp(-r2);
                    }
                }
            }

            // Rank equalization: value = rank / (n - 1).
            var order = new int[field.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            System.Array.Sort((float[])field.Clone(), order);
            var result = new float[field.Length];
            for (int rank = 0; rank < order.Length; rank++)
            {
                result[order[rank]] = rank / (float)(order.Length - 1);
            }

            return result;
        }

        /// <summary>
        /// Tileable water effects texture (linear RGBA, <see cref="WaterFxSize"/>²) for the waterfall and water shaders:
        /// R = falling-water streaks (fine across u, long along v), G = lacy foam (ridged noise: thin bright lines),
        /// B = soft breakup (low frequency), A = 1.
        /// </summary>
        public static Texture2D EnsureWaterFx(int seed)
        {
            if (!File.Exists(WaterFxPath))
            {
                Color[] values = WaterFxValues(seed, WaterFxSize);
                var texture = new Texture2D(WaterFxSize, WaterFxSize, TextureFormat.RGBA32, false, true);
                var pixels = new Color32[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    pixels[i] = values[i];
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                Write(texture, WaterFxPath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(WaterFxPath);
            if (importer != null && importer.sRGBTexture)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                SetIos(importer, TextureImporterFormat.ASTC_6x6);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(WaterFxPath);
        }

        /// <summary>Water FX values (row-major, size², each channel in 0..1); tiles exactly. Pure; unit-tested.</summary>
        public static Color[] WaterFxValues(int seed, int size)
        {
            uint h = (uint)seed * 2654435761u + 0x5EEDu;
            var result = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float streak = 0.5f * PeriodicNoise(u, v, 32, 2, h) + 0.3f * PeriodicNoise(u, v, 64, 4, h + 11u) + 0.2f * PeriodicNoise(u, v, 16, 1, h + 23u);
                    streak = LookTestMath.Smooth(0.3f, 0.72f, streak);
                    float foam = 0f;
                    float amplitude = 0.55f;
                    int period = 6;
                    for (int o = 0; o < 3; o++)
                    {
                        float n = PeriodicNoise(u, v, period, period, h + 101u + (uint)o * 7u);
                        float ridge = 1f - Mathf.Abs(2f * n - 1f);
                        foam += amplitude * ridge * ridge * ridge;
                        amplitude *= 0.55f;
                        period *= 2;
                    }

                    foam = LookTestMath.Smooth(0.25f, 0.62f, foam);
                    float breakup = 0.65f * PeriodicNoise(u, v, 4, 4, h + 211u) + 0.35f * PeriodicNoise(u, v, 8, 8, h + 223u);
                    result[y * size + x] = new Color(streak, foam, breakup, 1f);
                }
            }

            return result;
        }

        /// <summary>Value noise in 0..1 on a lattice of periodX × periodY cells over the unit square (wraps).</summary>
        public static float PeriodicNoise(float u, float v, int periodX, int periodY, uint seed)
        {
            float fx = u * periodX;
            float fy = v * periodY;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Lattice(x0, y0, periodX, periodY, seed);
            float b = Lattice(x0 + 1, y0, periodX, periodY, seed);
            float c = Lattice(x0, y0 + 1, periodX, periodY, seed);
            float d = Lattice(x0 + 1, y0 + 1, periodX, periodY, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Lattice(int x, int y, int periodX, int periodY, uint seed)
        {
            uint ix = (uint)(((x % periodX) + periodX) % periodX);
            uint iy = (uint)(((y % periodY) + periodY) % periodY);
            uint k = ix * 374761393u + iy * 668265263u + seed * 2246822519u;
            k = (k ^ (k >> 13)) * 1274126177u;
            k ^= k >> 16;
            return (k & 0xFFFFFFu) / 16777215f;
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
