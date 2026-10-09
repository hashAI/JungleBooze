namespace JungleBooze.Gameplay.Movement
{
    /// <summary>How the runner moves this tick (spec 103 §4–6). Run covers ground, air, slide and the vine release arc.</summary>
    public enum MoveMode : byte
    {
        Run = 0,

        /// <summary>In a water volume: surface swimming, dives (<see cref="DivePhase"/>) and leaps.</summary>
        Swim,

        /// <summary>Deep Breath passage along a zone's path (spec 103 §4.5): invulnerable, no steering.</summary>
        DeepDive,

        /// <summary>Hanging on a vine, scripted pendulum (spec 103 §5.3).</summary>
        Swing,
    }
}
