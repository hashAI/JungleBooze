# Spec 002: Track, obstacles and coins

**Owner:** game-designer | **Builder:** gameplay-engineer (simulation), gameplay/ui-engineer (views, HUD, Game Over) |
**Milestone:** First Playable (FP1), stage B (simulation) and C (presentation) | **Status:** Ready to build |
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
| `telegraphMinS` | s | 1.2 | GDD 8.3; checked by AC-238 |
| `grayBoxVisualMarginM` | m | 0.08 | FP1 gray-box meshes = hitbox grown by this on every side except the ground (5.3) |
| `poolPrewarm` | per pool | see 8.6 | Pool sizes |
| `moverMarkerWidthM` | m | 0.5 | Ink ground stripe along the mover path and an end circle (style guide 4.1) |
| `gapFlagLeadS` | s | 1.0 | Style guide 4.1: gap flags readable from 1.0 s; placed at the near edge |

### 3.7 `RunFlowTuning.asset` (`RunFlowConfigAsset` → `RunFlowConfig`), app layer

| Field | Unit | Start value | Notes |
|---|---|---|---|
| `startOnFirstInput` | bool | true | Ready screen waits for the first tap, swipe or key; that input only starts the run (12.1) |
| `gameOverInputLockMs` | ms | 400 | Game Over buttons ignore input for this long after the panel appears [ASSUMED] |
| `restartMaxMs` | ms | 1000 | Budget from Restart press to the first running tick (AC-252) |
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
| `quickSpeeds` | rule | band min, each tier's `vMaxMps` inside the band, band max | Used by the EditMode test (AC-243) |
| `fullSpeedStepMps` | m/s | 0.25 | Used by the nightly/balance-simulator full sweep (S2) |
| `phaseOffsets` | fractions of a tick's distance | 0, 1/3, 2/3 | Chunk start is not tick-aligned in real runs |
| `maxStatesPerTick` | count | 50,000 | Exceeding it is a validator error, never a pass |
| `positionQuantumM` | m | 0.001 | State dedupe quantum for `X`, `Y` |

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
Nothing else changes. The validator validates both orientations (AC-243).

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

15 chunks: 1 start, 12 normal (5 tier-1, 4 tier-2, 4 tier-3... see note) and 2 breathers. Names refer to the Jungle
skin; the layouts are world-neutral (`worldMask = All`). All normal chunks are 40 m and allowed up to tier 6, so
every one of them is validated up to the 21 m/s cap.

Note: tier 1 has 5 chunks rather than 4 so the opening minutes have more variety; tiers 2 and 3 have 4 each.

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
- `TierChanged(tier)` is emitted when the tier of a generated chunk differs from the previous one (for debug HUD,
  music later).
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

A pool that has to grow logs a warning in development builds and fails AC-249.

---

## 9. Telegraph and visibility

Pillar 1 is the highest pillar: the answer to every obstacle must be visible at least **1.2 s** before HERO reaches
it, at the current speed.

1. **Spawned and unfogged:** views exist from 95 m (beyond the 90 m fog end). At 1.2 s an obstacle is at most
   1.2 × 21 = 25.2 m away (40.3 m at a 33.6 m/s boost), always inside the fog start (45 m), so it is fully
   readable, not half-fogged. Checked by AC-238.
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

- Ground coins: center at `coinHeightM` = 0.75 m. A standing, sliding or jumping HERO collects them (see 10.4), so
  a slide under a branch or a small hop never costs coins.
- Coins never touch an obstacle (rule C1): coin sphere (radius 0.25 m) + 0.1 m clearance must not overlap any
  hitbox, including a mover's start and end positions.
- Coin patterns are always clean `Line`, `Arc` or `Trail` shapes (style guide 7.2: never random clouds).

### 10.2 Arcs over jumps

An `Arc` shows the jump. Its shape follows HERO's real jump at the speed HERO will have there:
- `v = SpeedCurve(world z of zCenter)` (validator: the constant validation speed). `jumpLength = 0.6 s × v`.
- Coin `k` of `n` (`n = 7`, `k = 0..n−1`): `d = (k − (n−1)/2) × arcSpanFraction × jumpLength / (n − 1)`,
  `z = zCenter + d`, `y = coinHeightM + Yjump(d)`, where `Yjump(d) = 1.5 − ½·g·(d/v)²` (apex 1.5 m above
  `zCenter`, `g` from `RunnerConfig`).
- At 10 m/s the arc spans 4.5 m and its outer coins sit 1.53 m high (inner ones up to 2.25 m), so they are collected
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
  the coin is uncollected and `|coin.x − X| ≤ 1.2 m` (the coin was in HERO's lane). A miss or a `Stumbled` resets
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

<!-- SECTIONS 11+ -->
