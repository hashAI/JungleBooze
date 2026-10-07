# Spec 002: Track, obstacles and coins

**Owner:** game-designer | **Builder:** gameplay-engineer (simulation), gameplay/ui-engineer (views, HUD, Game Over) |
**Milestone:** First Playable (FP1), stage B (simulation) and C (presentation) | **Status:** Ready to build (chunk
layouts are designer-checked by hand; the validator in section 11 is the gate that proves them) |
**Last updated:** 2026-10-07

**Source of truth:** GDD sections 8 (obstacles), 9 (worlds), 11 (difficulty), 13.1 (score), 14.1 (coin income),
`design/STYLE_GUIDE.md` 4.1 and 7 (readability), and spec 001 (movement, ticks, collision model). This spec
refines those numbers for FP1. It does not edit the GDD or spec 001; changes they need are listed in section 16.

**Covers:** the endless track built from modular chunks, the chunk data format, the FP1 chunk library (Jungle skin),
seeded chunk selection by tier, spawn-ahead and despawn, pooling, the shared obstacle kit with final hitboxes,
ground and gaps, telegraph and visibility, coin patterns and pickup, distance score, the fairness rules and the
chunk validator, the FP1 run lifecycle (start, death, Game Over, restart), config assets, events and view state.

**FP1 scope:** Jungle only, tiers 1 to 3 authored. The format, kit and generator are world-agnostic: other worlds
add a `WorldSkin` and (later) world-specific chunks without changing any code or hitbox.

**Out of scope (hooks only):** vine sections (`kind = Vine`), world gateways (`kind = Gateway`), signature hazards
(thorn patch skin, lane strike), power-ups and the Speed Boost, Duko, the tutorial and the "first appearance teaches"
rule (onboarding), continue, Results screen, missions, save. Tier 4 to 6 chunk pools (combos, mixed movers).

---

## 1. Player story and purpose

"The jungle path never ends and never repeats the same way twice. I always see what is coming early enough to
answer it: logs I jump, branches I slide under, trunks I dodge, a boulder that rolls into the next lane. Coins show
me a good line through it. When I die, it was my mistake, and one tap puts me back on a fresh path."

The track is what the player reads every second of a run. It must be:
- **Fair** (pillar 2): every layout the generator can produce is solvable, proven by a validator, not by hope.
- **Readable** (pillar 1): every answer is visible at least 1.2 s ahead, nothing hides behind a trunk.
- **Rewarding** (pillar 4): coins form clean lines and arcs that teach good lines and pay out every run.
- **Fast to restart**: one tap from Game Over to running again in under a second.

---

## 2. Conventions

Everything from spec 001 section 2 applies: 60 Hz ticks, authoring in designer units converted to an immutable
runtime config at run start, axes (`x` lateral, `y` height, `z` distance), lane index 0 = left, lane center
`x = (lane − 1) × laneWidthM` (−2.4, 0, +2.4 m), simulation in plain C# (`JungleBooze.Gameplay.Track`), presentation
read-only. `[ASSUMED]` marks a designer default that stands until the owner says otherwise.

Additional conventions:
- **Chunk space:** positions inside a chunk are chunk-relative: `zc` from 0 (chunk start) to `lengthM` (chunk end).
  World position `z = chunk.startZ + zc`. `startZ` is a `double` (as hero `Z` in spec 001).
- **Authoring grid:** obstacle and coin `zc` values are multiples of **0.5 m**; chunk lengths are multiples of
  **5 m**. The validator rejects off-grid values.
- **Obstacle "front":** the face with the smallest `z` (the face HERO runs into). Positions in this spec are fronts
  unless stated otherwise.
- **Row:** obstacles whose fronts lie within **1.0 m** of each other form one row. A row is one "required action"
  for spacing and density.
- **Band (speed band of a chunk):** the range of speeds at which the chunk can be selected, from the speed curve at
  the start of its lowest allowed tier to the speed at the end of its highest allowed tier (section 7.4).
- **N(l), the neutral state in lane l:** grounded, `Running`, not sliding, no lane move, no queued move, no buffered
  jump, not dazed, `X` = lane-l center, `Y = 0`. Chunk seams are proven safe through N (section 11).
- AC ids in this spec start at **AC-201** so they never clash with spec 001 test names.

---

## 3. Config assets and fields

All assets live in `UnityProject/Assets/_Game/Config/` (chunks in `Config/Chunks/`). Naming follows spec 001:
`<Name>.asset` of type `<Name>ConfigAsset`, converted by `ToConfig()` into an immutable `<Name>Config`. Each has
`OnValidate` range checks and is covered by the config-validation test (AC-201). The config hash used by replays
and sim reports covers every asset in this section, including the chunk library.

### 3.1 `TrackTuning.asset` (`TrackConfigAsset` → `TrackConfig`), simulation

| Field | Unit | Start value | Valid range | Notes |
|---|---|---|---|---|
| `generateAheadM` | m | 150 | 100–250 | The simulation always has chunks generated up to `heroZ + generateAheadM` |
| `despawnBehindM` | m | 15 | 10–40 | An obstacle, coin or chunk is removed once its back end is this far behind hero `Z` |
| `leadInM` | m | 6.0 | 4.0–10.0 | No row front before this `zc` (section 11, F4) |
| `leadOutM` | m | 6.0 | 4.0–10.0 | No row front after `lengthM − leadOutM` (F4) |
| `rowGroupingM` | m | 1.0 | 0.5–2.0 | Fronts within this distance form one row |
| `breatherIntervalMinS` | s | 25 | 10–60 | GDD 11.2 |
| `breatherIntervalMaxS` | s | 35 | ≥ min | GDD 11.2 |
| `breatherMinDurationS` | s | 2.0 | 1.0–4.0 | Breathers are repeated until their total length ≥ this × speed (section 8.4) |
| `noRepeatWindow` | chunks | 3 | 0–6 | A chunk id is not picked again within the last N normal chunks (relaxed if the pool is too small) |
| `mirrorChance` | 0..1 | 0.5 | 0–1 | Chance a chunk is mirrored (lane 0 ↔ lane 2) |
| `maxPickAttempts` | count | 8 | 1–32 | Weighted picks tried against the seam table before the breather fallback (8.3) |
| `maxActiveChunks` | count | 8 | 4–16 | Fixed capacity of the simulation chunk ring |
| `maxActiveObstacles` | count | 64 | 32–256 | Fixed capacity; overflow is an error in dev builds |
| `maxActiveCoins` | count | 256 | 64–1024 | Fixed capacity; overflow is an error in dev builds |
| `startChunk` | ref | `Chunk_Start` | | Always the first chunk of a run |
| `library` | ref | `ChunkLibrary_Main` | | The chunk library for this world sequence |

### 3.2 `ObstacleKit.asset` (`ObstacleKitConfigAsset` → `ObstacleKitConfig`), simulation

One entry per archetype. Hitboxes never differ between worlds (GDD 8.3). Full table and rationale in section 5.

| Field | Unit | Low barrier | High barrier | Full block | Mover |
|---|---|---|---|---|---|
| `boxWidthM` (per lane) | m | 2.04 | 2.04 | 2.04 | 1.90 |
| `boxDepthM` | m | 0.6 | 0.5 | 1.0 | 1.6 |
| `boxBottomM` | m | 0.0 | 1.1 | 0.0 | 0.0 |
| `boxTopM` | m | 0.8 | 3.0 | 3.0 | 1.9 |

Gap and mover fields:

| Field | Unit | Start value | Valid range | Notes |
|---|---|---|---|---|
| `gapLengthsM` | list, m | 3.0, 4.0 | each 2.0–6.0 | Allowed gap lengths (authoring picks one) |
| `gapMinWindowS` | s | 0.25 | 0.15–0.40 | Jump timing window a gap must leave at the band's minimum speed (F6) |
| `gapRunAcrossMarginM` | m | 0.25 | 0.1–0.5 | Extra length so a gap can never be crossed by coyote time alone (F6) |
| `gapLandingClearM` | m | 1.0 | 0.5–3.0 | No obstacle front within this distance after a gap's far edge (F6) |
| `moverLateralSpeedMps` | m/s | 4.8 | 3.0–8.0 | One lane (2.4 m) in 0.50 s; linear motion |
| `moverTriggerLeadS` | s | 1.4 | 1.2–2.5 | The mover starts when HERO's front is `moverTriggerLeadS × currentSpeed` before the mover's front |
| `moverMinSettleS` | s | 0.5 | 0.3–1.0 | The mover must reach its end lane at least this long before HERO can reach it (F8) |
| `moverMaxLaneShift` | lanes | 1 | 1 (FP1) | Movers move exactly one lane in FP1 |

### 3.3 `DifficultyTiers.asset` (`DifficultyTiersConfigAsset` → `DifficultyTiersConfig`), simulation

