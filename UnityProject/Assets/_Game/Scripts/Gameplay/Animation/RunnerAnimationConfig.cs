using System;

namespace JungleBooze.Gameplay.Animation
{
    /// <summary>
    /// Presentation tuning for the animated runner (Pista): which clip plays when, playback rates, fades,
    /// procedural lean, the in-air foot anchor and the ponytail spring. Clip timing numbers come from sampling the
    /// imported clips (frame numbers at 30 fps, see <c>PistaInspect.Analyze</c>). Nothing here touches the
    /// simulation. Lives in <c>Assets/_Game/Config/Movement/RunnerAnimationConfig.asset</c>.
    /// </summary>
    [Serializable]
    public sealed class RunnerAnimationConfig
    {
        // ---- Locomotion -------------------------------------------------------------------------------------------

        /// <summary>Below this forward speed (m/s) and grounded, the idle/ready pose plays.</summary>
        public float IdleSpeed = 0.25f;

        /// <summary>Ground speed at which the Run clip's planted foot doesn't slide, at 1.0x (m/s).</summary>
        public float RunNaturalSpeed = 5.0f;

        /// <summary>Same for Run_Alt (the sprint), m/s.</summary>
        public float SprintNaturalSpeed = 6.8f;

        /// <summary>Loop lengths (s) of Run and Run_Alt; the blended cycle length is their weighted mix.</summary>
        public float RunCycleLength = 16f / 30f;

        public float SprintCycleLength = 14f / 30f;

        /// <summary>Locomotion blend: 0 = Run, 1 = Run_Alt, smoothstep between these speeds (m/s).</summary>
        public float SprintBlendStartSpeed = 6f;

        public float SprintBlendEndSpeed = 9f;

        /// <summary>
        /// Most Run_Alt that is ever mixed in (0…1). Default 0 [ASSUMED]: Run_Alt is a wide-armed, deeply pitched
        /// sprint; ART_DIRECTION 7.5 asks for arms close to the body, so Run plus a procedural forward lean is used.
        /// </summary>
        public float SprintBlendMax = 0f;

        /// <summary>Procedural forward lean of the spine (deg) at <see cref="ForwardLeanSpeedA"/> and <see cref="ForwardLeanSpeedB"/>.</summary>
        public float ForwardLeanDegA = 4f;

        public float ForwardLeanDegB = 9f;

        public float ForwardLeanSpeedA = 8f;

        public float ForwardLeanSpeedB = 16f;

        /// <summary>
        /// Contact-phase time warp: while a foot is planted the cycle plays at the rate that keeps that foot still on
        /// the ground (no skating); while both feet are in the air it plays at most <see cref="FlightRateMax"/>, so
        /// cadence stays believable and the stride gets longer instead of the legs spinning faster.
        /// </summary>
        public bool ContactWarp = true;

        /// <summary>Normalized windows of the blended cycle where a foot is planted (Run is cycle-offset to match).</summary>
        public float ContactAStart = 0.0f;

        public float ContactAEnd = 0.24f;

        public float ContactBStart = 0.53f;

        public float ContactBEnd = 0.78f;

        /// <summary>
        /// Generated from the Run clip by the Pista setup (editor): backward speed (m/s at 1.0x) of the planted foot at
        /// evenly spaced phases of the locomotion cycle. When present it replaces the
        /// contact windows: the rate at each phase is ground speed / this value, so the planted foot stays put even
        /// where the source clip's foot speed is uneven. −1 = no foot planted (flight).
        /// </summary>
        public float[] StanceSpeedTable = new float[0];

        /// <summary>Planted entries slower than this (m/s, touch-down/lift-off frames) play at MaxRunRate.</summary>
        public float MinStanceSpeed = 1.5f;

        /// <summary>Rate for those touch-down/lift-off frames: effectively skips them (a source artifact).</summary>
        public float TouchdownRate = 8f;

        /// <summary>Soft edge of the contact windows (normalized), avoids rate pops.</summary>
        public float ContactEdge = 0.04f;

        /// <summary>Fraction of the ground speed matched by the planted foot (1 = no skating).</summary>
        public float StanceMatch = 1f;

        /// <summary>Playback rate cap while both feet are off the ground.</summary>
        public float FlightRateMax = 1.25f;

        /// <summary>Overall playback rate limits of the run cycle.</summary>
        public float MinRunRate = 0.6f;

        public float MaxRunRate = 4f;

        /// <summary>Run cycle start (normalized) after a landing on the left foot, matches the Jump clip.</summary>
        public float LandingRunPhase = 0.02f;

        // ---- Jump / air -------------------------------------------------------------------------------------------

