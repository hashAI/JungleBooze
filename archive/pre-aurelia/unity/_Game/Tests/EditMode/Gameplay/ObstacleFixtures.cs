using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>
    /// Obstacle boxes with the sizes of spec 002 table 5.1 (spec 001 9.2 reference boxes plus the mover), centered on
    /// a lane center (lane width 2.4 m). <c>zFront</c> is the face HERO runs into.
    /// </summary>
    internal static class ObstacleFixtures
    {
        public const float BarrierWidthM = 2.04f;
        public const float MoverWidthM = 1.90f;

        public static ObstacleBox LowBarrier(int id, int lane, double zFront)
        {
            return Box(id, ObstacleArchetype.LowBarrier, lane, BarrierWidthM, 0f, 0.8f, zFront, 0.6);
        }

        public static ObstacleBox HighBarrier(int id, int lane, double zFront, double depthM = 0.5)
        {
            return Box(id, ObstacleArchetype.HighBarrier, lane, BarrierWidthM, 1.1f, 3.0f, zFront, depthM);
        }

        public static ObstacleBox FullBlock(int id, int lane, double zFront, double depthM = 1.0)
        {
            return Box(id, ObstacleArchetype.FullBlock, lane, BarrierWidthM, 0f, 3.0f, zFront, depthM);
        }

        public static ObstacleBox Mover(int id, int lane, double zFront)
        {
            return Box(id, ObstacleArchetype.Mover, lane, MoverWidthM, 0f, 1.9f, zFront, 1.6);
        }

        public static ObstacleBox Box(
            int id,
            ObstacleArchetype archetype,
            int lane,
            float width,
            float yMin,
            float yMax,
            double zFront,
            double depthM)
        {
            float center = RunnerTestHarness.LaneX(lane);
            return ObstacleBox.Static(
                id,
                archetype,
                (byte)lane,
                center - width * 0.5f,
                center + width * 0.5f,
                yMin,
                yMax,
                zFront,
                zFront + depthM);
        }
    }
}
