# Spec 001: Player movement

**Owner:** game-designer | **Builder:** gameplay-engineer | **Week:** 1 | **Status:** Ready to build | **Last updated:** 2026-10-06

**Source of truth:** GDD sections 5 (controls and feel), 6 (movement and camera), 11.1 (speed curve), 16 (config assets).
This spec refines those numbers. Where it changes the GDD, the GDD has been updated in the same change (see section 14).

**Covers:** forward running and speed, 3-lane switching, jump, slide, fast-fall, input buffer, coyote time, edge lanes,
same-tick command rules, pause, collision categories (lethal and stumble), camera follow, events for animation, VFX,
audio and haptics, and the touch-to-command layer.

**Out of scope (later specs, hooks only):** vine swing (`vine-swing.md`), Lift and the companion (`companion.md`),
power-ups (`power-ups.md`), track generation and final obstacle sizes (`track-generation.md`, `obstacles.md`), the death
screen and continue flow (`death-and-results.md`, `continue.md`), the tutorial slow-down (`onboarding.md`).

---

## 1. Player story and purpose

"I swipe and HERO moves right away, the same way every time. When I swipe a little early, it still works. When I clip
the side of a log while changing lanes, I trip but I get another chance. When I die, I know exactly why."

Movement is what the player touches thousands of times per session. If it feels instant and fair, every other feature
(vines, the macaw, missions) has something solid to sit on. This spec supports pillars 1 (readable at speed) and
2 (one-thumb play, fair every time).

---

## 2. Conventions

- **Tick:** one simulation step at **60 Hz** = 16.667 ms (`FixedStepTimeSource`, ARCHITECTURE 5.2). All simulation
  timing is counted in whole ticks.
- **Authoring vs. runtime:** ScriptableObject fields are written in designer units (ms, m, m/s). When the run starts,
  `RunnerConfigAsset.ToConfig()` converts them into an immutable `RunnerConfig` with whole ticks:
  `ticks = max(1, floor(ms × 60 / 1000 + 0.5))`. Values in this spec show both, for example "120 ms → 7 ticks (117 ms)".
  Tests assert the **tick** values, because those are what the game actually does.
- **Axes:** `x` = lateral (left negative), `y` = height above the track surface, `z` = distance along the track.
  Lane index 0 = left, 1 = middle, 2 = right. Lane center `x = (lane − 1) × laneWidthM`.
- **Simulation vs. presentation:** everything in sections 3 to 9 is simulation (plain C#, `JungleBooze.Gameplay`,
  deterministic, no `UnityEngine.Time`/`Random`, no physics engine). Sections 10 and 11 are presentation (views,
  camera, VFX), which read simulation state and events and never write them.
- `[ASSUMED]` marks a designer default that stands until the owner says otherwise.

---

## 3. Config assets and fields

All assets live in `UnityProject/Assets/_Game/Config/`. Each has `OnValidate` range checks and is covered by a
config-validation EditMode test (AC-60).

### 3.1 `RunnerTuning.asset` (type `RunnerConfigAsset` → `RunnerConfig`), simulation

| Field | Unit | Start value | Ticks | Valid range | Notes |
|---|---|---|---|---|---|
| `laneCount` | lanes | 3 | | 3 (fixed for launch) | |
| `laneWidthM` | m | 2.4 | | 2.0–3.0 | |
| `startLane` | index | 1 | | 0..laneCount−1 | Middle lane |
| `laneSwitchMs` | ms | 120 | **7 (117 ms)** | 80–140 | Every lane move takes exactly this long, whatever its start x |
| `laneSwitchEaseExponent` | | 2 | | 1–4 | Ease-out: `p(u) = 1 − (1 − u)^k`, `u = elapsed / laneSwitchTicks` |
| `laneQueueStartFraction` | 0..1 | 0.5 | **tick 4** of the move | 0.3–1.0 | Queued second move starts at `ceil(laneSwitchTicks × fraction)` |
| `edgeForgivenessFraction` | 0..1 | 0.6 | | 0.5–0.8 | GDD 5.2 "edge forgiveness" (7.3) |
| `jumpApexHeightM` | m | 1.5 | | 1.0–2.5 | |
| `jumpAirtimeMs` | ms | 600 | **36** | 400–900 | Constant at all speeds |
| `fastFallMinSpeedMps` | m/s | 15.0 | | 8–30 | |
| `fastFallMaxMs` | ms | 100 | **6** | 50–120 | Fast-fall never takes longer than this, from any height |
| `slideMs` | ms | 650 | **39** | 400–1000 | |
| `inputBufferMs` | ms | 150 | **9** | 50–250 | Moved here from `InputTuning` (section 14) |
| `coyoteMs` | ms | 80 | **5 (83 ms)** | 0–150 | Moved here from `InputTuning` |
| `playerHitboxWidthM` | m | 0.7 | | 0.4–0.9 | Visual body is about 0.9 m |
| `playerHitboxDepthM` | m | 0.5 | | 0.3–0.8 | New number (not in GDD before) |
| `standingHeightM` | m | 1.8 | | | |
| `slidingHeightM` | m | 0.8 | | must be < lowest high-barrier underside | |
| `fallDeathDepthM` | m | 1.0 | | 0.5–3.0 | Below track surface; then "Fell" death |
| `runStartRampMs` | ms | 500 | **30** | 0–1500 | Speed eases from `runStartSpeedFraction` to curve speed |
| `runStartSpeedFraction` | 0..1 | 0.5 | | 0–1 | |
| `stumbleBounceMs` | ms | 150 | **9** | 80–300 | Bounce back to the origin lane |
| `stumbleDazeMs` | ms | 3000 | **180** | 1000–6000 | A second stumble inside this window is lethal [ASSUMED] |
| `nearMissDistanceM` | m | 0.35 | | 0.1–0.6 | GDD 13.1 |
| `eventBufferCapacity` | events | 64 | | 32–256 | Ring buffer size per run; overflow is an error in dev builds |