        /// <summary>Jump clip length (s), take-off and touch-down frames (30 fps).</summary>
        public float JumpClipLength = 26f / 30f;

        public float JumpTakeoffFrame = 3f;

        public float JumpTouchdownFrame = 17.5f;

        /// <summary>Air longer than the Jump clip covers (walk-off, gap, fast-fall) switches to the Fall loop.</summary>
        public float FallRate = 1f;

        /// <summary>Airborne without a jump for this long (s) before the Fall loop starts (steps down stay running).</summary>
        public float WalkOffFallDelay = 0.12f;

        // ---- Slide ------------------------------------------------------------------------------------------------

        public float SlideClipLength = 51f / 30f;

        /// <summary>Frames where the body reaches the low slide pose and where the clip starts to get up.</summary>
        public float SlideLowFrame = 16f;

        public float SlideHoldEndFrame = 27f;

        public float SlideStartFrame = 2f;

        /// <summary>Time (s) to drop into the slide pose; the simulation's hitbox is already low on the first tick.</summary>
        public float SlideDropTime = 0.14f;

        /// <summary>Expected slide length (s) from the simulation config; the hold part is stretched to fill it.</summary>
        public float SlideDuration = 0.65f;

        // ---- Stumble / landing / death -----------------------------------------------------------------------------

        public float StumbleClipLength = 75f / 30f;

        public float StumbleStartFrame = 2f;

        /// <summary>Stumble plays this long (s) at <see cref="StumbleRate"/> before blending back into the run.</summary>
        public float StumbleTime = 0.42f;

        public float StumbleRate = 1.5f;

        /// <summary>Hard-landing absorb (Land_Run sub-clip): start frame inside the sub-clip, rate and hold time.</summary>
        public float LandHardClipLength = 20f / 30f;

        public float LandHardStartFrame = 0f;

        public float LandHardRate = 1.6f;

        public float LandHardTime = 0.2f;

        public float DeathRate = 1.15f;

        // ---- Fades (s) ---------------------------------------------------------------------------------------------

        public float FadeToIdle = 0.25f;

        public float FadeIdleToRun = 0.2f;

        public float FadeToJump = 0.04f;

        public float FadeToFall = 0.15f;

        public float FadeLandToRun = 0.1f;

        public float FadeToSlide = 0.05f;

        public float FadeSlideToRun = 0.16f;

        public float FadeToStumble = 0.05f;

        public float FadeStumbleToRun = 0.18f;

        public float FadeToLandHard = 0.04f;

        public float FadeLandHardToRun = 0.18f;

        public float FadeToDeath = 0.08f;

        // ---- Procedural lean ---------------------------------------------------------------------------------------

        /// <summary>Body yaw toward the travel direction: fraction of atan(vLat / v), and its limit (deg).</summary>
        public float YawFactor = 0.55f;

        public float MaxYawDeg = 20f;

        /// <summary>Bank into the steering (deg at full lateral speed), positive leans toward the motion.</summary>
        public float RollAtMaxLateral = 9f;

        /// <summary>Extra bank on a dodge (deg), decaying over <see cref="DodgeRollTime"/> s.</summary>
        public float DodgeRollDeg = 8f;

        public float DodgeRollTime = 0.25f;

        /// <summary>Lean away from the path edge while brushing it (deg).</summary>
        public float EdgeBrushRollDeg = 3f;

        /// <summary>Half-life (s) of the lean smoothing; short so steering reads at once.</summary>
        public float LeanHalfLife = 0.045f;

        // ---- In-air foot anchor ------------------------------------------------------------------------------------

        /// <summary>
        /// In the air the body is lowered so the lowest foot is at the simulation's feet height: what clears an
        /// obstacle on screen is what clears it in the simulation.
        /// </summary>
        public bool AirFootAnchor = true;

        /// <summary>Ankle bone height when standing (m).</summary>
        public float AnkleRestHeight = 0.13f;

        /// <summary>Limit of the anchor offset (m).</summary>
        public float MaxAnchorDrop = 0.9f;

        public float AnchorHalfLife = 0.03f;

        // ---- Ponytail spring ---------------------------------------------------------------------------------------

        public float HairStiffness = 0.16f;

        public float HairDamping = 0.12f;

        public float HairGravity = 6f;

        /// <summary>Share of the body's acceleration fed into the hair as inertia (jumps, landings, steering).</summary>
        public float HairInertia = 0.6f;

        public float HairMaxAngleDeg = 55f;

        public float HairMaxAcceleration = 80f;

        public RunnerAnimationConfig Clone()
        {
            return (RunnerAnimationConfig)MemberwiseClone();
        }
    }
}
