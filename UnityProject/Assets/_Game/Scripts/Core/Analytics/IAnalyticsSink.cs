namespace JungleBooze.Core.Analytics
{
    /// <summary>
    /// Where serialized analytics events go (Core, review S10). The slice writes an on-device log only; nothing is
    /// sent over the network. Lines are buffered by <see cref="Write"/> and stored once per <see cref="Commit"/>.
    /// </summary>
    public interface IAnalyticsSink
    {
        void Write(string jsonLine);

        /// <summary>Stores the lines written since the last commit (one file write per flush).</summary>
        void Commit();
    }
}
