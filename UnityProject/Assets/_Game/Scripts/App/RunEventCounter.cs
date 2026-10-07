using JungleBooze.Gameplay.PowerUps;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;

namespace JungleBooze.App
{
    /// <summary>
    /// Counts the run events that missions need and the track totals do not keep: slides and Shields picked up
    /// (GDD 13.2). A run view only so it sees every event; it draws nothing. Reset at the start of each run.
    /// </summary>
    public sealed class RunEventCounter : IRunView
    {
        public int Slides { get; private set; }

        public int Shields { get; private set; }

        public void BeginRun(GameSession session)
        {
            Slides = 0;
            Shields = 0;
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (e.Type == RunnerEventType.SlideStarted)
            {
                Slides++;
            }
            else if (e.Type == RunnerEventType.PowerUpCollected && e.Value == (short)PowerUpType.Shield)
            {
                Shields++;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
        }
    }
}
