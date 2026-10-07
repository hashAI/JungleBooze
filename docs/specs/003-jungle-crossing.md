# Spec 003: Jungle crossing (curved route, layers, natural swings)

**Owner:** game-designer | **Builders:** gameplay-engineer (PathFrame, route, camera math), ui-engineer (views, camera, HUD touch-ups),
asset-pipeline (art kit, ribbons, scenery placement) | **Status:** Ready to build (numbers are first guesses, tuned at the feel check) |
**Last updated:** 2026-10-07

**Source of truth:** `design/DECISIONS.md` row 2026-10-07 ("Core feel: a wild kid crossing the jungle", approach A), GDD 6 (camera),
7 (vines), 8 (obstacles), 9 (worlds), specs 001 and 002 (simulation, track), `design/STYLE_GUIDE.md`. This spec does not change any
simulation number. `[ASSUMED]` marks a designer default that stands until the owner says otherwise.

**Covers:** the `PathFrame` (presentation-only mapping from path space to a curving 3D route), route generation, camera behavior, lane
mapping on curves, layers (forest floor and canopy), natural swings, terrain obstacle skins, per-world path styles, scenery rules and
budgets, fairness on curves, art asset list, implementation tasks, risks.

**Out of scope:** real 3D terrain or physics (decided against), new obstacle types, new hitboxes, new simulation rules, music, store art.

---

## 1. Player story and purpose

"I am not running on a road. I am a wild kid crossing a living jungle: the trail bends round huge roots, climbs onto a mossy ledge,
drops to a stream bank, and then a giant tree reaches a swaying vine out over a canyon. I swing, let go at the right moment, land in
a sunlit clearing, and Duko whoops overhead. Later the trail climbs into the treetops, where I run along thick branches with the
forest floor far below."

Why this makes the game better:
- Pillar 3 (vine swinging looks great in a 5 s clip): a vine that hangs from a real tree over a real canyon, with a banking camera,
  is a clip. A vine hanging from nothing over a straight road is not.
- Pillar 4 and retention: each run of each world feels like a different journey, not the same corridor with a new texture.
- Pillar 1 stays on top: every curve, slope and tree is placed so that obstacles are never hidden (section 9).
- Pillar 2: controls are identical (swipe left means the left lane, always), no gameplay number changes.

---

## 2. Principles (binding)

1. **Gameplay stays in path space.** The simulation (spec 001, 002, vines, power-ups) keeps using `s` (distance along the path,
   today `z`) and `x` (lateral, lane centers -2.4, 0, +2.4 m) and `y` (height). It never reads the route, the camera, or any scenery.
2. **One mapping for everything seen.** Every view (ground, obstacles, coins, runner, Duko, vines, power-ups, camera, scenery) places
   itself through `PathFrame.ToWorld(s, x, y)`. No view uses raw `z` as world z any more.
3. **What you see is what is tested.** An obstacle that the sim says is at `(s, lane)` is drawn exactly there. Curves change how it
   looks on screen, never where the hitbox is.
4. **Fewer, deeper pieces.** One route generator with ~10 beat types, two layers, five obstacle skins per world (the existing kit),
   one swing rig. Worlds differ by beat weights, limits and art, not by code.
5. **Quality bar over date** (project rule 10): the route and swing look are done only when the 5 s clip test in 14.6 passes.

---

## 3. PathFrame

### 3.1 Definition

`PathFrame` is a plain C# class in a new namespace `JungleBooze.Gameplay.Path` (presentation only). It owns the route table and
answers pose queries.

| Member | Contract |
|---|---|
| `void Extend(...)` | Appends route up to a new `sEnd` (section 4). Called by the presentation when the track spawns a chunk. |
| `PathPose Sample(double s)` | Returns the pose of the centerline at `s`: position `C`, unit tangent `T`, right `N`, up `U` (all include bank), `curvature` (rad/m, + = turning right), `gradePct`, `bankDeg`, `layer`, `surface` (ground kind), `halfWidthM`. Clamped to the built range. No allocation (struct out). |
| `Vector3 ToWorld(double s, float x, float y)` | `C(s) + x * N(s) + y * U(s)`, in `WorldRoot` space. `x` is the sim lateral, `y` the sim height. |
| `Quaternion RotationAt(double s)` | Frame rotation (forward `T`, up `U`) for oriented views. |
| `float GroundHeightAt(double s, float x)` | `x * tan(bank)` surface offset (for scenery bases and shadows). |
| `bool IsBuilt(double s)` | True if the route covers `s`. |

- `s` is the **3D arc length of the centerline** (not its horizontal projection), so m/s on screen equals m/s in the sim on slopes.
- Storage: ring buffer of samples every **1.0 m** (position, heading, pitch, bank, curvature, layer, surface), covering
  `[sHero - 40 m, sHero + 200 m]`; Catmull-Rom between samples. Memory fixed at start, no allocation per frame.
- `x` is measured along `N` on the banked surface; `y` along `U`. At the maximum bank of 6 degrees the error against world-up for a
  1.5 m jump is 0.08 m, accepted. Jump arcs, vine arcs and coin arcs are drawn in frame coordinates.
- **Lane distortion:** all lanes share `s`. On a bend of radius R the lane at lateral `x` has an arc length of `(1 - x/R)` times the
  path's (inner shorter). At R = 75 m and x = 2.4 m that is 3.2 percent: object depths are not rescaled (error under 0.1 m for a 3 m
  obstacle). Accepted [ASSUMED].
- **Floating origin:** all scenery, ground, views and cameras live under one `WorldRoot`. When the camera is more than **800 m** from
  the origin, translate `WorldRoot` by minus the camera position (rounded to 100 m) in one frame. Particle systems use local simulation
  space or are cleared.
- **Isolation rule:** nothing under `Gameplay/Runner`, `Gameplay/Track`, `Gameplay/Vine`, `Gameplay/PowerUps`, `Gameplay/Hazards`,
  `Gameplay/Companion` (simulation files), `Core` may reference `JungleBooze.Gameplay.Path`. An EditMode test greps for it (AC-301).

### 3.2 Which views change

| View | Change |
|---|---|
| `GroundView` | Replaced by the ribbon builder (section 5.4). |
| `ObstacleView`, `GapView`, `HazardView`, `CoinView`, `PowerUpView` | Position from `ToWorld(s, x, y)`, rotation from `RotationAt(s)` (obstacles) or none (coins). |
| `RunnerView`, `CompanionView` | Position from `ToWorld`; Duko adds route-aware lead (section 12). |
| `VineView` | Branch, span, rope (section 8). |
| `FollowCameraView` | Rewritten around the route (section 6). |
| `SkyView`, light rig | Light yaw follows the low-passed route heading (section 7.3). |
| `WorldThemeView` | Also picks the route style and the scenery kit. |

---

## 4. Route generation

### 4.1 Inputs and independence

- Inputs: `runSeed`, the world schedule (same distance table as the track: 0/1,100/2,300/3,600/5,000 m, GDD 9 and `WorldThemes`), and the **kind
  and length of each chunk the track generator has committed** (read-only: `Normal`, `Breather`, `Vine`, `Gateway`, `Start`).
