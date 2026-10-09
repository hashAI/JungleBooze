using System;
using JungleBooze.Core.Analytics;
using JungleBooze.Core.Save;
using JungleBooze.Gameplay.Analytics;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;

namespace JungleBooze.Gameplay.Expedition
{
    /// <summary>
    /// The economy and save flow around expedition runs, in plain C# so it is EditMode-testable (review S3, S10):
    /// run setup from the profile, banking a finished run, LEARN, the revive offer and payment, analytics, and the save
    /// policy. A failing save never blocks the results or a purchase: the profile in memory stays consistent (a LEARN
    /// deducts the cost and grants the ability together, or does neither) and the save is retried at the next safe
    /// point. The scene's MonoBehaviour only drives presentation. Allocates at run start/end only.
    /// </summary>
    public sealed class ExpeditionFlow
    {
        private readonly ExpeditionContent _content;
        private readonly ExpeditionSession _session;
        private readonly IProfileStore _store;
        private readonly Func<string, bool> _discovered;
        private bool _banked = true;

        public ExpeditionFlow(ExpeditionContent content, ExpeditionSession session, IProfileStore store, SaveData profile, AnalyticsRecorder analytics, IAnalyticsSink sink)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Analytics = analytics;
            Sink = sink;
            _discovered = id => Profile.IsDiscovered(id);
        }

        public SaveData Profile { get; }

        public AnalyticsRecorder Analytics { get; }

        public IAnalyticsSink Sink { get; }

        public IProfileStore Store => _store;

        /// <summary>Results of the last banked run (null while a run is in progress).</summary>
        public RunResults Results { get; private set; }

        /// <summary>False if the last save attempt did not reach storage (read-only or failed; see the store).</summary>
        public bool LastSaveOk { get; private set; } = true;

        /// <summary>The next run from the profile: Expedition 1 until a run was banked, then directed (spec 102 §6.1).</summary>
        public ExpeditionRunSetup NextRunSetup()
        {
            return new ExpeditionRunSetup
            {
                FirstExpedition = Profile.runsCompleted == 0,
                Seed = ExpeditionRunSetup.RunSeed(Profile.worldSeed, Profile.runsCompleted),
                Owned = (AbilityFlags)Profile.abilities,
                PendingShowcase = (AbilityFlags)Profile.pendingShowcase,
                Skill = Profile.skill,
                Discovered = _discovered,
            };
        }

        /// <summary>Begins a run; a run left unfinished (restart) is closed in analytics first (review N3).</summary>
        public void BeginRun(in ExpeditionRunSetup setup, bool landscape)
        {
            if (!_banked && Analytics != null && Analytics.RunOpen)
            {
                Analytics.RecordRunAbandoned(_session.Stats, _session.Run.RunSeconds);
                Analytics.Flush(Sink);
            }

            _session.BeginRun(setup);
            _banked = false;
            Results = null;
            Analytics?.BeginRun(Profile.runsCompleted, (int)setup.Owned, setup.Skill, landscape);
        }

        /// <summary>Banks the run, saves (never throws), records analytics. Call once when the session reaches Results.</summary>
        public RunResults FinishRun()
        {
            if (_banked && Results != null)
            {
                return Results;
            }

            RunStats stats = _session.Stats;
            Results = ProgressionRules.ApplyRun(Profile, stats, _content, _session.FirstExpedition, stats.ShowcaseReached);
            _banked = true;
            LastSaveOk = SafeSave();
            if (Analytics != null)
            {
                Analytics.RecordRunEnded(stats, _session.Run.RunSeconds, stats.Revives);
                Analytics.Flush(Sink);
            }

            return Results;
        }

        /// <summary>
        /// LEARN (AC-103-47): deducts the cost and grants the ability in one step, then saves. A failed save keeps
        /// both in memory and retries later; it never leaves coins deducted without the unlock.
        /// </summary>
        public bool Learn(AbilityDefinition ability)
        {
            if (ability == null || !ProgressionRules.TryLearn(Profile, ability))
            {
                return false;
            }

            LastSaveOk = SafeSave();
            if (Analytics != null)
            {
                Analytics.RecordAbilityUnlocked(ability, Profile.runsCompleted);
                Analytics.Flush(Sink);
            }

            return true;
        }

        /// <summary>The revive offer may show now (run 2+, under the limit, affordable, dying).</summary>
        public bool CanOfferRevive()
        {
            ResultsConfig cfg = _content.Results;
            RunStats stats = _session.Stats;
            return _session.Phase == RunPhase.Dying && ReviveRules.CanOffer(cfg, _session.FirstExpedition, stats) && ReviveRules.CanAfford(cfg, Profile, stats);
        }

        public void RecordReviveOffered(long timeMs)
        {
            RunStats stats = _session.Stats;
            Analytics?.RecordRevive(false, ReviveRules.Cost(_content.Results, stats.Revives), stats.Revives, timeMs);
        }

        /// <summary>Pays and revives (recorded in the session's replay). False when not allowed.</summary>
        public bool AcceptRevive(long timeMs)
        {
            ResultsConfig cfg = _content.Results;
            int index = _session.Stats.Revives;
            int cost = ReviveRules.Cost(cfg, index);
            if (!ReviveRules.TryRevive(_session, cfg, Profile))
            {
                return false;
            }

            Analytics?.RecordRevive(true, cost, index, timeMs);
            return true;
        }

        /// <summary>A safe point (run again, app pause, results visible): retries a pending save.</summary>
        public bool SafePoint(bool force)
        {
            bool ok;
            try
            {
                ok = _store.RetryPending(Profile, force);
            }
            catch (Exception)
            {
                ok = false;
            }

            if (ok)
            {
                LastSaveOk = true;
            }

            return ok;
        }

        private bool SafeSave()
        {
            try
            {
                return _store.Save(Profile);
            }
            catch (Exception)
            {
                // The store contract is not to throw; a broken implementation still must not block the results.
                return false;
            }
        }
    }
}
