# Spec 101: Movement, Input, Collisions and Camera (Phase 1 "Feel")

**Owner:** game-designer | **Implementers:** gameplay-engineer (simulation, input), ui-engineer/gameplay-engineer
(camera rig view), balance-simulator (bots, sim targets), qa-engineer (feel course test plan)
**Status:** v1, ready for implementation | **Last updated:** 2026-10-09
**Sources:** `design/aurelia/BLUEPRINT.md` Parts III.2, V, VI, XVII, XXXVI, XLVI, LIV; `design/aurelia/GDD.md` §5–7, §11;
`design/aurelia/ART_DIRECTION.md` §9; `docs/ARCHITECTURE.md` §4–5 (deterministic core).
**Not in scope:** swimming, vines and canopy beams (spec 103), chunks/routes generation (spec 102), power-up effects other
than Shield's hit rule (later spec). Nothing here uses lanes; the archived lane spec 001 must not be reused.

---

## 1. Player story and purpose
"I put my thumb down and Pista goes exactly where I mean, right now. Jumps and slides fire the instant I flick, the
arc is always the same, and when I get hit I know exactly why." Movement is pillar 1 and design principle 2
("if movement isn't satisfying, nothing else matters"). Phase 1 passes only when the owner says a 60-second run
on the phone is fun by itself (§8).

## 2. Simulation model

### 2.1 Frame of reference and determinism
- The runner lives in **path space**: `s` = distance along the path centreline (m), `x` = lateral offset from the
  centreline (m, + = right), `y` = height (m) in the path frame. Curves, slopes and world transforms are visual only;
  the view maps `(s, x, y)` to world space through the path spline. The simulation never uses world space.
- Fixed timestep: 60 ticks/s (`dt` = 1/60 s) from the injected `ITimeSource`. No `Time.deltaTime`,
  `UnityEngine.Random`, `System.Random` or physics engine in simulation code. Any randomness (none needed in this
  spec) uses an injected `IRandom` stream.
- All tunables below live in ScriptableObjects (§7). Code holds no tuning literals.
- Order inside one `Step()`: (1) read `InputFrame`, (2) resolve discrete command (buffer, coyote, ceiling hold),
  (3) lateral update, (4) vertical update and ground/ledge resolution, (5) forward advance, (6) collisions,
  (7) timers (slide, i-frames, stumble, regen), (8) emit events to the ring buffer.
- Path queries go through an interface (`IPathQuery`) so the feel course, unit tests and later chunks provide the
  same data: half-width `H(s)`, floor height `floorY(s, x)` or "no floor", obstacle boxes overlapping an `s` range,
  and split points.

### 2.2 Forward speed
`v(d) = v0 + (vMax − v0) · (1 − e^(−d / dScale))`, where `d` = run distance.
| Name | Value | Unit | Notes |
|---|---|---|---|
| `v0` | 10.0 | m/s | Start speed (a fast trail runner; the world must be readable) |
| `vMax` | 16.0 | m/s | Asymptote |
| `dScale` | 3,000 | m | v(500)=10.92, v(1,500)=12.36, v(3,000)=13.79, v(5,000)=14.87, v(8,000)=15.58 |
| `startRampTime` | 0.8 | s | 0 → v0 at run start, ease-out quad; inputs are live during the ramp |
| `stumbleSpeedFactor` | 0.80 | × | Instant multiplier on a minor hit |
| `stumbleRecoverTime` | 0.8 | s | Linear back to 1.0× |
| `reviveRampFrom` / `reviveRampTime` | 0.5 / 0.5 | × / s | After a revive |
Forward speed is never changed by steering, jumping or sliding (predictable arcs). Distance `d` accumulates actual
`s` travelled.

### 2.3 Lateral movement (steering and dodge)
The input sets a **lateral target** `xT`; the body follows with a speed- and acceleration-limited servo.
Each tick:
1. `xT += lateralDelta` (from the input frame, metres); then clamp `xT` to `[−xLim, +xLim]`, `xLim = H(s) − edgeMargin`.
   The clamp is applied to the target itself, so dragging past the edge never builds up "debt": reversing the
   finger moves Pista back on the very next tick.
2. `vDes = clamp((xT − x) / tau, −vLatMax, +vLatMax)`.
3. `vLat` moves toward `vDes` by at most `accelLat · dt` when speeding up or keeping direction, `decelLat · dt` when
   slowing or reversing.
4. Soft edge: if `|x| > xLim − softZone` and `vLat` points outward, outward `vLat` is scaled by
   `lerp(1.0, softEdgeMinFactor, penetration / softZone)`. When `|x| ≥ xLim − 0.05` while pushing outward, emit
   `EdgeBrush` (view: foliage brush VFX and sound, no damage, no haptic).
