namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Optional collision hooks called by <see cref="RunnerSimulation"/> during step 11 (power-ups, GDD 10). Set one
    /// implementation on <see cref="RunnerSimulation.ContactHooks"/>; with none set the runner behaves exactly as
    /// before. Implementations must be deterministic and allocation-free; they may write events with
    /// <see cref="RunnerSimulation.EmitExternal"/>, set invulnerability and remove the touched obstacle from the
    /// track (boxes were already copied for this tick).
    /// </summary>
    public interface IRunnerContactHooks
    {
        /// <summary>
        /// A contact that would end the run (front, from below, or a second stumble while dazed). Return true to
        /// absorb it (Shield): HERO lives and the rest of this tick's contacts are treated as invulnerable.
        /// </summary>
        bool TryAbsorbLethalContact(RunnerSimulation runner, long tick, in ObstacleBox box);

        /// <summary>HERO touched an obstacle while invulnerable (once per obstacle pass).</summary>
        void OnInvulnerableContact(RunnerSimulation runner, long tick, in ObstacleBox box);
    }
}
