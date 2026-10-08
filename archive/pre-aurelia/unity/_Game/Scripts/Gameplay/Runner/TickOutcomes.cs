namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Outcome of each movement command flag on the last processed tick, at the moment it was processed
    /// (<see cref="CommandOutcome.None"/> if the flag was not set). For tests, the bot report and debugging.
    /// </summary>
    public struct TickOutcomes
    {
        public CommandOutcome MoveLeft { get; internal set; }

        public CommandOutcome MoveRight { get; internal set; }

        public CommandOutcome Jump { get; internal set; }

        public CommandOutcome Slide { get; internal set; }
    }
}
