namespace JungleBooze.Core.Perf
{
    /// <summary>
    /// Device quality tier (ADR 0010). High is the full, owner-reviewed look on iPhone 12 (A14) and newer; Low is the
    /// fallback for older or memory-starved devices. The numeric values are the order of the Unity quality levels.
    /// </summary>
    public enum DeviceTier : byte
    {
        Low = 0,
        High = 1,
    }
}
