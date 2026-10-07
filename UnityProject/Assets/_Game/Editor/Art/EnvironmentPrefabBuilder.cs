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
    /// assigns the shared atlas material when one exists, and reports the triangle count against the budget.
    /// Models over budget are reported as errors and skipped. Missing models are fine: the views keep the gray-box.
    /// </summary>
    public static class EnvironmentPrefabBuilder
    {
        private const string ModelsFolder = "Assets/_Game/Art/Environment/Models";
        private const string OutputFolder = "Assets/_Game/Art/Environment/Resources/EnvironmentArt";
        private const string MaterialPath = "Assets/_Game/Art/Environment/Materials/EnvAtlas.mat";
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

        [MenuItem("JungleBooze/Art/Build Environment Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);
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

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    Object.DestroyImmediate(colliders[c]);
                }

                MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (atlas != null)
                    {
                        renderers[r].sharedMaterial = atlas;
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
                if (filters[i].sharedMesh != null)
                {
                    total += filters[i].sharedMesh.triangles.Length / 3;
                }
            }

            return total;
        }
    }
}