| Tier | `fromM` | `targetRowsPer100M` | `minActionS` | `vMaxMps` (derived) | `minRowSpacingM` (derived) | Pool in FP1 |
|---|---|---|---|---|---|---|
| 1 | 0 | 4 | 0.90 | 11.20 | 10.08 | Tier-1 weights |
| 2 | 300 | 6 | 0.75 | 12.25 | 9.19 | Tier-2 weights |
| 3 | 600 | 7 | 0.65 | 14.17 | 9.21 | Tier-3 weights |
| 4 | 1,500 | 8 | 0.55 | 16.58 | 9.12 | Copy of tier 3 in FP1 |
| 5 | 3,000 | 9 | 0.50 | 19.00 | 9.50 | Copy of tier 3 in FP1 |
| 6 | 5,000 | 10 | 0.45 | 21.00 | 9.45 | Copy of tier 3 in FP1 |

- `vMaxMps(T)` = `SpeedCurve(fromM of tier T+1)`, and the curve cap (21.0) for tier 6. Computed in `ToConfig()`.
- `minRowSpacingM(T)` = `minActionS(T) × vMaxMps(T)`. Computed in `ToConfig()`.
- Each tier also holds `weights`: a list of (`chunk`, `weight` int ≥ 0). FP1 values are in section 7.3.
- Validation: `fromM` strictly increasing, tier 1 `fromM = 0`, every tier's pool has total weight > 0, and every chunk
  with weight > 0 in tier T has `minTier ≤ T ≤ maxTier`.
- Design note: in metres, the minimum row spacing is almost the same in every tier (9.1 to 10.1 m), because the
  GDD's shrinking "min time" mostly follows the rising speed. Difficulty therefore rises through speed, density and
  pattern type, not through tighter metre spacing. This is intended.

### 3.4 `CoinTuning.asset` (`CoinConfigAsset` → `CoinConfig`), simulation

| Field | Unit | Start value | Valid range | Notes |
|---|---|---|---|---|
| `coinValue` | coins | 1 | 1–10 | GDD 14.1 |
| `pickupRadiusM` | m | 0.6 | 0.3–1.0 | HERO box expanded by this on every axis; coin center inside = collected (10.4) |
| `coinHeightM` | m | 0.75 | 0.5–1.0 | Center height of ground coins; keeps 0.1 m clear under a high barrier |
| `coinVisualRadiusM` | m | 0.25 | | Style guide 7.2 (0.5 m diameter); used by the overlap rule C1 |
| `coinClearanceM` | m | 0.1 | 0.05–0.3 | Min gap between a coin's visual sphere and any hitbox (C1) |
| `lineSpacingM` | m | 2.0 | 1.0–4.0 | Default spacing of `Line` and `Trail` coins |
| `arcCoinCount` | coins | 7 | 5–9, odd | Coins in an `Arc` |
| `arcSpanFraction` | 0..1 | 0.75 | 0.5–0.9 | The arc spans this fraction of the jump length at the arc's speed |
| `trailMinLengthM` | m | 6.0 | 4.0–12.0 | Minimum `z` length of a `Trail` |
| `coinBlockClearS` | s | 0.35 | 0.2–0.6 | Lure rule C3, evaluated at the band's max speed |
| `streakLength` | coins | 25 | 10–100 | GDD 13.1 |

### 3.5 `ScoreTuning.asset` (`ScoreConfigAsset` → `ScoreConfig`), simulation

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `pointsPerMeter` | points/m | 1 | GDD 13.1 |
| `nearMissBonus` | points | 20 | GDD 13.1; uses `NearMiss` from spec 001 |
| `coinStreakBonus` | points | 50 | GDD 13.1, per completed streak of `streakLength` |
| `scoreMultiplier` | × | 1 | Missions raise it later; fixed 1 in FP1 |

### 3.6 `TrackPresentationTuning.asset` (`TrackPresentationConfigAsset`), presentation only

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `viewSpawnAheadM` | m | 95 | Views appear when their front is this far ahead of HERO; beyond fog end (90 m), so nothing pops in |
| `fogStartM` / `fogEndM` | m | 45 / 90 | Style guide; `fogStartM` must equal `RunnerPresentationTuning.fogStartM` (validated) |
| `telegraphMinS` | s | 1.2 | GDD 8.3; checked by AC-237 and rule F7 |
| `grayBoxVisualMarginM` | m | 0.08 | FP1 gray-box meshes = hitbox grown by this on every side except the ground (5.3) |
| `poolPrewarm` | per pool | see 8.6 | Pool sizes |
| `moverMarkerWidthM` | m | 0.5 | Ink ground stripe along the mover path and an end circle (style guide 4.1) |
| `gapFlagLeadS` | s | 1.0 | Style guide 4.1: gap flags readable from 1.0 s; placed at the near edge |

### 3.7 `RunFlowTuning.asset` (`RunFlowConfigAsset` → `RunFlowConfig`), app layer

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `startOnFirstInput` | bool | true | Ready screen waits for the first tap, swipe or key; that input only starts the run (12.1) |
| `gameOverInputLockMs` | ms | 400 | Game Over buttons ignore input for this long after the panel appears [ASSUMED] |
| `restartMaxMs` | ms | 1000 | Budget from Restart press to the first running tick (AC-244) |
| `devFixedSeed` | ulong | 0 | 0 = off. Non-zero forces this seed for every run (dev builds only) |

### 3.8 `WorldSkin_Jungle.asset` (`WorldSkinConfigAsset`), presentation only, FP1 subset

FP1 needs only the per-archetype display name (Game Over cause text) and a gray-box tint. The full `WorldSkin`
(meshes, lighting preset, music) comes with `worlds.md`.

| Archetype | `displayName` (Jungle) |
|---|---|
| Low barrier | Fallen log |
| High barrier | Low branch |
| Full block | Giant tree trunk |
| Mover | Rolling boulder |
| Gap | Ravine |

### 3.9 `ChunkValidatorTuning.asset` (`ChunkValidatorConfigAsset`), editor and tests only

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `quickSpeeds` | rule | band min, each tier's `vMaxMps` inside the band, band max | Used by the EditMode library gate (AC-232) |
| `fullSpeedStepMps` | m/s | 0.25 | Used by the nightly/balance-simulator full sweep (S2) |
| `phaseOffsets` | fractions of a tick's distance | 0, 1/3, 2/3 | Chunk start is not tick-aligned in real runs |
| `maxStatesPerTick` | count | 50,000 | Exceeding it is a validator error, never a pass |
| `positionQuantumM` | m | 0.001 | State dedupe quantum for `X`, `Y` |
| `visibilitySampleOffsetM` | m | 0.7 | F7 sample points at lane center and ± this |
| `visibilityMinPoints` | of 3 | 2 | F7 pass threshold |
| `survivingLaneSampleM` | m | 0.5 | Spacing of the surviving-lane table used for seams |

---

## 4. Chunk data format

### 4.1 `ChunkAsset` → `ChunkData` (one asset per chunk, in `Config/Chunks/`)

| Field | Type | Notes |
|---|---|---|
| `id` | string | Stable, unique, never reused (`T1-01`). Stored in replays and sim reports |
| `formatVersion` | int | 1. Bumped when the format changes; loader migrates or rejects |
| `displayName` | string | Designer label only (debug overlay) |
| `kind` | enum | `Start`, `Normal`, `Breather`; reserved: `Vine`, `Gateway`, `Signature` |
| `lengthM` | float | Multiple of 5 m; 20–80. Normal chunks are 40 m in FP1 |
| `minTier`, `maxTier` | int | 1..6; the chunk may only get weight in tiers inside this range |
| `worldMask` | flags | Worlds the chunk may appear in. FP1 chunks: `All` (layouts are world-neutral, GDD 8.3) |
| `allowMirror` | bool | Default true |
| `obstacles` | list of `ObstaclePlacement` | Sorted by `zc` |
| `coins` | list of `CoinPattern` | Sorted by start `zc` |

`ObstaclePlacement`:

| Field | Type | Notes |
|---|---|---|
| `archetype` | enum `ObstacleArchetype` | `LowBarrier`, `HighBarrier`, `FullBlock`, `Mover`, `Gap` (spec 001 9.1) |
| `laneMask` | 3 bits | Contiguous lanes the obstacle occupies. `FullBlock`: 1 or 2 lanes. `LowBarrier`, `HighBarrier`, `Gap`: 1–3 lanes. `Mover`: exactly 1 (its start lane) |
| `zc` | float | Front (for a gap: the near edge) |
| `gapLengthM` | float | Gap only; must be one of `ObstacleKit.gapLengthsM` |
| `moverToLane` | int | Mover only; the end lane, exactly one lane from the start lane in FP1 |

`CoinPattern`:

| Type | Fields | Coins generated (chunk-relative) |
|---|---|---|
| `Line` | `lane`, `zStart`, `zEnd`, `spacingM` (default `lineSpacingM`) | `x = lane center`, `y = coinHeightM`, `z = zStart + k × spacing` for every `z ≤ zEnd` |
| `Arc` | `lane`, `zCenter` | `arcCoinCount` coins along a jump parabola centred on `zCenter` (10.2) |
| `Trail` | `fromLane`, `toLane`, `zStart`, `zEnd` (length ≥ `trailMinLengthM`) | Coins every `spacingM`; `x = lerp(from, to, smoothstep(u))`, `u = (z − zStart)/(zEnd − zStart)`, `y = coinHeightM` |
| `Single` | `x`, `y`, `zc` | One coin (rare; for hand placement) |

### 4.2 Mirroring

