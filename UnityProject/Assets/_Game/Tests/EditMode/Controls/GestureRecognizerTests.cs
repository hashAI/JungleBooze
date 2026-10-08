using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Controls;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Controls
{
    /// <summary>Spec 101 §3.3: AC-101-32 … AC-101-39 with timestamped samples (points, y up).</summary>
    public sealed class GestureRecognizerTests
    {
        private const double Hz = 1.0 / 60.0;

        private static GestureRecognizer Create()
        {
            return new GestureRecognizer(new GestureConfig());
        }

        private static void Begin(GestureRecognizer g, int id, float x, float y, double t)
        {
            g.Process(new TouchSample(id, TouchPhaseKind.Began, x, y, t));
        }

        private static void Move(GestureRecognizer g, int id, float x, float y, double t)
        {
            g.Process(new TouchSample(id, TouchPhaseKind.Moved, x, y, t));
        }

        private static void End(GestureRecognizer g, int id, float x, float y, double t)
        {
            g.Process(new TouchSample(id, TouchPhaseKind.Ended, x, y, t));
        }

        /// <summary>Straight motion from (x0,y0) by (dx,dy) over duration, sampled at 60 Hz. Returns the end time.</summary>
        private static double Stroke(GestureRecognizer g, int id, ref float x, ref float y, float dx, float dy, double t0, double duration, Action<int> afterSample = null)
        {
            int samples = Math.Max(1, (int)Math.Round(duration / Hz));
            float sx = x;
            float sy = y;
            for (int i = 1; i <= samples; i++)
            {
                float k = (float)i / samples;
                x = sx + (dx * k);
                y = sy + (dy * k);
                Move(g, id, x, y, t0 + (i * duration / samples));
                afterSample?.Invoke(i);
            }

            return t0 + duration;
        }

        private static int Count(GestureRecognizer g, InputCommand command)
        {
            int n = 0;
            for (int i = 0; i < g.Commands.Count; i++)
            {
                if (g.Commands.Peek(i) == command)
                {
                    n++;
                }
            }

            return n;
        }

        [Test]
        public void AC32_SwipeUpFiresOnTheCrossingSample()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            int firedAt = -1;
            float x = 100f;
            float y = 100f;
            Stroke(g, 1, ref x, ref y, 0f, 24f, 0.0, 6 * Hz, i =>
            {
                if (firedAt < 0 && g.Commands.Count > 0)
                {
                    firedAt = i;
                }
            });
            Assert.AreEqual(6, firedAt, "fires on the sample that crosses 24 pt (not on release)");
            Assert.AreEqual(InputCommand.Jump, g.Commands.Peek(0));
            Assert.AreEqual(1, g.Commands.Count);
        }

        [Test]
        public void AC32_23ptIsNothing()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 0f, 23f, 0.0, 0.1);
            End(g, 1, x, y, t + Hz);
            Assert.AreEqual(0, g.Commands.Count);
        }

        [Test]
        public void AC32_40DegreesIsNoSwipe()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            float dy = 30f;
            float dx = dy * (float)Math.Tan(40.0 * Math.PI / 180.0);
            Stroke(g, 1, ref x, ref y, dx, dy, 0.0, 0.1);
            Assert.AreEqual(0, Count(g, InputCommand.Jump));
        }

        [Test]
        public void AC32_TooSlowIsNoSwipe()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            Stroke(g, 1, ref x, ref y, 0f, 30f, 0.0, 0.3);
            Assert.AreEqual(0, g.Commands.Count, "30 pt in 0.3 s is under 200 pt/s");
        }

        [Test]
        public void SwipeDownIsSlide()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            Stroke(g, 1, ref x, ref y, 0f, -30f, 0.0, 0.08);
            Assert.AreEqual(InputCommand.Slide, g.Commands.Peek(0));
        }

        [Test]
        public void AC33_DiagonalSwipeJumpsWithoutSteering()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            float dy = 40f;
            float dx = dy * (float)Math.Tan(25.0 * Math.PI / 180.0);
            double t = Stroke(g, 1, ref x, ref y, dx, dy, 0.0, 0.1);
            End(g, 1, x, y, t + Hz);
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.Less(Math.Abs(g.ConsumeLateralM()), 0.05);
            Assert.AreEqual(0, Count(g, InputCommand.DodgeRight));
        }

        [Test]
        public void AC34_QuickFlickDodgesOnRelease()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 40f, 0f, 0.0, 0.15);
            Assert.AreEqual(0, g.Commands.Count, "nothing before release");
            End(g, 1, x, y, t);
            Assert.AreEqual(1, g.Commands.Count);
            Assert.AreEqual(InputCommand.DodgeRight, g.Commands.Peek(0));
            Assert.Greater(g.ConsumeLateralM(), 1.0, "the flick also steered");
        }

        [Test]
        public void AC34_SlowDragIsSteeringOnly()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, -40f, 0f, 0.0, 0.3);
            End(g, 1, x, y, t);
            Assert.AreEqual(0, g.Commands.Count);
            Assert.AreEqual(-40.0 * 0.04, g.ConsumeLateralM(), 1e-4);
        }

        [Test]
        public void ReleaseFlickAfterALongDragDodges()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 30f, 0f, 0.0, 0.5);
            t = Stroke(g, 1, ref x, ref y, 120f, 0f, t, 0.06); // 2,000 pt/s at release
            End(g, 1, x, y, t);
            Assert.AreEqual(InputCommand.DodgeRight, g.Commands.Peek(0));

            GestureRecognizer off = new GestureRecognizer(new GestureConfig { ReleaseFlickEnabled = false });
            Begin(off, 1, 100f, 100f, 0.0);
            x = 100f;
            y = 100f;
            t = Stroke(off, 1, ref x, ref y, 30f, 0f, 0.0, 0.5);
            t = Stroke(off, 1, ref x, ref y, 120f, 0f, t, 0.06);
            End(off, 1, x, y, t);
            Assert.AreEqual(0, off.Commands.Count, "setting off");
        }

        [Test]
        public void AC35_DragSwipeUpMidDragKeepDragging()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 60f, 0f, 0.0, 0.5);
            t = Stroke(g, 1, ref x, ref y, 0f, 30f, t, 0.08);
            t = Stroke(g, 1, ref x, ref y, 60f, 0f, t, 0.5);
            Move(g, 1, x, y, t + 0.1);
            End(g, 1, x, y, t + 0.12);
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            Assert.AreEqual(0, Count(g, InputCommand.DodgeLeft) + Count(g, InputCommand.DodgeRight));
            Assert.AreEqual(120.0 * 0.04, g.ConsumeLateralM(), 1e-3, "steering continued around the swipe");
        }

        [Test]
        public void AC36_TwoSwipesInOneFrameLandOnConsecutiveTicks()
        {
            GestureRecognizer g = Create();
            var dispatcher = new FrameInputDispatcher(g, 8);
            float x = 100f;
            float y = 100f;
            Begin(g, 1, x, y, 0.0);
            double t = Stroke(g, 1, ref x, ref y, 0f, 30f, 0.0, 0.05);
            End(g, 1, x, y, t);
            x = 300f;
            y = 300f;
            Begin(g, 2, x, y, t + 0.001);
            t = Stroke(g, 2, ref x, ref y, 0f, -30f, t + 0.001, 0.05);
            End(g, 2, x, y, t);

            dispatcher.BeginFrame(2);
            InputFrame first = dispatcher.ReadInput(0);
            InputFrame second = dispatcher.ReadInput(1);
            Assert.AreEqual(InputCommand.Jump, InputCommands.Discrete(first.Commands));
            Assert.AreEqual(InputCommand.Slide, InputCommands.Discrete(second.Commands));
            Assert.IsTrue(InputCommands.Has(first.Commands, InputCommand.TouchBegan));
        }

        [Test]
        public void AC37_SecondTouchNeverSteersButTakesOverWithoutAJump()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            Begin(g, 2, 600f, 100f, 0.01);
            float bx = 600f;
            float by = 100f;
            double t = Stroke(g, 2, ref bx, ref by, 50f, 0f, 0.02, 0.4);
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-9, "touch B never steers");
            End(g, 1, 100f, 100f, t);
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-9, "no target jump on hand-over");
            t = Stroke(g, 2, ref bx, ref by, 50f, 0f, t, 0.4);
            Assert.AreEqual(50.0 * 0.04, g.ConsumeLateralM(), 1e-4, "B steers from its current position");
        }

        [Test]
        public void AC37_SecondTouchCanSwipe()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            Begin(g, 2, 600f, 100f, 0.01);
            float bx = 600f;
            float by = 100f;
            Stroke(g, 2, ref bx, ref by, 0f, 30f, 0.02, 0.08);
            Assert.AreEqual(InputCommand.Jump, g.Commands.Peek(0));
        }

        [Test]
        public void AC38_LateralSplitsExactlyAcrossTicksAndCarriesSubMillimetres()
        {
            GestureRecognizer g = Create();
            var dispatcher = new FrameInputDispatcher(g, 8);
            Begin(g, 1, 0f, 0f, 0.0);
            Move(g, 1, 10f, 0f, 0.01); // past the dead zone: 0.4 m
            g.ConsumeLateralM();

            float x = 10f;
            double t = 0.01;
            long totalMm = 0;
            var pacing = new Pcg32Random(5UL);
            for (int frame = 0; frame < 200; frame++)
            {
                float dx = pacing.NextFloat(-3f, 3f);
                x += dx;
                t += Hz;
                Move(g, 1, x, 0f, t);
                int steps = pacing.NextInt(0, 6);
                dispatcher.BeginFrame(steps);
                long frameMm = 0;
                long first = long.MinValue;
                for (int i = 0; i < steps; i++)
                {
                    InputFrame f = dispatcher.ReadInput(i);
                    frameMm += f.LateralDeltaMm;
                    if (i == 0)
                    {
                        first = f.LateralDeltaMm;
                    }
                    else if (i < steps - 1)
                    {
                        Assert.AreEqual(first, f.LateralDeltaMm, "even split");
                    }
                }

                totalMm += frameMm;
                Assert.Less(Math.Abs(dispatcher.CarryMm), 1.0);
            }

            dispatcher.BeginFrame(1);
            totalMm += dispatcher.ReadInput(0).LateralDeltaMm;
            double expectedMm = (x - 10.0) * 0.04 * 1000.0;
            Assert.AreEqual(expectedMm, totalMm + dispatcher.CarryMm, 0.01, "no drift: delivered mm + carry = drag");
        }

        [Test]
        public void AC38_RemainderGoesOnTheLastTick()
        {
            GestureRecognizer g = Create();
            var dispatcher = new FrameInputDispatcher(g, 8);
            dispatcher.AddLateral(0.047);
            dispatcher.BeginFrame(5);
            short[] mm = new short[5];
            for (int i = 0; i < 5; i++)
            {
                mm[i] = dispatcher.ReadInput(i).LateralDeltaMm;
            }

            CollectionAssert.AreEqual(new short[] { 9, 9, 9, 9, 11 }, mm);
        }

        [Test]
        public void AC39_CancelledTouchEmitsNoFlickAndNoFurtherDelta()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 40f, 0f, 0.0, 0.1);
            g.ConsumeLateralM();
            g.Process(new TouchSample(1, TouchPhaseKind.Canceled, x, y, t));
            Move(g, 1, x + 50f, y, t + Hz);
            End(g, 1, x + 50f, y, t + (2 * Hz));
            Assert.AreEqual(0, g.Commands.Count);
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-9);
        }

        [Test]
        public void DeadZoneHoldsThenReleasesAllTravel()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            Move(g, 1, 103f, 100f, Hz);
            Assert.AreEqual(0.0, g.ConsumeLateralM(), 1e-9, "inside the 4 pt dead zone");
            Move(g, 1, 105f, 100f, 2 * Hz);
            Assert.AreEqual(5.0 * 0.04, g.ConsumeLateralM(), 1e-5, "full 5 pt applied once past it");
        }

        [Test]
        public void SameDirectionRefireNeedsRearmOppositeIsImmediate()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 0f, 30f, 0.0, 0.06);
            Assert.AreEqual(1, Count(g, InputCommand.Jump));
            t = Stroke(g, 1, ref x, ref y, 0f, 30f, t, 0.06);
            Assert.AreEqual(1, Count(g, InputCommand.Jump), "same direction within 0.18 s: no re-fire");
            t = Stroke(g, 1, ref x, ref y, 0f, -30f, t, 0.06);
            Assert.AreEqual(1, Count(g, InputCommand.Slide), "opposite direction fires immediately");
            t = Stroke(g, 1, ref x, ref y, 0f, 0f, t, 0.2);
            Stroke(g, 1, ref x, ref y, 0f, 30f, t, 0.06);
            Assert.AreEqual(2, Count(g, InputCommand.Jump), "after the re-arm time a fresh 24 pt fires again");
        }

        [Test]
        public void IgnoredTouchesAfterResumeDoNothingUntilLifted()
        {
            GestureRecognizer g = Create();
            Begin(g, 1, 100f, 100f, 0.0);
            g.IgnoreActiveTouches();
            float x = 100f;
            float y = 100f;
            double t = Stroke(g, 1, ref x, ref y, 0f, 40f, 0.0, 0.08);
            End(g, 1, x, y, t);
            Assert.AreEqual(0, g.Commands.Count);
            Begin(g, 2, 100f, 100f, t + 0.1);
            Move(g, 2, 120f, 100f, t + 0.2);
            Assert.Greater(g.ConsumeLateralM(), 0.0, "a fresh touch steers again");
        }
    }
}
