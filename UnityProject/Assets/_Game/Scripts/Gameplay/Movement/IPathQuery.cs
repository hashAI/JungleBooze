namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// Everything the simulation needs to know about the path (spec 101 §2.1): lateral bounds, floor height or
    /// "no floor", obstacles, coins and split points, all in path space. The feel course (<c>CoursePath</c>), unit
    /// tests and the chunk streamer (<c>WorldPath</c>, spec 102) implement it. Implementations must not allocate in
    /// these calls.
    ///
    /// Ids: obstacle and coin ids are stable for as long as the item exists. A finite path numbers them 0 … n−1; a
    /// streamed path hands out increasing ids and recycles storage, so per-item state is kept in arrays of
    /// <see cref="ObstacleSlots"/> / <see cref="CoinSlots"/> entries indexed by <c>id % slots</c> and stamped with
    /// the id (no clearing when a slot is reused).
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

        /// <summary>Size of per-obstacle state arrays; every live obstacle id maps to a distinct <c>id % ObstacleSlots</c>.</summary>
        int ObstacleSlots { get; }

        /// <summary>Obstacle by id (ids increase with <see cref="ObstacleBox.SMin"/>).</summary>
        ObstacleBox GetObstacle(int id);

        /// <summary>Writes ids of obstacles overlapping [sMin, sMax] into <paramref name="results"/>; returns the count.</summary>
        int FindObstacles(float sMin, float sMax, int[] results);

        /// <summary>Size of per-coin state arrays; every live coin id maps to a distinct <c>id % CoinSlots</c>.</summary>
        int CoinSlots { get; }

        CoinPoint GetCoin(int id);

        /// <summary>Writes ids of coins with s in [sMin, sMax] into <paramref name="results"/>; returns the count.</summary>
        int FindCoins(float sMin, float sMax, int[] results);

        /// <summary>Number of route splits currently on the path (a streamed path only lists loaded chunks).</summary>
        int ForkCount { get; }

        /// <summary>Split by index 0 … <see cref="ForkCount"/>−1 (ordered by s); <see cref="ForkPoint.Id"/> is stable.</summary>
        ForkPoint GetFork(int index);
    }
}
