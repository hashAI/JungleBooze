namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// Speed-source hook (spec 002 section 16.1.6): supplies the base forward speed before the run-start ramp and
    /// <see cref="RunnerSimulation.SpeedMultiplier"/> are applied. Default (none set): the <see cref="SpeedCurve"/>,
    /// or its tutorial speed while <see cref="RunnerSimulation.TutorialActive"/>. The chunk validator uses a constant
    /// source; onboarding can supply its own later. Must be deterministic and allocation-free.
    /// </summary>
    public interface ISpeedSource
    {
        /// <summary>Base speed in m/s at HERO distance <paramref name="distanceM"/> (z before this tick's move).</summary>
        double GetBaseSpeedMps(double distanceM);
    }
}
