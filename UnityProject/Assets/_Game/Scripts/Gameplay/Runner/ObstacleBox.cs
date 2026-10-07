namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// One axis-aligned collision box of an obstacle, in world space, as the track reports it for the current tick
    /// (spec 002 section 6). An obstacle has one box per occupied lane; all of them share <see cref="Id"/>.
    /// Plain value type so it can be written into a caller-owned buffer without allocating.
    /// </summary>
    /// <remarks>
    /// <see cref="XMinPrev"/>/<see cref="XMaxPrev"/> are the box's lateral bounds at the end of the previous tick.
    /// They equal <see cref="XMin"/>/<see cref="XMax"/> for every box that does not move; for a mover they hold
    /// <c>moverXPrev ± width / 2</c>, so the runner can sweep with relative motion (spec 002 section 5.2).
    /// On the tick a box is first reported, Prev must equal the current values (no motion "from nowhere").
    /// Boxes never move in y or z.
    /// </remarks>
    public struct ObstacleBox
    {
        /// <summary>Per-run obstacle id (1, 2, 3, ...), shared by every box of the obstacle. Never 0.</summary>
        public int Id;

        public ObstacleArchetype Archetype;

        /// <summary>The lane this box was placed in (for a mover: its start lane). Informational.</summary>
        public byte Lane;

        public float XMin;

        public float XMax;

        public float XMinPrev;

        public float XMaxPrev;

        /// <summary>Bottom of the box above the track surface (m).</summary>
        public float YMin;

        /// <summary>Top of the box above the track surface (m).</summary>
        public float YMax;

        /// <summary>Front face (smallest z, the face HERO runs into).</summary>
        public double ZMin;

        /// <summary>Back face.</summary>
        public double ZMax;

        /// <summary>Lateral center at the end of this tick.</summary>
        public float CenterX => (XMin + XMax) * 0.5f;

        /// <summary>True if the box moved sideways during this tick.</summary>
        public bool IsMoving => XMin != XMinPrev || XMax != XMaxPrev;

        /// <summary>A box that does not move (Prev = current).</summary>
        public static ObstacleBox Static(
            int id,
            ObstacleArchetype archetype,
            byte lane,
            float xMin,
            float xMax,
            float yMin,
            float yMax,
            double zMin,
            double zMax)
        {
            return new ObstacleBox
            {
                Id = id,
                Archetype = archetype,
                Lane = lane,
                XMin = xMin,
                XMax = xMax,
                XMinPrev = xMin,
                XMaxPrev = xMax,
                YMin = yMin,
                YMax = yMax,
                ZMin = zMin,
                ZMax = zMax,
            };
        }

        public override string ToString()
        {
            return "Box#" + Id + " " + Archetype + " x[" + XMin + "," + XMax + "] prev[" + XMinPrev + "," + XMaxPrev
                + "] y[" + YMin + "," + YMax + "] z[" + ZMin + "," + ZMax + "]";
        }
    }
}
