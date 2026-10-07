using System.IO;
using JungleBooze.Gameplay.Config;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Setup
{
    /// <summary>
    /// Menu JungleBooze > Setup > Create Default Config Assets: creates the First Playable tuning assets with the
    /// spec 001 start values in <see cref="ResourcesFolder"/>, where the run bootstrap loads them
    /// (<c>JungleBooze.App.RunConfigLoader</c>). Existing assets are never overwritten. Without these assets the
    /// game runs on the same defaults built into the code.
    /// </summary>
    public static class DefaultConfigAssetsMenu
    {
        public const string ResourcesFolder = "Assets/_Game/Config/Resources";

        private const string MenuPath = "JungleBooze/Setup/Create Default Config Assets";
        private const string LogPrefix = "[JungleBooze setup] ";

        [MenuItem(MenuPath, false, 20)]
        public static void CreateDefaultConfigAssets()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }

            int created = 0;
            created += CreateIfMissing<RunnerConfigAsset>("RunnerTuning");
            created += CreateIfMissing<SpeedCurveAsset>("SpeedCurve");
            created += CreateIfMissing<InputConfigAsset>("InputTuning");
            created += CreateIfMissing<RunnerPresentationConfigAsset>("RunnerPresentationTuning");
            created += CreateIfMissing<VineConfigAsset>("VineTuning");
            created += CreateIfMissing<CompanionConfigAsset>("CompanionTuning");
            created += CreateIfMissing<EconomyConfigAsset>("EconomyConfig");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(LogPrefix + "Default config assets: " + created + " created in " + ResourcesFolder + ".");
        }

        private static int CreateIfMissing<T>(string name)
            where T : ScriptableObject
        {
            string path = ResourcesFolder + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return 0;
            }

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return 1;
        }
    }
}
