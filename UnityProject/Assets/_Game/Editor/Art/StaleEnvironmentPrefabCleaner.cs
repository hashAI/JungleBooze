using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Art
{
    /// <summary>
    /// The environment models load straight from <c>Resources/EnvironmentArt</c> as FBX files. An older workflow built
    /// prefabs into the same folder; a prefab with the same name as an FBX wins <c>Resources.Load</c>, and one that
    /// pointed at a deleted source model has no renderers, which left its gray-box hitbox as the only thing drawn.
    /// This deletes any leftover prefab in that folder once, after the editor has loaded.
    /// </summary>
    [InitializeOnLoad]
    public static class StaleEnvironmentPrefabCleaner
    {
        private const string Folder = "Assets/_Game/Art/Environment/Resources/EnvironmentArt";

        static StaleEnvironmentPrefabCleaner()
        {
            EditorApplication.delayCall += Clean;
        }

        [MenuItem("JungleBooze/Art/Delete Stale Environment Prefabs")]
        public static void Clean()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Folder });
            int removed = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.DeleteAsset(path))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log("[JungleBooze] Deleted " + removed + " stale environment prefab(s) from " + Folder + ". The FBX models load directly.");
                AssetDatabase.Refresh();
            }
        }
    }
}
