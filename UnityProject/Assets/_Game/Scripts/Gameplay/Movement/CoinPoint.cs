namespace JungleBooze.Gameplay.Movement
{
    /// <summary>A collectable coin in path space.</summary>
    public readonly struct CoinPoint
    {
        public CoinPoint(int id, float s, float x, float y)
        {
            Id = id;
            S = s;
            X = x;
            Y = y;
        }

        public int Id { get; }

        public float S { get; }

        public float X { get; }

        public float Y { get; }
    }
}
