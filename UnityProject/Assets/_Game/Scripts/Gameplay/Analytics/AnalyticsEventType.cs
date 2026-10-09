namespace JungleBooze.Gameplay.Analytics
{
    /// <summary>GDD §22 events recorded by the slice (local-first, no PII). Append only.</summary>
    public enum AnalyticsEventType : byte
    {
        RunStarted = 0,
        RunEnded,
        Death,
        TraversalResult,
        CreatureFound,
        SecretFound,
        PowerUp,
        AbilityUnlocked,
        ReviveOffered,
        ReviveUsed,
        RouteSelected,
    }
}