- Output: the route table. The route never feeds back into the simulation (AC-301, AC-302).
- Random streams: new ids **Route = 6** and **Scenery = 7**, derived from the run seed with their own constants. Existing streams
  1 to 5 are untouched, so every existing seed produces the same track as before (AC-302).
- The route is extended chunk by chunk, in lockstep with the track: when chunk `k` is committed, the route is built over chunk `k`
  with one chunk of lookahead (the kind of chunk `k+1`). The sequence of beats is a pure function of (seed, committed chunk kinds).
  A replay with the same seed and the same inputs gives the same route. Float rounding across devices may differ below 1 cm and nothing
  reads it.
- **Requirement on the track generator (gameplay-engineer):** keep at least `viewSpawnAheadM (95) + 2 chunks (80) = 175 m` committed
  ahead of the runner and expose `TrackSimulation.PeekChunk(index)` (read-only kind, startZ, length). If a chunk arrives with less than
  one chunk of notice, the route applies the **emergency ease** (curvature to 0 at jerk 0.0015 rad/m^2, section 4.3) inside that chunk's
  first 25 m and the validator reports it (AC-312).

### 4.2 Beats

The route is a sequence of **beats**. Each beat sets a target curvature profile, a grade profile and a layer. Beats start and end on
the 5 m grid, aligned to chunk boundaries or inside a chunk.

| Beat | Length (m) | Curvature | Grade | Use |
|---|---|---|---|---|
| Straight | 40 to 100 | 0 (|kappa| <= 1/400) | -3 to +3 % | Rest, sight lines |
| GentleBend | 60 to 140 | R 150 to 300 m | -4 to +4 % | Flow |
| Bend | 60 to 120 | R `Rmin` to 150 m | -6 to +6 % | Turn into the jungle |
| SBend | 120 to 200 | two opposed bends, R 100 to 200 m | -5 to +5 % | The "winding" signature |
| Roll | 80 to 140 | gentle | sine +-2.5 m height, wavelength 80 to 140 m | Rolling forest floor, roots |
| Rise / Fall | 60 to 140 | gentle | +-6 to +`gradeMax` % | Climb a ledge, drop to a stream bank |
| Clearing | 40 to 60 | 0 | 0 to -2 % | Breath: open sky, light shaft, wide view (section 11.4) |
| Crossing | 20 to 40 | 0 | 0 | Ford, stepping stones, log bridge, mud (cosmetic ground, section 10.3) |
| Bridge | 60 to 90 | 0 | -2 to +2 % | Rope bridge (Mountains, River), temple walkway (Ruins) |
| Ascent / Descent | 90 to 130 | gentle (R >= 200) | up to +12 % / -12 % | Move between layers (section 7) |
| Swing zone | per vine section | |kappa| <= 1/250 (R >= 250 m) | |grade| <= 4 % | Natural swings (section 8) |
| Gateway | 54 | 0 | 0 | World boundary (spec 002 gateway chunk) |

### 4.3 Limits (hard, enforced by a route validator)

| Limit | Value [ASSUMED] | Why |
|---|---|---|
| `Rmin` sustained (per world, 4.4) | Jungle 75 m, River 90, Mountains 80, Ruins 85 | Next 40 m stays on screen (6.1) |
| Max yaw rate at 21 m/s (top normal speed) | 16 deg/s (R 75 m); hard cap **28 deg/s** at any speed up to the boost speed 33.6 m/s | Camera stays calm, no motion sickness |
| Curvature jerk (change of kappa) | **0.0006 rad/m^2** (0 to 1/75 in 22 m); emergency ease 0.0015 | Smooth clothoid entry and exit, no visible kinks |
| Max turn over any 120 m window | **75 deg** (R 75 m only for up to 80 m at a time) | The route never doubles back into its own view |
| Heading restoring | Soft pull of the long-run heading toward the world's forward axis: never more than **+-50 deg** from it (low-pass over 300 m) | The golden light and sky stay ahead of the runner |
| Grade, sustained | +-10 % (Mountains climbs +-14 %), +-12 % on Ascent/Descent | 6 to 8 deg slopes read as hills, not walls |
| Grade change rate | Crest (convex): vertical radius >= **250 m**. Sag (concave): >= 120 m | Sight distance over a crest from camera height 3.2 m to a 0.5 m obstacle is `2.5 * sqrt(2 * 250)` = 56 m, above the 45 m needed |
| Bank | `bankDeg = clamp(6 * kappa / (1/75), -6, 6)`, smoothed by the same jerk limit | Ground tilts into the turn |
| Net elevation per world | River -30 m, Mountains +60 m, Jungle and Ruins +-15 m; the route is rebased by the floating origin | Journey shape, no drift to infinity |
| Straight run before a vine section / gateway | Route reaches R >= 250 m at least **30 m before** the chunk start (one chunk of lookahead) | Swing zone readable (section 8) |
| First 25 s of a run and the whole tutorial | R >= 200 m, |grade| <= 4 % | Onboarding is calm [ASSUMED] |

### 4.4 Beat weights per world (percent of route length, a seeded weighted pick per beat, no beat repeated more than twice in a row)

| Beat | Jungle | River | Mountains | Ruins |
|---|---|---|---|---|
| Straight | 8 | 6 | 12 | 14 |
| GentleBend | 18 | 38 | 12 | 14 |
| Bend | 18 | 10 | 14 | 14 |
| SBend | 16 | 8 | 20 (switchbacks) | 8 |
| Roll | 12 | 6 | 4 | 4 |
| Rise / Fall | 8 | 12 (fall-biased) | 24 (rise-biased) | 18 (terraces) |
| Clearing | 8 | 6 (sandbar) | 6 (ledge) | 8 (courtyard) |
| Crossing | 4 | 8 | 0 | 4 |
| Bridge | 0 | 4 | 8 | 12 |
| Ascent/Descent pair | scheduled, 11.2 | scheduled (every 600 to 900 m) | scheduled as cliff ledge | scheduled as wall-top |

The Gateway and Swing zone beats are forced by chunk kind, never drawn from this table. A Clearing is forced if none occurred in the
last **350 m** (so the player always gets a breath).

---

## 5. Ground ribbons and terrain beside the path

### 5.1 Ribbon

The ground is a **procedural ribbon mesh** following the route, in pooled 20 m pieces (8 pieces alive). Cross-section rings every
**2.5 m**, 6 vertices per ring: left skirt, left shoulder, left lane edge, right lane edge, right shoulder, right skirt.

| Value | Start value |
|---|---|
| Playable width | 7.2 m (3 lanes x 2.4 m), `halfWidthM` 3.6 |
| Shoulders | 0.8 m each side (path border, roots, moss), not playable |
| Skirt | 1.5 m down, fades into fog color |
| UV | v = distance along s / 2.4 m (existing trail texture), u across |
| Mesh update | `SetVertices` on pooled arrays when a piece enters the build window; no allocation after prewarm |

Chord error against the true curve at 2.5 m rings: `2.5^2 / (8 * 75)` = 0.01 m. Good.

