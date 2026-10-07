# Spec 004: Fixed-pivot swing (a real pendulum from a real branch)

**Owner:** game-designer | **Builders:** gameplay-engineer (swing simulation, config, chunks), balance-simulator (reference model,
golden traces, fairness runs), ui-engineer (vine rig, camera, tutorial hints) | **Status:** Ready to build; numbers are first
guesses worked out by hand, to be confirmed by T401 (section 14) | **Last updated:** 2026-10-07

**Source of truth:** owner direction of 2026-10-07 (binding): "vines must look like they hang from a real tree: a visible branch
connected to a trunk; the swing must be a true pendulum about a FIXED pivot (the branch point), not a pivot that travels with the
runner." GDD 7 (vines), spec 003 section 8 and risk R1, `VineConfig`/`VineDesignValues`, `RunnerSimulation` vine code. `[ASSUMED]`
marks a designer default that stands until the owner says otherwise.

**Supersedes:** the swing model of GDD 7.3 steps 3 to 5 and 7.5 (scripted arc, pivot riding with the runner), spec 003 section 8.1
(span plus sliding knot) and AC-318, AC-319, AC-321. **Keeps:** grab zone, grab earliness rule shape, grades and their score/coin/meter
rewards, chain rules, `Missed vine` death, pad guarantee, everything outside the vine code (spec 001 movement, spec 002 track).

**Method note (honest reporting):** this session had no shell, so the Python model could not be run. Every number in sections 5 to
10 comes from hand calculation: energy conservation for the swing, the elliptic-integral correction for the swing time
(`T/4 = sqrt(L/g) * K(k)`, k = sin(theta_max / 2)), and ballistic flight. Expected accuracy: landing distances +-0.3 m, times +-2
ticks, angles +-0.5 deg. Task T401 replaces them with exact runs of the 60 Hz integrator. Nothing here has been run.

---

## 1. Player story and purpose

"A giant tree leans over the canyon. A long liana hangs from its limb, one fixed point high above the lane. I jump, catch the rope,
and the whole rope swings out over the drop like a real pendulum: I rise, slow down at the top, and the glowing ring tells me when to
let go. Let go at the gold moment and I fly high through a coin ring and land in the sunlight. The rope keeps swinging behind me."

Why this makes the game better:
- Pillar 3: a rope that pivots on one visible branch is physically readable and reads in a 5 s clip. A rope sliding on a rail
  (spec 003 8.1) looks like a trolley when slowed down.
- Pillar 1 and 2: the swing is the same for every runner speed that can meet a vine (section 5), so the windows, the chasm and the
  landing do not change with speed. The player learns one swing.
- Fewer, deeper mechanics: one pendulum, one release timing, one landing. No span, no knot, no second curve.

---

## 2. Summary of the change

| | Before (GDD 7, spec 003 8.1) | After (this spec) |
|---|---|---|
| Pivot | Rides with the runner; 11 m (8 m/s) to 29 m (21 m/s) of travel per swing | **Fixed** at `(sP, xLane, H)`; `sP` = vine `Z`; `H` = 17.0 m |
| Rope | 6 m, hand angle scripted from -20 to +55 deg in 1.40 s | 14.0 m, real pendulum `theta'' = -(g/L) sin(theta)` |
| Forward motion during swing | Run speed, constant | The pendulum: `z = sP + L sin(theta)` |
| Entry speed | Irrelevant | Tangential catch speed `vC = clamp(vIn, 13, 16)` m/s |
| Release | Fixed vertical speed by grade, forward = run speed | Pendulum velocity plus a small grade impulse, floors, flight gravity 16 |
| Chasm | 18 m, rim 3 m before the vine | **16 m, rim 4 m before the pivot**, far edge 12 m past the pivot |
| Visual | Span and sliding knot | One branch from a trunk, rope hangs from its tip |

---

## 3. Geometry (the contract between sim and view)

Frame coordinates are spec 003 path space: `s` (= sim `z`), `x` (lane), `y` (height above the path surface).

| Name | Value | Meaning |
|---|---|---|
| `RopeLengthM` L | **14.0** m | Pivot to the hand grip |
| `GrabPointHeightM` | 3.0 m (unchanged) | Height of the rope end at rest (the glow and tuft) |
| `PivotHeightM` H | `GrabPointHeightM + L` = **17.0** m | Derived, never stored |
| Pivot | `(s = vine.Z, x = laneCenterX(vine.Lane), y = H)` | Fixed for the life of the vine; the branch tip |
| `HandToFeetM` | 1.75 m | Feet are this far below the hand (old: 0.9 m low point with a 2.64 m hand) |
| Rest | Rope vertical; the rope end is at `(Z, x, 3.0)` | Sim has no sway; presentation sway fades out before the grab (section 12) |
| Hand | `(sP + L sin(theta), x, H - L cos(theta))` | Always on the circle of radius L around the pivot, after the 6 tick blend |
| Feet | `hand.y - 1.75` | At the bottom of the arc the feet are 1.25 m above the path |

The lane is fixed during the swing: `x` is the vine's lane center from the end of the blend until release. The swing is planar
(`s`, `y`). The branch comes from a trunk 6 to 9 m beside the path, so the rope hangs at the branch tip over the lane and the
swing plane runs along the path, parallel to the trunk line.

---

## 4. Behavior

### 4.1 Approach and grab (changes: earliness only)

1. Zone, lane test, height test and "airborne" rule are unchanged: zone 2.0 m long (+-1.0 m about `Z`) plus the hitbox half depth, 1.6 m wide, `top > 1.6 m` and `y < 3.6 m`;
   airborne includes `Falling` with `y > 0` (so a chain catch works) and the 80 ms coyote jump.
2. **`GrabEarlinessMs` 450 to 550** [ASSUMED]. The take-off window (below) shrinks because the pivot is now 4 m past the rim, not 3 m;
   550 ms restores the old window at 10 m/s (355 ms). Take-off window for a chasm vine, in take-off positions:
   `[sP - 1.25 - 0.55 v, rim + 0.08 v]` with `rim = sP - 4`, length `0.63 v - 2.75 m`; evaluated: **8 m/s 286 ms, 10 m/s 355 ms, 13 m/s 418 ms, 21 m/s 499 ms**
   (old: rim at -3 and earliness 450, length `0.53 v - 1.75 m`: 311 / 355 / 395 / 447 ms).
3. The grab triggers on the first tick the zone test passes: normally `z = sP - 1.25 .. -0.9`.

### 4.2 The catch (new): where the pendulum starts

On the grab tick (step 9a), with `z` the hero's z and `vIn` the horizontal speed the hero had on that tick (`_speed` for a ground jump,
`vx` for a chain arrival):

| Quantity | Rule |
|---|---|
| Start angle | `theta0 = asin(clamp((z - sP) / L, -sin(10 deg), +sin(10 deg)))` (about -5.1 deg at `z - sP = -1.25`). Because the angle is taken from `z`, there is **no z jump at the grab** |
| Catch speed | `vC = clamp(vIn, CatchMinSpeedMps, CatchMaxSpeedMps)` = `clamp(vIn, 13, 16)` m/s. `vIn` includes a Speed Boost multiplier; the clamp makes it harmless |
| Start angular speed | `omega0 = vC / L` (tangential speed `vC`); forward, always positive |
| Height and lateral blend | Only `y` and `x` are blended: `y = pendulum + resY * (1-u)^2`, `x` as today, `u = tick / 6` (`GrabBlendMs` 100). `resY = (y + 1.75) - (H - L cos(theta0))`. No z blend |
| Safe | Collisions are off while `Carried` (unchanged) |

Why a clamp instead of conservation of the runner's energy (energy conservation shown with `g = 22`, `L = 14`, so
`cos(theta_max) = 1 - vC^2 / (2 g L)`):

