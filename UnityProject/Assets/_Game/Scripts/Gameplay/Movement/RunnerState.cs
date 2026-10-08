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

        public float DodgeOriginX;

        public InputCommand Buffered;

        public long BufferedTick;

        public DropReason BufferedKind;

        public int Health;

        public float RegenProgress;

        public bool Shield;

        public bool Dead;

        public DeathCause Cause;

        public int DeathObstacle;

        public long DeathTick;

        public float DeathSpeed;

        public bool Finished;

        public long FinishTick;

        public int Hits;

        public int Coins;

        public int DroppedInputs;

        public int LastHitObstacle;

        public HitKind LastHitKind;

        public int NudgedFork;

        public int Revives;

        public bool IsInvulnerable => Tick <= InvulnerableUntilTick;
    }
}
