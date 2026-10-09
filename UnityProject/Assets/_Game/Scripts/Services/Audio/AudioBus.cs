namespace JungleBooze.Services.Audio
{
    /// <summary>Mixer buses (AudioMixer groups Music / Ambience / SFX / UI, each with a player volume).</summary>
    public enum AudioBus : byte
    {
        Music = 0,
        Ambience,
        Sfx,
        Ui,
    }
}
