namespace JungleBooze.Core.Save
{
    /// <summary>
    /// Saving the profile from gameplay code (review S3, S10). <see cref="Save"/> never throws for storage errors:
    /// it returns false and keeps the save pending; <see cref="RetryPending"/> writes it at the next safe point
    /// (run again, app pause) with back-off.
    /// </summary>
    public interface IProfileStore
    {
        /// <summary>True while the last save failed and has not been written since.</summary>
        bool HasPendingSave { get; }

        /// <summary>Last error text (null after a good save).</summary>
        string LastError { get; }

        /// <summary>Writes the profile. False if read-only (newer build's file) or the write failed (then pending).</summary>
        bool Save(SaveData data);

        /// <summary>
        /// Writes a pending save when the back-off has elapsed (or <paramref name="force"/>). True when nothing is
        /// pending afterwards.
        /// </summary>
        bool RetryPending(SaveData data, bool force);
    }
}
