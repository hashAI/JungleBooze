using JungleBooze.Gameplay.Runner;

namespace JungleBooze.Gameplay.Session
{
    /// <summary>
    /// Optional part of an <see cref="IRunWorld"/> that can bring HERO back after a death (Continue, GDD 14.4) and
    /// clear a stretch of track (Continue and the companion's Lift touchdown, GDD 15.1). Deterministic; called
    /// between simulation ticks, never inside a step.
    /// </summary>
    public interface IRunWorldRecovery
    {
        /// <summary>
        /// Revives a dead <paramref name="runner"/> at a safe spot: where HERO died if there is ground, otherwise
        /// on the first ground ahead (after a vine-chasm death: the landing pad after the section's chasm vines).
        /// Removes the obstacle that killed HERO and clears <paramref name="clearSeconds"/> of track ahead at run
        /// speed, then grants <paramref name="invulnerableTicks"/>. Returns false if the runner is not dead.
        /// </summary>
        bool Revive(RunnerSimulation runner, int invulnerableTicks, double clearSeconds);

        /// <summary>Removes every obstacle and gap overlapping [<paramref name="fromZ"/>, <paramref name="toZ"/>]. Returns how many.</summary>
        int ClearStretch(double fromZ, double toZ);

        /// <summary>Base run speed at <paramref name="z"/> (m/s), for turning seconds into a stretch length.</summary>
        double SpeedAt(double z);
    }
}
