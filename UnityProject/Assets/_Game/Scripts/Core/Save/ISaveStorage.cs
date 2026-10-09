namespace JungleBooze.Core.Save
{
    /// <summary>Where save text lives (file on device, memory in tests). Implementations write atomically.</summary>
    public interface ISaveStorage
    {
        string Description { get; }

        /// <summary>Main save text, or null if missing/unreadable.</summary>
        string ReadPrimary();

        /// <summary>Previous good save text, or null.</summary>
        string ReadBackup();

        /// <summary>
        /// Text of a write that was interrupted after the new file was complete but before it replaced the main file
        /// (review S12), or null.
        /// </summary>
        string ReadPending();

        /// <summary>Writes atomically and keeps the previous file as the backup. May throw on I/O or permission errors.</summary>
        void Write(string text);

        /// <summary>Keeps a copy of an unreadable save for diagnosis before it is replaced (review S12). Never throws.</summary>
        void KeepCorrupt(string text);
    }
}
