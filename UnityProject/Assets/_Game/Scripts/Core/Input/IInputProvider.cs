namespace JungleBooze.Core
{
    /// <summary>
    /// Source of player input for the simulation. Implementations: touch/keyboard (device and editor), bot
    /// (simulations), replay (bug reproduction and regression tests). The simulation cannot tell them apart.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Returns the input for the given tick. Called exactly once per simulation step, with strictly increasing
        /// tick values. Must not allocate.
        /// </summary>
        InputFrame ReadInput(long tick);
    }
}
