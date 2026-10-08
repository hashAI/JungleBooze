using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Presentation
{
    /// <summary>
    /// Session loop without Unity: Ready prompt, fixed steps from real time, pause and resume countdown (spec 001
    /// section 8, logic of AC-47), death timing (10.6), Game Over input lock, Run again / Same track and the
    /// session best (spec 002 section 12).
    /// </summary>
    public sealed class GameSessionTests
    {
        private const double Frame = 1.0 / 60.0;

        private static GameSession Create(IInputProvider input = null, ulong seed = 42UL, bool begin = true)
        {
            var session = new GameSession(
                RunnerConfig.CreateDefault(),
                SpeedCurve.CreateDefault(),
                input ?? new BotInputProvider(),
                new FlatRunWorldFactory(),
                SessionTimings.CreateDefault(),
                seed);
            if (begin)
            {
                Assert.IsTrue(session.Begin());
            }

            return session;
        }

        private static void ToGameOver(GameSession s)
        {
            s.DebugEndRun();
            s.Advance(2.0);
            Assert.AreEqual(SessionPhase.GameOver, s.Phase);
        }

        private static void Frames(GameSession session, int count, double dt = Frame)
        {
            for (int i = 0; i < count; i++)
            {
                session.Advance(dt);
            }
        }

        [Test]
        public void NewSession_IsReady_AtTickZero_AndDoesNotStep()
        {
            GameSession s = Create(begin: false);
            Assert.AreEqual(SessionPhase.Ready, s.Phase);
            Assert.AreEqual(1, s.RunNumber);
            Assert.AreEqual(0L, s.Runner.NextTick);
            Assert.AreEqual(0L, s.Time.Tick);

            Frames(s, 120);
            Assert.AreEqual(SessionPhase.Ready, s.Phase);
            Assert.AreEqual(0L, s.Runner.NextTick, "Nothing steps before the first input (spec 002 12.1).");
            Assert.IsFalse(s.RequestPause(), "No pause in Ready.");
        }

        [Test]
        public void Begin_StartsRunning_OnlyFromReady()
        {
            GameSession s = Create(begin: false);
            Assert.IsTrue(s.Begin());
            Assert.AreEqual(SessionPhase.Running, s.Phase);
            Assert.IsFalse(s.Begin());

            s.Advance(Frame + 1e-9);
            Assert.AreEqual(1L, s.Runner.NextTick);
        }

        [Test]
        public void Advance_StepsFromRealTime_AndRunnerMovesForward()
        {
            GameSession s = Create();
            Frames(s, 60, Frame + 1e-9);
            Assert.AreEqual(60L, s.Runner.NextTick);
            Assert.AreEqual(s.Runner.NextTick, s.Time.Tick);
            Assert.Greater(s.DistanceM, 0.0);
        }

        [Test]
        public void Advance_CapsStepsPerFrame()
        {
            GameSession s = Create();
            Assert.AreEqual(GameSession.MaxStepsPerFrame, s.Advance(1.0));
        }

        [Test]
        public void Pause_StopsTicks_CountdownStopsTicks_FirstFrameAfterRunsAtMostOneStep()
        {
            GameSession s = Create();
            Frames(s, 30, Frame + 1e-9);
            long tickAtPause = s.Runner.NextTick;

            Assert.IsTrue(s.RequestPause());
            Frames(s, 300); // 5 s real time
            Assert.AreEqual(tickAtPause, s.Runner.NextTick);

            Assert.IsTrue(s.RequestResume());
            Assert.AreEqual(SessionPhase.Countdown, s.Phase);
            Assert.AreEqual(1.5, s.CountdownSecondsLeft, 1e-9);
            Frames(s, 89); // just under 1.5 s
            Assert.AreEqual(SessionPhase.Countdown, s.Phase);
            Assert.AreEqual(tickAtPause, s.Runner.NextTick);

            for (int i = 0; i < 3 && s.Phase == SessionPhase.Countdown; i++)
            {
                s.Advance(Frame);
            }

            Assert.AreEqual(SessionPhase.Running, s.Phase);
            Assert.AreEqual(tickAtPause, s.Runner.NextTick, "No steps in the frame the countdown ends.");

            int steps = s.Advance(Frame + 1e-9);
            Assert.LessOrEqual(steps, 1);
            Assert.AreEqual(InputCommand.PauseResumed, s.LastStepCommands & InputCommand.PauseResumed,
                "First tick after the countdown carries PauseResumed.");

            s.Advance(Frame + 1e-9);
            Assert.AreEqual(InputCommand.None, s.LastStepCommands & InputCommand.PauseResumed);
        }

        [Test]
        public void PauseDuringCountdown_StopsIt_AndResumeStartsFromThree()
        {
            GameSession s = Create();
            s.RequestPause();
            s.RequestResume();
            Frames(s, 45);
            Assert.IsTrue(s.RequestPause());
            Assert.AreEqual(SessionPhase.Paused, s.Phase);
            s.RequestResume();
            Assert.AreEqual(1.5, s.CountdownSecondsLeft, 1e-9);
        }

        [Test]
        public void TogglePause_PausesThenStartsCountdown()
        {
            GameSession s = Create();
            Assert.IsTrue(s.TogglePause());
            Assert.AreEqual(SessionPhase.Paused, s.Phase);
            Assert.IsTrue(s.TogglePause());
            Assert.AreEqual(SessionPhase.Countdown, s.Phase);
        }

        [Test]
        public void DebugEndRun_GoesThroughDying_ToGameOver_AfterHitPauseAndHold()
        {
            GameSession s = Create();
            Frames(s, 10, Frame + 1e-9);
            Assert.IsTrue(s.DebugEndRun());
            Assert.AreEqual(SessionPhase.Dying, s.Phase);
            Assert.IsTrue(s.InHitPause);
            long tick = s.Runner.NextTick;

            s.Advance(1.0); // < 0.35 + 0.8
            Assert.AreEqual(SessionPhase.Dying, s.Phase);
            Assert.IsFalse(s.InHitPause);
            s.Advance(0.2);
            Assert.AreEqual(SessionPhase.GameOver, s.Phase);
            Assert.AreEqual(tick, s.Runner.NextTick, "Nothing steps while dying.");
            Assert.IsFalse(s.RequestPause(), "No pause after death (spec 8.8).");
        }

        [Test]
        public void Restart_ResetsRunState_WithNewSeed()
        {
            GameSession s = Create();
            RunnerSimulation first = s.Runner;
            ulong firstSeed = s.RunSeed;
            Frames(s, 120, Frame + 1e-9);
            s.DebugEndRun();
            s.Advance(2.0);
            Assert.AreEqual(SessionPhase.GameOver, s.Phase);

            s.Restart();

            Assert.AreEqual(SessionPhase.Running, s.Phase, "Restart goes straight to Running (spec 002 12.2).");
            Assert.AreEqual(2, s.RunNumber);
            Assert.AreNotSame(first, s.Runner);
            Assert.AreNotEqual(firstSeed, s.RunSeed);
            Assert.AreEqual(0L, s.Runner.NextTick);
            Assert.AreEqual(0L, s.Time.Tick);
            Assert.AreEqual(0.0, s.DistanceM);
            Assert.AreEqual(0, s.Coins);
            Assert.AreEqual(s.Runner.Config.StartLane, s.Runner.Current.TargetLane);
        }

        [Test]
        public void RestartSameTrack_KeepsTheSeed_AndResetsTheRun()
        {
            GameSession s = Create();
            ulong seed = s.RunSeed;
            RunnerSimulation first = s.Runner;
            Frames(s, 60, Frame + 1e-9);
            ToGameOver(s);

            s.RestartSameTrack();

            Assert.AreEqual(SessionPhase.Running, s.Phase);
            Assert.AreEqual(seed, s.RunSeed);
            Assert.AreEqual(seed, s.World.Seed);
            Assert.AreNotSame(first, s.Runner);
            Assert.AreEqual(0L, s.Runner.NextTick);
            Assert.AreEqual(0.0, s.DistanceM);

            s.Restart();
            Assert.AreNotEqual(seed, s.RunSeed, "Run again picks a new seed.");
        }

        [Test]
        public void GameOverInputLock_LastsFourHundredMs()
        {
            GameSession s = Create();
            Assert.IsTrue(s.GameOverInputLocked, "Locked outside Game Over.");
            s.DebugEndRun();
            s.Advance(0.35 + 0.8 + 1e-6);
            Assert.AreEqual(SessionPhase.GameOver, s.Phase);
            Assert.IsTrue(s.GameOverInputLocked);

            s.Advance(0.39);
            Assert.IsTrue(s.GameOverInputLocked);
            s.Advance(0.02);
            Assert.IsFalse(s.GameOverInputLocked);
        }

        [Test]
        public void CompleteDying_ShowsGameOverAtOnce_OnlyWhileDying()
        {
            GameSession s = Create();
            Assert.IsFalse(s.CompleteDying());
            s.DebugEndRun();
            Assert.IsTrue(s.CompleteDying());
            Assert.AreEqual(SessionPhase.GameOver, s.Phase);
            Assert.IsTrue(s.GameOverInputLocked, "The lock starts when the panel appears.");
        }

        [Test]
        public void BestDistance_KeepsTheSessionMaximum()
        {
            GameSession s = Create();
            Frames(s, 120, Frame + 1e-9);
            double firstDistance = s.DistanceM;
            ToGameOver(s);
            Assert.AreEqual(firstDistance, s.BestDistanceM, 1e-9);
            Assert.IsTrue(s.LastRunWasBest);

            s.Restart();
            Assert.IsFalse(s.LastRunWasBest);
            Frames(s, 30, Frame + 1e-9);
            ToGameOver(s);
            Assert.IsFalse(s.LastRunWasBest);
            Assert.AreEqual(firstDistance, s.BestDistanceM, 1e-9, "A shorter run keeps the best.");
        }

        [Test]
        public void SameSessionSeed_GivesSameRunSeeds()
        {
            GameSession a = Create(seed: 7UL);
            GameSession b = Create(seed: 7UL);
            Assert.AreEqual(a.RunSeed, b.RunSeed);
            a.Restart();
            b.Restart();
            Assert.AreEqual(a.RunSeed, b.RunSeed);
        }

        [Test]
        public void Commands_FromInput_ReachTheRunner()
        {
            var bot = new BotInputProvider().At(0, InputCommand.MoveLeft);
            GameSession s = Create(bot);
            s.Advance(Frame + 1e-9);
            Assert.AreEqual(InputCommand.MoveLeft, s.LastStepCommands);
            Assert.AreEqual(s.Runner.Config.StartLane - 1, s.Runner.Current.TargetLane);
        }
    }
}
