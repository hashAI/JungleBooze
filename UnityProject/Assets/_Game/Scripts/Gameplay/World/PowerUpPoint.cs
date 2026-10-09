namespace JungleBooze.Gameplay.World
{
    /// <summary>A placed power-up pickup in path space.</summary>
    public readonly struct PowerUpPoint
    {
        public PowerUpPoint(int id, PowerUpKind kind, float s, float x, float y)
        {
            Id = id;
            Kind = kind;
            S = s;
            X = x;
            Y = y;
        }

        public int Id { get; }

        public PowerUpKind Kind { get; }

        public float S { get; }

        public float X { get; }

        public float Y { get; }
    }
}