Derived values (computed in `ToConfig()`, not authored, asserted by AC-61):

| Derived | Formula | Value at start values |
|---|---|---|
| `jumpAirtimeTicks` | from `jumpAirtimeMs` | 36 |
| `T` (airtime in s) | `jumpAirtimeTicks / 60` | 0.600 s |
| `gravityMps2` | `8 × jumpApexHeightM / T²` | 33.33 m/s² |
| `jumpVelocityMps` | `4 × jumpApexHeightM / T` | 10.0 m/s |
| `laneQueueStartTick` | `ceil(laneSwitchTicks × laneQueueStartFraction)` | 4 |

### 3.2 `SpeedCurve.asset` (type `SpeedCurveAsset` → `SpeedCurve`), simulation

| Field | Unit | Start value |
|---|---|---|
| `rows` | list of (`distanceM`, `speedMps`) | (0, 10.0), (500, 12.0), (1100, 13.5), (2300, 15.5), (3600, 17.5), (5000, 19.0), (7000, 21.0) |
| `tutorialSpeedMps` | m/s | 8.0 (used only while the onboarding system says the tutorial is active) |

Rules: linear interpolation between rows; below the first row use the first speed; beyond the last row use the last
speed (cap). Rows must be sorted by distance and speeds must not decrease (validation).

### 3.3 `InputTuning.asset` (type `InputConfigAsset` → `InputConfig`), touch layer only

| Field | Unit | Start value | Source |
|---|---|---|---|
| `swipeThresholdPt` | pt | 28 | GDD 5.1 |
| `swipeWindowMs` | ms | 250 | GDD 5.1 |
| `swipeRearmDistancePt` | pt | 28 | GDD 5.1 |
| `diagonalTieGoesHorizontal` | bool | true | GDD 5.1 |
| `tapMaxMovementPt` | pt | 12 | New [ASSUMED] |
| `tapMaxDurationMs` | ms | 200 | New [ASSUMED] |
| `doubleTapWindowMs` | ms | 300 | GDD 16 (from first tap's lift to second tap's touch-down) |
| `gestureQueueCapacity` | gestures | 8 | New |

The touch layer works in real time (it is not simulation). Bots and replays bypass it.

### 3.4 `RunnerPresentationTuning.asset` (type `RunnerPresentationConfigAsset`), presentation only (new asset)

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `cameraOffsetBehindM` | m | 6.0 | GDD 6 |
| `cameraOffsetUpM` | m | 3.2 | GDD 6 |
| `cameraLookAheadM` | m | 8.0 | GDD 6 |
| `cameraLookAtHeightM` | m | 1.0 | New |
| `cameraFovDeg` | ° | 60 | Vertical FOV, portrait |
| `cameraLateralFollow` | 0..1 | 0.70 | GDD 6 |
| `cameraLateralSmoothMs` | ms | 90 | GDD 6 |
| `cameraVerticalFollow` | 0..1 | 0.25 | New: camera rises 25% of HERO's height |
| `cameraVerticalSmoothMs` | ms | 150 | New |
| `fogStartM` | m | 45 | GDD 6 |
| `minVisibleTrackS` | s | 1.6 | GDD 6 (≥ 34 m at 21 m/s) |
| `hitPauseMs` | ms | 350 | GDD 5.2 death readability |
| `deathCameraHoldMs` | ms | 800 | GDD 5.2 |
| `laneBumpWobbleMs` | ms | 40 | GDD 5.3.4 |
| `laneBumpWobbleM` | m | 0.12 | New: visual HERO offset only, not the camera |
| `stumbleShakeMs` | ms | 200 | New; off with Reduce Motion |
| `stumbleShakeM` | m | 0.08 | New |
| `runAnimReferenceSpeedMps` | m/s | 10.0 | Run cycle plays at 1.0× at this speed |
| `runAnimRateMin` / `runAnimRateMax` | × | 0.8 / 1.6 | Clamp of `speed / reference` |

---

## 4. Forward running and speed

### 4.1 Behavior

1. A run starts at tick 0 with HERO grounded in `startLane`, `z = 0`, state `Running`.
2. Every tick: `baseSpeed = SpeedCurve(distance)` (or `tutorialSpeedMps` while the tutorial flag is set).
3. During the first `runStartRampTicks` (30) ticks: `speed = baseSpeed × lerp(runStartSpeedFraction, 1, tick / 30)`.
   Input is accepted from tick 0; nothing about the ramp blocks control.
4. `speed = speed × speedMultiplier`. `speedMultiplier` is a hook for Speed Boost (week 3), default 1.0.
5. `z += speed × dt`. Distance is stored as `double` so precision does not drift on long runs. Collision maths may use
   chunk-relative floats.
6. Speed never changes jump airtime or slide duration (both are fixed in ticks). Jump length grows with speed
   (6.0 m at 10 m/s, 12.6 m at 21 m/s).
7. When distance crosses a row of the speed curve, emit `SpeedStepReached(rowIndex)` (for music intensity and
   wind VFX).

### 4.2 Speed reference table (ticks are per 60 Hz step)

| Distance | Speed | m per tick | Jump length |
|---|---|---|---|
| Tutorial | 8.0 m/s | 0.133 m | 4.8 m |
| 0 m | 10.0 m/s | 0.167 m | 6.0 m |
| 1,100 m | 13.5 m/s | 0.225 m | 8.1 m |
| 3,600 m | 17.5 m/s | 0.292 m | 10.5 m |
| 7,000 m+ | 21.0 m/s | 0.350 m | 12.6 m |
| Boost at cap (hook) | 33.6 m/s | 0.560 m | 20.2 m |

---

## 5. Lanes

### 5.1 State

- `TargetLane`: the lane HERO is moving to or standing in (0..2).
- `X`: current lateral position.
- `LaneMove`: active move or none (`startX`, `endLane`, `elapsedTicks`).
- `QueuedLateral`: at most one queued direction (−1, +1 or none).
- `OccupiedLane` (read-only, for pickups and other systems): lane whose center is nearest to `X`; a tie goes to
  `TargetLane`.

