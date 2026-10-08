# Spec 102: Chunks, Routes and the World Director (Phase 2 outline)

**Owner:** game-designer | **Implementers:** gameplay-engineer (data model, director, validator), tech-architect
(streaming, ADR for stream ids), balance-simulator (validator bots, difficulty curves), level authors
**Status:** v0.9 outline; numbers are binding starting values, prose detail grows in Phase 2 | **Last updated:** 2026-10-09
**Sources:** `design/aurelia/BLUEPRINT.md` Parts VII, VIII, X, XLI–XLIII, LIII; `design/aurelia/GDD.md` §8–10, §12–15;
spec 101 (movement numbers this spec depends on). Consistent with spec 101: path space `(s, x, y)`, 60 Hz ticks,
jump airtime 0.60 s, `vLatMax` 11 m/s, speed 10 → 16 m/s, hitbox and obstacle classes of spec 101 §2.6, §4.1.

---

## 1. Player story and purpose
"Every run, the forest is different but always fair. When the path splits I can tell which way is safe, which is
dangerous and shiny, and I sometimes spot a hidden way in. I never die to something I couldn't have avoided."
Serves Navigation (pillar 2) and makes the endless world possible (Part VIII) without ever generating impossible
gameplay (8.3).

## 2. Chunk data model
A chunk is a hand-authored stretch of path plus its scenery, joined to others at standard seams. Authored as a
ScriptableObject (`ChunkDefinition`) plus a scenery prefab (Addressable). Simulation reads only the definition.

| Field | Type | Notes |
|---|---|---|
| `Id` | string | `Biome_Category_Name_NN`, e.g. `F_Branch_StiltRoots_01`. Never renamed once shipped (analytics, saves) |
| `Biome` | enum | MVP: `VerdantForest` |
| `EnvironmentSet` | enum | `Forest`, `River`, `Waterfall`, `Canopy` (repetition and audio) |
| `Category` | enum | `Straight`, `Branch`, `Challenge`, `Discovery`, `Transition`, `Event`, `Recovery` (Part 8.2) |
| `Length` | m | 60–200, multiple of 10 |
| `EntryType` / `ExitType` | enum | `Ground` (MVP); `Water`, `Canopy` reserved for later open seams |
| `Rating` | 1–10 | Authored; validator checks it against measured metrics (§4.3) |
| `Dimensions` | 0–3 each | `Reaction`, `Navigation`, `Traversal`, `Risk`, `Complexity` (Part 10.2); speed comes from distance |
| `PhaseRange` | phase min–max | Phases where it may appear (§5) |
| `RequiredAbilities` | flags | Needed to pass the **main** line (MVP: always none) |
| `Routes` | list | See §3. ≥ 1 route; Branch chunks 2–3 |
| `ObstacleSlots` | list | Fixed obstacles + variant slots (≤ 4 slots × ≤ 3 options each) |
| `CoinPatterns` | list | Per route; pattern id + anchor |
| `DiscoverySlots` | 0–2 | Anchor, allowed journal categories, rarity cap, required ability |
| `PowerUpSlots` | 0–2 | Anchor; filled by the director (§7) |
| `EventCompatibility` | flags | Post-MVP events (e.g. `CreatureMigration`) |
| `RewardProfile` | enum | `Low`, `Standard`, `Rich` (multiplies coin density, crystal chance) |
| `EntryActionMargin` / `ExitActionMargin` | s at `vMax` | Time from the entry seam to the first required action and from the last required action to the exit seam (computed by the validator) |
| `SetPiece` | bool | Allowed to break environment-repetition rule R3 |

### 2.1 Seams (any chunk can follow any chunk with a matching type)
- `Ground` seam: path width 7.0 m, centreline at `x` = 0, floor `y` = 0, flat and straight in path space for a
  **6 m seam zone** on each side of the seam. No obstacles, gaps or slots in seam zones.
- The spline tangent is continuous across seams (view side); curvature radius ≥ 60 m everywhere.
- MVP rule `[ASSUMED]`: water and canopy sections are **closed inside a chunk** (enter and return to ground in the
  same chunk). Open `Water`/`Canopy` seams come with Riverlands/Canopy biomes.

### 2.2 Path geometry inside a chunk
Path segments with half-width `H(s)` (width 1.6–9.0 m; feel-course minimum 2.4 m), floor profile (ramps ≤ 20% grade,
steps ≤ 0.35 m are walkable per spec 101), gaps, and route branches (§3). Narrowing ≤ 0.10 m per metre per side.

## 3. Routes

