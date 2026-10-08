using System.Collections.Generic;
using System.Globalization;
using JungleBooze.Core;
using JungleBooze.Gameplay.Bots;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;
using JungleBooze.Gameplay.Run;
using NUnit.Framework;
using UnityEngine;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Spec 101 §6–7: the feel course asset, shipped tuning and AC-101-45 (Perfect bot).</summary>
    public sealed class FeelCourseTests
    {
        [Test]
        public void ShippedConfig_IsValidAndMatchesSpec101()
        {
            MovementConfig shipped = ShippedAssets.Config();
            CollectionAssert.IsEmpty(shipped.Validate());
            MovementConfig spec = SpecConfig.Create();

            // If you tuned a value deliberately, update spec 101 (game-designer) and this guard together.
            Assert.AreEqual(JsonUtility.ToJson(spec.Speed), JsonUtility.ToJson(shipped.Speed));
            Assert.AreEqual(JsonUtility.ToJson(spec.Lateral), JsonUtility.ToJson(shipped.Lateral));
            Assert.AreEqual(JsonUtility.ToJson(spec.JumpSlide), JsonUtility.ToJson(shipped.JumpSlide));
            Assert.AreEqual(JsonUtility.ToJson(spec.Hitbox), JsonUtility.ToJson(shipped.Hitbox));
            Assert.AreEqual(JsonUtility.ToJson(spec.Health), JsonUtility.ToJson(shipped.Health));
        }

        [Test]
        public void JumpArcValidation_FlagsOutOfRangeTuning()
        {
            MovementConfig config = SpecConfig.Create();
            config.JumpSlide.JumpVelocity = 12f;
            Assert.IsNotEmpty(config.Validate());
        }

        [Test]
        public void FeelCourse_FollowsSpecLayout()
        {
            var course = new CoursePath(ShippedAssets.Course());
            Assert.AreEqual(640f, course.FinishS);
            Assert.AreEqual(13, course.SectionCount);
            Assert.AreEqual(1, course.ForkCount);
            Assert.Greater(course.CoinCount, 80);

            course.GetLateralBounds(20f, 0f, out float a, out float b);
            Assert.AreEqual(8f, b - a, 1e-4f, "C1 is 8 m wide");
            course.GetLateralBounds(200f, 0f, out a, out b);
            Assert.AreEqual(7f, b - a, 1e-4f);
            course.GetLateralBounds(420f, 0f, out a, out b);
            Assert.AreEqual(2.4f, b - a, 1e-4f, "C9 ledge");
            course.GetLateralBounds(560f, -2f, out a, out b);
            Assert.AreEqual(3.5f, b - a, 1e-4f, "safe branch");
            course.GetLateralBounds(560f, 2f, out a, out b);
            Assert.AreEqual(2.4f, b - a, 1e-4f, "risky branch");

            Assert.IsFalse(course.TryGetFloor(316f, 0f, out _), "C7 gap 315–318");
            Assert.IsFalse(course.TryGetFloor(333f, 0f, out _), "C7 gap 330–334.5");
            Assert.IsTrue(course.TryGetFloor(560f, 2f, out _) == false, "risky gap");
            Assert.IsTrue(course.TryGetFloor(550f, 2f, out float raised));
            Assert.AreEqual(1f, raised, 1e-4f, "risky branch raised +1.0 m");

            int low = 0, high = 0, blocker = 0, thorns = 0, walkable = 0;
            for (int i = 0; i < course.ObstacleCount; i++)
            {
                ObstacleBox o = course.GetObstacle(i);
                switch (o.Class)
                {
                    case ObstacleClass.Low:
                        low++;
                        walkable += o.WalkableTop ? 1 : 0;
                        break;
                    case ObstacleClass.High:
                        high++;
                        break;
                    case ObstacleClass.Blocker:
                        blocker++;
                        break;
                    default:
                        thorns++;
                        break;
                }
            }

            Assert.AreEqual(9, low, "C4 ×3, C6 ×2, C10 log, C11 ×2, C12 safe root");
            Assert.AreEqual(2, walkable);
            Assert.AreEqual(7, high, "C5 ×2, C6 ×2, C10, C11, C12 risky");
            Assert.AreEqual(6, blocker);
            Assert.AreEqual(1, thorns);
        }

        private static string RunBot(float speed, bool risky, out RunnerState final, out PerfectBot bot, out List<string> hits)
        {
            MovementConfig config = ShippedAssets.Config();
            var course = new CoursePath(ShippedAssets.Course());
            var time = new FixedStepTimeSource(60, 5);
            var events = new RunEventBuffer(512);
            var session = new RunSession(config, course, time, events);
            session.Restart(new RunOptions { ForcedSpeed = speed, SkipStartRamp = speed > 0f });
            bot = new PerfectBot(session.Simulation, risky);
            hits = new List<string>();
            int edgeBrushes = 0;
            int dodges = 0;
            int nudges = 0;
            while (session.Phase != RunPhase.Results && session.SessionTick < 60 * 150)
            {
                session.Step(bot.ReadInput(session.SessionTick));
                for (int i = 0; i < events.Count; i++)
                {
                    RunEvent e = events[i];
                    if (e.Type == RunEventType.Hit || e.Type == RunEventType.Died)
                    {
                        hits.Add(e.Type + " " + (e.Type == RunEventType.Hit ? ((HitKind)e.Reason).ToString() : ((DeathCause)e.Reason).ToString()) +
                                 " " + course.GetObstacleLabel(e.Id) + " at s " + session.Simulation.State.S.ToString("0.0", CultureInfo.InvariantCulture));
                    }

                    edgeBrushes += e.Type == RunEventType.EdgeBrush ? 1 : 0;
                    dodges += e.Type == RunEventType.Dodge ? 1 : 0;
                    nudges += e.Type == RunEventType.ForkNudge ? 1 : 0;
                }

                events.Clear();
            }

            final = session.Simulation.State;
            return string.Format(
                CultureInfo.InvariantCulture,
                "[JungleBooze] PerfectBot speed {0} {1}: finished {2}, time {3:0.00} s, hits {4}, coins {5}/{6}, dropped {7}, gaveUp {8}, edgeBrush ticks {9}, nudges {10}, probe steps {11}",
                speed > 0f ? speed.ToString(CultureInfo.InvariantCulture) : "curve",
                risky ? "risky" : "safe",
                final.Finished,
                session.RunSeconds,
                final.Hits,
                final.Coins,
                course.CoinCount,
                final.DroppedInputs,
                bot.GaveUp,
                edgeBrushes,
                nudges,
                bot.ProbeSteps);
        }

        [TestCase(10f, false)]
        [TestCase(13f, false)]
        [TestCase(16f, false)]
        [TestCase(10f, true)]
        [TestCase(13f, true)]
        [TestCase(16f, true)]
        [TestCase(0f, false)]
        public void AC45_PerfectBotClearsTheFeelCourse(float speed, bool risky)
        {
            string report = RunBot(speed, risky, out RunnerState final, out PerfectBot bot, out List<string> hits);
            Debug.Log(report + (hits.Count > 0 ? "\n  " + string.Join("\n  ", hits) : string.Empty));
            Assert.IsTrue(final.Finished, report + " " + string.Join("; ", hits));
            Assert.AreEqual(0, final.Hits, report + " " + string.Join("; ", hits));
            Assert.IsFalse(final.Dead);
            Assert.AreEqual(0, bot.GaveUp);
        }
    }
}
