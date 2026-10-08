using System.Collections;
using JungleBooze.App.FeelTest;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>AC-101-40 (no allocation per tick) and AC-101-46 (debug overlay smoke test) in the player loop.</summary>
    public sealed class FeelTestPlayModeTests
    {
        private static CoursePath SmallCourse()
        {
            var c = new CourseData { FinishS = 100000f };
            c.Widths.Add(new CourseWidthKey(0f, -3.5f, 3.5f));
            c.Widths.Add(new CourseWidthKey(400f, -3.5f, 3.5f));
            c.Widths.Add(new CourseWidthKey(420f, -1.2f, 1.2f));
            c.Widths.Add(new CourseWidthKey(440f, -3.5f, 3.5f));
            c.Forks.Add(new CourseFork { SFront = 600f, SMerge = 650f, DividerHalfWidth = 0.6f, LeftXMin = -3.5f, LeftXMax = -0.6f, RightXMin = 0.6f, RightXMax = 3.5f, SafeSide = -1 });
            c.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = 300f, SMax = 303f, XMin = -10f, XMax = 10f });
            for (int i = 0; i < 40; i++)
            {
                float s = 30f + (i * 25f);
                c.Obstacles.Add(new CourseObstacle { Class = (ObstacleClass)(i % 4), SMin = s, SMax = s + 0.6f, XMin = i % 4 == 2 ? 1f : -4f, XMax = i % 4 == 2 ? 2.2f : 4f, YMin = i % 4 == 1 ? 1f : 0f, YMax = i % 4 == 1 ? 2.4f : 0.6f });
                c.Coins.Add(new CourseCoin(s - 5f, 0f, 0.9f));
            }

            return new CoursePath(c);
        }

        private static MovementConfig Config()
        {
            return new MovementConfig(new RunSpeedConfig(), new LateralMovementConfig(), new JumpSlideConfig(), new HitboxConfig(), new HealthConfig(), new RunFlowConfig());
        }

        [Test]
        public void AC40_SimulationStepAndInputDoNotAllocate()
        {
            CoursePath course = SmallCourse();
            var sim = new RunnerSimulation(Config(), course, 1f / 60f, new RunEventBuffer(256));
            sim.Reset(new RunOptions { FirstRun = true });
            var gestures = new GestureRecognizer(new GestureConfig());
            var dispatcher = new FrameInputDispatcher(gestures, 16);
            var bot = new PerfectBot(sim, false);
            double time = 0.0;
            int touch = 0;

            void Frame(int i)
            {
                // A touch every 30 frames: down, drag, swipe up, release.
                int phase = i % 30;
                float x = 100f + (phase * 3f);
                float y = phase > 20 ? 100f + ((phase - 20) * 9f) : 100f;
                if (phase == 0)
                {
                    touch++;
                    gestures.Process(new TouchSample(touch, TouchPhaseKind.Began, x, y, time));
                }
                else if (phase == 29)
                {
                    gestures.Process(new TouchSample(touch, TouchPhaseKind.Ended, x, y, time));
                }
                else
                {
                    gestures.Process(new TouchSample(touch, TouchPhaseKind.Moved, x, y, time));
                }

                time += 1.0 / 60.0;
                dispatcher.BeginFrame(1 + (i % 2));
                for (int k = 0; k < 1 + (i % 2); k++)
                {
                    InputFrame player = dispatcher.ReadInput(sim.State.Tick);
                    InputFrame frame = (i / 300) % 2 == 0 ? bot.ReadInput(sim.State.Tick) : player;
                    sim.Step(frame);
                }

                sim.Events.Clear();
            }

            for (int i = 0; i < 600; i++)
            {
                Frame(i);
            }

            int start = 600;
            Assert.That(() =>
            {
                for (int i = start; i < start + 1200; i++)
                {
                    Frame(i);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.Greater(sim.State.S, 100f);
        }

        [UnityTest]
        public IEnumerator AC46_FeelTestSceneRunsAndDebugOverlayShowsState()
        {
            SceneManager.LoadScene("FeelTest");
            yield return null;
            yield return null;
            FeelTestRoot root = Object.FindFirstObjectByType<FeelTestRoot>();
            Assert.IsNotNull(root, "FeelTest scene with FeelTestRoot");
            Assert.IsNotNull(root.Session);

            root.SetDebugVisible(true);
            root.SetBotDriving(true);
            root.StepTicks(600);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsTrue(root.DebugView.Visible, "hitboxes shown");
            Assert.IsTrue(root.Hud.DebugVisible);
            string text = root.Hud.DebugText;
            StringAssert.Contains("xT", text);
            StringAssert.Contains("buffered", text);
            StringAssert.Contains("dropped (last 5)", text);
            Assert.Greater(root.Simulation.State.S, 40f, "the run advanced");
            Assert.AreEqual(0, root.Simulation.State.Hits, "bot drove cleanly");

            root.SetDebugVisible(false);
            Assert.IsFalse(root.DebugView.Visible);
        }
    }
}
