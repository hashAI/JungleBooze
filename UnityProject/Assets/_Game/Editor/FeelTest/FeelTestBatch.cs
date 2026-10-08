using System;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.App.FeelTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.FeelTest
{
    /// <summary>
    /// Batch-mode entry points (tools/ci/unity.sh method …):
    ///   JungleBooze.Editor.FeelTest.FeelTestBatch.Setup          assets, materials, scene, build settings
    ///   JungleBooze.Editor.FeelTest.FeelTestBatch.CaptureShots   bot-driven screenshots of the FeelTest scene
    ///       [-jbShotsOut /tmp/junglebooze-shots/feeltest] [-jbShotSizes 2532x1170,1170x2532]
    /// CaptureShots needs a GPU (run Unity without -nographics). The scene is not saved.
    /// </summary>
    public static class FeelTestBatch
    {
        private const string LogPrefix = "[JungleBooze] ";
        private static readonly float[] ShotDistances = { 6f, 72f, 108f, 157.5f, 218f, 274f, 313.5f, 352f, 416f, 524f, 552f, 572f, 636f };

        public static void Setup()
        {
            int code = 0;
            try
            {
                FeelTestSetup.BuildAll();
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Feel test setup failed: " + exception);
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
                string outDir = Arg(args, "-jbShotsOut") ?? "/tmp/junglebooze-shots/feeltest";
                string sizes = Arg(args, "-jbShotSizes") ?? "2532x1170,1170x2532";
                code = Capture(outDir, sizes);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Feel test capture failed: " + exception);
                code = 1;
            }

            Exit(code);
        }

        private static int Capture(string outDir, string sizeList)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics to capture screenshots.");
                return 2;
            }

            Directory.CreateDirectory(outDir);
            var report = new StringBuilder();
            foreach (string part in sizeList.Split(','))
            {
                string[] wh = part.Trim().Split('x');
                int width = int.Parse(wh[0], CultureInfo.InvariantCulture);
                int height = int.Parse(wh[1], CultureInfo.InvariantCulture);
                bool landscape = width >= height;

                EditorSceneManager.OpenScene(FeelTestPaths.Scene, OpenSceneMode.Single);
                FeelTestRoot root = Object.FindFirstObjectByType<FeelTestRoot>();
                if (root == null)
                {
                    Debug.LogError(LogPrefix + "FeelTestRoot not found: run FeelTestBatch.Setup first.");
                    return 2;
                }

                root.Build();
                root.SetCameraProfile(landscape, true);
                root.SetBotDriving(true);
                root.SetDebugVisible(false);
                root.Restart();
                Camera camera = root.Camera;
                camera.aspect = (float)width / height;

                int shot = 0;
                int guard = 0;
                while (shot < ShotDistances.Length && guard++ < 60 * 200)
                {
                    root.StepTicks(1);
                    if (root.Simulation.State.S >= ShotDistances[shot] || root.Session.Phase == JungleBooze.Gameplay.Run.RunPhase.Results)
                    {
                        // Let the camera settle exactly as in play: frames at 60 Hz were already applied per tick.
                        string name = string.Format(CultureInfo.InvariantCulture, "{0}_{1:00}_{2:000}m", landscape ? "landscape" : "portrait", shot, ShotDistances[shot]);
                        Render(camera, width, height, Path.Combine(outDir, name + ".png"));
                        report.AppendLine(name + ": s " + root.Simulation.State.S.ToString("0.0", CultureInfo.InvariantCulture) +
                                          " x " + root.Simulation.State.X.ToString("0.00", CultureInfo.InvariantCulture) +
                                          " y " + root.Simulation.State.Y.ToString("0.00", CultureInfo.InvariantCulture) +
                                          (root.Simulation.State.Sliding ? " sliding" : string.Empty) +
                                          " hits " + root.Simulation.State.Hits);
                        shot++;
                    }
                }

                // One debug-overlay frame (hitboxes) at the slalom.
                root.Restart();
                root.SetDebugVisible(true);
                while (root.Simulation.State.S < 106f)
                {
                    root.StepTicks(1);
                }

                Render(camera, width, height, Path.Combine(outDir, (landscape ? "landscape" : "portrait") + "_debug_hitboxes.png"));
                camera.ResetAspect();
            }

            File.WriteAllText(Path.Combine(outDir, "shots.txt"), report.ToString());
            Debug.Log(LogPrefix + "Feel test screenshots written to " + outDir + Environment.NewLine + report);
            return 0;
        }

        private static void Render(Camera camera, int width, int height, string path)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true };
            var target = new RenderTexture(descriptor);
            target.Create();
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            var resolved = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, resolved);
            RenderTexture.active = resolved;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(resolved);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name)
                {
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
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
