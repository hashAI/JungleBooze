using System.Collections;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Analytics;
using JungleBooze.Gameplay.Movement;
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
                if (root.ReviveOfferSeconds > 0f)
                {
                    // Run 2 may offer "Continue?" (crystals picked up this run): the player skips it; the death
                    // fade then runs and the results follow.
                    root.DeclineRevive();
                    deathAt = t;
                    continue;
                }

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
        public IEnumerator AC103_49_TraversalFrames_SwimVineCanopyCreatures_AllocateNothing()
        {
            yield return Load(SaveData.CreateDefault(13L, -0.3f));
            ExpeditionRoot root = Root();
            root.SkipEstablishingShot();
            root.SetBotDriving(true);

            // Warm-up pass through the whole traversal stretch (first-use UI text, toasts, pools), then the same
            // Expedition 1 again: frames from the river to the canopy's end must allocate nothing.
            for (int i = 0; i < 60 * 240 && root.Simulation.State.Distance < 1900f && !root.Simulation.State.Dead; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            }

            root.BeginRun(new JungleBooze.Gameplay.Expedition.ExpeditionRunSetup { FirstExpedition = true, Skill = -0.3f, Discovered = id => false });
            for (int i = 0; i < 60 * 200 && root.Simulation.State.Distance < 940f && !root.Simulation.State.Dead; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 60 * 130 && root.Simulation.State.Distance < 1900f; i++)
                {
                    root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.GreaterOrEqual(root.Simulation.State.Distance, 1850f, "measured through C6–C8");
            Assert.AreEqual(0, root.Simulation.State.Hits);
            Assert.AreEqual(2, root.Session.Stats.PerfectReleases);
            yield return null;
        }

        /// <summary>
        /// Vine framing follow-up (2026-10-09): after a release the rope swings back toward the camera, which follows on
        /// the same line; no rope Pista isn't holding may cross the camera → Pista sight line through the air arc and
        /// the landing (both orientations).
        /// </summary>
        [UnityTest]
        public IEnumerator VineRelease_NoRopeBlocksTheCameraToPistaSightLine([Values(true, false)] bool landscape)
        {
            yield return Load(SaveData.CreateDefault(13L, -0.3f));
            ExpeditionRoot root = Root();
            root.SkipEstablishingShot();
            root.SetCameraProfile(landscape, true);
            root.SetBotDriving(true);
            var ropes = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("Vine") && t.name.Length <= 5)
                {
                    ropes.Add(t);
                }
            }

            Assert.AreEqual(8, ropes.Count, "vine rope pool");
            Animator pista = root.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(pista);
            int checkedFrames = 0;
            int releases = 0;
            int settle = 0;
            bool wasAir = false;
            float closest = float.MaxValue;
            for (int i = 0; i < 60 * 240 && root.Simulation.State.Distance < 1700f && !root.Simulation.State.Dead; i++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
                RunnerState s = root.Simulation.State;
                if (s.VineAir && !wasAir)
                {
                    releases++;
                    settle = 30;
                }

                wasAir = s.VineAir;
                if (!s.VineAir && settle-- <= 0)
                {
                    continue;
                }

                Vector3 eye = root.Camera.transform.position;
                Vector3 body = pista.transform.position + (Vector3.up * 1.0f);
                foreach (Transform rope in ropes)
                {
                    if (!rope.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Vector3 half = rope.up * rope.localScale.y;
                    float d = SegmentDistance(eye, body, rope.position - half, rope.position + half);
                    closest = Mathf.Min(closest, d);
                }

                checkedFrames++;
            }

            Debug.Log("[JungleBooze] vine occlusion " + (landscape ? "landscape" : "portrait") + ": " + releases + " releases, " + checkedFrames + " frames, closest rope to sight line " + closest.ToString("0.00") + " m");
            Assert.AreEqual(2, releases, "V1 and V2");
            Assert.Greater(checkedFrames, 100);
            Assert.Greater(closest, 0.3f, "a rope crossed the camera → Pista sight line");
        }

        private static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r);
            float b = Vector3.Dot(d1, d2);
            float denom = (a * e) - (b * b);
            float s = denom > 1e-6f ? Mathf.Clamp01(((b * f) - (c * e)) / denom) : 0f;
            float t = (b * s + f) / e;
            if (t < 0f)
            {
                t = 0f;
                s = Mathf.Clamp01(-c / a);
            }
            else if (t > 1f)
            {
                t = 1f;
                s = Mathf.Clamp01((b - c) / a);
            }

            return ((p1 + (d1 * s)) - (p2 + (d2 * t))).magnitude;
        }

        [UnityTest]
        public IEnumerator Revive_OfferedFromRun2_PaysACrystal_AndContinues()
        {
            SaveData veteran = SaveData.CreateDefault(19L, -0.3f);
            veteran.runsCompleted = 1;
            veteran.crystals = 3;
            yield return Load(veteran);
            ExpeditionRoot root = Root();
            for (int frame = 0; frame < 60 * 120 && root.ReviveOfferSeconds <= 0f; frame++)
            {
                root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            }

            Assert.Greater(root.ReviveOfferSeconds, 0f, "the offer shows on the first death of run 2");
            Assert.IsTrue(root.Hud.ReviveVisible);
            Assert.IsTrue(root.Hud.ReviveInteractable);
            root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            Assert.IsTrue(root.Simulation.State.Dead, "the run waits on the offer");
            Assert.IsTrue(root.AcceptRevive());
            Assert.IsFalse(root.Simulation.State.Dead);
            Assert.AreEqual(1, root.Session.Stats.ReviveCrystals);
            Assert.AreEqual(RunPhase.Ready, root.Session.Phase, "a 1.0 s ready beat at the revive point (review S4)");
            float s0 = root.Simulation.State.S;
            for (int i = 0; i < 58; i++)
            {
                root.StepTicks(1);
            }

            Assert.AreEqual(s0, root.Simulation.State.S, "frozen during the ready beat");
            for (int i = 0; i < 4; i++)
            {
                root.StepTicks(1);
            }

            Assert.AreEqual(RunPhase.Running, root.Session.Phase);
            Assert.Greater(root.Simulation.State.S, s0);
            Assert.AreEqual(1, root.Recording.MarkerCount, "the revive is in the replay (format 3)");
            root.Tick(1f / 60f, Time.realtimeSinceStartupAsDouble);
            Assert.IsFalse(root.Hud.ReviveVisible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AC103_50_AnalyticsEvents_OnDeviceOnly()
        {
            yield return Load(SaveData.CreateDefault(17L, -0.3f));
            ExpeditionRoot root = Root();
            root.SkipEstablishingShot();
            root.SetBotDriving(true);
            for (int i = 0; i < 60 * 200 && root.Simulation.State.Distance < 2080f && !root.Simulation.State.Dead; i++)
            {
                root.StepTicks(4);
            }

            AnalyticsRecorder a = root.Analytics;
            Assert.GreaterOrEqual(a.CountOf(AnalyticsEventType.TraversalResult), 6, "swim dives/leaps, vine releases, beam gaps");
            Assert.GreaterOrEqual(a.CountOf(AnalyticsEventType.CreatureFound), 1);
            Assert.GreaterOrEqual(a.CountOf(AnalyticsEventType.SecretFound), 1);
            Assert.AreEqual(1, a.CountOf(AnalyticsEventType.RunStarted));
            Assert.IsNull(root.AnalyticsLog.FilePath, "tests keep the log in memory; nothing leaves the device");
            int flushed = a.Flush(root.AnalyticsLog);
            Assert.AreEqual(flushed, root.AnalyticsLog.Lines.Count);
            StringAssert.StartsWith("{\"event\":\"run_started\"", root.AnalyticsLog.Lines[0]);
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
