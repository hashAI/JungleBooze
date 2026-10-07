namespace JungleBooze.Gameplay.Controls
{
    /// <summary>Non-simulation actions from the keyboard (they never enter the simulation's input stream).</summary>
    public enum RunMetaAction : byte
    {
        None = 0,

        /// <summary>P or Escape: pause while running, resume (start the countdown) while paused.</summary>
        TogglePause = 1,

        /// <summary>R: restart after Game Over.</summary>
        Restart = 2,

        /// <summary>K: end the run now (development builds only, until collisions exist).</summary>
        DebugEndRun = 3,
    }
}