### 5.2 Surface kinds (`surface` in the pose; one material each, shared atlas)

`Trail` (dirt, roots, moss), `Bough` (canopy limb, section 7), `Ledge` (stone/rock), `Planks` (rope bridge, temple walkway),
`Ford` (shallow water over stones, cosmetic), `Mud` (cosmetic). Surfaces switch only on beat boundaries, with a 2.5 m blend ring.
**Gameplay is the same on all surfaces** (no slowing, no slipping): the sim has one friction, one speed.

### 5.3 Terrain beside the path (no height-field mesh)

Scenery bases follow a simple profile per beat, never a real terrain: `terrainY(l) = pathY + profile(l)` where `l` is the distance from
the centerline. Profiles: `bank-up` (rises 1.5 m over 6 m, forest walls), `bank-down` (stream bank: falls 2 m over 5 m to a water
plane), `cliff` (vertical drop beside the path, 18 m, fog), `flat`. Chosen per beat and per side (left/right), seeded by the Scenery
stream. This is what makes a stream bank feel like a stream bank at almost no cost.

### 5.4 Gaps and rims

The existing sim **gap** (and vine **chasm**) is drawn as a break in the ribbon: the ribbon ends in a rim piece (roots/rock/broken
limb), the void shows the world's depth view (forest floor in fog, river with mist, canyon wall, temple pit), and the far rim piece
starts after the gap. Rim pieces are 2.4 m wide per lane, so partial gaps (one lane) read naturally. Red-and-ink flags at the near edge
stay exactly as specified (spec 002 3.6, `gapFlagLeadS` 1.0 s).

---

## 6. Camera

### 6.1 Rules

Base numbers from GDD 6 and `RunnerPresentationConfig` stay as defaults (6.0 m behind, 3.2 m up, FOV 60, lateral follow 70 percent).

| Parameter | Start value [ASSUMED] | Notes |
|---|---|---|
| Position | On the route at `s = sHero - 6.0 m`, lateral `0.7 * x`, up 3.2 m, in the **banked frame** | Replaces `new Vector3(camX, ..., z - behind)` |
| Look point (pitch and base yaw) | `ToWorld(sHero + 8 m, 0.7 * x, 1.0)` | Same 8 m / 1.0 m as today |
| Yaw lead | Aim yaw at `lerp(heading(sHero + 8), heading(sHero + 22), 0.5)` | Shows the bend coming; the tunable `yawLeadWeight` |
| Yaw smoothing | critically damped, time 180 ms, max yaw rate **40 deg/s** | Following a 28 deg/s route never lags more than 5 deg |
| Camera roll (bank) | `0.8 * bankDeg`, max **5 deg**, rate max 20 deg/s | Banks into turns |
| Pitch follow | 0.6 * route pitch, smoothing 250 ms, max 15 deg/s | Climbs feel like climbs; crest sight limits in 4.3 |
| Bend FOV bonus | `+4 deg * |kappa| / (1/75)` (max +4 deg), blend 300 ms | Safety valve so the next 40 m stay on screen |
| Speed FOV (boost) | +3 deg while Speed Boost is active, blend 400 ms | Replaces nothing, additive |
| Lateral follow / lane switch | unchanged: 70 percent, smooth 90 ms, measured along `N` | Lane switch visual <= 120 ms is unaffected (AC-307) |
| Obstruction | Camera path corridor has no scenery (section 11.1); no camera collision code | Camera never clips a tree |

**On-screen rule.** For the camera in steady state at both ends of the normal speed curve, with Pista in any lane, the points
`ToWorld(sHero + d, lane center, 0)` for `d` in {10, 25, 40} m lie inside the viewport with a **6 percent margin** on each side
(portrait 9:19.5, horizontal half-FOV about 14.9 degrees at FOV 60). Worked check at R = 75 m: bearing difference between the
yaw-lead aim (chord to `sHero + 15`) and the 40 m point is 9.6 deg; plus 3 deg of lateral lane offset gives 12.6 deg against an
allowed 14.0 deg. Margin is thin by design: the bend FOV bonus and `yawLeadWeight` are the tuning knobs and the validator in 14.2
proves it (AC-304).

### 6.2 Swing camera (the clip camera)

While `Locomotion.Carried` or `InVineFlight` (existing blend 250 ms, existing +10 deg FOV and 8 deg up-tilt stay):

| Parameter | Start value [ASSUMED] | Notes |
|---|---|---|
| FOV | 60 -> 70 (existing) | |
| Pull-back | camera 6.0 -> 7.5 m behind, +1.2 m up | Shows the canyon and the branch overhead |
| Side offset | up to 1.5 m toward the open side of the canyon (the side without the anchor tree) | Shows depth, not just the back of Pista |
| Roll | `swingAngle * 0.15` (about +-8 deg), plus route bank | Pendulum feel |
| Hang moment | presentation 0.8x for the first 300 ms (existing) | Unchanged |
| Landing | Blend back to the follow camera in 400 ms, FOV overshoot -2 deg then settle | A small "land" punch |

**Reduce Motion:** roll 0, pull-back and side offset 0, FOV shifts 0, yaw lead kept but max yaw rate 20 deg/s, bend FOV bonus 0.
Route bank of the ground stays (it is scenery).

---

## 7. Layers

### 7.1 Two layers, one lane model

| Layer | Look | Gameplay |
|---|---|---|
| **Floor** | Trail on the forest floor, trunks and walls beside, canopy overhead at 12+ m with light shafts | Normal |
| **High** | Jungle/River: the **canopy** (thick boughs high in the trees, ground 20 to 30 m below). Mountains: cliff ledge. Ruins: temple wall-top | Normal |

The High layer is a **visual skin on normal lane segments**. It adds no rule: three lanes, same hitboxes, same obstacle kit, same gaps.
- The ribbon becomes a **bough**: 7.2 m wide path built from 2 to 3 interwoven limbs, mossy, with a 0.8 m leafy rim (a soft visual wall
  at lane edge, never a death edge: lanes already clamp at +-3.6 m).
- Gaps are **broken boughs**: the limb ends, the next limb starts across the void. Void shows the forest floor 25 m below in fog,
  with light shafts rising through it.
- Below the bough: trunk columns going down into fog, hanging vines, leaf layers. Above: sparse leaves, sky holes, light shafts.
- Obstacle skins in the High layer (11.3). Coins and power-ups unchanged.

### 7.2 Schedule

- Jungle and River: a **Canopy section** every **500 to 800 m** [ASSUMED], 200 to 400 m long, as `Ascent` (90 to 130 m, up to +12 %)
  -> `High` run -> `Descent`. Ascent is dressed as a giant root ramp or fallen trunk ramp with a rope ladder look; Descent as a
  trunk spiral or ramp of limbs. Elevation difference 8 to 12 m (floor to bough).
- Mountains: cliff-ledge sections (same machinery, `Ledge` surface, drop on one side), every 400 to 700 m.
- Ruins: wall-top sections (`Planks`/`Ledge`), every 400 to 700 m.
- Rules: no High section in the first **300 m** of a run or within 10 s of a world gateway; High sections start and end on chunk
  boundaries; a vine section may lie inside a High section (swinging between tree crowns, ground far below, the best clip) but its
  first grab is at least 30 m after the Ascent ends.
