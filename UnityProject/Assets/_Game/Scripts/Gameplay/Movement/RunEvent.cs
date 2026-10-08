namespace JungleBooze.Gameplay.Movement
{
    /// <summary>One simulation event. Plain struct, no boxing.</summary>
    public readonly struct RunEvent
    {
        public RunEvent(RunEventType type, long tick, int id, byte reason, float value)
        {
            Type = type;
            Tick = tick;
            Id = id;
            Reason = reason;
            Value = value;
        }

        public RunEventType Type { get; }

        public long Tick { get; }

        public int Id { get; }

        public byte Reason { get; }

        public float Value { get; }
    }
}
