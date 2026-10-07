using JungleBooze.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
#endif

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Editor / desktop keyboard bindings for First Playable (docs/PLAY_FIRST_BUILD.md, Step 5).
    /// Pure mapping functions so tests can check them without a keyboard device.
    /// <list type="table">
    /// <item>Left arrow / A: MoveLeft. Right arrow / D: MoveRight.</item>
    /// <item>Up arrow / W / Space: Jump. Down arrow / S: Slide. E / Left Shift: companion Lift (the double tap).</item>
    /// <item>P / Escape: pause toggle. R: Run again (Game Over). T: Same track (Game Over).
    /// K: end run (development builds only).</item>
    /// <item>Confirm keys (Space, Enter, keypad Enter): the primary button on Game Over (Run again).</item>
    /// <item>Any gameplay or confirm key starts the run from the Ready prompt.</item>
    /// </list>
    /// </summary>
    public static class KeyboardBindings
    {
#if ENABLE_INPUT_SYSTEM
        /// <summary>Every Input System key that has a binding. Polled once per frame without allocating.</summary>
        public static readonly Key[] InputSystemKeys =
        {
            Key.LeftArrow, Key.A,
            Key.RightArrow, Key.D,
            Key.UpArrow, Key.W, Key.Space,
            Key.DownArrow, Key.S,
            Key.E, Key.LeftShift,
            Key.Enter, Key.NumpadEnter,
            Key.P, Key.Escape,
            Key.R, Key.T,
            Key.K,
        };

        /// <summary>Simulation command for an Input System key, or <see cref="InputCommand.None"/>.</summary>
        public static InputCommand CommandForKey(Key key)
        {
            switch (key)
            {
                case Key.LeftArrow:
                case Key.A:
                    return InputCommand.MoveLeft;
                case Key.RightArrow:
                case Key.D:
                    return InputCommand.MoveRight;
                case Key.UpArrow:
                case Key.W:
                case Key.Space:
                    return InputCommand.Jump;
                case Key.DownArrow:
                case Key.S:
                    return InputCommand.Slide;
                case Key.E:
                case Key.LeftShift:
                    return InputCommand.CompanionAssist;
                default:
                    return InputCommand.None;
            }
        }

        /// <summary>Meta action for an Input System key, or <see cref="RunMetaAction.None"/>.</summary>
        public static RunMetaAction MetaActionForKey(Key key)
        {
            switch (key)
            {
                case Key.P:
                case Key.Escape:
                    return RunMetaAction.TogglePause;
                case Key.R:
                    return RunMetaAction.Restart;
                case Key.T:
                    return RunMetaAction.RestartSameTrack;
                case Key.K:
                    return RunMetaAction.DebugEndRun;
                default:
                    return RunMetaAction.None;
            }
        }

        /// <summary>True for keys that press the primary button of a panel (Space, Enter, keypad Enter).</summary>
        public static bool IsConfirmKey(Key key)
        {
            return key == Key.Space || key == Key.Enter || key == Key.NumpadEnter;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>Every legacy key code that has a binding. Polled once per frame without allocating.</summary>
        public static readonly KeyCode[] LegacyKeyCodes =
        {
            KeyCode.LeftArrow, KeyCode.A,
            KeyCode.RightArrow, KeyCode.D,
            KeyCode.UpArrow, KeyCode.W, KeyCode.Space,
            KeyCode.DownArrow, KeyCode.S,
            KeyCode.E, KeyCode.LeftShift,
            KeyCode.Return, KeyCode.KeypadEnter,
            KeyCode.P, KeyCode.Escape,
            KeyCode.R, KeyCode.T,
            KeyCode.K,
        };

        /// <summary>Simulation command for a legacy key code, or <see cref="InputCommand.None"/>.</summary>
        public static InputCommand CommandForKeyCode(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    return InputCommand.MoveLeft;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    return InputCommand.MoveRight;
                case KeyCode.UpArrow:
                case KeyCode.W:
                case KeyCode.Space:
                    return InputCommand.Jump;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    return InputCommand.Slide;
                case KeyCode.E:
                case KeyCode.LeftShift:
                    return InputCommand.CompanionAssist;
                default:
                    return InputCommand.None;
            }
        }

        /// <summary>Meta action for a legacy key code, or <see cref="RunMetaAction.None"/>.</summary>
        public static RunMetaAction MetaActionForKeyCode(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.P:
                case KeyCode.Escape:
                    return RunMetaAction.TogglePause;
                case KeyCode.R:
                    return RunMetaAction.Restart;
                case KeyCode.T:
                    return RunMetaAction.RestartSameTrack;
                case KeyCode.K:
                    return RunMetaAction.DebugEndRun;
                default:
                    return RunMetaAction.None;
            }
        }

        /// <summary>True for keys that press the primary button of a panel (Space, Return, keypad Enter).</summary>
        public static bool IsConfirmKeyCode(KeyCode key)
        {
            return key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter;
        }
#endif
    }
}
