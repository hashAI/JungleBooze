using UnityEditor;
using UnityEngine;

namespace JungleBooze.Editor.Art
{
    /// <summary>
    /// Builds the environment prefabs automatically so nobody has to click the menu: when the editor loads and a model
    /// has no prefab yet, and whenever an environment FBX or PNG is (re)imported. Runs after the asset database is
    /// idle. The prefab build only writes to <c>Resources/EnvironmentArt</c> and <c>Materials</c>, never to
    /// <c>Models</c>, so it cannot re-trigger itself; a per-session cap guards the "prefab never appears" case.
    /// The menu item "JungleBooze > Art > Build Environment Prefabs" still works.
    /// </summary>
    [InitializeOnLoad]
    public static class EnvironmentAutoBuild
    {
        private const string AttemptsKey = "JungleBooze.EnvArt.AutoBuildAttempts";
        private const int MaxMissingAttempts = 3;

        private static bool _pending;
        private static bool _force;

        static EnvironmentAutoBuild()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            Request(false);
        }

        /// <summary>Schedules a build. <paramref name="force"/> rebuilds even when every prefab already exists.</summary>
        public static void Request(bool force)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            _force |= force;
            if (_pending)
            {
                return;
            }

            _pending = true;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            _pending = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Request(false);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            bool force = _force;
            _force = false;
            if (!force)
            {
                if (!EnvironmentPrefabBuilder.NeedsBuild())
                {
                    return;
                }

                int attempts = SessionState.GetInt(AttemptsKey, 0);
                if (attempts >= MaxMissingAttempts)
                {
                    return;
                }

                SessionState.SetInt(AttemptsKey, attempts + 1);
            }

            EnvironmentPrefabBuilder.Build();
        }
    }
}
