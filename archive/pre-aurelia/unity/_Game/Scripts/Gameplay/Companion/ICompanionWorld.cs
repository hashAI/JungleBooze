namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// Optional part of a run world that supports the companion's Lift coin pull (GDD 15.1: coins from all lanes
    /// within the pull range are collected while HERO is lifted). The session sets the range from the companion
    /// tuning when the run is set up.
    /// </summary>
    public interface ICompanionWorld
    {
        /// <summary>Pull range ahead of HERO in m while lifted (0 = no pull).</summary>
        double LiftCoinPullAheadM { get; set; }
    }
}
