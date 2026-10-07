using System.IO;
using JungleBooze.Gameplay.Views;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Art
{
    /// <summary>
    /// Menu "JungleBooze > Art > Build Environment Prefabs". For every model file in
    /// <c>Art/Environment/Models</c> whose file name (without extension) is a prefab name the run views look up
    /// (see <see cref="EnvironmentArt"/>), writes a prefab to <c>Art/Environment/Resources/EnvironmentArt</c>,
    /// assigns a URP material built from the model's sibling PNG (falls back to the shared atlas material when there is
    /// no PNG), and reports the triangle count against the budget.
    /// Models over budget are reported as errors and skipped. Missing models are fine: the views keep the gray-box.
    /// </summary>
    public static class EnvironmentPrefabBuilder
    {
        public const string ModelsFolder = "Assets/_Game/Art/Environment/Models";
        private const string OutputFolder = "Assets/_Game/Art/Environment/Resources/EnvironmentArt";
        private const string MaterialPath = "Assets/_Game/Art/Environment/Materials/EnvAtlas.mat";
        private const string MaterialsFolder = "Assets/_Game/Art/Environment/Materials";
        private const int PropTriangleBudget = 1500;
        private const int TileTriangleBudget = 3000;

        private static readonly string[] Names =
        {
            EnvironmentArt.LowBarrier, EnvironmentArt.HighBarrier, EnvironmentArt.FullBlock, EnvironmentArt.Boulder,
            EnvironmentArt.ThornPatch, EnvironmentArt.StrikeColumn, EnvironmentArt.Coin, EnvironmentArt.Magnet,
            EnvironmentArt.Shield, EnvironmentArt.Boost, EnvironmentArt.PathTile, EnvironmentArt.RavineEdge,
            EnvironmentArt.VineBranch, EnvironmentArt.Signpost, EnvironmentArt.TreeA, EnvironmentArt.TreeB,
            EnvironmentArt.Bush,
        };

        private static readonly string[] Extensions = { ".fbx", ".glb", ".gltf", ".obj" };

        /// <summary>True when at least one model exists whose prefab has not been built yet.</summary>
        public static bool NeedsBuild()
        {
            for (int n = 0; n < Names.Length; n++)
            {
                if (FindModel(Names[n]) != null &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(OutputFolder + "/" + Names[n] + ".prefab") == null)
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem("JungleBooze/Art/Build Environment Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(MaterialsFolder);
            Material atlas = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            int built = 0;
            for (int n = 0; n < Names.Length; n++)
            {
                GameObject model = FindModel(Names[n]);
                if (model == null)
                {
                    continue;
                }

                int triangles = CountTriangles(model);
                int budget = Names[n] == EnvironmentArt.PathTile ? TileTriangleBudget : PropTriangleBudget;
                if (triangles > budget)
                {
                    Debug.LogError(Names[n] + ": " + triangles + " triangles is over the budget of " + budget + "; skipped.");
                    continue;
                }

                // A plain clone (not a linked prefab instance) so colliders can be removed and materials replaced.
                GameObject instance = Object.Instantiate(model);
                instance.name = Names[n];
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    Object.DestroyImmediate(colliders[c]);
                }

                Material material = EnsureMaterial(Names[n]);
                if (material == null)
                {
                    material = atlas;
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (material != null)
                    {
                        var materials = new Material[Mathf.Max(1, renderers[r].sharedMaterials.Length)];
                        for (int m = 0; m < materials.Length; m++)
                        {
                            materials[m] = material;
                        }

                        renderers[r].sharedMaterials = materials;
                    }

                    renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderers[r].receiveShadows = false;
                }

                PrefabUtility.SaveAsPrefabAsset(instance, OutputFolder + "/" + Names[n] + ".prefab");
                Object.DestroyImmediate(instance);
                Debug.Log(Names[n] + ": " + triangles + " triangles (budget " + budget + ").");
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Environment prefabs built: " + built + " of " + Names.Length + ".");
        }

        /// <summary>
        /// Creates or updates <c>Materials/&lt;name&gt;_Mat.mat</c>: URP Simple Lit (or Lit) with the sibling
        /// <c>&lt;name&gt;.png</c> as base color, so FBX imports never render pink. Returns null when there is no PNG or
        /// no URP shader.
        /// </summary>
        private static Material EnsureMaterial(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelsFolder + "/" + name + ".png");
            if (texture == null)
            {
                return null;
            }

            string path = MaterialsFolder + "/" + name + "_Mat.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }

                if (shader == null)
                {
                    Debug.LogWarning(name + ": no URP shader found; the model keeps its imported material.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_SpecColor"))
            {
                material.SetColor("_SpecColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject FindModel(string name)
        {
            for (int e = 0; e < Extensions.Length; e++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelsFolder + "/" + name + Extensions[e]);
                if (model != null)
                {
                    return model;
                }
            }

            return null;
        }

        private static int CountTriangles(GameObject model)
        {
            int total = 0;
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    total += (int)(mesh.GetIndexCount(s) / 3);
                }
            }

            return total;
        }
    }
}
