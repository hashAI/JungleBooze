namespace JungleBooze.Core
{
    /// <summary>One recorded, non-empty input sample.</summary>
    public readonly struct InputFrame
    {
        public InputFrame(long tick, InputCommand commands)
        {
            Tick = tick;
            Commands = commands;
        }

        public long Tick { get; }

        public InputCommand Commands { get; }
    }
}