### 5.2 A lane move

A lane move tweens `X` from `startX` (current `X` when the move starts) to the center of `endLane` over exactly
`laneSwitchTicks` (7) ticks with the ease-out curve `p(u) = 1 − (1 − u)²`. At the start values HERO covers 26% of a
lane on the first tick, 49% after 2, 67% after 3, and 100% on tick 7.

The first `X` change is applied **on the same tick** the command is processed (no idle tick).

### 5.3 Lateral command rules (MoveLeft = −1, MoveRight = +1)

Process in this order and stop at the first rule that applies:

| # | Situation | Result | Event |
|---|---|---|---|
| L1 | HERO is dead or below the track surface (`Y < 0`) | Ignored | none (outcome `Ignored`) |
| L2 | A stumble bounce is running | Stored in `QueuedLateral` (replaces any older one); starts when the bounce ends | none |
| L3 | No move active | If `TargetLane + dir` is a valid lane: start a move. Otherwise: **bump** | `LaneChangeStarted` or `LaneBlocked(dir)` |
| L4 | Move active, `dir` is **opposite** to the move, and a queued move exists | Cancel the queued move. The active move continues | `LaneChangeCancelled` |
| L5 | Move active, `dir` is opposite, no queue | **Reverse:** start a new move from the current `X` back to the origin lane (7 ticks) | `LaneChangeStarted(reversal = true)` |
| L6 | Move active, same direction, no queue | If `TargetLane + dir` is valid: queue it. Otherwise: bump | none, or `LaneBlocked(dir)` |
| L7 | Move active, same direction, queue already holds a move | `TargetLane + 2·dir` is always outside 3 lanes: bump | `LaneBlocked(dir)` |

- **Tick counting:** a move processed on tick `s` applies its first `X` change on tick `s` (u = 1/7) and arrives on
  tick `s + 6` (u = 7/7), so it occupies 7 ticks.
- A queued move starts once the active move has completed `laneQueueStartTick` (4) ticks, that is on tick `s + 4`,
  or immediately if the active move has already completed 4 or more ticks when the command arrives. It starts from
  the current `X` and takes 7 ticks. So a fast double swipe from lane 0 reaches lane 2 on tick `s + 10`, an elapsed
  span of 4 + 7 = **11 ticks (183 ms)** (GDD 5.3.3: "about 180 ms").
- "Origin lane" of a move = `endLane − dir` (the lane HERO is moving away from).
- **Bump (edge lane):** `X` does not change. The simulation emits `LaneBlocked(dir)`; the view plays the 40 ms wobble
  and a light haptic. The command is consumed (not buffered), so it never fires later by surprise.
- Lane moves work the same on the ground, in the air, while sliding and while fast-falling. They never change `Y`,
  the slide timer or the jump arc.

---

## 6. Vertical movement: jump, fast-fall, slide

### 6.1 Locomotion states

`Running` (grounded), `Sliding` (grounded, short hitbox), `Airborne` (jump arc), `FastFalling`, `Coyote` (just left the
ground without jumping), `Falling` (no ground, below the surface or past coyote), `Stumbling` is **not** a separate
state: it is a flag (`DazeTicksLeft > 0`) plus an optional bounce, layered on top of the other states. `Dead`.
Hook state for later: `Carried` (vine, Lift). While `Carried`, this spec's movement is suspended and commands are
routed to the owning system.

### 6.2 Jump

1. **Start:** a Jump command while `Running`, `Sliding` or `Coyote` starts a jump on that tick (`jumpStartTick`).
   Sliding ends immediately (`SlideEnded(reason = Jump)`) and the standing hitbox returns on the same tick.
2. **Arc:** `Y` is evaluated analytically, not integrated, so every jump is identical:
   `n = tick − jumpStartTick`, `t = n / 60`, `Y = v₀·t − ½·g·t²` with `v₀ = 10 m/s`, `g = 33.33 m/s²`.
   Apex 1.5 m at n = 18. On the tick where `n = 36`, `Y` is set to exactly 0 and HERO lands (if there is ground).
3. **Landing:** emit `Landed(airTicks, wasFastFall)`. Then, on the same tick, in this order:
   a buffered Jump fires (rule I3), else a pending slide-on-landing starts (6.3), else HERO is `Running`.
4. **No ground at landing:** HERO continues the same parabola below 0 (`Falling`), see 6.5.
5. A Jump command while `Airborne`, `FastFalling` or `Falling` above the surface is **buffered** (section 7).

### 6.3 Fast-fall (Slide command in the air)

1. A Slide command while `Airborne` or `Coyote` starts a fast-fall on that tick: `FastFalling`, emit `FastFallStarted`.
2. Fall speed is fixed for the whole fast-fall: `v = max(fastFallMinSpeedMps, Y₀ / (fastFallMaxMs / 1000))`, where `Y₀`
   is the height when it starts. `Y` drops by `v·dt` per tick and is clamped to 0 on ground contact.
3. Duration from apex (1.5 m): 1.5 / 15 = 0.100 s = **6 ticks**. From any height the cap gives **≤ 6 ticks (100 ms)**,
   which is inside the GDD target of ≤ 120 ms.
4. A slide-on-landing is set. On landing HERO slides for the full `slideTicks` (39), unless a buffered Jump fires
   (a buffered Jump wins, because GDD 5.3.2 lets a jump cancel a slide anyway).
5. A Slide command while already `FastFalling` is ignored (outcome `AlreadyActive`).
6. Fast-fall over a gap: HERO falls into it (the player chose to). Fast-fall during `Coyote` behaves the same.

### 6.4 Slide

1. A Slide command while `Running` starts a slide on that tick: `Sliding`, hitbox height 0.8 m, `slideTicksLeft = 39`.
   Emit `SlideStarted(restart = false)`.
