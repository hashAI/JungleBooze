using UnityEditor;
using UnityEngine;

namespace JungleBooze.EnvironmentImport
{
    /// <summary>
    /// Import presets for the environment models in <c>Art/Environment/Resources/EnvironmentArt</c>. The run views load
    /// each FBX straight from Resources (no prefab build step). Models import as static meshes without cameras, lights
    /// or animation, and after import each FBX is remapped to a URP Simple Lit material made from its sibling
    /// <c>&lt;Name&gt;_basecolor.png</c> (the same approach as the characters). Materials live in
    /// <c>Art/Environment/Materials</c>. Textures are ASTC and capped at 1024. If this never runs, the runtime
    /// fallback in <c>EnvironmentArt.cs</c> still builds a textured material.
    /// </summary>
    public sealed class EnvironmentImportPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/_Game/Art/Environment/Resources/EnvironmentArt/";
        private const string MaterialsFolder = "Assets/_Game/Art/Environment/Materials";
        private const int MaxTextureSize = 1024;

        private static bool IsEnvironmentAsset(string path)
        {
            return path.StartsWith(Root, System.StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            if (!IsEnvironmentAsset(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.useFileScale = false;
            importer.globalScale = 1f;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
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
            foreach (string path in imported)
            {
                if (!IsEnvironmentAsset(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + file + "_basecolor.png");
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (texture == null || importer == null || importer.GetExternalObjectMap().Count > 0)
                {
                    continue; // texture not imported yet (a later pass handles it) or already remapped
                }

                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader == null)
                {
                    Debug.LogWarning(file + ": no URP shader found; the runtime fallback material will be used.");
                    continue;
                }

                if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Game/Art/Environment", "Materials");
                }

                string matPath = MaterialsFolder + "/" + file + "_Mat.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                mat.mainTexture = texture;
                mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_SpecColor")) { mat.SetColor("_SpecColor", Color.black); }
                EditorUtility.SetDirty(mat);

                bool remapped = false;
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is Material embedded)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
                        remapped = true;
                    }
                }

                if (remapped)
                {
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
