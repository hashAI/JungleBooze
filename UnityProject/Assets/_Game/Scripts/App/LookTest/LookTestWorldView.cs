using System;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Loops the look-test stretch around the runner. The stretch (built by the editor scene builder in loop-0 world
    /// coordinates) is split into equal segments along the path distance s; segment i covers
    /// [i × length, (i + 1) × length) of a loop. Each frame every segment is shifted by whole loop offsets
    /// (<see cref="LookTestPath.LoopOffset"/>) so the segments together cover [s − behind, s − behind + loop).
    /// Heading and height of the path repeat every loop, so the loop is seamless.
    /// The optional backdrop (far landforms, L5 in ENVIRONMENT_STRATEGY 4.2) rides with the run: it moves by the
    /// loop offset continuously, so it has no translation parallax (like a matte painting) and never pops.
    /// Moves only segments that fell behind; no allocations per frame.
    /// </summary>
    public sealed class LookTestWorldView : MonoBehaviour
    {
        private Transform[] _segments;
        private long[] _placedLoop;
        private float _segmentLengthM;
        private float _loopLengthM;
        private float _behindM;
        private Vector3 _loopOffset;
        private Transform _backdrop;
        private GameObject[] _detail;
        private sbyte[] _detailActive;
        private float _detailRangeM;

        /// <summary>Number of segments being looped.</summary>
        public int SegmentCount => _segments == null ? 0 : _segments.Length;

        public void Init(Transform[] segments, float loopLengthM, Vector3 loopOffset, float behindM, Transform backdrop)
        {
            if (segments == null || segments.Length == 0)
            {
                throw new ArgumentException("The look test needs at least one stretch segment.", nameof(segments));
            }

            if (!(loopLengthM > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(loopLengthM));
            }

            _segments = segments;
            _loopLengthM = loopLengthM;
            _loopOffset = loopOffset;
            _segmentLengthM = loopLengthM / segments.Length;
            _behindM = Mathf.Clamp(behindM, 0f, loopLengthM - _segmentLengthM);
            _backdrop = backdrop;
            _placedLoop = new long[segments.Length];
            ResetPlacement();
        }

        /// <summary>
        /// Near-detail groups (one per segment, may be null): active only while the segment starts less than
        /// <paramref name="rangeM"/> ahead of the runner. The dense verge plants are only drawn where they read
        /// (ENVIRONMENT_STRATEGY 4.2 budget for L0).
        /// </summary>
        public void InitDetail(GameObject[] detail, float rangeM)
        {
            _detail = detail;
            _detailRangeM = rangeM;
            _detailActive = detail == null ? null : new sbyte[detail.Length];
        }

        /// <summary>True if a segment starting at <paramref name="segmentStartS"/> shows its near detail for the runner at <paramref name="s"/>.</summary>
        public static bool DetailVisible(double segmentStartS, double s, float rangeM)
        {
            return segmentStartS < s + rangeM;
        }

        /// <summary>Forgets the placement so the next <see cref="Render"/> places every segment again.</summary>
        public void ResetPlacement()
        {
            if (_placedLoop == null)
            {
                return;
            }

            for (int i = 0; i < _placedLoop.Length; i++)
            {
                _placedLoop[i] = long.MinValue;
            }

            if (_detailActive != null)
            {
                for (int i = 0; i < _detailActive.Length; i++)
                {
                    _detailActive[i] = 0;
                }
            }
        }

        /// <summary>Loop index segment <paramref name="index"/> is placed at for the runner at distance <paramref name="s"/>.</summary>
        public static long LoopFor(int index, double s, float segmentLengthM, float loopLengthM, float behindM)
        {
            double windowStart = s - behindM;
            double segmentStart = index * (double)segmentLengthM;
            return (long)Math.Ceiling((windowStart - segmentStart - segmentLengthM) / loopLengthM);
        }

        /// <summary>Places the segments around the runner's path distance <paramref name="s"/> (meters).</summary>
        public void Render(double s)
        {
            if (_segments == null)
            {
                return;
            }

            for (int i = 0; i < _segments.Length; i++)
            {
                long loop = LoopFor(i, s, _segmentLengthM, _loopLengthM, _behindM);
                if (_detail != null && i < _detail.Length && _detail[i] != null)
                {
                    bool show = DetailVisible(i * (double)_segmentLengthM + loop * (double)_loopLengthM, s, _detailRangeM);
                    sbyte state = show ? (sbyte)1 : (sbyte)-1;
                    if (_detailActive[i] != state)
                    {
                        _detailActive[i] = state;
                        _detail[i].SetActive(show);
                    }
                }

                if (_placedLoop[i] == loop)
                {
                    continue;
                }

                _placedLoop[i] = loop;
                Transform segment = _segments[i];
                if (segment != null)
                {
                    segment.localPosition = _loopOffset * loop;
                }
            }

            if (_backdrop != null)
            {
                _backdrop.localPosition = _loopOffset * (float)(s / _loopLengthM);
            }
        }
    }
}