A mirrored chunk maps every lane `l` to `2 − l` (lane masks, `moverToLane`, coin lanes) and every `x` to `−x`.
Nothing else changes. The validator validates both orientations (AC-232).

### 4.3 Chunk library

`ChunkLibrary_Main.asset` (`ChunkLibraryConfigAsset` → `ChunkLibrary`) holds the ordered list of chunk assets.
**List order is part of determinism** (weighted selection walks it in order); appending is safe, reordering changes
seeds and needs the replay format version bumped. FP1: one library, used with the Jungle skin. Other worlds reuse it
and add world-specific chunks with a `worldMask` later.

### 4.4 Runtime instance (simulation)

When a chunk is generated, the simulation copies it into fixed-capacity arrays (no allocation):
- `ChunkInstance { int chunkIndex; bool mirrored; double startZ; byte tier; int firstObstacle; int firstCoin; }`
  in a ring of `maxActiveChunks`.
- `ObstacleInstance { int id; ObstacleArchetype archetype; byte laneMask; double z; float gapLength; byte fromLane;
  byte toLane; MoverPhase phase; float moverX; float moverXPrev; }` in a FIFO ring ordered by `z`.
- `CoinInstance { int id; float x; float y; double z; bool collected; }` in a FIFO ring ordered by `z`.
- Ids are per-run counters starting at 1 (obstacles and coins counted separately). `ObstacleId` in spec 001 events
  is this id.

---

## 5. The shared obstacle kit

Five archetypes, one hitbox set, every world (GDD 8.1, 8.3). Shape language: **low = jump, high = slide,
tall = change lane** (style guide 4.1).

### 5.1 Hitboxes (final; replaces spec 001 9.2 reference boxes with the same numbers plus the mover)

An obstacle has one box **per occupied lane**, each centered on its lane center, all sharing the obstacle's id.
HERO box (spec 001): 0.7 m wide, 0.5 m deep, 1.8 m tall standing, 0.8 m sliding.

| Archetype | Player's answer | Box width per lane | Depth (z) | Bottom → top (y) | Why these numbers |
|---|---|---|---|---|---|
| **Low barrier** (jump-over) | Jump, or change lane | 2.04 m | 0.6 m | 0 → 0.8 m | Jump clears 0.8 m for 0.41 s; leaves ≥ 18 ticks of timing slack at 10 m/s (spec 001 S2 ≥ 15) |
| **High barrier** (slide-under) | Slide, or change lane | 2.04 m | 0.5 m | 1.1 → 3.0 m | Sliding HERO (0.8 m) has 0.3 m headroom; standing (1.8 m) hits; coins at 0.75 m fit under |
| **Full block** (lane-block) | Change lane | 2.04 m | 1.0 m | 0 → 3.0 m | Taller than any jump (HERO bottom peaks at 1.5 m) |
| **Mover** (moving) | Change lane, read where it ends | 1.90 m | 1.6 m | 0 → 1.9 m | Boulder about 2.2 m visual; cannot be jumped (top 1.9 > 1.5) or slid under |
| **Gap** | Jump (or change lane if not all lanes) | no box | length 3.0 or 4.0 m | ground missing | See 5.4 |

- Lane width 2.4 m: a 2.04 m box leaves 0.18 m to each lane boundary, and 0.36 m between boxes in adjacent lanes
  (narrower than HERO, so no one can squeeze between lanes).
- A two-lane full block (GDD tier 2 "two-lane block"; later skinned as the Jungle thorn patch) is one `FullBlock`
  placement with a 2-lane mask. Three-lane full blocks are forbidden (F5).

### 5.2 Collision categories (spec 001 9.3 and 9.4 apply unchanged)

| Contact | Low barrier | High barrier | Full block | Mover |
|---|---|---|---|---|
| Front (z) | Lethal | Lethal (running in standing, or airborne at head height) | Lethal | Lethal |
| From below (y) | n/a | Lethal (jumping up into it, e.g. from a slide) | n/a | n/a |
| From above (y) | **Stumble** (scramble over) | n/a | n/a (cannot be reached) | n/a |
| Side (x) | Stumble | Stumble | Stumble | Stumble (also when the mover pushes into HERO from the side) |
| Exact tie | Stumble | Stumble | Stumble | Stumble |

- **Second stumble within the daze window is lethal** ("Tripped twice", spec 001 9.4.5).
- After a stumble, every box of that obstacle id is ignored for the rest of HERO's pass (spec 001 9.4.1).
- **Moving boxes:** the entry axis is found from **relative** motion during the tick: HERO's `z` motion and `x` motion
  against the mover's `x` motion (`moverXPrev → moverX`). A mover that slides into a standing HERO enters on `x`,
  so it is a stumble, never an unreadable death. (Spec 001 9.1 must allow this; section 16.)
- Edge forgiveness (spec 001 9.5) applies per box.
- Gaps kill with cause `Fell` (spec 001 6.5); invulnerability never saves from a gap (spec 001 9.6).

### 5.3 Visual size against hitbox (presentation)

A pass that looks safe must be safe, and a hit must look like a hit. FP1 gray-box meshes are each hitbox grown by
`grayBoxVisualMarginM` (0.08 m) on every side except the ground side, so the visual is never smaller than the hitbox
and stays within the style guide's 10 cm. Final art follows the same rule (see section 16 about the wording in GDD
5.2 and style guide 7.4). Telegraph markings follow style guide 4.1: low barrier red band on the top edge, high
barrier red-and-ink stripes on the underside, full block red band at about 1 m, mover red band plus ink path stripe,
gap red-and-ink flags at the near edge.

### 5.4 Gaps