5. `x += vLat · dt`, then hard clamp to `[−xLim, +xLim]` (zero `vLat` on clamp).
6. Narrowing path: if `|x| > xLim` because `H` shrank, `x` is pushed inward at `narrowPushSpeed`. Never damage.

| Name | Value | Unit | Notes |
|---|---|---|---|
| `tau` | 0.075 | s | Servo time constant (tight, no float) |
| `vLatMax` | 11.0 | m/s | |
| `accelLat` | 150 | m/s² | ≥ 5 cm movement within 2 ticks of a step input |
| `decelLat` | 180 | m/s² | Snappier stops than starts: no overshoot |
| `edgeMargin` | 0.30 | m | Runner half-width 0.25 + 0.05 |
| `softZone` | 0.35 | m | |
| `softEdgeMinFactor` | 0.40 | × | |
| `narrowPushSpeed` | 6.0 | m/s | Path narrowing rate is capped by spec 102 |
| `airLateralFactor` / `slideLateralFactor` | 1.0 / 1.0 | × | Full control everywhere (responsiveness first) |

**Dodge** (`DodgeLeft`/`DodgeRight` command, sign `dir`):
- `xT = clamp(xOrigin + dir · max(dodgeDistance, dir · (xT − xOrigin)), −xLim, +xLim)`, where `xOrigin` is the
  target at the latest `TouchBegan` of the gesture that produced the dodge (keyboard: the current `xT`). The dodge
  guarantees a total shift of at least `dodgeDistance` from where the gesture started; it never cancels drag motion
  already made in that direction.
- For `dodgeBoostTime`, `vLatMax` → `dodgeVLatMax` and `accelLat` → `dodgeAccel`. Emit `Dodge` (view: dodge
  animation, whoosh).
- Works on the ground, in the air and while sliding. A second dodge during the boost stacks from the new target.
| Name | Value | Unit |
|---|---|---|
| `dodgeDistance` | 2.2 | m |
| `dodgeVLatMax` | 14.0 | m/s |
| `dodgeAccel` | 220 | m/s² |
| `dodgeBoostTime` | 0.22 | s |

Reference result (EditMode-checkable; continuous-time estimates 0.15 s / 0.30 s / 0.29 s, limits allow for tick
discretization): from rest, a 2.0 m target step reaches 63% (1.26 m) in ≤ 0.17 s and within 0.10 m in ≤ 0.33 s with
≤ 0.02 m overshoot. A dodge from rest covers 2.2 m to within 0.10 m in ≤ 0.32 s.

### 2.4 Vertical movement: jump, gravity, coyote, buffer
Jump sets `vy = jumpVelocity`. Gravity is asymmetric with an apex hang:
- Rising (`vy > 0`): `g = gUp`. Falling: `g = gDown`. If `|vy| < apexHangThreshold`: `g ×= apexHangFactor`.
- Fast-fall active: `g = gDown · fastFallGravityFactor`, no apex hang.
| Name | Value | Unit | Notes |
|---|---|---|---|
| `jumpVelocity` | 9.33 | m/s | = 2·H/tUp for H 1.40 m, tUp 0.30 s |
| `gUp` | 31.1 | m/s² | |
| `gDown` | 42.0 | m/s² | 1.35 × gUp: quick, weighty landing |
| `apexHangThreshold` | 1.0 | m/s | |
| `apexHangFactor` | 0.60 | × | |
| `maxFallSpeed` | 24 | m/s | |
| Resulting apex | 1.41 ±0.02 | m | Clears low obstacles ≤ 1.0 m authored (0.9 m effective + feet margin) |
| Resulting airtime (flat ground) | 0.60 ±0.02 | s | Jump length = airtime × speed: 6.0 m at 10 m/s, 9.6 m at 16 m/s |
| `coyoteTime` | 0.10 | s | Jump still allowed this long after walking off an edge |
| `inputBufferTime` | 0.15 | s | A jump/slide that can't execute yet is held this long |
| `stepUpHeight` | 0.35 | m | Floor rises up to this are walked up without leaving the ground |
| `stepDownSnap` | 0.35 | m | Floor drops up to this keep Pista grounded; larger drops make her airborne (coyote starts) |
| `fallKillDepth` | 1.20 | m | Below the lip of the floor she left over a gap → major fall |
| `ledgeAssistReach` | 0.30 | m | Far lip within this distance ahead … |
| `ledgeAssistDrop` | 0.25 | m | … and feet no lower than this under the lip → snap onto the lip (pull-up animation) |
| `hardLandingFall` | 2.5 | m | Fall height for the hard-landing event (camera/haptic) |
| `softLandingFall` | 1.0 | m | Minimum fall height for the landing haptic |

