namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Player-facing session actions the HUD and menus can trigger (Play, pause button, Resume, Restart, Home,
    /// Game Over buttons). Implementations ignore calls that do not fit the current phase (for example Play again
    /// during the Game Over input lock).
    /// </summary>
    public interface IRunCommands
    {
        /// <summary>Main menu "Play": starts the run set up behind the menu (GDD section 19).</summary>
        void Play();

        void Pause();

        void Resume();

        /// <summary>
        /// Game Over "Play again" or pause menu "Restart": a new run with a new seed (spec 002 section 12.2).
        /// </summary>
        void Restart();

        /// <summary>Game Over "Same track": a new run with the last run's seed (spec 002 section 12.2).</summary>
        void RestartSameTrack();

        /// <summary>Pause menu or Game Over "Home": leaves the run and shows the main menu.</summary>
        void GoHome();
    }
}
