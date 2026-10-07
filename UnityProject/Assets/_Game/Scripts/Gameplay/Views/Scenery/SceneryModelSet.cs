namespace JungleBooze.Gameplay.Views
{
    /// <summary>One scenery model at two levels of detail: the parts to draw near HERO and the simpler parts far away.</summary>
    public sealed class SceneryModelSet
    {
        public SceneryModelSet(SceneryPart[] near, SceneryPart[] far)
        {
            Near = near;
            Far = far;
            NearTriangles = Sum(near);
            FarTriangles = Sum(far);
        }

        public SceneryPart[] Near { get; }

        public SceneryPart[] Far { get; }

        /// <summary>Triangles of one piece drawn with <see cref="Near"/>.</summary>
        public int NearTriangles { get; }

        /// <summary>Triangles of one piece drawn with <see cref="Far"/>.</summary>
        public int FarTriangles { get; }

        private static int Sum(SceneryPart[] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                total += parts[i].Triangles;
            }

            return total;
        }
    }
}
