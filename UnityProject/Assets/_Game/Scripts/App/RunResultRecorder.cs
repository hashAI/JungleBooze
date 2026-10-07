using System;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;
using JungleBooze.Services.Meta;
using JungleBooze.Services.Persistence;

namespace JungleBooze.App
{
    /// <summary>
    /// Banks each run into the player's save exactly once: score, distance and coins (coins go into the wallet,
    /// GDD 13.1 and 14). A run is recorded when it reaches Game Over (not at the death itself, since a Continue
    /// (GDD 14.4) can bring it back) or when the player leaves it early
    /// from the pause menu (Restart or Home) [ASSUMED: coins collected before quitting are kept]. Each record is
    /// written to disk right away. <see cref="RecordIfEnded"/> is called every frame and does not allocate unless
    /// it records.
    /// A run that is still going can be banked early with <see cref="BankProgress"/> (app sent to the background,
    /// coins paid for a continue): its coins go into the wallet and the best score is raised, but the run is not
    /// counted yet. The final record adds only the coins not banked before, so nothing is ever counted twice.
    /// </summary>
    public sealed class RunResultRecorder
    {
        private int _recordedRunNumber;
        private int _bankedRunNumber;
        private int _bankedCoins;
        private bool _savePending;
        private long _bestBeforeBank;
        private MetaProgress _meta;
        private RunEventCounter _counter;

        public RunResultRecorder(PlayerSave save)
        {
            Save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public PlayerSave Save { get; }

        /// <summary>
        /// Optional: missions and the daily challenge (GDD 13). <paramref name="counter"/> supplies the slide and
        /// Shield counts. Either may be null.
        /// </summary>
        public void AttachMeta(MetaProgress meta, RunEventCounter counter)
        {
            _meta = meta;
            _counter = counter;
        }

        /// <summary>Records the current run if it has ended and was not recorded yet. Returns true if it recorded.</summary>
        public bool RecordIfEnded(GameSession session)
        {
            if (session.Phase != SessionPhase.GameOver)
            {
                return false;
            }

            // The write waits one frame (FlushPending) so the Game Over frame does not stall on the disk.
            return Record(session, true);
        }

        /// <summary>Writes a save that was held back by the last Game Over record. Call once per frame, first thing.</summary>
        public void FlushPending()
        {
            if (_savePending)
            {
                _savePending = false;
                Save.SaveIfDirty();
            }
        }

        /// <summary>
        /// Records a run the player is leaving (pause menu Restart or Home), if it had started. A run still at the
        /// Ready prompt or behind the main menu is not counted.
        /// </summary>
        public bool RecordIfLeaving(GameSession session)
        {
            SessionPhase phase = session.Phase;
            if (phase == SessionPhase.Ready || phase == SessionPhase.Menu)
            {
                return false;
            }

            return Record(session, false);
        }

        /// <summary>Coins of the current run that are not in the wallet yet (0 once the run is recorded).</summary>
        public int UnbankedCoins(GameSession session)
        {
            if (session == null || _recordedRunNumber == session.RunNumber)
            {
                return 0;
            }

            int coins = session.Coins - (_bankedRunNumber == session.RunNumber ? _bankedCoins : 0);
            return coins > 0 ? coins : 0;
        }

        /// <summary>
        /// Banks a run in progress: coins into the wallet, best score raised, written to disk. Does not count the run
        /// and may be called again; a run at the Ready prompt, behind the menu or already recorded is ignored.
        /// Returns true if it banked.
        /// </summary>
        public bool BankProgress(GameSession session)
        {
            SessionPhase phase = session.Phase;
            if (phase == SessionPhase.Ready || phase == SessionPhase.Menu || _recordedRunNumber == session.RunNumber)
            {
                return false;
            }

            if (_bankedRunNumber != session.RunNumber)
            {
                _bankedRunNumber = session.RunNumber;
                _bankedCoins = 0;
                _bestBeforeBank = Save.BestScore;
            }

            int coins = session.Coins;
            int delta = coins - _bankedCoins;
            double distance = session.DistanceM;
            Save.BankRunProgress(session.Score, distance > 0.0 ? (long)distance : 0L, delta > 0 ? delta : 0);
            if (coins > _bankedCoins)
            {
                _bankedCoins = coins;
            }

            Save.Save();
            return true;
        }

        private bool Record(GameSession session, bool deferSave)
        {
            if (_recordedRunNumber == session.RunNumber)
            {
                return false;
            }

            _recordedRunNumber = session.RunNumber;
            double distance = session.DistanceM;
            bool banked = _bankedRunNumber == session.RunNumber;
            Save.RecordRun(
                session.Score,
                distance > 0.0 ? (long)distance : 0L,
                session.Coins,
                banked ? _bankedCoins : 0,
                banked ? _bestBeforeBank : -1L);
            _meta?.ApplyRun(BuildStats(session));
            if (deferSave)
            {
                _savePending = true;
            }
            else
            {
                _savePending = false;
                Save.Save();
            }

            return true;
        }

        private RunStats BuildStats(GameSession session)
        {
            double distance = session.DistanceM;
            var stats = new RunStats
            {
                Coins = session.Coins,
                DistanceM = distance > 0.0 ? (distance >= int.MaxValue ? int.MaxValue : (int)distance) : 0,
            };

            if (session.World is TrackRunWorld world)
            {
                RunTotals totals = world.Totals;
                stats.Vines = totals.VinesGrabbed;
                stats.PerfectReleases = totals.PerfectReleases;
                stats.NearMisses = totals.NearMisses;
            }

            if (_counter != null)
            {
                stats.Slides = _counter.Slides;
                stats.Shields = _counter.Shields;
            }

            return stats;
        }
    }
}
