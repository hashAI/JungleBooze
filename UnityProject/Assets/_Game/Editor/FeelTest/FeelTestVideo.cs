using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using JungleBooze.App.FeelTest;
using JungleBooze.Gameplay.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.FeelTest
{
    /// <summary>
    /// Gameplay video of the FeelTest scene for the owner (needs a GPU: run Unity without -nographics):
    ///   tools/ci/unity.sh method JungleBooze.Editor.FeelTest.FeelTestVideo.Capture
    ///     [-jbVideoOut /tmp/junglebooze-video] [-jbVideoSizes 1280x592,592x1280] [-jbFfmpeg /opt/homebrew/bin/ffmpeg]
    ///     [-jbVideoName feeltest] [-jbBotOff 155-162,215-222] [-jbCrf 24]
    /// The Perfect bot runs the whole course in the real scene at the fixed 60 Hz step; every second tick is rendered
    /// (30 fps) and piped as raw RGB into ffmpeg (H.264, yuv420p, faststart), so no frame dumps touch the disk.
    /// <c>-jbBotOff</c> lists distance ranges (m) where the bot lets go, to show hits, stumbles and the death.
    /// The scene is not saved.
    /// </summary>
    public static class FeelTestVideo
    {
        private const string LogPrefix = "[JungleBooze] ";
        private const int TicksPerFrame = 2;
        private const float TailSeconds = 1.5f;
        private const int MaxTicks = 60 * 120;

        // Diagnostics: a side view that tracks Pista (animation review, foot skating).
        private static bool _sideCamera;

        public static void Capture()
        {
            int code;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbVideoOut") ?? "/tmp/junglebooze-video";
                string sizes = Arg(args, "-jbVideoSizes") ?? "1280x592,592x1280";
                string ffmpeg = Arg(args, "-jbFfmpeg") ?? FindFfmpeg();
                string name = Arg(args, "-jbVideoName") ?? "feeltest";
                string botOff = Arg(args, "-jbBotOff") ?? string.Empty;
                string crf = Arg(args, "-jbCrf") ?? "24";
                _sideCamera = Arg(args, "-jbSideCam") != null;
                code = Run(outDir, sizes, ffmpeg, name, ParseRanges(botOff), crf);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Feel test video failed: " + exception);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        private static int Run(string outDir, string sizeList, string ffmpeg, string name, float[] botOff, string crf)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics to capture video.");
                return 2;
            }

            if (ffmpeg == null || !File.Exists(ffmpeg))
            {
                Debug.LogError(LogPrefix + "ffmpeg not found (pass -jbFfmpeg <path>).");
                return 2;
            }

            Directory.CreateDirectory(outDir);
            foreach (string part in sizeList.Split(','))
            {
                string[] wh = part.Trim().Split('x');
                int width = int.Parse(wh[0], CultureInfo.InvariantCulture);
                int height = int.Parse(wh[1], CultureInfo.InvariantCulture);
                bool landscape = width >= height;
                string file = Path.Combine(outDir, name + "_" + (landscape ? "landscape" : "portrait") + ".mp4");
                int result = Record(width, height, landscape, file, ffmpeg, botOff, crf);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static int Record(int width, int height, bool landscape, string file, string ffmpeg, float[] botOff, string crf)
        {
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

            // Outside play mode the skinning isn't refreshed per camera.Render() unless asked to.
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.forceMatrixRecalculationPerRender = true;
            }

            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true };
            var target = new RenderTexture(descriptor);
            target.Create();
            var resolved = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            resolved.Create();
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            byte[] frame = new byte[width * height * 3];

            var info = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = string.Format(CultureInfo.InvariantCulture,
                    "-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {0}x{1} -r 30 -i - -vf vflip -c:v libx264 -preset slow -crf {2} -pix_fmt yuv420p -movflags +faststart \"{3}\"",
                    width, height, crf, file),
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            var log = new System.Text.StringBuilder(1 << 16);
            int frames = 0;
            int ticks = 0;
            int tail = -1;
            // Warm-up render: the first URP render after the scene opens has no lighting/fog set up yet.
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            camera.targetTexture = null;

            using (Process process = Process.Start(info))
            {
                Stream stdin = process.StandardInput.BaseStream;
                while (ticks < MaxTicks)
                {
                    for (int i = 0; i < TicksPerFrame; i++)
                    {
                        float s = root.Simulation.State.S;
                        root.SetBotDriving(!InRanges(botOff, s));
                        root.StepTicks(1);
                        ticks++;
                    }

                    if (_sideCamera)
                    {
                        var st = root.Simulation.State;
                        camera.transform.SetPositionAndRotation(new Vector3(st.X + 4.5f, st.Y + 1.0f, st.S + 0.3f), Quaternion.LookRotation(new Vector3(-1f, -0.05f, 0f)));
                        camera.fieldOfView = 40f;
                    }

                    camera.targetTexture = target;
                    camera.Render();
                    camera.targetTexture = null;
                    Graphics.Blit(target, resolved);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = resolved;
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    image.Apply(false);
                    RenderTexture.active = previous;
                    image.GetRawTextureData<byte>().CopyTo(frame);
                    stdin.Write(frame, 0, frame.Length);
                    var ls = root.Simulation.State;
                    log.AppendFormat(CultureInfo.InvariantCulture, "{0} t {1:0.000} s {2:0.0} x {3:0.00} y {4:0.00} v {5:0.0} {6}{7}{8} hits {9}\n", frames, frames / 30f, ls.S, ls.X, ls.Y, ls.Speed,
                        ls.Grounded ? "G" : "A", ls.Sliding ? "S" : "-", ls.Dead ? "D" : "-", ls.Hits);
                    frames++;

                    if (tail < 0 && root.Session.Phase == RunPhase.Results)
                    {
                        tail = Mathf.RoundToInt(TailSeconds * 30f);
                    }

                    if (tail >= 0 && tail-- == 0)
                    {
                        break;
                    }
                }

                stdin.Flush();
                stdin.Close();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Debug.LogError(LogPrefix + "ffmpeg exited with " + process.ExitCode);
                    return 3;
                }
            }

            File.WriteAllText(Path.ChangeExtension(file, ".frames.txt"), log.ToString());
            target.Release();
            resolved.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(resolved);
            Object.DestroyImmediate(image);
            camera.ResetAspect();

            var state = root.Simulation.State;
            long bytes = new FileInfo(file).Length;
            Debug.Log(LogPrefix + "Feel test video " + file + ": " + frames + " frames (" + (frames / 30f).ToString("0.0", CultureInfo.InvariantCulture) +
                      " s), " + (bytes / 1048576f).ToString("0.0", CultureInfo.InvariantCulture) + " MB, finished " + state.Finished +
                      ", dead " + state.Dead + (state.Dead ? " (" + state.Cause + ")" : string.Empty) + ", hits " + state.Hits +
                      ", distance " + state.Distance.ToString("0", CultureInfo.InvariantCulture) + " m");
            return 0;
        }

        private static bool InRanges(float[] ranges, float s)
        {
            for (int i = 0; i + 1 < ranges.Length; i += 2)
            {
                if (s >= ranges[i] && s <= ranges[i + 1])
                {
                    return true;
                }
            }

            return false;
        }

        private static float[] ParseRanges(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<float>();
            }

            string[] parts = text.Split(',');
            var result = new float[parts.Length * 2];
            for (int i = 0; i < parts.Length; i++)
            {
                string[] ab = parts[i].Split('-');
                result[2 * i] = float.Parse(ab[0], CultureInfo.InvariantCulture);
                result[(2 * i) + 1] = float.Parse(ab[1], CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static string FindFfmpeg()
        {
            string[] candidates = { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" };
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
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
    }
}
