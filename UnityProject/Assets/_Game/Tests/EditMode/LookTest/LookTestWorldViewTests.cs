using System;
using JungleBooze.App.LookTest;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Views;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleBooze.Tests.EditMode
{
    /// <summary>ADR 0004 look test: the looped segments always cover [z − behind, z − behind + loop) without gaps.</summary>
    public sealed class LookTestWorldViewTests
    {
        private const float LoopM = 200f;
        private const float BehindM = 22f;
        private const int Segments = 8;

        private sealed class NoInput : IInputProvider
        {
            public InputCommand ReadCommands(long tick)
            {
                return InputCommand.None;
            }
        }

        [TestCase(0.0)]
        [TestCase(37.0)]
        [TestCase(260.0)]
        [TestCase(1234.0)]
        public void SegmentsCoverTheWindowAroundTheRunner(double distanceM)
        {
            var session = new GameSession(
                RunnerConfig.CreateDefault(),
                SpeedCurve.CreateConstant(20.0),
                new NoInput(),
                new FlatRunWorldFactory(),
                RunnerPresentationConfig.CreateDefault().ToSessionTimings(),
                7UL);
            session.Begin();
            int guard = 0;
            while (session.Runner.Current.Z < distanceM && guard++ < 100000)
            {
                session.Advance(1.0 / 60.0);
            }

            var root = new GameObject("Stretch");
            try
            {
                var segments = new Transform[Segments];
                for (int i = 0; i < Segments; i++)
                {
                    segments[i] = new GameObject("Segment_" + i).transform;
                    segments[i].SetParent(root.transform, false);
                }

                LookTestWorldView view = root.AddComponent<LookTestWorldView>();
                view.Init(segments, LoopM, BehindM);
                view.BeginRun(session);

                double windowStart = session.Runner.Current.Z - BehindM;
                float segLength = LoopM / Segments;
                var starts = new float[Segments];
                for (int i = 0; i < Segments; i++)
                {
                    starts[i] = segments[i].localPosition.z;
                    float offset = starts[i] - i * segLength;
                    Assert.AreEqual(0f, Mathf.Repeat(offset + 1f, LoopM) - 1f, 0.01f, "segment " + i + " moved by whole loops only");
                }

                Array.Sort(starts);
                Assert.LessOrEqual(starts[0], windowStart + 0.01);
                Assert.Greater(starts[0] + segLength, windowStart);
                for (int i = 1; i < Segments; i++)
                {
                    Assert.AreEqual(starts[i - 1] + segLength, starts[i], 0.01f, "contiguous");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
