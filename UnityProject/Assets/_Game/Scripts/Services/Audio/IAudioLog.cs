namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Optional record of every audio decision (tools: offline mix of a bot run for the demo video; debugging).
    /// Times are the service clock (seconds). Not used in shipping builds.
    /// </summary>
    public interface IAudioLog
    {
        void Cue(double time, string id, int clip, float semitones, float gain);

        void Sting(double time, string id);

        void Ambience(double time, string channel, string id, float gain, float fade);

        /// <summary>Music actions: start (value 1 = with intro), level, stopSoft, results, underwater (value 1/0).</summary>
        void Music(double time, string action, int value);
    }
}