Airtime and arc are identical at every forward speed (predictable arc). No double jump without the Double Jump
ability (later spec). There is no variable jump height (swipes have no hold).

### 2.5 Slide and fast-fall
| Name | Value | Unit | Notes |
|---|---|---|---|
| `slideDuration` | 0.65 | s | Fixed time |
| `slideHitboxHeight` | 0.70 | m | |
| `fastFallSpeed` | 14.0 | m/s | On swipe down in air: `vy = min(vy, −fastFallSpeed)` |
| `fastFallGravityFactor` | 2.0 | × | Until landing |
| `ceilingHoldTime` | 0.35 | s | See jump-under-ceiling rule |
Rules:
- Swipe down on the ground → slide starts this tick (hitbox drops this tick).
- Swipe down while sliding → slide timer restarts.
- Swipe up while sliding → slide ends and the jump starts the same tick (slide→jump cancel), unless a ceiling rule applies.
- Swipe down in the air → fast-fall; on landing, a full slide starts automatically.
- **Ceiling guard (end of slide):** if the slide timer expires while a High obstacle is over the standing hitbox,
  the slide extends tick by tick until clear. Pista never stands up into a branch.
- **Ceiling hold (jump under a ceiling):** a jump command while a High obstacle is over the standing hitbox is held
  and executes on the first tick with clearance, if that comes within `ceilingHoldTime`; otherwise it is dropped and
  `InputDropped(reason: Ceiling)` is emitted (debug overlay shows it).

### 2.6 Runner hitbox (shrunk vs the visual, edge forgiveness)
| State | Width (x) | Depth (s) | Height |
|---|---|---|---|
| Running | 0.50 m | 0.40 m | 1.50 m (visual ~1.65 m) |
| Airborne | 0.50 m | 0.40 m | 1.30 m (tuck) from feet `y` |
| Sliding | 0.50 m | 0.70 m | 0.70 m |
Obstacle hitboxes are their authored boxes shrunk by `obstacleShrinkX` = 0.10 m per side, Low tops lowered by
`lowTopForgiveness` = 0.10 m, High bottoms raised by `highBottomForgiveness` = 0.10 m.

### 2.7 Discrete command resolution (one per tick)
| State | Jump | Slide | Dodge |
|---|---|---|---|
| Grounded | Jump now (ceiling hold applies) | Slide now | Dodge |
| Coyote window | Jump now | Slide now (ground slide) | Dodge |
| Airborne | Buffer 150 ms (executes on landing) | Fast-fall | Dodge |
| Sliding | Cancel → jump (ceiling hold applies) | Restart timer | Dodge |
| Stumbling | as Grounded/Airborne | as Grounded/Airborne | Dodge |
| Dead / paused | ignored | ignored | ignored |
- Only one buffered command exists; a newer command replaces it ("latest intent wins").
- On the landing tick the order is: land → start auto-slide (if fast-falling) → execute buffered command. So
  "fast-fall, then swipe up before landing" ends in a jump on the landing tick.
- A buffered command that expires emits `InputDropped(reason: Buffer)`.

## 3. Input

### 3.1 Scheme decision: hybrid "Steer + Flick"
- **Steering = relative drag** (finger delta → lateral target). Rationale: free horizontal movement (Blueprint V)
  needs analogue precision to thread gaps, line up coin trails and choose fork sides; relative mapping works from
  anywhere on screen, in both orientations, with either thumb, and never jumps when a touch begins.
- **Dodge = quick horizontal flick** (fixed 2.2 m burst). Rationale: Blueprint V lists "Dodge: horizontal swipe";
  players trained on lane runners swipe instinctively, and an emergency sidestep must not depend on drag distance.
- Rejected: pure drag (no emergency move, flicks feel dead); pure swipe-dodge (that is a lane game in disguise and
  kills precision); absolute finger-to-position mapping (the thumb covers the path in portrait, breaks two-thumb landscape).

### 3.2 Per-tick input contract
`InputFrame { InputCommand Commands; short LateralDeltaMm; }`
- `Commands` flags: `Jump`, `Slide`, `DodgeLeft`, `DodgeRight`, `TouchBegan`. At most one of Jump/Slide/Dodge* per tick.
- `LateralDeltaMm`: drag delta for this tick in integer millimetres (after sensitivity). Quantization keeps replays
  exact; the sub-millimetre remainder is carried to the next tick (no drift).
