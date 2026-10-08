using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
#endif

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// Keyboard controls from docs/PLAY_FIRST_BUILD.md Step 5 map to the right commands and actions, and a key press
    /// on a keyboard device reaches the simulation's input stream as the right <see cref="InputCommand"/>.
    /// </summary>
    public sealed class KeyboardBindingsTests
    {
#if ENABLE_INPUT_SYSTEM
        [TestCase(Key.LeftArrow, InputCommand.MoveLeft)]
        [TestCase(Key.A, InputCommand.MoveLeft)]
        [TestCase(Key.RightArrow, InputCommand.MoveRight)]
        [TestCase(Key.D, InputCommand.MoveRight)]
        [TestCase(Key.UpArrow, InputCommand.Jump)]
        [TestCase(Key.W, InputCommand.Jump)]
        [TestCase(Key.Space, InputCommand.Jump)]
        [TestCase(Key.DownArrow, InputCommand.Slide)]
        [TestCase(Key.S, InputCommand.Slide)]
        [TestCase(Key.Enter, InputCommand.None)]
        [TestCase(Key.P, InputCommand.None)]
        [TestCase(Key.Q, InputCommand.None)]
        public void InputSystemKey_MapsToCommand(Key key, InputCommand expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.CommandForKey(key));
        }

        [TestCase(Key.P, RunMetaAction.TogglePause)]
        [TestCase(Key.Escape, RunMetaAction.TogglePause)]
        [TestCase(Key.R, RunMetaAction.Restart)]
        [TestCase(Key.T, RunMetaAction.RestartSameTrack)]
        [TestCase(Key.K, RunMetaAction.DebugEndRun)]
        [TestCase(Key.Space, RunMetaAction.None)]
        [TestCase(Key.Enter, RunMetaAction.None)]
        public void InputSystemKey_MapsToMetaAction(Key key, RunMetaAction expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.MetaActionForKey(key));
        }

        [TestCase(Key.Space, true)]
        [TestCase(Key.Enter, true)]
        [TestCase(Key.NumpadEnter, true)]
        [TestCase(Key.W, false)]
        [TestCase(Key.R, false)]
        public void InputSystemKey_ConfirmKeys(Key key, bool expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.IsConfirmKey(key));
        }

        [Test]
        public void EveryPolledInputSystemKey_HasABinding_AndNeverBothCommandAndMeta()
        {
            foreach (Key key in KeyboardBindings.InputSystemKeys)
            {
                bool isCommand = KeyboardBindings.CommandForKey(key) != InputCommand.None;
                bool isMeta = KeyboardBindings.MetaActionForKey(key) != RunMetaAction.None;
                bool isConfirm = KeyboardBindings.IsConfirmKey(key);
                Assert.IsTrue(isCommand || isMeta || isConfirm, key + " is polled but bound to nothing.");
                Assert.IsFalse(isCommand && isMeta, key + " must not be both a command and a meta action.");
                Assert.IsFalse(isMeta && isConfirm, key + " must not be both a meta action and a confirm key.");
            }
        }

        [TestCase(Key.LeftArrow, InputCommand.MoveLeft)]
        [TestCase(Key.D, InputCommand.MoveRight)]
        [TestCase(Key.Space, InputCommand.Jump)]
        [TestCase(Key.S, InputCommand.Slide)]
        public void KeyboardDevicePress_ReachesTheSimulationAsCommand(Key key, InputCommand expected)
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("JungleBoozeTestKeyboard");
            try
            {
                keyboard.MakeCurrent();
                var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                InputSystem.Update();
                adapter.Poll(0.0);

                Assert.AreEqual(expected, adapter.ReadCommands(0));
                Assert.AreEqual(InputCommand.None, adapter.ReadCommands(1), "One key press is one command.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void KeyboardDevicePress_MetaKey_IsNotACommand()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("JungleBoozeTestKeyboard");
            try
            {
                keyboard.MakeCurrent();
                var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                InputSystem.Update();
                adapter.Poll(0.0);

                Assert.AreEqual(InputCommand.None, adapter.ReadCommands(0));
                Assert.AreEqual(RunMetaAction.RestartSameTrack, adapter.ConsumeMetaAction());
                Assert.IsFalse(adapter.ConsumeStartPress(), "Meta keys do not start the run.");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        [TestCase(KeyCode.LeftArrow, InputCommand.MoveLeft)]
        [TestCase(KeyCode.A, InputCommand.MoveLeft)]
        [TestCase(KeyCode.RightArrow, InputCommand.MoveRight)]
        [TestCase(KeyCode.D, InputCommand.MoveRight)]
        [TestCase(KeyCode.UpArrow, InputCommand.Jump)]
        [TestCase(KeyCode.W, InputCommand.Jump)]
        [TestCase(KeyCode.Space, InputCommand.Jump)]
        [TestCase(KeyCode.DownArrow, InputCommand.Slide)]
        [TestCase(KeyCode.S, InputCommand.Slide)]
        [TestCase(KeyCode.Return, InputCommand.None)]
        [TestCase(KeyCode.Escape, InputCommand.None)]
        public void LegacyKey_MapsToCommand(KeyCode key, InputCommand expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.CommandForKeyCode(key));
        }

        [TestCase(KeyCode.P, RunMetaAction.TogglePause)]
        [TestCase(KeyCode.Escape, RunMetaAction.TogglePause)]
        [TestCase(KeyCode.R, RunMetaAction.Restart)]
        [TestCase(KeyCode.T, RunMetaAction.RestartSameTrack)]
        [TestCase(KeyCode.K, RunMetaAction.DebugEndRun)]
        [TestCase(KeyCode.W, RunMetaAction.None)]
        public void LegacyKey_MapsToMetaAction(KeyCode key, RunMetaAction expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.MetaActionForKeyCode(key));
        }

        [TestCase(KeyCode.Space, true)]
        [TestCase(KeyCode.Return, true)]
        [TestCase(KeyCode.KeypadEnter, true)]
        [TestCase(KeyCode.W, false)]
        public void LegacyKey_ConfirmKeys(KeyCode key, bool expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.IsConfirmKeyCode(key));
        }

        [Test]
        public void EveryPolledLegacyKey_HasABinding_AndNeverBothCommandAndMeta()
        {
            foreach (KeyCode key in KeyboardBindings.LegacyKeyCodes)
            {
                bool isCommand = KeyboardBindings.CommandForKeyCode(key) != InputCommand.None;
                bool isMeta = KeyboardBindings.MetaActionForKeyCode(key) != RunMetaAction.None;
                bool isConfirm = KeyboardBindings.IsConfirmKeyCode(key);
                Assert.IsTrue(isCommand || isMeta || isConfirm, key + " is polled but bound to nothing.");
                Assert.IsFalse(isCommand && isMeta, key + " must not be both a command and a meta action.");
                Assert.IsFalse(isMeta && isConfirm, key + " must not be both a meta action and a confirm key.");
            }
        }
