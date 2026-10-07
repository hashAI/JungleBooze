namespace JungleBooze.Gameplay.Runner
{
    /// <summary>
    /// The points inside <see cref="RunnerSimulation.Step(JungleBooze.Core.InputCommand)"/> where the track, coin and
    /// score systems run (spec 002 section 13.3, amending spec 001 rule I8). The run's composition root passes one
    /// implementation to <see cref="RunnerSimulation.StepHooks"/>; with none set the runner behaves exactly as in
    /// spec 001. Implementations must be deterministic and allocation-free. They may write events with
    /// <see cref="RunnerSimulation.EmitExternal"/>; they must never change runner state.
    /// <para>Full step order: (1)–(7) spec 001, <b>(7a) OnTrackUpdate</b>, (8)–(10) spec 001, (11) collisions
    /// against <see cref="ITrackQuery.GetBoxes"/>, <b>(11a) OnCoinPickups</b> (skipped if HERO died on this tick),
    /// <b>(11b) OnScore</b>, (12) events and snapshot. No hook runs on ticks that start with HERO already dead.</para>
    /// </summary>
    public interface IRunnerStepHooks
    {
        /// <summary>
        /// Step 7a, after speed and z (step 7): generate ahead, despawn behind, mover triggers and motion,
        /// <c>ChunkEntered</c>/<c>TierChanged</c>. Boxes reported by <see cref="ITrackQuery.GetBoxes"/> from now on
        /// must reflect this update.
        /// </summary>
        void OnTrackUpdate(RunnerSimulation runner, in RunnerTickInfo info);

        /// <summary>Step 11a, after collisions: coin pickups and coin streak. Not called if HERO died this tick.</summary>
        void OnCoinPickups(RunnerSimulation runner, in RunnerTickInfo info);

        /// <summary>Step 11b: distance score and bonuses (this tick's near-misses, streaks). Called on the death tick too.</summary>
        void OnScore(RunnerSimulation runner, in RunnerTickInfo info);
    }
}
