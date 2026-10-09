using JungleBooze.Core;

namespace JungleBooze.Gameplay.Movement
{
    /// <summary>
    /// The complete scalar state of the runner (path space, spec 101 §2.1). A plain struct so it can be copied for
    /// interpolation (previous/current) and bot look-ahead. Timers are absolute tick stamps.
    /// </summary>
    public struct RunnerState
    {
        /// <summary>Ticks stepped in this run (the first step is tick 1).</summary>
        public long Tick;

        /// <summary>Distance along the path centreline, m.</summary>
        public float S;

        /// <summary>Lateral offset from the centreline, m (+ = right).</summary>
        public float X;

        /// <summary>Feet height in the path frame, m.</summary>
        public float Y;

        /// <summary>Lateral target the servo follows, m.</summary>
        public float XTarget;

        public float VLat;

        public float Vy;

        /// <summary>Forward speed used this tick, m/s.</summary>
        public float Speed;

        /// <summary>Run distance d (actual s travelled), m.</summary>
        public float Distance;

        public bool Grounded;

        public bool Sliding;

        public bool FastFalling;

        /// <summary>Airborne because of a jump (no coyote).</summary>
        public bool Jumped;

        /// <summary>Fell below a lip: no landing is possible any more.</summary>
        public bool BelowLip;

        /// <summary>Floor height under her (grounded) or of the last ground she stood on (airborne).</summary>
        public float GroundY;

        /// <summary>Height of the floor she left; falls die below this − fallKillDepth.</summary>
        public float LastGroundY;

        /// <summary>Highest feet height since leaving the ground (fall height on landing).</summary>
        public float AirPeakY;

        public long AirborneSinceTick;

        public long SlideEndTick;

        public long DodgeBoostUntilTick;

        public long InvulnerableUntilTick;

        public long StumbleTick;

        public long ReviveTick;

        public InputCommand Buffered;

        public long BufferedTick;

        public DropReason BufferedKind;

        public int Health;

        public float RegenProgress;

        public bool Shield;

        /// <summary>Tick the shield expires on (0 = no limit).</summary>
        public long ShieldUntilTick;

        public bool Dead;

        public DeathCause Cause;

        public int DeathObstacle;

        public long DeathTick;

        public float DeathSpeed;

        /// <summary>Run distance on the death tick (a revive rewinds <see cref="Distance"/> to the revive point).</summary>
        public float DeathDistance;

        /// <summary>Path position on the death tick.</summary>
        public float DeathS;

        /// <summary>Falls caught by the post-revive guard (spec 101 §4.2 i-frames extended to falls, review S4).</summary>
        public int FallRescues;

        public bool Finished;

        public long FinishTick;

        public int Hits;

        public int Coins;

        public int DroppedInputs;

        public int LastHitObstacle;

        public HitKind LastHitKind;

        public int NudgedFork;

        public int Revives;

        // ---- Traversal (spec 103 §4–6) ----

        public MoveMode Mode;

        public DivePhase Dive;

        /// <summary>First tick of the current dive phase.</summary>
        public long DivePhaseTick;

        /// <summary>Body height when the current dive phase started.</summary>
        public float DiveFromY;

        /// <summary>Up phase at <c>DiveRiseFactor</c> (a leap cancelled the dive).</summary>
        public bool DiveRiseFast;

        /// <summary>Action queued for surfacing: Jump = leap, Slide = dive again.</summary>
        public InputCommand OnSurface;

        /// <summary>Airborne out of the water (swim leap).</summary>
        public bool Leaping;

        /// <summary>Water surface of the volume she swims in.</summary>
        public float WaterY;

        /// <summary>Forward speed blend: from this speed over <see cref="BlendTicks"/> starting at <see cref="BlendTick"/>.</summary>
        public float BlendFromSpeed;

        public long BlendTick;

        public int BlendTicks;

        /// <summary>Vine being swung (or the last one), path vine id.</summary>
        public int VineId;

        public long GrabTick;

        public float GrabS;

        public float GrabX;

        public float GrabY;

        /// <summary>A swipe up before the release window opened (fires at the window).</summary>
        public bool ReleaseHeld;

        /// <summary>Airborne after a vine release: forward speed is <see cref="LaunchSpeed"/> until landing.</summary>
        public bool VineAir;

        public float LaunchSpeed;

        public bool LastReleasePerfect;

        /// <summary>Deep dive: zone id, start tick and start point.</summary>
        public int DeepZone;

        public long DeepStartTick;

        public float DeepS0;

        public float DeepX0;

        public float DeepExitS;

        public float DeepExitX;

        /// <summary>Hits when the current dive/leap/swing started (traversal success = none since).</summary>
        public int ActionHits;

        /// <summary>Airborne over a gap in a canopy section (a beam-gap traversal is in progress).</summary>
        public bool OverGap;

        /// <summary>Run counters: vine releases, Perfect releases, traversals attempted / succeeded (DDA, analytics).</summary>
        public int Releases;

        public int Perfects;

        public int TraversalAttempts;

        public int TraversalSuccesses;

        /// <summary>Beam landing assist: x pulled from → to over ticks [SnapTick, SnapUntilTick].</summary>
        public long SnapTick;

        public long SnapUntilTick;

        public float SnapFromX;

        public float SnapToX;

        /// <summary>Under water with the surface obstacles above her (dive Under phase or a deep dive).</summary>
        public bool Submerged => (Mode == MoveMode.Swim && Dive == DivePhase.Under) || Mode == MoveMode.DeepDive;

        public bool IsInvulnerable => Tick <= InvulnerableUntilTick;
    }
}
