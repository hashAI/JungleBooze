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

        /// <summary>Current app session number (1 = first session), after <see cref="BeginSession"/>.</summary>
        public int SessionNumber => _data.sessionsStarted < 0 ? 0 : _data.sessionsStarted;

        /// <summary>The free first-session continue (GDD 14.4) was already used.</summary>
        public bool FreeContinueUsed => _data.freeContinueUsed;

        /// <summary>The first-run tutorial was finished or skipped once (GDD 12).</summary>
        public bool TutorialCompleted => _data.tutorialCompleted;

        /// <summary>Settings asked to replay the tutorial on the next run.</summary>
        public bool TutorialReplayRequested => _data.tutorialReplay;

        /// <summary>
        /// The next run starts with the tutorial: a replay was requested, or this is a brand-new player.
        /// [ASSUMED] A save that already recorded runs (from before the tutorial existed) never gets it unasked.
        /// </summary>
        public bool ShouldRunTutorial =>
            _data.tutorialReplay || (!_data.tutorialCompleted && (_data.runsPlayed == 0 || _data.tutorialStarted));

        /// <summary>The tutorial begins now (remembered so a quit halfway repeats it). Does not write.</summary>
        public void MarkTutorialStarted()
        {
            if (!_data.tutorialStarted)
            {
                _data.tutorialStarted = true;
                _dirty = true;
            }
        }

        /// <summary>The tutorial ended (finished or skipped): never shown again unless replayed. Does not write.</summary>
        public void CompleteTutorial()
        {
            if (!_data.tutorialCompleted || _data.tutorialReplay)
            {
                _data.tutorialCompleted = true;
                _data.tutorialReplay = false;
                _dirty = true;
            }
        }

        /// <summary>Settings "Replay tutorial": the next run teaches again. Does not write.</summary>
        public void RequestTutorialReplay()
        {
            if (!_data.tutorialReplay)
            {
                _data.tutorialReplay = true;
                _dirty = true;
            }
        }

        /// <summary>
        /// Counts a new app session (call once at app start) and writes the save. Returns the new session number.
        /// </summary>
        public int BeginSession()
        {
            if (_data.sessionsStarted < 0)
            {
                _data.sessionsStarted = 0;
            }

            if (_data.sessionsStarted < int.MaxValue)
            {
                _data.sessionsStarted++;
            }

            _dirty = true;
            Save();
            return _data.sessionsStarted;
        }

        /// <summary>
        /// Takes <paramref name="amount"/> coins from the wallet for a continue (GDD 14.4). Returns false (and changes
        /// nothing) if the wallet holds fewer. Does not write; call <see cref="Save"/> after.
        /// </summary>
        public bool TrySpendCoinsOnContinue(long amount)
        {
            if (amount < 0L || _data.totalCoins < amount)
            {
                return false;
            }

            _data.totalCoins -= amount;
            _data.coinsSpentOnContinues += amount;
            _dirty = true;
            return true;
        }

        /// <summary>Marks the free first-session continue as used. Does not write; call <see cref="Save"/> after.</summary>
        public void MarkFreeContinueUsed()
        {
            if (!_data.freeContinueUsed)
            {
                _data.freeContinueUsed = true;
                _dirty = true;
            }
        }

        /// <summary>The save's data, for the missions, daily reward and shop services of this assembly.</summary>
        internal SaveData Data => _data;

        /// <summary>Marks the data as changed; the services call it after every change.</summary>
        internal void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>Adds coins to the wallet (mission, set and daily rewards). Does not write.</summary>
        internal void AddCoins(long amount)
        {
            if (amount <= 0L)
            {
                return;
            }

            _data.totalCoins = _data.totalCoins > long.MaxValue - amount ? long.MaxValue : _data.totalCoins + amount;
            _dirty = true;
        }

        /// <summary>
        /// Takes <paramref name="amount"/> coins from the wallet for a shop purchase. Returns false (and changes
        /// nothing) if the wallet holds fewer. Does not write.
        /// </summary>
        internal bool TrySpendCoinsInShop(long amount)
        {
            if (amount < 0L || _data.totalCoins < amount)
            {
                return false;
            }

            _data.totalCoins -= amount;
            _data.coinsSpentInShop += amount;
            _dirty = true;
            return true;
        }

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
        /// Banks a run that is still going (app sent to the background, or coins spent on a continue): adds
        /// <paramref name="coins"/> to the wallet and raises the best score and distance. Does not count the run;
        /// <see cref="RecordRun"/> does that later. Does not write; call <see cref="Save"/> after.
        /// </summary>
        public void BankRunProgress(long score, long distanceM, int coins)
        {
            if (score > _data.bestScore)
            {
                _data.bestScore = score;
            }

            if (distanceM > _data.bestDistanceM)
            {
                _data.bestDistanceM = distanceM;
            }

            if (coins > 0)
            {
                _data.totalCoins = _data.totalCoins > long.MaxValue - coins ? long.MaxValue : _data.totalCoins + coins;
            }

            _dirty = true;
        }

        /// <summary>
        /// Records one finished (or abandoned) run: adds its coins to the wallet, updates the best score and
        /// distance, counts the run. Does not write; call <see cref="Save"/> after.
        /// <paramref name="coinsAlreadyBanked"/>: coins of this run already added by <see cref="BankRunProgress"/>
        /// (only the rest is added). <paramref name="bestScoreBeforeRun"/>: the best score before the run's first
        /// banking, for the "new best" stamp; negative = use the current best.
        /// </summary>
        public RunRecord RecordRun(long score, long distanceM, int coins, int coinsAlreadyBanked = 0, long bestScoreBeforeRun = -1L)
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

            long previousBest = bestScoreBeforeRun >= 0L ? bestScoreBeforeRun : _data.bestScore;
            if (score > _data.bestScore)
            {
                _data.bestScore = score;
            }

            if (distanceM > _data.bestDistanceM)
            {
                _data.bestDistanceM = distanceM;
            }

            int toAdd = coins - (coinsAlreadyBanked > 0 ? coinsAlreadyBanked : 0);
            if (toAdd > 0)
            {
                _data.totalCoins = _data.totalCoins > long.MaxValue - toAdd ? long.MaxValue : _data.totalCoins + toAdd;
            }

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
