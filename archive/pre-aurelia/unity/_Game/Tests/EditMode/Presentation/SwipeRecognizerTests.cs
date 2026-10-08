using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Presentation
{
    /// <summary>Spec 001 section 12 and AC-50 to AC-52 (timestamped samples in points, y up).</summary>
    public sealed class SwipeRecognizerTests
    {
        private static SwipeRecognizer Create()
        {
            return new SwipeRecognizer(InputConfig.CreateDefault());
        }

        [Test]
        public void Ac50_27PtIn100Ms_IsNoSwipe()
        {
            SwipeRecognizer r = Create();
            r.Begin(100f, 100f, 0.0);
            Assert.AreEqual(InputCommand.None, r.Move(127f, 100f, 0.100));
        }

        [Test]
        public void Ac50_28PtAt240Ms_IsRecognizedOnThatSample()
        {
            SwipeRecognizer r = Create();
            r.Begin(100f, 100f, 0.0);
            Assert.AreEqual(InputCommand.None, r.Move(110f, 100f, 0.100));
            Assert.AreEqual(InputCommand.MoveRight, r.Move(128f, 100f, 0.240));
        }

        [Test]
        public void Ac50_28PtAt260Ms_IsNoSwipe()
        {
            SwipeRecognizer r = Create();
            r.Begin(100f, 100f, 0.0);
            Assert.AreEqual(InputCommand.None, r.Move(128f, 100f, 0.260));
        }

        [Test]
        public void Ac51_ExactDiagonal_GoesHorizontal()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            Assert.AreEqual(InputCommand.MoveLeft, r.Move(-30f, 30f, 0.1));
        }

        [Test]
        public void Ac51_20RightAnd30Up_IsJump()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            Assert.AreEqual(InputCommand.Jump, r.Move(20f, 30f, 0.1));
        }

        [Test]
        public void DownSwipe_IsSlide()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            Assert.AreEqual(InputCommand.Slide, r.Move(0f, -40f, 0.1));
        }

        [Test]
        public void Ac52_DragRightThenUp_GivesMoveRightThenJump()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            Assert.AreEqual(InputCommand.MoveRight, r.Move(30f, 0f, 0.05));
            Assert.AreEqual(InputCommand.None, r.Move(30f, 15f, 0.10));
            Assert.AreEqual(InputCommand.Jump, r.Move(30f, 30f, 0.15));
        }

        [Test]
        public void OneTouch_SameDirectionDrag_GivesOneSwipe()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            Assert.AreEqual(InputCommand.MoveRight, r.Move(30f, 0f, 0.05));
            Assert.AreEqual(InputCommand.None, r.Move(60f, 0f, 0.10));
            Assert.AreEqual(InputCommand.None, r.Move(90f, 0f, 0.15));
        }

        [Test]
        public void Cancel_StopsTheTouch()
        {
            SwipeRecognizer r = Create();
            r.Begin(0f, 0f, 0.0);
            r.Cancel();
            Assert.IsFalse(r.IsTracking);
            Assert.AreEqual(InputCommand.None, r.Move(60f, 0f, 0.05));
        }

        [Test]
        public void TieGoesVertical_WhenConfiguredSo()
        {
            Assert.AreEqual(InputCommand.Jump, SwipeRecognizer.DirectionOf(30f, 30f, false));
            Assert.AreEqual(InputCommand.MoveRight, SwipeRecognizer.DirectionOf(30f, 30f, true));
        }
    }
}
