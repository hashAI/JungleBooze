using JungleBooze.Core;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §2.3: AC-101-04 … AC-101-09.</summary>
    public sealed class LateralTests
    {
        [Test]
        public void AC04_TargetStepResponse()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(new InputFrame(InputCommand.None, 2000));
            float max = rig.State.X;
            int settledTick = -1;
            for (int tick = 2; tick <= 120; tick++)
            {
                rig.Steps(1);
                if (tick == 2)
                {
                    Assert.GreaterOrEqual(rig.State.X, 0.05f, "≥ 5 cm within 2 ticks");
                }

                if (tick == 10)
                {
                    Assert.GreaterOrEqual(rig.State.X, 1.26f, "63% by 0.17 s");
                }

                if (settledTick < 0 && rig.State.X >= 1.90f)
                {
                    settledTick = tick;
                }

                max = System.Math.Max(max, rig.State.X);
            }

            Assert.LessOrEqual(settledTick * SimRig.Dt, 0.33f, "within 0.10 m by 0.33 s (tick " + settledTick + ")");
            Assert.LessOrEqual(max, 2.02f, "overshoot ≤ 0.02 m");
            Assert.AreEqual(2f, rig.State.X, 1e-3f);
        }

        [Test]
        public void AC04_DodgeFromRestSettlesBy032s()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(InputCommand.TouchBegan | InputCommand.DodgeRight);
            Assert.AreEqual(2.2f, rig.State.XTarget, 1e-5f);
            int settled = -1;
            float max = 0f;
            for (int tick = 2; tick <= 120; tick++)
            {
                rig.Steps(1);
                if (settled < 0 && rig.State.X >= 2.1f)
                {
                    settled = tick;
                }

                max = System.Math.Max(max, rig.State.X);
            }

            Assert.LessOrEqual(settled * SimRig.Dt, 0.32f, "dodge within 0.10 m by 0.32 s (tick " + settled + ")");
            Assert.LessOrEqual(max, 2.22f);
        }

        [Test]
        public void M1_LateralCommandMovesOnTheSameTick()
        {
            SimRig rig = SimRig.Flat();
            rig.Step(new InputFrame(InputCommand.None, 500));
            Assert.Greater(rig.State.X, 0f);
        }

        [Test]
        public void AC05_NoDebtPastTheEdge()
        {
            SimRig rig = SimRig.Flat(half: 3.5f);
            rig.Step(new InputFrame(InputCommand.None, 6200));
            Assert.AreEqual(3.2f, rig.State.XTarget, 1e-5f, "target clamped at xLim");
            rig.Steps(60);
            rig.Step(new InputFrame(InputCommand.None, -10));
            Assert.AreEqual(3.19f, rig.State.XTarget, 1e-4f, "1 cm reverse moves the target inward on the next tick");
            rig.Steps(30);
            Assert.Less(rig.State.X, 3.2f);
        }

        [Test]
        public void AC06_EdgeIsSoftAndHarmless()
        {
            SimRig rig = SimRig.Flat(half: 3.5f);
            for (int i = 0; i < 240; i++)
            {
                rig.Step(new InputFrame(InputCommand.None, 200));
                Assert.LessOrEqual(rig.State.X, 3.2f + 1e-5f);
                Assert.GreaterOrEqual(rig.State.X, -3.2f - 1e-5f);
            }

            Assert.Greater(rig.CountEvents(RunEventType.EdgeBrush), 0);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.Hit));
            Assert.AreEqual(3, rig.State.Health);
        }

        [Test]
        public void AC06_SoftZoneSlowsOutwardMotion()
        {
            SimRig free = SimRig.Flat(half: 20f);
            SimRig edge = SimRig.Flat(half: 3.5f);
            free.Step(new InputFrame(InputCommand.None, 5000));
            edge.Step(new InputFrame(InputCommand.None, 5000));
            for (int i = 0; i < 30; i++)
            {
                free.Steps(1);
                edge.Steps(1);
                if (edge.State.X > 3.2f - 0.35f + 0.05f && edge.State.X < 3.15f)
                {
                    Assert.Less(edge.State.VLat, free.State.VLat + 1e-4f);
                }
            }
        }

        [Test]
        public void AC07_NarrowingPushesInwardAtMost6mps()
        {
            var data = new CourseData { FinishS = 1000f };
            data.Widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            data.Widths.Add(new CourseWidthKey(10f, -3.5f, 3.5f));
            data.Widths.Add(new CourseWidthKey(12f, -1.0f, 1.0f));
            var rig = new SimRig(data, new RunOptions { ForcedSpeed = 16f, SkipStartRamp = true, StartX = 3.0f });
            bool wasOutside = false;
            for (int i = 0; i < 120; i++)
            {
                float before = rig.State.X;
                rig.Steps(1);
                Assert.GreaterOrEqual(rig.State.X - before, -6f * SimRig.Dt - 1e-4f, "inward ≤ 6 m/s");
                rig.Course.GetLateralBounds(rig.State.S, rig.State.X, out _, out float xMax);
                wasOutside |= rig.State.X > xMax - 0.3f + 1e-3f;
            }

            Assert.IsTrue(wasOutside, "the narrowing outran the runner (push exercised)");
            Assert.AreEqual(0.7f, rig.State.X, 1e-3f);
            Assert.AreEqual(0, rig.CountEvents(RunEventType.Hit));
        }

        [TestCase(1.0f, 2.2f)]
        [TestCase(2.8f, 2.8f)]
        public void AC08_DodgeNeverPullsBackDragMotion(float drag, float expected)
        {
            SimRig rig = SimRig.Flat(half: 4.0f);
            rig.Step(new InputFrame(InputCommand.TouchBegan, 0));
            int mm = (int)System.Math.Round(drag * 1000f);
            for (int i = 0; i < 10; i++)
            {
                rig.Step(new InputFrame(InputCommand.None, (short)(mm / 10)));
            }

            rig.Step(InputCommand.DodgeRight);
            Assert.AreEqual(expected, rig.State.XTarget, 1e-4f);

            // Mirror.
            SimRig left = SimRig.Flat(half: 4.0f);
            left.Step(new InputFrame(InputCommand.TouchBegan, 0));
            for (int i = 0; i < 10; i++)
            {
                left.Step(new InputFrame(InputCommand.None, (short)(-mm / 10)));
            }

            left.Step(InputCommand.DodgeLeft);
            Assert.AreEqual(-expected, left.State.XTarget, 1e-4f);
        }

        [Test]
        public void AC08_KeyboardDodgeStartsFromCurrentTarget()
        {
            SimRig rig = SimRig.Flat(half: 4.0f);
            rig.Step(new InputFrame(InputCommand.None, 1000));
            rig.Steps(30);
            rig.Step(InputCommand.TouchBegan | InputCommand.DodgeLeft);
            Assert.AreEqual(1f - 2.2f, rig.State.XTarget, 1e-4f);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AC09_DodgeNeverChangesVerticalOrSlideState(int posture)
        {
            SimRig with = SimRig.Flat();
            SimRig without = SimRig.Flat();
            InputCommand setup = posture == 1 ? InputCommand.Jump : posture == 2 ? InputCommand.Slide : InputCommand.None;
            with.Step(setup);
            without.Step(setup);
            with.Steps(5);
            without.Steps(5);
            with.Step(InputCommand.TouchBegan | InputCommand.DodgeRight);
            without.Steps(1);
            Assert.AreNotEqual(without.State.XTarget, with.State.XTarget);
            Assert.IsTrue(with.CountEvents(RunEventType.Dodge) == 1);
            for (int i = 0; i < 60; i++)
            {
                Assert.AreEqual(without.State.Y, with.State.Y);
                Assert.AreEqual(without.State.Vy, with.State.Vy);
                Assert.AreEqual(without.State.Sliding, with.State.Sliding);
                Assert.AreEqual(without.State.Grounded, with.State.Grounded);
                with.Steps(1);
                without.Steps(1);
            }
        }

        [Test]
        public void ForkNudge_RunnerAimedAtDividerIsNudgedNeverCrashed()
        {
            var data = SimRig.Path(3.5f);
            data.Forks.Add(new CourseFork { SFront = 30f, SMerge = 60f, DividerCenterX = 0f, DividerHalfWidth = 0.6f, LeftXMin = -3.5f, LeftXMax = -0.6f, RightXMin = 0.6f, RightXMax = 3.5f, SafeSide = -1 });
            var rig = new SimRig(data, new RunOptions { ForcedSpeed = 16f, SkipStartRamp = true, StartX = 0.05f });
            rig.RunTo(50f);
            Assert.AreEqual(1, rig.CountEvents(RunEventType.ForkNudge));
            Assert.GreaterOrEqual(rig.State.X, 0.9f - 1e-4f, "right side: x was right of centre");
            Assert.AreEqual(0, rig.State.Hits);
            Assert.IsFalse(rig.State.Dead);
        }

        [Test]
        public void ForkNudge_TieGoesToSafeSide()
        {
            var data = SimRig.Path(3.5f);
            data.Forks.Add(new CourseFork { SFront = 30f, SMerge = 60f, DividerCenterX = 0f, DividerHalfWidth = 0.6f, LeftXMin = -3.5f, LeftXMax = -0.6f, RightXMin = 0.6f, RightXMax = 3.5f, SafeSide = -1 });
            var rig = new SimRig(data, new RunOptions { ForcedSpeed = 16f, SkipStartRamp = true, StartX = 0f });
            rig.RunTo(50f);
            Assert.LessOrEqual(rig.State.X, -0.9f + 1e-4f);
        }
    }
}
