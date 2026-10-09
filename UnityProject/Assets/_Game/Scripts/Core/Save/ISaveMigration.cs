namespace JungleBooze.Core.Save
{
    /// <summary>One save-format step (ARCHITECTURE §9): upgrades data read as version <see cref="From"/> to From + 1.</summary>
    public interface ISaveMigration
    {
        int From { get; }

        void Apply(SaveData data);
    }
}
