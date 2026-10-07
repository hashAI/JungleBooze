using JungleBooze.Gameplay.Views;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Art
{
    /// <summary>
    /// Menu "JungleBooze > Art > Check Environment Models". Read-only: reports which of the environment models in
    /// <c>Resources/EnvironmentArt</c> load, and their triangle counts against the budget. It builds no prefabs and
    /// writes no assets (the run views load the FBX files directly).
    /// </summary>
    public static class EnvironmentPrefabBuilder
    {
        private const int PropTriangleBudget = 1500;
        private const int TileTriangleBudget = 3000;

        [MenuItem("JungleBooze/Art/Check Environment Models")]
        public static void Check()
        {
            int found = 0;
            for (int n = 0; n < EnvironmentArt.AllNames.Length; n++)
            {
                string name = EnvironmentArt.AllNames[n];
                var model = Resources.Load<GameObject>("EnvironmentArt/" + name);
                if (model == null)
                {
                    Debug.LogWarning(name + ": not found in Resources/EnvironmentArt (gray-box fallback).");
                    continue;
                }

                found++;
                int triangles = CountTriangles(model);
                int budget = name == EnvironmentArt.PathTile ? TileTriangleBudget : PropTriangleBudget;
                string line = name + ": " + triangles + " triangles (budget " + budget + ").";
                if (triangles > budget)
                {
                    Debug.LogWarning(line + " Over budget.");
                }
                else
                {
                    Debug.Log(line);
                }
            }

            Debug.Log("Environment models found: " + found + " of " + EnvironmentArt.AllNames.Length + ".");
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
