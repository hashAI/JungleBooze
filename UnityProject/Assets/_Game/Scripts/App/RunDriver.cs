using System;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using JungleBooze.UI.Hud;
using UnityEngine;

namespace JungleBooze.App
{
    /// <summary>
    /// The only per-frame entry point of a run (ARCHITECTURE section 4). Each frame: read device input, handle
    /// the Ready prompt, pause and Game Over keys, advance the <see cref="GameSession"/> by real frame time (fixed
    /// 60 Hz steps), hand the simulation's events to every view, then let every view render the interpolated state.
    /// Pauses on app background and focus loss (spec 001 8.1); never auto-resumes. Backgrounded while dying, it
    /// shows Game Over directly (spec 002 12.1). Game Over actions are ignored during the input lock.
    /// Menus (GDD 19): Play starts the run behind the main menu, Home leaves a paused or finished run for the main
    /// menu, Restart also works from the pause menu. With a <see cref="RunResultRecorder"/> attached, every run is
    /// banked into the save once (at its end, or when left from the pause menu) and the save is flushed when the
    /// app goes to the background.
    /// No allocations per frame (except the development-build error log on event-buffer overflow).
    /// </summary>
    public sealed class RunDriver : MonoBehaviour, IRunCommands
    {
        private GameSession _session;
        private PlayerInputAdapter _input;
        private IRunView[] _views;
        private GrayBoxKit _kit;
        private RunnerSimulation _overflowRunner;
        private int _reportedOverflow;
        private RunResultRecorder _recorder;
        private HudView _hud;

        public GameSession Session => _session;

        public PlayerInputAdapter InputAdapter => _input;

        /// <summary>Number of views driven (tests).</summary>
        public int ViewCount => _views == null ? 0 : _views.Length;

        /// <summary>
        /// Wires the driver and starts the first run's views. <paramref name="kit"/> (optional) is disposed with
        /// the driver.
        /// </summary>
        public void Init(GameSession session, PlayerInputAdapter input, IRunView[] views, GrayBoxKit kit)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _kit = kit;
            _input.GameplayEnabled = _session.Phase == SessionPhase.Running;
            BeginViews();
        }

        /// <summary>The save recorder, if attached.</summary>
        public RunResultRecorder Recorder => _recorder;

        /// <summary>
        /// Optional meta hooks: <paramref name="recorder"/> banks run results into the save; <paramref name="hud"/>
        /// lets the keyboard Play (Space/Enter on the main menu) wait while Settings is open. Either may be null.
        /// </summary>
        public void AttachMeta(RunResultRecorder recorder, HudView hud)
        {
            _recorder = recorder;
            _hud = hud;
        }

        /// <summary>Main menu "Play": the run set up behind the menu starts running. Ignored outside the menu.</summary>
        public void Play()
        {
            if (_session == null || _session.Phase != SessionPhase.Menu)
            {
                return;
            }

            if (_session.Begin())
            {
                _input.Reset();
                _input.GameplayEnabled = true;
            }
        }

        /// <summary>
        /// "Home" from the pause menu or Game Over (after the input lock): banks the run if needed, sets up a fresh
        /// run behind the main menu and snaps every view to it. Ignored in other phases.
        /// </summary>
        public void GoHome()
        {
            if (_session == null)
            {
                return;
            }

            SessionPhase phase = _session.Phase;
            bool allowed = phase == SessionPhase.Paused || (phase == SessionPhase.GameOver && !_session.GameOverInputLocked);
            if (!allowed)
            {
                return;
            }

            _recorder?.RecordIfLeaving(_session);
            _session.ReturnToMenu();
            _input.Reset();
            _input.GameplayEnabled = false;
            BeginViews();
        }

        public void Pause()
        {
            if (_session != null && _session.RequestPause())
            {
                _input.Reset();
            }
        }

        public void Resume()
        {
            _session?.RequestResume();
        }

        /// <summary>
        /// Game Over "Play again" (button, R, Space, Enter) or pause menu "Restart": new seed. Ignored in other
        /// phases and during the Game Over lock.
        /// </summary>
        public void Restart()
        {
            TryRestart(false);
        }

        /// <summary>Game Over "Same track" (button, T): same seed. Ignored outside Game Over and during the lock.</summary>
        public void RestartSameTrack()
        {
            TryRestart(true);
        }

