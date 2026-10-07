using System;
using JungleBooze.Core;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Vine;

namespace JungleBooze.Gameplay.Companion
{
    /// <summary>
    /// The companion's simulation side for one run (GDD 15.1): the Assist meter, the Lift (double tap) and the three
    /// call-outs. Plain C#, deterministic: the session calls <see cref="AfterStep"/> once after every runner tick
    /// with that tick's commands; everything it reads comes from the runner, its event buffer and the track
    /// queries, and the cheer variant comes from its own forked random stream
    /// (<see cref="RandomStreamIds.Companion"/>). Results go out as runner events
    /// (<see cref="RunnerEventType.CompanionCallout"/>, <see cref="RunnerEventType.CompanionMeterFull"/>) so views
    /// read one ordered stream.
    /// <list type="bullet">
    /// <item>Meter: near-miss +5%, coin streak +5%, Good release +10%, Perfect release +25% (VineTuning). A full
    /// meter stays full until the player double taps (no auto-trigger).</item>
    /// <item>Lift: a double tap with a full meter starts the Lift after this tick (a death on the same tick wins).
    /// During a vine swing or the flight after a release, or during a speed boost, it is queued and fires on
    /// landing. The predicted touchdown stretch is cleared at the start, and again at the descent and at the
    /// touchdown, so the track is clear for 1.0 s after touchdown.</item>
    /// <item>Call-outs: "Vine!" 2.0 s before a vine section (once per section), "Look out!" 1.5 s before a mover or a
    /// signature hazard (any archetype after <see cref="ObstacleArchetype.Gap"/>), a cheer for a Perfect release and
    /// for passing the record distance. At most one call-out per <c>calloutMinGapMs</c>; a waiting call fires as soon
    /// as the gap is over while its target is still ahead. No call-outs during Lift except cheers.</item>
    /// </list>
    /// No allocation after construction.
    /// </summary>
    public sealed class CompanionSimulation
    {
        private const int VineBufferCapacity = 16;
        private const int BoxBufferCapacity = 64;

        private readonly CompanionConfig _config;
        private readonly RunnerSimulation _runner;
        private readonly IRunWorldRecovery _recovery;
        private readonly IVineTrackQuery _vineTrack;
        private readonly IRandom _random;
        private readonly float _perfectMeterPercent;
        private readonly VineAnchor[] _vines = new VineAnchor[VineBufferCapacity];
        private readonly ObstacleBox[] _boxes = new ObstacleBox[BoxBufferCapacity];

        private float _meter;
        private bool _liftQueued;
        private int _lastVineGroupCalled;
        private int _lastDangerIdCalled;
        private int _pendingDangerId;
        private byte _pendingDangerLane;
        private long _lastCalloutTick;
        private bool _recordCheered;
        private int _eventCountSeen;
        private int _overflowSeen;

        public CompanionSimulation(CompanionConfig config, RunnerSimulation runner, IRunWorldRecovery recovery, ulong runSeed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _recovery = recovery;
            _vineTrack = runner.Track as IVineTrackQuery;
            _random = new Pcg32Random(runSeed).Fork(RandomStreamIds.Companion);
            _perfectMeterPercent = runner.Vines.PerfectCompanionMeterPercent;
            _lastVineGroupCalled = -1;
            _lastCalloutTick = long.MinValue / 2;
        }

        public CompanionConfig Config => _config;

        /// <summary>Assist meter, 0..100%.</summary>
        public float MeterPercent => _meter;

        /// <summary>Meter fill 0..1 (HUD).</summary>
        public float MeterFill => _meter / CompanionConfig.MeterFullPercent;

        public bool MeterFull => _meter >= CompanionConfig.MeterFullPercent;

        /// <summary>A double tap is waiting for the swing (or boost) to end.</summary>
        public bool LiftQueued => _liftQueued;

        /// <summary>Lifts used in this run.</summary>
        public int LiftsUsed { get; private set; }

        /// <summary>Best distance before this run (m); passing it once makes the companion cheer. 0 = no record yet.</summary>
        public double RecordDistanceM { get; set; }

        /// <summary>
        /// Remembers how many events are already in the runner's buffer, so the next <see cref="AfterStep"/> reads only
        /// the events of its own tick. The session calls it right before each runner step.
        /// </summary>
        public void BeforeStep()
        {
            RunnerEventBuffer events = _runner.Events;
            _eventCountSeen = events.Count;
            _overflowSeen = events.OverflowCount;
        }