- A High section never contains a one-lane gap longer than the existing sim gap (the sim decides gaps, the layer only skins them).

### 7.3 Light

One key light yaw follows the route heading low-passed over 200 m (time constant about 9 s at 21 m/s), so it keeps coming from
ahead of the runner (GDD 9.2) and Pista keeps her bright rim. Light shafts are additive quad cards (no bloom, no shadow maps): 1 shaft
per 40 to 60 m, max 3 in view, brighter in Clearings and in the High layer.

---

## 8. Natural swings (vine sections)

> **Superseded in part (2026-10-07, owner direction):** the swing is now a true pendulum about a fixed pivot at the branch tip (rope 14 m, pivot 17 m up,
> 16 m chasm, pivot spacing 18 m). The span plus sliding knot (8.1), the "simulation is unchanged" sentence below, AC-318, AC-319, AC-321 and risk R1 are replaced by
> `docs/specs/004-fixed-pivot-swing.md`. Layout, per-world dressing, chains and edge cases below still apply except where spec 004 section 8 says otherwise.
> The text below is kept as written for history.

The simulation is unchanged: grab zone 2.0 m x 1.6 m, y 1.6 to 3.6 m, grab earliness 450 ms, swing 1.40 s over `VineConfig`
angles, chains up to 3, 18 m chasms (spec 001/GDD 7, `VineDesignValues`). This section only decides how it looks.

### 8.1 Geometry derived from the sim (no second curve)

- Rest pivot height `pivotY = GrabPointHeightM + SwingRadiusM * cos(SwingStartAngleDeg)` = 3.0 + 6 * cos(20 deg) = **8.64 m**
  above the path surface. At swing start the hand is at the grab point, so the rope is tilted 20 deg back; its top is
  `SwingRadiusM * sin(20 deg)` = **2.05 m ahead** of the grab point: `pivotS = grabS + 2.05`.
- During the swing, the existing view already draws the rope from the hand along `SwingAngleRad`: the pivot rides with the runner along
  `s`. **The arc is the sim's. There is no authored curve.** The presentation derives `hand = ToWorld(...)`, `pivot = hand + R * up(angle)`
  in frame coordinates and maps both through `PathFrame`. Rope end and hand coincide to within 0.02 m (AC-321).
- Because the pivot travels with the runner (about 11 m at 8 m/s up to 29 m at 21 m/s during 1.4 s), the rope cannot hang from a single
  point on a branch. **Solution: the swing line.** A thick overhead limb or creeper cable (a "span") at `pivotY`, running along the
  path from the anchor tree on the near rim to a tree on the far rim. The vine is a **long liana looped over the span**, free to slide:
  its top is a leafy knot that rides the span, hidden in foliage tufts. At rest the knot is parked at `pivotS`.
  Required span length per vine: `2.05 + SwingTravelM + 4 m` where `SwingTravelM = v * 1.4 s` at the highest speed at which that
  section can spawn (e.g. 29 m + 6 = **35 m** at 21 m/s; 20 m at 10 m/s). Chains are covered by consecutive spans overlapping by 6 m.
  [ASSUMED] (see risk R1 and open question Q2).

### 8.2 Layout of a swing section (maps onto the existing 60 to 110 m vine chunk)

```
 near rim                      canyon / river / gorge                    far rim (landing glade)
 [approach 30 m: trail,          18 m chasm (per vine)                    [landing pad 20 m, clearing,
  anchor tree beside the path]  ---- span (limb or creeper) at 8.6 m ---   light shaft, Duko cheers]
        T1 (anchor tree) ====== vine 1 hangs, swaying ====== T2 (far tree) [== vine 2 ... T3 ...]
```

| Element | Rule | Start value [ASSUMED] |
|---|---|---|
| Anchor tree (near rim) | Giant trunk **6 to 9 m beside the path** (outside the sight corridor, 11.1), height 28 to 40 m, a limb reaching **over the path at 8.64 m** | Trunk diameter 2.5 to 3.5 m |
| Far tree | Same, on the far rim, on the **opposite side** of the path from the anchor tree when the section is a chain (zigzag, so each limb is seen from the side) | |
| Span | Limb (tapered, mossy) or taut creeper cable, at `pivotY` +-0.3 m; may bend gently (route R >= 250 m) | Diameter 0.5 to 0.8 m |
| Vine | Liana, dia 0.08 m (existing rope), lower end tuft of leaves 1.2 m below the grab point, glow core + ring icon at the grab point (existing), **hangs over its lane**, lane `0`, `1`, `2` | Rope length to pivot 6.0 m |
| Visibility | The anchor tree, span and the first vine are in view at **>= 2.0 s** (42 m at 21 m/s, 56 m at the top of the boost range) before the grab zone, inside the fog start of 45 m; so the route is straight and the sight corridor free (sections 4.3 and 11.1) | Duko calls at 2.0 s (existing) |
| Already swaying | Vines sway from spawn: amplitude 5 deg about the rest angle, period 3.2 s, phase from the Scenery stream; amplitude fades to **0.5 deg** during the last 30 m before the grab zone (so the glow does not move out of the 2 m box), full amplitude again after release | The glow stays at the grab point; only the upper rope and leaf tuft sway |
| Landing | Far rim clearing with a flat 20 m pad, 24 m wide, grass and a light shaft, no scenery in the sight corridor, floor "cushion" decals | Matches the existing 1.0 s obstacle-free pad |
| Chasm below | Per world (8.3), visible from 56 m, near rim flags per spec 002 | Depth 25 m fog |
| Ground vines | A "safe" vine (ground below) hangs from the same anchor/limb over a shallow ravine, root garden or stream; missing it only loses the bonus | Same art, no void |
| Grab ring | Existing glow + ring icon at 2 Hz at the grab point; additionally a **glow-lit knot** on the span above each vine so the player can follow the span with the eye | Sun-gold, no bloom |

### 8.3 Per world canyon dressing

| World | Void and rims | Span |
|---|---|---|
| Jungle | Ravine with roots, moss walls, mist | Giant tree limb |
| River | River gorge with rapids and mist below, rocky rims | Limb with hanging creeper |
| Mountains | Cliff gap, snow rims, rope-and-wood anchor posts (the vine skin is a rope) | Rope strung between two rock pillars (the "tree" is a pillar with a beam) |
| Ruins | Temple pit, broken floor, a hanging chain (vine skin is a chain) | Stone beam or carved arm from a pillar |

Same vine rig and glow in every world (GDD 8.3).

### 8.4 Chains across canyons and rivers

A 3-vine chain is 3 spans linked tree to tree (T1 ... T4), with 18 m chasms between the grab points as in the sim; between vine 1 and
vine 2 the void continues (a long canyon) with a **mid-rock or mid-tree** on one side so the player sees where the next limb comes from.
Route in a chain: R >= 200 m allowed, never an S inside the swing zone.

### 8.5 Edge cases

