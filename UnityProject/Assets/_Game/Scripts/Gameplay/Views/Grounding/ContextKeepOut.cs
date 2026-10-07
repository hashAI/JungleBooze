using JungleBooze.Gameplay.Path;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Views
{
    /// <summary>
    /// Read-only query for the scenery placer (spec 005 3.3, AC-509): the ground footprints the context pieces of the
    /// obstacles in a stretch of route occupy. It uses exactly the layout the rigs are built from
    /// (<see cref="ContextLayout"/>), so a scenery cell that skips these circles never overlaps a root plate, stump,
    /// support trunk, verge trunk, scree bank or root crown. Stateless: the same inputs always give the same circles.
    /// The scenery system calls it when a cell is rebuilt; nothing here changes any simulation state.
    /// </summary>
    public static class ContextKeepOut
    {
        /// <summary>Largest circle count one obstacle row can add.</summary>
        public const int MaxPerObstacle = ContextLayout.MaxAnchors;

        /// <summary>
        /// Adds the footprints (inflated by <paramref name="bufferM"/>) of one obstacle row to
        /// <paramref name="output"/> starting at <paramref name="start"/>; returns the number added (0 when the
        /// archetype has no context or the output is full). <paramref name="scratch"/> must hold
        /// <see cref="ContextLayout.MaxAnchors"/> entries (pass a cached array to avoid allocating).
        /// </summary>
        public static int Add(
            ulong runSeed,
            in ObstacleInstance obstacle,
            PathFrame frame,
            float laneWidthM,
            float bendCurvature,
            float bufferM,
            ContextAnchor[] scratch,
            KeepOutCircle[] output,
            int start)
        {
            if (obstacle.Archetype == ObstacleArchetype.Gap || obstacle.Archetype == ObstacleArchetype.LaneStrike)
            {
                return 0;
            }

            double centerS = obstacle.Z + (obstacle.DepthM * 0.5);
            frame.Sample(centerS, out PathPose pose);
            int count = ContextLayout.ForObstacle(
                runSeed,
                obstacle.Id,
                obstacle.Archetype,
                obstacle.LaneMask,
                obstacle.FromLane,
                obstacle.ToLane,
                pose.Curvature,
                laneWidthM,
                bendCurvature,
                out _,
                out _,
                scratch);
            int added = 0;
            for (int i = 0; i < count && start + added < output.Length; i++)
            {
                output[start + added] = new KeepOutCircle
                {
                    S = centerS + scratch[i].ZOffsetM,
                    X = scratch[i].X,
                    RadiusM = scratch[i].RadiusM + bufferM,
                    HeightM = scratch[i].HeightM,
                };
                added++;
            }

            return added;
        }
    }
}
