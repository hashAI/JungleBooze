using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// A presentation object driven by the run driver (ARCHITECTURE section 4). Views read simulation state and
    /// events and never write them. All three calls must not allocate during a run.
    /// </summary>
    public interface IRunView
    {
        /// <summary>A new run started (first run or Restart). Snap to the new state and clear transient effects.</summary>
        void BeginRun(GameSession session);

        /// <summary>One simulation event, oldest first, after the frame's simulation steps.</summary>
        void OnRunnerEvent(in RunnerEvent e);

        /// <summary>
        /// Once per rendered frame after events. <paramref name="alpha"/> interpolates between
        /// <see cref="RunnerSimulation.Previous"/> and <see cref="RunnerSimulation.Current"/>;
        /// <paramref name="realDeltaSeconds"/> is real frame time for cosmetic smoothing only.
        /// </summary>
        void Render(GameSession session, float alpha, float realDeltaSeconds);
    }
}
