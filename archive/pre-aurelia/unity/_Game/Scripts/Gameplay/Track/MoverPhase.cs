namespace JungleBooze.Gameplay.Track
{
    /// <summary>Mover state (spec 002 section 5.5). Not a mover: always <see cref="Idle"/>.</summary>
    public enum MoverPhase : byte
    {
        /// <summary>Waiting in its start lane for the trigger.</summary>
        Idle = 0,

        /// <summary>Moving linearly toward its end lane.</summary>
        Moving = 1,

        /// <summary>Stopped for good on its end lane centre.</summary>
        Settled = 2,
    }
}
