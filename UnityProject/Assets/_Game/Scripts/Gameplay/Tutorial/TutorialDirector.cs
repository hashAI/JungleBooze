using System;
using JungleBooze.Gameplay.Companion;
using JungleBooze.Gameplay.Runner;
using JungleBooze.Gameplay.Session;
using JungleBooze.Gameplay.Track;

namespace JungleBooze.Gameplay.Tutorial
{
    /// <summary>
    /// The first-run tutorial (GDD 12). It does not change the track: it watches the real tier-1 run and teaches each
    /// thing the first time it comes up (a lane obstacle: swipe sideways; a low log or gap: swipe up; a low branch:
    /// swipe down; a coin trail; the first vine; the Assist), slowing the game to 30% until the player acts. A death
    /// is undone at once (<see cref="GameSession.RescueInTutorial"/>) with a gentle hint. Runs at the tutorial speed
    /// until it ends with "You're on your own!". Skippable once it has been completed before (replay from Settings).
    /// Plain C#: the run driver applies <see cref="TimeScale"/> and calls <see cref="TryTakeRescue"/>; the UI reads
    /// <see cref="Hint"/> and <see cref="Gesture"/>. No allocations per frame.
    /// </summary>
    public sealed class TutorialDirector : IRunView
    {
        private RunnerSimulation _runner;
        private long _beginTick;
        private long _nextLessonTick;
        private long _lessonStartTick;
        private long _hintEndTick;
        private long _actionTick;
        private int _taught;
        private double _lessonBackZ;
        private double _lessonVineZ;
        private bool _actionSeen;
        private bool _doneSeen;
        private bool _vineGrabbed;
        private bool _rescuePending;
        private DeathCause _rescueCause;
        private ObstacleArchetype _rescueArchetype;

        /// <summary>Raised once when the tutorial ends by finishing or by Skip (not when a run is abandoned).</summary>
        public event Action Finished;

        /// <summary>The tutorial is running in the current run.</summary>
        public bool Active { get; private set; }

        /// <summary>The player may skip it (it was completed before).</summary>
        public bool Skippable { get; private set; }

        /// <summary>Factor for the real frame time given to the session (1 = normal, 0.3 while the player must act).</summary>
        public double TimeScale { get; private set; } = 1.0;

        public TutorialHint Hint { get; private set; }

        /// <summary>The ghost-hand gesture that goes with <see cref="Hint"/>.</summary>
        public TutorialGesture Gesture
        {
            get
            {
                switch (Hint)
                {
                    case TutorialHint.Lateral:
                    case TutorialHint.RescueLateral:
                        return TutorialGesture.SwipeSides;
                    case TutorialHint.Jump:
                    case TutorialHint.RescueJump:
                    case TutorialHint.VineGrab:
                    case TutorialHint.VineRelease:
                    case TutorialHint.RescueVine:
                        return TutorialGesture.SwipeUp;
                    case TutorialHint.Slide:
                    case TutorialHint.RescueSlide:
                        return TutorialGesture.SwipeDown;
                    case TutorialHint.Assist:
                        return TutorialGesture.DoubleTap;
                    default:
                        return TutorialGesture.None;
                }
            }
        }

        /// <summary>Starts the tutorial in the run that is about to move. <paramref name="skippable"/>: show Skip.</summary>
        public void Begin(GameSession session, bool skippable)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            ResetState();
            _runner = session.Runner;
            _runner.TutorialActive = true;
            _beginTick = _runner.NextTick;
            _nextLessonTick = _beginTick;
            Active = true;
            Skippable = skippable;
        }

        /// <summary>Ends the tutorial because the run was left or restarted. No <see cref="Finished"/>.</summary>
        public void Cancel()
        {
            if (_runner != null)
            {
                _runner.TutorialActive = false;
            }

            ResetState();
        }

        /// <summary>Skip button: ends the tutorial as completed. Ignored unless <see cref="Skippable"/>.</summary>
        public void Skip()
        {
            if (Active && Skippable)
            {
                Finish();
            }
        }

