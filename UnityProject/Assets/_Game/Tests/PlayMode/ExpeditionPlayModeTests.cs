using System.Collections;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// Smoke tests of the Expedition scene (spec 103 Part A): it is the first build scene, builds with the real
    /// content, the bot runs Expedition 1 through streamed chunks, death → results ≤ 1.0 s → RUN AGAIN → control
    /// ≤ 1.5 s (AC-103-45), and the frame loop allocates nothing after warm-up (AC-103-49 for Part A systems).
    /// The profile is kept in memory so tests never touch a real save.
    /// </summary>
    public sealed class ExpeditionPlayModeTests
    {
        private static SaveData _profile;

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            // Runs after Awake and before Start: switch the root to an in-memory profile.
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                var root = go.GetComponent<ExpeditionRoot>();
                if (root != null)
                {
                    root.UseMemorySave(_profile);
                }
            }
        }

        private static IEnumerator Load(SaveData profile)
        {
            _profile = profile;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene("Expedition");
            yield return null;
            yield return null;
            SceneManager.sceneLoaded -= OnLoaded;
        }

        private static ExpeditionRoot Root()
        {
            var root = Object.FindFirstObjectByType<ExpeditionRoot>();
            Assert.IsNotNull(root, "ExpeditionRoot missing (JungleBooze > Expedition > Build Scene)");
            return root;
        }

        [UnityTest]
        public IEnumerator ExpeditionIsTheFirstBuildScene_AndTheBotRunsExpedition1()
        {
            Assert.AreEqual("Expedition", System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)));
            yield return Load(SaveData.CreateDefault(7L, -0.3f));
            ExpeditionRoot root = Root();
            Assert.IsTrue(root.Session.FirstExpedition);
            Assert.Greater(root.EstablishingSeconds, 0f, "the establishing shot plays before Expedition 1");
            root.SkipEstablishingShot();
            root.SetBotDriving(true);
            root.StepTicks(60 * 50);
            Assert.Greater(root.Simulation.State.Distance, 450f);
            Assert.AreEqual(0, root.Simulation.State.Hits);
            Assert.Greater(root.WorldView.BoundChunks, 2, "streamed chunks are on screen");
            Assert.Greater(root.WorldView.ActiveCoinViews, 10);
            Assert.IsNotNull(root.Hud);
            Assert.IsNotNull(root.Avatar);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AC103_45_DeathToResults_RunAgainToControl()
        {
            SaveData veteran = SaveData.CreateDefault(9L, -0.3f);
            veteran.runsCompleted = 1;
            veteran.bestDistance = 3000f;
            veteran.coins = 160;
            yield return Load(veteran);
            ExpeditionRoot root = Root();
            Assert.IsFalse(root.Session.FirstExpedition, "run 2+: directed run with the Short start");

            // No input: the first obstacle ends a directed run eventually; then the results must follow.
            float deathAt = -1f;
            float resultsAt = -1f;
            for (int frame = 0; frame < 60 * 120 && resultsAt < 0f; frame++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                float t = frame / 60f;
                if (deathAt < 0f && root.Simulation.State.Dead)
                {
                    deathAt = t;
                }

                if (root.Hud.ResultsVisible)
                {
                    resultsAt = t;
                }
            }

            Assert.GreaterOrEqual(deathAt, 0f, "died");
            Assert.GreaterOrEqual(resultsAt, 0f, "results shown");
            Assert.LessOrEqual(resultsAt - deathAt, 1.0f + (1f / 60f), "results ≤ 1.0 s after the death (fade included)");
            Assert.IsNotNull(root.LastResults);
            Assert.AreEqual(2, root.Profile.runsCompleted);
            StringAssert.StartsWith("Deep Breath ready", root.Hud.ObjectiveText);

            // Objective → ability card → LEARN.
            root.OpenObjective();
            Assert.IsTrue(root.Hud.UpgradeVisible);
            Assert.IsTrue(root.Hud.LearnInteractable);
            Assert.IsTrue(root.Learn());
            Assert.Less(root.Profile.coins, 160);
            Assert.AreEqual(1, root.Profile.abilities & 1);

            // RUN AGAIN → control within 1.5 s.
            root.RunAgain();
            float control = -1f;
            for (int frame = 0; frame < 120 && control < 0f; frame++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                if (root.Session.Phase == RunPhase.Running)
                {
                    control = (frame + 1) / 60f;
                }
            }

            Assert.Greater(control, 0f);
            Assert.LessOrEqual(control, 1.5f);
            Assert.IsFalse(root.Hud.ResultsVisible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AC103_49_FrameLoop_AllocatesNothing_AfterWarmup()
        {
            yield return Load(SaveData.CreateDefault(11L, -0.3f));
            ExpeditionRoot root = Root();
            root.SkipEstablishingShot();
            root.SetBotDriving(true);
            for (int i = 0; i < 600; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 900; i++)
                {
                    root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.AreEqual(0, root.Simulation.State.Hits);
            yield return null;
        }
    }
}
