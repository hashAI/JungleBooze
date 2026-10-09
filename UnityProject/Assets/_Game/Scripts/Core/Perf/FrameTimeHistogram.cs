using System;

namespace JungleBooze.Core.Perf
{
    /// <summary>
    /// Allocation-free frame-time statistics for long runs (a 10-minute soak is 36,000 frames): a fixed histogram of
    /// <c>bucketMs</c>-wide buckets up to <c>maxMs</c> (anything slower lands in the last bucket and is still counted
    /// in <see cref="MaxMs"/>), plus count, mean and worst frame. Percentiles return the upper edge of the bucket that
    /// holds the requested rank, so they are exact to one bucket (0.1 ms by default).
    /// </summary>
    public sealed class FrameTimeHistogram
    {
        private readonly int[] _buckets;
        private readonly float _bucketMs;
        private double _sumMs;

        public FrameTimeHistogram(float bucketMs = 0.1f, float maxMs = 200f)
        {
            if (bucketMs <= 0f || maxMs <= bucketMs)
            {
                throw new ArgumentOutOfRangeException(nameof(bucketMs), "Need 0 < bucketMs < maxMs.");
            }

            _bucketMs = bucketMs;
            _buckets = new int[(int)Math.Ceiling(maxMs / bucketMs) + 1];
        }

        public long Count { get; private set; }

        public float MaxMs { get; private set; }

        public double MeanMs => Count > 0 ? _sumMs / Count : 0.0;

        public void Clear()
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            Count = 0;
            MaxMs = 0f;
            _sumMs = 0.0;
        }

        public void Add(float ms)
        {
            if (ms < 0f || float.IsNaN(ms))
            {
                return;
            }

            int index = (int)(ms / _bucketMs);
            if (index >= _buckets.Length)
            {
                index = _buckets.Length - 1;
            }

            _buckets[index]++;
            Count++;
            _sumMs += ms;
            if (ms > MaxMs)
            {
                MaxMs = ms;
            }
        }

        /// <summary>Frame time at or below which <paramref name="fraction"/> of frames fall (0.5 = median).</summary>
        public float Percentile(double fraction)
        {
            if (Count == 0)
            {
                return 0f;
            }

            long rank = (long)Math.Ceiling(Math.Max(0.0, Math.Min(1.0, fraction)) * Count);
            if (rank < 1)
            {
                rank = 1;
            }

            long seen = 0;
            for (int i = 0; i < _buckets.Length; i++)
            {
                seen += _buckets[i];
                if (seen >= rank)
                {
                    return i == _buckets.Length - 1 ? MaxMs : Math.Min(MaxMs, (i + 1) * _bucketMs);
                }
            }

            return MaxMs;
        }

        /// <summary>Share of frames slower than <paramref name="thresholdMs"/> (to one bucket).</summary>
        public double FractionAbove(float thresholdMs)
        {
            if (Count == 0)
            {
                return 0.0;
            }

            int first = (int)(thresholdMs / _bucketMs) + 1;
            long above = 0;
            for (int i = Math.Max(0, first); i < _buckets.Length; i++)
            {
                above += _buckets[i];
            }

            return (double)above / Count;
        }
    }
}
