namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Decides whether the Continue screen is offered after a death (GDD 14.4). The app layer implements it, since
    /// the answer depends on the player's save (wallet, first session, free continue used). Without a policy the
    /// session goes straight to Game Over.
    /// </summary>
    public interface IContinuePolicy
    {
        /// <summary>True if at least one usable continue option exists for this death (free, or affordable coins).</summary>
        bool CanOfferContinue(GameSession session);
    }
}
