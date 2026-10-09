namespace JungleBooze.App.Perf
{
    /// <summary>Forces a quality tier for testing; Auto picks it from the device (ADR 0010).</summary>
    public enum QualityTierOverride
    {
        Auto = 0,
        High = 1,
        Low = 2,
    }
}
