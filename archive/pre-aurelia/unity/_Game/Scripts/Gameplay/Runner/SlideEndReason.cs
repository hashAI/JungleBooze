namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Payload of <see cref="RunnerEventType.SlideEnded"/> (stored in <see cref="RunnerEvent.Value"/>).</summary>
    public enum SlideEndReason : byte
    {
        Timeout = 0,
        Jump = 1,
        Ledge = 2,

        /// <summary>The companion's Lift picked HERO up (GDD 15.1).</summary>
        Lift = 3,
    }
}
