namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// One playback request. <see cref="Spoken"/> uses the companion voice bus; everything else uses sound effects.
    /// Pitch and gain are multipliers (1 = the clip as recorded).
    /// </summary>
    public readonly struct AudioCue
    {
        public readonly AudioClipId Clip;
        public readonly float Pitch;
        public readonly float Gain;
        public readonly bool Spoken;

        public AudioCue(AudioClipId clip, float pitch, float gain, bool spoken)
        {
            Clip = clip;
            Pitch = pitch;
            Gain = gain;
            Spoken = spoken;
        }
    }
}
