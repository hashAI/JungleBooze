using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;
#endif

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>Keyboard controls from docs/PLAY_FIRST_BUILD.md Step 5 map to the right commands and actions.</summary>
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
        [TestCase(Key.P, InputCommand.None)]
        [TestCase(Key.Q, InputCommand.None)]
        public void InputSystemKey_MapsToCommand(Key key, InputCommand expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.CommandForKey(key));
        }

        [TestCase(Key.P, RunMetaAction.TogglePause)]
        [TestCase(Key.Escape, RunMetaAction.TogglePause)]
        [TestCase(Key.R, RunMetaAction.Restart)]
        [TestCase(Key.K, RunMetaAction.DebugEndRun)]
        [TestCase(Key.Space, RunMetaAction.None)]
        public void InputSystemKey_MapsToMetaAction(Key key, RunMetaAction expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.MetaActionForKey(key));
        }

        [Test]
        public void EveryPolledInputSystemKey_HasExactlyOneBinding()
        {
            foreach (Key key in KeyboardBindings.InputSystemKeys)
            {
                bool isCommand = KeyboardBindings.CommandForKey(key) != InputCommand.None;
                bool isMeta = KeyboardBindings.MetaActionForKey(key) != RunMetaAction.None;
                Assert.IsTrue(isCommand ^ isMeta, key + " must be bound to exactly one thing.");
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
        [TestCase(KeyCode.Escape, InputCommand.None)]
        public void LegacyKey_MapsToCommand(KeyCode key, InputCommand expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.CommandForKeyCode(key));
        }

        [TestCase(KeyCode.P, RunMetaAction.TogglePause)]
        [TestCase(KeyCode.Escape, RunMetaAction.TogglePause)]
        [TestCase(KeyCode.R, RunMetaAction.Restart)]
        [TestCase(KeyCode.K, RunMetaAction.DebugEndRun)]
        [TestCase(KeyCode.W, RunMetaAction.None)]
        public void LegacyKey_MapsToMetaAction(KeyCode key, RunMetaAction expected)
        {
            Assert.AreEqual(expected, KeyboardBindings.MetaActionForKeyCode(key));
        }

        [Test]
        public void EveryPolledLegacyKey_HasExactlyOneBinding()
        {
            foreach (KeyCode key in KeyboardBindings.LegacyKeyCodes)
            {
                bool isCommand = KeyboardBindings.CommandForKeyCode(key) != InputCommand.None;
                bool isMeta = KeyboardBindings.MetaActionForKeyCode(key) != RunMetaAction.None;
                Assert.IsTrue(isCommand ^ isMeta, key + " must be bound to exactly one thing.");
            }
        }
#endif

        [Test]
        public void InjectedKeyCommand_IsReadByTheSimulationTick()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);
            adapter.InjectCommand(InputCommand.Jump);
            Assert.AreEqual(InputCommand.Jump, adapter.ReadCommands(0));
            Assert.AreEqual(InputCommand.None, adapter.ReadCommands(1));
        }
    }
}
