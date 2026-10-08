using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Course;
using JungleBooze.Gameplay.Movement;

namespace JungleBooze.Tests.EditMode.Movement
{
    /// <summary>Test harness: a runner simulation on a small hand-built course.</summary>
    internal sealed class SimRig
    {
        public const float Dt = 1f / 60f;

        public SimRig(CourseData course, RunOptions options, MovementConfig config = null)
        {
            Course = new CoursePath(course);
            Events = new RunEventBuffer(4096);
            Sim = new RunnerSimulation(config ?? SpecConfig.Create(), Course, Dt, Events);
            Sim.Reset(options);
        }

        public CoursePath Course { get; }

        public RunEventBuffer Events { get; }

        public RunnerSimulation Sim { get; }

        public ref readonly RunnerState State => ref Sim.State;

        /// <summary>Flat open path (half-width 3.5 m unless given), constant speed, no start ramp.</summary>
        public static SimRig Flat(float speed = 10f, Action<CourseData> build = null, float half = 3.5f, float startX = 0f, MovementConfig config = null, bool firstRun = false)
        {
            CourseData data = Path(half);
            build?.Invoke(data);
            return new SimRig(data, new RunOptions { ForcedSpeed = speed, SkipStartRamp = true, StartX = startX, FirstRun = firstRun }, config);
        }

        public static CourseData Path(float half)
        {
            var data = new CourseData { FinishS = 100000f, RunOutLength = 0f };
            data.Widths.Add(new CourseWidthKey(0f, -half, half));
            return data;
        }

        public static void Low(CourseData c, float sMin, float sMax, float height = 0.6f, float xMin = -10f, float xMax = 10f, bool walkable = false)
        {
            c.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Low, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax, YMin = 0f, YMax = height, WalkableTop = walkable });
        }

        public static void High(CourseData c, float sMin, float sMax, float bottom = 1.0f, float xMin = -10f, float xMax = 10f)
        {
            c.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.High, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax, YMin = bottom, YMax = bottom + 1.5f });
        }

        public static void Blocker(CourseData c, float sMin, float sMax, float xMin, float xMax)
        {
            c.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Blocker, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax, YMin = 0f, YMax = 2.5f });
        }

        public static void Thorns(CourseData c, float sMin, float sMax, float xMin = -10f, float xMax = 10f)
        {
            c.Obstacles.Add(new CourseObstacle { Class = ObstacleClass.Thorns, SMin = sMin, SMax = sMax, XMin = xMin, XMax = xMax, YMin = 0f, YMax = 0.5f });
        }

        public static void Gap(CourseData c, float sMin, float sMax)
        {
            c.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Gap, SMin = sMin, SMax = sMax, XMin = -10f, XMax = 10f });
        }

        public static void Floor(CourseData c, float sMin, float sMax, float y)
        {
            c.Floors.Add(new CourseFloorPatch { Kind = CourseFloorKind.Ramp, SMin = sMin, SMax = sMax, XMin = -10f, XMax = 10f, Y0 = y, Y1 = y });
        }

        public void Step(InputFrame frame)
        {
            Sim.Step(frame);
        }

        public void Step(InputCommand command)
        {
            Sim.Step(InputFrame.FromCommands(command));
        }

        public void Steps(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Sim.Step(InputFrame.Empty);
            }
        }

        /// <summary>Steps until the condition holds; returns false if it never did.</summary>
        public bool StepUntil(Func<RunnerState, bool> condition, int maxTicks = 6000)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (condition(Sim.State))
                {
                    return true;
                }

                Sim.Step(InputFrame.Empty);
            }

            return condition(Sim.State);
        }

        /// <summary>Steps until s ≥ target (empty input).</summary>
        public void RunTo(float s)
        {
            StepUntil(st => st.S >= s);
        }

        public int CountEvents(RunEventType type)
        {
            return Events.CountOf(type);
        }

        public bool HasEventAt(RunEventType type, long tick)
        {
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Type == type && Events[i].Tick == tick)
                {
                    return true;
                }
            }

            return false;
        }

        public RunEvent LastEvent(RunEventType type)
        {
            for (int i = Events.Count - 1; i >= 0; i--)
            {
                if (Events[i].Type == type)
                {
                    return Events[i];
                }
            }

            return default;
        }
    }
}
