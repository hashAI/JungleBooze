using System;
using JungleBooze.Core.Save;

namespace JungleBooze.Services.Save
{
    /// <summary>
    /// Loads and saves <see cref="SaveData"/> (ARCHITECTURE §9): main file → backup → defaults; a corrupt save never
    /// crashes the game; a save from a newer build is used read-only and never overwritten. Allocates; call on load
    /// and at run end / purchase, never per frame.
    /// </summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly Func<SaveData> _createDefault;

        public SaveService(ISaveStorage storage, Func<SaveData> createDefault)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
        }

        public SaveLoadOutcome LastOutcome { get; private set; }

        public string LastError { get; private set; }

        /// <summary>True after loading a save from a newer build: <see cref="Save"/> does nothing.</summary>
        public bool ReadOnly { get; private set; }

        public SaveData Load()
        {
            ReadOnly = false;
            LastError = null;
            string primary = _storage.ReadPrimary();
            if (primary == null && _storage.ReadBackup() == null)
            {
                LastOutcome = SaveLoadOutcome.New;
                return _createDefault();
            }

            SaveData data = null;
            bool future = false;
            string error = null;
            if (primary != null && SaveCodec.TryDecode(primary, out data, out future, out error))
            {
                ReadOnly = future;
                LastOutcome = future ? SaveLoadOutcome.FutureVersion : SaveLoadOutcome.Primary;
                return data;
            }

            LastError = primary == null ? "main save missing" : error;
            if (SaveCodec.TryDecode(_storage.ReadBackup(), out data, out future, out error))
            {
                ReadOnly = future;
                LastOutcome = future ? SaveLoadOutcome.FutureVersion : SaveLoadOutcome.Backup;
                return data;
            }

            // Both unreadable: start fresh; the next save replaces them (the main file becomes the backup).
            LastOutcome = SaveLoadOutcome.Corrupt;
            return _createDefault();
        }

        /// <summary>Writes the save. Returns false if read-only (a file from a newer build is never overwritten).</summary>
        public bool Save(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (ReadOnly)
            {
                return false;
            }

            _storage.Write(SaveCodec.Encode(data));
            return true;
        }

        /// <summary>Starts over with defaults (debug/reset), even after loading a newer build's file.</summary>
        public SaveData ResetToDefaults()
        {
            ReadOnly = false;
            SaveData data = _createDefault();
            _storage.Write(SaveCodec.Encode(data));
            LastOutcome = SaveLoadOutcome.New;
            return data;
        }
    }
}
