using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;

namespace JungleBooze.Gameplay.Path
{
    /// <summary>
    /// Keeps a <see cref="PathFrame"/> in step with the run: rebuilds it from the run seed on <see cref="BeginRun"/>
    /// and extends it ahead of the hero every frame. Draws nothing and changes nothing in the simulation.
    /// </summary>
    public sealed class PathFrameRunView : IRunView
    {
        private readonly PathFrame _frame;

        public PathFrameRunView(PathFrame frame)
        {
            _frame = frame ?? throw new System.ArgumentNullException(nameof(frame));
        }

        /// <summary>The frame views will read through (T3).</summary>
        public PathFrame Frame => _frame;

        public void BeginRun(GameSession session)
        {
            _frame.BeginRun(session.RunSeed, -_frame.Tuning.BehindM);
            if (session.Runner != null)
            {
                _frame.Extend(session.Runner.Current.Z + _frame.Tuning.AheadM);
            }
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (session.Runner != null)
            {
                _frame.Extend(session.Runner.Current.Z + _frame.Tuning.AheadM);
            }
        }
    }
}
