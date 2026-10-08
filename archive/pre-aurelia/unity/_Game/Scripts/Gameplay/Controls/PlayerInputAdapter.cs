using System;
using JungleBooze.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Device input for a run: keyboard (editor), mouse-drag swipes (editor) and touch swipes (iPhone).
    /// The run driver calls <see cref="Poll"/> once per rendered frame before the simulation steps (spec 001 12.6);
    /// recognized commands wait in a fixed queue and <see cref="ReadCommands"/> hands out one per tick (spec 12.5).
    /// Uses the Input System when it is enabled (<c>ENABLE_INPUT_SYSTEM</c>), otherwise the legacy input manager.
    /// Only the first touch counts (the Input System's primary touch / legacy touch 0). No allocations per frame.
    /// </summary>
    public sealed class PlayerInputAdapter : IInputProvider
    {
        /// <summary>Screen density of one iOS point (1x). Used to convert pixels to points.</summary>
        public const float PointDpi = 163f;

        private readonly SwipeRecognizer _swipes;
        private readonly TapRecognizer _taps;
        private readonly CommandQueue _queue;
        private readonly float _pixelsPerPoint;
        private RunMetaAction _pendingMeta;
        private bool _confirmPressed;
        private bool _startPressed;
        private bool _gameplayEnabled = true;

        public PlayerInputAdapter(InputConfig config, float pixelsPerPoint)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _swipes = new SwipeRecognizer(config);
            _taps = new TapRecognizer(config);
            _queue = new CommandQueue(config.GestureQueueCapacity);
            _pixelsPerPoint = pixelsPerPoint > 0f ? pixelsPerPoint : 1f;
        }

        /// <summary>
        /// While false (paused, countdown, dying, Game Over) no gameplay command is recognized; switching it off
        /// cancels the tracked touch and clears the queue (spec 001 8.2), and a touch that started while it was off
        /// never produces a swipe (spec 8.3 and AC-49). Meta keys (pause, restart) always work.
        /// </summary>
        public bool GameplayEnabled
        {
            get => _gameplayEnabled;
            set
            {
                if (_gameplayEnabled && !value)
                {
                    // Presses (start, confirm) survive: they belong to the phase that is starting.
                    _swipes.Cancel();
                    _taps.Cancel();
                    _queue.Clear();
                }

                _gameplayEnabled = value;
            }
        }

        /// <summary>Commands waiting for the simulation.</summary>
        public int QueuedCount => _queue.Count;

        /// <summary>Pixels per point for a screen dpi: iOS points are 1/163 inch [ASSUMED approximation].</summary>
        public static float PixelsPerPointForDpi(float dpi)
        {
            if (!(dpi > 0f))
            {
                return 1f;
            }

            float ppp = dpi / PointDpi;
            return ppp < 1f ? 1f : (ppp > 4f ? 4f : ppp);
        }

        /// <summary>One command per tick, oldest first. Never allocates.</summary>
        public InputCommand ReadCommands(long tick)
        {
            return _queue.Dequeue();
        }

        /// <summary>The meta action pressed since the last call (latest wins), then clears it.</summary>
        public RunMetaAction ConsumeMetaAction()
        {
            RunMetaAction action = _pendingMeta;
            _pendingMeta = RunMetaAction.None;
            return action;
        }

        /// <summary>
        /// Cancels the tracked touch and drops queued commands and unconsumed presses (pause, focus loss, restart).
        /// </summary>
        public void Reset()
        {
            _swipes.Cancel();
            _taps.Cancel();
            _queue.Clear();
            _confirmPressed = false;
            _startPressed = false;
        }

        /// <summary>
        /// Feeds a gameplay command as if a key had been pressed (tests, on-screen debug buttons). Not queued while
        /// <see cref="GameplayEnabled"/> is false, but still counts as a start press. Returns true if queued.
        /// </summary>
        public bool InjectCommand(InputCommand command)
        {
            if (command == InputCommand.None)
            {
                return false;
            }

            int before = _queue.Count;
            OnKey(command, RunMetaAction.None, false);
            return _queue.Count > before;
        }

        /// <summary>
        /// True once if a confirm key (Space, Enter) was pressed since the last call, then clears it. Pressed even
        /// while <see cref="GameplayEnabled"/> is false (Game Over's primary button).
        /// </summary>
        public bool ConsumeConfirm()
        {
            bool pressed = _confirmPressed;
            _confirmPressed = false;
            return pressed;
        }

        /// <summary>
        /// True once if any gameplay key, confirm key, tap or click started since the last call, then clears it.
        /// The Ready prompt uses it to start the run (spec 002 12.1); pause and restart keys do not count.
        /// </summary>
        public bool ConsumeStartPress()
        {
            bool pressed = _startPressed;
            _startPressed = false;
            return pressed;
        }

        /// <summary>Feeds a confirm key press (tests).</summary>
        public void InjectConfirm()
        {
            OnKey(InputCommand.None, RunMetaAction.None, true);
        }

        /// <summary>Feeds a meta action (tests).</summary>
        public void InjectMetaAction(RunMetaAction action)
        {
            if (action != RunMetaAction.None)
            {
                _pendingMeta = action;
            }
        }

        /// <summary>
        /// Feeds one pointer sample in screen pixels (y up). <paramref name="pressedThisFrame"/> marks the touch-down
        /// sample; <paramref name="isPressed"/> is false on the lift sample.
        /// </summary>
        public void FeedPointer(bool pressedThisFrame, bool isPressed, Vector2 screenPixels, double nowSeconds)
        {
            if (pressedThisFrame)
            {
                _startPressed = true;
            }

            if (!_gameplayEnabled)
            {
                if (_swipes.IsTracking)
                {
                    _swipes.Cancel();
                }

                _taps.Cancel();
                return;
            }

            float x = screenPixels.x / _pixelsPerPoint;
            float y = screenPixels.y / _pixelsPerPoint;

            if (pressedThisFrame)
            {
                _swipes.Begin(x, y, nowSeconds);
                _taps.Begin(x, y, nowSeconds);
            }

            if (!_swipes.IsTracking)
            {
                return;
            }

            InputCommand swipe = _swipes.Move(x, y, nowSeconds);
            if (swipe != InputCommand.None)
            {
                _queue.TryEnqueue(swipe);
            }

            _taps.Move(x, y, swipe != InputCommand.None);

            if (!isPressed)
            {
                _swipes.End();

                // GDD 5.1: double tap = companion Assist (Lift).
                if (_taps.End(nowSeconds))
                {
                    _queue.TryEnqueue(InputCommand.CompanionAssist);
                }
            }
        }

        /// <summary>Reads keyboard, touch and mouse for this frame. Call once per rendered frame.</summary>
        public void Poll(double nowSeconds)
        {
#if ENABLE_INPUT_SYSTEM
            PollInputSystem(nowSeconds);
#elif ENABLE_LEGACY_INPUT_MANAGER
            PollLegacy(nowSeconds);
#endif
        }

        private void OnKey(InputCommand command, RunMetaAction meta, bool confirm)
        {
            if (meta != RunMetaAction.None)
            {
                _pendingMeta = meta;
                return;
            }

            if (confirm)
            {
                _confirmPressed = true;
            }

            if (confirm || command != InputCommand.None)
            {
                _startPressed = true;
            }

            if (_gameplayEnabled && command != InputCommand.None)
            {
                _queue.TryEnqueue(command);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void PollInputSystem(double nowSeconds)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Key[] keys = KeyboardBindings.InputSystemKeys;
                for (int i = 0; i < keys.Length; i++)
                {
                    Key key = keys[i];
                    if (keyboard[key].wasPressedThisFrame)
                    {
                        OnKey(
                            KeyboardBindings.CommandForKey(key),
                            KeyboardBindings.MetaActionForKey(key),
                            KeyboardBindings.IsConfirmKey(key));
                    }
                }
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var press = touchscreen.primaryTouch.press;
                if (press.isPressed || press.wasPressedThisFrame || press.wasReleasedThisFrame)
                {
                    FeedPointer(
                        press.wasPressedThisFrame,
                        press.isPressed,
                        touchscreen.primaryTouch.position.ReadValue(),
                        nowSeconds);
                    return;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                var button = mouse.leftButton;
                if (button.isPressed || button.wasPressedThisFrame || button.wasReleasedThisFrame)
                {
                    FeedPointer(button.wasPressedThisFrame, button.isPressed, mouse.position.ReadValue(), nowSeconds);
                }
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private void PollLegacy(double nowSeconds)
        {
            KeyCode[] codes = KeyboardBindings.LegacyKeyCodes;
            for (int i = 0; i < codes.Length; i++)
            {
                KeyCode code = codes[i];
                if (UnityEngine.Input.GetKeyDown(code))
                {
                    OnKey(
                        KeyboardBindings.CommandForKeyCode(code),
                        KeyboardBindings.MetaActionForKeyCode(code),
                        KeyboardBindings.IsConfirmKeyCode(code));
                }
            }

            if (UnityEngine.Input.touchCount > 0)
            {
                UnityEngine.Touch touch = UnityEngine.Input.GetTouch(0);
                bool began = touch.phase == UnityEngine.TouchPhase.Began;
                bool held = touch.phase != UnityEngine.TouchPhase.Ended && touch.phase != UnityEngine.TouchPhase.Canceled;
                FeedPointer(began, held, touch.position, nowSeconds);
                return;
            }

            if (UnityEngine.Input.mousePresent)
            {
                bool down = UnityEngine.Input.GetMouseButtonDown(0);
                bool held = UnityEngine.Input.GetMouseButton(0);
                bool up = UnityEngine.Input.GetMouseButtonUp(0);
                if (down || held || up)
                {
                    Vector3 p = UnityEngine.Input.mousePosition;
                    FeedPointer(down, held, new Vector2(p.x, p.y), nowSeconds);
                }
            }
        }
#endif
    }
}
