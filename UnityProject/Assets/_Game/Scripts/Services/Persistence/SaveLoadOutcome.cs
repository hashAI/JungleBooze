namespace JungleBooze.Services.Persistence
{
    /// <summary>How <see cref="PlayerSave.Load"/> got its data.</summary>
    public enum SaveLoadOutcome : byte
    {
        /// <summary>No save yet (first launch): defaults.</summary>
        NoSave = 0,

        /// <summary>The primary save was read.</summary>
        Loaded = 1,

        /// <summary>The primary save was missing or broken; the backup was read.</summary>
        RestoredFromBackup = 2,

        /// <summary>The save and its backup were unreadable: defaults (the broken file is overwritten on the next save).</summary>
        CorruptReset = 3,
    }
}
