using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>Creates a <see cref="FlatRunWorld"/> per run.</summary>
    public sealed class FlatRunWorldFactory : IRunWorldFactory
    {
        public IRunWorld Create(ulong seed, RunnerConfig config)
        {
            return new FlatRunWorld(seed);
        }
    }
}
