using System.Collections;
using System.Collections.Generic;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Expedition;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.UI.Common;
using JungleBooze.UI.Expedition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// UI smoke tests in the Expedition scene: every button leads somewhere and Back always returns one level
    /// (home, journal, abilities → ability card, settings → credits, pause → settings → resume, camp, results),
    /// and in both orientations every visible control sits inside the safe area with a ≥ 44 pt touch target.
    /// </summary>
    public sealed class ExpeditionUiPlayModeTests
    {
        private static SaveData _profile;

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                go.GetComponent<ExpeditionRoot>()?.UseMemorySave(_profile);
            }
        }

        private static IEnumerator Load()
        {
            _profile = SaveData.CreateDefault(31L, -0.3f);
            _profile.runsCompleted = 2;
            _profile.bestDistance = 1800f;
            _profile.coins = 200;
            _profile.crystals = 3;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene("Expedition");
            yield return null;
            yield return null;
            SceneManager.sceneLoaded -= OnLoaded;
        }

        private static ExpeditionRoot Root()
        {
            var root = Object.FindFirstObjectByType<ExpeditionRoot>();
            Assert.IsNotNull(root);
            return root;
        }

        private static void Frames(ExpeditionRoot root, int n)
        {
            for (int i = 0; i < n; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            }
        }

        private static void Click(UiButton b)
        {
            Assert.IsTrue(b.Root.gameObject.activeInHierarchy, b.Root.name + " is visible");
            Assert.IsTrue(b.Button.interactable, b.Root.name + " is interactable");
            b.Button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator EveryButtonLeadsSomewhere_AndBackAlwaysWorks()
        {
            yield return Load();
            ExpeditionRoot root = Root();
            ExpeditionHud hud = root.Hud;
            root.ShowHome();
            Frames(root, 2);
            Assert.IsTrue(root.AtHome);
            Assert.AreEqual(UiScreen.Home, hud.CurrentScreen);
            StringAssert.Contains("1,800 m", hud.HomeView.BestText);

            Click(hud.HomeView.JournalButton);
            Assert.AreEqual(UiScreen.Journal, hud.CurrentScreen);
            Click(hud.JournalView.Shell.BackButton);
            Assert.AreEqual(UiScreen.Home, hud.CurrentScreen);

            Click(hud.HomeView.AbilitiesButton);
            Assert.AreEqual(UiScreen.Abilities, hud.CurrentScreen);
            root.OpenAbility(AbilityFlags.DeepBreath);
            Assert.AreEqual(UiScreen.Upgrade, hud.CurrentScreen);
            Assert.IsTrue(hud.LearnInteractable, "200 coins buy Deep Breath (150)");
            Click(hud.UpgradeView.Shell.BackButton);
            Assert.AreEqual(UiScreen.Abilities, hud.CurrentScreen);
            Assert.IsTrue(hud.Back());
            Assert.AreEqual(UiScreen.Home, hud.CurrentScreen);

            Click(hud.HomeView.SettingsButton);
            Assert.AreEqual(UiScreen.Settings, hud.CurrentScreen);
            Assert.IsTrue(hud.Back());
            Assert.AreEqual(UiScreen.Home, hud.CurrentScreen);
            Assert.IsFalse(hud.Back(), "Back at Home stays on Home");

            // START EXPEDITION → the run's HUD, control after the ready beat.
            Click(hud.HomeView.Start);
            Assert.IsFalse(root.AtHome);
            Assert.AreEqual(UiScreen.Hud, hud.CurrentScreen);
            Frames(root, 70);
            Assert.AreEqual(RunPhase.Running, root.Session.Phase);

            // Pause → Settings → Back → Resume (countdown) → running.
            hud.HudView.PauseRect.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(root.Paused);
            Assert.AreEqual(UiScreen.Pause, hud.CurrentScreen);
            Click(hud.PauseView.Settings);
            Assert.AreEqual(UiScreen.Settings, hud.CurrentScreen);
            Click(hud.SettingsView.Shell.BackButton);
            Assert.AreEqual(UiScreen.Pause, hud.CurrentScreen);
            Click(hud.PauseView.Resume);
            Assert.AreEqual(UiScreen.Hud, hud.CurrentScreen);
            Frames(root, 70);
            Assert.IsFalse(root.Paused);

            // Pause → Return to Camp.
            root.TogglePause();
            Click(hud.PauseView.Camp);
            Assert.IsTrue(root.AtHome);
            Assert.AreEqual(UiScreen.Home, hud.CurrentScreen);

            // Results → objective card → ability card → back → RUN AGAIN; results → Camp.
            Click(hud.HomeView.Start);
            for (int i = 0; i < 60 * 120 && !hud.ResultsVisible; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                if (root.ReviveOfferSeconds > 0f)
                {
                    Assert.IsTrue(hud.ReviveVisible);
                    Click(hud.ReviveView.Skip);
                }
            }

            Assert.IsTrue(hud.ResultsVisible, "the run ended on its own (no input)");
            Frames(root, 150);
            Assert.IsFalse(hud.CountingUp);
            Click(hud.ResultsView.Camp);
            Assert.IsTrue(root.AtHome);
            Click(hud.HomeView.Start);
            Assert.AreEqual(UiScreen.Hud, hud.CurrentScreen);
        }

        [UnityTest]
        public IEnumerator SettingsChanges_ApplyAndPersist()
        {
            yield return Load();
            ExpeditionRoot root = Root();
            ExpeditionHud hud = root.Hud;
            hud.Push(UiScreen.Settings);
            float before = root.Feel.Sensitivity;
            root.Feel.StepSensitivity(2);
            root.Preferences.SetTextSizeIndex(3);
            root.Preferences.SetLeftHanded(true);
            hud.ApplyTextScale();
            root.SettingsChanged();
            Frames(root, 2);
            Assert.AreEqual(before + 0.2f, root.Feel.Sensitivity, 1e-4f);
            Assert.AreEqual(1.5f, root.Preferences.TextScale, 1e-4f);
            Assert.Less(hud.HudView.Rects.Pause.X, hud.Frame.SafeWidth * 0.5f, "left-handed: pause moves left");
            root.Preferences.SetTextSizeIndex(0);
            root.Preferences.SetLeftHanded(false);
            root.Feel.StepSensitivity(-2);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryScreen_InsideTheSafeArea_44ptTargets_BothOrientations([Values(true, false)] bool landscape)
        {
            yield return Load();
            ExpeditionRoot root = Root();
            ExpeditionHud hud = root.Hud;
            DeviceProfile device = DeviceProfile.All[2];
            Vector2Int px = device.Pixels(landscape);
            int w = px.x / 2;
            int h = px.y / 2;
            var rt = new RenderTexture(w, h, 24);
            Camera cam = root.Camera;
            cam.targetTexture = rt;
            SafeAreaFitter.Simulated = SafeAreaFitter.Normalized(device, landscape);
            try
            {
                root.SetCameraProfile(landscape, true);
                hud.ConfigureForCapture(cam, w, h);
                Frames(root, 2);
                Assert.AreEqual(landscape, hud.Frame.Landscape, "canvas follows the target's orientation");

                root.ShowHome();
                CheckScreen(hud, "home");
                hud.Push(UiScreen.Settings);
                CheckScreen(hud, "settings");
                hud.Back();
                hud.Push(UiScreen.Journal);
                CheckScreen(hud, "journal");
                hud.Back();
                hud.Push(UiScreen.Abilities);
                CheckScreen(hud, "abilities");
                hud.Back();
                root.StartExpedition();
                Frames(root, 5);
                CheckScreen(hud, "hud");
                root.TogglePause();
                CheckScreen(hud, "pause");
                root.TogglePause();
                Frames(root, 70);
                var results = new RunResults { Distance = 2486f, Best = 2486f, NewRecord = true, TotalCoins = 320, CleanLineCoins = 36, PerfectCoins = 20, DiscoveryCoins = 50, Crystals = 4, CategoryCounts = "Creatures 1/3 · Locations 2/4" };
                results.NewDiscoveryNames.Add("Sailback");
                results.Objective = new NextObjective { Kind = ObjectiveKind.AbilityReady, Ability = AbilityFlags.DeepBreath, Progress = 150, Target = 150 };
                hud.ShowResults(results, 0.01f);
                CheckScreen(hud, "results");
                StringAssert.StartsWith("Deep Breath ready", hud.ObjectiveText);
                root.OpenAbility(AbilityFlags.DeepBreath);
                CheckScreen(hud, "ability card");
                hud.HideUpgrade(true);
                hud.HideResults();
                hud.ShowRevive(1, true, 3f, 4f);
                CheckScreen(hud, "revive");
                hud.HideRevive();
            }
            finally
            {
                SafeAreaFitter.Simulated = null;
                cam.targetTexture = null;
                rt.Release();
            }

            yield return null;
        }

        private static void CheckScreen(ExpeditionHud hud, string what)
        {
            for (int i = 0; i < 3; i++)
            {
                hud.Tick(0.5f);
                hud.ForceLayout();
            }

            Transform canvas = hud.Canvas.transform;
            const float unit = 1f;
            RectTransform safe = (RectTransform)hud.HudView.Root.parent;
            Rect safeWorld = LocalRect(canvas, safe);
            var buttons = new List<Button>();
            hud.GetComponentsInChildren(false, buttons);
            int checkedButtons = 0;
            foreach (Button b in buttons)
            {
                if (!b.gameObject.activeInHierarchy || b.GetComponentInParent<ScrollRect>() != null)
                {
                    continue;
                }

                CanvasGroup group = b.GetComponentInParent<CanvasGroup>();
                if (group != null && group.alpha < 0.99f)
                {
                    continue;
                }

                Rect r = LocalRect(canvas, (RectTransform)b.transform);
                string name = what + ": " + b.name;
                Assert.GreaterOrEqual(r.width / unit, 43.9f, name + " width");
                Assert.GreaterOrEqual(r.height / unit, 43.9f, name + " height");
                float tolerance = unit * 0.5f;
                Assert.IsTrue(r.xMin >= safeWorld.xMin - tolerance && r.xMax <= safeWorld.xMax + tolerance && r.yMin >= safeWorld.yMin - tolerance && r.yMax <= safeWorld.yMax + tolerance,
                    name + " outside the safe area: " + r + " vs " + safeWorld);
                checkedButtons++;
            }

            Assert.Greater(checkedButtons, 0, what + ": no visible buttons");
        }

        /// <summary>A rect in canvas units (canvas-local space; works for overlay and camera canvases).</summary>
        private static Rect LocalRect(Transform canvas, RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            for (int i = 0; i < 4; i++)
            {
                c[i] = canvas.InverseTransformPoint(c[i]);
            }

            float xMin = Mathf.Min(c[0].x, c[2].x);
            float xMax = Mathf.Max(c[0].x, c[2].x);
            float yMin = Mathf.Min(c[0].y, c[2].y);
            float yMax = Mathf.Max(c[0].y, c[2].y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
