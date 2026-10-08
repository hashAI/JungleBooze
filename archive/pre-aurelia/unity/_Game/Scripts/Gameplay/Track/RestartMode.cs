namespace JungleBooze.Gameplay.Track
{
    /// <summary>The two Game Over buttons (spec 002 section 12.2).</summary>
    public enum RestartMode : byte
    {
        /// <summary>Primary button (Space/Enter): a new seed.</summary>
        RunAgain = 0,

        /// <summary>Reuses the last seed: same chunks, mirrors and coins [ASSUMED button].</summary>
        SameTrack = 1,
    }
}
