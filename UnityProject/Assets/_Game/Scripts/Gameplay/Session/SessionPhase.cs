namespace JungleBooze.Gameplay.Session
{
    /// <summary>Where a <see cref="GameSession"/> is in its run loop (spec 001 sections 8 and 10.6).</summary>
    public enum SessionPhase : byte
    {
        /// <summary>The simulation steps every frame.</summary>
        Running = 0,

        /// <summary>Pause menu is shown; nothing steps and nothing counts down.</summary>
        Paused = 1,

        /// <summary>3-2-1 resume countdown in real time; the simulation does not step.</summary>
        Countdown = 2,

        /// <summary>HERO died: hit-pause, then camera hold. The simulation does not step.</summary>
        Dying = 3,

        /// <summary>Game Over panel is shown; waiting for Restart.</summary>
        GameOver = 4,
    }
}
