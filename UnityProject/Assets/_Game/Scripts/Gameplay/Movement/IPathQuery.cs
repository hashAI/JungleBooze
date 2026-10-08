namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Everything the simulation needs to know about the path (spec 101 §2.1): lateral bounds, floor height or
    /// "no floor", obstacles, coins and split points, all in path space. The feel course, unit tests and later the
    /// chunk streamer implement it. Implementations must not allocate in these calls.
    /// </summary>
    public interface IPathQuery
    {
        /// <summary>Finish line s (m); <see cref="float.PositiveInfinity"/> for endless paths.</summary>
        float FinishS { get; }

        /// <summary>
        /// Walkable lateral range at <paramref name="s"/>. <paramref name="x"/> picks the branch inside a fork.
        /// </summary>
        void GetLateralBounds(float s, float x, out float xMin, out float xMax);

        /// <summary>Floor height at (s, x), or false over a gap.</summary>
        bool TryGetFloor(float s, float x, out float floorY);

        /// <summary>
        /// From a point over a gap: the nearest s in (s, s + reach] where floor starts again, and its height.
        /// </summary>
        bool TryFindFloorAhead(float s, float x, float reach, out float lipS, out float lipY);

        int ObstacleCount { get; }

        /// <summary>Obstacle by id (ids are 0 … count−1, sorted by <see cref="ObstacleBox.SMin"/>).</summary>
        ObstacleBox GetObstacle(int id);

        /// <summary>Writes ids of obstacles overlapping [sMin, sMax] into <paramref name="results"/>; returns the count.</summary>
        int FindObstacles(float sMin, float sMax, int[] results);

        int CoinCount { get; }

        CoinPoint GetCoin(int id);

        /// <summary>Writes ids of coins with s in [sMin, sMax] into <paramref name="results"/>; returns the count.</summary>
        int FindCoins(float sMin, float sMax, int[] results);

        int ForkCount { get; }

        ForkPoint GetFork(int index);
    }
}