| Entry speed v (m/s) | Pure physics: apex angle | Verdict |
|---|---|---|
| 8 | 26.4 deg | Apex release lands about 9 m past the pivot: cannot cross a 12 m half-chasm |
| 13 | 43.4 deg | fine |
| 16 | 54.2 deg | fine |
| 21 | 73.5 deg | steep, rope nearly horizontal at the top, over the 62 deg cap |
| 28 | `v^2 / (2gL) = 1.27 > 1`: above the horizontal, rope goes slack | impossible as a taut pendulum |

One chasm length cannot serve 8 to 28 m/s under pure physics. The clamp is the "rope catch": a slow runner is pulled forward by the
rope (the vine is spring-loaded, `+5 m/s` at 8 m/s), a fast runner is slowed (`-5 m/s` at 21 m/s). From about 900 m to 2,600 m of a run
(speed 13 to 16 m/s) the catch conserves speed exactly.

| Entry speed vIn (m/s) | 8 | 10 | 12 | 13 | 14 | 15 | 16 | 18 | 21 | 24 | 28 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `vC` (m/s) | 13 | 13 | 13 | 13 | 14 | 15 | 16 | 16 | 16 | 16 | 16 |
| Change at the catch | +5 | +3 | +1 | 0 | 0 | 0 | 0 | -2 | -5 | -8 | -12 |

[ASSUMED] The catch jolt (speed change in one tick) is hidden by the 0.8x hang, the swing camera pull-back, a short rope-snap sound
and a camera FOV kick; if it feels bad the fallback is a 150 ms speed blend inside the catch (presentation only), see open question 2.

### 4.3 Swing integration (new, per tick, step 9)

Fixed step `dt = 1/60 s`, semi-implicit (symplectic) Euler in `double`, state `theta`, `omega`:

```
omega = omega - (SwingGravityMps2 / RopeLengthM) * Sin(theta) * dt
theta = theta + omega * dt
z     = sP + L * Sin(theta)             // replaces the run-speed advance while Carried
handY = H - L * Cos(theta)
feetY = handY - HandToFeetM + blendResidualY
```

- `SwingGravityMps2` = **22** m/s^2 [ASSUMED]: an arcade pendulum like the jump (33.3 m/s^2). The same arcs under real gravity (9.8) have
  a 7.5 s period; at 22 the small-angle period is 5.0 s and the swing to the apex takes 1.30 to 1.33 s (the old 1.40 s).
- **Deterministic math:** the sim must not call `Math.Sin`/`Math.Cos` here. Use `DeterministicMath.Sin` / `Cos`: odd Taylor
  polynomial to `x^11` for `|x| <= 1.3` (max error 1e-7), argument always inside that range because `|theta| <= 62 deg = 1.08 rad`.
  Only `+ - * /` and `Math.Sqrt` appear in the swing. This is stricter than the old `Math.Cos` in `SwingFeetYAt` and removes the
  platform dependency. The Python reference model implements the same polynomial.
- Energy drift of this integrator: <= 0.1 % over 100 ticks at `omega_n dt = 0.021`.
- `z` is monotone during the upswing (`omega > 0`) and the flight, so distance, scoring and `ITrackQuery` sweeps stay valid.
- The snapshot `Speed` while carried is `L * omega * cos(theta)`; `SwingAngleRad` is the real `theta`; new `SwingOmegaRadS`.

Amplitude and timing for the catch speeds (hand calculation):

| `vC` | 13 | 14 | 15 | 16 |
|---|---|---|---|---|
| Apex angle `theta_max` | 43.4 deg | 47.0 deg | 50.6 deg | 54.2 deg |
| Apex height of the rope end above the path | 7.4 m | 8.2 m | 8.9 m | 9.6 m |
| Time to the apex (swing `T/4`) | 1.30 s (78 ticks) | 1.31 s (78.5) | 1.32 s (79) | 1.33 s (80) |

Closed form for the HUD ring (no trig): `k2 = vC^2 / (4 g L)`; `T4 = sqrt(L/g) * (pi/2) * (1 + k2/4 + 9 k2^2/64 + 25 k2^3/256)`. Hard limit
`MaxSwingAngleDeg` = 62: the config validator rejects `vCmax` with `1 - vCmax^2 / (2 g L) < cos(62 deg)` (that is `vCmax <= 18.1`).

### 4.4 Release windows and grades

All windows are in ticks since the grab tick (so one table for every speed; `T4` varies only +-3 percent with `vC`).

| Swing tick (ms) | Rope angle at `vC` 14 | Result |
|---|---|---|
| 0 to 26 (0 to 433) | 0 to 24 deg | **Too early.** Ignored, buffered 9 ticks (150 ms) from the swipe; a swipe made at tick 18 or later fires at tick 27 as Good, a swipe at tick 17 or earlier expires |
| 27 to 41 (450 to 683) | 24 to 34 deg | **Good** |
| 42 to 52 (700 to 867) | 34 to 41 deg | **Perfect**, 11 ticks = 183 ms, centered on tick 47.5 (792 ms) |
| 53 to apex (883 to about 1,300) | 41 to 47 deg | **Good** (late) |
| No swipe by the apex | apex | **Poor**: automatic release on the first tick with `omega <= 0` (tick 78 to 80), never later than `SwingMaxMs` (1,600 ms = 96 ticks) |

- `VineReleaseGrade.Auto` is the code name for **Poor**; text shown to the player is "Poor" only on the results line, never as a failure message. Poor never kills (section 6).
- Perfect is the moment with the best combination of height and speed (the ring on the HUD turns gold), not the longest throw: the longest throw is an earlier Good,
  the Perfect adds the biggest impulse and the coin ring (section 6). Windows live in `VineDesignValues` as ms (`GoodStartMs` 450, `PerfectStartMs` 700, `PerfectWidthMs` 183)
  and convert to ticks like today (`PhaseToTick` becomes `MsToTicks`).
- The old phase value used by the HUD ring (`SwingPhase`) becomes `swingTick / ApexTicksEstimate(vC)`, clamped 0 to 1, with the three window markers at the tick values above.
- Input rules unchanged: Jump on a vine = release (rule I5: a buffered jump is consumed at the grab and means "release" only after the swing starts),
  Move = aim, Slide = kept for landing. Release is processed in step 6 and acts on the same tick (0 ms added latency).

### 4.5 Release velocity and the flight (new)

State at release: the state after the previous tick's swing update (`theta`, `omega`; for a Poor release the apex tick).

```
vt   = L * omega                                   // tangential speed
vx   = vt * Cos(theta) + I * Cos(alpha)
vy   = vt * Sin(theta) + I * Sin(alpha)
vx   = max(vx, ReleaseMinForwardMps)               // floors
vy   = max(vy, ReleaseMinUpMps)
y0   = feetY at release;  z0 = z at release
flight: ballistic with LaunchGravityMps2 = 16, y(t) = y0 + vy t - 8 t^2, z(t) = z0 + vx t   // forward speed constant in flight
```

| Grade | Impulse I (m/s) | Why |
|---|---|---|
| Perfect | 3.0 | Higher and longer arc through the bonus ring |
| Good | 1.5 | Small push |
| Poor | 0.0 | Floors alone carry it (below) |

`alpha` = 30 deg (`ImpulseAngleDeg`), `ReleaseMinForwardMps` = 6.0, `ReleaseMinUpMps` = 2.0 [ASSUMED]. The floors are what makes the swing
unfailable: a release at the apex has no tangential speed, so the hero is "pushed off" the rope with 6 m/s forward and 2 m/s up.

