using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Import settings for the CC0 assets fetched by <c>tools/assets/fetch_cc0.py</c> into
    /// <see cref="CC0Root"/> (ADR 0004, texture compression). Applied only on the first import of a file (when its
    /// .meta is new), so later hand changes in the Inspector are kept.
    /// Textures by file-name suffix (Poly Haven naming):
    ///   _diff / _diffuse: color, sRGB, ASTC 6x6 on iOS;
    ///   _nor_gl: normal map, ASTC 5x5 (6x6 shows block noise on normals);
    ///   _arm / _rough / _ao: linear data, ASTC 8x8;
    ///   _alpha: linear mask, ASTC 6x6;
    ///   .hdr in HDRI/: cubemap (from the lat-long image), RGB9E5 on iOS (32 bits per pixel, HDR, works on every
    ///   Metal GPU).
    /// Models: no materials (the scene builder makes them), no cameras, lights, animation or blend shapes,
    /// axis conversion baked, Mikk tangents, medium mesh compression.
    /// </summary>
    public sealed class LookTestAssetImportRules : AssetPostprocessor
    {
        public const string CC0Root = "Assets/_Game/Art/CC0/";
        public const string IosPlatform = "iPhone";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(CC0Root, System.StringComparison.Ordinal) || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool isHdri = assetPath.EndsWith(".hdr", System.StringComparison.OrdinalIgnoreCase);

            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;

            if (isHdri)
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.TextureCube;
                importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                SetIos(importer, 2048, TextureImporterFormat.RGB9E5);
                return;
            }

            TextureImporterFormat iosFormat;
            if (file.Contains("_nor_gl"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                iosFormat = TextureImporterFormat.ASTC_5x5;
            }
            else if (file.Contains("_arm") || file.Contains("_rough") || file.Contains("_ao_"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                iosFormat = TextureImporterFormat.ASTC_8x8;
            }
            else if (file.Contains("_alpha"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                iosFormat = TextureImporterFormat.ASTC_6x6;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                iosFormat = TextureImporterFormat.ASTC_6x6;
            }

            importer.anisoLevel = assetPath.Contains("/Textures/") ? 2 : 1;
            importer.maxTextureSize = 2048;
            SetIos(importer, 2048, iosFormat);
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CC0Root, System.StringComparison.Ordinal) || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.bakeAxisConversion = true;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.addCollider = false;

            // Unity 6 import-time Mesh LOD (ADR 0004 follow-up): the dense CC0 scans get automatic simplified
            // levels, chosen per renderer by screen size, so distant plants and rocks cost a fraction of LOD0.
            importer.generateMeshLods = true;
        }

        private static void SetIos(TextureImporter importer, int maxSize, TextureImporterFormat format)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(IosPlatform);
            settings.overridden = true;
            settings.maxTextureSize = maxSize;
            settings.format = format;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
