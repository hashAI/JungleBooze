namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>Receives "observed long enough" from <see cref="SailbackSystem"/> (the run tracker turns it into a discovery or a sighting).</summary>
    public interface ICreatureDiscoverySink
    {
        void OnCreatureObserved(string entryId, int chunkSerial, long tick);
    }
}