#endif

        [Test]
        public void InjectedKeyCommand_IsReadByTheSimulationTick()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);
            Assert.IsTrue(adapter.InjectCommand(InputCommand.Jump));
            Assert.AreEqual(InputCommand.Jump, adapter.ReadCommands(0));
            Assert.AreEqual(InputCommand.None, adapter.ReadCommands(1));
        }

        [Test]
        public void KeyWhileGameplayDisabled_IsNotQueued_ButCountsAsStartPress()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f) { GameplayEnabled = false };
            Assert.IsFalse(adapter.InjectCommand(InputCommand.MoveLeft));
            Assert.AreEqual(0, adapter.QueuedCount);
            Assert.IsTrue(adapter.ConsumeStartPress());
            Assert.IsFalse(adapter.ConsumeStartPress(), "Consumed once.");
        }

        [Test]
        public void ConfirmKey_IsReportedOnce_AndClearedByReset()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f) { GameplayEnabled = false };
            adapter.InjectConfirm();
            Assert.IsTrue(adapter.ConsumeConfirm());
            Assert.IsFalse(adapter.ConsumeConfirm());

            adapter.InjectConfirm();
            adapter.Reset();
            Assert.IsFalse(adapter.ConsumeConfirm());
            Assert.IsFalse(adapter.ConsumeStartPress());
        }
    }
}