2. A Slide command while `Sliding` restarts the timer to 39. Emit `SlideStarted(restart = true)` (GDD 5.3.2).
3. A Jump command while `Sliding` cancels the slide and jumps on that tick (6.2).
4. Lane moves keep the slide running (GDD 5.3.2).
5. **Slide ends under a high barrier:** if the slide timer reaches 0 while the standing hitbox (1.8 m) would overlap any
   obstacle, the slide **extends** tick by tick until it would not. No cap. Emit `SlideEnded(reason = Timeout)` only
   when HERO actually stands up. (Standing up into a barrier would be an unreadable death.)
6. **Slide off a ledge** (ground ends under HERO while sliding): the slide ends, HERO enters `Coyote` (6.5), and a Jump
   in the coyote window works as usual.

### 6.5 Coyote time, gaps and falling

The track answers one question for movement: `ITrackQuery.HasGround(x, zMin, zMax)`, true if any ground lies under the
HERO footprint (`X ± 0.35 m`, `z ± 0.25 m`). Week 1 ships `FlatTrackQuery` (always true) and a `TestTrackQuery`
with gaps for tests. The final track (week 2) implements the same interface.

1. **Leaving the ground without jumping** (`Running` or `Sliding` and `HasGround` becomes false on tick `e`, the
   "leave tick"): state `Coyote` with a counter of `coyoteTicks` (5). The counter goes down by 1 in step 9 of each
   following tick; at 0 the state becomes `Falling`. So a Jump is a ground jump on ticks `e + 1` to `e + 5`, and is
   buffered from tick `e + 6`. `Y` stays at 0 during these ticks. A Jump command in this window is a normal full jump
   (`JumpStarted(coyote = true)`). If ground returns within the window (for example a lane move onto a lane with
   ground), HERO is `Running` again.
2. After the window, state `Falling`: `Y` follows gravity from rest (`Y = −½·g·t²`).
3. **Landing from a jump over a gap:** HERO lands only if `HasGround` is true on the landing tick (`Y` reaches 0 from
   above). Ground is only checked while `Y ≥ 0`; once `Y < 0` there is no recovery.
4. Once `Y < 0`: all commands are ignored (outcome `Ignored`). When `Y ≤ −fallDeathDepthM` (1.0 m; about 15 ticks after
   leaving the surface from rest), HERO dies with cause `Fell`.
5. A buffered Jump expires normally while falling; it never fires below the surface.

---

## 7. Input buffer and same-tick rules

The buffer lives in the simulation (ARCHITECTURE 5.3), so bots and replays get exactly the same forgiveness.

| # | Rule |
|---|---|
| I1 | **What is buffered:** only Jump (one slot, `bufferedJumpTick`). Lateral commands are never buffered; they execute, queue (section 5) or bump. Slide in the air is not buffered: it becomes a fast-fall. |
| I2 | **Age:** a buffered Jump is valid while `tick − bufferedJumpTick ≤ inputBufferTicks` (9). A newer Jump replaces an older one (resets the age). |
| I3 | **Fire:** on the landing tick (the first tick a jump is possible again), a valid buffered Jump fires at once: `JumpStarted(buffered = true)`. A landing with no ground under HERO is not a landing (6.5.3), so nothing fires. |
| I4 | **Expire:** at age 10 the buffered Jump is dropped: outcome `Expired`. |
| I5 | **No longer possible:** if HERO enters a state where Jump means something else (for example `Carried` on a vine, week 3) or dies, the buffer is dropped: outcome `Invalidated` (GDD 5.3.5). |
| I6 | **Pause:** the buffer and `QueuedLateral` are cleared when play resumes after a pause (section 8). The active lane move, jump arc and slide continue from where they were. |
| I7 | **Same-tick conflicts:** one tick can carry several flags (bots, replays). MoveLeft + MoveRight in the same tick cancel each other (outcome `Cancelled`). Jump + Slide in the same tick: Jump is processed, Slide is dropped (outcome `Superseded`). |
| I8 | **Processing order per tick:** (1) read commands, (2) `PauseResumed` clears buffers, (3) resolve conflicts (I7), (4) age the buffer, (5) lateral command, (6) vertical command, (7) speed and `z`, (8) lane tween and queued-move start, (9) vertical motion, ground check, landing and buffered fire, (10) slide timer, (11) collisions (section 9), (12) events. This order is part of the contract: tests and replays depend on it. |
| I9 | **Accounting:** every command flag gets exactly one outcome: `Executed`, `Queued`, `Buffered`, `Bumped`, `Cancelled`, `Superseded`, `Expired`, `Invalidated`, `AlreadyActive`, `Ignored`. Counters per outcome are exposed for the sim report. A command that disappears without an outcome is a bug. `CompanionAssist` is not a movement command and is passed through untouched. |

---

## 8. Pause

1. **Triggers:** pause button; app goes to background; focus lost (incoming call, Control Center, notification
   center). Implemented in `App` via `OnApplicationPause(true)` / `OnApplicationFocus(false)`.
2. **While paused:** the simulation does not step. The run driver does not call `ITimeSource.Accumulate`. All active
   touches are cancelled and the gesture recognizer is reset, so a swipe that started before pause never completes
   after it.
3. **Resume:** the player taps Resume (or returns to the app, which shows the pause menu first; never auto-resume).
   A 3-2-1 countdown plays for **1.5 s real time** (0.5 s per number). Touches during the countdown are ignored.
4. When the countdown ends, the run driver discards the accumulated real time (the time source must start the next
   frame with an empty accumulator, so no catch-up steps run; if `FixedStepTimeSource` has no method for this,
   the engineer adds one, for example `ClearAccumulator()`, without touching `Tick`).
5. The first simulation tick after resume carries the command flag **`PauseResumed`**. The simulation clears the
   buffer and the lateral queue when it sees it (I6). Because this happens through the input stream, replays
   reproduce it exactly.
   - **Core change:** append `PauseResumed = 1 << 5` to `InputCommand` (append-only is allowed by the enum's
     contract). tech-architect signs this off in the PR.
