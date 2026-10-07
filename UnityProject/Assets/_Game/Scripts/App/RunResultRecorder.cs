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
    /// </summary>
    public sealed class RunResultRecorder
    {
        private int _recordedRunNumber;
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

            return Record(session);
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

            return Record(session);
        }

        private bool Record(GameSession session)
        {
            if (_recordedRunNumber == session.RunNumber)
            {
                return false;
            }

            _recordedRunNumber = session.RunNumber;
            double distance = session.DistanceM;
            Save.RecordRun(session.Score, distance > 0.0 ? (long)distance : 0L, session.Coins);
            _meta?.ApplyRun(BuildStats(session));
            Save.Save();
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