Flight rules: step 7 advances `z` by `vx` while the hero is in vine flight (state `Falling` with `launchedFromVine`); `SpeedMultiplier` and the
curve speed are not used. Landing is the normal ground check (`y = 0` with ground under the footprint).

### 4.6 Landing target, the chasm and the numbers (hand calculation)

Chasm (chunks V-03 and V-04): **length 16 m, near rim at `sP - 4`, far edge at `sP + 12`**. 16 m is 1.7 m longer than the longest plain
jump (14.3 m at 21 m/s with coyote), so the chasm is still vine-only; the old 18 m chasm would need Poor floors of 9 m/s that overshoot
every Perfect.

`s` below is the landing distance from the pivot at `y = 0`; "past edge" is `s - 12`. Windows: Good-early at tick 27 (450 ms), Perfect at its
center, tick 47.5 (792 ms), Good-late at tick 66 (1,100 ms), Poor at the apex.

| `vC` | Perfect: rope angle | Perfect: grab-to-release distance | Perfect: release speed (vx, vy) | Perfect: feet height at release | Perfect: flight time | **Perfect landing, past edge** |
|---|---|---|---|---|---|---|
| 13 | 35.6 deg | 9.4 m | 8.6, 5.8 | 3.9 m | 1.14 s | 17.9 m, **+5.9 m** |
| 14 | 38.4 deg | 9.9 m | 8.8, 6.4 | 4.3 m | 1.24 s | 19.6 m, **+7.6 m** |
| 15 | 41.2 deg | 10.5 m | 9.0, 7.1 | 4.7 m | 1.33 s | 21.2 m, **+9.2 m** |
| 16 | 43.9 deg | 11.0 m | 9.2, 7.9 | 5.2 m | 1.43 s | 22.9 m, **+10.9 m** |

| `vC` | Good-early (tick 27) landing, past edge | Good-late (tick 66) landing, past edge | **Poor (apex) landing, past edge** |
|---|---|---|---|
| 13 | 16.1 m, +4.1 m | 15.3 m, +3.3 m | 15.2 m, **+3.2 m (worst case)** |
| 14 | 18.0 m, +6.0 m | 16.2 m, +4.2 m | 16.1 m, +4.1 m |
| 15 | 20.2 m, +8.2 m | 17.2 m, +5.2 m | 17.0 m, +5.0 m |
| 16 | 22.4 m, +10.4 m | 18.2 m, +6.2 m | 17.8 m, +5.8 m |

Reading: every grab clears the chasm by at least **3.2 m** with any valid release or no release. Perfect lands 6 to 11 m past the edge, 3 to 5 m
farther than Poor, so the grade ladder is visible. Grab-to-release distance for any grade at any `vC`: 6.7 to 12.6 m (Perfect 9.4 to 11.0 m).
Release speed: Perfect 10.3 to 12.1 m/s total, Good-early 12.7 to 14.4 m/s. Whole airborne time from the grab to landing: Perfect 1.93 to 2.22 s (old swing
plus flight: 2.35 s). Highest feet height in a Perfect flight: 4.9 m (`vC` 13) to 7.1 m (`vC` 16).

Pad: last landing at `vC` 16 is `sP + 22.9`; the pad guarantee of "20 m or 1.0 s clear" stays, the chunk is long enough (section 9). Hero `y` can reach 8 m (old max 3.5 m):
the camera (spec 003 6.2) needs the taller framing; every other system only reads `y` as a number.

### 4.7 Speed after the swing (conservation rule)

- While `Carried` or in vine flight the hero's forward speed is the pendulum's or the flight's own (about 6 to 14 m/s). The run speed curve keeps its own value
  (it is distance based and does not stop), and **a Speed Boost multiplier is not applied** to this motion; the boost timers keep counting.
- On landing: `speed(t) = vLand + (vCurve * multiplier - vLand) * (3u^2 - 2u^3)`, `u = ticksSinceLanding / 30` (`LandingSpeedBlendMs` 500), then the normal curve. It
  works both ways (a fast Good-early landing at 14 m/s while the curve says 10 m/s decelerates smoothly). No speed overshoot.
- Cost of a swing in distance at 21 m/s: about 2.2 s at an average 10 m/s instead of 21 m/s, around 22 m of progress (about 1 s of running). A Perfect is worth 400 points. Accepted [ASSUMED].
- Chains: speed is conserved up to the clamp: the arrival `vx` of a chain flight is 6 to 12.1 m/s, so every chain swing after the first has `vC = 13`. A chain is
  the same swing again; there is no energy build-up and no loss. [ASSUMED]

### 4.8 Chains (handing from one fixed pivot to the next)

- **Pivot spacing D = 18.0 m** (old 44 m), same height `H` for all pivots, lanes as authored. Chain sections stay over ground (a chain over a chasm needs aim assist;
  not in this spec, the validator forbids `OverChasm` on rows 1 and 2, unchanged rule).
- Aim, `RefreshAimTarget` and `ChainMaxReachM` (now **40** m) are as today. The release is **guided** to the next vine's `Z`:
  `T = clamp((Zb - z0) / vxPhys, ChainFlightMinS, ChainFlightMaxS)` with `vxPhys` = the release `vx` after floors; then
  `vx = (Zb - z0) / T`, `vy = (ChainArriveFeetM - y0 + 8 T^2) / T`, gravity stays 16 (the old gravity-solving guide is removed, so every arc has the same gravity).
  `ChainFlightMinS` 0.85, `ChainFlightMaxS` 1.5, `ChainArriveFeetM` 1.5 [ASSUMED].
- Check with D = 18 (hand calculation): `T` is 0.86 to 1.50 s for every grade at `vC` 13 to 16 (Poor at `vC` 13: 8.4 m left, 6 m/s, `T = 1.4 s`, a high lob with
  `vy = 8.7 m/s`, apex 8 m; Perfect at `vC` 16: `T = 0.90 s`). The catch of the next rope is automatic (as today).
- Hand-off view: the hero is released from pivot A at about 9 to 11.5 m past A, flies about 1 s over the 18 m gap between pivots and catches rope B near its
  near edge. B's pivot is on the opposite side of the path (zigzag trees, spec 003 8.2). Both ropes are visible at once during the flight.
- An unaimed lane (no vine) falls back to a normal release with the grade's launch; a chain release always scores its grade (unchanged).

### 4.9 Missed vine (fall rule)

- Unchanged in meaning: **a fall into a chasm that has a vine the hero did not swing on = `DeathCause.MissedVine`**; a fall after a swing on that row =
  `Fell` (cannot happen after any valid release, section 4.6, so it is a bug signal in tests).
- Numbers move with the geometry: `MissedVineLookBackM` 40 stays, `MissedVineLookAheadM` 6 to **16** (the hero can fall anywhere up to 12 m past the pivot).
- Missed over ground: unchanged (`VineMissed` event, no penalty, the rope sways on).
- Edge cases: jumped too early (lands before the zone, rim is 4 m before the pivot): falls, Missed vine. Jumped too late (past the zone while in the air
  over the chasm): no grab, Missed vine. Grab but swipe nothing: Poor, safe. Hero on the rope when the run is paused: the swing state (`theta`, `omega`, tick) is kept;
  on resume the ring is shown during the countdown (unchanged).
- Continue after a chasm death respawns on the far side pad; the pad start moves from `sP + 15` to `sP + 12` (check `RespawnAt` and `ClearObstacles` arguments).

### 4.10 Interactions

