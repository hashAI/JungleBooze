using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>Stage A/C world: endless flat ground, open headroom, no obstacles, no coins.</summary>
    public sealed class FlatRunWorld : IRunWorld
    {
        public FlatRunWorld(ulong seed)
        {
            Seed = seed;
        }

        public ulong Seed { get; }

        public int Coins => 0;

        public RunnerSimulation CreateRunner(RunnerConfig config, SpeedCurve speedCurve)
        {
            return new RunnerSimulation(config, speedCurve, FlatTrackQuery.Instance, OpenHeadroomQuery.Instance);
        }

        public void AfterRunnerStep(long tick, RunnerSimulation runner)
        {
        }
    }
}
