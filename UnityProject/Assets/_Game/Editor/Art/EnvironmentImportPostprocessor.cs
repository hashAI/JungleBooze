using UnityEditor;

namespace JungleBooze.Editor.Art
{
    /// <summary>
    /// Import presets for <c>Art/Environment/Models</c>: static meshes without embedded materials (the prefab builder
    /// assigns a URP material from the sibling PNG, so nothing renders pink), and ASTC textures up to 1024. Also asks
    /// <see cref="EnvironmentAutoBuild"/> to rebuild the prefabs after any model or texture there is imported.
    /// </summary>
    public sealed class EnvironmentImportPostprocessor : AssetPostprocessor
    {
        private const int MaxTextureSize = 1024;

        private static bool IsEnvironmentAsset(string path)
        {
            return path.StartsWith(EnvironmentPrefabBuilder.ModelsFolder + "/", System.StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (!IsEnvironmentAsset(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessTexture()
        {
            if (!IsEnvironmentAsset(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                maxTextureSize = MaxTextureSize,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
            });
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            for (int i = 0; i < imported.Length; i++)
            {
                string path = imported[i];
                if (!IsEnvironmentAsset(path))
                {
                    continue;
                }

                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                {
                    EnvironmentAutoBuild.Request(true);
                    return;
                }
            }
        }
    }
}
