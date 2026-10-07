namespace JungleBooze.Core
{
    /// <summary>
    /// The calendar, injected wherever a rule depends on the date (daily reward, daily challenge; GDD 13.3).
    /// Rules read the day only from here, never from the system clock, so tests can move time by hand.
    /// </summary>
    public interface IDayClock
    {
        /// <summary>Whole days since 1970-01-01 in the player's local time ("one claim per calendar day").</summary>
        int Today { get; }
    }
}
