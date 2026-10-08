using System.Collections;
using JungleBooze.App;
using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// Run lifecycle through the real driver and HUD (spec 002 section 12, spec 001 section 8): Ready → Running,
    /// pause button → pause menu → Resume → countdown, Game Over with its 400 ms input lock, Run again (new seed),
    /// Same track (same seed), confirm key, and backgrounding while dying. Every HUD button leads somewhere.
    /// The driver's own Update is switched off; each test drives frames with <see cref="RunDriver.Tick"/>, so real
    /// frame times do not matter.
    /// </summary>
    public sealed class RunDriverLifecycleTests
    {
        private const float Frame = 1f / 60f;
        private const ulong SessionSeed = 1234UL;

        private Scene _previous;
        private Scene _scene;
        private RunDriver _driver;
        private HudView _hud;
        private double _clock;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previous = SceneManager.GetActiveScene();
            _scene = SceneManager.CreateScene(RunSceneBootstrap.RunSceneName);
            SceneManager.SetActiveScene(_scene);

            _driver = RunSceneBootstrap.Build(_scene, RunConfigSet.CreateDefault(), SessionSeed);
            _driver.enabled = false;
            _hud = RunSceneBootstrapTests.FindInScene<HudView>(_scene);
            _clock = 0.0;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_previous.IsValid() && _previous.isLoaded)
            {
                SceneManager.SetActiveScene(_previous);
            }

            if (_scene.IsValid() && _scene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_scene);
            }

            _scene = default;
            _driver = null;
            _hud = null;
        }

        [UnityTest]
        public IEnumerator ReadyPrompt_FirstKeyStartsTheRun_WithoutMovingHero()
        {
            GameSession s = _driver.Session;
            Tick(10);
            Assert.AreEqual(SessionPhase.Ready, s.Phase);
            Assert.AreEqual(0L, s.Runner.NextTick);
            Assert.IsTrue(_hud.ReadyPromptVisible);

            int lane = s.Runner.Current.TargetLane;
            _driver.InputAdapter.InjectCommand(InputCommand.MoveRight);
            Tick(1);
            Assert.AreEqual(SessionPhase.Running, s.Phase);
            Assert.IsFalse(_hud.ReadyPromptVisible);

            Tick(20);
            Assert.AreEqual(lane, s.Runner.Current.TargetLane, "The starting key is not a command (spec 002 12.1).");

            _driver.InputAdapter.InjectCommand(InputCommand.MoveRight);
            Tick(1);
            Assert.AreEqual(lane + 1, s.Runner.Current.TargetLane, "Keys after the start are commands.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseButton_OpensPauseMenu_ResumeButton_StartsCountdown_ThenRuns()
        {
            StartRun();
            long tick = _driver.Session.Runner.NextTick;

            _hud.PauseButton.onClick.Invoke();
            Tick(1);
            Assert.AreEqual(SessionPhase.Paused, _driver.Session.Phase);
            Assert.IsTrue(_hud.PauseMenuVisible);
            Assert.AreEqual(tick, _driver.Session.Runner.NextTick);

            _hud.ResumeButton.onClick.Invoke();
            Tick(1);
            Assert.AreEqual(SessionPhase.Countdown, _driver.Session.Phase);
            Assert.IsFalse(_hud.PauseMenuVisible);

            Tick(100); // > 1.5 s
            Assert.AreEqual(SessionPhase.Running, _driver.Session.Phase);
            Assert.Greater(_driver.Session.Runner.NextTick, tick);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseKey_TogglesPauseAndCountdown()
        {
            StartRun();
            _driver.InputAdapter.InjectMetaAction(RunMetaAction.TogglePause);
            Tick(1);
            Assert.AreEqual(SessionPhase.Paused, _driver.Session.Phase);
            _driver.InputAdapter.InjectMetaAction(RunMetaAction.TogglePause);
            Tick(1);
            Assert.AreEqual(SessionPhase.Countdown, _driver.Session.Phase);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameOver_ShowsPanel_AndHidesPauseButton()
        {
            StartRun();
            Tick(60);
            ToGameOver();

            Assert.IsTrue(_hud.GameOverVisible);
            Assert.IsFalse(_hud.PauseButtonVisible, "No pause on Game Over (spec 002 12.1).");
            Assert.AreEqual(HudStrings.CauseEnded, _hud.CauseText, "A run ended with the debug key has no death cause.");
            Assert.Greater(_driver.Session.BestDistanceM, 0.0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameOverButtons_IgnoreInputFor400Ms()
        {
            StartRun();
            ToGameOver();
            int run = _driver.Session.RunNumber;

            Assert.IsFalse(_hud.RunAgainButton.interactable);
            Assert.IsFalse(_hud.SameTrackButton.interactable);
            _hud.RunAgainButton.onClick.Invoke();
            _driver.InputAdapter.InjectConfirm();
            _driver.InputAdapter.InjectMetaAction(RunMetaAction.Restart);
            Tick(1);
            Assert.AreEqual(SessionPhase.GameOver, _driver.Session.Phase, "Locked: every Game Over input is ignored.");
            Assert.AreEqual(run, _driver.Session.RunNumber);

            Tick(25); // 1 + 25 frames > 400 ms
            Assert.IsFalse(_driver.Session.GameOverInputLocked);
            Assert.IsTrue(_hud.RunAgainButton.interactable);
            Assert.IsTrue(_hud.SameTrackButton.interactable);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RunAgainButton_RestartsWithNewSeed_AndResetsSessionAndViews()
        {
            StartRun();
            Tick(120);
            ulong seed = _driver.Session.RunSeed;
            var oldRunner = _driver.Session.Runner;
            ToGameOver();
            Unlock();

            _hud.RunAgainButton.onClick.Invoke();

            GameSession s = _driver.Session;
            Assert.AreEqual(SessionPhase.Running, s.Phase, "Restart goes straight to Running (spec 002 12.2).");
            Assert.AreEqual(2, s.RunNumber);
            Assert.AreNotEqual(seed, s.RunSeed);
            Assert.AreNotSame(oldRunner, s.Runner);
            Assert.AreEqual(0L, s.Runner.NextTick);
            Assert.AreEqual(0.0, s.DistanceM);
            Assert.AreEqual(0, _driver.InputAdapter.QueuedCount);

            Tick(1);
            Assert.IsFalse(_hud.GameOverVisible);
            Assert.IsTrue(_hud.PauseButtonVisible);

            // Camera snapped back to the start pose (no smoothing from the death pose, spec 002 12.3).
            FollowCameraView cameraView = RunSceneBootstrapTests.FindInScene<FollowCameraView>(_scene);
            Assert.Less(cameraView.transform.position.z, 0f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameTrackButton_RestartsWithTheSameSeed()
        {
            StartRun();
            Tick(30);
            ulong seed = _driver.Session.RunSeed;
            ToGameOver();
            Unlock();

            _hud.SameTrackButton.onClick.Invoke();

            Assert.AreEqual(SessionPhase.Running, _driver.Session.Phase);
            Assert.AreEqual(seed, _driver.Session.RunSeed);
            Assert.AreEqual(0L, _driver.Session.Runner.NextTick);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Keys_OnGameOver_ConfirmRunsAgain_TRunsSameTrack()
        {
            StartRun();
            ulong seed = _driver.Session.RunSeed;
            ToGameOver();
            Unlock();
            _driver.InputAdapter.InjectMetaAction(RunMetaAction.RestartSameTrack);
            Tick(1);
            Assert.AreEqual(SessionPhase.Running, _driver.Session.Phase);
            Assert.AreEqual(seed, _driver.Session.RunSeed);

            ToGameOver();
            Unlock();
            _driver.InputAdapter.InjectConfirm();
            Tick(1);
            Assert.AreEqual(SessionPhase.Running, _driver.Session.Phase);
            Assert.AreNotEqual(seed, _driver.Session.RunSeed);
            Assert.AreEqual(3, _driver.Session.RunNumber);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Restart_IsIgnored_OutsideGameOver()
        {
            StartRun();
            int run = _driver.Session.RunNumber;
            Assert.IsFalse(_driver.TryRestart(false));
            Assert.IsFalse(_driver.TryRestart(true));
            Assert.AreEqual(run, _driver.Session.RunNumber);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Backgrounded_WhileDying_ShowsGameOverDirectly()
        {
            StartRun();
            _driver.Session.DebugEndRun();
            Assert.AreEqual(SessionPhase.Dying, _driver.Session.Phase);

            _driver.SendMessage("OnApplicationPause", true);
            Tick(1);
            Assert.AreEqual(SessionPhase.GameOver, _driver.Session.Phase);
            Assert.IsTrue(_hud.GameOverVisible);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Backgrounded_WhileRunning_Pauses()
        {
            StartRun();
            _driver.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(SessionPhase.Paused, _driver.Session.Phase);
            yield return null;
        }

        private void StartRun()
        {
            _driver.InputAdapter.InjectConfirm();
            Tick(1);
            Assert.AreEqual(SessionPhase.Running, _driver.Session.Phase);
        }

        private void ToGameOver()
        {
            Assert.IsTrue(_driver.Session.DebugEndRun());
            Tick(70); // > 350 ms hit-pause + 800 ms hold
            Assert.AreEqual(SessionPhase.GameOver, _driver.Session.Phase);
        }

        private void Unlock()
        {
            Tick(30); // > 400 ms
            Assert.IsFalse(_driver.Session.GameOverInputLocked);
        }

        private void Tick(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                _clock += Frame;
                _driver.Tick(Frame, _clock);
            }
        }
    }
}
