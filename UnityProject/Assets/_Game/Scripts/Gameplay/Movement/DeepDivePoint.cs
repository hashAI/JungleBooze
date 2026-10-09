namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// A placed Deep Breath zone (spec 103 §4.5): a dive starting in [SMin, SMax] × [XMin, XMax] follows the
    /// underwater passage to (ExitS, ExitX) at <see cref="Depth"/> below the swim line.
    /// </summary>
    public readonly struct DeepDivePoint
    {
        public DeepDivePoint(int id, float sMin, float sMax, float xMin, float xMax, float exitS, float exitX, float waterY)
        {
            Id = id;
            SMin = sMin;
            SMax = sMax;
            XMin = xMin;
            XMax = xMax;
            ExitS = exitS;
            ExitX = exitX;
            WaterY = waterY;
        }

        public int Id { get; }

        public float SMin { get; }

        public float SMax { get; }

        public float XMin { get; }

        public float XMax { get; }

        public float ExitS { get; }

        public float ExitX { get; }

        public float WaterY { get; }
    }
}
