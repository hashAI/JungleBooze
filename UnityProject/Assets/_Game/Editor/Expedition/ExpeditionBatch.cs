using System;
using JungleBooze.Gameplay.Config;
using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Expedition
{
    /// <summary>
    /// Batch-mode entry points (tools/ci/unity.sh method …):
    ///   JungleBooze.Editor.Expedition.ExpeditionBatch.Setup      [-jbResetChunks]  content, validation, scene, build settings
    ///   JungleBooze.Editor.Expedition.ExpeditionBatch.Validate   re-run the chunk validator (exit 3 on failures)
    /// </summary>
    public static class ExpeditionBatch
    {
        private const string LogPrefix = "[JungleBooze] ";

        public static void Setup()
        {
            int code = 0;
            try
            {
                bool reset = Array.IndexOf(Environment.GetCommandLineArgs(), "-jbResetChunks") >= 0;
                var result = ExpeditionSetup.BuildAll(reset);
                code = result.Failures > 0 ? 3 : 0;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Expedition setup failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        public static void Validate()
        {
            int code = 0;
            try
            {
                var content = AssetDatabase.LoadAssetAtPath<ExpeditionContentAsset>(ExpeditionPaths.Content);
                var result = ExpeditionSetup.Validate(content);
                AssetDatabase.SaveAssets();
                code = result.Failures > 0 ? 3 : 0;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Chunk validation failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
