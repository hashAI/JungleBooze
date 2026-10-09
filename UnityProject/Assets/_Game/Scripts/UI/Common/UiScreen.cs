namespace JungleBooze.UI.Common
{
    /// <summary>Screens of the game UI (one is current; see <see cref="NavigationStack"/>).</summary>
    public enum UiScreen : byte
    {
        /// <summary>In-run HUD only (no menu).</summary>
        Hud = 0,
        Home,
        Pause,
        Settings,
        Credits,
        Journal,
        Abilities,
        Results,
        Upgrade,
    }
}
