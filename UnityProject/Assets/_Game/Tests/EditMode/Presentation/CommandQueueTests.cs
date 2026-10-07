using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Presentation
{
    /// <summary>Spec 001 12.5 / AC-54: one gesture per tick, in order; plus the adapter's pause rules (8.2, AC-49).</summary>
    public sealed class CommandQueueTests
    {
        [Test]
        public void Queue_ReturnsOnePerRead_InOrder_AndDropsWhenFull()
        {
            var q = new CommandQueue(2);
            Assert.IsTrue(q.TryEnqueue(InputCommand.MoveLeft));
            Assert.IsTrue(q.TryEnqueue(InputCommand.Jump));
            Assert.IsFalse(q.TryEnqueue(InputCommand.Slide));
            Assert.AreEqual(1, q.DroppedCount);
            Assert.AreEqual(InputCommand.MoveLeft, q.Dequeue());
            Assert.AreEqual(InputCommand.Jump, q.Dequeue());
            Assert.AreEqual(InputCommand.None, q.Dequeue());
        }

        [Test]
        public void Adapter_TwoFastSwipes_AreDeliveredOnTwoTicks()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);
            adapter.FeedPointer(true, true, new Vector2(0f, 0f), 0.0);
            adapter.FeedPointer(false, true, new Vector2(30f, 0f), 0.02);
            adapter.FeedPointer(false, false, new Vector2(30f, 30f), 0.04);

            Assert.AreEqual(InputCommand.MoveRight, adapter.ReadCommands(0));
            Assert.AreEqual(InputCommand.Jump, adapter.ReadCommands(1));
            Assert.AreEqual(InputCommand.None, adapter.ReadCommands(2));
        }

        [Test]
        public void Adapter_ConvertsPixelsToPoints()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 3f);
            adapter.FeedPointer(true, true, new Vector2(0f, 0f), 0.0);
            adapter.FeedPointer(false, true, new Vector2(81f, 0f), 0.05); // 27 pt
            Assert.AreEqual(InputCommand.None, adapter.ReadCommands(0));
            adapter.FeedPointer(false, true, new Vector2(84f, 0f), 0.06); // 28 pt
            Assert.AreEqual(InputCommand.MoveRight, adapter.ReadCommands(1));
        }

        [Test]
        public void Adapter_TouchStartedBeforePause_NeverSwipesAfterResume()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);
            adapter.FeedPointer(true, true, new Vector2(0f, 0f), 0.0);
            adapter.InjectCommand(InputCommand.Jump);

            adapter.GameplayEnabled = false; // pause
            Assert.AreEqual(0, adapter.QueuedCount, "Pause clears queued commands.");
            adapter.FeedPointer(false, true, new Vector2(10f, 0f), 0.5); // touch during the countdown
            adapter.GameplayEnabled = true; // resumed

            adapter.FeedPointer(false, true, new Vector2(60f, 0f), 2.0);
            adapter.FeedPointer(false, false, new Vector2(90f, 0f), 2.01);
            Assert.AreEqual(InputCommand.None, adapter.ReadCommands(0));
        }

        [Test]
        public void Adapter_IgnoresCommandsWhileDisabled_ButKeepsMetaActions()
        {
            var adapter = new PlayerInputAdapter(InputConfig.CreateDefault(), 1f);
            adapter.GameplayEnabled = false;
            Assert.IsFalse(adapter.InjectCommand(InputCommand.Jump));
            adapter.InjectMetaAction(RunMetaAction.TogglePause);
            Assert.AreEqual(RunMetaAction.TogglePause, adapter.ConsumeMetaAction());
            Assert.AreEqual(RunMetaAction.None, adapter.ConsumeMetaAction());
        }

        [Test]
        public void PixelsPerPoint_FromDpi()
        {
            Assert.AreEqual(2f, PlayerInputAdapter.PixelsPerPointForDpi(326f), 1e-4f);
            Assert.AreEqual(1f, PlayerInputAdapter.PixelsPerPointForDpi(0f));
            Assert.AreEqual(1f, PlayerInputAdapter.PixelsPerPointForDpi(96f));
        }
    }
}
