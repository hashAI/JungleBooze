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
            importer.importAnimation = true; // Duko.fbx carries Idle / Flap / TailWag clips

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
            bool loops = file == "duko"
                || file.EndsWith("_run", System.StringComparison.Ordinal)
                || file.EndsWith("_idle", System.StringComparison.Ordinal)
                || file.EndsWith("_walk", System.StringComparison.Ordinal);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                bool clipLoops = loops;
                clips[i].loopTime = clipLoops;
                clips[i].loopPose = clipLoops;
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// The Meshy/Blender FBX files embed an unlit-looking material. After a main model (Pista.fbx, Duko.fbx) imports,
        /// build a URP Simple Lit material from the sibling "&lt;Name&gt;_basecolor.png" and remap the model to it, once.
        /// </summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.StartsWith(Root, System.StringComparison.Ordinal) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file != "Pista" && file != "Duko")
                {
                    continue;
                }

                string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string matPath = dir + "/" + file + "_Mat.mat";
                string texPath = dir + "/" + file + "_basecolor.png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (texture == null || importer == null || importer.GetExternalObjectMap().Count > 0)
                {
                    continue; // texture not imported yet (a later pass handles it) or already remapped
                }

                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit");
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

                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub is Material embedded)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
                    }
                }

                importer.SaveAndReimport();
            }
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