### 3.1 Types (GDD §8)
| Route | Rating offset | Coins | Crystals | Other |
|---|---|---|---|---|
| Safe | −2 vs the chunk (min 1) | ×1.0 | base | Wide (≥ 3.5 m), flat, few required actions |
| Risky | +2 (max 10) | ×2.0–2.5 | 15–25% chance of 1 per branch | Clean Line bonus: +50% branch coins if no damage |
| Secret | ≈ chunk rating, but needs observation and/or an ability | — | 1–3 | Guaranteed discovery slot; first-time reward |

### 3.2 Readability (art direction §6)
- Fork visible ≥ **2.5 s** before the split at `vMax`; carrier cues (moss/ferns, bellcaps/sun shafts, veilmoss/duskbells)
  start 60 m ahead and peak 20–40 m ahead.
- Cue intensity multiplier from dynamic difficulty: 0.85–1.30 (§6).
- Every branch also differs in shape/light (width, elevation, openness) so cues survive grayscale and colour blindness.
- Locked branches (missing ability) stay **visible** but physically closed (vines out of reach, a flooded passage);
  they never kill and never look open.

### 3.3 Split and merge mechanics
- A split is a divider (rock, stiltwood root, waterfall pillar) ≥ 1.2 m wide. The branch is decided by the runner's
  `x` relative to the divider centre at the divider front `s` (tie → Safe). State (jump, slide, dodge) carries over.
- **Fork nudge:** from 8 m before the divider front, if the runner's hitbox would touch the divider (within its
  half-width + 0.25 m + `forkNudgeZone` 0.6 m), `xT` is moved to the nearest free `x` of the side the runner is on
  with dodge boost (spec 101 §2.3). A divider front is never a Crash. Emit `ForkNudge`.
- Branches are separate path segments with their own `H(s)`, floor and obstacles; each is ≥ 30 m long; all merge
  before the exit seam zone.
- Secret entrances are narrow (1.6–2.4 m) side openings or "through-curtain" passages. Missing one is never
  punished (no damage, no dead end).

## 4. Generator validity (never impossible)

### 4.1 Offline validator (editor, CI)
Every chunk × every variant combination × each route is checked by the **Perfect bot** (spec 101 §6.2) at the
lowest and highest speed of its `PhaseRange` with only the route's required abilities. Any hit = invalid; the asset
fails import and CI. The runtime only picks validated combinations (stored as a bitmask per chunk).

### 4.2 Rules (numbers per phase in §5)
| # | Rule |
|---|---|
| V1 | Perfect bot passes each route with 0 hits at both speed extremes |
| V2 | Time between consecutive required actions ≥ the phase's `minActionGap` at the extreme speed |
| V3 | Required lateral shift `Δx` between constraints: `Δx ≤ 8.0 m/s × (Δt − 0.10 s)` (73% of `vLatMax`, human margin) |
| V4 | Gap length 1.0 m ≤ L ≤ 0.75 × 0.60 s × v (4.5 m at 10 m/s); ≥ 0.35 s of clear floor before the gap |
| V5 | Jump then slide (or slide then jump) constraints ≥ 0.75 s apart (airtime 0.60 + 0.15) unless fast-fall is the taught intent (Challenge chunks only, rating ≥ 6) |
| V6 | Each route keeps a free corridor ≥ 1.2 m wide at every `s` (≥ 1.6 m in Learning), except full-width jump/slide obstacles |
| V7 | Every obstacle is unoccluded and inside the camera frustum ≥ 1.5 s before contact (≥ 2.0 s in Learning), both camera profiles |
| V8 | Path width 1.6–9.0 m; narrowing ≤ 0.10 m/m per side; nothing in seam zones |
| V9 | Fork visible ≥ 2.5 s; divider ≥ 1.2 m; branches ≥ 30 m; all merge in-chunk |
| V10 | Secret entrances contain no gaps or Crash-capable blockers |
| V11 | Cross-seam: previous `ExitActionMargin` + next `EntryActionMargin` ≥ phase `minActionGap` (director check at pick time) |
| V12 | Locked branches are physically closed and harmless |

### 4.3 Measured difficulty
The validator records per route: action count, min action gap, peak actions/s (1 s window), max required `Δx/Δt`,
gap count, narrowest width. It derives a measured rating and warns if it differs from `Rating` by more than 1.

