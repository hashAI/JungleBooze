namespace JungleBooze.Gameplay.Session
{
    /// <summary>Player-facing session actions the HUD can trigger (pause button, Resume, Restart).</summary>
    public interface IRunCommands
    {
        void Pause();

        void Resume();

        void Restart();
    }
}
