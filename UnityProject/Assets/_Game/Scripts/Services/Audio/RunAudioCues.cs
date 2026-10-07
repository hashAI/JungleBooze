namespace JungleBooze.Services.Audio
{
    /// <summary>
    /// Maps a runner event onto a clip. Event numbers match <c>JungleBooze.Gameplay.Runner.RunnerEventType</c>
    /// and call-out numbers match <c>CompanionCalloutId</c>. This assembly does not reference Gameplay, so the
    /// coordinator passes the event fields through <see cref="TryGet"/>. No allocations.
    /// </summary>
    public static class RunAudioCues
    {
        public const byte JumpStarted = 6;
        public const byte SlideStarted = 10;
        public const byte Stumbled = 13;
        public const byte NearMiss = 15;
        public const byte Died = 16;
        public const byte LaneChangeStarted = 3;
        public const byte CoinCollected = 21;
        public const byte VineGrabbed = 24;
        public const byte PowerUpCollected = 40;
        public const byte ShieldAbsorbed = 43;
        public const byte CompanionCallout = 63;

        public const short CallVine = 1;
        public const short CallDanger = 2;
        public const short CallCheer1 = 3;
        public const short CallCheer2 = 4;
        public const short CallCheer3 = 5;

        public const short PowerMagnet = 1;
        public const short PowerShield = 2;
        public const short PowerSpeedBoost = 3;

        /// <summary>
        /// Resolves a clip. <paramref name="voiceVolume"/> 0 plays the squawk fallback for call-outs (GDD 15.1)
        /// and suppresses spoken cheers the same way. Coin pitch and gain vary with <paramref name="entityId"/>
        /// so a streak does not sound like one sample.
        /// </summary>
        public static bool TryGet(byte eventType, short value, byte flags, int entityId, float voiceVolume, out AudioCue cue)
        {
            // flags is part of the runner event. Shield pop is keyed off ShieldAbsorbed, so the used-up flag is unused.
            _ = flags;

            switch (eventType)
            {
                case JumpStarted:
                    cue = new AudioCue(AudioClipId.Jump, 1f, 1f, false);
                    return true;
                case SlideStarted:
                    cue = new AudioCue(AudioClipId.Slide, 1f, 1f, false);
                    return true;
                case LaneChangeStarted:
                    cue = new AudioCue(AudioClipId.LaneSwitch, 1f, 0.9f, false);
                    return true;
                case CoinCollected:
                    cue = new AudioCue(AudioClipId.Coin, CoinPitch(entityId), CoinGain(entityId), false);
                    return true;
                case Stumble:
                    cue = new AudioCue(AudioClipId.Stumble, 1f, 1f, false);
                    return true;
                case NearMiss:
                    cue = new AudioCue(AudioClipId.NearMiss, 1f, 0.85f, false);
                    return true;
                case Died:
                    cue = new AudioCue(AudioClipId.Death, 1f, 1f, false);
                    return true;
                case VineGrabbed:
                    cue = new AudioCue(AudioClipId.VineGrab, 1f, 1f, false);
                    return true;
                case PowerUpCollected:
                    cue = new AudioCue(PowerUpClip(value), 1f, 1f, false);
                    return true;
                case ShieldAbsorbed:
                    cue = new AudioCue(AudioClipId.ShieldPop, 1f, 1f, false);
                    return true;
                case CompanionCallout:
                    return TryCallout(value, voiceVolume, out cue);
                default:
                    cue = default;
                    return false;
            }
        }

        /// <summary>Ambient chatter clips (GDD 15.1). Index wraps. Not a runner event.</summary>
        public static AudioClipId ChatterClip(int index)
        {
            int n = index % 3;
            if (n < 0)
            {
                n += 3;
            }

            switch (n)
            {
                case 0:
                    return AudioClipId.ChatterPretty;
                case 1:
                    return AudioClipId.ChatterMine;
                default:
                    return AudioClipId.ChatterGiggle;
            }
        }

        /// <summary>Five coin pitches from the coin id, about 0.92 to 1.13.</summary>
        public static float CoinPitch(int entityId)
        {
            int n = entityId & 7;
            if (n > 4)
            {
                n -= 5;
            }

            return 0.92f + (n * 0.052f);
        }

        public static float CoinGain(int entityId)
        {
            int n = (entityId >> 3) & 3;
            return 0.82f + (n * 0.06f);
        }

        private static AudioClipId PowerUpClip(short powerUpType)
        {
            if (powerUpType == PowerSpeedBoost)
            {
                return AudioClipId.SpeedBoost;
            }

            return AudioClipId.PowerUp;
        }

        private static bool TryCallout(short calloutId, float voiceVolume, out AudioCue cue)
        {
            bool spoken = voiceVolume > 0f;
            switch (calloutId)
            {
                case CallVine:
                    cue = new AudioCue(spoken ? AudioClipId.CallVine : AudioClipId.SquawkVine, 1f, 1f, spoken);
                    return true;
                case CallDanger:
                    cue = new AudioCue(spoken ? AudioClipId.CallDanger : AudioClipId.SquawkDanger, 1f, 1f, spoken);
                    return true;
                case CallCheer1:
                    cue = new AudioCue(spoken ? AudioClipId.CallCheerShiny : AudioClipId.SquawkCheer, 1f, 1f, spoken);
                    return true;
                case CallCheer2:
                    cue = new AudioCue(spoken ? AudioClipId.CallCheerWow : AudioClipId.SquawkCheer, 1.04f, 1f, spoken);
                    return true;
                case CallCheer3:
                    cue = new AudioCue(spoken ? AudioClipId.CallCheerWoohoo : AudioClipId.SquawkCheer, 0.96f, 1f, spoken);
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }
    }
}