- **Note for tech-architect:** this replaces the lane-era `MoveLeft/MoveRight` byte commands in ADR 0002 /
  ARCHITECTURE §5.3 and needs an ADR amendment and a replay format version bump. Bots and replays produce the same struct.

### 3.3 Gesture recognizer (plain C#, presentation side, EditMode-tested with timestamped samples)
Units are iOS points (pt). Samples: `(touchId, phase, positionPt, timestampS)`.
| Name | Value | Unit | Notes |
|---|---|---|---|
| `dragSensitivity` | 0.040 | m/pt | 175 pt of thumb travel = full 7 m path. Settings multiplier 0.5–2.0 |
| `touchDeadZone` | 4 | pt | No lateral output until the touch moves this far; then the full distance is applied (no lost travel) |
| `verticalIntentAngle` | 30 | ° from vertical | A sample whose direction is within this of vertical contributes 0 lateral delta |
| `swipeDistance` | 24 | pt | Vertical swipe threshold … |
| `swipeWindow` | 0.12 | s | … reached within this window (≥ 200 pt/s) |
| `swipeAngleTolerance` | 35 | ° from vertical | `|Δy| ≥ 1.43·|Δx|` over the window |
| `swipeRearmTime` | 0.18 | s | Same-direction re-fire on the same touch needs a fresh 24 pt after this; opposite direction re-fires immediately |
| `flickMaxDuration` | 0.22 | s | Quick flick: touch lifetime ≤ this … |
| `flickMinDistance` | 30 | pt | … and `|Δx|` ≥ this … |
| `flickAngleTolerance` | 35 | ° from horizontal | … and `|Δx| ≥ 1.43·|Δy|`, and no vertical swipe fired on this touch → Dodge |
| `releaseFlickSpeed` | 1,200 | pt/s | Longer drags: horizontal speed over the last 0.06 s at release ≥ this → Dodge. Setting "Flick to dodge on release", default on |
| `maxTrackedTouches` | 2 | | |
| `commandQueueSize` | 16 | | Fixed-size, no allocation |

Behaviour:
1. Vertical swipes fire **when the threshold is crossed** (not on release): latency is the time to move 24 pt.
2. Steering and swiping share one touch: a player can drag, swipe up mid-drag, and keep dragging. The
   vertical-intent filter stops the swipe from also steering.
3. Two touches (landscape two-thumb): touch A (first down) owns steering and can swipe/flick; touch B can swipe and
   flick but never steers. If A lifts while B is down, B becomes the steering owner from its current position
   (no target jump).
4. Taps do nothing. Touches that begin on a UI control (pause) belong to the UI.
5. iOS edge gestures: the run screen defers system gestures on all edges (`preferredScreenEdgesDeferringSystemGestures`)
   so a swipe up from the bottom jumps instead of leaving the app; the home indicator auto-hides during a run.
6. Dispatch: Update reads touches before the simulation steps that frame. Discrete commands go to the queue and are
   delivered one per tick in recognition order. Lateral delta accumulated since the last tick is delivered on the
   next tick; if one frame runs several ticks, the delta is split evenly across them (remainder on the last).
7. A cancelled touch (orientation change, system interruption) emits no flick and no further deltas.

### 3.4 Keyboard and mouse (editor and Mac builds)
| Input | Action |
|---|---|
| A / D or ← / → (held) | Lateral target moves at `keyboardLateralSpeed` = 10 m/s |
| W / ↑ / Space | Jump |
| S / ↓ | Slide |
| Q / E | Dodge left / right |
| Mouse left-drag | Emulates one touch; pixels → pt by `editorPixelsPerPoint` (default 2.0) |
| Esc | Pause · R restart · F1 debug overlay (hitboxes, buffer state, `xT`, dropped inputs) · O toggle camera orientation profile |

## 4. Collisions, health and failure

### 4.1 Obstacle classes (Phase 1 set)
| Class | Shape | Avoid by | Contact result |
|---|---|---|---|
| Low | Top ≤ 1.0 m authored | Jump, or steer if not full width | Minor "Trip" |
| High | Bottom 0.85–1.2 m authored, extends up | Slide, or steer | Minor "HeadClip" |
| Blocker | Full height | Steer / dodge | Frontal: **Major "Crash"**; side: Minor "SideClip" |
| Thorns | Soft hazard, any height ≤ 1.0 m | Steer or jump | Minor "Thorns" |
| Gap | No floor over an `s` range (full or partial width) | Jump or steer | **Major "Fall"** past `fallKillDepth` |
| Walkable top (flag on Low) | Logs, flat rocks | — | Landing on top from above = run across it, no damage |