## 5. Difficulty phases (Part 10.1)
| Phase | Distance | Chunk rating band | `minActionGap` | Max actions/s (1 s window) | Visibility | Forks | Recovery every |
|---|---|---|---|---|---|---|---|
| Learning | 0–500 m | 1–2 | 1.20 s | 0.8 | 2.0 s | 0 (FTUE) / ≤ 1 | 3 chunks |
| Rhythm | 500–1,500 m | 2–4 | 0.80 s | 1.2 | 1.5 s | every 4–5 chunks | 4 |
| Decision | 1,500–3,000 m | 3–5 | 0.65 s | 1.5 | 1.5 s | every 2–3 | 5 |
| Challenge | 3,000–5,000 m | 4–7 | 0.55 s | 1.8 | 1.5 s | every 2–3 | 5 |
| Danger | 5,000–8,000 m | 6–8 | 0.50 s | 2.0 | 1.5 s | every 2 | 6 |
| Mastery | 8,000 m+ | 7–10 | 0.45 s | 2.2 | 1.5 s | every 2 | 6 |
Dimension mix: the director alternates "fast but simple" and "slow but complex" (Part 10.2): no more than 2
consecutive chunks with `Reaction` ≥ 2, and no more than 2 with `Navigation` ≥ 2.

## 6. World Director

### 6.1 Inputs
Distance and phase · biome · skill estimate `S` (§6.4) · owned abilities · recent chunks (last 8: id, category,
environment, variant signature) · recent difficulty (ratings of the last 3) · cooldowns (fork, recovery, power-up,
event) · discoveries already found · run state (health, hits in the last 15 s, active power-ups) · FTUE state.

### 6.2 Outputs (one `ChunkPick` per chunk, planned 2 chunks ahead for streaming)
`{ ChunkId, VariantIndex, EnabledRoutes, RewardProfile, CoinDensityMultiplier, CrystalPlacements, PowerUpSlot,
DiscoveryAssignments, CueIntensity, EventFlag (post-MVP) }`.

### 6.3 Selection algorithm
1. **Filter:** entry matches previous exit; `PhaseRange` contains the phase; rating within band shifted by DDA;
   `RequiredAbilities` ⊆ owned; repetition rules R1–R5 pass; V11 passes.
2. **Forced picks:** Recovery if due (§5) or mercy triggered (2 hits within 15 s); Branch if the fork cooldown expired;
   FTUE script overrides everything in the first run.
3. **Weight:** `w = baseWeight × freshness × dimensionFit × skillBias`, where freshness = 0.5 if used in the last 12
   chunks else 1.0; dimensionFit favours the dimension not used last; skillBias raises Challenge/risky-rich chunks for
   `S > 0.5` (×1.3) and Recovery/Straight for `S < −0.5` (×1.3).
4. **Pick** with the `TrackGeneration` stream; variant with `ObstacleVariants` stream (validated combos only); coins,
   crystals, power-ups with `Pickups`; discovery assignment with a new `Discovery` stream (stream ids are appended,
   never renumbered).
5. **Fallback:** if the filter is empty, relax in order: freshness → dimension mix → band ±1 → pick any Recovery chunk
   with a matching entry. Content rule: at least 2 Recovery chunks per entry type exist.

### 6.4 Dynamic difficulty (basic; Part 10.3)
Per run: `distScore = clamp(ln(distance / 1,500 m) / ln 4, −1, 1)`, `hitScore = clamp(1 − hitsPerKm, −1, 1)`,
`travScore = clamp((traversalSuccessRate − 0.85) / 0.15, −1, 1)`;
`S_run = 0.5·distScore + 0.3·hitScore + 0.2·travScore`.
Update after each run: `S ← S + clamp(0.3 · (S_run − S), −0.2, +0.2)`; new players start at `S = −0.3` `[ASSUMED]`.
Effects (applied at run start, never mid-run except mercy):
| `S` | Band shift | Recovery every | Cue intensity | Risky branch reward shown |
|---|---|---|---|---|
| < −0.5 | −1 | phase value − 1 (min 3) | 1.30 | unchanged |
| −0.5…0.5 | 0 | phase value | 1.00 | unchanged |
| > 0.5 | +1 | phase value + 1 | 0.85 | unchanged |
Never changed by DDA: the speed curve, the spec 101 movement numbers, and anything referencing the player's best
distance (Part XXVII: no manipulated "almost").

## 7. Rewards placement
- Coins: patterns per route; base density 1 coin / 4.5 m on Safe; Risky ×2.0–2.5; Low/Standard/Rich profiles ×0.7/1.0/1.3.
- Crystals: 1 per ~1,000 m of flow (Pickups stream, min spacing 600 m), Risky 15–25% per branch, Secret 1–3.
- Power-ups: one slot filled every 600–900 m (more likely on Risky), never inside seam zones or within 1 s of a
  required action.
- Discovery slots: filled with undiscovered entries first (weighted by rarity), then repeats for "seen again" flavour.
  FTUE guarantees across runs 1–3: first creature encounter ≤ 5 min cumulative play; first secret reachable ≤ 10 min.

