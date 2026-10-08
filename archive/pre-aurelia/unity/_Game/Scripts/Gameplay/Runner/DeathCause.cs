namespace JungleBooze.Gameplay.Runner
{
    /// <summary>Payload of <see cref="RunnerEventType.Died"/> (stored in <see cref="RunnerEvent.Value"/>).</summary>
    public enum DeathCause : byte
    {
        None = 0,

        /// <summary>Hit an obstacle (collision stage).</summary>
        Hit = 1,

        /// <summary>Fell below the track surface past <see cref="RunnerConfig.FallDeathDepthM"/>.</summary>
        Fell = 2,

        /// <summary>Fell into the chasm under a vine HERO did not swing on (GDD 7.4).</summary>
        MissedVine = 3,
    }
}
