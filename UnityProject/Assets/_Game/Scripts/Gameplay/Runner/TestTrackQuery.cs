using System;
using System.Collections.Generic;

namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Flat ground with rectangular gaps and fixed obstacle boxes, for tests, the movement gauntlet and the chunk
    /// validator fixtures (spec 001 sections 6.5 and 15, spec 002 section 6). A footprint has no ground only when it
    /// lies entirely inside one gap, so gaps must not touch or overlap (merge them into one instead). Add gaps and
    /// boxes at setup time; the query methods do not allocate. Boxes can be moved between ticks with
    /// <see cref="MoveBox"/> (for example from an <see cref="IRunnerStepHooks.OnTrackUpdate"/> test hook) to
    /// script a mover.
    /// </summary>
    public sealed class TestTrackQuery : ITrackQuery
    {
        private readonly List<Gap> _gaps = new List<Gap>();
        private readonly List<ObstacleBox> _boxes = new List<ObstacleBox>();
        private readonly float _halfWidth;
        private readonly float _laneWidth;
        private readonly int _laneCount;

        /// <summary>Uses the footprint and lanes of the spec 001 start values.</summary>
        public TestTrackQuery()
            : this(RunnerDesignValues.CreateDefault())
        {
        }

        /// <summary>Uses the footprint half-width and lanes of <paramref name="config"/>.</summary>
        public TestTrackQuery(RunnerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _halfWidth = config.PlayerHitboxWidthM * 0.5f;
            _laneWidth = config.LaneWidthM;
            _laneCount = config.LaneCount;
        }

        private TestTrackQuery(RunnerDesignValues values)
        {
            _halfWidth = values.PlayerHitboxWidthM * 0.5f;
            _laneWidth = values.LaneWidthM;
            _laneCount = values.LaneCount;
        }

        public int GapCount => _gaps.Count;

        public int BoxCount => _boxes.Count;

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

        /// <summary>Adds a box. Boxes are kept in id order (stable for equal ids). Ids must be positive.</summary>
        public TestTrackQuery AddBox(ObstacleBox box)
        {
            if (box.Id <= 0)
            {
                throw new ArgumentException("Obstacle ids start at 1.", nameof(box));
            }

            if (!(box.XMax > box.XMin) || !(box.YMax > box.YMin) || !(box.ZMax > box.ZMin))
            {
                throw new ArgumentException("A box needs a positive size on every axis.", nameof(box));
            }

            int index = _boxes.Count;
            while (index > 0 && _boxes[index - 1].Id > box.Id)
            {
                index--;
            }

            _boxes.Insert(index, box);
            return this;
        }

        /// <summary>Box by insertion-sorted index (0 = lowest id).</summary>
        public ObstacleBox GetBox(int index)
        {
            return _boxes[index];
        }

        /// <summary>
        /// Moves every box with <paramref name="id"/> so its lateral center is <paramref name="centerX"/>. The old
        /// bounds become the Prev bounds, as a track update does for a mover. Call once per tick (step 7a).
        /// </summary>
        public void MoveBox(int id, float centerX)
        {
            for (int i = 0; i < _boxes.Count; i++)
            {
                ObstacleBox b = _boxes[i];
                if (b.Id != id)
                {
                    continue;
                }

                float half = (b.XMax - b.XMin) * 0.5f;
                b.XMinPrev = b.XMin;
                b.XMaxPrev = b.XMax;
                b.XMin = centerX - half;
                b.XMax = centerX + half;
                _boxes[i] = b;
            }
        }

        /// <summary>Marks every box as not moving (Prev = current). Call on ticks where a scripted mover stands still.</summary>
        public void SettleBoxes()
        {
            for (int i = 0; i < _boxes.Count; i++)
            {
                ObstacleBox b = _boxes[i];
                b.XMinPrev = b.XMin;
                b.XMaxPrev = b.XMax;
                _boxes[i] = b;
            }
        }

        public bool HasGround(float x, double zMin, double zMax)
        {
            float xMin = x - _halfWidth;
            float xMax = x + _halfWidth;
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

        public int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer)
        {
            int count = 0;
            for (int i = 0; i < _boxes.Count && count < buffer.Length; i++)
            {
                ObstacleBox b = _boxes[i];
                if (b.ZMax >= zMin && b.ZMin <= zMax)
                {
                    buffer[count++] = b;
                }
            }

            return count;
        }

        public bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length)
        {
            float center = (lane - (_laneCount - 1) * 0.5f) * _laneWidth;
            bool found = false;
            nearEdge = 0.0;
            length = 0f;
            for (int i = 0; i < _gaps.Count; i++)
            {
                Gap g = _gaps[i];
                if (center < g.XMin || center > g.XMax || g.ZStart < fromZ)
                {
                    continue;
                }

                if (!found || g.ZStart < nearEdge)
                {
                    found = true;
                    nearEdge = g.ZStart;
                    length = (float)(g.ZEnd - g.ZStart);
                }
            }

            return found;
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
