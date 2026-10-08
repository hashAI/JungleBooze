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
using Unity.Profiling;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.LookTest
{
    /// <summary>
    /// Batch-mode entry points for the look test (ADR 0004), run by <c>tools/ci/unity.sh method</c>:
    ///
    ///   JungleBooze.Editor.LookTest.LookTestBatch.BuildScene      builds Assets/_Game/Scenes/LookTest.unity
    ///   JungleBooze.Editor.LookTest.LookTestBatch.CaptureShots    renders the game camera to PNG files
    ///       [-jbShotsOut /tmp/junglebooze-shots] [-jbShotDistances 20,80,124,190] [-jbShotSizes 2532x1170,1170x2532]
    ///   JungleBooze.Editor.LookTest.LookTestBatch.UseLookTestBuildSettings   look test first scene, landscape
    ///
    /// CaptureShots needs a GPU: run Unity without -nographics. It places the stand-in runner and the camera exactly
    /// as <see cref="LookTestRoot"/> does at Play (<see cref="LookTestCameraRig"/>), renders each frame with MSAA 4x,
    /// and writes <c>stats.txt</c> next to the images: Unity's render counters for the frame (batches, draw calls,
    /// SetPass calls, triangles) plus a CPU-side count of visible renderers. The scene file is never saved.
    /// </summary>
    public static class LookTestBatch
    {
        private const string LogPrefix = "[JungleBooze] ";
        private const string DefaultOut = "/tmp/junglebooze-shots";
        private static readonly float[] DefaultDistances = { 20f, 80f, 124f, 190f };

        private static readonly string[] RenderCounters =
        {
            "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "Shadow Casters Count",
        };

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
                float[] distances = ParseDistances(Arg(args, "-jbShotDistances")) ?? DefaultDistances;
                List<Vector2Int> sizes = ParseSizes(Arg(args, "-jbShotSizes") ?? "2532x1170,1170x2532");
                code = Capture(outDir, distances, sizes);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Capture failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        private static int Capture(string outDir, float[] distances, List<Vector2Int> sizes)
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
            SerializedProperty segmentsProperty = so.FindProperty("_segments");
            var segments = new Transform[segmentsProperty.arraySize];
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = (Transform)segmentsProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            LookTestWorldView world = stretch.GetComponent<LookTestWorldView>();
            if (world == null)
            {
                world = stretch.gameObject.AddComponent<LookTestWorldView>();
            }

            world.Init(segments, config.LoopLengthM, config.RecycleBehindM);
            Transform runner = LookTestRoot.CreateStandIn(root.transform);

            foreach (ParticleSystem particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                particles.Simulate(4f, true, true);
            }

            Directory.CreateDirectory(outDir);
            var stats = new StringBuilder();
            stats.AppendLine("Look test frame stats (editor, " + SystemInfo.graphicsDeviceName + ", " + SystemInfo.graphicsDeviceType + ")");
            stats.AppendLine("Columns: shot | Unity counters: batches, draw calls, SetPass, triangles, vertices, shadow casters | CPU count: visible renderers, visible triangles");

            for (int s = 0; s < sizes.Count; s++)
            {
                Vector2Int size = sizes[s];
                string orientation = size.x >= size.y ? "landscape" : "portrait";
                for (int d = 0; d < distances.Length; d++)
                {
                    float distance = distances[d];
                    float runTime = distance / Mathf.Max(0.1f, config.RunSpeedMps);
                    float x = LookTestCameraRig.RunnerX(runTime);
                    world.Render(distance);
                    runner.localPosition = LookTestCameraRig.RunnerPosition(runTime, distance);
                    camera.transform.position = LookTestCameraRig.CameraTarget(config, x, distance);
                    camera.aspect = (float)size.x / size.y;
                    camera.transform.LookAt(LookTestCameraRig.LookAtPoint(config, x, distance, camera.aspect));
                    camera.fieldOfView = LookTestCameraRig.VerticalFov(config, camera.aspect);

                    string name = string.Format(CultureInfo.InvariantCulture, "{0}_{1:000}m", orientation, distance);
                    string counters = RenderToPng(camera, size, Path.Combine(outDir, name + ".png"));
                    string visible = CountVisible(camera);
                    stats.AppendLine(name + " | " + counters + " | " + visible);
                }
            }

            camera.ResetAspect();
            File.WriteAllText(Path.Combine(outDir, "stats.txt"), stats.ToString());
            Debug.Log(LogPrefix + "Screenshots written to " + outDir + Environment.NewLine + stats);
            return 0;
        }

        private static string RenderToPng(Camera camera, Vector2Int size, string path)
        {
            var descriptor = new RenderTextureDescriptor(size.x, size.y, RenderTextureFormat.ARGB32, 24)
            {
                msaaSamples = 4,
                sRGB = true,
            };
            var target = new RenderTexture(descriptor);
            target.Create();
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;

            // Twice: the first render warms up shader variants and the shadow map; the second is measured.
            camera.Render();
            var recorders = new ProfilerRecorder[RenderCounters.Length];
            for (int i = 0; i < recorders.Length; i++)
            {
                recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Render, RenderCounters[i]);
            }

            camera.Render();
            var counterText = new StringBuilder();
            for (int i = 0; i < recorders.Length; i++)
            {
                long value = recorders[i].Valid ? recorders[i].CurrentValue : -1L;
                long last = recorders[i].Valid ? recorders[i].LastValue : -1L;
                counterText.Append(RenderCounters[i]).Append(' ').Append(Math.Max(value, last).ToString("N0", CultureInfo.InvariantCulture)).Append(", ");
                recorders[i].Dispose();
            }

            string counters = counterText.ToString().TrimEnd(',', ' ');

            var resolved = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, resolved);
            RenderTexture.active = resolved;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(resolved);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            return counters;
        }

        /// <summary>Renderers inside the view frustum and not culled by their LODGroup, and their triangles.</summary>
        private static string CountVisible(Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var culledByLod = new HashSet<Renderer>();
            float halfTan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            foreach (LODGroup group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                LOD[] lods = group.GetLODs();
                if (lods.Length == 0)
                {
                    continue;
                }

                Vector3 center = group.transform.TransformPoint(group.localReferencePoint);
                Vector3 scale = group.transform.lossyScale;
                float worldSize = group.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                float distance = Vector3.Distance(camera.transform.position, center);
                float relative = worldSize / Mathf.Max(0.001f, 2f * distance * halfTan) * QualitySettings.lodBias;
                if (relative < lods[lods.Length - 1].screenRelativeTransitionHeight)
                {
                    foreach (Renderer r in lods[lods.Length - 1].renderers)
                    {
                        culledByLod.Add(r);
                    }
                }
            }

            int count = 0;
            long tris = 0;
            foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || culledByLod.Contains(renderer))
                {
                    continue;
                }

                if (!GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                count++;
                tris += LookTestMeshFactory.TriangleCount(filter.sharedMesh);
            }

            return string.Format(CultureInfo.InvariantCulture, "visible renderers {0}, visible tris {1:N0}", count, tris);
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

        private static float[] ParseDistances(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] parts = text.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                result[i] = float.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static List<Vector2Int> ParseSizes(string text)
        {
            var result = new List<Vector2Int>();
            foreach (string part in text.Split(','))
            {
                string[] wh = part.Trim().Split('x');
                result.Add(new Vector2Int(int.Parse(wh[0], CultureInfo.InvariantCulture), int.Parse(wh[1], CultureInfo.InvariantCulture)));
            }

            return result;
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
