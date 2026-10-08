using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Optional extra for an <see cref="IRunWorld"/> that keeps score and knows the world's obstacle names
    /// (spec 002 sections 10 and 12.4). The Game Over panel shows the score row and the named cause only when the
    /// current world implements this; otherwise it shows a generic cause and no score.
    /// </summary>
    public interface IRunWorldSummary
    {
        /// <summary>Score of this run (distance points plus bonuses).</summary>
        long Score { get; }

        /// <summary>
        /// Game Over cause line, for example "Hit: Giant tree trunk". Called once when the panel opens (may allocate).
        /// </summary>
        string DescribeDeath(DeathCause cause, ObstacleArchetype archetype, bool afterStumble);
    }
}