6. **Pause during the countdown:** the countdown stops; resuming starts it again from 3.
7. **Pause in the air, mid lane move, mid slide, in coyote time:** all continue from the exact tick state on resume.
8. **Pause after death:** not offered by this spec (death flow owns that screen).

---

## 9. Collisions

### 9.1 Model

- Gameplay collisions are **simulation** maths on axis-aligned boxes (AABB). No Unity physics, colliders or
  triggers are used for gameplay.
- HERO box: width 0.7 m, depth 0.5 m, height 1.8 m (0.8 m while sliding), bottom at `Y`.
- Obstacles provide a box per lane they occupy plus an `ObstacleArchetype` (`LowBarrier`, `HighBarrier`,
  `FullBlock`, `Mover`, `Gap`) and an id. Movers move in the simulation and are boxes like the rest.
- **Swept test along z:** a hit is tested over the whole distance moved this tick (`z_prev → z_now`), so no obstacle
  can be skipped at any speed (0.56 m per tick with a boost).

### 9.2 Reference obstacle boxes for tests (owned later by `obstacles.md`)

These are fixtures for the EditMode tests in this spec. The obstacles spec (week 2) sets final sizes and must keep
these tests green or update them with the designer.

| Archetype | Width | Depth | Bottom → top |
|---|---|---|---|
| Low barrier | 2.04 m (85% of a lane) | 0.6 m | 0 → 0.8 m |
| High barrier | 2.04 m | 0.5 m | 1.1 → 3.0 m |
| Full block | 2.04 m | 1.0 m | 0 → 3.0 m |

### 9.3 Entry side decides the category

When HERO's box first overlaps an obstacle box, find the axis that started overlapping **last** inside this tick
(standard swept AABB time of entry):

| Entry | Category | What happens |
|---|---|---|
| **Front** (z axis: HERO runs into the face) | **Lethal** | Death, cause `Hit(archetype, obstacleId)` |
| **From below** (y axis: head rises into a high barrier during a jump) | **Lethal** | Death, cause `Hit(...)` (GDD 5.3.7; telegraphed, and the generator never makes a jump the only escape) |
| **Side** (x axis: a lane move pushes HERO into the side of an obstacle) | **Stumble** [ASSUMED] | See 9.4 |
| **From above** (y axis: HERO comes down onto the top of a low barrier) | **Stumble** [ASSUMED] | See 9.4 |
| Exact tie between axes | **Stumble** | Ties go to the player (pillar 2) |

Falling into a gap is lethal, cause `Fell` (6.5).

### 9.4 Stumble [ASSUMED, new rule]

A stumble is a "you clipped it" moment, not a death.

1. Emit `Stumbled(side | top, obstacleId)`. The obstacle is ignored by HERO's collisions for the rest of its pass.
2. **Side stumble:** cancel the lane move and the queue. Bounce: tween `X` back to the center of the lane HERO was
   moving away from, over `stumbleBounceTicks` (9), same ease-out. Lateral commands during the bounce are queued (L2).
   Jump and Slide work normally during the bounce. HERO keeps the current `Y` motion.
3. **Top stumble:** no bounce. HERO keeps the jump or fall arc down to the ground ("scrambles over" animation).
4. HERO is **dazed** for `stumbleDazeTicks` (180, 3.0 s), visible on HERO (view). Speed does not change.
5. **A second stumble while dazed is lethal:** cause `Hit(archetype, obstacleId)` with `afterStumble = true`, shown as
   "Tripped twice: Fallen log" on the death screen.
6. Hooks for other systems: a stumble resets the coin streak (GDD 13.1) and never counts as a near-miss.

### 9.5 Edge forgiveness (GDD 5.2)

An obstacle in lane `L` cannot hit HERO while HERO is moving **away** from `L` and `|X − center(L)| ≥ 0.6 × laneWidth`
(1.44 m). With the reference boxes the geometry already clears at about 1.37 m, so this rule is a safety cap for
wider obstacles (movers, the two-lane thorn patch). When HERO reverses toward `L`, the rule no longer applies.

### 9.6 Invulnerability hook

`InvulnerableTicks` (default 0; set by continue, Lift landing and power-ups in later weeks). While > 0, obstacle
overlaps cause no death and no stumble, and gaps still kill (GDD 10: the shield does not save from falling).

### 9.7 Near-miss

When HERO's box has fully passed an obstacle's z range with no contact, and the smallest gap between the two boxes
(x or y) during the z overlap was ≤ `nearMissDistanceM` (0.35 m), emit `NearMiss(obstacleId)`. Not emitted while
invulnerable or for an obstacle that caused a stumble.

---

## 10. Camera follow (presentation)

1. Camera position = (`camX`, `camY + 3.2`, `HERO z − 6.0`), looking at (`camX`, 1.0 + `camY`, `HERO z + 8.0`),
   vertical FOV 60°, portrait, no roll.
2. `camX` smooth-damps toward `0.7 × X` with a smooth time of 90 ms. `camY` smooth-damps toward `0.25 × Y` with 150 ms.
   Slides do not move the camera.
3. Camera smoothing uses real frame time (presentation is allowed to); it never feeds back into the simulation.
4. The camera follows interpolated HERO state (`InterpolationAlpha`) so motion stays smooth at uneven frame times.
5. At least 1.6 s of track ahead is visible at the lane height in every lane at every speed (≥ 34 m at 21 m/s);
   fog starts at 45 m.
6. **Death:** on `Died`, all views freeze for 350 ms (hit-pause), then the camera holds on the cause for 800 ms.
   The death screen is out of scope.
7. **Stumble:** a 200 ms camera shake of 0.08 m, off when Reduce Motion is on.
8. **Bump:** HERO's visual model wobbles 0.12 m toward the blocked side for 40 ms. The camera does not move.

---

## 11. Events for animation, VFX, audio and haptics

