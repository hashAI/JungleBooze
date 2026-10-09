using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using JungleBooze.Core;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace JungleBooze.Gameplay.Controls
{
    /// <summary>
    /// Thin adapter from the Input System to <see cref="GestureRecognizer"/> and <see cref="FrameInputDispatcher"/>
    /// (spec 101 §3.3–3.4). Touches arrive through Enhanced Touch finger callbacks (every sample, in order, with
    /// its own timestamp), converted from pixels to iOS points. In the editor and on desktop, the left mouse button
    /// emulates one touch (pixels → points by <see cref="GestureConfig.EditorPixelsPerPoint"/>) and the keyboard
    /// maps A/D/arrows (steer), W/Up/Space (jump), S/Down (slide), Q/E (dodge). Touches that begin over UI belong to
    /// the UI. No allocation per frame.
    /// </summary>
    public sealed class UnityPointerInput : IDisposable
    {
        private const int MouseTouchId = 1 << 20;
        private const int MaxIgnored = 8;

        private readonly GestureRecognizer _gestures;
        private readonly FrameInputDispatcher _dispatcher;
        private readonly GestureConfig _config;
        private readonly int[] _ignored = new int[MaxIgnored];
        private readonly Action<Finger> _onDown;
        private readonly Action<Finger> _onMove;
        private readonly Action<Finger> _onUp;
        private int _ignoredCount;
        private bool _mouseDown;
        private bool _subscribed;

        public UnityPointerInput(GestureRecognizer gestures, FrameInputDispatcher dispatcher, GestureConfig config)
        {
            _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _onDown = OnFingerDown;
            _onMove = OnFingerMove;
            _onUp = OnFingerUp;
            PixelsPerPoint = ScreenPointScale.PixelsPerPoint(_config.EditorPixelsPerPoint);
        }

        /// <summary>Returns true when a pointer (touch id, pixel position) is over UI. Set by the composition root.</summary>
        public Func<int, Vector2, bool> IsOverUi { get; set; }

        /// <summary>When false, touches, mouse and keyboard actions are read but discarded (bot driving, paused).</summary>
        public bool Enabled { get; set; } = true;

        public float PixelsPerPoint { get; set; }

        public void Enable()
        {
            if (_subscribed)
            {
                return;
            }

            EnhancedTouchSupport.Enable();
            EnhancedTouch.onFingerDown += _onDown;
            EnhancedTouch.onFingerMove += _onMove;
            EnhancedTouch.onFingerUp += _onUp;
            _subscribed = true;
        }

        public void Dispose()
        {
            if (!_subscribed)
            {
                return;
            }

            EnhancedTouch.onFingerDown -= _onDown;
            EnhancedTouch.onFingerMove -= _onMove;
            EnhancedTouch.onFingerUp -= _onUp;
            EnhancedTouchSupport.Disable();
            _subscribed = false;
        }

        /// <summary>Reads mouse and keyboard for this frame. Call once per Update before the simulation steps.</summary>
        public void PollFrame(float frameSeconds, double now)
        {
            PollMouse(now);
            PollKeyboard(frameSeconds);
        }

        private void PollMouse(double now)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed))
            {
                return;
            }

            Vector2 pixels = mouse.position.ReadValue();
            float scale = _config.EditorPixelsPerPoint > 0f ? _config.EditorPixelsPerPoint : 1f;
            float x = pixels.x / scale;
            float y = pixels.y / scale;
            bool pressed = mouse.leftButton.isPressed;
            if (pressed && !_mouseDown)
            {
                _mouseDown = true;
                if (!Enabled || (IsOverUi != null && IsOverUi(-1, pixels)))
                {
                    AddIgnored(MouseTouchId);
                    return;
                }

                _gestures.Process(new TouchSample(MouseTouchId, TouchPhaseKind.Began, x, y, now));
            }
            else if (pressed)
            {
                if (!IsIgnored(MouseTouchId))
                {
                    _gestures.Process(new TouchSample(MouseTouchId, TouchPhaseKind.Moved, x, y, now));
                }
            }
            else if (_mouseDown)
            {
                _mouseDown = false;
                if (!RemoveIgnored(MouseTouchId))
                {
                    _gestures.Process(new TouchSample(MouseTouchId, TouchPhaseKind.Ended, x, y, now));
                }
            }
        }

        private void PollKeyboard(float frameSeconds)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !Enabled)
            {
                return;
            }

            int steer = 0;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                steer--;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                steer++;
            }

            if (steer != 0)
            {
                _dispatcher.AddLateral(steer * _config.KeyboardLateralSpeed * (double)frameSeconds);
            }

            // Keyboard commands share the gesture queue so mixed sources are delivered in recognition order.
            CommandQueue queue = _gestures.Commands;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                queue.TryEnqueue(InputCommand.Jump);
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                queue.TryEnqueue(InputCommand.Slide);
            }

            if (keyboard.qKey.wasPressedThisFrame)
            {
                queue.TryEnqueue(InputCommand.DodgeLeft);
            }

            if (keyboard.eKey.wasPressedThisFrame)
            {
                queue.TryEnqueue(InputCommand.DodgeRight);
            }
        }

        private void OnFingerDown(Finger finger)
        {
            EnhancedTouch touch = finger.currentTouch;
            Vector2 pixels = touch.screenPosition;
            if (!Enabled || (IsOverUi != null && IsOverUi(touch.touchId, pixels)))
            {
                AddIgnored(touch.touchId);
                return;
            }

            _gestures.Process(ToSample(touch, TouchPhaseKind.Began));
        }

        private void OnFingerMove(Finger finger)
        {
            EnhancedTouch touch = finger.currentTouch;
            if (IsIgnored(touch.touchId))
            {
                return;
            }

            _gestures.Process(ToSample(touch, TouchPhaseKind.Moved));
        }

        private void OnFingerUp(Finger finger)
        {
            EnhancedTouch touch = finger.currentTouch;
            if (RemoveIgnored(touch.touchId))
            {
                return;
            }

            TouchPhaseKind phase = touch.phase == InputTouchPhase.Canceled ? TouchPhaseKind.Canceled : TouchPhaseKind.Ended;
            _gestures.Process(ToSample(touch, phase));
        }

        private TouchSample ToSample(in EnhancedTouch touch, TouchPhaseKind phase)
        {
            Vector2 pixels = touch.screenPosition;
            float scale = PixelsPerPoint > 0f ? PixelsPerPoint : 1f;
            return new TouchSample(touch.touchId, phase, pixels.x / scale, pixels.y / scale, touch.time);
        }

        private void AddIgnored(int id)
        {
            if (!IsIgnored(id) && _ignoredCount < MaxIgnored)
            {
                _ignored[_ignoredCount++] = id;
            }
        }

        private bool IsIgnored(int id)
        {
            for (int i = 0; i < _ignoredCount; i++)
            {
                if (_ignored[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private bool RemoveIgnored(int id)
        {
            for (int i = 0; i < _ignoredCount; i++)
            {
                if (_ignored[i] == id)
                {
                    _ignored[i] = _ignored[--_ignoredCount];
                    return true;
                }
            }

            return false;
        }
    }
}
