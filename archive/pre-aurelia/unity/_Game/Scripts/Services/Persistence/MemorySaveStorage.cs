namespace JungleBooze.Services.Persistence
{
    /// <summary>Save kept in memory only (tests and test scenes never touch the player's real save).</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public MemorySaveStorage(string primary = null, string backup = null)
        {
            Primary = primary;
            Backup = backup;
        }

        public string Primary { get; set; }

        public string Backup { get; set; }

        /// <summary>Number of <see cref="Write"/> calls.</summary>
        public int WriteCount { get; private set; }

        public string Description => "memory";

        public bool PrimaryExists => Primary != null;

        public string ReadPrimary()
        {
            return Primary;
        }

        public string ReadBackup()
        {
            return Backup;
        }

        public void Write(string text)
        {
            if (Primary != null)
            {
                Backup = Primary;
            }

            Primary = text;
            WriteCount++;
        }
    }
}