## 8. Repetition prevention
| # | Rule |
|---|---|
| R1 | No chunk id within the last 6 chunks |
| R2 | Same category at most 2 in a row (Recovery never twice in a row) |
| R3 | Same environment set at most 3 in a row unless `SetPiece` |
| R4 | Same variant signature not within the last 3 chunks of that id's family |
| R5 | Recovery cadence per §5 / §6.4 |

## 9. MVP chunk set (14; Verdant Forest)
| # | Id | Category | Rating | Env | Routes | Required ability for a branch |
|---|---|---|---|---|---|---|
| 1 | F_Straight_Glade_01 | Straight | 1–3 | Forest | 1 | — |
| 2 | F_Straight_Roots_01 | Straight | 2–5 | Forest | 1 | — |
| 3 | F_Recovery_Meadow_01 | Recovery | 1 | Forest | 1 | — |
| 4 | F_Recovery_Riverbank_01 | Recovery | 1–2 | River | 1 | — |
| 5 | F_Branch_StiltRoots_01 | Branch | 3–6 | Forest | Safe/Risky/Secret | Secret: Trail Sense |
| 6 | F_Branch_Ravine_01 | Branch | 4–7 | Forest | Safe/Risky | Risky high ledge: Root Vault |
| 7 | F_Challenge_FallenGiants_01 | Challenge | 5–8 | Forest | 1 (+fast-fall line) | — |
| 8 | F_Challenge_ThornRun_01 | Challenge | 6–9 | Forest | 1 | — |
| 9 | R_Transition_Ford_01 | Transition | 2–4 | River | 1 | — |
| 10 | R_Swim_Pool_01 | Straight (swim) | 3–6 | River | Safe/Secret | Secret: Deep Breath |
| 11 | R_Branch_Waterfall_01 | Branch | 4–8 | Waterfall | Safe/Risky/Secret | Secret behind the falls: observation only |
| 12 | C_Canopy_VineSpan_01 | Branch (set piece) | 4–7 | Canopy | Safe/Risky | Risky: Vine Grip |
| 13 | D_Discovery_Grotto_01 | Discovery | 2–5 | Forest | Safe/Secret | Secret chamber: Shoulder Charge |
| 14 | D_Discovery_Overlook_01 | Discovery | 1–3 | Waterfall | 1 | — (vista + creature sighting) |
Chunks 10–12 depend on specs 103 (swim) and 104 (vine). Double Jump's upper canopy secret is a second variant of #12.

## 10. Acceptance criteria (outline; extended in Phase 2)
- **AC-102-01** Validator rejects a chunk where the Perfect bot takes any hit on any validated combo/route at either speed extreme.
- **AC-102-02** Validator flags V2–V12 violations with the rule id and `s` position.
- **AC-102-03** Same seed + same abilities + same `S` + same inputs → identical chunk sequence, variants and pickups.
- **AC-102-04** Adding a random call in pickup placement does not change the chunk sequence (separate streams).
- **AC-102-05** Over 10,000 generated chunks per phase, R1–R5 are never violated.
- **AC-102-06** Never selects a chunk whose main line needs an unowned ability; locked branches are closed.
- **AC-102-07** Recovery cadence and mercy (2 hits in 15 s → next unplanned chunk is Recovery) hold in all phases.
- **AC-102-08** Fork side is decided by `x` vs divider centre at the divider front; ties → Safe; a runner aimed at the divider is nudged, never crashed.
- **AC-102-09** `S` update matches §6.4 for scripted run histories; |ΔS| ≤ 0.2 per run; DDA never alters speed.
- **AC-102-10** Director planning and streaming of 2 chunks ahead allocate 0 bytes per tick after warm-up.
- **AC-102-11** Empty filter falls back as in §6.3 step 5 and never stalls.

## 11. Simulation targets (balance-simulator)
- Average bot (spec 101 §6.2) median distance on runs 1–5 with `S` starting −0.3: 1,200–2,500 m; Novice 500–1,200 m;
  Perfect never dies before 8,000 m.
- Deaths spread: no single chunk causes > 20% of Average-bot deaths in any phase.
- Route mix for Average bot choosing by expected value: Risky taken 30–50% of forks.
- Economy: Average bot unlocks a new ability every 1–3 runs during runs 1–10; first run (FTUE) earns ≥ 150 coins.
- DDA convergence: a bot whose skill changes mid-series reaches a stable `S` within 4 runs, without oscillating by > 0.3.

## 12. Assumptions `[ASSUMED]`
Closed water/canopy sections in MVP; 6 m seam zones; DDA start `S` = −0.3; human-margin lateral rate 8 m/s; fork
nudge 0.6 m; new `Discovery` random stream; 14-chunk MVP list and its ability gates.
