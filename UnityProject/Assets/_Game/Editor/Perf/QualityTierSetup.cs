using System.Collections.Generic;
using JungleBooze.App.Perf;
using JungleBooze.Editor.LookTest;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JungleBooze.Editor.Perf
{
    /// <summary>
    /// Sets up the two quality tiers (docs/ARCHITECTURE.md 10.4) idempotently:
    /// <list type="bullet">
    /// <item>Quality levels become [0] "Low" and [1] "High". High is the former "Ultra" level unchanged (LOD bias 2,
    ///   forced anisotropic, unlimited skin weights: what every editor capture and owner review used) with no pipeline
    ///   override, so it renders with the Graphics default URP-Realistic exactly as before. Low is the former "Medium"
    ///   level with LOD bias 1 and the pipeline override URP-Realistic-Low. Every platform defaults to High; the
    ///   run-time switch (<see cref="QualityTiers"/>) drops old devices to Low.</item>
    /// <item>URP-Realistic-Low = a copy of URP-Realistic (refreshed on every run, so look changes carry over) with
    ///   MSAA 2x, a 1024 shadow map, 30 m shadow distance and low soft-shadow quality.</item>
    /// <item>Config assets: Config/Perf/Resources/QualityTiers.asset and Config/Perf/DeviceBenchConfig.asset
    ///   (created with defaults when missing, never overwritten).</item>
    /// </list>
    /// URP-Realistic itself is never modified.
    /// </summary>
    public static class QualityTierSetup
    {
        public const string HighPipelinePath = LookTestPipelineSetup.PipelinePath;
        public const string LowPipelinePath = "Assets/_Game/Config/Rendering/URP-Realistic-Low.asset";
        public const string TierConfigPath = "Assets/_Game/Config/Perf/Resources/QualityTiers.asset";
        public const string BenchConfigPath = "Assets/_Game/Config/Perf/DeviceBenchConfig.asset";
        public const string LowLevel = "Low";
        public const string HighLevel = "High";

        [MenuItem("JungleBooze/Perf/Set Up Quality Tiers")]
        public static void ApplyFromMenu()
        {
            foreach (string note in Apply())
            {
                Debug.Log("[JungleBooze perf] " + note);
            }
        }

        public static List<string> Apply()
        {
            var notes = new List<string>();
            var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(HighPipelinePath);
            if (high == null)
            {
                notes.Add("URP-Realistic not found at " + HighPipelinePath + ": build the look test or hero scene first. Tiers not set up.");
                return notes;
            }

            UniversalRenderPipelineAsset low = UpdateLowPipeline(high, notes);
            UpdateQualityLevels(low, notes);
            EnsureAsset<QualityTierConfigAsset>(TierConfigPath, notes);
            EnsureAsset<DeviceBenchConfigAsset>(BenchConfigPath, notes);
            AssetDatabase.SaveAssets();
            return notes;
        }

        private static UniversalRenderPipelineAsset UpdateLowPipeline(UniversalRenderPipelineAsset high, List<string> notes)
        {
            var low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowPipelinePath);
            if (low == null)
            {
                AssetDatabase.CopyAsset(HighPipelinePath, LowPipelinePath);
                low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowPipelinePath);
                notes.Add("Created " + LowPipelinePath + ".");
            }
            else
            {
                EditorUtility.CopySerialized(high, low);
                low.name = "URP-Realistic-Low";
            }

            var so = new SerializedObject(low);
            Set(so, "m_MSAA", 2);
            Set(so, "m_MainLightShadowmapResolution", 1024);
            Set(so, "m_SoftShadowQuality", 1);
            SerializedProperty distance = so.FindProperty("m_ShadowDistance");
            if (distance != null)
            {
                distance.floatValue = 30f;
            }

            SerializedProperty scale = so.FindProperty("m_RenderScale");
            if (scale != null)
            {
                scale.floatValue = 1f;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(low);
            return low;
        }

        private static void UpdateQualityLevels(UniversalRenderPipelineAsset low, List<string> notes)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets.Length == 0)
            {
                notes.Add("QualitySettings.asset not found.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty list = so.FindProperty("m_QualitySettings");
            int lowIndex = Find(list, LowLevel);
            int highIndex = Find(list, HighLevel);
            bool done = list.arraySize == 2 && lowIndex == 0 && highIndex == 1;
            if (!done)
            {
                int sourceHigh = Find(list, "Ultra");
                int sourceLow = Find(list, "Medium");
                if (sourceHigh < 0 || sourceLow < 0)
                {
                    notes.Add("Quality levels are neither the Unity defaults nor Low/High; left unchanged.");
                    return;
                }

                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    if (i != sourceHigh && i != sourceLow)
                    {
                        list.DeleteArrayElementAtIndex(i);
                    }
                }

                if (sourceHigh < sourceLow)
                {
                    list.MoveArrayElement(1, 0);
                }

                list.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = LowLevel;
                list.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = HighLevel;
                notes.Add("Quality levels are now Low (from Medium) and High (from Ultra, unchanged).");
            }

            SerializedProperty lowLevel = list.GetArrayElementAtIndex(0);
            lowLevel.FindPropertyRelative("lodBias").floatValue = 1f;
            lowLevel.FindPropertyRelative("customRenderPipeline").objectReferenceValue = low;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("customRenderPipeline").objectReferenceValue = null;
            so.FindProperty("m_CurrentQuality").intValue = 1;
            SerializedProperty perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null && perPlatform.isArray)
            {
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    SerializedProperty second = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (second != null)
                    {
                        second.intValue = 1;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(1, false);
        }

        private static int Find(SerializedProperty list, string name)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Set(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
            {
                p.intValue = value;
            }
        }

        private static void EnsureAsset<T>(string path, List<string> notes) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
            {
                return;
            }

            LookTestAssets.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            notes.Add("Created " + path + " with defaults.");
        }
    }
}
