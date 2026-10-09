namespace JungleBooze.Gameplay.Analytics
{
    /// <summary>Where serialized events go. The slice writes an on-device log only; nothing is sent over the network.</summary>
    public interface IAnalyticsSink
    {
        void Write(string jsonLine);
    }
}
