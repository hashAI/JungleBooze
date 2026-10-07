namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// One simulation event (spec 001 section 11). Plain struct written into <see cref="RunnerEventBuffer"/>;
    /// see <see cref="RunnerEventType"/> for what each field means per type.
    /// </summary>
    public struct RunnerEvent
    {
        public RunnerEventType Type;
        public long Tick;
        public sbyte Dir;
        public byte Lane;
        public byte Flags;
        public int ObstacleId;
        public byte Archetype;
        public short Value;

        public bool HasFlag(byte flag)
        {
            return (Flags & flag) != 0;
        }

        public override string ToString()
        {
            return Type + "@" + Tick + " dir=" + Dir + " lane=" + Lane + " flags=" + Flags + " value=" + Value;
        }
    }
}
