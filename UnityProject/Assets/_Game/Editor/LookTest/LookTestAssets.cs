using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Finds the CC0 files fetched by <c>tools/assets/fetch_cc0.py</c> (folder per asset id under
    /// <see cref="LookTestAssetImportRules.CC0Root"/>, Poly Haven file names) and records what is missing, so the
    /// scene builder can fall back to plain materials and tell the owner which assets to fetch. Editor only.
    /// </summary>
    public sealed class LookTestAssets
    {
        public const string TexturesFolder = "Textures";
        public const string ModelsFolder = "Models";
        public const string HdriFolder = "HDRI";

        private readonly List<string> _missing = new List<string>();

        /// <summary>One Poly Haven texture set (any map may be null).</summary>
        public struct TextureSet
        {
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Arm;
            public Texture2D Alpha;

            public bool IsComplete => Albedo != null && Normal != null && Arm != null;
        }

        /// <summary>Asset ids (or files) the builder looked for and did not find.</summary>
        public IReadOnlyList<string> Missing => _missing;

        public static string AssetFolder(string typeFolder, string id)
        {
            return LookTestAssetImportRules.CC0Root + typeFolder + "/" + id;
        }

        /// <summary>
        /// Texture maps in the asset's folder. <paramref name="mapPrefix"/> selects a material of a multi-material
        /// asset (for example "leaves" for island_tree_02_leaves_diff_1k.jpg); empty for single-material assets.
        /// </summary>
        public TextureSet FindTextures(string typeFolder, string id, string mapPrefix)
        {
            var set = new TextureSet();
            string folder = AssetFolder(typeFolder, id);
            if (string.IsNullOrEmpty(id) || !AssetDatabase.IsValidFolder(folder))
            {
                _missing.Add(typeFolder + "/" + id);
                return set;
            }

            string prefix = string.IsNullOrEmpty(mapPrefix) ? "_" : "_" + mapPrefix + "_";
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (name.Contains(prefix + "diff"))
                {
                    set.Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                else if (name.Contains(prefix + "nor_gl"))
                {
                    set.Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                else if (name.Contains(prefix + "arm"))
                {
                    set.Arm = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                else if (name.Contains(prefix + "alpha"))
                {
                    set.Alpha = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            if (set.Albedo == null)
            {
                _missing.Add(typeFolder + "/" + id + " (albedo" + (string.IsNullOrEmpty(mapPrefix) ? string.Empty : ", " + mapPrefix) + ")");
            }

            return set;
        }

        /// <summary>The model file (FBX) in the asset's folder, or null.</summary>
        public GameObject FindModel(string id)
        {
            string folder = AssetFolder(ModelsFolder, id);
            if (string.IsNullOrEmpty(id) || !AssetDatabase.IsValidFolder(folder))
            {
                _missing.Add(ModelsFolder + "/" + id);
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
            if (guids.Length == 0)
            {
                _missing.Add(ModelsFolder + "/" + id + " (FBX)");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>The HDRI imported as a cubemap, or null.</summary>
        public Cubemap FindHdri(string id)
        {
            string folder = AssetFolder(HdriFolder, id);
            if (string.IsNullOrEmpty(id) || !AssetDatabase.IsValidFolder(folder))
            {
                _missing.Add(HdriFolder + "/" + id);
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Cubemap", new[] { folder });
            if (guids.Length == 0)
            {
                _missing.Add(HdriFolder + "/" + id + " (not imported as a cubemap: select the .hdr, Texture Shape = Cube, Apply)");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Cubemap>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        /// <summary>Creates an asset folder and its parents if needed.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        /// <summary>Saves <paramref name="asset"/> at <paramref name="path"/>, replacing an older asset of the same path.</summary>
        public static T SaveOrReplace<T>(T asset, string path)
            where T : Object
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
