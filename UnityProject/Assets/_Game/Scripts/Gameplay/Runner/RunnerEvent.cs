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
        /// <summary>
        /// Obstacle id (Stumbled, NearMiss, Died, MoverStarted, MoverSettled) or coin id (CoinCollected); 0 if none.
        /// Renamed from <c>ObstacleId</c> by spec 002 section 16.1.5.
        /// </summary>
        public int EntityId;
        public byte Archetype;
        public short Value;

        /// <summary>Same field as <see cref="EntityId"/>, kept for code written against the spec 001 name.</summary>
        public int ObstacleId
        {
            get => EntityId;
            set => EntityId = value;
        }

        public bool HasFlag(byte flag)
        {
            return (Flags & flag) != 0;
        }

        public override string ToString()
        {
            return Type + "@" + Tick + " dir=" + Dir + " lane=" + Lane + " flags=" + Flags + " value=" + Value + " entity=" + EntityId + " archetype=" + Archetype;
        }
    }
}