| Thing | Rule |
|---|---|
| Speed Boost | Generator hold unchanged (no vine chunk starts during a boost; `HoldVineSections`). A boosted runner can still grab a committed chasm vine (the way across: the 16 m chasm is longer than a boost auto-jump at 16 to 20 m/s, about 10 to 12 m, so the swing is needed; verify in `PowerUpSystem` that the auto-jump take-off puts the hero airborne in the zone). Multiplier ignored while carried and in flight; `DashUntilLanding` already waits for the ground, so a boost never ends on the rope; speed blend after landing uses the current multiplier. |
| Shield / Magnet / Lift | Unchanged (shield lasts to 0.5 s after landing, magnet pulls the bonus ring, Lift is queued until landing). |
| Companion | "Vine!" call-out 2.0 s ahead unchanged; Good +10 % and Perfect +25 % meter unchanged; one new cheer line at the catch is optional (ui-engineer). |
| Score and coins | Unchanged values: Good 150 / Perfect 400 / Poor (`Auto`) 50, chain x1, x1.5, x2; Good 10 coins, Perfect 25 with a 12 coin ring at the flight apex (`ta = vy / 16`, 0.36 to 0.49 s after release). |
| Obstacles | None in a vine section from approach to pad; the Normal chunk after it starts at chunk end (section 9). |

### 4.11 Tutorial implications (GDD 12, `TutorialDirector`)

- The first vine is a safe (ground) vine, as now. The lesson trigger `VineTriggerM` 22 m stays. Tutorial speed 8 m/s gives `vC = 13`, the same swing as everyone's.
- The release lesson (`TutorialHint.VineRelease`) shows the ring and the three markers (27 / 42 / 53 ticks); text "Tap when the ring glows gold". The 30 percent time scale
  until the player acts stays; at 30 percent the Perfect window is 0.61 s long on screen.
- The grab lesson should say "Jump to catch the rope" (the rope is a visible 14 m liana, not a glow on nothing): `TutorialStrings` update.
- If no release by the apex: Poor release, safe, hint again next time (no death, no rescue). `VineGiveUpPastM` 30 m past the vine is replaced by `landing + 5 m`
  (the hero is now about 23 m past the pivot after a Perfect).
- Assist window for learners (wider Perfect for the first 3 swings) is **not** added: it would be a second set of tick values in replays. The pad is safe, the ring
  is the teacher.

---

## 5. Numbers (all in `VineDesignValues`, units and starting values; `[ASSUMED]` except where noted as unchanged)

| Field | Unit | Start | Notes |
|---|---|---|---|
| `RopeLengthM` (new, replaces `SwingRadiusM` 6) | m | 14.0 | Range 6 to 20 |
| `SwingGravityMps2` (new) | m/s^2 | 22.0 | Range 8 to 40 |
| `CatchMinSpeedMps` (new) | m/s | 13.0 | |
| `CatchMaxSpeedMps` (new) | m/s | 16.0 | validator: pendulum apex <= `MaxSwingAngleDeg` |
| `MaxSwingAngleDeg` (new) | deg | 62 | Validation only |
| `GrabMaxAngleDeg` (new) | deg | 10 | Clamp of the start angle |
| `HandToFeetM` (new, replaces `SwingLowestFeetM` 0.9) | m | 1.75 | |
| `GrabPointHeightM` | m | 3.0 | Unchanged; `PivotHeightM` derived |
| `GrabEarlinessMs` | ms | 550 | Was 450 |
| `GrabBlendMs` | ms | 100 | Unchanged, blends y and x only |
| `SwingMaxMs` (replaces `SwingMs` 1400) | ms | 1600 | Failsafe auto release; normally the apex ends it first |
| `GoodStartMs` (replaces `GoodStartPhase` 0.45) | ms | 450 | Tick 27 |
| `PerfectStartMs` (replaces `PerfectStartPhase` 0.66) | ms | 700 | Tick 42 |
| `PerfectWidthMs` (replaces `PerfectEndPhase` 0.80) | ms | 183 | 11 ticks, end tick 53 exclusive |
| `ReleaseBufferMs` | ms | 150 | Unchanged |
| `HangMs`, `HangTimeScale` | ms, x | 300, 0.8 | Unchanged, presentation only |
| `LaunchGravityMps2` | m/s^2 | 16 | Unchanged, now the only flight gravity |
| `PerfectImpulseMps`, `GoodImpulseMps`, `PoorImpulseMps` (replace `Perfect/Good/AutoLaunchSpeedMps` 10/8/4) | m/s | 3.0, 1.5, 0.0 | |
| `ImpulseAngleDeg` (new) | deg | 30 | |
| `ReleaseMinForwardMps`, `ReleaseMinUpMps` (new) | m/s | 6.0, 2.0 | |
| `ChainArriveFeetM` | m | 1.5 | Was 1.8 |
| `ChainFlightMinS`, `ChainFlightMaxS` (replace `ChainGravityMin/MaxMps2`) | s | 0.85, 1.5 | |
| `ChainMaxReachM` | m | 40 | Was 70 |
| `LandingSpeedBlendMs` (new) | ms | 500 | |
| Score, coin, ring, meter values, `ChainMultipliers`, schedule and capacity fields | | unchanged | |

Chunk data (not config): chasm `Gap(All, sP - 4, 16)`; chain pivot spacing 18 m; chunk lengths in section 9.

### Feel targets (measurable)

| Target | Value |
|---|---|
| Grab to first visible pendulum motion | <= 1 tick (the pendulum starts on the grab tick; y and x blend 100 ms) |
| Release input to launch | 0 ms added (same tick) |
| Swing to the apex | 1.30 to 1.33 s |
| Perfect window | 11 ticks = 183 ms (old: 12 ticks = 200 ms) |
| Rope hangs from one fixed point | pivot world position changes by 0.000 m during the whole swing |
| Rope end equals the hand | <= 0.02 m after the blend |
| Rest sway period | `2 pi sqrt(L/g)` = 5.0 s |
| Speed back to the curve after landing | <= 500 ms |

---

## 6. Fairness (analysis and results)

**Hard guarantee (S-401):** every grab crosses the chasm. Worst case in the tables of 4.6: Poor at `vC` 13, **+3.2 m past the far edge**. All valid
releases (tick 27 to the apex) and the auto release land at least 3.2 m past the edge and no more than 10.9 m (Perfect at `vC` 16) past it, inside a pad that
is clear for 20 m (and at least 1.0 s).

**Bot tolerance profiles (analytic, Gaussian release time around the Perfect center 792 ms).** The old reaction-time profiles do not apply (releasing is an
anticipated action, not a reaction), so release profiles are defined here [ASSUMED]: `sigma` = timing spread of the whole action (anticipation error, finger
latency, rope reading), no bias.

| Profile | `sigma` | Perfect (+-91.7 ms) | Good (incl. buffered early) | Poor (no valid swipe) | GDD 7.6 target |
|---|---|---|---|---|---|
| expert | 70 ms | 81.0 % | 19.0 % | about 0 % | Perfect >= 70 %: met |
| average | 180 ms | 38.9 % | 60.6 % | 0.5 % | Perfect 25 to 40 %: met (38.9) |
| new | 260 ms | 27.6 % | 66.9 % | 5.5 % (2.6 late + 2.9 swipe before tick 18 and no retry) | informational |

If the average profile turns out to be 150 ms instead of 180, Perfect would be 45 %: set `PerfectWidthMs` to 150 (9 ticks). The width is the one knob.
Chasm deaths after a grab: 0 % for all profiles (guaranteed). The grab itself (jump timing) is the only way to die on a vine: take-off windows in 4.1.
Not simulated here (the Python model has no jump-to-grab bot): the `Missed vine` share of deaths for the new bot (GDD 7.6: <= 15 %) and grab success (new >= 75 %, average >= 90 %) are re-measured in T406 with a grab-timing bot.

---

## 7. Edge cases (summary)