        /// <summary>True once after a death that should be undone; the driver then calls the session's rescue.</summary>
        public bool TryTakeRescue()
        {
            if (!Active || !_rescuePending)
            {
                return false;
            }

            _rescuePending = false;
            return true;
        }

        /// <summary>The driver undid the death: show the gentle hint for what killed HERO and go on.</summary>
        public void OnRescued()
        {
            if (!Active)
            {
                return;
            }

            TutorialHint hint = TutorialHint.RescueLateral;
            if (_rescueCause == DeathCause.Fell)
            {
                hint = TutorialHint.RescueJump;
            }
            else if (_rescueCause == DeathCause.MissedVine)
            {
                hint = TutorialHint.RescueVine;
            }
            else if (_rescueArchetype == ObstacleArchetype.LowBarrier)
            {
                hint = TutorialHint.RescueJump;
            }
            else if (_rescueArchetype == ObstacleArchetype.HighBarrier)
            {
                hint = TutorialHint.RescueSlide;
            }

            long tick = _runner.NextTick;
            Hint = hint;
            TimeScale = 1.0;
            _hintEndTick = tick + Ticks(TutorialDesignValues.RescueHintSeconds);
            _nextLessonTick = _hintEndTick + Ticks(TutorialDesignValues.BetweenLessonsSeconds);
            _actionSeen = false;
            _doneSeen = false;
        }

        public void BeginRun(GameSession session)
        {
            // A new run (restart, home) ends a running tutorial without completing it; the driver may Begin again.
            Cancel();
        }

        public void OnRunnerEvent(in RunnerEvent e)
        {
            if (!Active)
            {
                return;
            }

            switch (e.Type)
            {
                case RunnerEventType.Died:
                    _rescuePending = true;
                    _rescueCause = (DeathCause)e.Value;
                    _rescueArchetype = (ObstacleArchetype)e.Archetype;
                    break;

                case RunnerEventType.LaneChangeStarted:
                    if (Hint == TutorialHint.Lateral && !_actionSeen)
                    {
                        _actionSeen = true;
                        _actionTick = e.Tick;
                    }

                    break;

                case RunnerEventType.JumpStarted:
                    if (Hint == TutorialHint.Jump)
                    {
                        _actionSeen = true;
                    }

                    break;

                case RunnerEventType.Landed:
                    if (Hint == TutorialHint.Jump && _actionSeen)
                    {
                        _doneSeen = true;
                    }

                    break;

                case RunnerEventType.SlideStarted:
                    if (Hint == TutorialHint.Slide)
                    {
                        _actionSeen = true;
                    }

                    break;

                case RunnerEventType.SlideEnded:
                    if (Hint == TutorialHint.Slide && _actionSeen)
                    {
                        _doneSeen = true;
                    }

                    break;

                case RunnerEventType.VineGrabbed:
                    if (Hint == TutorialHint.VineGrab)
                    {
                        _vineGrabbed = true;
                    }

                    break;

                case RunnerEventType.VineReleased:
                case RunnerEventType.VineMissed:
                    if (Hint == TutorialHint.VineGrab || Hint == TutorialHint.VineRelease)
                    {
                        _doneSeen = true;
                    }

                    break;

                case RunnerEventType.CompanionLiftEnded:
                    if (Hint == TutorialHint.Assist)
                    {
                        _doneSeen = true;
                    }

                    break;
            }
        }

