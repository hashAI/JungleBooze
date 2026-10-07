using UnityEditor;
using UnityEngine;

namespace JungleBooze.CharacterArt
{
    /// <summary>
    /// Import presets for everything under Art/Characters: Generic rigs, mesh compression, loop flags on the run and
    /// idle clips, no materials on the animation-only files, and ASTC textures capped by budget (Pista 1024, Duko 512).
    /// </summary>
    public sealed class CharacterImportPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/_Game/Art/Characters/";

        private static bool IsCharacterAsset(string path)
        {
            return path.StartsWith(Root, System.StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (!IsCharacterAsset(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool isMain = file == "Pista" || file == "Duko";

            importer.globalScale = 1f;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = file != "Duko";

            if (!isMain)
            {
                // Animation-only files: the clip is used on the main model's skeleton, no geometry or materials needed.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsCharacterAsset(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool loops = file.EndsWith("_run", System.StringComparison.Ordinal)
                || file.EndsWith("_idle", System.StringComparison.Ordinal)
                || file.EndsWith("_walk", System.StringComparison.Ordinal);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loops;
                clips[i].loopPose = loops;
            }

            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!IsCharacterAsset(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            int maxSize = assetPath.Contains("/Duko/") ? 512 : 1024;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                maxTextureSize = maxSize,
                format = TextureImporterFormat.ASTC_4x4,
                textureCompression = TextureImporterCompression.Compressed,
            });
        }
    }
}
