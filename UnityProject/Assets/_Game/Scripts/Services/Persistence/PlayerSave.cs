using System;
using System.IO;
using UnityEngine;

namespace JungleBooze.Services.Persistence
{
    /// <summary>
    /// The player's local progress and settings (best score, coin wallet, settings), loaded once at start and
    /// written with <see cref="Save"/>. Load order: primary save, then the backup, then defaults; a broken file
    /// never stops the game. Changes are kept in memory and marked dirty; callers write at natural points (run end,
    /// closing Settings, app sent to background). Getters never allocate; load, record and save do.
    /// </summary>
    public sealed class PlayerSave
    {
        private readonly ISaveStorage _storage;
        private readonly SaveData _data;
        private bool _dirty;

        private PlayerSave(ISaveStorage storage, SaveData data, SaveLoadOutcome outcome, bool dirty)
        {
            _storage = storage;
            _data = data;
            LoadOutcome = outcome;
            _dirty = dirty;
        }

        /// <summary>Raised after any setting changes (volume, haptics, reduce motion).</summary>
        public event Action SettingsChanged;

        public SaveLoadOutcome LoadOutcome { get; }

        /// <summary>True when there are changes not yet written.</summary>
        public bool IsDirty => _dirty;

        public long BestScore => _data.bestScore;

        public long BestDistanceM => _data.bestDistanceM;

        public long TotalCoins => _data.totalCoins;

        public int RunsPlayed => _data.runsPlayed;

        /// <summary>The last run recorded this session (default value until the first one).</summary>
        public RunRecord LastRun { get; private set; }

        public float MusicVolume
        {
            get => _data.settings.musicVolume;
            set
            {
                float v = ClampVolume(value);
                if (v != _data.settings.musicVolume)
                {
                    _data.settings.musicVolume = v;
                    OnSettingChanged();
                }
            }
        }

        public float SfxVolume
        {
            get => _data.settings.sfxVolume;
            set
            {
                float v = ClampVolume(value);
                if (v != _data.settings.sfxVolume)
                {
                    _data.settings.sfxVolume = v;
                    OnSettingChanged();
                }
            }
        }

        public bool HapticsEnabled
        {
            get => _data.settings.hapticsEnabled;
            set
            {
                if (value != _data.settings.hapticsEnabled)
                {
                    _data.settings.hapticsEnabled = value;
                    OnSettingChanged();
                }
            }
        }

        public bool ReduceMotion
        {
            get => _data.settings.reduceMotion;
            set
            {
                if (value != _data.settings.reduceMotion)
                {
                    _data.settings.reduceMotion = value;
                    OnSettingChanged();
                }
            }
        }

        /// <summary>Reads the save from <paramref name="storage"/> (primary, then backup, then defaults). Never throws on bad data.</summary>
        public static PlayerSave Load(ISaveStorage storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            bool primaryExists = storage.PrimaryExists;
            string primaryError = null;
            if (primaryExists && SaveCodec.TryDecode(storage.ReadPrimary(), out SaveData primary, out primaryError))
            {
                return new PlayerSave(storage, primary, SaveLoadOutcome.Loaded, false);
            }

            string backupText = storage.ReadBackup();
            if (backupText != null && SaveCodec.TryDecode(backupText, out SaveData backup, out _))
            {
                Debug.LogWarning("[JungleBooze] Save " + storage.Description + " could not be read" +
                    (primaryError != null ? " (" + primaryError + ")" : string.Empty) + "; restored the backup.");

                // Dirty, so the next save writes a good primary again.
                return new PlayerSave(storage, backup, SaveLoadOutcome.RestoredFromBackup, true);
            }

            if (primaryExists)
            {
                Debug.LogWarning("[JungleBooze] Save " + storage.Description + " is broken (" + primaryError +
                    ") and there is no usable backup; starting from defaults.");
                return new PlayerSave(storage, SaveData.CreateDefault(), SaveLoadOutcome.CorruptReset, false);
            }

            return new PlayerSave(storage, SaveData.CreateDefault(), SaveLoadOutcome.NoSave, false);
        }

        /// <summary>A save that lives in memory only (tests and test scenes).</summary>
        public static PlayerSave CreateInMemory()
        {
            return Load(new MemorySaveStorage());
        }

        /// <summary>
        /// Records one finished (or abandoned) run: adds its coins to the wallet, updates the best score and
        /// distance, counts the run. Does not write; call <see cref="Save"/> after.
        /// </summary>
        public RunRecord RecordRun(long score, long distanceM, int coins)
        {
            if (score < 0L)
            {
                score = 0L;
            }

            if (distanceM < 0L)
            {
                distanceM = 0L;
            }

            if (coins < 0)
            {
                coins = 0;
            }

            long previousBest = _data.bestScore;
            if (score > _data.bestScore)
            {
                _data.bestScore = score;
            }

            if (distanceM > _data.bestDistanceM)
            {
                _data.bestDistanceM = distanceM;
            }

            _data.totalCoins = _data.totalCoins > long.MaxValue - coins ? long.MaxValue : _data.totalCoins + coins;
            if (_data.runsPlayed < int.MaxValue)
            {
                _data.runsPlayed++;
            }

            _dirty = true;
            LastRun = new RunRecord(score, distanceM, coins, previousBest, _data.totalCoins);
            return LastRun;
        }

        /// <summary>Writes the save now. Returns false (and logs a warning) if the device refused the write.</summary>
        public bool Save()
        {
            string json = SaveCodec.Encode(_data);
            try
            {
                _storage.Write(json);
            }
            catch (IOException e)
            {
                Debug.LogWarning("[JungleBooze] Could not write save " + _storage.Description + ": " + e.Message);
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning("[JungleBooze] Could not write save " + _storage.Description + ": " + e.Message);
                return false;
            }

            _dirty = false;
            return true;
        }

        /// <summary>Writes only if something changed since the last write. Returns true if nothing is left unsaved.</summary>
        public bool SaveIfDirty()
        {
            return !_dirty || Save();
        }

        private static float ClampVolume(float value)
        {
            return float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        private void OnSettingChanged()
        {
            _dirty = true;
            SettingsChanged?.Invoke();
        }
    }
}
