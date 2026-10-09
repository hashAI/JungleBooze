using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.World;
using JungleBooze.UI.Common;
using JungleBooze.UI.Expedition;
using JungleBooze.Editor.Expedition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.UI
{
    /// <summary>
    /// Screenshots of every UI screen at the standard iPhone sizes, both orientations, with each device's safe area
    /// (needs a GPU: run Unity without -nographics):
    ///   tools/ci/unity.sh method JungleBooze.Editor.UI.UiScreenshots.Capture [-jbUiOut /tmp/junglebooze-ui]
    ///     [-jbUiDevices "iPhone SE 3,iPhone 12-14,iPhone 16 Pro Max"] [-jbUiScale 0.5]
    /// Drives the real ExpeditionRoot (in-memory profile, bot-driven run for the HUD) and writes
    /// &lt;device&gt;_&lt;orientation&gt;_&lt;nn-screen&gt;.png. Also writes layout.txt with every HUD rect per device.
    /// </summary>
    public static class UiScreenshots
    {
        private const string LogPrefix = "[JungleBooze] ";

        public static void Capture()
        {
            int code;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbUiOut") ?? "/tmp/junglebooze-ui";
                string devices = Arg(args, "-jbUiDevices") ?? "iPhone SE 3,iPhone 12-14,iPhone 16 Pro Max";
                float scale = float.Parse(Arg(args, "-jbUiScale") ?? "0.5", CultureInfo.InvariantCulture);
                code = Run(outDir, devices.Split(','), scale);
            }
            catch (Exception e)
            {
                Debug.LogError(LogPrefix + "UI screenshots failed: " + e);
                code = 1;
            }

            SafeAreaFitter.Simulated = null;
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        private static int Run(string outDir, string[] deviceNames, float scale)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogError(LogPrefix + "No GPU device: run Unity without -nographics.");
                return 2;
            }

            Directory.CreateDirectory(outDir);
            var log = new System.Text.StringBuilder();
            int shots = 0;
            foreach (string raw in deviceNames)
            {
                string name = raw.Trim();
                DeviceProfile device = Array.Find(DeviceProfile.All, d => d.Name == name);
                if (device.Name == null)
                {
                    Debug.LogError(LogPrefix + "Unknown device " + name);
                    return 2;
                }

                foreach (bool landscape in new[] { true, false })
                {
                    shots += CaptureDevice(device, landscape, scale, outDir, log);
                }
            }

            File.WriteAllText(Path.Combine(outDir, "layout.txt"), log.ToString());
            Debug.Log(LogPrefix + "UI screenshots: " + shots + " files in " + outDir);
            return 0;
        }

        private static int CaptureDevice(DeviceProfile device, bool landscape, float scale, string outDir, System.Text.StringBuilder log)
        {
            EditorSceneManager.OpenScene(ExpeditionPaths.Scene, OpenSceneMode.Single);
            ExpeditionRoot root = Object.FindFirstObjectByType<ExpeditionRoot>();
            SaveData profile = SaveData.CreateDefault(2026L, -0.3f);
            profile.runsCompleted = 3;
            profile.bestDistance = 2410f;
            profile.coins = 186;
            profile.crystals = 5;
            profile.journal.Add(new JournalRecord { id = "D-01", sightings = 3, firstRun = 1 });
            profile.journal.Add(new JournalRecord { id = "D-02", sightings = 2, firstRun = 1 });
            root.UseMemorySave(profile);
            root.EnableToolsUi();
            root.Build();
            root.SetCameraProfile(landscape, true);

            Vector2Int px = device.Pixels(landscape);
            int w = Mathf.RoundToInt(px.x * scale);
            int h = Mathf.RoundToInt(px.y * scale);
            SafeAreaFitter.Simulated = SafeAreaFitter.Normalized(device, landscape);
            Camera camera = root.Camera;
            var target = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            target.Create();
            camera.targetTexture = target; // the camera canvas takes its size from the target
            camera.aspect = (float)w / h;
            ExpeditionHud hud = root.Hud;
            hud.ConfigureForCapture(camera, w, h);
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.forceMatrixRecalculationPerRender = true;
            }

            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            string prefix = Path.Combine(outDir, Slug(device.Name) + "_" + (landscape ? "landscape" : "portrait") + "_");
            int n = 0;

            // 1 Home, then its screens.
            root.ShowHome();
            n += Shot(root, camera, target, image, prefix + "01-home.png");
            hud.Push(UiScreen.Journal);
            n += Shot(root, camera, target, image, prefix + "02-journal.png");
            hud.Back();
            hud.Push(UiScreen.Abilities);
            n += Shot(root, camera, target, image, prefix + "03-abilities.png");
            hud.Back();
            hud.Push(UiScreen.Settings);
            n += Shot(root, camera, target, image, prefix + "04-settings.png");
            hud.Back();

            // In-run HUD: the bot runs Expedition-style directed content for a while.
            root.StartExpedition();
            root.SetBotDriving(true);
            root.SetBotRoutes(RouteType.Risky, RouteType.Safe, RouteType.Secret);
            for (int i = 0; i < 60 * 22 && !root.Simulation.State.Dead; i += 4)
            {
                root.StepTicks(4);
            }

            hud.SetHealth(2, true, 0.62f);
            hud.ShowToast(hud.Strings.Get("hud.toastTitle"), "Unknown Species · Added to Journal");
            n += Shot(root, camera, target, image, prefix + "05-hud-toast.png");
            HudRects r = hud.HudView.Rects;
            log.Append(device.Name).Append(landscape ? " landscape" : " portrait").Append(": oneRow=").Append(r.OneRow)
                .Append(" distance ").Append(r.Distance).Append(" health ").Append(r.Health).Append(" coins ").Append(r.Coins)
                .Append(" crystals ").Append(r.Crystals).Append(" pause ").Append(r.Pause).Append(" toast ").Append(r.Toast)
                .Append(" | results two=").Append(hud.ResultsView.CurrentLayout.TwoColumns).Append(" fits=").Append(hud.ResultsView.CurrentLayout.Fits).Append('\n');
            hud.HideToast();
            hud.SetHelp((int)HelpMove.Jump);
            n += Shot(root, camera, target, image, prefix + "06-hud-hint.png");
            hud.SetHelp(-1);
            hud.ShowRevive(2, true, 2.6f, 4f);
            n += Shot(root, camera, target, image, prefix + "07-revive.png");
            hud.HideRevive();
            root.TogglePause();
            n += Shot(root, camera, target, image, prefix + "08-pause.png");
            hud.Push(UiScreen.Settings);
            n += Shot(root, camera, target, image, prefix + "09-pause-settings.png");
            hud.Back();
            hud.Back(); // resume (countdown)

            // Results with every line populated, then the ability card.
            RunResults results = SampleResults();
            hud.ShowResults(results, 0.01f);
            hud.CompleteCountUp();
            n += Shot(root, camera, target, image, prefix + "10-results.png");
            root.OpenAbility(AbilityFlags.DeepBreath);
            n += Shot(root, camera, target, image, prefix + "11-ability-card.png");
            hud.HideUpgrade(true);

            // Largest text (200 % menus, 130 % HUD/results).
            root.Preferences.SetTextSizeIndex(UiPreferences.TextScales.Length - 1);
            hud.ApplyTextScale();
            n += Shot(root, camera, target, image, prefix + "12-results-text200.png");
            hud.ShowHudOnly();
            n += Shot(root, camera, target, image, prefix + "13-hud-text200.png");
            root.ShowHome();
            hud.Push(UiScreen.Settings);
            n += Shot(root, camera, target, image, prefix + "14-settings-text200.png");
            hud.Back();
            root.Preferences.SetTextSizeIndex(0);
            root.Preferences.SetLeftHanded(true);
            hud.ApplyTextScale();
            root.StartExpedition();
            root.StepTicks(60);
            n += Shot(root, camera, target, image, prefix + "15-hud-lefthanded.png");
            root.Preferences.SetLeftHanded(false);

            Object.DestroyImmediate(image);
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            SafeAreaFitter.Simulated = null;
            return n;
        }

        private static int Shot(ExpeditionRoot root, Camera camera, RenderTexture target, Texture2D image, string file)
        {
            ExpeditionHud hud = root.Hud;
            for (int i = 0; i < 3; i++)
            {
                hud.Tick(0.5f);
                hud.ForceLayout();
            }

            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(file, image.EncodeToPNG());
            return 1;
        }

        private static RunResults SampleResults()
        {
            var r = new RunResults
            {
                Distance = 2486f,
                Best = 2486f,
                NewRecord = true,
                Coins = 214,
                CleanLineCoins = 36,
                PerfectCoins = 20,
                DiscoveryCoins = 50,
                TotalCoins = 320,
                Crystals = 4,
                CategoryCounts = "Creatures 1/3 · Locations 2/4",
            };
            r.NewDiscoveryNames.Add("Sailback");
            r.Objective = new NextObjective { Kind = ObjectiveKind.AbilityReady, Ability = AbilityFlags.DeepBreath, Progress = 150, Target = 150, Title = "Deep Breath ready · 150/150" };
            return r;
        }

        private static string Slug(string name)
        {
            return name.Replace(' ', '-').Replace("iPhone-", "iphone").ToLowerInvariant();
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
