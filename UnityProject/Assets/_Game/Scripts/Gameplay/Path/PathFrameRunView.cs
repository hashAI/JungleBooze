using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using UnityEngine;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Keeps a <see cref="PathFrame"/> in step with the run: rebuilds it from the run seed on <see cref="BeginRun"/>
    /// and extends it ahead of the hero every frame. Draws nothing and changes nothing in the simulation.
    /// In the editor and development builds F4 toggles the curved debug route for the next run (spec 003 T3).
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
        /// between the straight and the curved debug route (null: no switching).
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
                _selectable.Curved = !_selectable.Curved;
                Debug.Log("[JungleBooze] Debug route for the NEXT run: " + (_selectable.Curved ? "CURVED" : "straight") + " (F4).");
            }

            if (session.Runner != null)
            {
                _frame.Extend(session.Runner.Current.Z + _frame.Tuning.AheadM);
            }
        }

        private static bool DebugRouteKeyPressed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.f4Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.F4);
#else
            return false;
#endif
#else
            return false;
#endif
        }
    }
}