- **Grab missed over ground:** the rope's top knot keeps sliding with the unused vine (sway restarts), nothing else.
- **Missed over a chasm:** existing death presentation (0.35 s hit-pause, camera on the missed vine); the swing camera pulls to the
  missed vine and the span. Death cause "Missed vine" unchanged.
- **Grab at a different lane than aimed:** the span covers all 3 lanes (it crosses the full path width at `pivotY`); the knot is under
  the vine's lane.
- **Release during a bend (chain with R >= 200 m):** the launch arc is the sim's `(s, x, y)`, mapped through the frame, so the arc follows
  the bend. Fly path never leaves the landing pad width because lateral `x` is unchanged.
- **Tutorial:** the first vine section uses a straight route (R >= 400 m) and a ground vine.

---

## 9. Fairness on curves (pillar 1)

1. **Telegraph distances unchanged:** every answer is visible >= 1.2 s before impact, at any speed (spec 002 section 9), at most 25.2 m
   (40.3 m at boost speed). The route validator (14.2) projects every obstacle front, mover end circle and gap flag into the camera at
   1.2 s and at 2.0 s before impact; all must be inside the viewport with a 6 percent margin and not occluded.
2. **No hiding behind bends:** on a bend the inside of the turn can block the view across the corner. The **sight corridor** (11.1) is kept
   free of scenery; the validator checks the line of sight from the camera to every obstacle's top and base.
3. **No hiding behind crests:** vertical crest radius >= 250 m (4.3). A Rise/Fall beat that ends in a crest is followed by >= 25 m of
   grade <= 3 percent before any obstacle row [ASSUMED] (obstacle placement is the track's; the route moves the crest away: the route
   builder aligns crests to chunk gaps/breathers where possible, and otherwise lowers the grade until rule 1 passes).
4. **Movers on curves:** the mover end-lane marker and boulder path use the frame, so the path stays visible; the 1.4 s start remains.
5. **Obstacles are drawn upright and oriented to `T(s)`** (never skewed by banking more than 6 deg).
6. **No decorative thing may look like a hazard** (STYLE_GUIDE 4.1: angular/spiky + red = do something): scenery is round and green or
   brown; hazard red appears only on hazards. Roots and stumps beside the path stay out of the lane band (|x| > 4.2 m).
7. **Death is explained:** the Game Over cause texts of spec 002 12.4 are unchanged; the death camera freezes on the cause (existing hold).
8. Lane marker cues (wear tracks) continue on all surfaces so the player always reads three lanes.

---

## 10. Obstacles from the terrain

Hitboxes, categories, telegraph and display names are those of spec 002 5.1 and 3.8. Only the visual skin changes. Skins must cover the
hitbox silhouette within 0.08 m (existing `grayBoxVisualMarginM`): terrain-shaped skins are authored inside a hitbox template (a
"fit box" in the art file).

### 10.1 Skins per world and layer

| Archetype (answer) | Jungle floor | Jungle canopy | River | Mountains | Ruins |
|---|---|---|---|---|---|
| Low barrier (jump) | **Fallen mossy trunk**, root hump | Branch fork stub, knotty burl | Driftwood log on the bank | Snow drift, boulder row | Broken column drum |
| High barrier (slide) | **Root arch** to slide under, low branch | Hanging vine curtain, low limb | Hanging creeper net / low branch | Overhanging ice/rock ledge | Fallen lintel, stone beam |
| Full block (change lane) | **Boulder**, giant trunk, rock outcrop | Thick trunk through the bough, large burl | River rock | Rock pillar | Statue, pillar |
| Mover (lane timing) | Rolling boulder | Rolling hollow log | Drifting raft/log | Rolling snowball | Rolling stone disc |
| Gap | Ravine (roots, mist) | Broken bough | Creek / gorge | Cliff gap, broken bridge | Pit, broken floor |

Existing display names (Fallen log, Low branch, Giant tree trunk, Rolling boulder, Ravine) stay the Game Over texts in the Jungle
(spec 002 3.8); new skins add names ("Root arch", "Boulder", "Hanging vines", "Broken bough") in `WorldSkin` display names.

### 10.2 Hanging vines (duck)

