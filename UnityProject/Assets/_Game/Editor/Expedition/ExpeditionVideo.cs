using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.UI.Expedition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.Expedition
{
    /// <summary>
    /// Bot video of Expedition 1 in the Expedition scene (needs a GPU: run Unity without -nographics):
    ///   tools/ci/unity.sh method JungleBooze.Editor.Expedition.ExpeditionVideo.Capture
    ///     [-jbVideoOut /tmp/junglebooze-video] [-jbVideoSizes 1280x592] [-jbVideoName expedition] [-jbUntil 2860]
    ///     [-jbFfmpeg /opt/homebrew/bin/ffmpeg] [-jbCrf 24]
    /// Same pipeline as FeelTestVideo: fixed 60 Hz ticks, every second tick rendered (30 fps) and piped as raw RGB
    /// into ffmpeg. The bot takes secret > risky > safe routes so the video shows the risky ridge, the crystals and
    /// the Veil Grotto. In-memory profile (no save is touched); the scene is not saved. A per-frame log
    /// (chunk, s, x, hits) is written next to the video.
    /// </summary>
    public static class ExpeditionVideo
    {
        private const string LogPrefix = "[JungleBooze] ";
        private const int TicksPerFrame = 2;
        private const int MaxTicks = 60 * 60 * 6;

        /// <summary>Ending: max ticks without input before giving up on the results screen.</summary>
        private const int EndingMaxTicks = 60 * 60;

        /// <summary>Seconds of results screen recorded.</summary>
        private const int ResultsSeconds = 5;

        /// <summary>Results count-up in the video, s.</summary>
        private const float ResultsCountUp = 1.5f;

        public static void Capture()
        {
            int code;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbVideoOut") ?? "/tmp/junglebooze-video";
                string sizes = Arg(args, "-jbVideoSizes") ?? "1280x592";
                string ffmpeg = Arg(args, "-jbFfmpeg") ?? FindFfmpeg();
                string name = Arg(args, "-jbVideoName") ?? "expedition";
                string crf = Arg(args, "-jbCrf") ?? "24";
                float until = float.Parse(Arg(args, "-jbUntil") ?? "2860", CultureInfo.InvariantCulture);
                code = Run(outDir, sizes, ffmpeg, name, crf, until);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Expedition video failed: " + exception);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        private static int Run(string outDir, string sizeList, string ffmpeg, string name, string crf, float until)
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
                int result = Record(width, height, landscape, file, ffmpeg, crf, until);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static int Record(int width, int height, bool landscape, string file, string ffmpeg, string crf, float until)
        {
            EditorSceneManager.OpenScene(ExpeditionPaths.Scene, OpenSceneMode.Single);
            ExpeditionRoot root = Object.FindFirstObjectByType<ExpeditionRoot>();
            if (root == null)
            {
                Debug.LogError(LogPrefix + "ExpeditionRoot not found: run ExpeditionBatch.Setup first.");
                return 2;
            }

            root.UseMemorySave(SaveData.CreateDefault(2026L, -0.3f));
            root.Build();
            root.SetCameraProfile(landscape, true);
            root.SetBotDriving(true);
            root.SetBotRoutes(RouteType.Secret, RouteType.Risky, RouteType.Safe);
            root.SetDebugVisible(false);
            root.StartRun();
            Camera camera = root.Camera;
            camera.aspect = (float)width / height;
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
                    "-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {0}x{1} -r 30 -i - -vf vflip -c:v libx264 -preset medium -crf {2} -pix_fmt yuv420p -movflags +faststart \"{3}\"",
                    width, height, crf, file),
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            var log = new System.Text.StringBuilder(1 << 20);
            int frames = 0;
            int ticks = 0;
            int endTick = -1;
            ExpeditionHud hud = null;
            GameObject hudObject = null;
            camera.targetTexture = target;
            camera.Render();
            camera.Render();
            camera.targetTexture = null;
            using (Process process = Process.Start(info))
            {
                Stream stdin = process.StandardInput.BaseStream;
                // Ending: past `until` the bot lets go, Pista runs into the next obstacles, and the results screen
                // (HUD canvas rendered by the capture camera) is recorded through its count-up.
                int resultsFrames = -1;
                while (ticks < MaxTicks && resultsFrames < ResultsSeconds * 30)
                {
                    if (root.Simulation.State.Distance >= until && !root.Simulation.State.Dead && endTick < 0)
                    {
                        endTick = ticks;
                        root.SetBotDriving(false);
                    }

                    if (endTick >= 0 && ticks - endTick > EndingMaxTicks && root.Session.Phase != RunPhase.Results && !root.Simulation.State.Dead)
                    {
                        Debug.LogWarning(LogPrefix + "ending: Pista survived " + (EndingMaxTicks / 60) + " s without input; no results screen recorded.");
                        break;
                    }

                    if (root.Session.Phase == RunPhase.Results && root.LastResults != null)
                    {
                        if (resultsFrames < 0)
                        {
                            // The scene builds its HUD only in Play mode: the tool builds one for the ending and
                            // shows the run's real results through it.
                            hudObject = new GameObject("VideoResultsHud");
                            hud = hudObject.AddComponent<ExpeditionHud>();
                            var store = new JungleBooze.Core.Settings.MemorySettingsStore();
                            hud.Build(null, 3, null, new JungleBooze.Gameplay.Feedback.FeelSettings(store, false), new JungleBooze.UI.Common.UiPreferences(store), root.Session.Content, () => root.Profile, Application.version);
                            hud.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                            hud.Canvas.worldCamera = camera;
                            hud.Canvas.planeDistance = camera.nearClipPlane + 0.5f;
                            hud.ShowResults(root.LastResults, ResultsCountUp);
                            hud.SetWallet(root.LastResults.WalletCoins, root.LastResults.WalletCrystals);
                            hud.SetDistance(root.LastResults.Distance);
                            Debug.Log(LogPrefix + "ending: results screen from frame " + frames);
                        }

                        hud.Tick(TicksPerFrame / 60f);
                        resultsFrames++;
                    }

                    root.StepTicks(TicksPerFrame);
                    ticks += TicksPerFrame;
                    camera.targetTexture = target;
                    if (resultsFrames >= 0)
                    {
                        // No player loop in batch mode: rebuild the HUD canvas against the bound target size.
                        Canvas.ForceUpdateCanvases();
                    }

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
                    RunnerState s = root.Simulation.State;
                    log.AppendFormat(CultureInfo.InvariantCulture, "{0} t {1:0.00} {2} s {3:0.0} x {4:0.00} y {5:0.00} v {6:0.0} hits {7} coins {8} crystals {9}\n", frames, frames / 30f,
                        root.Session.Stats.CurrentChunk, s.Distance, s.X, s.Y, s.Speed, s.Hits, root.Session.Stats.TotalCoins, root.Session.Stats.TotalCrystals);
                    frames++;
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
            if (hudObject != null)
            {
                Object.DestroyImmediate(hudObject);
            }

            RunnerState state = root.Simulation.State;
            var stats = root.Session.Stats;
            Debug.Log(LogPrefix + "Expedition video " + file + ": " + frames + " frames (" + (frames / 30f).ToString("0.0", CultureInfo.InvariantCulture) + " s), " +
                      (new FileInfo(file).Length / 1048576f).ToString("0.0", CultureInfo.InvariantCulture) + " MB, distance " + state.Distance.ToString("0", CultureInfo.InvariantCulture) +
                      " m, hits " + state.Hits + ", dead " + state.Dead + ", coins " + stats.TotalCoins + ", crystals " + stats.TotalCrystals + ", discoveries " + stats.NewDiscoveryCount +
                      ", routes risky/safe/secret " + stats.RiskyRoutes + "/" + stats.SafeRoutes + "/" + stats.SecretRoutes);
            return 0;
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
