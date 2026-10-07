namespace JungleBooze.UI.Menus
{
    /// <summary>
    /// English menu strings (main menu, pause menu, Game Over, Settings). [ASSUMED] Placeholder string table until
    /// the localization system exists (week 4), like <see cref="Hud.HudStrings"/>: every menu label is read from
    /// here so the switch to localization tables touches one file.
    /// </summary>
    public static class MenuStrings
    {
        /// <summary>[ASSUMED] Placeholder game name until the owner picks one (docs/STATUS.md).</summary>
        public const string GameTitle = "Jungle Runner";

        // Main menu.
        public const string Play = "Play";
        public const string Settings = "Settings";
        public const string BestScore = "Best score";
        public const string TotalCoins = "Coins";

        // Pause menu.
        public const string Restart = "Restart";
        public const string Home = "Home";

        // Game Over.
        public const string Score = "Score";
        public const string Distance = "Distance";
        public const string CoinsThisRun = "Coins";

        // Continue (GDD 14.4).
        public const string ContinueTitle = "Continue?";
        public const string ContinueFree = "Free continue";
        public const string ContinueFreeNote = "Duko will catch you!";
        public const string ContinuePaid = "Continue";
        public const string ContinueWatchAd = "Watch ad (soon)";
        public const string ContinueSkip = "Skip";
        public const string ContinueWallet = "Your coins";
        public const string ContinueKeyHint = "Space: continue   Esc: skip";

        // Settings.
        public const string Music = "Music";
        public const string SoundEffects = "Sound effects";
        public const string Haptics = "Haptics";
        public const string ReduceMotion = "Reduce motion";
        public const string On = "On";
        public const string Off = "Off";
        public const string Back = "Back";
        public const string ReplayTutorial = "Replay tutorial";
        public const string TutorialQueued = "Tutorial: next run";
        public const string PercentSuffix = "%";
    }
}