        /// <summary>Test and debug hook: sets the meter (clamped to 0..100%).</summary>
        public void SetMeterPercent(float percent)
        {
            _meter = percent < 0f ? 0f : (percent > CompanionConfig.MeterFullPercent ? CompanionConfig.MeterFullPercent : percent);
        }

        /// <summary>
        /// After the runner stepped <paramref name="tick"/> with <paramref name="commands"/>: meter gains from the
        /// tick's events, Lift clear stretches, the double tap, call-outs.
        /// </summary>
        public void AfterStep(long tick, InputCommand commands)
        {
            ReadTickEvents(tick);

            RunnerState state = _runner.Current;
            if (state.IsDead)
            {
                // GDD 15.1: a death on the same tick as the double tap wins; nothing is queued past a death.
                _liftQueued = false;
                return;
            }

            if ((commands & InputCommand.CompanionAssist) != 0 && MeterFull && !_runner.IsLifted)
            {
                _liftQueued = true;
            }

            if (_liftQueued && CanLiftNow(in state))
            {
                StartLift(state.Z);
            }

            if (!_runner.IsLifted)
            {
                UpdateCallouts(tick, in state);
            }

            if (!_recordCheered && RecordDistanceM > 0.0 && state.Z > RecordDistanceM)
            {
                _recordCheered = true;
                Cheer(tick);
            }
        }

        /// <summary>A Continue brought HERO back: drop a queued Lift.</summary>
        public void OnRevived()
        {
            _liftQueued = false;
        }

        private bool CanLiftNow(in RunnerState state)
        {
            // GDD 15.1: queued during a vine swing (and its flight) until landing; no overlap with a speed boost.
            return _runner.CanStartLift && !state.InVineFlight && _runner.SpeedMultiplier <= 1.0;
        }

        private void StartLift(double z)
        {
            LiftParameters lift = _config.ToLiftParameters();
            if (!_runner.TryStartLift(in lift))
            {
                return;
            }

            _liftQueued = false;
            _meter = 0f;
            LiftsUsed++;

            // Clear the predicted touchdown stretch now, while it is still far ahead (beyond the fog at most speeds).
            if (_recovery != null)
            {
                double speed = _recovery.SpeedAt(z) * _runner.SpeedMultiplier;
                double glideS = (lift.TotalTicks - lift.DescentTicks) * RunnerConfig.TickSeconds;
                double landS = lift.TotalTicks * RunnerConfig.TickSeconds;
                _recovery.ClearStretch(z + speed * glideS, z + speed * (landS + _config.LiftClearAfterSeconds));
            }
        }

        private void ReadTickEvents(long tick)
        {
            RunnerEventBuffer events = _runner.Events;
            int count = events.Count;
            int added = (count - _eventCountSeen) + (events.OverflowCount - _overflowSeen);
            if (added > count)
            {
                added = count;
            }

            int first = count - (added > 0 ? added : 0);
            for (int i = first; i < count; i++)
            {
                RunnerEvent e = events[i];
                switch (e.Type)
                {
                    case RunnerEventType.NearMiss:
                        AddMeter(_config.NearMissMeterPercent, tick);
                        break;

                    case RunnerEventType.CoinStreak:
                        AddMeter(_config.CoinStreakMeterPercent, tick);
                        break;

                    case RunnerEventType.VineReleased:
                        var grade = (VineReleaseGrade)e.Value;
                        if (grade == VineReleaseGrade.Good)
                        {
                            AddMeter(_config.GoodReleaseMeterPercent, tick);
                        }
                        else if (grade == VineReleaseGrade.Perfect)
                        {
                            AddMeter(_perfectMeterPercent, tick);
                            Cheer(tick);
                        }

                        break;

                    case RunnerEventType.HazardWarning:
                        // A telegraphed lane strike has no box until it strikes: call it out from its warning.
                        if (e.EntityId > _lastDangerIdCalled)
                        {
                            _pendingDangerId = e.EntityId;
                            _pendingDangerLane = e.Lane;
                        }

                        break;

                    case RunnerEventType.CompanionLiftDescending:
                        ClearAfterTouchdown(_runner.Current.Z + _runner.Current.Speed * _config.LiftDescentTicks * RunnerConfig.TickSeconds);
                        break;

                    case RunnerEventType.CompanionLiftEnded:
                        ClearAfterTouchdown(_runner.Current.Z);
                        break;
                }
            }
        }

