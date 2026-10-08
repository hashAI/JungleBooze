namespace JungleBooze.Core
{
    /// <summary>One recorded, non-empty input frame and the tick it belongs to.</summary>
    public readonly struct RecordedInput
    {
        public RecordedInput(long tick, InputFrame frame)
        {
            Tick = tick;
            Frame = frame;
        }

        public long Tick { get; }

        public InputFrame Frame { get; }
    }
}