The simulation writes structs into a pre-allocated ring buffer (ARCHITECTURE 4). Views read them after the
simulation steps of each frame. No delegates, no allocations.

`RunnerEvent { RunnerEventType Type; long Tick; sbyte Dir; byte Lane; byte Flags; int ObstacleId; byte Archetype; short Value; }`

| Event | Payload | Typical use |
|---|---|---|
| `RunStarted` | lane | Start run cycle, macaw intro |
| `SpeedStepReached` | row index | Music intensity, speed lines VFX |
| `LaneChangeStarted` | from, to, dir, `reversal`, `queued` | Lean animation, whoosh SFX |
| `LaneChangeCancelled` | dir | none (debug) |
| `LaneBlocked` | dir | Wobble, light haptic, soft "thud" SFX |
| `JumpStarted` | `fromSlide`, `coyote`, `buffered` | Jump anim (one 0.60 s clip), dust puff |
| `JumpApex` | | Optional pose hold |
| `FastFallStarted` | | Dive pose, streak VFX |
| `Landed` | `airTicks`, `wasFastFall` | Land anim (roll when `airTicks ≥ 30` or fast-fall), dust, footstep |
| `SlideStarted` | `restart` | Knee-slide anim, leaf spray VFX |
| `SlideEnded` | reason (`Timeout`, `Jump`, `Ledge`) | Stand-up blend |
| `LeftGround` | `coyote` | Edge teeter pose |
| `Stumbled` | side/top, obstacle id, archetype | Trip anim, medium haptic, macaw "danger" squawk |
| `DazeEnded` | | Clear dazed VFX |
| `NearMiss` | obstacle id | Swish SFX, small sparkle (companion meter later) |
| `Died` | cause (`Hit`, `Fell`), archetype, obstacle id, `afterStumble` | Hit-pause, heavy haptic, death camera |

Read-only state snapshot for views (current and previous tick for interpolation): `X`, `Y`, `Z`, `Speed`,
`Locomotion`, `TargetLane`, `OccupiedLane`, `LaneMoveProgress` (0..1), `JumpPhase` (0..1), `SlideTicksLeft`,
`DazeTicksLeft`, `InvulnerableTicks`, `IsDead`.

---

## 12. Touch to commands (`TouchInputProvider` + `GestureRecognizer`)

GDD 5.1 is the source of truth; this section only pins down what the tests check.

1. A swipe is recognized while the finger is moving, the moment it has moved ≥ 28 pt from its start point within
   250 ms. Direction = dominant axis; an exact tie goes horizontal.
2. One touch makes one swipe; continuing to drag re-arms after 28 pt more in a new direction.
3. Only the first active touch counts.
4. Tap = touch down and up within 200 ms with < 12 pt movement. Double tap = second touch-down within 300 ms of the
   first tap's lift → `CompanionAssist`.
5. Recognized gestures go into a fixed queue (8); `ReadCommands(tick)` returns **one** gesture per tick, oldest first,
   so two fast swipes are never merged into one tick.
6. The run driver runs `Update` input reading before the simulation steps of the same frame, so a gesture recognized
   in a frame is simulated in that frame if at least one step is due, otherwise on the next frame.

---

## 13. Acceptance criteria

Test location: **EditMode** = `JungleBooze.Tests.EditMode` (plain C#, driven tick by tick with scripted commands and
`TestTrackQuery`). **PlayMode** = `JungleBooze.Tests.PlayMode` (scene, frame loop, views). All tick values assume the
start values in section 3; tests read them from a `RunnerConfig` built from those values.

### Forward running and speed
- **AC-01 [EditMode]** `SpeedCurve` returns 10.0 at 0 m, 11.0 at 250 m, 21.0 at 7,000 m and 21.0 at 20,000 m;
  `tutorialSpeedMps` 8.0 is used while the tutorial flag is set.
- **AC-02 [EditMode]** With the start ramp, speed at tick 0 is 5.0 m/s, at tick 15 is 7.5 m/s, and from tick 30 on equals the curve.
- **AC-03 [EditMode]** After 600 ticks at constant 10 m/s (ramp disabled), `Z` = 100.000 m ± 0.001.
- **AC-04 [EditMode]** `SpeedStepReached` fires exactly once per curve row crossed, on the tick the distance crosses it.
- **AC-05 [EditMode]** `speedMultiplier = 1.6` at curve speed 21 m/s moves HERO 0.56 m per tick, and jump airtime is still 36 ticks.

### Lanes
- **AC-06 [EditMode]** MoveRight from lane 1 changes `X` on the same tick it is processed, reaches lane-2 center
  exactly on the 7th tick, and `X` never overshoots.
- **AC-07 [EditMode]** Progress after 1/2/3 ticks is 0.265/0.490/0.673 ± 0.001 of a lane (ease-out, k = 2).
- **AC-08 [EditMode]** Double swipe (MoveRight on tick 0 and tick 1) from lane 0: the second move starts on tick 4 and
  HERO is at lane-2 center on tick 11.
- **AC-09 [EditMode]** Second same-direction swipe arriving at tick 5 of a move starts the next move on tick 5.
- **AC-10 [EditMode]** Opposite swipe during a move with no queue reverses from the current `X` and reaches the origin
  lane center 7 ticks later; `LaneChangeStarted(reversal = true)` is emitted.
- **AC-11 [EditMode]** Opposite swipe while a move is queued cancels the queued move only; HERO ends in the first
  move's lane.
- **AC-12 [EditMode]** MoveLeft in lane 0 (and MoveRight in lane 2): `X` unchanged on every following tick,
  `LaneBlocked(dir)` emitted once, outcome `Bumped`, and nothing fires later.
- **AC-13 [EditMode]** Third same-direction swipe while one move is active and one is queued: `Bumped`.
- **AC-14 [EditMode]** Lane moves during a jump, slide and fast-fall do not change `Y`, the jump tick count or the
  slide timer.
