using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Flat ground with rectangular gaps, for tests and the movement gauntlet (spec 001 sections 6.5 and 15).
    /// A footprint has no ground only when it lies entirely inside one gap, so gaps must not touch or overlap
    /// (merge them into one instead). Add gaps at setup time; <see cref="HasGround"/> does not allocate.
    /// </summary>
    public sealed class TestTrackQuery : ITrackQuery
    {
        private readonly List<Gap> _gaps = new List<Gap>();

        public int GapCount => _gaps.Count;

        /// <summary>Adds a gap across the whole track width from <paramref name="zStart"/> to <paramref name="zEnd"/>.</summary>
        public TestTrackQuery AddGap(double zStart, double zEnd)
        {
            return AddGap(zStart, zEnd, float.NegativeInfinity, float.PositiveInfinity);
        }

        /// <summary>Adds a gap limited to the lateral range [xMin, xMax].</summary>
        public TestTrackQuery AddGap(double zStart, double zEnd, float xMin, float xMax)
        {
            if (!(zEnd > zStart) || !(xMax > xMin))
            {
                throw new ArgumentException("A gap needs a positive length and width.");
            }

            _gaps.Add(new Gap(zStart, zEnd, xMin, xMax));
            return this;
        }

        public bool HasGround(float xMin, float xMax, double zMin, double zMax)
        {
            for (int i = 0; i < _gaps.Count; i++)
            {
                Gap g = _gaps[i];
                if (zMin >= g.ZStart && zMax <= g.ZEnd && xMin >= g.XMin && xMax <= g.XMax)
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct Gap
        {
            public Gap(double zStart, double zEnd, float xMin, float xMax)
            {
                ZStart = zStart;
                ZEnd = zEnd;
                XMin = xMin;
                XMax = xMax;
            }

            public double ZStart { get; }

            public double ZEnd { get; }

            public float XMin { get; }

            public float XMax { get; }
        }
    }
}