| Case | Result |
|---|---|
| Swipe during the first 6 ticks (blend) | Too early: buffered, fires at tick 27 if made at tick 18 or later |
| Two grabs in one section (second vine of a chain missed) | Chain rows are over ground only: `VineMissed`, no penalty |
| Aimed lane has no vine | Normal release, launch of the grade, pad landing |
| Swipe on the apex tick | A command is processed in step 6, before the swing update that would auto-release, so it wins: Good (late) |
| Run paused on the rope | Swing state kept; `PauseResumed` clears the release buffer (rule I6); the swing continues |
| Death while on the rope | Cannot happen (collisions off); a fall after release is the bug signal described in 4.9 |
| Hero on a vine in the last metres of a world segment | Vine chunks are not started if they could cross a gateway (existing `VineWouldCrossBoundary`, uses chunk length 120 now) |
| Slide swipe on the rope | Kept as a slide on landing, as today |
| Lane move buffered before the grab | Invalidated at the grab (rule I5), as today |

---

## 8. Presentation contract (for ui-engineer; spec 003 8 is superseded where it conflicts)

| Element | Rule |
|---|---|
| Anchor tree | As spec 003 8.2 and `SwingRigMath.PlaceTree` (trunk 6 to 9 m beside the path, 28 to 40 m tall; the zigzag by row stays). Trunk height >= `H + 8` m = 25 m (met by 28 m minimum) |
| Branch | **One limb**, tapered, 0.8 m thick at the trunk and 0.35 m at the tip, growing out of the trunk at 15 to 19 m up, curving over the lane, tip exactly at the pivot `(sP, xLane, 17.0)` (<= 0.02 m). The limb is contiguous with the trunk (branch root inside the trunk radius). No span, no sliding knot, no `Vine_Knot` art slot. Limb length from the trunk surface to the pivot: `LateralM - TrunkRadius` = 4.3 to 7.8 m, so it is a short real branch |
| Rope | `Vine_Liana` from the pivot to the hand, length exactly L (14.0 m); visible knot of leaves at the pivot, tuft and the glow at the rope end (existing) |
| Rest sway | The rope hangs as a free pendulum: amplitude 4 deg, period 5.0 s (`2 pi sqrt(L/g)`), phase from the Scenery stream (stateless, as today). Fades to 0.5 deg (end moves 0.12 m) from 30 m before the grab, so the glow stays inside the 2.0 m zone (AC-320 unchanged in meaning) |
| After release | The rope continues as a damped pendulum from the release state (`theta`, `omega`, damping 0.35 per second); at rest within 4 s; presentation copy only, not in the hash; sways back over the canyon behind the hero. This is the signature clip image |
| Visibility | The branch and rope are visible >= 2.0 s ahead (42 m at 21 m/s); pivot at 17 m means the limb is above the camera's top edge inside 25 m: the rope and glow stay in frame; the limb is seen on approach and at the top of the swing (swing camera, spec 003 6.2, to be retuned for a 17 m pivot and an 8 m high hero) |
| Landing glade | Starts 12 m past the last pivot (was 15), 20 m long, as spec 003 8.2 |
| Chasm dressing | Spec 003 8.3 and spec 005 13.2 unchanged except length 16 m and the rim 4 m before the pivot (`SwingRigMath.GladeStartAfterGrabM` 15 to 12) |

---

## 9. Chunk and generator changes

| Chunk | Old | New |
|---|---|---|
| V-01 Vine Clearing, V-02 Side Vine | 110 m, vine `Z` 34 | Unchanged (110 m, `Z` 34); pad coin lines start at 62 (landing 51 to 57 at most) |
| V-03 Chasm Vine, V-04 Chasm Side Vine | Gap at 31, 18 m | **Gap at 30, 16 m**; length 110; pad coins from 62 |
| V-05 Twin Vines | 150 m, `Z` 34 and 78 | **110 m, `Z` 34 and 52** (D = 18); last landing 52 + 22.9 = 75; pad coins 78 to 106 |
| V-06 Vine Ladder | 200 m, `Z` 34, 78, 122 | **120 m, `Z` 34, 52, 70**; last landing 93; pad coins 96 to 118 |

`TrackGenerator._maxVineLengthM` is derived (now 120). Section length and landing zone satisfy "20 m or 1.0 s clear after the longest landing" in every chunk.
`ChasmVinesFromM` 600 unchanged. Spec 003 route rule "R >= 250 m, |grade| <= 4 percent from 60 m before to the end of the chunk" is unchanged and now covers a shorter
chunk. Vine chunk length 110 to 120 m must be read by `RouteGenerator` from the chunk (it already reads committed chunk lengths).

---

## 10. Simulation targets for balance-simulator (S-401 to S-408)

| ID | Target |
|---|---|
| S-401 | Hard: for `vIn` 8 to 28 in steps of 0.5, 1,000 random grab offsets each (z in the zone, `y` in the airborne range), every release tick 27 to apex and Poor: landing >= `sP + 14.5` and <= `sP + 30`; 0 landings in the chasm |
| S-402 | The tables in 4.6 within +-0.3 m (landing), +-2 ticks (apex), +-0.5 deg (angle) |
| S-403 | Bot grades with `sigma` 70, 180, 260 ms: Perfect 70+ / 25 to 40 / informational, Poor <= 6 % for all |
| S-404 | Energy drift <= 0.1 % in 100 ticks for 1,000 start states; `theta` never above 62 deg |
| S-405 | Chain: 10,000 trials per grade and `vC`, guided `T` in [0.85, 1.5] s, catch success 100 %, arrival feet 1.5 +-0.1 m at the zone center |
| S-406 | Grab take-off window per speed (8, 10, 13, 21 m/s) vs the table in 4.1 (+-1 tick) |
| S-407 | Time and distance cost of a swing vs the old model at 10, 15, 21 m/s |
| S-408 | Full-run regression: S1 to S9 for runs without vines identical to the 2026-10-07 report |

Reference model: new `tools/sim/pendulum_model.py` (same polynomial sine, same integrator, same floors), a study script `tools/sim/pendulum_study.py` that emits the tables of
4.2 to 4.6 and 6 as a report in `docs/sim-reports/`, `test_pendulum_model.py`. The existing `runner_model.py` has no vine code at all (the previous report has no vine
metric), so this is an extension, not a change to spec 001 traces.

---

## 11. Migration

### 11.1 Code (gameplay-engineer)

