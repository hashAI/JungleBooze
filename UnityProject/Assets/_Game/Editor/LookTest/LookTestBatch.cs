using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.App.LookTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Batch-mode entry points for the look test (ADR 0004), run by <c>tools/ci/unity.sh method</c>:
    ///
    ///   JungleBooze.Editor.LookTest.LookTestBatch.BuildScene      builds Assets/_Game/Scenes/LookTest.unity
    ///   JungleBooze.Editor.LookTest.LookTestBatch.CaptureShots    renders the config's shot list to PNG files
    ///       [-jbShotsOut /tmp/junglebooze-shots] [-jbShotFilter F1] [-jbShotScale 0.5]
    ///   JungleBooze.Editor.LookTest.LookTestBatch.UseLookTestBuildSettings   look test first scene, landscape
    ///
    /// CaptureShots needs a GPU: run Unity without -nographics. It places the stand-in runner and the camera exactly
    /// as <see cref="LookTestRoot"/> does at Play (<see cref="LookTestCameraRig"/>), renders each frame with MSAA 4x,
    /// and writes <c>stats.txt</c> next to the images: per budget layer, the draws and triangles of the renderers in
    /// view and an estimate of the shadow pass (<see cref="LookTestFrameBudget"/>). The scene file is never saved.
    /// </summary>
    public static class LookTestBatch
    {
        private const string LogPrefix = "[JungleBooze] ";
        private const string DefaultOut = "/tmp/junglebooze-shots";
        public static void BuildScene()
        {
            int code = 0;
            try
            {
                LookTestSceneBuilder.Build();
                if (!File.Exists(LookTestSceneBuilder.ScenePath))
                {
                    Debug.LogError(LogPrefix + "Scene was not written: " + LookTestSceneBuilder.ScenePath);
                    code = 1;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Look test build failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        public static void UseLookTestBuildSettings()
        {
            int code = 0;
            try
            {
                LookTestMenu.UseLookTestBuildSettings();
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + exception);
                code = 1;
            }

            Exit(code);
        }

        /// <summary>
        /// Development macOS player of the look test (-jbOutput, default /tmp/jb-mac/LookTest.app), used only to
        /// read real render counters with "-jbBenchmark 30". Not a shipping target.
        /// </summary>
        public static void BuildMacBenchmarkPlayer()
        {
            int code = 0;
            try
            {
                string output = Arg(Environment.GetCommandLineArgs(), "-jbOutput") ?? "/tmp/jb-mac/LookTest.app";
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { LookTestSceneBuilder.ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.Development,
                };
                UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
                Debug.Log(LogPrefix + "Mac benchmark player: " + report.summary.result + " -> " + output);
                code = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Mac benchmark build failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        public static void CaptureShots()
        {
            int code = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbShotsOut") ?? DefaultOut;
                string only = Arg(args, "-jbShotFilter");
                string scale = Arg(args, "-jbShotScale");
                float sizeScale = string.IsNullOrEmpty(scale) ? 1f : float.Parse(scale, CultureInfo.InvariantCulture);
                code = Capture(outDir, only, sizeScale);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Capture failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        private static int Capture(string outDir, string filter, float sizeScale)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics to capture screenshots.");
                return 2;
            }

            EditorSceneManager.OpenScene(LookTestSceneBuilder.ScenePath, OpenSceneMode.Single);
            LookTestRoot root = Object.FindFirstObjectByType<LookTestRoot>();
            if (root == null)
            {
                Debug.LogError(LogPrefix + "LookTestRoot not found. Build the scene first.");
                return 2;
            }

            var so = new SerializedObject(root);
            var config = (LookTestConfigAsset)so.FindProperty("_config").objectReferenceValue;
            var camera = (Camera)so.FindProperty("_camera").objectReferenceValue;
            var stretch = (Transform)so.FindProperty("_stretchRoot").objectReferenceValue;
            var backdrop = (Transform)so.FindProperty("_backdrop").objectReferenceValue;
            SerializedProperty segmentsProperty = so.FindProperty("_segments");
            var segments = new Transform[segmentsProperty.arraySize];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = (Transform)segmentsProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            LookTestAtmosphere atmosphere = Object.FindFirstObjectByType<LookTestAtmosphere>();
            if (atmosphere != null)
            {
                atmosphere.Apply();
            }

            LookTestPath path = config.CreatePath();
            LookTestWorldView world = stretch.GetComponent<LookTestWorldView>();
            if (world == null)
            {
                world = stretch.gameObject.AddComponent<LookTestWorldView>();
            }

            world.Init(segments, config.LoopLengthM, path.LoopOffset, config.RecycleBehindM, backdrop);
            SerializedProperty detailProperty = so.FindProperty("_detailGroups");
            var detail = new GameObject[detailProperty.arraySize];
            for (int i = 0; i < detail.Length; i++)
            {
                detail[i] = (GameObject)detailProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            world.InitDetail(detail, config.DetailRangeM);
            Transform runner = LookTestRoot.CreateStandIn(root.transform);

            foreach (ParticleSystem particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                particles.Simulate(4f, true, true);
            }

            Directory.CreateDirectory(outDir);
            var stats = new StringBuilder();
            stats.AppendLine("Look test v2 frame stats (editor, " + SystemInfo.graphicsDeviceName + ", " + SystemInfo.graphicsDeviceType + ")");
            stats.AppendLine("Main view: renderers in the frustum (each merged renderer = 1 draw with the SRP Batcher). Shadow pass: casters within the shadow distance " +
                             "(estimate). Budget: " + config.MainDrawBudget + " + " + config.ShadowDrawBudget + " draws, " +
                             config.MainTriangleBudget.ToString("N0", CultureInfo.InvariantCulture) + " + " + config.ShadowTriangleBudget.ToString("N0", CultureInfo.InvariantCulture) + " tris.");

            LookTestShot[] shots = config.Shots;
            for (int i = 0; i < shots.Length; i++)
            {
                LookTestShot shot = shots[i];
                if (!string.IsNullOrEmpty(filter) && shot.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var size = shot.Portrait ? new Vector2Int(1170, 2532) : new Vector2Int(2532, 1170);
                size = new Vector2Int(Mathf.RoundToInt(size.x * sizeScale), Mathf.RoundToInt(size.y * sizeScale));
                double s = shot.DistanceM;
                float runTime = shot.DistanceM / Mathf.Max(0.1f, config.RunSpeedMps);
                float x = LookTestCameraRig.RunnerX(runTime);
                world.Render(s);
                runner.localPosition = LookTestCameraRig.RunnerPosition(path, s, x);
                runner.localRotation = Quaternion.Euler(0f, path.HeadingRad(s) * Mathf.Rad2Deg, 0f);
                camera.aspect = (float)size.x / size.y;
                LookTestCameraProfile profile = LookTestCameraRig.Profile(config, camera.aspect);
                LookTestCameraRig.Pose(path, profile, s, x, out Vector3 position, out Quaternion rotation);
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = profile.VerticalFovDeg;

                RenderToPng(camera, size, Path.Combine(outDir, shot.Name + ".png"));
                stats.AppendLine();
                stats.Append(shot.Name).Append(" (s = ").Append(shot.DistanceM.ToString("0", CultureInfo.InvariantCulture)).AppendLine(" m)");
                stats.Append(LookTestFrameBudget.Measure(camera, config));
            }

            camera.ResetAspect();
            File.WriteAllText(Path.Combine(outDir, "stats.txt"), stats.ToString());
            Debug.Log(LogPrefix + "Screenshots written to " + outDir + Environment.NewLine + stats);
            return 0;
        }

        private static void RenderToPng(Camera camera, Vector2Int size, string path)
        {
            Texture2D image = Render(camera, size);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        /// <summary>Renders <paramref name="camera"/> at <paramref name="size"/> with MSAA 4x into a new RGB24 texture (caller destroys it).</summary>
        internal static Texture2D Render(Camera camera, Vector2Int size)
        {
            // An HDR target: URP sizes its intermediate colour buffer from the camera's target texture, so an
            // ARGB32 target made the whole frame LDR (sky and highlights clamped at 1 before tonemapping, no bloom
            // from the sun), unlike the device, which renders HDR to the screen. Post writes tonemapped linear values;
            // the blit below encodes them to sRGB.
            var descriptor = new RenderTextureDescriptor(size.x, size.y, RenderTextureFormat.ARGBHalf, 24)
            {
                msaaSamples = 4,
                sRGB = false,
            };
            var target = new RenderTexture(descriptor);
            target.Create();
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;

            // Twice: the first render warms up shader variants and the shadow map.
            camera.Render();
            camera.Render();

            var resolved = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, resolved);
            RenderTexture.active = resolved;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(resolved);
            target.Release();
            Object.DestroyImmediate(target);
            return image;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
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
