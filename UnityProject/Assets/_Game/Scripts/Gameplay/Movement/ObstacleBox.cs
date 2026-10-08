namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// An authored obstacle box in path space (s along the path, x lateral, y height in the path frame).
    /// The simulation derives the forgiving hitbox from it (spec 101 §2.6).
    /// </summary>
    public readonly struct ObstacleBox
    {
        public ObstacleBox(int id, ObstacleClass kind, float sMin, float sMax, float xMin, float xMax, float yMin, float yMax, bool walkableTop)
        {
            Id = id;
            Class = kind;
            SMin = sMin;
            SMax = sMax;
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            WalkableTop = walkableTop;
        }

        public int Id { get; }

        public ObstacleClass Class { get; }

        public float SMin { get; }

        public float SMax { get; }

        public float XMin { get; }

        public float XMax { get; }

        public float YMin { get; }

        public float YMax { get; }

        /// <summary>Low obstacles only: landing on top from above runs across it.</summary>
        public bool WalkableTop { get; }

        public float CenterX => (XMin + XMax) * 0.5f;
    }
}
