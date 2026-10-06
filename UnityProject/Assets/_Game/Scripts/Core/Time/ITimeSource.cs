namespace JungleBooze.Core
{
    /// <summary>
    /// Simulation clock. Simulation code reads time only from here, never from UnityEngine.Time.
    /// Time advances in whole fixed steps, so the simulation result depends only on the
    /// number of steps, not on the device frame rate.
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>Number of completed simulation steps since the run started.</summary>
        long Tick { get; }

        /// <summary>Length of one simulation step in seconds. Constant for a run.</summary>
        float DeltaTime { get; }

        /// <summary>Simulated seconds since the run started (Tick * DeltaTime, no drift).</summary>
        double ElapsedSeconds { get; }
    }
}
