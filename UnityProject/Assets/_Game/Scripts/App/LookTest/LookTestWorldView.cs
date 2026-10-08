using System;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using UnityEngine;

namespace JungleBooze.App.LookTest
{
    /// <summary>
    /// Loops the look-test stretch around the runner. The stretch (built by the editor scene builder) is split into
    /// equal segments along +z; segment i covers [i × length, (i + 1) × length) of a loop of
    /// <c>count × length</c> meters. Each frame every segment is moved by whole loops so the segments together cover
    /// [z − behind, z − behind + loop). The stretch is built periodic in z, so the loop is seamless.
    /// Moves only the segment that fell behind; no allocations per frame.
    /// </summary>
    public sealed class LookTestWorldView : MonoBehaviour, IRunView
    {
        private Transform[] _segments;
        private long[] _placedLoop;
        private float _segmentLengthM;
        private float _loopLengthM;
        private float _behindM;

        /// <summary>Number of segments being looped.</summary>
        public int SegmentCount => _segments == null ? 0 : _segments.Length;

        public void Init(Transform[] segments, float loopLengthM, float behindM)
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
            _segmentLengthM = loopLengthM / segments.Length;
            _behindM = Mathf.Clamp(behindM, 0f, loopLengthM - _segmentLengthM);
            _placedLoop = new long[segments.Length];
            for (int i = 0; i < _placedLoop.Length; i++)
            {
                _placedLoop[i] = long.MinValue;
            }
        }

        public void BeginRun(GameSession session)
        {
            if (_placedLoop == null)
            {
                return;
            }

            for (int i = 0; i < _placedLoop.Length; i++)
            {
                _placedLoop[i] = long.MinValue;
            }

            Render(session, 1f, 0f);
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (_segments == null)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            RunnerInterpolation.Evaluate(runner.Previous, runner.Current, alpha, out _, out _, out double z);
            double windowStart = z - _behindM;

            for (int i = 0; i < _segments.Length; i++)
            {
                // Smallest loop index k whose copy of segment i still ends ahead of the window start.
                double segmentStart = i * (double)_segmentLengthM;
                long loop = (long)Math.Ceiling((windowStart - segmentStart - _segmentLengthM) / _loopLengthM);
                if (_placedLoop[i] == loop)
                {
                    continue;
                }

                _placedLoop[i] = loop;
                Transform segment = _segments[i];
                if (segment != null)
                {
                    segment.localPosition = new Vector3(0f, 0f, (float)(segmentStart + loop * (double)_loopLengthM));
                }
            }
        }
    }
}
