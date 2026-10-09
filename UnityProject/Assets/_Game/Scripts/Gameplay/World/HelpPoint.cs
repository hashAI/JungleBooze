namespace JungleBooze.Gameplay.World
{
    /// <summary>A placed first-run help marker in path space.</summary>
    public readonly struct HelpPoint
    {
        public HelpPoint(int id, HelpMove move, float s, float xMin, float xMax)
        {
            Id = id;
            Move = move;
            S = s;
            XMin = xMin;
            XMax = xMax;
        }

        public int Id { get; }

        public HelpMove Move { get; }

        public float S { get; }

        public float XMin { get; }

        public float XMax { get; }
    }
}