Rules:
- **Frontal vs side (blocker):** at the first overlapping tick, if the runner's centre `x` lies inside the blocker's
  **crash core** (hitbox narrowed by `crashCoreInset` = 0.20 m per side, minimum core width 0.20 m) → Crash.
  Otherwise → SideClip: −1 health, `xT` set to the near free side (`blocker edge ± 0.30 m`), dodge boost applied.
  Example: a 1.2 m rock has a 1.0 m hitbox and a 0.6 m crash core: only a near-centre hit kills.
- **Walkable top:** if at the first overlapping tick the feet are ≥ (effective top − 0.15 m) and `vy ≤ 0`, the
  obstacle becomes floor for its extent.
- Each obstacle resolves **once** per run: after its first contact it is ignored.
- Path edges never damage.
- During invulnerability, Low/High/Blocker/Thorns contacts are ignored (Pista ghosts through, view blinks at 8 Hz);
  gaps still apply (ledge assist still helps).
- Several contacts on one tick: Major beats Minor; among Minors, only one segment is lost.

### 4.2 Health
| Name | Value | Unit |
|---|---|---|
| `maxHealth` | 3 | segments |
| `invulnerableTime` (after a minor hit) | 1.2 | s |
| `regenDistance` | 350 | m without damage → +1 segment (max 3) |
| `ftueHealthFloorTime` | 60 | s (first run only: health cannot drop below 1) |
- Minor hit: −1 segment, stumble (§2.2), i-frames, event `Hit(class, obstacleId)`. Health reaching 0 → Dead
  (cause `Health`).
- Major: Dead at any health (cause `Crash` or `Fall`). Forward speed → 0 within 0.1 s (crash) or the fall continues
  visually for 0.5 s before the fade.
- **Shield** (power-up): consumes on the next Minor or Crash (not Fall), grants 1.0 s i-frames, no stumble, no health
  loss. If i-frames are already active, the shield is not consumed.
- **Revive** (from the GDD): restores 3 segments, places Pista at the last safe point (most recent grounded position
  ≥ 6 m before the hazard, `x` clamped to a free spot), removes obstacles in the next 30 m, 2.0 s i-frames, speed ramp §2.2.

### 4.3 Stumble
Speed ×0.80 recovering over 0.8 s; 0.35 s stumble animation layered on the run; all inputs stay live. The small
speed loss is deliberate: it reads as a hit without causing a second, unavoidable one.

## 5. Camera (presentation; plain C# rig model, EditMode-testable with an explicit `dt`)
The rig reads interpolated simulation state (`InterpolationAlpha`) and may use frame time, because it is not
simulation. It never writes simulation state. All smoothing is critically damped (half-life parameterization) and
frame-rate independent.

| Parameter | Landscape | Portrait | Notes |
|---|---|---|---|
| Offset back (along path tangent) | 5.5 m | 6.2 m | |
| Height above Pista's ground level | 2.4 m | 3.0 m | |
| Pitch (down) | 9° | 12° | Horizon upper third (landscape) / ~35% from top (portrait) |
| Vertical FOV (at `v0`) | 55° | 65° | |
| FOV gain at `vMax` | +6° | +5° | Linear in `(v − v0)/(vMax − v0)`, half-life 0.5 s |
| Lateral follow | 0.70 · x | 0.80 · x | Camera keeps the path partly centred, so edges stay readable; half-life 0.10 s |
| Path yaw follow half-life | 0.25 s | 0.25 s | Smooths spline curvature |
| Ground-level follow half-life | 0.30 s | 0.30 s | Floor height changes (ramps, ledges) |
| Air follow | 35% of jump height, half-life 0.20 s | 30% | Jumps visible, horizon stable |
| Slide dip | −0.25 m, half-life 0.12 s | −0.20 m | |
| Bank (roll) | ≤ 2.0°, ∝ `vLat / vLatMax`, half-life 0.12 s | ≤ 1.5° | |
| Pista on screen | 14–18% of height, feet at ~28% from bottom | ~12% of height, feet at ~22% from bottom | Art direction §9 |
| Path visible ahead | ≥ 35 m | ≥ 45 m | |

**Shake** (only these events; none while running): hard landing 0.04 m / 0.18 s; minor hit 0.07 m / 0.25 s;
crash 0.12 m / 0.35 s; 18 Hz smooth noise; hard caps 0.12 m position, 1.0° rotation; new shake replaces a weaker one.
**Reduced Motion** (setting, defaults on when iOS Reduce Motion is on): shake 0, FOV gain halved, bank 0, slide dip halved.
**Orientation change:** the rig blends to the other profile over 0.4 s; the simulation is untouched; active touches are cancelled.
**Camera corridor rule:** chunks and the feel course keep a clear box from Pista to the camera (+0.5 m margin); no
camera collision logic in Phase 1.