        public void Render(GameSession session, float alpha, float realDeltaSeconds)
        {
            if (!Active)
            {
                TimeScale = 1.0;
                return;
            }

            if (session.Phase != SessionPhase.Running)
            {
                return;
            }

            RunnerSimulation runner = session.Runner;
            long tick = runner.NextTick;
            double heroZ = runner.Current.Z;
            double elapsed = (tick - _beginTick) * RunnerConfig.TickSeconds;
            TrackSimulation track = (session.World as TrackRunWorld)?.Track;

            switch (Hint)
            {
                case TutorialHint.None:
                    TimeScale = 1.0;
                    if (tick >= _nextLessonTick)
                    {
                        TryStartLesson(session, runner, track, tick, heroZ, elapsed);
                    }

                    break;

                case TutorialHint.Lateral:
                    TimeScale = TutorialDesignValues.WaitTimeScale;
                    if ((_actionSeen && tick >= _actionTick + Ticks(TutorialDesignValues.LateralHoldSeconds)) || LessonOver(tick, heroZ))
                    {
                        EndLesson(tick);
                    }

                    break;

                case TutorialHint.Jump:
                case TutorialHint.Slide:
                    TimeScale = TutorialDesignValues.WaitTimeScale;
                    if (_doneSeen || LessonOver(tick, heroZ))
                    {
                        EndLesson(tick);
                    }

                    break;

                case TutorialHint.Coins:
                    TimeScale = 1.0;
                    if (tick >= _hintEndTick)
                    {
                        EndLesson(tick);
                    }

                    break;

                case TutorialHint.VineGrab:
                case TutorialHint.VineRelease:
                    TimeScale = 1.0;
                    if (_vineGrabbed)
                    {
                        Hint = TutorialHint.VineRelease;
                    }

                    if (_doneSeen || heroZ > _lessonVineZ + TutorialDesignValues.VineGiveUpPastM
                        || tick - _lessonStartTick > Ticks(TutorialDesignValues.MaxLessonSeconds * 3.0))
                    {
                        EndLesson(tick);
                    }

                    break;

                case TutorialHint.Assist:
                    TimeScale = 1.0;
                    if (_doneSeen || tick >= _hintEndTick)
                    {
                        EndLesson(tick);
                    }

                    break;

                case TutorialHint.Outro:
                    TimeScale = 1.0;
                    if (tick >= _hintEndTick)
                    {
                        Finish();
                    }

                    break;

                default:
                    // Rescue hints.
                    TimeScale = 1.0;
                    if (tick >= _hintEndTick)
                    {
                        Hint = TutorialHint.None;
                    }

                    break;
            }
        }

        private static long Ticks(double seconds)
        {
            return (long)(seconds * RunnerConfig.TicksPerSecond);
        }

        private static int Bit(TutorialHint hint)
        {
            return 1 << (hint == TutorialHint.VineRelease ? (int)TutorialHint.VineGrab : (int)hint);
        }

        private bool Taught(TutorialHint hint)
        {
            return (_taught & Bit(hint)) != 0;
        }

        private bool LessonOver(long tick, double heroZ)
        {
            return heroZ > _lessonBackZ + TutorialDesignValues.PassedMarginM
                || tick - _lessonStartTick > Ticks(TutorialDesignValues.MaxLessonSeconds);
        }

        private static TutorialHint HintFor(ObstacleArchetype archetype)
        {
            switch (archetype)
            {
                case ObstacleArchetype.LowBarrier:
                case ObstacleArchetype.Gap:
                    return TutorialHint.Jump;
                case ObstacleArchetype.HighBarrier:
                    return TutorialHint.Slide;
                case ObstacleArchetype.FullBlock:
                case ObstacleArchetype.Mover:
                case ObstacleArchetype.LaneDenial:
                    return TutorialHint.Lateral;
                default:
                    return TutorialHint.None;
            }
        }

        private static double TriggerDistance(TutorialHint hint)
        {
            switch (hint)
            {
                case TutorialHint.Jump:
                    return TutorialDesignValues.JumpTriggerM;
                case TutorialHint.Slide:
                    return TutorialDesignValues.SlideTriggerM;
                default:
                    return TutorialDesignValues.LateralTriggerM;
            }
        }

