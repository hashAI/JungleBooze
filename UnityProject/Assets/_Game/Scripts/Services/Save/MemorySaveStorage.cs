using JungleBooze.Core.Save;

namespace JungleBooze.Services.Save
{
    /// <summary>In-memory storage for tests and tools (keeps a backup like the file storage).</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public string Primary { get; set; }

        public string Backup { get; set; }

        public int Writes { get; private set; }

        public string Description => "memory";

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
            Writes++;
        }
    }
}