        /// <summary>
        /// Restarts from Game Over once the input lock is over, or from the pause menu: rebuilds the run scope in
        /// place (no scene reload), clears input and snaps every view to the new run. Returns false if the restart
        /// was ignored.
        /// </summary>
        public bool TryRestart(bool sameTrack)
        {
            if (_session == null)
            {
                return false;
            }

            bool fromPause = _session.Phase == SessionPhase.Paused;
            if (!fromPause && (_session.Phase != SessionPhase.GameOver || _session.GameOverInputLocked))
            {
                return false;
            }

            if (fromPause)
            {
                _recorder?.RecordIfLeaving(_session);
            }

            if (sameTrack)
            {
                _session.RestartSameTrack();
            }
            else
            {
                _session.Restart();
            }

            _input.Reset();
            _input.GameplayEnabled = true;
            BeginViews();
            return true;
        }

        /// <summary>
        /// One frame of the run loop with an explicit real frame time. <see cref="Update"/> calls it with
        /// <see cref="Time.unscaledDeltaTime"/>; tests may call it directly.
        /// </summary>
        public void Tick(float realDeltaSeconds, double realTimeSeconds)
        {
            if (_session == null)
            {
                return;
            }

            // Input is read before the frame's steps (spec 001 12.6). Gameplay input only while running.
            _input.GameplayEnabled = _session.Phase == SessionPhase.Running;
            _input.Poll(realTimeSeconds);
            RunMetaAction meta = _input.ConsumeMetaAction();
            bool confirm = _input.ConsumeConfirm();
            bool startPress = _input.ConsumeStartPress();

            HandleMetaAction(meta);
            if (_session.Phase == SessionPhase.Ready && startPress)
            {
                // Spec 002 12.1: the first tap, swipe or key only starts the run (it was not queued).
                _session.Begin();
                _input.GameplayEnabled = true;
            }
            else if (_session.Phase == SessionPhase.Menu && confirm && (_hud == null || !_hud.SettingsVisible))
            {
                // Editor convenience: Space/Enter = Play. Taps and swipes never start a run from the menu.
                Play();
            }
            else if (_session.Phase == SessionPhase.GameOver && confirm)
            {
                TryRestart(false);
            }

            _session.Advance(realDeltaSeconds);
            _recorder?.RecordIfEnded(_session);

            DispatchEvents();

            float alpha = _session.InterpolationAlpha;
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].Render(_session, alpha, realDeltaSeconds);
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime, Time.unscaledTimeAsDouble);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                OnBackgrounded();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                OnBackgrounded();
            }
        }

        /// <summary>
        /// App backgrounded or lost focus: pause a running run; skip the rest of a death sequence (and bank it);
        /// write unsaved settings or results.
        /// </summary>
        private void OnBackgrounded()
        {
            if (_session != null && _session.CompleteDying())
            {
                _recorder?.RecordIfEnded(_session);
            }
            else
            {
                Pause();
            }

            _recorder?.Save.SaveIfDirty();
        }

        private void OnDestroy()
        {
            _kit?.Dispose();
            _kit = null;
        }

        private void HandleMetaAction(RunMetaAction action)
        {
            switch (action)
            {
                case RunMetaAction.TogglePause:
                    if (_session.Phase == SessionPhase.Paused)
                    {
                        Resume();
                    }
                    else
                    {
                        Pause();
                    }

                    break;

                case RunMetaAction.Restart:
                    TryRestart(false);
                    break;

                case RunMetaAction.RestartSameTrack:
                    TryRestart(true);
                    break;

                case RunMetaAction.DebugEndRun:
                    if (Debug.isDebugBuild)
                    {
                        _session.DebugEndRun();
                    }

                    break;
            }
        }

        private void DispatchEvents()
        {
            RunnerSimulation runner = _session.Runner;
            RunnerEventBuffer events = runner.Events;
            int count = events.Count;
            for (int e = 0; e < count; e++)
            {
                RunnerEvent item = events[e];
                for (int v = 0; v < _views.Length; v++)
                {
                    _views[v].OnRunnerEvent(in item);
                }
            }

            events.Clear();

            if (!ReferenceEquals(runner, _overflowRunner))
            {
                _overflowRunner = runner;
                _reportedOverflow = 0;
            }

            if (events.OverflowCount != _reportedOverflow)
            {
                if (Debug.isDebugBuild)
                {
                    Debug.LogError(
                        "[JungleBooze] Runner event buffer overflow: " + (events.OverflowCount - _reportedOverflow) +
                        " event(s) lost (capacity " + events.Capacity + "). Raise eventBufferCapacity or read events every frame.",
                        this);
                }

                _reportedOverflow = events.OverflowCount;
            }
        }

        private void BeginViews()
        {
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].BeginRun(_session);
            }
        }
    }
}
