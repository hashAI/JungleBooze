using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>Creates the <see cref="IRunWorld"/> for a new run. Called at session start and on every Restart.</summary>
    public interface IRunWorldFactory
    {
        IRunWorld Create(ulong seed, RunnerConfig config);
    }
}
