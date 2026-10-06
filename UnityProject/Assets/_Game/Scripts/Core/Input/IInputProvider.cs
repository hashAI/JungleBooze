namespace JungleBooze.Core
{
    /// <summary>
    /// Source of player commands for the simulation. Implementations: touch (device), bot (simulations),
    /// replay (bug reproduction and regression tests). The simulation cannot tell them apart.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Returns the commands for the given tick. Called exactly once per simulation step,
        /// with strictly increasing tick values. Must not allocate.
        /// </summary>
        InputCommand ReadCommands(long tick);
    }
}
