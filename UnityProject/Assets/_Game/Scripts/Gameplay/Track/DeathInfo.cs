using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Track
{
    /// <summary>
    /// How the run ended (spec 002 sections 12.4 and 13.2). Recorded on the death tick from the runner's state;
    /// <see cref="WorldSkinConfig.GetCauseText(in DeathInfo)"/> turns it into the Game Over cause text.
    /// </summary>
    public struct DeathInfo
    {
        /// <summary>False while HERO is alive.</summary>
        public bool HasDied;

        public DeathCause Cause;

        public ObstacleArchetype Archetype;

        /// <summary>Obstacle id that killed HERO; 0 for a fall.</summary>
        public int ObstacleId;

        /// <summary>Second stumble while dazed ("Tripped twice").</summary>
        public bool AfterStumble;

        /// <summary>Library index of the chunk HERO died in (-1 if unknown).</summary>
        public int ChunkIndex;

        /// <summary>Id of that chunk (null if unknown). Not allocated: points at the library's string.</summary>
        public string ChunkId;

        public double DistanceM;

        public long Tick;
    }
}