- **AC-15 [PlayMode]** On device-like frame pacing (60 fps), the lane move's visible duration measured on HERO's
  view from first movement to arrival is 117 ms ± 17 ms (≤ 140 ms hard limit).

### Jump and fast-fall
- **AC-16 [EditMode]** A ground jump lands exactly 36 ticks after the start tick, with `Y` max = 1.500 m ± 0.001 at tick 18.
- **AC-17 [EditMode]** Jump airtime and apex are identical at 8, 10, 21 and 33.6 m/s.
- **AC-18 [EditMode]** Jump while airborne does not start a second jump (no double jump); it is `Buffered`.
- **AC-19 [EditMode]** Slide at the apex (1.5 m) lands within 6 ticks; from a forced height of 4.0 m it also lands
  within 6 ticks; from 0.3 m it lands within 2 ticks.
- **AC-20 [EditMode]** After a fast-fall landing HERO slides for 39 ticks; `SlideStarted` is emitted on the landing tick.
- **AC-21 [EditMode]** Jump during a fast-fall: on landing HERO jumps instead of sliding (`JumpStarted(buffered = true)`, no `SlideStarted`).

### Slide
- **AC-22 [EditMode]** Slide on the ground lasts 39 ticks; hitbox height is 0.8 m from the start tick and 1.8 m after it ends.
- **AC-23 [EditMode]** Slide again at tick 20 of a slide: the slide ends 39 ticks after the second command.
- **AC-24 [EditMode]** Jump during a slide starts the jump on the same tick and restores the 1.8 m hitbox that tick.
- **AC-25 [EditMode]** Slide timer ending while under a high barrier: HERO keeps sliding until the standing box is clear,
  then stands up; no death.

### Input buffer and same-tick rules
- **AC-26 [EditMode]** Jump received 9 ticks before landing fires on the landing tick; received 10 ticks before landing
  it expires (outcome `Expired`) and HERO does not jump.
- **AC-27 [EditMode]** Two Jumps in the air, 12 and 5 ticks before landing: the newer one fires on landing.
- **AC-28 [EditMode]** MoveLeft + MoveRight in one tick: no movement, both `Cancelled`. Jump + Slide in one tick on the
  ground: HERO jumps, Slide is `Superseded`.
- **AC-29 [EditMode]** For a 10,000-tick random command stream (seeded), the sum of outcome counters equals the number
  of command flags received (no silent drops).

### Coyote, gaps, falling
- **AC-30 [EditMode]** Running off a ledge: a Jump on the 5th tick after leaving the ground is a full jump (36 ticks,
  1.5 m apex) with `coyote = true`; a Jump on the 6th tick is buffered and HERO falls.
- **AC-31 [EditMode]** With no input after a ledge, HERO dies with cause `Fell` when `Y ≤ −1.0 m`, and every command
  after `Y < 0` is `Ignored`.
- **AC-32 [EditMode]** A buffered Jump fires on entering `Coyote` if it is still within 9 ticks (jump pressed just before the edge while landing onto it).
- **AC-33 [EditMode]** A jump over a 4 m gap at 10 m/s started 1 m before the edge lands on the far side; started 5.5 m
  before the edge, it lands in the gap and dies with `Fell`.

### Collisions
- **AC-34 [EditMode]** Running straight into a full block: death on the first overlapping tick, cause
  `Hit(FullBlock, id)`.
- **AC-35 [EditMode]** Jumping over a low barrier with a correctly timed jump: no contact, `NearMiss` emitted if the
  vertical clearance was ≤ 0.35 m.
- **AC-36 [EditMode]** Running (not sliding) into a high barrier: death. Sliding under it: no contact.
- **AC-37 [EditMode]** Jumping into a high barrier from below during the arc: death (GDD 5.3.7).
- **AC-38 [EditMode]** Lane move into the side of a full block whose front HERO has already passed: `Stumbled(side)`,
  HERO is back at the origin lane center 9 ticks later, no death, `DazeTicksLeft = 180`.
- **AC-39 [EditMode]** Coming down onto the top of a low barrier: `Stumbled(top)`, no bounce, HERO lands on the ground beyond.
- **AC-40 [EditMode]** A second stumble 179 ticks after the first is lethal with `afterStumble = true`; at 181 ticks it is
  a new stumble.
- **AC-41 [EditMode]** Edge forgiveness: with a test obstacle 2.4 m wide in lane 1, HERO moving from lane 1 to lane 2
  with `|X| ≥ 1.44 m` is not hit; with `|X| = 1.40 m` the normal overlap rule applies.
- **AC-42 [EditMode]** No tunneling: a 0.1 m deep full block is hit at 40 m/s (0.67 m per tick).
- **AC-43 [EditMode]** `InvulnerableTicks > 0`: obstacles cause no death and no stumble; a gap still kills.
- **AC-44 [EditMode]** Exact-tie entry (x and z start overlapping at the same time) is a stumble, not a death.

### Pause
- **AC-45 [EditMode]** `PauseResumed` clears the buffered Jump and the queued lane move, but the active lane move,
  jump arc, slide timer, coyote counter and daze counter continue from the same values.
- **AC-46 [EditMode]** A replay recorded with a pause in it reproduces the same state hash at every tick as the live run.
- **AC-47 [PlayMode]** Pause for 5 s real time, then resume: `Tick` does not change while paused or during the 1.5 s
  countdown, and the first frame after the countdown runs at most 1 step.
- **AC-48 [PlayMode]** App background (`OnApplicationPause(true)`) and focus loss pause the run and show the pause menu;
  returning does not auto-resume.
- **AC-49 [PlayMode]** A swipe started before pause and continued after resume produces no command; touches during
  the countdown produce no command.

### Touch to commands
- **AC-50 [EditMode]** Timestamped samples: 27 pt in 100 ms → no swipe; 28 pt at 240 ms → swipe recognized on that
  sample; 28 pt at 260 ms → no swipe.
