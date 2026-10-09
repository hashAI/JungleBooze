using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Scenery
{
    /// <summary>
    /// Import settings for asset-pipeline's environment assets under <see cref="EnvironmentAssetRules.Root"/>
    /// (ADR 0008). Applied on the first import of a file only (new .meta), so hand changes are kept.
    /// Textures (iOS): albedo ASTC 6x6 sRGB; normal ASTC 5x5; ARM ASTC 8x8 linear; alpha mask ASTC 6x6 linear with
    /// coverage-preserving mips; backdrop layers ASTC 8x8 sRGB with alpha, clamped (ENVIRONMENT_STRATEGY 4.2: 2048x1024).
    /// All max 2048. Models: no materials (the builder maps them to Nature Lit), no cameras, lights, animation or
    /// blend shapes, axis conversion baked, Mikk tangents, medium mesh compression (authored LODs, no generated ones).
    /// </summary>
    public sealed class EnvironmentAssetImportRules : AssetPostprocessor
    {
        private const string IosPlatform = "iPhone";

        private void OnPreprocessTexture()
        {
            if (!EnvironmentAssetRules.IsEnvironmentAsset(assetPath) || !assetImporter.importSettingsMissing)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 2048;
            TextureImporterFormat format;
            switch (EnvironmentAssetRules.TextureKindOf(assetPath))
            {
                case EnvironmentTextureKind.Normal:
                    importer.textureType = TextureImporterType.NormalMap;
                    format = TextureImporterFormat.ASTC_5x5;
                    break;
                case EnvironmentTextureKind.Arm:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    format = TextureImporterFormat.ASTC_8x8;
                    break;
                case EnvironmentTextureKind.Alpha:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.5f;
                    format = TextureImporterFormat.ASTC_6x6;
                    break;
                case EnvironmentTextureKind.Backdrop:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    format = TextureImporterFormat.ASTC_8x8;
                    break;
                default:
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    if (assetPath.Contains("/Plants/"))
                    {
                        // Leaf-card atlases carry their cutout in the albedo's alpha: keep coverage in the mips.
                        importer.alphaIsTransparency = true;
                        importer.mipMapsPreserveCoverage = true;
                        importer.alphaTestReferenceValue = 0.4f;
                    }

                    format = TextureImporterFormat.ASTC_6x6;
                    break;
            }

            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(IosPlatform);
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = format;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
        }

        private void OnPreprocessModel()
        {
            if (!EnvironmentAssetRules.IsEnvironmentAsset(assetPath) || !assetImporter.importSettingsMissing)
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
            // Pieces ship their own LOD0/1/2 meshes; the builder merges LOD0 per segment, so no generated levels.
            importer.generateMeshLods = false;
        }
    }
}
