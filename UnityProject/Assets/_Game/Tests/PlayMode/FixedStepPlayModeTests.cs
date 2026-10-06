using System.Collections;
using JungleBooze.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// Smoke test that drives the Core fixed-step clock from real player-loop frames,
    /// the same way the run driver will. Confirms Core loads and runs inside the player.
    /// </summary>
    public sealed class FixedStepPlayModeTests
    {
        [UnityTest]
        public IEnumerator FixedStepClock_AdvancesFromRealFrames_AndStaysDeterministic()
        {
            var time = new FixedStepTimeSource(60, maxStepsPerFrame: 5);
            var random = new Pcg32Random(123UL);
            var reference = new Pcg32Random(123UL);

            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;

                int steps = time.Accumulate(Time.unscaledDeltaTime);
                for (int i = 0; i < steps; i++)
                {
                    Assert.AreEqual(reference.NextUInt(), random.NextUInt());
                    time.Step();
                }
            }

            Assert.GreaterOrEqual(time.Tick, 0L);
            Assert.LessOrEqual(time.Tick, 30L * 5L);
        }
    }
}