- **AC-51 [EditMode]** A 45° diagonal exactly → horizontal; 20 pt right + 30 pt up → Jump.
- **AC-52 [EditMode]** Continuous drag right 30 pt then up 30 pt → MoveRight then Jump, delivered on two consecutive
  ticks.
- **AC-53 [EditMode]** Two taps 250 ms apart → `CompanionAssist`; 350 ms apart → nothing; second finger touches are ignored.
- **AC-54 [EditMode]** `ReadCommands` returns at most one gesture per tick, in order, and does not allocate.

### Camera and presentation
- **AC-55 [PlayMode]** First visible HERO movement after a recognized swipe: ≤ 1 rendered frame (≤ 17 ms at 60 fps).
- **AC-56 [PlayMode]** After a lane move settles, camera `x` = 0.7 × HERO `x` ± 0.01 m; camera reaches 90% of the
  way within 3 × smooth time.
- **AC-57 [PlayMode]** At 21 m/s, a point on the track 34 m ahead in each lane is inside the viewport.
- **AC-58 [PlayMode]** On `Died`, HERO and obstacle views stay frozen for 350 ms ± 17 ms; the camera holds for a
  further 800 ms ± 17 ms.
- **AC-59 [PlayMode]** Each event in section 11 triggers its view hook exactly once (checked with a test view that
  counts calls) in a scripted run that exercises all of them.

### Config, determinism, performance
- **AC-60 [EditMode]** Every asset in section 3 loads, passes validation, and rejects out-of-range values (for example
  `slidingHeightM ≥ standingHeightM`, unsorted speed rows).
- **AC-61 [EditMode]** `ToConfig()` produces the tick values and derived values in section 3.1 (7, 4, 36, 6, 39, 9, 5,
  30, 9, 180; g = 33.33, v₀ = 10.0).
- **AC-62 [EditMode]** Same seed + same command stream → identical state hash at every tick over 10 runs; feeding the
  same stream through 30 fps, 60 fps, 120 fps and jittery frame pacing gives identical hashes.
- **AC-63 [EditMode]** No `UnityEngine.Time`, `UnityEngine.Random`, `System.Random` or physics calls in runner
  simulation code (reflection or source scan test).
- **AC-64 [PlayMode]** A 60 s scripted run with constant input allocates 0 bytes per frame after warm-up
  (`Is.Not.AllocatingGCMemory()`).
- **AC-65 [PlayMode]** Runner simulation step (movement + collisions with 30 obstacles in range) ≤ 0.5 ms on the
  editor benchmark; the device check (≤ 1 ms on iPhone 11 / SE 2nd gen) is done by perf-engineer.

---

## 14. Changes to the GDD made with this spec

| GDD section | Change | Why |
|---|---|---|
| 5.2 | Lane switch 120 ms is now 7 ticks (117 ms); coyote 80 ms is 5 ticks (83 ms); fast-fall ≤ 100 ms (6 ticks), still inside ≤ 120 ms | Whole ticks at 60 Hz |
| 5.2 | "Swipe to first visible movement: same frame" → "≤ 1 rendered frame (≤ 17 ms)" | A gesture recognized after the frame's steps ran is simulated next frame; "same frame" cannot be guaranteed or tested honestly |
| 5.3 | Added rule 8 (lane reversal) and rule 9 (side and top contacts are stumbles; second stumble within 3 s is lethal) [ASSUMED] | Collision categories were not defined |
| 5.4 | Stumble uses a medium haptic | New event |
| 6 | Player hitbox depth 0.5 m | Needed for collisions |
| 16 | Buffer and coyote moved from `InputTuning` to `RunnerTuning`; new `RunnerPresentationTuning` asset | Buffer and coyote live in the simulation (ARCHITECTURE 5.3) |
| 22 | Week 1 specs are now `specs/001-player-movement.md` (and `specs/bot-player.md`) | One spec instead of two |

---

## 15. Simulation targets (balance-simulator)

Week 1 has no track generator yet. The balance-simulator runs a **movement gauntlet**: a seeded `TestTrackQuery`
with the reference obstacles (9.2) and gaps (2–4 m), placed single or in pairs, with the spacing rules of the GDD
11.2 tiers. Report per run: seed, config hash, speed, tier spacing, outcome counters (I9), stumbles, deaths by cause.

| # | Target | Value |
|---|---|---|
| S1 | **Oracle bot** (perfect timing, no reaction delay) on 10,000 gauntlet segments per speed in {8, 10, 13.5, 17.5, 21, 33.6} m/s and per tier spacing (0.90 s down to 0.45 s) | **0 deaths, 0 stumbles.** Any failure means the movement numbers make a spacing rule impossible and must be reported to the designer before week 2. |
| S2 | Success window for the oracle (range of input ticks that clear the obstacle), per archetype per speed | Jump over low barrier **≥ 15 ticks (250 ms)**; slide under high barrier **≥ 30 ticks**; latest lane dodge of a full block **≥ 3 ticks** before front contact |
| S3 | Jump-then-jump over two low barriers at 21 m/s | Feasible for every spacing from 0.45 s to 1.0 s |
| S4 | Expert bot (from `bot-player.md`) on tier-1 spacing at 10 m/s, 60 s segments | ≥ 99% survive |
| S5 | Average bot, same setup | ≥ 90% survive; `Expired` buffered jumps ≤ 5% of buffered jumps |
| S6 | New bot, tier-1 spacing at 8 m/s, 30 s segments | ≥ 60% survive |
| S7 | Death causes (all bots) | "Tripped twice" ≤ 15% of deaths; deaths from an old-lane obstacle while ≥ 60% into a new lane = **0** |
| S8 | Determinism | 1,000 seeded runs recorded and replayed: 100% identical final state hash |
| S9 | Cost | Mean simulation step ≤ 0.5 ms in the headless editor run; 0 allocations per step |

S4–S6 are first estimates; the full-run targets (GDD 11.3) are checked once the generator exists in week 2.
