namespace JungleBooze.Gameplay.Session
{
    /// <summary>Actions of the Continue screen (GDD 14.4). Implementations ignore calls outside the offer.</summary>
    public interface IContinueCommands
    {
        /// <summary>The free first-session continue from the companion is offered.</summary>
        bool FreeContinueAvailable { get; }

        /// <summary>Coin price of the next continue in this run.</summary>
        int NextContinueCost { get; }

        /// <summary>The wallet holds enough coins for <see cref="NextContinueCost"/>.</summary>
        bool CanAffordContinue { get; }

        /// <summary>Continue with the free first-session continue if it is offered, otherwise by paying coins.</summary>
        bool ContinueRun();

        /// <summary>Skip: go on to Game Over.</summary>
        void SkipContinue();
    }
}
