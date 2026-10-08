using JungleBooze.Core;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace JungleBooze.Tests.PlayMode
{
    /// <summary>
    /// Baseline cost of the deterministic core. Also guards the zero-allocation rule:
    /// the measured code must not allocate (checked with the GC allocation counter).
    /// </summary>
    public sealed class CorePerformanceTests
    {
        private static uint _sink;

        [Test, Performance]
        public void Pcg32_NextUInt_Throughput()
        {
            var rng = new Pcg32Random(1UL);

            Measure.Method(() =>
                {
                    for (int i = 0; i < 10000; i++)
                    {
                        _sink ^= rng.NextUInt();
                    }
                })
                .WarmupCount(5)
                .MeasurementCount(20)
                .GC()
                .Run();
        }

        [Test]
        public void ReplayInputProvider_ReadCommands_DoesNotAllocate()
        {
            var recording = new InputRecording(1UL);
            for (int tick = 0; tick < 1000; tick += 7)
            {
                recording.Add(tick, InputFrame.FromCommands(InputCommand.Jump));
            }

            var replay = new ReplayInputProvider(recording);
            var rng = new Pcg32Random(2UL);

            Assert.That(() =>
            {
                for (int tick = 0; tick < 1000; tick++)
                {
                    replay.ReadInput(tick);
                    rng.NextFloat();
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
