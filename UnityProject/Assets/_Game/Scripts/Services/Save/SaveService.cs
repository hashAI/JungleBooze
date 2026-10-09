using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using JungleBooze.Core.Save;

namespace JungleBooze.Services.Save
{
    /// <summary>
    /// Loads and saves <see cref="SaveData"/> (ARCHITECTURE §9): main file → interrupted write (.tmp) → backup →
    /// defaults; a corrupt save never crashes the game and is copied aside before it is replaced; a save from a newer
    /// build is used read-only and never overwritten. Storage errors never escape <see cref="Save"/> (review S3): the
    /// save stays pending and <see cref="RetryPending"/> writes it later with exponential back-off. Allocates; call on
    /// load and at run end / purchase / safe points, never per frame.
    /// </summary>
    public sealed class SaveService : IProfileStore
    {
        /// <summary>First retry delay after a failed write, s (doubles per failure up to <see cref="MaxBackoffSeconds"/>).</summary>
        public const double BaseBackoffSeconds = 2.0;

        public const double MaxBackoffSeconds = 60.0;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private readonly ISaveStorage _storage;
        private readonly Func<SaveData> _createDefault;
        private readonly Func<double> _now;
        private double _nextRetry;

        public SaveService(ISaveStorage storage, Func<SaveData> createDefault, Func<double> clockSeconds = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
            _now = clockSeconds ?? (() => Clock.Elapsed.TotalSeconds);
        }

        public SaveLoadOutcome LastOutcome { get; private set; }

        public string LastError { get; private set; }

        /// <summary>True after loading a save from a newer build: <see cref="Save"/> does nothing.</summary>
        public bool ReadOnly { get; private set; }

        public bool HasPendingSave { get; private set; }

        /// <summary>Consecutive failed writes (0 after a good one).</summary>
        public int Failures { get; private set; }

        public SaveData Load()
        {
            ReadOnly = false;
            LastError = null;
            string primary = _storage.ReadPrimary();
            string pending = _storage.ReadPending();
            string backup = _storage.ReadBackup();
            if (primary == null && pending == null && backup == null)
            {
                LastOutcome = SaveLoadOutcome.New;
                return _createDefault();
            }

            SaveData data;
            bool future;
            string error = null;
            if (primary != null && SaveCodec.TryDecode(primary, out data, out future, out error))
            {
                ReadOnly = future;
                LastOutcome = future ? SaveLoadOutcome.FutureVersion : SaveLoadOutcome.Primary;
                return data;
            }

            LastError = primary == null ? "main save missing" : error;
            if (primary != null)
            {
                _storage.KeepCorrupt(primary);
            }

            // Review S12: killed between the two renames → the complete new file is still in .tmp.
            if (pending != null && SaveCodec.TryDecode(pending, out data, out future, out _))
            {
                ReadOnly = future;
                LastOutcome = future ? SaveLoadOutcome.FutureVersion : SaveLoadOutcome.Pending;
                if (!future)
                {
                    Save(data);
                }

                return data;
            }

            if (SaveCodec.TryDecode(backup, out data, out future, out error))
            {
                ReadOnly = future;
                LastOutcome = future ? SaveLoadOutcome.FutureVersion : SaveLoadOutcome.Backup;
                return data;
            }

            // Both unreadable: keep copies for diagnosis, then start fresh (the next save replaces them).
            if (backup != null)
            {
                _storage.KeepCorrupt(backup);
            }

            LastOutcome = SaveLoadOutcome.Corrupt;
            return _createDefault();
        }

        /// <summary>
        /// Writes the save. Returns false if read-only (a file from a newer build is never overwritten) or if the
        /// storage failed (I/O, full disk, permissions): then <see cref="HasPendingSave"/> is set and nothing throws.
        /// </summary>
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

            string text = SaveCodec.Encode(data);
            try
            {
                _storage.Write(text);
            }
            catch (IOException exception)
            {
                return Failed(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                return Failed(exception);
            }
            catch (SecurityException exception)
            {
                return Failed(exception);
            }
            catch (NotSupportedException exception)
            {
                return Failed(exception);
            }

            HasPendingSave = false;
            Failures = 0;
            LastError = null;
            return true;
        }

        public bool RetryPending(SaveData data, bool force)
        {
            if (!HasPendingSave || ReadOnly)
            {
                return !HasPendingSave;
            }

            if (!force && _now() < _nextRetry)
            {
                return false;
            }

            return Save(data);
        }

        /// <summary>Starts over with defaults (debug/reset), even after loading a newer build's file.</summary>
        public SaveData ResetToDefaults()
        {
            ReadOnly = false;
            SaveData data = _createDefault();
            Save(data);
            LastOutcome = SaveLoadOutcome.New;
            return data;
        }

        private bool Failed(Exception exception)
        {
            HasPendingSave = true;
            Failures++;
            LastError = exception.GetType().Name + ": " + exception.Message;
            double delay = Math.Min(MaxBackoffSeconds, BaseBackoffSeconds * Math.Pow(2.0, Math.Min(10, Failures - 1)));
            _nextRetry = _now() + delay;
            return false;
        }
    }
}
