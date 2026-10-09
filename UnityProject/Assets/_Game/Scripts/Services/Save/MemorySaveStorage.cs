using System;
using System.Collections.Generic;
using JungleBooze.Core.Save;

namespace JungleBooze.Services.Save
{
    /// <summary>In-memory storage for tests and tools (keeps a backup like the file storage).</summary>
    public sealed class MemorySaveStorage : ISaveStorage
    {
        public string Primary { get; set; }

        public string Backup { get; set; }

        /// <summary>An interrupted write's complete new file (tests set it to simulate a crash between renames).</summary>
        public string Pending { get; set; }

        /// <summary>Tests: when set, <see cref="Write"/> throws this (e.g. an IOException for a full disk).</summary>
        public Func<Exception> FailWith { get; set; }

        public List<string> Corrupt { get; } = new List<string>();

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

        public string ReadPending()
        {
            return Pending;
        }

        public void Write(string text)
        {
            Exception failure = FailWith?.Invoke();
            if (failure != null)
            {
                throw failure;
            }

            if (Primary != null)
            {
                Backup = Primary;
            }

            Primary = text;
            Pending = null;
            Writes++;
        }

        public void KeepCorrupt(string text)
        {
            Corrupt.Add(text);
        }
    }
}
