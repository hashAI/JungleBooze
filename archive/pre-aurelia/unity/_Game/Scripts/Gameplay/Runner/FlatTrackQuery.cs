using System;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Endless flat ground everywhere and no obstacles. Default track when none is given.</summary>
    public sealed class FlatTrackQuery : ITrackQuery
    {
        public static readonly FlatTrackQuery Instance = new FlatTrackQuery();

        public bool HasGround(float x, double zMin, double zMax)
        {
            return true;
        }

        public int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer)
        {
            return 0;
        }

        public bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length)
        {
            nearEdge = 0.0;
            length = 0f;
            return false;
        }
    }
}
