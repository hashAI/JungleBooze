namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Player-facing session actions the HUD can trigger (pause button, Resume, Game Over buttons).
    /// Implementations ignore calls that do not fit the current phase (for example Run again during the
    /// Game Over input lock).
    /// </summary>
    public interface IRunCommands
    {
        void Pause();

        void Resume();

        /// <summary>Game Over "Run again": a new run with a new seed (spec 002 section 12.2).</summary>
        void Restart();

        /// <summary>Game Over "Same track": a new run with the last run's seed (spec 002 section 12.2).</summary>
        void RestartSameTrack();
    }
}
