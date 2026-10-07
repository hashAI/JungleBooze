using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Keeps a <see cref="PathFrame"/> in step with the run: rebuilds it from the run seed on <see cref="BeginRun"/>
    /// and extends it ahead of the hero every frame. Draws nothing and changes nothing in the simulation.
    /// In the editor and development builds F4 (or a four-finger tap) cycles the route mode for the next run:
    /// Generated, Debug curve, Straight (spec 003 T3). The choice is saved in PlayerPrefs.
    /// </summary>
    public sealed class PathFrameRunView : IRunView
    {
        private readonly PathFrame _frame;
        private readonly SelectableRouteSource _selectable;

        public PathFrameRunView(PathFrame frame)
            : this(frame, null)
        {
        }

        /// <summary>
        /// <paramref name="selectable"/> is the route source behind <paramref name="frame"/> when F4 should switch
        /// between the route modes (null: no switching).
        /// </summary>
        public PathFrameRunView(PathFrame frame, SelectableRouteSource selectable)
        {
            _frame = frame ?? throw new System.ArgumentNullException(nameof(frame));
            _selectable = selectable;
        }

        /// <summary>The frame views will read through (T3).</summary>
        public PathFrame Frame => _frame;

        public void BeginRun(GameSession session)
        {
            _frame.BeginRun(session.RunSeed, -_frame.Tuning.BehindM);
            if (session.Runner != null)
            {
                _frame.Extend(session.Runner.Current.Z + _frame.Tuning.AheadM);
            }
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_selectable != null && DebugRouteKeyPressed())
            {
                _selectable.Mode = RouteModeSettings.Next(_selectable.Mode);
                RouteModeSettings.Save(_selectable.Mode);
                Debug.Log("[JungleBooze] Route mode for the NEXT run: " + _selectable.Mode + " (F4).");
            }

            if (session.Runner != null)
            {
                _frame.Extend(session.Runner.Current.Z + _frame.Tuning.AheadM);
            }
        }

        // The Game view must have focus for the key to register (click once inside it). The Input System package is
        // used when it is enabled (the project setting is Both); the legacy manager is the fallback.
        private static bool DebugRouteKeyPressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame)
            {
                return true;
            }

            // Touch devices (development builds): put a fourth finger down while at least four are on the screen.
            UnityEngine.InputSystem.Touchscreen screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen != null)
            {
                bool began = false;
                int down = 0;
                for (int i = 0; i < screen.touches.Count; i++)
                {
                    UnityEngine.InputSystem.Controls.TouchControl touch = screen.touches[i];
                    if (touch.press.isPressed)
                    {
                        down++;
                    }

                    if (touch.press.wasPressedThisFrame)
                    {
                        began = true;
                    }
                }

                return began && down >= 4;
            }

            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.F4))
            {
                return true;
            }

            if (UnityEngine.Input.touchCount >= 4)
            {
                for (int i = 0; i < UnityEngine.Input.touchCount; i++)
                {
                    if (UnityEngine.Input.GetTouch(i).phase == TouchPhase.Began)
                    {
                        return true;
                    }
                }
            }

            return false;
#else
            return false;
#endif
#else
            return false;
#endif
        }
    }
}