## 6. Feel test course (gray-box, hand-authored, ~61 s)
Course path: straight, flat, default width 7.0 m (`H` = 3.5), starts at `s` = 0, finish arch at 640 m.
Gray-box colours: path light grey, Low = tan, High = brown, Blocker = dark grey, Thorns = crimson `#9E2238` with spikes,
coins = gold, safe branch tint green, risky tint orange. The course is data (a ScriptableObject `FeelCourse`), not
scene geometry, so tests load it.

| Section | `s` (m) | Content | Exercises |
|---|---|---|---|
| C1 Start | 0–40 | Wide 8 m path, coin line on the centre | Start ramp, nothing to fail |
| C2 Weave | 40–100 | Coin trail, sine `x = 2.5·sin(2π(s−40)/30)` | Drag steering precision |
| C3 Slalom | 100–150 | Blockers 1.4 m wide at x = −1.5 (110), +1.5 (125), −1.5 (140) | Steering around blockers, side-clip forgiveness |
| C4 Jumps | 150–210 | Full-width Low 0.6 m at 160, 185; log 0.9 m (walkable top) at 205 | Jump timing, walkable top |
| C5 Slides | 210–260 | Full-width High (bottom 1.0 m) at 220, 240 | Slide |
| C6 Combos | 260–300 | High 270 → Low 280 (slide→jump); Low 292 → High 300 | Slide→jump cancel; buffered slide on landing |
| C7 Gaps | 300–340 | Full-width gaps 315–318 (3.0 m) and 330–334.5 (4.5 m) | Jump across gaps, ledge assist, falls |
| C8 Dodge | 340–380 | Blocker spanning x −0.5…+3.5 at 355; blocker x −3.5…+0.5 at 365 | Flick dodge / fast steering (≥ 1.3 m shift, typically ~3 m, in ~0.9 s) |
| C9 Ledge | 380–440 | Width 7.0 → 2.4 m over 380–404, 2.4 m ledge 404–430 (coins), back to 7.0 m by 440 | Soft edges, narrowing push |
| C10 Fast-fall | 440–470 | Log 0.9 m at 450; ground coin cluster 452–455 (normal arc overflies it); High at 462 | Optional fast-fall + auto-slide |
| C11 Mix | 470–520 | Low 480; Blocker 1.2 m at x = +1.0 (490); High 500; Thorns patch x −3.5…−1.0 at 506; Low 512 | Rhythm, mixed classes |
| C12 Fork | 520–600 | Divider Blocker 1.2 m wide at x = 0 from 530 to 590 (front at 530) with 0.6 m nudge zone (spec 102 §3.3). Left/safe: 3.5 m wide, one Low at 560. Right/risky: 2.4 m wide, ramp up to +1.0 m over 535–545, gap 2.5 m at 560, High at 575, coins ×2. Merge at 590 | Route choice, nudge, risky reward |
| C13 Finish | 600–640 | Cooldown, finish arch at 640 → results overlay (time, hits, coins, dropped inputs) | |

### 6.1 "Feels good" metrics
| # | Metric | Target | How measured |
|---|---|---|---|
| M1 | Command → state change | Same tick (0 added ticks) | EditMode |
| M2 | Touch → visible motion | ≤ 50 ms at 60 fps | 240 fps slow-motion video on the device |
| M3 | Lateral onset / settle | ≥ 5 cm in 2 ticks; 2 m step within 0.10 m in ≤ 0.33 s; overshoot ≤ 0.02 m | EditMode |
| M4 | Gesture classification | ≥ 98% correct; drag→false jump ≤ 1% | 200 recorded gestures from 5 testers, both orientations |
| M5 | No unfair deaths | Perfect bot clears the course at any constant speed 10–16 m/s with 0 hits; every hit names its obstacle and cause | EditMode + sim |
| M6 | Visibility | Every obstacle on screen ≥ 1.5 s before contact at `vMax` | PlayMode camera test |
| M7 | Frame pacing | 60 fps, no frame > 25 ms on the minimum device | performance-engineer |
| M8 | Learnability | 5 testers × 3 runs: median hits on run 3 ≤ 2; "controls do what I mean" ≥ 4/5; no "didn't register" report the replay can't explain | Playtest |
| M9 | Owner gate | "Is just running fun?" yes on the phone, in the chosen orientation | Owner |

