namespace JungleBooze.Core.Save
{
    /// <summary>
    /// Version 1 was the FP1 lane runner's save (score, lane coins, continues). AURELIA is a different game, so its
    /// progress is not carried over: the AURELIA fields start fresh (the lane fields are ignored by the reader)
    /// [ASSUMED 2026-10-09; the owner was the only FP1 player].
    /// </summary>
    public sealed class MigrateV1LaneGameToV2 : ISaveMigration
    {
        public int From => 1;

        public void Apply(SaveData data)
        {
            data.coins = 0;
            data.crystals = 0;
            data.bestDistance = 0f;
            data.totalDistance = 0;
            data.runsCompleted = 0;
            data.abilities = 0;
            data.pendingShowcase = 0;
            data.journal.Clear();
        }
    }
}
