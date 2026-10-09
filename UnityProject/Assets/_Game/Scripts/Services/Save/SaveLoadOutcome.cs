namespace JungleBooze.Services.Save
{
    /// <summary>Where <see cref="SaveService.Load"/> got its data from.</summary>
    public enum SaveLoadOutcome
    {
        /// <summary>No save yet (first launch).</summary>
        New = 0,
        Primary,
        /// <summary>The main file was unreadable; the backup was used.</summary>
        Backup,
        /// <summary>Both files were unreadable; fresh defaults (the next save replaces them).</summary>
        Corrupt,
        /// <summary>The file is from a newer build: read-only, never overwritten.</summary>
        FutureVersion,
        /// <summary>The main file was missing or unreadable; a complete interrupted write (.tmp) was promoted (review S12).</summary>
        Pending,
    }
}
