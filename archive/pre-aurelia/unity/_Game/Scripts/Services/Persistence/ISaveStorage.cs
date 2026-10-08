namespace JungleBooze.Services.Persistence
{
    /// <summary>Where the save text lives: a file on the device, or memory for tests and test scenes.</summary>
    public interface ISaveStorage
    {
        /// <summary>Human-readable location for logs.</summary>
        string Description { get; }

        /// <summary>True if a primary save exists (even if it later turns out to be unreadable).</summary>
        bool PrimaryExists { get; }

        /// <summary>The primary save text, or null if there is none or it cannot be read.</summary>
        string ReadPrimary();

        /// <summary>The previous good save kept by the last write, or null.</summary>
        string ReadBackup();

        /// <summary>
        /// Replaces the primary save with <paramref name="text"/> so that a crash at any moment leaves either the
        /// old or the new save readable (primary or backup). Throws <see cref="System.IO.IOException"/> or
        /// <see cref="System.UnauthorizedAccessException"/> on failure.
        /// </summary>
        void Write(string text);
    }
}