"Hanging vines to duck" is the High barrier skin: a curtain of vines hanging from an overhead branch with exactly the existing
high barrier clearance (spec 002 5.1); striped red-and-ink underside stays on the lowest hanging band so the rule
"red underside = slide" is kept. These vines are dull green, never glowing: the **only glowing vines are the swing vines** (a rule that
keeps pillar 3's glow unique).

### 10.3 Streams, mud and fords

A **Crossing** beat is cosmetic: ground continues, water or mud decal, small splash particles on foot contact, a soft sound. No slow-down.
A **gap** in a stream is a creek gorge too deep to cross. This keeps "water = safe unless a ravine rim is visible".
**Rule:** a cosmetic crossing never uses hazard red, and never has a visible rim.

---

## 11. Scenery

### 11.1 Bands (lateral distance `l` from the centerline) and the sight corridor

| Band | `l` (m) | Contents | Rule |
|---|---|---|---|
| Path | 0 to 3.6 | Lanes | Only authored obstacles, coins, power-ups, vines, rims |
| Shoulder | 3.6 to 4.2 | Border, moss, small roots | Flat decor under 0.15 m |
| Sight corridor | 0 to 7.0 (inner side of a bend), 0 to 5.0 elsewhere; heights 0 to 4 m (and 4 to 7 m up to `l` 5.0) | **Nothing** except the swing section's span and anchor limb; leaves and ferns only as ground-hugging cards under 0.3 m | Never occludes the camera-to-obstacle sight lines (9.2) |
| Near ring | corridor edge to 9 | Ferns, bushes, small rocks, roots, thin trunks (dia <= 0.6 m, spacing >= 4 m) | 1 prop per 2.5 to 4 m per side |
| Mid ring | 9 to 25 | Big trees, boulders, stream bank, cliff face | 1 big tree per 10 to 14 m per side; canopy trunks at 5.5+ m only in "dense" beats |
| Far ring | 25 to 90 | Silhouette impostors (2 to 3 quads), fog-colored | Alpha-tested, 1 draw per type |
| Overhead | height 12+ m (Floor layer) | Leaf masses, hanging vines (non-gameplay, short), light shafts | Cards only; none below 7 m inside the corridor |

### 11.2 Placement is stateless

Scenery is placed per **10 m cell** from a hash of `(seed, Scenery stream, cell index, band)`, not from a sequential RNG, so a cell
rebuilt after streaming is identical (AC-310) and the player's actions can never change a cell. Props use `ToWorld(cellS, l, terrainY)`
and are rotated by `RotationAt` plus a hashed yaw jitter. Pooled, instanced per prop type.

### 11.3 Per-beat dressing (density modifiers)

| Beat | Near/mid ring density | Notes |
|---|---|---|
| Dense (default Jungle) | 100 % | Trunks at 5.5 to 7 m, overhead leaf cards from 7 m up to 12 m outside the corridor; "closing in" feeling |
| Clearing | 40 % in the near ring, mid ring pushed to >= 14 m, overhead open | Sky visible, 1 to 2 light shafts, Duko circles |
| Crossing | 70 %, water plane both sides | |
| Bridge | 30 %, void both sides, far cliffs | Hand-rails as rope or stone |
| High layer | Near ring: leaf tufts and vines only; mid ring: trunk columns down into fog every 14 to 20 m | Light shafts through the leaf holes below |
| Swing zone | As Dense but the corridor extends to `l` 9 m on both sides for the 60 m of the swing | Free view of the span and canyon |

### 11.4 Clearing rules ("breath")

At least one Clearing within every **350 m**; Clearings are 40 to 60 m, flat, with open sky. No Clearing within a swing zone; the
landing glade after a swing counts as a Clearing.

### 11.5 Budgets (hints; the performance audit refines them) [ASSUMED]

In view window `[sHero - 10 m, sHero + 95 m]` (fog end 90 m): **<= 60k scenery triangles, <= 60 scenery draw calls**, within the global
style-guide budget (draw calls 78/120, tris 137k/150k need the benchmark scene).

| Item | Tris | Draws |
|---|---|---|
| Ground ribbons (5 pieces, 1 atlas material) | 4k | 6 |
| Near-ring props (instanced types: fern, bush, rock, root, thin trunk) | 8k | 12 |
| Big trees (about 16 in view, 2 LODs, trunk+crown separate materials) | 28k | 16 |
| Far-ring impostors | 3k | 4 |
| Overhead leaf cards, vines, light shafts | 8k | 8 |
| Water / mist / void | 3k | 4 |
| Special sets (anchor trees, spans, rim pieces) | 6k | 10 |
| **Total** | **60k** | **60** |

The look pass's baked 12 m "jungle wall segments" (~58k tris, ~20 draws) are the **stopgap**: each 12 m piece is placed with
`ToWorld` and `RotationAt` per piece (error `12^2 / (8 * 75)` = 0.24 m, wedge gaps on the outer side hidden by fog and overlap rules)
until the modular props exist (T3 in section 15).

---

## 12. Duko and the runner on the route

- Duko flies **ahead** at `s = sHero + 7 m` (existing), `y` 4 to 5 m above the path, and now **leads into bends**: lateral target
  `min(1.5 m, 1.5 m * |kappa| / (1/75))` toward the inside of the bend, banks its body into the turn
  with the route bank times 1.5, and tilts with the slope.
- Before a swing zone, Duko's call-out (existing "Vine!") is paired with a swoop along the span, landing briefly on the anchor limb
  (perches until the player grabs), then rejoins.
- In Clearings Duko circles once (cosmetic), and after a Perfect it whoops (existing cheer) and does a barrel roll over the landing glade.
- Duko never leaves the screen: a clamp keeps its screen position inside the upper third (GDD 9.2), tested in 14.2.
- Runner: the existing lean on lane change; on bends, the body leans into the turn by `bankDeg * 1.0` (max 6 deg), no sim effect.

---

## 13. Per-world path styles (summary)

| World | Journey | Route character | Layers | Signature moments |
|---|---|---|---|---|
| Jungle | Game trail through dense jungle | Winding: SBends, rolls, roots; Rmin 75; grade +-10 % | Floor + canopy (every 500 to 800 m) | Giant tree swings over ravines, golden light shafts, clearings; later the dusk variant (lighting only) |
| River | Along rapids and banks | Long sweeps (R 150 to 300), falling grade, net -30 m; Rmin 90 | Floor (bank) + tree-crown canopy | Fords and stepping stones, driftwood, canyon-river swings with rapids below |
| Mountains | Cliff ledges and rope bridges | Switchbacks (SBend 20 %), climbs +14 %, net +60 m; Rmin 80 | Floor (ledge) + cliff ledge as High | Rope bridges (straight, void both sides), snow, rope-and-pillar swings |
| Ruins | Temple ledges and arches | Terraces (flat runs + stair ramps), Rmin 85, stone arches every 40 to 60 m (clearance >= 5.5 m, outside the corridor sides) | Floor (courtyards) + wall-top | Overgrown temple arches, chain swings across pits, torchlight |

The gateway chunks (cave mouth, waterfall, rope bridge, temple gate) stay straight and flat for 54 m (spec 002).

---

## 14. Numbers, feel targets, acceptance criteria, simulation

### 14.1 Config assets (all in `Assets/_Game/Config`, presentation only, never in the sim hash)

| Asset | Fields |
|---|---|
| `RouteTuning.asset` | per world: `rMinM`, `gradeMaxPct`, `bankMaxDeg`, `kappaJerk`, `turnWindowM`, `turnWindowMaxDeg`, `restoringDeg`, beat table (weights, lengths), `clearingEveryM`, `canopyEveryM`, `canopyLengthM`, `ascentLengthM`, `ascentGradePct`, `calmStartS` |
| `CameraRouteTuning.asset` | all of 6.1 and 6.2 (`yawLeadWeight`, `yawTimeMs`, `maxYawRateDegS`, `rollGain`, `rollMaxDeg`, `pitchGain`, `bendFovBonusDeg`, `boostFovDeg`, `swingPullBackM`, `swingSideOffsetM`, `swingRollGain`) |
| `SceneryTuning.asset` | bands, corridor widths, densities per beat, budgets, cell size |
| `SwingDressingTuning.asset` | `swaySwingDeg`, `swayPeriodS`, `swayFadeDistanceM`, `spanLengthPadM`, `treeSideOffsetM`, `treeHeightM`, `landingPadWidthM` |

`RunnerPresentationConfig` keeps its existing camera values; the new asset only adds route fields.

### 14.2 Tools (written by gameplay-engineer in T2)

`RouteValidator` (EditMode test + menu `JungleBooze > Route > Validate seeds`): builds routes for N seeds x 5,000 m, simulating chunk
kinds from the real generator with scripted bots (so Boost holds and other player effects are covered), and checks sections 4.3, 6.1
on-screen rule and 9. `ViewportProbe` projects points through the camera model without Unity rendering (pure math).

### 14.3 Feel targets (measurable)

| Target | Value |
|---|---|
| Lane switch visual (unchanged) | <= 120 ms on straight and on R = 75 m bends |
| Camera yaw lag behind route heading | <= 5 deg at 28 deg/s |
| Camera yaw rate | <= 40 deg/s (<= 20 with Reduce Motion) |
| Camera roll | <= 5 deg; rate <= 20 deg/s |
| Next-40 m on-screen margin | >= 6 % each side |
| First sight of an anchor tree and span | >= 2.0 s before the grab zone |
| Frame time added by route and ribbons | <= 0.4 ms CPU on the lowest supported device (iPhone 11 / SE 2) |
| Allocations per frame from route, ribbons, scenery | 0 |

### 14.4 Acceptance criteria

Isolation and determinism
- **AC-301** An EditMode test fails if any file under the simulation namespaces references `JungleBooze.Gameplay.Path`, `Camera`,
  `Transform` or scenery types.
- **AC-302** All spec 001 and 002 golden traces (`tools/sim` and EditMode) are byte-identical with and without the route; every existing
  seed yields the same track.
- **AC-303** For 1,000 seeds, building the same route twice (same committed chunk kinds) gives identical beat lists and samples within
  0.01 m. Different seeds give different beat lists in >= 99 percent of pairs.

Route limits
- **AC-304** Fuzz of 10,000 seeds x 5,000 m x 4 worlds: no violation of 4.3 (Rmin, jerk, yaw rate, turn window, heading restoring, grade,
  crest and sag radius, bank, straight-before-vine) and no beat repeated more than twice in a row; each 350 m window has a Clearing.
- **AC-305** Same fuzz: for every sampled `sHero`, in both speed ends and lanes 0, 1, 2, the 6.1 on-screen rule holds with the
  default camera tuning.
- **AC-306** The route never comes closer than 60 m to itself outside a 120 m s-window (no doubling back into view).
- **AC-307** The lane switch (spec 001) measured on screen is <= 120 ms at R = 75 m (PlayMode, camera model).
- **AC-308** No scenery prop or ribbon vertex lies inside the sight corridor (11.1) for 1,000 seeds (automated check on placed cells).
- **AC-309** Any rebuilt cell is identical to its first build; two seeds give different dressing in >= 95 percent of cells.
- **AC-310** Floating origin: a 20,000 m run with scripted input shows no visible pop (positions of ground/camera continuous
  within 0.001 m across a rebase) and vertex jitter on the runner under 0.002 m.

Mapping
- **AC-311** For random `(s, x, y)`, `ToWorld` of the runner, an obstacle, and a coin at the same `(s, x, y)` return the same point;
  an obstacle's visual bounds contain its hitbox box (mapped) within 0.08 m on every side except the ground.
- **AC-312** If a Vine or Gateway chunk is committed with less than one chunk of notice, the emergency ease keeps jerk <= 0.0015 and
  the validator logs it; in normal runs it never triggers (0 in the 10,000-seed fuzz with the real generator).
- **AC-313** The track generator keeps >= 175 m committed ahead of the runner in 100 percent of ticks (assert).

Camera
- **AC-314** Camera values stay within 6.1 limits (yaw rate, roll, pitch rate) for 1,000 scripted runs, including swings and boost.
- **AC-315** Reduce Motion: no roll, no FOV shift, no pull-back, yaw rate <= 20 deg/s.
- **AC-316** Swing camera: FOV and pull-back reach 90 percent within 250 ms of the grab; return within 400 ms of landing; no clipping of
  the span (span is outside the camera path).

Swings
- **AC-317** For every vine in 1,000 generated sections: anchor tree and first vine are visible and unoccluded at 2.0 s before the grab at
  the planned speed; the route inside `[sGrab - 60, sGrab + sectionLength]` has R >= 250 m and |grade| <= 4 %.
- **AC-318** Rope end equals the sim's hand position within 0.02 m in every swing frame; pivot height at rest is `3.0 + R * cos(start
  angle)` (8.64 m) within 0.01 m, read from `VineConfig` not hard-coded.
- **AC-319** The span covers the pivot travel for the highest speed at which the section can spawn plus 4 m, in 100 percent of
  sections; chains have 6 m of overlap.
- **AC-320** The vine glow stays within the 2.0 m grab zone horizontally during the last 30 m of approach (sway amplitude <= 0.5 deg).
- **AC-321** The grab, release grades and score values are identical to the pre-spec run for the same input trace (replay test).

Fairness
- **AC-322** For 10,000 seeds x 5,000 m: every obstacle front, mover end circle and gap flag is inside the viewport (6 % margin) and
  not occluded by scenery at 1.2 s and 2.0 s before impact, at the planned speed and the boost speed (extends spec 002 AC-236/237).
- **AC-323** Hazard red appears only on hazard skins (asset audit script, all prefabs).
- **AC-324** No gameplay-relevant skin deviates from its hitbox silhouette by more than 0.08 m (fit-box test per prefab).

Layers
- **AC-325** A High section always begins >= 300 m into the run and >= 10 s after a gateway; Ascent and Descent grade <= 12 %; a vine
  section inside a High section starts >= 30 m after the Ascent.
- **AC-326** High-layer gaps and obstacles use the same sim data: the number and place of gaps equal a run with the Floor layer for the
  same chunk list (replay test).

Performance
- **AC-327** Scenery in view <= 60k tris and <= 60 draws on the benchmark route (perf scene); frame time +0.4 ms at most; 0 allocations
  per frame (profiler capture on iPhone 11 / SE 2).
- **AC-328** Ribbon rebuild causes no frame spike above 1.0 ms.

### 14.5 Simulation targets (balance-simulator)

No gameplay number changed; the targets are **regression only**: S1 to S9 (docs/sim-reports) identical for the same seeds.
New: simulate the route validator over 100,000 seeds for a report: share of route length per beat type per world, turn totals, max grade
distribution, number of Clearings per km, High share per world (target Jungle 20 to 30 percent of distance after 300 m).

### 14.6 The clip test (pillar 3, manual, owner)

Record a 5 s clip of a Perfect release in the Jungle, a River chain and a Mountains rope swing. Pass: anchor tree and limb are visible
in the opening second, the camera banks and pulls back without clipping, the canyon has depth, the landing glade shows light, no
popping. Fail items go back to the art or camera backlog.

---

## 15. Art asset list (asset-pipeline, with art-director)

Style: Inkbound Pulp (STYLE_GUIDE). Scenery is round, hazards angular. Triangles are per piece.

| # | Asset | Variants | Tris | Notes |
|---|---|---|---|---|
| A1 | **Giant tree with limb** (anchor/far tree) | 3 (jungle, river-bank, tropical), one-limb-left / right | 2.5k | Span socket at 8.64 m, limb reaching over the path, root flare |
| A2 | **Span** (limb or creeper cable) | 3 lengths (22, 35, 50 m), straight + 2 arcs | 0.8k | Tapered; tiling bark |
| A3 | **Swing vine** | 1 + skins (jungle, river creeper, rope, chain) | 0.5k | Rope 6 m, leaf tuft, sliding knot; glow card separate |
| A4 | **Hanging vine curtain** (duck) | 2 | 0.6k | High barrier skin, dull green |
| A5 | **Root arch** (slide) | 2 | 1.0k | High barrier skin, hitbox fit box |
| A6 | **Fallen trunk, mossy log** (jump) | 2 | 0.8k | Low barrier |
| A7 | **Boulder / rock outcrop** (block) | 3 | 0.8k | Full block |
| A8 | **Rolling boulder / hollow log** (mover) | 2 | 0.6k | Existing boulder reused |
| A9 | **Canopy bough platform pieces** | straight 20 m x 7.2 m, bend L/R, end cap, ramp up/down, broken end (gap rims) | 2k each | Ribbon with 2 to 3 limbs; moss; leaf rim |
| A10 | **Ascent/Descent pieces** | root ramp, trunk spiral | 3k | Climb up to 12 percent |
| A11 | **Rock ledge** (rim, cliff face, ledge ribbon) | 3 + cliff wall 20 m | 1.5k | Mountains, Ruins |
| A12 | **Rope bridge** | deck ribbon 20 m + rails + posts | 1.5k | Mountains, River |
| A13 | **Stream / ford / mud decals and water plane** | 3 | 0.3k | Cosmetic crossing |
| A14 | **Cliff / ruin set** | wall, terrace, arch (5.5 m clearance), pillar, statue, wall-top ribbon | 1.2k | Per world |
| A15 | **Path ribbon materials** | Trail, Bough, Ledge, Planks, Ford, Mud with shared atlas | | Existing trail texture reused for Trail |
| A16 | **Near ring props** | fern x2, bush x2, rock x3, root x3, thin trunk x2 | 100 to 400 | Instanced |
| A17 | **Overhead leaf cards, light shaft quad, mist card** | 4 | 0.1k | Additive, no bloom |
| A18 | **Far impostors** | tree row x3, cliff x2, temple x2 | 2 quads | Fog-matched |
| A19 | **Landing glade decals** | 2 | 0.2k | Grass, light |
| A20 | **Modular tree kit** (replaces baked walls) | 6 trunks, 4 crowns, 3 LODs | 1.0 to 2.5k | Prop types for mid ring |

Everything uses the Environment prefab auto-build and the `EnvironmentArt.Attach` loader (fit to a unit box or a named fit box).
Generation costs: Meshy/Blender per `docs/LICENSES.md`; the owner reviews A1, A3, A9 first (they set the look).

---

## 16. Implementation plan (small tasks; each one compile-checked and playable before the next)

| Task | Owner | Depends on | Deliverable | Done when |
|---|---|---|---|---|
| T1 | gameplay-engineer | none | `PathFrame`, route storage, `Sample/ToWorld/RotationAt`, floating origin hook, **straight route only** (identity mapping); `RunSceneBootstrap` creates it | AC-301/302 pass; game looks unchanged |
| T2 | gameplay-engineer | T1 | `RouteGenerator` (beats, limits 4.3, streams 6/7), `RouteTuning`, `RouteValidator`, track generator commit-ahead + `PeekChunk` | AC-303/304/306/312/313 pass in EditMode |
| T3 | ui-engineer | T1 | Adopt `ToWorld` in all views; ribbon builder; baked wall stopgap per segment; sky/light follow heading | Runner on a curved route (debug route) in the editor |
| T4 | ui-engineer | T2, T3 | Camera rewrite (6.1, 6.2), Reduce Motion, camera tuning asset | AC-305/307/314/315/316 pass |
| T5 | gameplay-engineer | T2 | Layer schedule (Ascent/High/Descent), `Bough` surface, gap rim skinning hooks | AC-325/326 |
| T6 | ui-engineer | T3 | Swing rig on the route: span, knot slider, sway, rope from frame, anchor trees (gray-box first) | AC-318/319/320/321 |
| T7 | asset-pipeline | art-director plan | A1, A2, A3, A9, A10 (the look-setting set), prefabs and fit boxes | Owner reviews the clip test on gray-box + real art |
| T8 | asset-pipeline | T3 | Modular tree kit + near ring props + overhead cards + stateless scenery placer (11.2) | AC-308/309/327 |
| T9 | ui-engineer | T6, T7 | Terrain obstacle skins A4 to A8 per archetype; display names; red audit | AC-322/323/324 |
| T10 | asset-pipeline | T8 | Per-world sets (A11, A12, A13, A14, A18) one world at a time: Jungle, River, Ruins, Mountains | Each world passes the clip test |
| T11 | ui-engineer | T4 | Duko lead/perch/circle (12), runner lean, haptics for swing landing (optional) | Duko stays in the upper third |
| T12 | performance-engineer | T8 | Benchmark route scene, budget refinement, ribbon spike check | AC-327/328 |
| T13 | qa-engineer | T4 | Seed screenshot gallery (50 seeds x 4 worlds), the 14.6 clip test script | Gallery reviewed |

Order: T1 -> (T2 || T3) -> T4 -> (T5 || T6 || T7) -> T8 -> T9 -> T10 -> T11/T12/T13. The game stays playable after every task (T1 changes
nothing visible; T3 adds a debug curved route switch `RouteTuning.debugRoute`).

---

## 17. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | The sim's swing pivot travels with the runner (11 to 29 m), so a natural single branch cannot be the pivot. The span plus sliding knot is a visual compromise; scrutinized in slow-mo it looks like a trolley | Hide the knot in leaf tufts, keep the 0.8x hang, clip test in 14.6. If it looks wrong, Q2 option B (change the sim to a true fixed pivot), which needs a new vine spec and re-run of balance |
| R2 | Narrow portrait FOV (14.9 deg half-angle) leaves only a 1.4 deg margin on R = 75 m bends | Tuning knobs, per-world Rmin, validator AC-305; raise Rmin to 90 m if the owner plays it as tight |
| R3 | Motion comfort (banking, yaw, slopes) | Limits in 4.3, Reduce Motion, optional "Calm winding" setting (Q3) |
| R4 | Generator commit-ahead of 175 m may conflict with boost-held vines | AC-313, emergency ease, `PeekChunk` contract |
| R5 | Scenery budget overrun (dense jungle plus curves exposes more of the forest) | Stateless cells, density modifiers, LODs; performance audit T12 |
| R6 | Terrain-shaped skins hide hitbox corners and cause "invisible obstacle" deaths | Fit boxes, AC-324, red band rule, validator F7 |
| R7 | Floating origin breaks particles/trails | Local space particles; AC-310 |
| R8 | Art volume (20 asset groups x 4 worlds) | Build order Jungle, River, Ruins, Mountains; shared kit; quality over schedule rule 10 |
| R9 | The route depends on committed chunk kinds, so Boost can change route shape | Accepted; the route is presentation and reproducible per input trace |
| R10 | Two sources of truth if the old `z` placement remains in any view | Task T3 checks every view; AC-311 |

---

## Open questions for owner

All have a recommended default that is already applied (`[ASSUMED]`).

1. **How wide is the canopy path?**
   a) Wide bough, 7.2 m, interwoven limbs, leaf rim (recommended: keeps three lanes readable, no new rules).
   b) Thin branches (about 2.5 m each) with a lane per limb and gaps between them (more dramatic, but lane switching becomes jumping across voids: a new rule).
   c) Mix: wide boughs, thin limb bridges only as gaps.
2. **How do swings hang?**
   a) Span plus sliding knot: a long limb or creeper with a looped vine, no sim change (recommended, risk R1).
   b) True fixed pivot: change the swing sim so the arc pivots on a single point (needs a new vine spec, balance re-run, and a shorter swing travel; most natural look).
   c) Hybrid: fixed-pivot vines for chasm vines only.
3. **How winding should it be (comfort vs drama)?**
   a) Default winding as specified: R >= 75 m, bank 6 deg (recommended).
   b) Calmer: R >= 120 m, bank 3 deg, plus a Settings option "Calm winding".
   c) Wilder: R >= 55 m, 9 deg bank (needs FOV and camera work; not recommended for portrait).
4. **Do vine sections appear inside the canopy?**
   a) Yes, after the Ascent has finished (recommended: the best clip, ground far below).
   b) No, only on the forest floor.
   c) Only after the first 2,000 m of a run (rare treat).