| File | Change |
|---|---|
| `Gameplay/Vine/VineDesignValues.cs`, `VineConfig.cs`, `Config/VineConfigAsset.cs`, `Editor/Setup/DefaultConfigAssetsMenu.cs` | Fields in section 5 (add, rename, remove); `Validate` ranges and the `MaxSwingAngleDeg` rule; `ComputeHash` updated; remove `SwingAngleAt`, `SwingFeetYAt`, `PhaseAt(phase)`; add `PivotHeightM`, `CatchSpeedFor(vIn)`, `ApexTicksEstimate(vC)`, `LaunchVelocity(...)`. `GradeForSwingTick` keeps its signature (ticks) |
| New `Core/DeterministicMath.cs` (or Gameplay/Vine) | `Sin`, `Cos` polynomial; test of max error 1e-7 |
| `Gameplay/Runner/RunnerSimulation.cs` | New state `_swingTheta`, `_swingOmega`, `_swingPivotZ`, `_swingResY`, `_launchVx`, `_landBlendTick`, `_landSpeed`; `GrabVine` (start angle, catch speed, residual), `UpdateSwing` (integration, z/y/x), `ReleaseVine` (impulse, floors, chain guide with fixed gravity), `UpdateSpeedAndDistance` (skip the run advance while `Carried`, use `_launchVx` in vine flight, landing blend), `DieFalling` (`MissedVineLookAheadM` 16), `Land` (start blend), `Hash`, `CopyFrom`, `BuildSnapshot` (real `SwingAngleRad`, new `SwingOmegaRadS`, `SwingPhase` from `ApexTicksEstimate`) |
| `Gameplay/Runner/RunnerTickInfo.cs`, `RunnerState.cs` | New snapshot fields (`SwingOmegaRadS`, `SwingPivotZ`, `LandBlend`) |
| `Gameplay/Track/JungleChunkLibraryDefaults.cs` | V-03 to V-06 per section 9; coin lines; the doc comment about 18 m. `TrackSimulation.SpawnVineBonusCoins` needs no change (reads the runner's launch fields) but check the ring apex time |
| `Gameplay/Track/TrackGenerator.cs`, `Path/RouteGenerator.cs`, `Path/RouteValidator.cs`, `RouteTuning.cs` | Read the new max chunk length 120; no logic change expected |
| `Gameplay/Views/VineView.cs`, `SwingRigMath.cs`, `SwingRigMath` consumers, `Scenery/ScenerySystem.cs` (swing zone corridor 9 m), `FollowCameraView`/`CameraRouteModel` (swing camera) | Single branch and fixed-pivot rope; remove span, knot, `SpanLengthM`, `SpanCoversTravel`, `RestPivotAheadM`, `PivotOffsetFromHand`, `SwingTravelM`; add `PivotWorld`, free-pendulum sway and the after-release pendulum; camera framing for H = 17 m and `y` up to 8 m |
| `Gameplay/Tutorial/TutorialDirector.cs`, `TutorialDesignValues.cs`, `UI/Tutorial/TutorialStrings.cs` | Hint text, `VineGiveUpPastM` rule (4.11) |
| `Gameplay/PowerUps/PowerUpSystem.cs`, `PowerUpPlacer.cs` | Verify only: boost multiplier is not used while carried; `SpeedBoostVineBlockMarginM` still covers a 120 m section; the auto-jump take-off point reaches the grab zone for a boosted chasm vine |
| `Services/Audio/RunAudioCues.cs`, `Companion/CompanionSimulation.cs` | No change; optional catch sound and cheer |

### 11.2 Tests and golden traces

- Spec 001 goldens `01` to `06` (`tools/sim/golden/*.json`, `test_golden.py`): **unchanged and must stay byte-identical** (no vines in them; AC-435).
- New goldens (schema bump to `junglebooze.golden-trace.v2`, additive: top-level `vines` list `{id, z, lane, row, over_chasm}`, per tick `swing {theta, omega}` and
  `launch {vx, vy, y0}` where relevant; v1 files stay valid): `07_vine_perfect_release`, `08_vine_poor_auto_release`, `09_vine_catch_speed_clamp` (entry 8 and 21 m/s),
  `10_vine_chain_two`, `11_vine_missed_over_chasm`, `12_vine_boost_entry`.
- New EditMode tests: `VineConfigTests`, `PendulumSwingTests` (AC-401 to 412), `VineReleaseTests` (413 to 417), `VineChainTests` (418 to 420), `VineSpeedTests` (421 to 423),
  `MissedVineTests` (424 to 427), `DeterministicMathTests`; replay of the new goldens. `SwingRigMathTests` (AC-318 to 320 of spec 003) is rewritten for AC-429 to 433.
- Tests that pin a run hash with vines enabled (any run with `TrackRunSetup.CreateDefault`): regenerate (the swing changes every vine run).
- Stale PlayMode tests stay deferred by the owner's order.

### 11.3 Docs

GDD 7.2 to 7.5 sync (this spec lists the facts: rope 14 m, one fixed branch, windows 450 / 700 / 883 ms, chasm 16 m, rim 4 m before the vine, catch 13 to 16 m/s, chain spacing 18 m); spec 003 section 8 gets a pointer note (done);
spec 005 section 13.2 (chasm is 16 m, rim 4 m before the pivot) and `docs/art/jungle-crossing-art-plan.md` (drop span and knot art, add the branch) get a one line follow-up from their owners.

---

## 12. Acceptance criteria (numbers [ASSUMED]; tolerances are the pass thresholds)

Pendulum and catch
- **AC-401** `VineConfig.PivotHeightM` equals `GrabPointHeightM + RopeLengthM` = 17.000 +-0.001 m, read from config; the pivot of a vine is `(Z, laneX, PivotHeightM)`.
- **AC-402** For each tick after the 6 tick blend of any swing at entry 8, 15 and 28 m/s: `|hand - pivot|` = 14.000 +-1e-6 m, and the pivot is unchanged (the test reads the same vine pivot every tick: 0 change).
- **AC-403** Energy `0.5 L^2 omega^2 + g L (1 - cos theta)` drifts <= 0.1 % over 100 ticks of an unreleased swing for 1,000 start states.
- **AC-404** Catch speed `L * omega0` equals `clamp(vIn, 13, 16)` +-0.001 for `vIn` in {8, 10, 12, 13, 14, 15, 16, 18, 21, 24, 28}; the entry speed table of 4.2 is reproduced.
- **AC-405** At the grab tick `z` is continuous (`|dz| <= vIn * dt`); `theta0 = asin((z - sP) / L)`; y reaches the pendulum within 6 ticks; x within 6 ticks to the lane center.
- **AC-406** Peak angle is <= 62 deg for every `vIn` from 0 to 40 m/s, and within +-0.5 deg of 43.4 / 47.0 / 50.6 / 54.2 deg for `vC` 13 / 14 / 15 / 16.
- **AC-407** First tick with `omega <= 0` is 78 +-2 (`vC` 13), 78 +-2 (14), 79 +-2 (15), 80 +-2 (16).
- **AC-408** The swing code calls neither `Math.Sin` nor `Math.Cos`; `DeterministicMath.Sin/Cos` error <= 1e-7 on [-1.3, 1.3]; two runs with the same seed and inputs give equal state hashes.

Grades and release
- **AC-409** `GoodStartTick` 27, `PerfectStartTick` 42, `PerfectEndTick` 53 (exclusive), from config ms; `GradeForSwingTick`: ticks 0 to 26 `None`, 27 to 41 Good, 42 to 52 Perfect, 53 to apex Good.
- **AC-410** A release swipe on tick 18 to 26 fires on tick 27 as Good; on tick 17 or earlier it expires with no effect; the buffer count is 9 ticks.
- **AC-411** With no swipe the release is `Auto` (Poor) on the first tick with `omega <= 0` (78 to 80), and no later than tick 96 for any config.
- **AC-412** A release command acts on its own tick: `VineReleased` is emitted on that tick and the hero's `z` that tick advances by `vx * dt`.
- **AC-413** For `vIn` 8 to 28 (step 0.5), 1,000 grab offsets each, every release tick 27 to apex and the Poor release: landing `s` >= `sP + 14.5` and <= `sP + 30` in 100 percent; 0 deaths `Fell` after a grab.
- **AC-414** At the same `vC`: landing(Perfect center) - landing(Poor) >= 2.5 m for `vC` 13 to 16.
- **AC-415** Release velocity floors: after any release `vx >= 6.0` and `vy >= 2.0`; Perfect impulse 3.0, Good 1.5, Poor 0 applied at 30 deg (unit test with a fixed state).
- **AC-416** The Perfect bonus ring centers on the flight apex (`vy / 16`) within 0.05 s and 0.2 m; ring and trail coins are collected by the flying hero (existing test pattern).
- **AC-417** Flight uses gravity 16 and constant `vx`; landing tick equals the closed form `t = (vy + sqrt(vy^2 + 32 y0)) / 16` to +-1 tick.

Chains
- **AC-418** Chain pivot spacing 18.0 m in V-05 and V-06; for 10,000 chain trials per grade and `vC` 13 to 16, guided `T` is in [0.85, 1.5] s, arrival feet height 1.5 +-0.1 m at the vine `Z`, catch success 100 percent.
- **AC-419** In a chain the second and third swing have `vC` = 13.000 (arrival `vx` < 13 in every trial).
- **AC-420** A chain release with no vine in the aimed lane is a normal release (no guidance), lands on the pad, and the validator rejects `OverChasm` rows 1 and 2.

Speed
- **AC-421** While `Carried` and in vine flight, `z` follows the pendulum or `z0 + vx t` exactly (1e-6 m); `SpeedMultiplier` 1.0 and 1.6 give identical `z`.
- **AC-422** After landing the speed follows the smoothstep to the curve speed in 30 ticks, is monotone, and never exceeds `max(vLand, vCurve * multiplier)`.
- **AC-423** Speed Boost active at the grab: grab allowed, the boost does not end while carried or in vine flight (`DashUntilLanding`), timers keep counting, generator hold unchanged.

Missed vine and chasm
- **AC-424** A fall into a chasm with an ungrabbed vine row within [-40, +16] m = `MissedVine`; a fall after a swing on that row = `Fell`.
- **AC-425** Missed vine over ground: `VineMissed` event once, no death.
- **AC-426** Chasm chunks: gap 16.00 m, near rim at `Z - 4.00`, far edge at `Z + 12.00`; plain jump at 21 m/s with coyote (14.3 m) cannot cross it (test in the movement model).
- **AC-427** Grab take-off window (bot sweeps the jump tick): >= 280 ms at 8 m/s, >= 350 ms at 10 m/s, >= 410 ms at 13 m/s, matching 4.1 within 1 tick.
- **AC-428** `x` equals the vine lane center from tick 6 to the release (1e-6); aim swipes change the aim lane but not `x` until release.

Presentation
- **AC-429** For every vine: exactly one branch object, contiguous with its trunk (root inside the trunk radius, gap <= 0.05 m), tip at the pivot within 0.02 m; no span or knot objects exist in the scene or prefab list.
- **AC-430** The rope's top is at the pivot and its bottom at the hand, length 14.0 +-0.02 m in every swing frame after the blend; the pivot's frame position is identical on every frame of the swing (0.000 m).
- **AC-431** Rest sway: period 5.0 +-0.05 s, amplitude <= 4 deg, and <= 0.5 deg (end offset <= 0.12 m) from 30 m before the grab.
- **AC-432** After release the rope settles to 0.5 deg within 4 s, using a presentation copy; the sim hash does not change if the view is removed.
- **AC-433** The anchor tree's trunk top is >= 25 m, the limb root at 15 to 19 m, and the whole branch lies inside the sight-corridor exemption of spec 003 11.1 (swing zone 9 m).

Regression
- **AC-434** Tutorial: first vine is a safe vine; at 8 m/s the swing equals the 13 m/s swing; a missed release lesson never kills.
- **AC-435** Goldens `01` to `06` byte-identical; new goldens `07` to `12` replay with `x`, `y`, `z` within 1e-4 m and `theta` within 1e-6.
- **AC-436** The new swing state is in `ComputeHash`, `CopyFrom` and `BuildSnapshot` (a snapshot round-trip test mid-swing and mid-flight gives equal hashes).

---

## 13. Risks

| # | Risk | Mitigation |
|---|---|---|
| R401 | The catch jolt (13 to 16 m/s, from 8 to 28) feels like a bug rather than a rope catch | Hang 0.8x, camera pull, FOV kick, sound; fallback 150 ms speed blend; owner question 2 |
| R402 | A 17 m pivot is out of frame close to the vine in portrait | Rope and glow stay in frame; limb is seen from afar and at the top; swing camera retune (T405); owner feel check |
| R403 | Poor floors feel like cheating (push-off from the apex) | Impulse is small and unseen in the clip; the Poor result line only says "Poor" |
| R404 | Perfect is not the longest throw; some players expect it | The HUD ring and coin ring make "gold = best"; the ladder (Perfect farther than Poor by 3 to 5 m) holds |
| R405 | Shorter chasm (16 m) and 4 m pivot offset reduce the take-off window at slow speeds | `GrabEarlinessMs` 550 restores 355 ms at 10 m/s; S-406 |
| R406 | Hand numbers (+-0.3 m) are wrong | T401 exact runs before any tuning; acceptance AC-413 uses 14.5 m, not 15.2 |
| R407 | Spec 003 views (T6) were built on the span/knot rig | T405 replaces them; `SwingRigMath` tests are rewritten, not patched |

---

## 14. Implementation plan (small tasks, each compile-checked and playable before the next)

| # | Owner | Task | Done when |
|---|---|---|---|
| T401 | balance-simulator | `pendulum_model.py`, study script, goldens 07 to 12, replaces the hand numbers | S-401 to S-405 reported; tables in 4.2 to 4.6 updated |
| T402 | gameplay-engineer | `DeterministicMath`, `VineDesignValues`/`VineConfig`/asset, validator | AC-401, 404, 406 to 408 |
| T403 | gameplay-engineer | Runner: catch, swing, release, flight, landing blend, hash/snapshot | AC-402, 403, 405, 409 to 417, 421 to 424, 428, 436 |
| T404 | gameplay-engineer | Chunks V-03 to V-06, chain guide, generator lengths | AC-418 to 420, 426 |
| T405 | ui-engineer | VineView and SwingRigMath rewrite (branch, rope, sway, post-release pendulum), camera, tutorial text | AC-429 to 433, 434 |
| T406 | balance-simulator | Full fairness re-run with bots incl. grab timing | S-403, 406 to 408 |

---

## Open questions for owner

All have a recommended default that is already applied (`[ASSUMED]`).

1. **How big is the swing?**
   a) Long rope, 14 m, pivot 17 m up, arcs to 54 deg, a big sweeping swing (recommended: the best 5 s clip, most natural look, real tree height).
   b) Medium, 10 m rope, pivot 13 m, needs a stronger swing gravity (about 16) and a shorter chasm (about 13 m); calmer, smaller.
   c) Short, 6 m rope as before; cannot cross a chasm without a travelling pivot, so not possible with this model.
