namespace JungleBooze.Gameplay.Movement
{
    /// <summary>Phase 1 obstacle classes (spec 101 §4.1). Gaps are floor, not obstacles (<see cref="IPathQuery"/>).</summary>
    public enum ObstacleClass : byte
    {
        Low = 0,
        High = 1,
        Blocker = 2,
        Thorns = 3,
    }
}