- A gap removes the ground in its lanes for `z` in `[nearEdge, nearEdge + gapLengthM)`.
- The two lengths in FP1 are **3.0 m** (any tier) and **4.0 m** (tier 2+ only, because of F6). Bounds (F6):
  - Max (jumpable with a fair window at the band's slowest speed `vMin`):
    `gapLengthM ≤ 0.6·vMin + playerHitboxDepthM − gapMinWindowS·vMin` = `0.35·vMin + 0.5`
    → 4.0 m at 10 m/s, 4.42 m at 11.2 m/s.
  - Min (cannot be crossed without jumping, thanks to coyote time, at the band's fastest speed `vMax`):
    `gapLengthM ≥ (coyoteTicks + 1)·vMax/60 + playerHitboxDepthM + gapRunAcrossMarginM` → 2.85 m at 21 m/s.
- At 10 m/s a 4.0 m gap leaves a 15-tick takeoff window (plus 5 coyote ticks); a 3.0 m gap leaves 21 ticks.

### 5.5 Mover behavior (FP1: Jungle "rolling boulder")

1. A mover sits in its start lane at its `z` (it is on screen and outlined from spawn; an ink path stripe on the
   ground runs from its start lane to its end lane, ending in an ink circle where it will stop).
2. **Trigger:** on the first tick where `moverFrontZ − heroFrontZ ≤ moverTriggerLeadS × currentSpeed` (1.4 s),
   the mover starts moving: emit `MoverStarted(id, fromLane, toLane)`.
3. **Motion:** `moverX` moves linearly toward the end lane center at `moverLateralSpeedMps` (0.08 m per tick); one
   lane takes 30 ticks (0.50 s). It never moves in `z`.
4. **Settle:** on reaching the end lane center it stops for good: emit `MoverSettled(id)`. With the start values it
   settles 0.9 s before HERO can reach it (rule F8 requires ≥ 0.5 s).
5. It collides like a full block at all times (its box moves with it), with the side rule in 5.2.
6. Movers update only from HERO's `z` and speed, so they are deterministic and the validator can treat their
   position as a function of the tick.
7. **Pause:** the mover continues from the same tick state on resume (it is simulation).

---

## 6. Ground and ledges (`ITrackQuery`)

Spec 001 6.5 defines `ITrackQuery.HasGround(x, zMin, zMax)`. The track implements it as `ChunkTrackQuery` and adds
the obstacle query the collision step needs. No method allocates.

| Member | Contract |
|---|---|
| `bool HasGround(float x, double zMin, double zMax)` | True if any part of the footprint `[x − 0.35, x + 0.35] × [zMin, zMax]` lies over ground. Ground exists everywhere on the 3 lane bands (`|x| ≤ 3.6 m`) for `z ≥ startZ` of the start chunk, except inside gaps (per lane band, `[nearEdge, nearEdge + length)`). Partial support counts as ground (generous). The half-width comes from `RunnerConfig.playerHitboxWidthM / 2` |
| `int GetBoxes(double zMin, double zMax, Span<ObstacleBox> buffer)` | Writes every obstacle box whose z range overlaps `[zMin, zMax]`, in id order, and returns the count. `ObstacleBox { int id; ObstacleArchetype archetype; byte lane; float xMin, xMax, xMinPrev, xMaxPrev, yMin, yMax; double zMin, zMax; }` (`Prev` values differ only for movers) |
| `bool TryGetNextGapEdge(int lane, double fromZ, out double nearEdge, out float length)` | For bots and debug overlays; not used by movement |

Notes for movement (spec 001 6.5): leaving a ledge without jumping gives coyote time (5 ticks); landing from a jump
needs `HasGround` on the landing tick; a lane move from a grounded lane into a lane over a gap leaves the ground as
soon as the footprint has no ground (coyote applies); once `Y < 0` there is no recovery.

---

## 7. Chunk library for FP1

16 chunks: 1 start, 13 normal (5 tier-1, 4 tier-2, 4 tier-3) and 2 breathers. Names refer to the Jungle skin; the
layouts are world-neutral (`worldMask = All`). All normal chunks are 40 m and allowed up to tier 6, so every one of
them is validated up to the 21 m/s cap. Tier 1 has 5 chunks so the opening minutes have more variety.

### 7.1 Notation

- Obstacles: `Low[lanes]@zc`, `High[lanes]@zc`, `Full[lanes]@zc`, `Mover[from→to]@zc`, `Gap[lanes]@zc len L`.
- Coins: `Line(lane, z0→z1)` (2 m spacing, both ends included), `Arc(lane, zCenter)` (7 coins),
  `Trail(a→b, z0→z1)` (2 m spacing).
- "Best path" is the designer's estimate of the most coins one path can collect; the validator computes the exact
  value (11.4) and section 15 sets the target bands.
- Arc centers: low barrier `front + 0.3`; gap `nearEdge + length/2`.

### 7.2 Chunks

| Id | Name | Kind | Tiers | Obstacles (rows) | Coins | Rows | Best path |
|---|---|---|---|---|---|---|---|
| `S-01` | Trailhead | Start, 50 m | n/a | none | `Line(1, 20→48)` | 0 | 15 |
| `T1-01` | Log Step | Normal | 1–6 | `Low[1]@16` | `Line(0, 6→20)`, `Line(2, 26→36)` | 1 | 14 |
| `T1-02` | Low Branch | Normal | 1–6 | `High[1]@16` | `Line(2, 4→16)`, `Line(0, 22→34)` | 1 | 14 |
| `T1-03` | Trunk Gate | Normal | 1–6 | `Full[0]@8`; `Full[2]@22` | `Line(1, 4→32)` | 2 | 15 |
| `T1-04` | Log, then Branch | Normal | 1–6 | `Low[2]@8`; `High[0]@22` | `Line(1, 4→18)`, `Line(2, 24→34)` | 2 | 14 |
| `T1-05` | Short Ravine | Normal | 1–6 | `Gap[0,1,2]@16 len 3.0` | `Line(1, 4→12)`, `Line(1, 22→36)` | 1 | 13 |
| `T2-01` | Thorn Wall | Normal | 2–6 | `Full[0,1]@8`; `Low[2]@20`; `High[1,2]@30` | `Line(2, 2→14)`, `Arc(2, 20.3)` | 3 | 14 |
| `T2-02` | Switchback | Normal | 2–6 | `Full[1,2]@6`; `Full[0,1]@18`; `Low[0,1,2]@30` | `Trail(0→2, 10→18)`, `Arc(2, 30.3)` | 3 | 12 |
| `T2-03` | Ravine Run | Normal | 2–6 | `Gap[0,1,2]@8 len 4.0`; `High[0,1]@22`; `Low[2]@32` | `Arc(1, 10.0)`, `Line(2, 16→26)` | 3 | 13 |
| `T2-04` | Branch Tunnel | Normal | 2–6 | `High[0,1,2]@8`; `Full[1]@18`; `High[0,2]@28` | `Line(1, 2→10)`, `Line(0, 16→30)` | 3 | 13 |
| `T3-01` | Boulder Roll | Normal | 3–6 | `Mover[1→0]@10`; `Low[1,2]@21`; `High[0]@31` | `Line(1, 2→6)`, `Line(1, 13→17)`, `Arc(1, 21.3)` | 3 | 13 |
| `T3-02` | Boulder Cross | Normal | 3–6 | `Full[0]@6`; `Mover[2→1]@16`; `Low[0,1]@26` | `Line(2, 2→12)`, `Line(2, 20→32)` | 3 | 13 |
| `T3-03` | Pinch | Normal | 3–6 | `Full[0]@8` + `Full[2]@8`; `Mover[1→2]@19`; `Low[0,1]@29` | `Line(1, 4→14)`, `Arc(1, 29.3)` | 3 | 13 |
| `T3-04` | Ravine Boulder | Normal | 3–6 | `Gap[0,1]@6 len 3.0`; `Mover[2→1]@17`; `High[0,1,2]@28` | `Arc(0, 7.5)`, `Line(0, 16→28)` | 3 | 14 |
| `B-01` | Coin Straight | Breather, 30 m | 1–6 | none | `Line(1, 2→28)` | 0 | 14 |
| `B-02` | Coin Weave | Breather, 30 m | 1–6 | none | `Trail(1→0, 2→10)`, `Trail(0→2, 12→22)`, `Trail(2→1, 24→30)` | 0 | 15 |

What each tier teaches (GDD 11.2): tier 1 = single obstacles, coins only in free lanes; tier 2 = two-lane blocks,
coins over and under obstacles (arcs, lines under branches), full-width rows; tier 3 = movers ("trust the ground
marker": coins wait in the lane the boulder leaves). Combos (jump then slide in one lane) are tier 4 content and are
not in FP1.

Design intent per chunk is a one-line comment field in each asset (for example `T3-01`: "the boulder leaves the
middle lane; coins reward staying").

### 7.3 Tier weights (in `DifficultyTiers.asset`)

| Chunk | Tier 1 | Tier 2 | Tier 3 (and 4–6 in FP1) |
|---|---|---|---|
| `T1-01`, `T1-02`, `T1-05` (1 row) | 2 | 1 | 1 |
| `T1-03`, `T1-04` (2 rows) | 3 | 1 | 1 |
| `T2-01` … `T2-04` | 0 | 3 | 2 |
| `T3-01` … `T3-04` | 0 | 0 | 4 |

Expected density over normal chunks (rows per 100 m): tier 1 = 3.75 (target 4), tier 2 = 6.3 (target 6), tier 3 =
6.8 (target 7). Breathers and the start chunk are not counted. Tiers 4 to 6 in FP1 reuse the tier-3 pool at their
higher speeds, so their density (6.8) is below the GDD targets (8 to 10) until tier 4+ chunks are authored (16.3).

### 7.4 Speed bands

| Chunk tiers | Band min | Band max | Quick validation speeds (m/s) |
|---|---|---|---|
| 1–6 | 10.00 | 21.00 | 10.00, 11.20, 12.25, 14.17, 16.58, 19.00, 21.00 |
| 2–6 | 11.20 | 21.00 | 11.20, 12.25, 14.17, 16.58, 19.00, 21.00 |
| 3–6 | 12.25 | 21.00 | 12.25, 14.17, 16.58, 19.00, 21.00 |
| Breathers, start | n/a (no obstacles) | | coin reachability only, at 10.00 and 21.00 |

The tutorial speed (8 m/s) and the Speed Boost (33.6 m/s, invulnerable) are not in any band in FP1; onboarding and
power-ups validate their own chunks.

---

## 8. Generator

Plain C# `TrackGenerator` in `JungleBooze.Gameplay.Track`. Inputs: `TrackConfig`, `DifficultyTiersConfig`,
`ChunkLibrary`, `ObstacleKitConfig`, `CoinConfig`, `SpeedCurve`, the precomputed seam table (8.5), and one
`IRandom` stream. No allocation after run setup.

### 8.1 Random stream

The run's root `IRandom` (seeded with the run seed) is forked once at run setup with
`RandomStreamIds.TrackGeneration`. The generator is the only user of that stream. Proposed constants for the shared
stream-id file (ARCHITECTURE 5.1; ids are never renumbered): `TrackGeneration = 1`, `ObstacleVariants = 2`,
`Pickups = 3`, `Bot = 4`, `Cosmetic = 5` [ASSUMED; tech-architect owns the file].

### 8.2 When it runs

In simulation step 7a (section 13.3), after HERO's `z` has advanced: while the end of the last generated chunk is
`< heroZ + generateAheadM`, generate the next chunk. At run setup the generator fills the first 150 m before tick 0
(the start chunk plus the following chunks), so tick 0 never generates more than the steady state.

### 8.3 Choosing the next chunk (deterministic)

```
startZ   = end of the previous chunk (0 for the start chunk)
tier     = highest tier with fromM ≤ startZ
if breather is due (8.4): emit breather chunks, then continue
pool     = chunks with weight(tier) > 0, excluding ids among the last noRepeatWindow normal chunks
           (if that leaves an empty pool, ignore the no-repeat rule)
repeat up to maxPickAttempts (8) times:
    r      = rng.NextInt(0, totalWeight(pool))        // draw 1
    chunk  = walk pool in library order, subtracting weights, until r < weight
    mirror = rng.NextFloat() < mirrorChance && chunk.allowMirror   // draw 2, always drawn
    if seamTable.IsCompatible(prev, prevMirror, chunk, mirror): accept and stop
if nothing was accepted: emit breather B-01 (not mirrored) and record "seam fallback" (sim counter)
```

- Exactly 2 draws per attempt, always, so a change in one chunk's `allowMirror` does not shift other runs'
  sequences more than necessary.
- The generator stores the tier on each `ChunkInstance`; `TierChanged(tier)` is emitted when HERO enters the first
  chunk of a new tier (13.1), not at generation time.
- **No spike rule (GDD 11.2):** tiers are at least 300 m apart, so density never rises by more than one tier step
  within 200 m. The config validation checks this for `fromM` (AC-201).

### 8.4 Breathers

- At run setup and after every breather, draw the next interval: `breatherDueS = rng.NextFloat(25, 35)` (one draw).
- Count the **planned** run time of each generated normal chunk: `lengthM / SpeedCurve(startZ)` (deterministic,
  independent of the player). When the sum reaches `breatherDueS`, the next generated chunk is a breather.
- Breather selection: one draw `rng.NextInt(0, breatherCount)` plus the mirror draw. Breathers are repeated until
  their total length ≥ `breatherMinDurationS × SpeedCurve(startZ)`: one 30 m breather up to 15 m/s, two above.
- The start chunk does not count toward the interval.

### 8.5 Seam table (precomputed)

A table `IsCompatible(prev, prevMirror, next, nextMirror)` for every ordered pair of chunks in the library, computed
by the chunk validator in the editor and stored in the library asset (with a hash of the chunk data it was built
from; a stale table fails AC-201). A pair is compatible when the seam visibility rule (F7) holds across the seam.
Solvability across seams needs no table: it follows from F1 and F2 (section 11.2). Breathers and the start chunk
are compatible with everything.

### 8.6 Despawn and pooling

- **Simulation:** each step (7a), obstacles, coins and chunks whose back end is more than `despawnBehindM` (15 m)
  behind hero `Z` are removed from the front of their FIFO rings. Rings have fixed capacity; nothing is allocated.
  Collected coins stay in the ring (flagged) until they despawn, so ids stay stable for views.
- **Presentation:** a `TrackView` reads the simulation rings each frame. An obstacle or coin view is taken from its
  pool when its front comes within `viewSpawnAheadM` (95 m) of HERO, and returned when the simulation removes it
  (or, for coins, plays the pickup VFX and returns on `CoinCollected`). Views never `Instantiate` or `Destroy` during
  a run (ARCHITECTURE 10.3); pools are pre-warmed at scene load.

| Pool | Prewarm | Reason |
|---|---|---|
| Ground segment (one lane, 5 m) | 80 | (95 + 15) m / 5 m × 3 lanes = 66, plus margin |
| Low barrier box | 12 | ≤ 3 boxes per row × 1 row per 10 m in range |
| High barrier box | 12 | same |
| Full block box | 12 | same |
| Mover (boulder + path marker) | 4 | ≤ 1 per chunk, 3 chunks in range |
| Gap flags | 6 | |
| Coin | 120 | ≤ 15 per 40 m × 110 m in range, plus margin |
| Coin pickup VFX | 16 | |

A pool that has to grow logs a warning in development builds and fails AC-222.

---

## 9. Telegraph and visibility

Pillar 1 is the highest pillar: the answer to every obstacle must be visible at least **1.2 s** before HERO reaches
it, at the current speed.

1. **Spawned and unfogged:** views exist from 95 m (beyond the 90 m fog end). At 1.2 s an obstacle is at most
   1.2 × 21 = 25.2 m away (40.3 m at a 33.6 m/s boost), always inside the fog start (45 m), so it is fully
   readable, not half-fogged. Checked by AC-236 and AC-237.
2. **Not hidden behind another obstacle:** checked by the validator's visibility rule F7 (11.1). Tall obstacles
   (full blocks 3.0 m, movers 1.9 m) seen from a camera 3.2 m high can hide what is behind them in the same lane.
3. **Mover end lane:** the ink path stripe and end circle are on the ground from spawn, so the end lane is readable
   long before 1.2 s; the boulder starts moving at 1.4 s and settles ≥ 0.5 s before HERO can reach it.
4. **Gaps:** the void and the red-and-ink flags at the near edge are visible from spawn; flags are sized to read at
   1.0 s (style guide 4.1). The gap's answer (jump) is readable at 1.2 s from the void itself.
5. **Shape first:** the low/high/tall shapes carry the answer; color is never the only cue (GDD 20).

---

## 10. Coins and score

### 10.1 Coin heights and placement

- Ground coins: center at `coinHeightM` = 0.75 m. A running or sliding HERO collects them (see 10.4), so a slide
  under a branch never costs coins. During a jump they are collected while HERO's feet are at most 1.35 m high, so a
  full jump over a 2 m-spaced line skips about one coin near the apex (0.19 s above 1.35 m).
- Coins never touch an obstacle (rule C1): coin sphere (radius 0.25 m) + 0.1 m clearance must not overlap any
  hitbox, including a mover's start and end positions.
- Coin patterns are always clean `Line`, `Arc` or `Trail` shapes (style guide 7.2: never random clouds).

### 10.2 Arcs over jumps

An `Arc` shows the jump. Its shape follows HERO's real jump at the speed HERO will have there:
- `v = SpeedCurve(world z of zCenter)` (validator: the constant validation speed). `jumpLength = 0.6 s × v`.
- Coin `k` of `n` (`n = 7`, `k = 0..n−1`): `d = (k − (n−1)/2) × arcSpanFraction × jumpLength / (n − 1)`,
  `z = zCenter + d`, `y = coinHeightM + Yjump(d)`, where `Yjump(d) = 1.5 − ½·g·(d/v)²` (apex 1.5 m above
  `zCenter`, `g` from `RunnerConfig`).
- At 10 m/s the arc spans 4.5 m and its outer coins sit 1.41 m high (the middle one 2.25 m), so they are collected
  by a jump timed anywhere in the middle of the window, and a well-timed jump collects all 7.

### 10.3 Lane-change trails

A `Trail` shows a lane change: coins every 2 m with `x` following a smoothstep between the two lane centers. Trail
coins between lanes are collected while HERO is moving (the expanded pickup box is 1.9 m wide).

### 10.4 Pickup

- A coin is collected on the first tick its center lies inside HERO's box expanded by `pickupRadiusM` (0.6 m) on
  every axis: `x` within `X ± 0.95`, `y` within `[Y − 0.6, Y + height + 0.6]`, `z` within the swept range of HERO's
  front and back faces this tick ± 0.6 (swept, so no coin is skipped at any speed).
- Processed in step 11a, **after** collisions. On a tick where HERO dies, no coins are collected [ASSUMED].
- Each collected coin: `coins += coinValue`, emit `CoinCollected(coinId, lane, value)`.
- Magnet (week 3) will widen the pickup through the same method; nothing else changes.

### 10.5 Coin streak

- `streak` counts consecutive collected coins. A coin is **missed** when HERO's back face passes `coin.z + 0.6` while
  the coin is uncollected and `|coin.x − X| ≤ 1.2 m` (the coin was in HERO's lane; 1.2 m is half the lane width, derived, and 0.6 m is
  `pickupRadiusM`). A miss or a `Stumbled` resets
  the streak to 0.
- When `streak` reaches `streakLength` (25): add `coinStreakBonus` (50) to the bonus score, emit
  `CoinStreak(count = 25)`, and reset `streak` to 0.

### 10.6 Distance and score

- `distanceM = heroZ` (the run starts at `z = 0`). HUD shows `floor(distanceM)`.
- `score = (floor(distanceM × pointsPerMeter) + bonusScore) × scoreMultiplier`, where `bonusScore` collects
  `nearMissBonus` per `NearMiss` and `coinStreakBonus` per streak. FP1 shows distance and coins on the HUD and all
  three (distance, coins, score) on Game Over.
- Run totals are integers (`int` coins, `long` score).

---

## 11. Fairness rules and the chunk validator

### 11.1 Rules every chunk must satisfy

"Validated at its band" means: at every quick validation speed of its band (7.4), with every `phaseOffset`, from
every entry lane, in both mirror orientations.

| # | Rule | How it is checked |
|---|---|---|
| **F1** | **Solvable:** from N(l) at the chunk start, for each lane l, at least one input sequence reaches the chunk end with no death and **no stumble**, validated at its band. | Validator search (11.3) |
| **F2** | **Exit flexibility:** from N(l), for each lane l and each lane l', some no-stumble path is in N(l') on the first tick HERO's center reaches the chunk end. | Validator search |
| **F3** | **Row spacing:** consecutive row fronts are ≥ `minRowSpacingM` of every tier in the chunk's tier range apart (tier 1 chunks: 10.08 m; tier 2+ chunks: 9.50 m). | Static |
| **F4** | **Lead zones:** first row front ≥ `leadInM` (6 m); last row front ≤ `lengthM − leadOutM`; every box and gap lies inside `[0, lengthM]`. Across a seam this gives ≥ 12 m between rows, more than any tier's spacing. | Static |
| **F5** | **A lane is always open:** at every `z`, at least one lane has no `FullBlock` box and no mover (start or end position). Full blocks span at most 2 lanes. | Static |
| **F6** | **Gap bounds:** `(coyoteTicks + 1)·vMax/60 + 0.5 + gapRunAcrossMarginM ≤ gapLengthM ≤ 0.35·vMin + 0.5` over the band (5.4). Nothing but coins (arcs) above a gap; no obstacle within 1.0 m after a gap's far edge. | Static |
| **F7** | **Visible answer (readability):** at the moment HERO's front is `telegraphMinS × v` before an obstacle's front, the obstacle's answer feature is not hidden behind another obstacle, as seen from the camera of every lane HERO can be in on a surviving path at that moment. Method below. | Validator geometry, within the chunk and across seams (seam table) |
| **F8** | **Movers:** exactly one lane of movement; end lane open at the mover's row in at least one other lane (F5); with `moverTriggerLeadS` and `moverLateralSpeedMps`, the mover settles ≥ `moverMinSettleS` before HERO can reach it at `vMax`: `moverTriggerLeadS − laneWidth/moverLateralSpeed ≥ moverMinSettleS` (1.4 − 0.5 = 0.9 ≥ 0.5). | Static |
| **F9** | **No ceiling trap (GDD 5.3.7):** a high barrier is never placed where a jump is the only escape. Covered by F1, because the validator uses the real collision rules (jumping into a barrier from below is lethal); fixture AC-230 proves it. | Validator search |
| **C1** | Coins never overlap a hitbox (coin radius + `coinClearanceM`), including mover start and end positions. | Static |
| **C2** | **No unreachable coins:** every coin is collected by at least one path that also satisfies F1 and F2. | Validator search |
| **C3** | **No lures:** the last coin of a `Line`, and the end of a `Trail`, in lane L lies ≥ `coinBlockClearS × vMax` (7.35 m at 21 m/s) before the front of the next full block or mover end position in lane L. | Static |
| **C4** | Coins above a gap only as an `Arc`. | Static |
| **G1** | Format: grid alignment (0.5 m, 5 m lengths), sorted lists, contiguous lane masks, kind rules (start and breathers have no obstacles), allowed gap lengths, `formatVersion`. | Static |

**F7 method.** Camera of a grounded HERO in lane l, settled (spec 001 section 10): position
`(0.7 × laneCenter(l), 3.2, heroZ − 6.0)`. Answer feature sample points on the obstacle's visual box (hitbox +
0.08 m), per occupied lane box, at `x = center − 0.7, center, center + 0.7`:
low barrier = top front edge; high barrier = bottom front edge; full block = front face at `y = 1.0`
(the red band); mover = end circle on the ground in its end lane and the boulder's front at `y = 1.0`; gap = near
edge on the ground. Occluders: visual boxes of every other obstacle, movers at their position on that tick. The
obstacle passes if, for the lane box nearest to the camera's lane, at least 2 of 3 points have a clear line of sight.
Lanes checked: every lane in which a surviving state (11.3) is settled at that tick. When the check point lies in
the previous chunk (seams), the previous chunk's surviving-lane table is used; the seam table (8.5) stores the
result per ordered pair and mirror combination.

### 11.2 Why seams are safe

1. The start chunk has no obstacles and is validated for F2 at the run-start speeds (5 to 10 m/s), so HERO can be in
   N(l) for any l when the first generated chunk begins.
2. If HERO is in N(l) at the start of a chunk, F1 says the chunk can be survived, and F2 says HERO can be in N(l')
   for any l' at its end, which is the start of the next chunk.
3. So for every sequence the generator can produce, a perfect player can survive forever (no impossible seams),
   without validating pairs. F7 is the only seam-dependent rule, and the seam table covers it.
4. Honest limits: the validator checks a grid of constant speeds and three tick phases; real runs have continuous
   speeds (rising at most 0.16 m/s within a 40 m chunk) and continuous phases. The full sweep (S-202, 0.25 m/s
   steps) and the oracle bot over 100,000 generated chunks at real speeds (S-201) cover the space between grid
   points. A failure in either is a bug in a chunk, never "acceptable".

### 11.3 Validator search (F1, F2, C2, F7)

`ChunkValidator` lives in `JungleBooze.Gameplay.Track` (plain C#), driven by an EditMode test and an editor menu
item `JungleBooze/Validate Chunk Library`, which writes a report and rebuilds the seam table.

1. **Setup** per (chunk, mirror, speed `v`, phase `φ`, entry lane l): a production `RunnerSimulation` with the
   start-value `RunnerConfig`, a constant speed `v` (ramp off, through a speed-source hook; section 16), and a
   `ChunkTrackQuery` holding only this chunk at `startZ = 0`, with flat ground before and after it. HERO center
   starts at `z = −φ × v/60` in N(l).
2. **Layered search:** layer `t` is the set of distinct states on tick `t`. For each state and each command in
   `{none, MoveLeft, MoveRight, Jump, Slide}`, copy the state, step once, and discard the copy if it emitted `Died` or
   `Stumbled`. Insert survivors into layer `t + 1`, deduplicated by a key of everything that affects the future:
   `TargetLane`, lane-move elapsed ticks, start `X`, queued direction, locomotion state, jump tick, fast-fall speed,
   slide ticks left, slide-on-landing flag, coyote counter, `X` and `Y` (quantized to 1 mm). `z` is the same for all
   states in a layer. On a key collision the first inserted state wins, in command order (none, left, right, jump,
   slide), so results are deterministic. Parent links are kept for failure traces.
3. **End:** the search stops on the first tick HERO's center reaches `lengthM`. F1 passes if the last layer is
   non-empty; F2 passes if it contains N(l') for every l'.
4. **Surviving states:** a backward pass marks every state that can reach an N exit state. Only these are used for
   C2 and F7 and for the surviving-lane table (sampled every 0.5 m, stored for the seam table).
5. **Coins:** a coin is reachable (C2) if some surviving transition collects it (pickup test of 10.4). Best-path
   coins = maximum over surviving paths (dynamic programming over the layers; a coin counts on the tick a path
   first touches it). Re-touching the same coin after leaving it is ignored; it needs a lane reversal within 1 m of
   the coin, so the error is negligible for a designer metric.
6. **Limits:** more than `maxStatesPerTick` states in a layer is a validator error (reported, never a pass).
7. **Report:** per chunk and case: pass or fail per rule, the failing rule id, a shortest failure trace (ticks and
   commands), unreachable coin ids, best-path coins and coins per second, and the seam table.

The generator never solves anything at runtime; it relies on this offline proof plus the seam table (GDD 11.2's
"never picks a chunk that fails the solver" is met by never shipping one).

---

## 12. Run lifecycle (FP1)

### 12.1 States

| State | Entered when | What happens | Leaves when |
|---|---|---|---|
| `Ready` | App start, after scene build | Run is set up with a seed: track generated 150 m ahead, HERO idle at `z = 0` in lane 1, HUD shows 0 m and 0 coins, prompt "Swipe or press a key to run" | First tap, swipe or key. That input only starts the run; it is not passed to the simulation as a command [ASSUMED] |
| `Running` | From `Ready` or a restart | Simulation steps (spec 001 run driver); HUD reads the snapshot. Pause works as in spec 001 section 8 | `Died` event |
| `Dying` | `Died` | Simulation stops stepping after the death tick (state frozen). Hit-pause 350 ms, then the camera holds on the cause 800 ms (spec 001 10.6). All input ignored; pause button hidden | 1,150 ms of real time |
| `GameOver` | End of `Dying` | Panel: cause text (12.4), distance, coins, score, session best distance, seed (dev builds only). Buttons: **Run again** (primary; Space/Enter) and **Same track** [ASSUMED]. Buttons ignore input for `gameOverInputLockMs` (400 ms) | A button |

App sent to background during `Dying`: on return, show `GameOver` directly. There is no pause in `Ready` or
`GameOver`.

### 12.2 Restart

- **Run again** uses a new seed: the App takes it from a non-simulation seed source (for example the system clock
  mixed with a counter through a hash; allowed in App, never in simulation). `devFixedSeed ≠ 0` overrides it.
- **Same track** reuses the last seed: same chunks, same mirrors, same coins.
- Restart goes straight to `Running` (the button press is the "tap to run"), with no scene reload: the run scope is
  disposed and rebuilt in place, and all views go back to their pools. Budget: ≤ 1,000 ms from press to the first
  running tick (GDD 19 allows 2 s).

### 12.3 What resets and what stays

| Resets on every restart | Stays for the session (memory only; no save in FP1) |
|---|---|
| Tick counter and time-source accumulator; runner state (spec 001); root `IRandom` re-seeded and all forks; generator state (tier, breather timer, no-repeat history, seam state); chunk, obstacle and coin rings; obstacle and coin ids (back to 1); distance, coins, score, bonus score, streak; event ring buffer; input queue and gesture recognizer; replay recording (new recording with the new seed); all views returned to pools; camera snapped to the start pose (no smoothing from the death pose); HUD values | Session best distance and best score; last seed (for Same track); settings |

### 12.4 Cause text (Game Over)

Uses `WorldSkin.displayName` of the archetype (3.8): `Hit` → "Hit: Giant tree trunk"; `Hit` with
`afterStumble` → "Tripped twice: Fallen log"; `Fell` → "Fell into a ravine" [ASSUMED wording]. The run summary also
records the chunk id and distance of the death (for sim reports and later analytics).

---

## 13. Events and view state

### 13.1 New events (appended to spec 001's `RunnerEventType`; append-only)

| Event | Payload (fields of `RunnerEvent`) | Typical use |
|---|---|---|
| `ChunkEntered` | `Value` = chunk library index, `Flags` = mirrored, kind | Debug overlay; music later |
| `TierChanged` | `Value` = tier | Debug overlay; music intensity later |
| `MoverStarted` | `ObstacleId`, `Lane` = from, `Value` = to | Rumble SFX, roll animation, haptic later |
| `MoverSettled` | `ObstacleId`, `Lane` = end lane | Thud, dust |
| `CoinCollected` | `ObstacleId` field carries the coin id, `Lane`, `Value` = coin value | Pickup VFX, chime, HUD tick |
| `CoinStreak` | `Value` = streak length | Light haptic (GDD 5.4), "+50" popup |
| `ScoreBonus` | `Value` = points, `Flags` = source (`NearMiss`, `Streak`) | Score popup |

`ChunkEntered` and `TierChanged` fire when HERO's center crosses the chunk start (not when the chunk is generated).
`Died` (spec 001) ends the run; the run summary is read from state, not from an event.

### 13.2 Read-only state for views and HUD

- `TrackSnapshot`: active chunks (id, mirrored, start z, tier), obstacle ring (read-only span of `ObstacleInstance`),
  coin ring (span of `CoinInstance`), gaps per lane in range.
- `RunTotals`: `distanceM`, `coins`, `score`, `bonusScore`, `streak`, `tier`, current chunk id.
- `DeathInfo`: cause, archetype, obstacle id, `afterStumble`, chunk id, distance.
- HUD text updates only when a value changes, with non-allocating formatting (ARCHITECTURE 10.3).

### 13.3 Step order (amends spec 001 rule I8)

(1)–(7) as spec 001; **(7a) track update:** generate ahead, despawn behind, mover triggers and motion,
`ChunkEntered`/`TierChanged`; (8)–(10) as spec 001; (11) collisions against `GetBoxes`; **(11a) coin pickups and
streak** (skipped if HERO died this tick); **(11b) score** (distance, bonuses from this tick's `NearMiss` and
streak); (12) events.

---

## 14. Acceptance criteria

Test locations as in spec 001: **EditMode** = plain C# driven tick by tick; **PlayMode** = scene, frame loop, views.
Values assume the start values in section 3 and spec 001 section 3.

### Config and format
- **AC-201 [EditMode]** Every asset in section 3 and every chunk asset loads and passes validation; out-of-range
  values are rejected (for example `breatherIntervalMaxS < breatherIntervalMinS`, unsorted tier `fromM`, a weight
  for a chunk outside its tier range, a stale seam table hash, `fogStartM` not equal to the runner presentation
  value, an empty pool).
- **AC-202 [EditMode]** `DifficultyTiers.ToConfig()` gives `vMaxMps` / `minRowSpacingM` of 11.20/10.08,
  12.25/9.19, 14.17/9.21, 16.58/9.12, 19.00/9.50, 21.00/9.45 (± 0.01).
- **AC-203 [EditMode]** Every library chunk passes the static rules G1, F3–F6, F8, C1, C3, C4.
- **AC-204 [EditMode]** Mirroring a fixture chunk maps lanes `l → 2 − l` and `x → −x` for obstacles, movers and
  coins; mirroring twice gives the original.
- **AC-205 [EditMode]** Coin generation: a `Line(1, 4→12)` gives 5 coins at `x = 0`, `y = 0.75`; an `Arc` at
  10 m/s has outer coins at `zCenter ± 2.25` and `y = 1.406`, and its middle coin at `y = 2.25` (± 0.001); a
  `Trail(0→2, 10→18)` has its middle coin at `x = 0`.

### Obstacle kit and ground
- **AC-206 [EditMode]** Boxes built from `ObstacleKit` match table 5.1, and spec 001's collision tests (AC-34 to
  AC-44) pass unchanged against them.
- **AC-207 [EditMode]** A 2-lane full block produces two boxes with the same id, centered on their lane centers,
  0.36 m apart.
- **AC-208 [EditMode]** A mover at 10 m/s starts on the first tick its front is ≤ 14.0 m ahead of HERO's front,
  moves 0.08 m per tick, settles on its end lane center exactly 30 ticks later, and emits `MoverStarted` and
  `MoverSettled` once each.
- **AC-209 [EditMode]** A mover sliding sideways into HERO (fixture with z overlap) causes `Stumbled(side)`, not
  `Died`.
- **AC-210 [EditMode]** One test per cell of table 5.2: front contact is lethal for low barrier, high barrier
  (standing), full block and mover; top contact on a low barrier and side contact on each archetype are stumbles.
- **AC-211 [EditMode]** `HasGround` is true everywhere outside gaps; false when the footprint is fully inside a gap;
  true when the footprint partly overlaps ground (z at an edge, or `X` on the boundary between a gap lane and a
  ground lane).
- **AC-212 [EditMode]** Running into a 3.0 m full-width gap at 21 m/s with no input: HERO leaves the ground, gets
  exactly 5 coyote ticks, does not reach the far side, and dies with `Fell`.
- **AC-213 [EditMode]** Over a 4.0 m full-width gap at 10 m/s, a ground jump (no coyote) succeeds for exactly
  15 ± 1 consecutive command ticks; with coyote jumps included the window grows by 5 ticks.
- **AC-214 [EditMode]** A grounded lane move from a ground lane into a lane over a gap: HERO leaves the ground when
  the footprint has no ground, gets coyote time, and falls if no jump follows.

### Generator, despawn and pooling
- **AC-215 [EditMode]** Same seed → identical sequence of (chunk id, mirrored, `startZ`) for the first 200 chunks,
  10 runs out of 10.
- **AC-216 [EditMode]** Tier selection by chunk `startZ`: a chunk starting at 299.5 m uses the tier-1 pool, at
  300.0 m the tier-2 pool. Over 100,000 tier-1 picks (no-repeat off), each chunk's share is within ± 1 percentage
  point of its weight share.
- **AC-217 [EditMode]** No chunk id appears twice within any 4 consecutive normal chunks while its pool has more than
  3 chunks.
- **AC-218 [EditMode]** Planned normal-chunk time between breathers lies in [25 s, 35 s + one chunk's planned
  time]; breathers total ≥ 2.0 s × `SpeedCurve(startZ)` (one at 10 m/s, two at 21 m/s); the start chunk does not
  count.
- **AC-219 [EditMode]** With a fixture library and seam table that forbids a pair, the generator never emits that
  pair; when all attempts fail it emits `B-01` and increments the seam-fallback counter.
- **AC-220 [EditMode]** In a 20,000 m bot run: generated track always reaches ≥ `heroZ + 150 m`; nothing older than
  15 m behind HERO stays in the rings; no ring exceeds its capacity.
- **AC-221 [EditMode]** The generator draws only from the `TrackGeneration` stream: extra draws on other streams do
  not change the chunk sequence. It makes exactly 2 draws per pick attempt, 1 per breather interval and 2 per
  breather pick.
- **AC-222 [PlayMode]** In a 120 s bot run reaching 21 m/s, no pool grows and no `Instantiate`/`Destroy` happens
  after scene load (pool counters).

### Coins and score
- **AC-223 [EditMode]** A coin at `y = 0.75` in HERO's lane is collected while running and while sliding; during a
  jump it is collected when HERO's `Y ≤ 1.35` and not when `Y ≥ 1.36`. A coin in the next lane is collected when
  `|X − coin.x| ≤ 0.95` and not at 0.96.
- **AC-224 [EditMode]** At 40 m/s (0.67 m per tick) every coin of a 2 m-spaced line in HERO's lane is collected
  (swept pickup).
- **AC-225 [EditMode]** On the tick HERO dies no coin is collected.
- **AC-226 [EditMode]** 25 coins in a row → `CoinStreak`, +50 bonus, streak back to 0; a missed coin in HERO's lane
  or a `Stumbled` resets the streak; an uncollected coin in another lane does not.
- **AC-227 [EditMode]** Distance 1,234.9 m, 2 near-misses and 1 streak → score 1,324.

### Fairness and validator
- **AC-228 [EditMode]** For each static rule (G1, F3, F4, F5, F6, F8, C1, C3, C4) a fixture chunk breaking only that
  rule is rejected with that rule id in the report.
- **AC-229 [EditMode]** With static checks off, a fixture `Low[0,1,2]@10` + `High[0,1,2]@12` at 21 m/s fails F1;
  the report contains a failure trace.
- **AC-230 [EditMode]** Ceiling-trap fixture (static checks off): `Gap[0,1,2]@10 len 4.0` + `High[0,1,2]@13`
  (a branch above the gap) at 10 m/s fails F1 (F9 is covered by the real collision rules).
- **AC-231 [EditMode]** Fixture (static checks off) `Full[0,1]@38` in a 40 m chunk at 10 m/s passes F1 but fails F2
  (from lane 2, N(0) is not reachable by the chunk end).
- **AC-232 [EditMode]** **Library gate:** every library chunk, both mirrors, every quick speed of its band, all 3
  phases and all 3 entry lanes pass F1, F2, F7 and C2. The validator steps the production `RunnerSimulation` (no
  copy of movement rules). The test finishes in ≤ 60 s in the editor, or is split per chunk.
- **AC-233 [EditMode]** The seam table stored in the library equals a fresh computation, and covers every ordered
  pair and mirror combination.
- **AC-234 [EditMode]** Best-path coins: a fixture with `Line(2, 4→12)` (5 coins) and `Line(0, 24→36)` (7 coins)
  reports 12 at 10 and 21 m/s (12 m is enough to switch); a fixture with `Line(0, 4→12)` and `Line(2, 4→12)`
  (side by side) reports 5.
- **AC-235 [EditMode]** The validator is deterministic: two runs give byte-identical reports.

### Telegraph and presentation
- **AC-236 [PlayMode]** No pop-in: every obstacle and coin view is first activated while ≥ 90 m ahead of HERO, at
  10 and 21 m/s.
- **AC-237 [PlayMode]** At 21 m/s, for one scripted chunk per archetype, when the obstacle's front is 25.2 m ahead of
  HERO's front its view is active, inside the camera frustum and nearer than the fog start.
- **AC-238 [PlayMode]** Gray-box visuals are each hitbox grown by 0.08 m (± 0.005) on every side except the ground.

### Run lifecycle
- **AC-239 [EditMode]** `RunSession` (fake clock): `Ready` → first input → `Running`; the first simulation tick
  receives no command from that input.
- **AC-240 [EditMode]** `Died` → `Dying`; `GameOver` exactly 1,150 ms later; input during `Dying` is ignored;
  `GameOver` buttons ignore input for 400 ms.
- **AC-241 [EditMode]** Run again uses a new seed (1,000 restarts with a deterministic test seed source: no repeats);
  Same track reuses the seed, and a scripted bot's first 600 ticks give identical state hashes.
- **AC-242 [EditMode]** After a restart, every item in the "resets" column of 12.3 equals its value in a freshly
  built run with the same seed (including generator state and id counters); session best values are kept and only
  rise.
- **AC-243 [EditMode]** Cause text: `Hit(FullBlock)` → "Hit: Giant tree trunk"; `Hit(LowBarrier, afterStumble)` →
  "Tripped twice: Fallen log"; `Fell` → "Fell into a ravine".
- **AC-244 [PlayMode]** Restart press to first running tick ≤ 1,000 ms, with no scene reload.
- **AC-245 [PlayMode]** HUD distance equals `floor(distanceM)` and coins equal the run total on every frame; HUD
  text is not rebuilt on frames where neither changes (0 allocations).

### Events, determinism, performance
- **AC-246 [EditMode]** In a scripted run, each event of 13.1 fires exactly once per occurrence; `ChunkEntered` and
  `TierChanged` fire on the tick HERO's center crosses the chunk start.
- **AC-247 [EditMode]** Same seed + same command stream → identical state hash (runner and track rings) on every
  tick, across 30, 60, 120 fps and jittery pacing (extends spec 001 AC-62).
- **AC-248 [EditMode]** No `UnityEngine.Random`, `System.Random`, `UnityEngine.Time`, `DateTime` or physics calls in
  `JungleBooze.Gameplay.Track` (extends spec 001 AC-63).
- **AC-249 [PlayMode]** A 120 s bot run allocates 0 bytes per frame after warm-up.
- **AC-250 [PlayMode]** Simulation step with track update, 30 obstacles in range and coin pickups ≤ 0.5 ms on the
  editor benchmark; generating one chunk ≤ 0.1 ms.

---

## 15. Simulation targets (balance-simulator)

Report per run: seed, config hash (including the library), chunk sequence, tier per chunk, deaths by cause,
archetype and chunk id, coins, best-path coins, seam fallbacks.

| # | Target | Value |
|---|---|---|
| S-201 | **Oracle bot** over 100,000 generated chunks at real speeds (runs to 10,000 m) | **0 deaths, 0 stumbles** (GDD 11.3 "0 impossible segments") |
| S-202 | **Full validator sweep:** every chunk, both mirrors, every 0.25 m/s of its band, 3 phases, 3 entry lanes | 100% pass F1, F2, F7, C2 |
| S-203 | **Action windows:** for each row, the oracle's success window for its answer from a neutral approach | ≥ 6 ticks (100 ms) at every band speed; report the 10 tightest rows |
| S-204 | Density (rows per 100 m, normal chunks), 10,000 seeds | Tiers 1–3 within ± 15% of 4 / 6 / 7 |
| S-205 | Coin rate (best-path coins per second of running, normal chunks and breathers) | Tier 1: 3.15–3.85; tier 2: 3.4–4.2; tier 3: 3.7–4.5 (GDD 14.1 line from 3.5 to 5.0, ± 10%). Tiers 4–6: report only in FP1 |
| S-206 | Bot coin collection (share of best-path coins) | Average bot 55–80%; new bot ≥ 35% |
| S-207 | Run length medians (GDD 11.3) | New 25–45 s, average 90–150 s, expert ≥ 300 s. In FP1 (no vines, power-ups or tutorial) report them and flag any miss > 25% to the designer |
| S-208 | Death causes per bot level | No single archetype > 35% of deaths; "Tripped twice" ≤ 15%; deaths from an old-lane obstacle while ≥ 60% into a new lane = 0 |
| S-209 | Spikes | No 200 m window with average-bot death rate > 2× the fitted trend |
| S-210 | Chunk lethality | No chunk's average-bot death rate > 2.5× the median of its tier (flag for redesign) |
| S-211 | Variety | No chunk > 30% of picks within a tier; ≥ 6 distinct chunk ids per 1,000 m in tiers 2–3 |
| S-212 | Breathers | Planned dense time between breathers 25–35 s (+ one chunk); breathers 6–10% of run time |
| S-213 | Seam fallbacks | ≤ 1% of picks (more means the library needs seam-friendlier chunks) |
| S-214 | Determinism | 1,000 seeded runs recorded and replayed: identical final hashes; once the Python reference model includes the generator, identical chunk sequences for 100 seeds |
| S-215 | Cost | Mean step ≤ 0.5 ms headless; generation ≤ 0.1 ms per chunk; 0 allocations |

---

## 16. Changes needed elsewhere (not edited by this spec)

### 16.1 Spec 001 (player movement)
1. **9.2:** the reference boxes become final here with the same numbers; add the mover box (1.90 × 1.6 × 0–1.9 m).
   No test changes.
2. **9.1:** the swept test must use relative motion when the obstacle moves (movers move in `x`), so the entry
   axis is right and a mover pushing into HERO is a side stumble.
3. **I8 (step order):** insert 7a (track update), 11a (coin pickups), 11b (score), as in 13.3.
4. **6.5 (`ITrackQuery`):** add `GetBoxes` and `TryGetNextGapEdge`; `HasGround` takes the footprint half-width from
   `RunnerConfig` (section 6).
5. **11 (events):** append the events of 13.1. Rename the `ObstacleId` field to `EntityId` (it carries coin ids
   too), or add a field; tech-architect decides.
6. **Validator support:** the runner state must be cheap to copy (a value-type snapshot or `CopyFrom`), and the
   simulation needs a speed-source hook (curve, constant speed for the validator, tutorial later).
7. **15:** the movement gauntlet (S1–S3) can now use the real chunk library.

### 16.2 GDD and style guide
1. **GDD 5.2** "obstacle hitboxes 85% of visual" conflicts with style guide 7.4 ("within 10 cm"). Proposed: "obstacle
   hitboxes are 85% of the lane width (2.04 m); visuals are never smaller than the hitbox and at most 10 cm larger
   on the side the player interacts with."
2. **Style guide 7.4** says "never visually bigger than the hitbox on that side, so a pass that looks safe is safe".
   The reason given needs the opposite: a visual smaller than its hitbox makes safe-looking passes lethal. Proposed
   "never visually smaller" (art-director).
3. **GDD 8.2** Jungle mover "Rolling boulder down a lane" → "boulder that rolls across into the next lane and stops;
   an ink ground path shows where". **GDD 8.1** mover telegraph: marker visible from spawn, motion starts 1.4 s
   ahead, sound cue on `MoverStarted`.
4. **GDD 11.2:** normal chunks 40 m, breathers 30 m (repeated to ≥ 2 s), start chunk 50 m; density counted over
   normal chunks; the solver requirement is met offline (F1, F2, F7 and the seam table); add the note that metre
   spacing is nearly constant across tiers (3.3).
5. **GDD 13.1:** define a missed coin as in 10.5.
6. **GDD 14.1:** with one shared library, tier 1–3 chunks at tier 4–6 speeds give about 5.3–6.9 coins/s, above the
   4.4–5.0 target; tier 4–6 chunks must carry fewer coins per metre.
7. **GDD 16:** add `TrackTuning`, `ObstacleKit`, `CoinTuning`, `ScoreTuning`, `TrackPresentationTuning`,
   `RunFlowTuning`, `ChunkLibrary_Main`, `ChunkValidatorTuning`.
8. **GDD 22:** the week-2 specs `track-generation.md`, `obstacles.md` (archetypes part) and `difficulty.md` are this
   spec for FP1; `obstacles.md` keeps the signature hazards (week 3).

### 16.3 Follow-up content (needed before launch, not FP1)
- Tier 4–6 chunks (jump-then-slide combos, mixed movers) so densities reach 8–10 rows per 100 m and coin rates fit.
- Thorn patch skin on the 2-lane full block, lane-strike hazard, vine and gateway chunk kinds, onboarding's
  "first appearance teaches" rule, per-world chunk tags.

### 16.4 Architecture
- The shared `RandomStreamIds` constants file (8.1) and the validator's editor menu and seam-table storage in the
  library asset.

