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

        /// <summary>Writes atomically and keeps the previous file as the backup.</summary>
        void Write(string text);
    }
}