### 6.2 Simulation targets (balance-simulator)
Bot profiles (own `IRandom` stream): Perfect (0 ms reaction, exact), Average (reaction 260 ms, lateral noise σ 0.25 m,
gesture miss 3%), Novice (380 ms, σ 0.40 m, miss 8%). On the feel course, 1,000 seeds each:
Perfect 100% finish, 0 hits · Average ≥ 90% finish, mean hits ≤ 1.5 · Novice ≥ 60% finish, mean hits ≤ 3.
Report the deadliest obstacle per profile; any obstacle killing > 10% of Average runs is a course or tuning bug.

## 7. Config ScriptableObjects (`Assets/_Game/Config/Movement/`)
| Asset | Fields (from this spec) |
|---|---|
| `RunSpeedConfig` | §2.2 table |
| `LateralMovementConfig` | §2.3 tables (incl. dodge) |
| `JumpSlideConfig` | §2.4, §2.5 tables |
| `HitboxConfig` | §2.6 table, `obstacleShrinkX`, `lowTopForgiveness`, `highBottomForgiveness`, `crashCoreInset` |
| `HealthConfig` | §4.2 table, stumble values |
| `GestureConfig` | §3.3 table, `keyboardLateralSpeed`, `editorPixelsPerPoint` |
| `CameraProfile` ×2 | §5 table (Landscape, Portrait), shake values, reduced-motion factors |
| `FeelCourse` | §6 layout |
Validation (editor): `jumpVelocity`/gravities must give apex 1.35–1.50 m and airtime 0.55–0.65 s, else a warning.

