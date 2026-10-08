using JungleBooze.Gameplay.Controls;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Views;

namespace JungleBooze.App
{
    /// <summary>All configs a run needs, already converted to plain runtime objects.</summary>
    public sealed class RunConfigSet
    {
        public RunConfigSet(RunnerConfig runner, SpeedCurve speedCurve, InputConfig input, RunnerPresentationConfig presentation, string source)
        {
            Runner = runner;
            SpeedCurve = speedCurve;
            Input = input;
            Presentation = presentation;
            Source = source;
        }

        public RunnerConfig Runner { get; }

        public SpeedCurve SpeedCurve { get; }

        public InputConfig Input { get; }

        public RunnerPresentationConfig Presentation { get; }

        /// <summary>Human-readable note on where each config came from (asset or built-in defaults).</summary>
        public string Source { get; }

        /// <summary>Spec 001 start values for everything (no assets).</summary>
        public static RunConfigSet CreateDefault()
        {
            return new RunConfigSet(
                RunnerConfig.CreateDefault(),
                SpeedCurve.CreateDefault(),
                InputConfig.CreateDefault(),
                RunnerPresentationConfig.CreateDefault(),
                "defaults");
        }
    }
}