        private void ClearAfterTouchdown(double touchdownZ)
        {
            if (_recovery == null)
            {
                return;
            }

            double speed = _recovery.SpeedAt(touchdownZ) * _runner.SpeedMultiplier;
            _recovery.ClearStretch(touchdownZ, touchdownZ + speed * _config.LiftClearAfterSeconds);
        }

        private void AddMeter(float percent, long tick)
        {
            if (percent <= 0f || MeterFull)
            {
                return;
            }

            _meter += percent;
            if (_meter >= CompanionConfig.MeterFullPercent)
            {
                _meter = CompanionConfig.MeterFullPercent;
                _runner.EmitExternal(new RunnerEvent
                {
                    Type = RunnerEventType.CompanionMeterFull,
                    Tick = tick,
                    Value = (short)CompanionConfig.MeterFullPercent,
                    Lane = (byte)_runner.Current.TargetLane,
                });
            }
        }

        private void Cheer(long tick)
        {
            // Cheers never wait for the gap (they react to the moment) but do start a new one.
            int variant = _random.NextInt(0, CompanionCalloutKeys.CheerVariantCount);
            Emit(tick, (CompanionCalloutId)((int)CompanionCalloutId.Cheer1 + variant), (byte)_runner.Current.TargetLane, 0);
        }

        private void UpdateCallouts(long tick, in RunnerState state)
        {
            if (tick - _lastCalloutTick < _config.CalloutMinGapTicks)
            {
                return;
            }

            if (_pendingDangerId != 0)
            {
                int id = _pendingDangerId;
                _pendingDangerId = 0;
                if (id > _lastDangerIdCalled)
                {
                    _lastDangerIdCalled = id;
                    Emit(tick, CompanionCalloutId.Danger, _pendingDangerLane, id);
                    return;
                }
            }

            double speed = state.Speed > 0f ? state.Speed : 0.0;
            double front = state.Z + _runner.Config.PlayerHitboxDepthM * 0.5;

            // Danger first: it is the more urgent cue.
            double hazardReach = front + speed * _config.HazardCalloutLeadSeconds;
            if (hazardReach > front)
            {
                int n = _runner.Track.GetBoxes(front, hazardReach, new Span<ObstacleBox>(_boxes));
                if (n > _boxes.Length)
                {
                    n = _boxes.Length;
                }

                for (int i = 0; i < n; i++)
                {
                    ref ObstacleBox b = ref _boxes[i];
                    if (b.Id <= _lastDangerIdCalled || b.ZMin < front || !IsDanger(b.Archetype))
                    {
                        continue;
                    }

                    _lastDangerIdCalled = b.Id;
                    Emit(tick, CompanionCalloutId.Danger, b.Lane, b.Id);
                    return;
                }
            }

            if (_vineTrack == null)
            {
                return;
            }

            double vineReach = state.Z + speed * _config.VineCalloutLeadSeconds;
            if (vineReach <= state.Z)
            {
                return;
            }

            int count = _vineTrack.GetVines(state.Z, vineReach, new Span<VineAnchor>(_vines));
            if (count > _vines.Length)
            {
                count = _vines.Length;
            }

            for (int i = 0; i < count; i++)
            {
                VineAnchor v = _vines[i];
                if (v.Group <= _lastVineGroupCalled)
                {
                    continue;
                }

                // Vines are sorted by z, so the first one of a new section is its first row.
                _lastVineGroupCalled = v.Group;
                Emit(tick, CompanionCalloutId.Vine, (byte)v.Lane, v.Id);
                return;
            }
        }

        private static bool IsDanger(ObstacleArchetype archetype)
        {
            return archetype == ObstacleArchetype.Mover || (byte)archetype > (byte)ObstacleArchetype.Gap;
        }

        private void Emit(long tick, CompanionCalloutId id, byte lane, int entityId)
        {
            _lastCalloutTick = tick;
            _runner.EmitExternal(new RunnerEvent
            {
                Type = RunnerEventType.CompanionCallout,
                Tick = tick,
                Lane = lane,
                Value = (short)id,
                EntityId = entityId,
            });
        }
    }
}