2. **How does the rope take the runner's speed?**
   a) Clamp 13 to 16 m/s at the catch (recommended: one fair swing at every run speed; a slow runner is pulled forward, a fast one slowed; a short camera and sound kick hides the jolt).
   b) Full physics: arc and chasm depend on run speed (impossible from 21 m/s up; would need a speed-scaled chasm).
   c) One constant catch speed of 14.5 m/s (simplest and most predictable, loses the feel that faster is wider).
3. **Chasm length.**
   a) 16 m with the rim 4 m before the pivot (recommended: bigger Poor safety margin, +3.2 m, and Perfect lands 6 to 11 m past the edge).
   b) Keep 18 m (needs Poor floors of 9 m/s; every Perfect then overshoots by 10 to 16 m).
   c) 14 m (can be jumped at top speed with coyote, so the vine would be optional: not recommended).
4. **What does Poor mean?**
   a) A safe automatic release at the apex with a small push-off, never a death (recommended, pillar 2).
   b) A no-push release that can drop short into the chasm for a "Missed vine" style death (harder, ties the swing to a skill check; risky for fairness).

---

## 15. T401 verified numbers (balance-simulator, 2026-10-07; supersede the hand numbers in sections 4 to 6, 10 and 12 wherever they differ)

Source: exact runs of `tools/sim/pendulum_model.py` (the section 4 model implemented literally; 33 tests in `tools/sim/tests`), full evidence and seeds in
`docs/sim-reports/2026-10-07-fixed-pivot-swing.md`, per-tick tables in `docs/sim-reports/data/2026-10-07-fixed-pivot-swing-tables.csv`, golden vectors
`tools/sim/golden/07..12` (schema v2). No parameter in section 5 was changed; the design holds (0 of 3.1 million fuzzed releases in the chasm). Only the
derived numbers below were wrong. Convention: swing tick k = ticks since the grab tick (grab tick = 0, nothing integrated on it); a swipe at tick k uses the state
after k-1 integrations; the auto release uses the state after k_a integrations. Canonical grab z = pivot - 1.25 (theta0 = -5.12 deg).