        private void TryStartLesson(GameSession session, RunnerSimulation runner, TrackSimulation track, long tick, double heroZ, double elapsed)
        {
            if (track != null)
            {
                int lane = runner.Current.TargetLane;
                double bestDistance = double.MaxValue;
                TutorialHint bestHint = TutorialHint.None;
                double bestBackZ = 0.0;
                int obstacles = track.ObstacleCount;
                for (int i = 0; i < obstacles; i++)
                {
                    ref readonly ObstacleInstance o = ref track.GetObstacle(i);
                    double distance = o.Z - heroZ;
                    if (distance < 0.0 || distance >= bestDistance)
                    {
                        continue;
                    }

                    TutorialHint hint = HintFor(o.Archetype);
                    if (hint == TutorialHint.None || Taught(hint) || distance > TriggerDistance(hint))
                    {
                        continue;
                    }

                    bool inLane = (o.Archetype == ObstacleArchetype.Gap && o.LaneMask == 0) || LaneMasks.Contains(o.LaneMask, lane);
                    if (!inLane)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestHint = hint;
                    bestBackZ = o.BackZ;
                }

                if (bestHint != TutorialHint.None)
                {
                    StartLesson(bestHint, tick);
                    _lessonBackZ = bestBackZ;
                    return;
                }

                if (!Taught(TutorialHint.Coins) && CoinTrailAhead(track, heroZ))
                {
                    StartLesson(TutorialHint.Coins, tick);
                    _hintEndTick = tick + Ticks(TutorialDesignValues.CoinsHintSeconds);
                    return;
                }

                if (!Taught(TutorialHint.VineGrab))
                {
                    int vines = track.VineCount;
                    for (int i = 0; i < vines; i++)
                    {
                        ref readonly VineInstance v = ref track.GetVine(i);
                        double distance = v.Z - heroZ;
                        if (distance > 0.0 && distance <= TutorialDesignValues.VineTriggerM)
                        {
                            StartLesson(TutorialHint.VineGrab, tick);
                            _lessonVineZ = v.Z;
                            return;
                        }
                    }
                }
            }

            if (!Taught(TutorialHint.Assist) && elapsed >= TutorialDesignValues.AssistAfterSeconds)
            {
                CompanionSimulation companion = session.Companion;
                if (companion == null)
                {
                    _taught |= Bit(TutorialHint.Assist);
                }
                else
                {
                    // GDD 12 (40 s): the meter is shown filled so the player can try the Assist right away.
                    companion.SetMeterPercent(CompanionConfig.MeterFullPercent);
                    StartLesson(TutorialHint.Assist, tick);
                    _hintEndTick = tick + Ticks(TutorialDesignValues.AssistHintSeconds);
                }

                return;
            }

            if (elapsed >= TutorialDesignValues.MaxSeconds)
            {
                StartOutro(tick);
            }
        }

        private static bool CoinTrailAhead(TrackSimulation track, double heroZ)
        {
            int coins = track.CoinCount;
            for (int i = 0; i < coins; i++)
            {
                ref readonly CoinInstance c = ref track.GetCoin(i);
                double distance = c.Z - heroZ;
                if (!c.Collected && distance >= TutorialDesignValues.CoinsMinAheadM && distance <= TutorialDesignValues.CoinsMaxAheadM)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartLesson(TutorialHint hint, long tick)
        {
            Hint = hint;
            _lessonStartTick = tick;
            _actionSeen = false;
            _doneSeen = false;
            _vineGrabbed = false;
        }

        private void EndLesson(long tick)
        {
            bool wasAssist = Hint == TutorialHint.Assist;
            _taught |= Bit(Hint);
            Hint = TutorialHint.None;
            TimeScale = 1.0;
            _nextLessonTick = tick + Ticks(TutorialDesignValues.BetweenLessonsSeconds);
            if (wasAssist)
            {
                // GDD 12 (45 s): the Assist is the last lesson.
                StartOutro(tick);
            }
        }

        private void StartOutro(long tick)
        {
            Hint = TutorialHint.Outro;
            TimeScale = 1.0;
            _hintEndTick = tick + Ticks(TutorialDesignValues.OutroSeconds);
        }

        private void Finish()
        {
            Cancel();
            Finished?.Invoke();
        }

        private void ResetState()
        {
            Active = false;
            Skippable = false;
            TimeScale = 1.0;
            Hint = TutorialHint.None;
            _taught = 0;
            _actionSeen = false;
            _doneSeen = false;
            _vineGrabbed = false;
            _rescuePending = false;
            _rescueCause = DeathCause.None;
            _rescueArchetype = ObstacleArchetype.None;
            _lessonBackZ = 0.0;
            _lessonVineZ = 0.0;
            _hintEndTick = 0L;
            _lessonStartTick = 0L;
            _actionTick = 0L;
            _beginTick = 0L;
            _nextLessonTick = 0L;
        }
    }
}
