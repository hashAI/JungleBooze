using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Everything in a run besides HERO's movement: track, obstacles, coins (stage B and later). One instance per
    /// run, created by an <see cref="IRunWorldFactory"/> from the run seed. Deterministic, no allocations per step.
    /// The default is <see cref="FlatRunWorld"/> (endless flat ground, no obstacles, no coins).
    /// </summary>
    public interface IRunWorld
    {
        /// <summary>Seed the world was generated from.</summary>
        ulong Seed { get; }

        /// <summary>Coins collected in this run (HUD).</summary>
        int Coins { get; }

        /// <summary>
        /// Builds this run's runner simulation, wired to the world's track, headroom and (later) collision queries.
        /// Called once per run, right after the world is created.
        /// </summary>
        RunnerSimulation CreateRunner(RunnerConfig config, SpeedCurve speedCurve);

        /// <summary>
        /// Called once per simulation tick, after the runner stepped <paramref name="tick"/> (spawn ahead, despawn
        /// behind, coin pickups). Must not write runner state except through the runner's public hooks.
        /// </summary>
        void AfterRunnerStep(long tick, RunnerSimulation runner);
    }
}