## 8. Acceptance criteria (EditMode unless stated)
Movement
- **AC-101-01** Speed follows §2.2 within 0.01 m/s at d = 0, 500, 1,500, 3,000, 8,000 m (after the start ramp).
- **AC-101-02** Start ramp reaches `v0` at exactly 48 ticks; inputs during the ramp are applied.
- **AC-101-03** Same seed + same `InputFrame` stream + same config → bit-identical runner state after 3,600 ticks, run twice and with frames split into 1–5 ticks per frame.
- **AC-101-04** A lateral target step of +2.0 m from rest: ≥ 0.05 m after 2 ticks, ≥ 1.26 m by 0.17 s, within 0.10 m by 0.33 s, overshoot ≤ 0.02 m. A dodge from rest: within 0.10 m of 2.2 m by 0.32 s.
- **AC-101-05** Dragging the target 3 m past the edge then reversing 1 cm moves `xT` inward on the next tick (no debt).
- **AC-101-06** Pista never leaves `[−xLim, +xLim]`; pushing outward at the edge emits `EdgeBrush` and never `Hit`.
- **AC-101-07** When `H` narrows below `|x|`, `x` moves inward at ≤ 6 m/s with no damage.
- **AC-101-08** Dodge from a touch origin with 1.0 m of same-direction drag ends with `xT` = origin ± 2.2 m; with 2.8 m of drag it ends at origin ± 2.8 m (dodge never pulls back).
- **AC-101-09** Dodge works grounded, airborne and sliding and never changes `y`, `vy` or slide state.
- **AC-101-10** Jump on flat ground: apex 1.41 ±0.02 m, airtime 0.60 ±0.02 s, identical at 10 and 16 m/s.
- **AC-101-11** Jump within 6 ticks after walking off an edge executes (coyote); at 7 ticks it is buffered/dropped.
- **AC-101-12** Jump received 9 ticks before landing executes on the landing tick; at 10 ticks it is dropped with `InputDropped(Buffer)`.
- **AC-101-13** A newer command replaces a buffered one.
- **AC-101-14** Slide lasts 39 ticks; hitbox height 0.70 m from the first slide tick.
- **AC-101-15** Swipe up while sliding → jump on the same tick, slide ended.
- **AC-101-16** Swipe down while sliding restarts the 39-tick timer.
- **AC-101-17** Swipe down in the air sets `vy ≤ −14 m/s`; a full slide begins on the landing tick.
- **AC-101-18** Fast-fall then jump before landing → jump executes on the landing tick.
- **AC-101-19** Slide timer expiring under a High obstacle extends the slide until clear (ceiling guard).
- **AC-101-20** Jump under a High obstacle is held and fires on the first clear tick within 21 ticks; otherwise dropped with `InputDropped(Ceiling)`.
- **AC-101-21** Floor rise ≤ 0.35 m is walked up grounded; drop > 0.35 m makes the runner airborne and starts coyote.
- **AC-101-22** Landing short of a gap's far lip by ≤ 0.30 m with feet ≤ 0.25 m below the lip snaps onto the lip; beyond that the runner falls and dies at 1.20 m below the lip (cause `Fall`).
Collisions and health
- **AC-101-23** Each obstacle class (§4.1) produces exactly its listed result; Low/High/Thorns cost 1 segment.
- **AC-101-24** Blocker 1.2 m wide: centre offsets 0.0–0.29 m → Crash; 0.31–0.74 m → SideClip with `xT` moved to the free side; ≥ 0.76 m → no contact.
- **AC-101-25** Landing from above onto a walkable top within 0.15 m of its effective top → no hit, runs across.
- **AC-101-26** After a minor hit: speed ×0.80, back to 1.0× after 48 ticks; i-frames 72 ticks; obstacles contacted during i-frames cause nothing; gaps still kill.
- **AC-101-27** An obstacle resolves once: staying inside it for several ticks costs one segment.
- **AC-101-28** Two minor contacts on one tick cost 1 segment; Major + Minor on one tick → Dead.
- **AC-101-29** Third minor hit → Dead (cause `Health`); in the first-run FTUE window health floors at 1.
- **AC-101-30** 350 m without damage restores 1 segment, never above 3.
- **AC-101-31** Shield absorbs a Minor or a Crash (no health loss, no stumble, 60 ticks i-frames) and does not absorb a Fall; not consumed during existing i-frames.
Input
- **AC-101-32** Recognizer: 24 pt up within 120 ms at ≤ 35° from vertical → `Jump` on the crossing sample; 23 pt → nothing; 40° → no swipe.
- **AC-101-33** A diagonal swipe up (25° from vertical) produces `Jump` and < 0.05 m of lateral target change.
- **AC-101-34** Flick 40 pt in 150 ms horizontally → one Dodge on release; same motion over 300 ms at < 1,200 pt/s release speed → steering only, no Dodge.
- **AC-101-35** Drag, swipe up mid-drag, continue dragging → one Jump and continuous steering; no Dodge.
- **AC-101-36** Two swipes in the same frame → two commands on consecutive ticks, in order.
- **AC-101-37** Touch B never steers; when A lifts, B takes over steering with no target jump.
- **AC-101-38** Lateral deltas split across N ticks of a frame sum exactly (mm) to the frame's drag; sub-mm remainders carry.
- **AC-101-39** Touch cancelled (orientation change) → no Dodge, no further delta.
- **AC-101-40** Recognizer and simulation step allocate 0 bytes per tick after warm-up (PlayMode allocation test).
Camera
- **AC-101-41** Rig converges to each profile's offsets/pitch/FOV within 1 cm / 0.1° at constant speed; FOV equals base + gain at `vMax`.
- **AC-101-42** Smoothing gives the same result (within 1 cm) at 30, 60 and 120 Hz frame rates.
- **AC-101-43** Shake never exceeds 0.12 m / 1.0°; with Reduced Motion, shake and bank are exactly 0.
- **AC-101-44** (PlayMode) On the feel course at `vMax`, every obstacle is inside the view frustum ≥ 1.5 s before contact, in both profiles.
Course
- **AC-101-45** The Perfect bot finishes `FeelCourse` with 0 hits at constant 10, 13 and 16 m/s.
- **AC-101-46** Debug overlay shows hitboxes, `xT`, buffered command and the last 5 dropped inputs (PlayMode smoke test).

## 9. Edge cases (summary)
| Situation | Result |
|---|---|
| Swipe up during a jump | Buffered 150 ms, else dropped (no double jump without the ability) |
| Swipe down during a jump | Fast-fall, auto-slide on landing |
| Swipe up under a branch while sliding | Held until clear (≤ 0.35 s) |
| Slide ends under a branch | Extends until clear |
| Dodge during jump/slide | Executes; vertical state unchanged |
| Route split reached mid-jump or mid-slide | Branch chosen by `x` at the split `s` (spec 102 §3.3); state unchanged |
| Drag past the path edge | Clamped, edge brush, no damage, no debt |
| Hit during i-frames | Ignored (gaps still apply) |
| Missed gap | Ledge assist if close, else fall → Dead (revive offer) |
| Pause / app backgrounded | Simulation frozen; resume after a 1.0 s "ready" beat; touches held at resume are ignored until lifted |
| Frame hitch | ≤ 5 ticks per frame (ARCHITECTURE §5.2); input deltas split across them |

## 10. Assumptions `[ASSUMED]`
Hybrid Steer + Flick; 0.040 m/pt; steering never kills; 60 Hz simulation; release-flick dodge on by default;
landscape two-thumb rule (B swipes only); walkable-top logs; i-frames ghost through obstacles; per-orientation camera
numbers in §5 extend ART_DIRECTION §9.
