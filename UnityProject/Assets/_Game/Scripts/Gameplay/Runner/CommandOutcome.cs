namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// What happened to one movement command flag (spec 001 rule I9).
    /// <see cref="Buffered"/> and <see cref="Queued"/> are pending outcomes: when the buffered jump or queued lane
    /// move resolves, its count moves to the final outcome (for example Buffered to Executed or Expired), so the
    /// counters always sum to the number of command flags received.
    /// </summary>
    public enum CommandOutcome : byte
    {
        /// <summary>No command flag of this kind on this tick.</summary>
        None = 0,
        Executed = 1,
        Queued = 2,
        Buffered = 3,
        Bumped = 4,
        Cancelled = 5,
        Superseded = 6,
        Expired = 7,
        Invalidated = 8,
        AlreadyActive = 9,
        Ignored = 10,
    }
}
