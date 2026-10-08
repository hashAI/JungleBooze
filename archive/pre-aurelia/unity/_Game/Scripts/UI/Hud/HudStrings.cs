namespace JungleBooze.UI.Hud
{
    /// <summary>
    /// English HUD strings. [ASSUMED] Placeholder string table until the localization system exists (week 4);
    /// every HUD label is read from here so the switch to localization tables touches one file.
    /// </summary>
    public static class HudStrings
    {
        public const string DistanceUnit = "m";
        public const string Paused = "Paused";
        public const string Resume = "Resume";
        public const string PauseButtonName = "Pause";

        // Ready prompt (spec 002 12.1).
        public const string ReadyPromptKeyboard = "Swipe or press a key to run";
        public const string ReadyPromptTouch = "Swipe or tap to run";

        // In-run call-outs.
        public const string Stumble = "Stumble!";
        public const string NearMiss = "Near miss!";

        // Game Over panel (spec 002 12.1 and 12.4).
        public const string GameOver = "Game Over";
        public const string Distance = "Distance";
        public const string Coins = "Coins";
        public const string Score = "Score";
        public const string Best = "Best";
        public const string NewBest = "New best!";
        public const string Seed = "Seed";
        public const string RunAgain = "Play again";
        public const string SameTrack = "Same track";
        public const string GameOverKeyHint = "Space: play again    T: same track";

        // Generic cause lines, used until the track world names the obstacle (spec 002 12.4, [ASSUMED] wording).
        public const string CauseHit = "Hit an obstacle";
        public const string CauseTrippedTwice = "Tripped twice";
        public const string CauseFell = "Fell into a ravine";
        public const string CauseEnded = "Run ended";
    }
}