**Corrections (the reading of section 4 is unchanged):**

| Was | Is |
|---|---|
| Apex tick 78 / 78.5 / 79 / 80 (AC-407), time to the apex 1.30 to 1.33 s | **Tick 85 (84..85 for a normal first-tick grab; 73 for a grab late in the zone)**, 1.417 s from the grab. 1.30 to 1.33 s is the time from the lowest point. `ApexTicksEstimate` = 85; `SwingMaxMs` 1600 (96 ticks) still leaves 11 ticks |
| Peak angle 43.4 / 47.0 / 50.6 / 54.2 deg | **43.9 / 47.4 / 51.0 / 54.6 deg** (the start angle adds energy) |
| Rope end at the apex 7.4 / 8.2 / 8.9 / 9.6 m | **6.9 / 7.5 / 8.2 / 8.9 m** (feet 5.2 / 5.8 / 6.4 / 7.1 m) |
| Energy drift <= 0.1 % (AC-403) | **<= 1.5 %** (1.27 % measured, bounded; symplectic Euler as specified) |
| Validator `vCmax <= 18.1` | **`vCmax <= 17.8`**: `cos(theta0max) - vC^2/(2 g L) >= cos(62 deg)` with theta0max = 10 deg |
| Perfect lands 6 to 11 m past the edge | **6.6 to 13.6 m** (window range per vC 13..16: 6.6..7.3, 8.2..9.3, 9.9..11.5, 11.5..13.6) |
| Every valid release >= 3.2 m past the edge | **>= 2.86 m** (Good at tick 27, vC 13); Poor >= 3.32 m. AC-413 bounds (14.5 / 30) hold: measured 14.86 and 25.61 |
| "The longest throw is an earlier Good" (4.4, R404) | **Perfect is the longest throw at every vC** (min Perfect minus max Good at one vC: 1.46 / 1.20 / 0.85 / 0.34 m); the maximum is at tick 42 |
| Chain: arrival vx 6 to 12.1 m/s, every later swing vC 13 (AC-419) | **Arrival vx 6.0 to 14.78 m/s; later swings vC 13 to 14.78** (13 after Perfect and Poor). Catch 100 %, T 0.85..1.5, feet 1.5 in 120,000 trials |
| Take-off window `[sP - 1.25 - 0.55v, rim + 0.08v]`: 286 / 355 / 418 / 499 ms | **`[sP - 1.25 - 0.55v, rim + 0.25 + 0.0833v]`: 333 / 400 / 467 / 533 ms at 8 / 10 / 13 / 21 m/s** (capped at 550 ms from 24 m/s); with 450 ms earliness only 233 / 300 ms, so `GrabEarlinessMs` 550 is confirmed. AC-427 floors (280 / 350 / 410) stay |
| `theta0 = asin(...)` | `DeterministicMath` has no asin: add `a(1 + a^2(1/6 + a^2(3/40 + a^2 15/336)))` (error 4.5e-9 rad for `|a| <= sin 10 deg`) |
| DeterministicMath error <= 1e-7 (AC-408) | float64: 5e-9 (sin), 4e-10 (cos). float32 sin: 1.04e-7. Keep the swing in double (section 4.3 already says so) or allow 2e-7 |
| Golden theta tolerance 1e-6 (AC-435) | **5e-6** (float vs double differ by up to 6e-7 rad over 1,000 random swings; results are not bit-identical) |
| Bot table "new": Poor 5.5 % (2.6 late + 2.9 early) | Poor 3.6 % (late swipes after the apex are 0.7 %); Perfect 81.0 / 38.9 / 27.6 % confirmed (120k bot trials 80.9 / 38.6 / 26.9) |
| Pad coins V-05 / V-06 from 78 / 96 | Max landing is `sP + 25.6` (77.6 / 95.6): start the pad coin lines at **82 / 100** |

**4.2/4.3 swing, exact (vC = catch speed, canonical grab):**

| vC | Peak deg | Apex tick | Apex hand y / feet y (m) | Min rope tension (m/s^2) |
|---|---|---|---|---|
| 13 | 43.87 | 85 | 6.90 / 5.15 | 15.9 |
| 14 | 47.39 | 85 | 7.52 / 5.77 | 14.9 |
| 15 | 50.96 | 85 | 8.18 / 6.43 | 13.9 |
| 16 | 54.58 | 85 | 8.89 / 7.14 | 12.8 |

Rope never slack for vC 13 to 16. Unclamped 28 m/s: slack at 100.5 deg from tick 78 (claim confirmed); 24 m/s stays taut (86 deg, tension 1.3).

**4.6 landings, exact (past the far edge, m; Perfect = release tick 47 with the window range in brackets; Good-early = tick 27, Good-late = tick 66, Poor = auto at tick 85):**

| vC | Perfect | Good-early | Good-late | Poor | Perfect rope angle / grab-to-release m / release (vx, vy) / feet y / flight s |
|---|---|---|---|---|---|
| 13 | **7.11** [6.58 .. 7.30] | 2.86 | 3.42 | 3.32 | 32.0 deg / 8.7 / 10.2, 6.2 / 3.4 / 1.15 |
| 14 | **8.98** [8.24 .. 9.34] | 4.81 | 4.47 | 4.20 | 34.7 deg / 9.2 / 10.5, 6.9 / 3.7 / 1.24 |
| 15 | **10.87** [9.89 .. 11.45] | 6.93 | 5.52 | 5.05 | 37.4 deg / 9.7 / 10.7, 7.7 / 4.1 / 1.34 |
| 16 | **12.76** [11.52 .. 13.61] | 9.20 | 6.54 | 5.87 | 40.1 deg / 10.3 / 10.9, 8.5 / 4.5 / 1.45 |

Rope angle at the windows (vC 14): Good 19.3 to 30.6 deg (ticks 27..41), Perfect 31.3 to 37.7 deg (42..52), late Good 38.3 to 47.4 deg. Grab-to-release distance for any grade and vC:
5.5 to 12.7 m. Perfect ring: `vy / 16` = 0.39 to 0.53 s after release. Whole airborne time grab to landing (Perfect): 1.91 to 2.22 s.

**Fairness bots (20k trials x 6 speeds per profile, release sigma 70 / 180 / 260 ms, jump sigma 33 / 67 / 100 ms):** Perfect 80.9 / 38.6 / 26.9 %, Good 18.9 / 60.6 / 66.1 %,
Poor 0.0 / 0.3 / 3.5 %, no grab (Miss) 0.2 / 0.5 / 3.5 %. Grab success: expert 99.8 %, average 99.5 %, new 96.5 % (targets 90 and 75: met; at double the jump sigma new drops to 72.7 % at 12 m/s).
The average Perfect rate is 1.4 points under the 40 % cap: at sigma 150 ms it would be 45.9 % with 11 ticks (38.3 % with 9 ticks).

**Open for game-designer (not applied):** (a) accept the corrected guarantee "Perfect 6.6 to 13.6 m, minimum 2.86 m" or choose `GoodStartMs` 500 (minimum 3.23 m) and/or `CatchMaxSpeedMps` 15
(Perfect at most 11.45 m); (b) `PerfectImpulseMps` 2.0 is not recommended (the best Good would out-throw Perfect); (c) cost of a Poor swing at 21 m/s is 33 m, 1.6 s of progress (Perfect 20.5 m, 0.98 s).
