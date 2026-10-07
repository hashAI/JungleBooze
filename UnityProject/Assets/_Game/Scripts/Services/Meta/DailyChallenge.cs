namespace JungleBooze.Services.Meta
{
    /// <summary>Today's daily challenge (GDD 13.3): one seeded goal per day, the same for everyone.</summary>
    public readonly struct DailyChallenge
    {
        public DailyChallenge(MissionKind kind, int target, int reward, bool done)
        {
            Kind = kind;
            Target = target;
            Reward = reward;
            Done = done;
        }

        public MissionKind Kind { get; }

        public int Target { get; }

        public int Reward { get; }

        public bool Done { get; }
    }
}
