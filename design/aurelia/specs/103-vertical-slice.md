# Spec 103: The 5-Minute Vertical Slice (Phase 2 go/no-go gate)

**Owner:** game-designer | **Implementers:** gameplay-engineer (swim, vine, canopy, creature, pickups, shield, script,
director hooks), ui-engineer (toast, results, upgrade card, HUD), level authors (chunk layouts below), balance-simulator
(bots, sim targets §13), audio/VFX (hooks §11), qa-engineer (fun playtest §12)
**Status:** v1, ready for implementation | **Last updated:** 2026-10-09
**Sources:** `BLUEPRINT.md` Parts IV, VI, VII, XIII–XIX, XXVII–XXVIII, XXXIII–XXXVI, LIII, **LV (this gate)**, LVI, LXIII;
`GDD.md`; spec 101 (movement, collisions, health, camera); spec 102 (chunk model, routes, validator, World Director);
`ENVIRONMENT_STRATEGY.md` §10 and `keyframes/` (F1–F4, P1: the places the slice is built around); `DECISIONS.md`.
**Supersedes:** the GDD's planned "spec 104 vine" (vine, canopy and swim live here). Spec 104 is free for later
climbing/upper-canopy work. **Not in scope:** journal screen, Magnet, Explorer Vision, abilities 2–7, events, audio
content (hooks only), climbing, gliding, double jump.

Units: m, s, m/s, pt. 60 ticks/s. Path space `(s, x, y)` as in spec 101 §2.1. All numbers are starting values and live in
ScriptableObjects (§14). `[ASSUMED]` = designer default awaiting owner review.

---

## 1. Player story and purpose

"I run out from under giant roots into a sunlit forest, pick a shiny dangerous path, wade into a turquoise river, swim
through it, stop breathing for a second at the falls, swing across a gorge on vines, run along branches high above the
ground, startle little gliding lizards that lead me to a hidden grotto behind a waterfall, and then a nasty stretch of
fallen trees finally gets me. The results tell me I can now dive deep, and I remember that glinting passage under the
river. I hit RUN AGAIN."

This is Blueprint Part LV: forest → obstacle → route choice → river → swimming → waterfall → vine → canopy → creature →
secret → difficult section → death → reward → upgrade → replay. **If it is not fun, we stop, fix the gameplay and add no
content.** Every beat serves a pillar: Movement (swim, vine, canopy), Navigation (two forks, a secret), Discovery
(location, creature, secret), Progression (an ability that opens a place the player has already seen).

## 2. The slice at a glance

The slice is **Expedition 1**: the first-ever run, a fixed script (§10). The run itself takes ~4:05 to clear the
gauntlet; a player who dies in the gauntlet goes death → results → upgrade → running again in ≤ 5:00 total.

| # | Beat | Chunk (spec 102 id) | Run `d` (m) | Clock (enter) | Keyframe |
|---|---|---|---|---|---|
| 0 | Establishing shot (4 s, no input needed) | — | 0 | 0:00 | F1 |
| 1 | Forest, steering | `F_Start_RootGate_01` **(new)** | 0–160 | 0:04 | F1 |
| 2 | Obstacles: jump, slide, dodge | `F_Straight_Roots_01` var. `Learn` | 160–380 | 0:20 | — |
| 3 | First route choice | `F_Branch_StiltRoots_01` var. `Slice` | 380–600 | 0:41 | F2 |
| 4 | Rhythm, first gap | `F_Straight_Glade_01` var. `Slice` | 600–800 | 1:01 | — |
| 5 | River (ford) | `R_Transition_Ford_01` | 800–1,020 | 1:19 | F3 |
| 6 | Swimming | `R_Swim_Pool_01` | 1,020–1,340 | 1:38 | — |
| 7 | Waterfall vista | `D_Discovery_Overlook_01` | 1,340–1,520 | 2:13 | F4 / P1 |
| 8 | Vine + canopy + creature | `C_Canopy_VineSpan_01` | 1,520–1,820 | 2:27 | — |
| 9 | Secret behind the falls | `R_Branch_Waterfall_01` | 1,820–2,060 | 2:51 | — |
| 10 | Rhythm | `F_Straight_Roots_01` var. `B` | 2,060–2,260 | 3:10 | — |
| 11 | Breather + Shield | `F_Recovery_Meadow_01` | 2,260–2,380 | 3:25 | — |
| 12 | Difficult section, part 1 | `F_Challenge_FallenGiants_01` | 2,380–2,620 | 3:34 | — |
| 13 | Difficult section, part 2 | `F_Challenge_ThornRun_01` | 2,620–2,820 | 3:52 | — |
| — | Endless continuation (World Director) | §10.3 | 2,820+ | 4:07 | — |
| — | Death → results → upgrade → RUN AGAIN | §9 | — | ≤ 5:00 | — |

Clock assumes no hits (speed curve spec 101 §2.2; swim speed §4.2). The compression of Blueprint 4.2's 10-minute
first-run timeline into 4 minutes is deliberate for the gate `[ASSUMED]`; GDD §16 follows this table and is revisited
after the owner's Phase 2 verdict.

**Chunk set impact:** one new chunk (`F_Start_RootGate_01`, the start of every run) brings the MVP set to 15 (Blueprint
LIII: 10–15). The script uses 12 of the 15; `F_Recovery_Riverbank_01` is not scripted but joins the Phase 2 pool
(§10.2) as the second Recovery chunk; `F_Branch_Ravine_01` and `D_Discovery_Grotto_01` are Phase 3.

## 3. Chunk layouts

### 3.0 Notation and shared rules
- Positions are chunk-local `s` (0 = entry seam). Every chunk obeys spec 102 §2.1 seams (7.0 m wide, flat, 6 m clear
  zones at both ends) and V1–V12. Default path width `W` = 7.0 m (`x` −3.5…+3.5) unless stated.
- Obstacles (spec 101 §4.1 classes): **Low h** = Low with authored top `h`; **High b** = High with authored bottom `b`;
  **Blk w @x** = Blocker `w` wide centred at `x`; **Thorns** with an `x` range; **Gap** = no floor over the `s` range;
  **Walk** = walkable top. "Full" = full path width. `s` of an obstacle = its front face; default depth 0.6 m
  (logs 2.0 m).
- Coins: **Line(s0–s1, x, step)**, **Weave(s0–s1, amp, period)** (`x = amp·sin(2π(s−s0)/period)`), **Arc(s, x, n)**
  (n coins on the ideal jump arc centred on the obstacle), **Under(s, n)** (at `y` 0.3 under a High). Step default 1.5 m.
- Crystals are placed by the script in Expedition 1 (§10.1); in endless runs they come from spec 102 §7 rules.
- Discovery slots are **D-xx** (§7.3). Slow-time help markers are **[ST:move]** (Expedition 1 only, GDD §16).
- In Branch chunks, each branch is its own path segment (own width ≤ 9.0 m); the combined footprint may exceed 9.0 m
  inside the branch zone (clarifies spec 102 V8).

### 3.1 C1 `F_Start_RootGate_01` (new) · Straight · rating 1 · Forest · 160 m · Ground→Ground · routes: 1
Phase range Learning–Rhythm (the opening chunk of every run). Composition = keyframe F1 (out of the root arch, curving
trail, rootstone arch and falls through the canopy gap).
| `s` | Content |
|---|---|
| 0–30 | Pista emerges under the stiltwood root arch (camera corridor kept clear). Path widens from 7.0 m at 6 (seam zone end) to 8.0 m by 25 |
| 20–58 | Coins Line(20–58, 0, 4.0): 10 coins |
| 60–130 | Coins Weave(60–130, 2.5, 35): 28 coins. Whirlseeds spin down through a sun shaft at 70–90 (ambient) |
| 140 | **Blk 1.4 @0** (mossy boulder) **[ST:steer]**; coins Line(134–146, −2.0, 3.0): 5 coins |
| 148–154 | Back to 7.0 m |
Coins 43. No damage possible before `s` 140. From run 2 every run opens with variant `Short` (60 m: arch + coin line,
no blocker) `[ASSUMED]` (§10.2).

### 3.2 C2 `F_Straight_Roots_01` variant `Learn` · Straight · rating 2 · Forest · 220 m · routes: 1
Learning rules (min action gap 1.20 s, visibility 2.0 s). Variant `Learn` is script-only.
| `s` | Content |
|---|---|
| 20 | **Low 0.5 full** (root) **[ST:jump]**; Arc(20, 0, 3) |
| 45 | **Low 0.6 full**; Arc(45, 0, 3) |
| 70 | **Low 0.9 full, Walk** (log, 2.0 m deep); Line(70–72, 0, 1.0) on top: 3 coins |
| 100 | **High 1.0 full** (hanging branch) **[ST:slide]**; Under(100, 3) |
| 125 | **High 1.0 full** |
| 160 | **Blocker spanning x −3.5…+0.5** **[ST:dodge]** (flick glyph); Line(150–175, +2.0): 9 coins |
| 180 | **Blocker spanning x −0.5…+3.5**; Line(176–190, −2.0): 6 coins |
| 200 | **Low 0.5, x −3.5…0** (jump or steer right); Arc(200, −1.8, 3) |
Coins ≈ 50 (incl. connecting lines). Min spacing 20 m = 1.83 s at 10.9 m/s.

### 3.3 C3 `F_Branch_StiltRoots_01` variant `Slice` · Branch · rating 3 (Safe 1 / Risky 4) · Forest · 220 m
Keyframe F2. The **first route choice.** Secret branch (Trail Sense, spec 102 §9) is present but **locked** (V12).
| `s` | Content |
|---|---|
| 20–50 | Path widens to 9.0 m (`x` −4.5…+4.5). Carrier cues from 0: bellcaps + gold pollen + sun shafts left, soft moss and even light centre/right, veilmoss and duskbells far right |
| 60–170 | **Divider D1**: stiltwood root wall `x` −1.6…−0.4 (front at 60, fork visible from `s` ≤ 33 = 2.5 s) |
| Left = **Risky** | `x` −4.5…−1.6 (2.9 m, narrowing to 2.4 m by 80). Raised root ridge: ramp +1.2 m over 62–72 (12%), down over 160–170. **Low 0.5 full** at 95 · **High 1.0 full** at 120 · **Low 0.6 full** at 148 with **crystal** at `y` 1.6 on the arc (script) · coins ×2.2: Line(75–158, ridge centre, 1.5) ≈ 55 coins. Clean Line bonus on merge |
| Right = **Safe** | `x` −0.4…+4.5 (4.9 m), flat. **Low 0.4, x +0.5…+4.5** at 115 (steer left or jump). Coins Line(65–165, +1.5, 4.0) ≈ 25 |
| 100–130 | **Locked secret teaser**: beyond the right soft edge (`x` > +4.5), a veilmoss curtain over a sealed rootstone crack with a faint cyan glint and cool mist. No path, no collision role. Becomes the Trail Sense secret in Phase 3 |
| 170–190 | Merge; back to 7.0 m by 200 |
Fork nudge per spec 102 §3.3. In Expedition 1, risky is **optional** (no slow-time, no glyph).

### 3.4 C4 `F_Straight_Glade_01` variant `Slice` · Straight · rating 3 · Forest · 200 m
Rhythm rules (0.80 s). First gap.
| `s` | Content |
|---|---|
| 25 | **Low 0.6 full**; Arc |
| 45–47.5 | **Gap 2.5 m full** **[ST:gap]** (first gap counts as its own move for slow-time help) `[ASSUMED]`; Arc(46, 0, 4) |
| 70 → 82 | **High 1.0 full** → **Low 0.6 full** (slide→jump cancel, 1.07 s) |
| 105, 120 | **Blk 1.2 @+1.0**, **Blk 1.2 @−1.5**; Weave(95–130, 1.8, 25) |
| 145–150 | **Thorns x −3.5…−1.0** (steer right or jump) |
| 170–173 | **Gap 3.0 m full**; Arc |
Coins ≈ 45.

### 3.5 C5 `R_Transition_Ford_01` · Transition · rating 2 · River · 220 m
Keyframe F3. Shallow water is ground with `Surface = ShallowWater` (splash footsteps, ripple VFX); **no speed or arc
change** `[ASSUMED]` so the land rules stay predictable.
| `s` | Content |
|---|---|
| 6–60 | Path descends 1.0 m to river level (≤ 8% grade); river opens on the right |
| 60–180 | Ford, ankle-deep. **Blk 1.4 @−1.5** at 80, **Blk 1.4 @+1.8** at 100, **Blk 1.6 @−0.5** at 120 (boulders); **Low 0.5 full, Walk** (driftwood) at 140; Weave(65–135, 2.0, 30) + Line on the driftwood ≈ 35 coins |
| 90–130 | **Ambient sailback crossing** 45–60 m ahead, `y` ≥ 8 m, 2 animals: foreshadowing, outside the discovery volume (§7.2) |
| 180–220 | Bank widens; the deeper, faster river is visible ahead. Coins ≈ 5 |

### 3.6 C6 `R_Swim_Pool_01` · Straight (swim) · rating 3 · River · 320 m · routes: Safe / Secret (Deep Breath)
Closed water section (spec 102 §2.1). River width 9.0 m (`x` −4.5…+4.5) between soft banks. Mechanics in §4.
| `s` | Content |
|---|---|
| 6–30 | Bank slopes into the water; depth reaches `swimEnterDepth` at 30 → swim |
| 40–120 | **Lateral current +1.5 m/s** (pushes right); coins Line(45–115, −2.5, 2.0) on the surface: 36 coins. Cue: foam streaks flowing right |
| 60 | **FloatingLog full** **[ST:dive]** (dive under or leap over) |
| 90 | **Debris mat (FloatingLog) x −4.5…+0.5** (steer right, dive or leap) |
| 115 | **Snag full** **[ST:leap]** (branches above water, tangle below: leap only) |
| 140–200 | **Rapids: forward current +3.0 m/s**; **Rock 1.4 @+1.5** at 150, **@−1.5** at 165, **@+0.5** at 180; Weave(140–200, 1.6, 30) |
| 210–250 | Calm deep pool, rootstone arch overhead. **Underwater coins** Line(230–240, 0, 1.5) at `y` −1.1 (dive only): 7 coins |
| 214–222 | **DeepDiveZone** `x` −3.5…−0.5: the **Sunken Arch** entrance 2.5 m down, cyan glint, 2 crystals visible in the dark. **Locked without Deep Breath** (a normal dive reaches 1.1 m and can't enter). With Deep Breath: deep dive (§4.5) through 222–262, surfaces at `s` 262, `x` −2.0, 2 crystals + **D-04 Sunken Arch** |
| 255 | **LowBranch x −1.0…+4.5** (bottom 0.5 m above water: dive or steer left) |
| 270 | **FloatingLog full** |
| 290–305 | Riverbed rises; wade out at depth < `swimExitDepth`; running resumes |
Coins ≈ 50 (+ crystals behind the lock). Validator: swim rules W1–W5 (§4.7).

### 3.7 C7 `D_Discovery_Overlook_01` · Discovery · rating 1 · Waterfall · 180 m
Keyframes F4 (landscape) and P1 (portrait): the hero waterfall under the rootstone arch, plunge pool, mist valley.
| `s` | Content |
|---|---|
| 0–60 | Wet rock ledge path 7.0 m, spray drifting across; Line(10–60, 0, 3.0) |
| 90 | **D-01 Falls Basin** (Location) trigger: vista camera beat (§11) |
| 130 | **Low 0.5 full** (wet rock); Arc |
| 150–180 | Path turns toward the giant stiltwood beside the falls (the canopy entrance) |
Coins ≈ 25. This is the "wonder" beat: nothing hard happens here on purpose.

### 3.8 C8 `C_Canopy_VineSpan_01` · Branch (set piece) · rating 4 · Canopy · 300 m
Closed canopy section. Mechanics in §5 (vine) and §6 (canopy). Heights are relative to the chunk's entry floor.
| `s` | Content |
|---|---|
| 20–70 | Root ramp climbs +9.0 m (18%); width 7.0 → 3.0 m by 60; coins Line up the ramp |
| 70–96 | Takeoff funnel, width 2.4 m centred on `x` 0; lip at 96 |
| 96–104 | **Gorge gap** (8.0 m; not jumpable: max jump length at this speed ≈ 7.6 m). Vine gaps are exempt from V4; V13 governs them |
| 99 | **Vine V1**: anchor `sA` = 99, `x` 0, 8.0 m above the takeoff floor **[ST:release]** |
| 108.6–109.6 | **Perfect column V1**: 3 coins at `y` 2.6 / 3.4 / 4.2 above the landing floor (perfect arcs only) |
| 104–140 | Landing branch, 2.4 m wide, same height as the takeoff; coins Line(112–136, 0, 2.0) |
| 140 | Lip; 140–148 gap; **Vine V2** anchor 143; **perfect column V2** at 152.0–153.0: 2 coins + **1 crystal** at `y` 3.4 |
| 148–175 | **Beam A** (2.4 m wide, `x` 0). **High 1.0 full** (hanging moss) at 160 |
| 172–181 | **Sailback perch** (§7): 3 sailbacks on a side branch at `x` +2.8, `y` +1.5, at 172 / 176 / 181. **D-02 Sailback** |
| 175–178.5 | **Gap 3.5 m** to **Beam B** (2.0 m wide, centred `x` +0.8) |
| 192 | **Low 0.5 full, Walk** (branch knot) |
| 205–208 | **Gap 3.0 m** to **Beam C** (2.4 m wide, centred `x` −0.6) |
| 222 | **High 1.0 full** |
| 175–235 | Coins: Line along each beam centre, Arc over each gap ≈ 30 |
| 140–235 | **Locked upper vine line** (teaser for Vine Grip, Phase 3): vines hanging 3 m above reach on the left, gold-lit |
| 240–290 | Descent ramp spiralling down the trunk, −9.0 m (18%), width 2.4 → 7.0 m |
Coins ≈ 40 + perfect columns. Action spacing at 12.6 m/s: 160/175/192/205/222 → min 13 m = 1.03 s.

### 3.9 C9 `R_Branch_Waterfall_01` variant `Slice` · Branch · rating 4 (Safe 2 / Risky 6 / Secret 3) · Waterfall · 240 m
The **secret** (§8). Veil Falls: a thin, wide fall pouring off a rootstone ledge into a plunge pool.
| `s` | Content |
|---|---|
| 40–60 | Path widens to 9.0 m |
| 70–180 | **Divider D1**: waterfall pillar `x` −2.4…−1.2 (front at 70; visible from `s` ≤ 38) |
| Left = **Risky** | `x` −4.5…−2.4 (2.1 m): wet stepping rocks across the plunge pool. **Gap 3.0** 100–103 · **Low 0.6 full** 112 · **Gap 3.5** 125–128.5 · **High 1.0 full** 145 · **crystal** at 155 (`y` 1.0; script) · coins ×2.2 ≈ 66. Bellcaps, sun shafts, gold spray |
| Right = **Safe** | `x` −1.2…+2.6 (3.8 m) along the pool's edge. **Low 0.5, x −1.2…+1.0** at 130 · coins ≈ 30 |
| 90–110 | The right edge widens to `x` +5.8 (+1.3 m over 13 m) |
| 110–165 | **Divider D2**: rock pillar `x` +2.6…+3.8 (front at 110). **Secret entrance** `x` +3.8…+5.8 (2.0 m) |
| 118–121 | **Water curtain** (no collision; VFX/SFX pass-through) |
| 121–165 | **Veil Grotto**: dim cave, cyan crystal veins. **2 crystals** at 135 and 150 (`x` +4.8). **D-03 Veil Grotto** trigger at 140. Roosting sailbacks inside (seen-again flavour). Coins Line(125–160, +4.8, 2.0) ≈ 18. No gaps, no blockers (V10) |
| 165–175 | Grotto merges into Safe; D1 ends 180; back to 7.0 m by 200 |

### 3.10 C10 `F_Straight_Roots_01` variant `B` · Straight · rating 3 · Forest · 200 m
| `s` | Content |
|---|---|
| 20 | **Low 0.6 full** · 40 **High 1.0 full** · 60 **Blk 1.2 @+1.2** |
| 80 → 92 | **Low 0.6 full** → **High 1.0 full** (0.92 s) |
| 115–118.5 | **Gap 3.5 full** |
| 135, 150, 165 | **Blk 1.4 @−1.5**, **@+1.5**, **@−1.0** (slalom) |
| 185 | **Low 0.5 full** |
Coins ≈ 45.

### 3.11 C11 `F_Recovery_Meadow_01` · Recovery · rating 1 · Forest · 120 m
Open sunny meadow, whirlseeds, slower music layer. Coins Line(10–110, weave 1.5) ≈ 25.
**Shield** (§9.3) at `s` 61, `x` +2.5, `y` 2.3: only reachable by jumping there (a running body tops out below it).
The player chooses: grab it before the gauntlet, or not.

### 3.12 C12 `F_Challenge_FallenGiants_01` · Challenge · rating 6 · Forest · 240 m
Difficult section part 1: huge fallen trunks. **Challenge-phase rules** (min gap 0.55 s, max 1.8 actions/s) although
the run is in Decision distance: the script's deliberate spike. No fast-fall-only combos (V5 exception not used, since
fast-fall was never taught).
| `s` | Content |
|---|---|
| 15 | **Low 0.9 full, Walk** (trunk, 5 m long top) |
| 28 | **High 1.0 full** |
| 38 | **Low 0.6 full** (slide→jump, 0.75 s) |
| 52–56 | **Gap 4.0 full** |
| 66 | **Blk 2.0 @−1.0** + **Blk 2.0 @+2.5** (corridors 1.5 m at `x` −3.5…−2.0 and 0…+1.5) |
| 78 | **High 1.0 full** |
| 90 | **Thorns x −3.5…0** + **Low 0.5 x 0…+3.5** (a jump everywhere) |
| 104–108 | **Gap 4.0, x −3.5…+1.0** (floor stays on `x` +1.0…+3.5: steer right or jump) |
| 118 → 129 | **Low 0.6 full** → **High 1.0 full** (0.82 s) |
| 142 | **Blk 1.6 @0** (coins on both sides) |
| 156–160.5 | **Gap 4.5 full** |
| 168 → 178 | **High 1.0 full** → **Low 0.6 full** |
| 192, 204 | **Blocker x −3.5…+0.5**, then **Blocker x −0.5…+3.5** |
| 218–221.5 | **Gap 3.5 full** |
Coins ≈ 60 (arcs over every gap, lines in corridors).

### 3.13 C13 `F_Challenge_ThornRun_01` · Challenge · rating 6 · Forest · 200 m
Difficult section part 2.
| `s` | Content |
|---|---|
| 15–60 | Path narrows to 3.0 m. **Thorns x −1.5…0** at 25, **x 0…+1.5** at 35, **x −1.5…0** at 45 (weave) |
| 60 | **High 1.0 full** |
| 75–78 | **Gap 3.0 full** |
| 85–100 | Back to 7.0 m |
| 92, 101, 110 | **Blk 1.4 @−2.0**, **@+1.0**, **@−1.0** |
| 125 → 137 | **Low 0.8 full, Walk** → **High 1.0 full** |
| 150–154.5 | **Gap 4.5 full** |
| 165 | **Thorns full**, height 0.6 (jump) |
| 178 | **High 1.0 full** |
Coins ≈ 45. After C13, the script hands over to the World Director (§10.3).

## 4. Swimming

### 4.1 Player story
"Water feels different: heavier, floatier, the river pushes me, but my thumb still does exactly what it does on land:
up goes over, down goes under."

### 4.2 Entering, moving, leaving
- **Water volume** (chunk data): `s` range, surface height, depth profile, current fields. Water is never a hazard by
  itself: **there is no drowning, no Crash and no Fall in water** `[ASSUMED]`.
- **Enter:** when the depth under Pista ≥ `swimEnterDepth` → state `Swimming`. Forward speed blends to the swim speed
  over `swimEnterBlendTime`; body `y` settles to the surface line. Entering from the air = splash entry (landing tick).
- **Forward:** `vSwim = swimSpeedFactor · v(d) + cs(s)` (`cs` = forward current). `d` keeps accumulating real `s`.
- **Lateral:** same servo as spec 101 §2.3 with swim values. **Lateral current** `cx(s)` drifts the lateral *target*:
  `xT += cx · dt` every tick, so the player must drag against it (the servo alone would hide it).
- **Leave:** depth < `swimExitDepth` (hysteresis) → `Running`; forward speed blends back to `v(d)` over
  `swimExitBlendTime`. A dive in progress ends with its Up phase first.
- Banks are soft walls (spec 101 rules, `EdgeBrush` with a water-splash variant).

| Name | Value | Unit | Notes |
|---|---|---|---|
| `swimEnterDepth` / `swimExitDepth` | 0.9 / 0.6 | m | Hysteresis |
| `swimEnterBlendTime` / `swimExitBlendTime` | 0.25 / 0.30 | s | |
| `swimSpeedFactor` | 0.70 | × `v(d)` | 8.4 m/s at 1,185 m |
| `swimTau` | 0.14 | s | Floatier than land (0.075) |
| `swimVLatMax` | 7.0 | m/s | |
| `swimAccelLat` / `swimDecelLat` | 45 / 60 | m/s² | |
| `swimDodgeDistance` / `swimDodgeVLatMax` / `swimDodgeBoostTime` | 1.8 / 9.0 / 0.25 | m / m/s / s | A strong stroke |
| `maxLateralCurrent` | 2.0 | m/s | Author limit; validator enforces |
| `maxForwardCurrent` | 3.0 | m/s | |
| Swim hitbox | width 0.60, depth 1.00, height 0.70 centred on body `y` | m | Horizontal swimmer |

### 4.3 Dive (swipe down) and leap (swipe up)
**Dive:** Down phase `diveDownTime` → Under phase `diveUnderTime` (body `y` = `diveDepth`) → Up phase `diveUpTime`.
Only the **Under** phase is `Submerged`: surface obstacles (FloatingLog, LowBranch) don't touch, underwater coins can be
collected. Snags and Rocks still do.
**Leap:** `vy = leapVelocity`, spec 101 gravity (`gUp`, `gDown`, apex hang). Airborne hitbox while leaping: width 0.50,
depth 0.40 (tuck), height 0.70. Splash-down = landing tick in water.

| Name | Value | Unit | Notes |
|---|---|---|---|
| `diveDepth` | −1.10 | m | Body centre below the surface line |
| `diveDownTime` / `diveUnderTime` / `diveUpTime` | 0.10 / 0.55 / 0.20 | s | 6 / 33 / 12 ticks, total 51 |
| `leapVelocity` | 8.64 | m/s | Apex 1.20 ±0.03 m above the surface line, airtime 0.55 ±0.03 s |
| `diveRiseFactor` | 2.0 | × | Up phase speed when a leap cancels a dive |

### 4.4 Command table (water); mirrors the land grammar
| State | Swipe up | Swipe down | Flick |
|---|---|---|---|
| Swimming (surface) | Leap now | Dive now | Swim dodge |
| Diving (Down/Under) | Cancel: rise at 2× and leap on surfacing; if a surface obstacle is overhead, held (spec 101 ceiling-hold, 0.35 s) | Restart Under timer | Swim dodge |
| Diving (Up phase) | Leap on surfacing | Dive again on surfacing | Swim dodge |
| Leaping (air) | Buffered 150 ms (executes on splash-down) | Dive-in: `vy = −14 m/s`; on splash-down a full dive starts | Swim dodge |
| Entry tick (from land/air) | A buffered jump becomes a leap | A buffered slide becomes a dive; a fast-fall becomes a dive | — |
A slide in progress when the water gets deep ends on the entry tick.

### 4.5 Deep Breath (first ability; §9.4)
A `DeepDiveZone` is authored with an `s`/`x` box, an underwater path spline and an exit point. If the player owns Deep
Breath and a dive **starts** inside the zone: `DeepDive` state follows the spline (`deepDiveDepth` −2.5 m, duration
`deepDiveTime` 2.4 s, lateral input ignored, invulnerable to everything, camera goes under, §11), then surfaces at the
exit point in `Swimming`. Without the ability, a dive in the zone is a normal dive. Zones need ≥ 8 m of length so a
dive is easy to start inside them.

### 4.6 Water obstacles and hits
| Class | Vertical extent | Avoid by | Contact |
|---|---|---|---|
| FloatingLog | −0.40…+0.40 m | dive, leap, steer | Minor "Bump" |
| LowBranch | bottom +0.50 m, up | dive, steer | Minor "Bump" |
| Snag | riverbed…+0.30 m | leap, steer | Minor "Bump" |
| Rock | full height | steer, dodge | Minor "Bump" + pushed to the near free side (spec 101 SideClip behaviour, never Crash) |
Forgiveness as spec 101 §2.6 (tops −0.10, bottoms +0.10, `x` shrink 0.10/side). Bump = −1 health, stumble (forward
×0.80, 0.8 s), 1.2 s i-frames. Health 0 → Dead (cause `Health`; Pista is swept downstream and fades). Shield absorbs a
Bump.

### 4.7 Swim validator rules (added to spec 102 §4.2)
- **W1** Action gaps ≥ phase `minActionGap`, measured at `vSwim` including forward current.
- **W2** No Crash-capable blocker and no Gap in a water volume.
- **W3** Every full-width water obstacle names its required answer (dive or leap); the Perfect bot passes with that answer only.
- **W4** No obstacle within 10 m after water entry or within 10 m before water exit.
- **W5** Current: `|cx|` ≤ 2.0 m/s and a 1.6 m free corridor reachable at `0.73·(swimVLatMax − |cx|)` (human margin as V3).

## 5. Vine swing

### 5.1 Player story
"I run off the edge and she just catches the vine. All I do is choose the moment to let go, and when I nail it she
flies higher and further and grabs the gold."

### 5.2 Grab (automatic, Blueprint V)
- Each vine: anchor `sA`, `xVine`, anchor height `hA` above the takeoff floor, length `L`, takeoff lip `sLip`.
- **Grab zone:** `s ∈ [sLip − 1.5, sLip + 1.0]`, `|x − xVine| ≤ 1.2 m`, feet within −1.0…+2.0 m of the takeoff floor,
  grounded or airborne, not dead. On the first tick inside the zone Pista grabs: commands queued or buffered before
  this tick are cleared; she blends onto the arc at `θ0` over `grabSnapTime`.
- Takeoff funnels are 2.4 m wide centred on the vine, so with `edgeMargin` 0.30 the grab **cannot be missed by
  steering** in the slice. Jumping into the zone also grabs. A grab can only be missed if the author breaks the funnel
  rule (validator V13).

### 5.3 Swing (scripted pendulum, no physics engine)
Swing phase `p` = ticks since grab / `swingTicks`, 0 → 1. Angle from vertical (positive = forward):
`θ(p) = θc − θa · cos(π · p)`, `θc = (θ0 + θEnd)/2`, `θa = (θEnd − θ0)/2`. Hand point:
`s = sA + L·sin θ`, `y = hA − L·cos θ`; feet = hands − `handToFeet`. Lateral target locked to `xVine`; steering input is
ignored (the lateral delta is discarded, not stored).

### 5.4 Release
- **Swipe up = release.** Release window opens at `p = releaseOpen`; a swipe before that is **held** and fires at
  `releaseOpen` (Good). No input → **auto-release** at `p = 1.0` (Good). A vine can never be "held too long".
- **Good release:** launch along the arc tangent at `releaseSpeed`. **Perfect release** (`p` within
  `[perfectStart, perfectEnd]`): launch at `releaseSpeed · perfectBoost`, event `PerfectRelease`.
- After release: airborne with spec 101 gravity (apex hang included), lateral control restored, forward speed = the
  launch's `s` component until landing, then blends to `v(d)` over `vineLandBlendTime`.
- **Rewards:** Perfect = `perfectReleaseCoins` credited instantly + gold trail + haptic; the higher, longer arc passes
  through the **perfect column** (coins, plus a crystal on V2), which Good arcs can't reach. Two Perfects in one span =
  **Perfect Span**, `perfectSpanBonusCoins` `[ASSUMED]`. Perfects count as clean traversal for mastery and DDA.
- Swipe down / flick during the swing: dropped with `InputDropped(Swing)`.

| Name | Value | Unit | Notes |
|---|---|---|---|
| `L` | 6.0 | m | Default per vine |
| `hA` | 8.0 | m | Above the takeoff floor |
| `handToFeet` | 1.90 | m | |
| `θ0` / `θEnd` | −35 / +55 | ° | `θc` 10°, `θa` 45° |
| `swingTicks` | 60 | ticks | 1.00 s, independent of run speed (predictable, like the jump arc) |
| `grabSnapTime` | 0.10 | s | |
| `releaseOpen` | 0.60 | p | θ 23.9° |
| `perfectStart` / `perfectEnd` | 0.75 / 0.90 | p | θ 41.8°–52.8°, ticks 45–54 inclusive = **10 ticks ≈ 167 ms** |
| `releaseSpeed` | 10.0 | m/s | |
| `perfectBoost` | 1.35 | × | 13.5 m/s |
| `vineLandBlendTime` | 0.30 | s | |
| `perfectReleaseCoins` / `perfectSpanBonusCoins` | 10 / 25 | coins | |
| Landing platform start | ≥ `sA` + 5.0 | m | Validator V13 |
| Swing corridor | 1.0 m clear around the arc and every release trajectory | m | Validator V13 |

**Reference trajectories** (landing floor = takeoff height, computed without apex hang; the validator uses the real sim):
| Release | `p` | Lands at | Apex (feet) |
|---|---|---|---|
| Good, earliest | 0.60 | `sA` + 5.5 | 0.9 m |
| Good, auto | 1.00 | `sA` + 8.8 | 3.7 m |
| Perfect | 0.75 / 0.825 / 0.90 | `sA` + 10.7 / 11.2 / 11.3 | 2.9 / 3.8 / 4.3 m |
At `s = sA + 9.0…10.0` every Perfect arc has feet between 2.0 and 3.7 m, while every Good arc is already on the ground.
The perfect column (coins at 2.6 / 3.4 / 4.2 m) sits there.

**Validator V13 (vines):** funnel ≤ 2.4 m wide centred on the vine; every Good and Perfect release (all ticks of the
window plus auto-release) lands on the landing platform with 0 hits at the chunk's speed extremes; no Good arc
collects the perfect column; every Perfect arc collects ≥ 1 column item.

### 5.5 Edge cases
| Situation | Result |
|---|---|
| Swipe up during the jump that carries her into the grab zone | Cleared at the grab (it never becomes an instant release) |
| Swipe up at `p` 0.30 | Held, releases at 0.60 (Good) |
| No input at all | Auto-release at 1.00, lands safely (Good) |
| Release from V1 when V2 follows | V1 landing branch is ≥ 26 m long, so V2 is always a fresh grab from running |
| Shield / i-frames during the swing | Unchanged; nothing to hit on the arc |
| Pause during the swing | Frozen; on resume the swing continues from the same tick |
| Death right after landing | Revive point = takeoff funnel 8 m before the lip; the swing starts over |

## 6. Canopy section (balance-beam branches)

- Beams are path segments 2.0–2.4 m wide with **soft edges** (steering never kills, spec 101). Danger comes from the
  **gaps between beams**: the next beam may be offset laterally by up to `maxBeamOffset`, so the player must line up
  before or during the jump. Landing beside the next beam = Fall.
- Over a gap, the lateral limit is the **envelope** of the current and next beam, so steering toward the next beam is
  never clamped.
- **Beam landing assist:** feet landing within `beamLandingAssist` laterally outside a beam edge snap onto the beam
  (x pulled in over 4 ticks). Combined with spec 101 ledge assist along `s`.
- Falling from the canopy: the camera stops following down, holds 0.8 s, fade (`canopyFallHoldTime`). Cause `Fall`.
- Camera: canopy profile modifiers (§11) show the drops on both sides.

| Name | Value | Unit |
|---|---|---|
| Beam width | 2.0–2.4 | m |
| Gap length | 3.0–3.5 | m (≤ V4) |
| `maxBeamOffset` | 1.0 | m (centre to centre) |
| `beamLandingAssist` | 0.25 | m |
| `canopyFallHoldTime` | 0.8 | s |
Validator **V14:** for each beam gap, the required lateral shift ≤ `8.0 m/s × (Δt − 0.10 s)` measured from the
previous required action; free corridor on beams ≥ 1.6 m.

## 7. Creature: the sailback (one MVP creature for the slice)

### 7.1 What it is
A small glider lizard (≤ 1 m sail span, four limbs, two eyes, matte skin with a translucent amber-to-teal dorsal sail;
art per ART_DIRECTION §5 bans and checklist C4). **Category: passive and helpful.** It never touches the path space
below `y` 2.5 m, never collides, never damages.

### 7.2 Behaviour (plain C# state machine, ticked by the simulation from Pista's `s`)
| State | Enter when | Behaviour |
|---|---|---|
| Perched | spawn | Basks with the sail half open; head tracks Pista once `Δs` (creature − Pista) < 30 m |
| Alert | `Δs` ≤ `alertDistance`, or Pista within `startleLateral` of it | Sail flares, chirp; 0.4 s |
| Launch | `Δs` ≤ `launchDistance` (or Alert finished after a startle) | Drops off the branch, sail opens |
| Glide | after Launch | Follows its authored spline at `vPista + glideSpeedBonus`, 2.5–12 m above the path; flock members offset 0.25 s |
| Gone | spline end | Lands at the spline end (Veil Falls curtain in C8→C9; a far tree otherwise) or leaves the view |
Spawns are deterministic (chunk data + script). In C8 the flock's spline descends ahead and toward the right; in C9 the
chunk's own sailback (perched on D2) launches when Pista is 25 m away and glides **into the water curtain** (§8.2).

### 7.3 Discovery trigger
- **Observed** = creature state ≠ Gone, `Δs` ∈ [2, 35] m, `|Δx|` ≤ 10 m (proxy for "on screen"; camera shows ≥ 35 m).
- **NEW DISCOVERY** fires when the cumulative observed time reaches `observeTime`. First time per profile: toast
  "NEW DISCOVERY · Unknown Species · Added to Journal" (creature names are revealed in the journal later), reward
  (§9.1), event `creature_found(first_time: true)`. Already discovered: no toast, sighting count +1.
- Discovery entries in the slice: **D-01** Falls Basin (Location, entering trigger), **D-02** Sailback (Creature),
  **D-03** Veil Grotto (Location, secret), **D-04** Sunken Arch (Location, Deep Breath; run 2+). Working labels.

| Name | Value | Unit |
|---|---|---|
| `alertDistance` | 22 | m |
| `launchDistance` | 14 | m |
| `startleLateral` | 2.0 | m |
| `glideSpeedBonus` | 2.0 | m/s |
| `observeTime` | 0.8 | s |
| Toast duration / queue gap | 2.0 / 0.5 | s |

## 8. The secret: Veil Grotto (behind Veil Falls)

### 8.1 Design goal
"I wouldn't have noticed that if I wasn't paying attention" (Part XV). Observation only, no ability, never
mandatory, missing it costs nothing.

### 8.2 Readable cues (each has shape/motion, not only colour)
1. **The sailbacks fly into the falls and don't come out** (C8 flock and C9's own sailback): the creature reveals the
   secret (Part XIV chain: creature → location).
2. **The curtain is thinner** over `x` +3.8…+5.8: the water sheet has gaps and a dark cavity shows through.
3. **Cyan glint** behind the thin part, pulsing slowly (every 1.6 s).
4. **Mist drifts outward**, against the spray direction of the rest of the fall.
5. **Veilmoss** on pillar D2 and **duskbells** along the right edge from 80–110 (violet = secret language from C3).
6. **Audio:** hollow drip-echo panned right from 40 m before D2.
Cue intensity × DDA multiplier 0.85–1.30 (glint brightness, mist amount; spec 102 §6.4).

### 8.3 Entering and payoff
Steer to the far right at D2's front (`s` 110). The fork rule decides: `x` > D2 centre (+3.2) → Secret. Running through
the curtain: splash burst, a 0.25 s droplet overlay on screen edges (never the centre), muffled audio, no damage. Inside:
2 crystals, coins, roosting sailbacks, D-03 toast + reward, then merge into Safe. Missed: nothing happens; the results
may point at it (§9.2, priority 5).

## 9. Rewards, power-up, results, upgrade

### 9.1 Pickup rules
| Pickup | Collect when | Value | Notes |
|---|---|---|---|
| Coin | The current state's hitbox, padded by `coinPad` (0.35 m in `x` and `s`, 0.25 m in `y`), overlaps the coin point | 1 | Any state: run, jump, slide, swim, dive (underwater coins need `Submerged`), swing |
| Crystal | Same with `crystalPad` 0.40 m | 1 | Never pulled by power-ups `[ASSUMED]`; own sting and haptic |
| Clean Line | Risky branch finished with no damage | +50% of that branch's collected coins | At the merge `s` |
| First-time discovery | Toast fires | +50 coins, +2 crystals | GDD §12 |
| Perfect Release / Perfect Span | §5.4 | +10 / +25 coins | |
Pickups are pooled; collected state is per run; nothing collected is lost on death. A pickup and a fatal hit on the same
tick: the pickup counts.

### 9.2 Results: "EXPEDITION COMPLETE"
Appears ≤ 1.0 s after the death fade. Count-ups ≤ 2.0 s total (any tap completes them).
- **Distance** (m) and **Best** (run 1: "Best 2,410 m · first expedition", no NEW RECORD fanfare; from run 2 the
  NEW RECORD banner when beaten) `[ASSUMED]`.
- **Coins** (with "+X Clean Line", "+X Perfect" lines), **Crystals**, **Discoveries** (new entries as cards with
  silhouette art and category counts, e.g. "Creatures 1/3 · Locations 2/4").
- **One highlighted next objective** (GDD §17 priority). In the slice the expected top result is priority 1:
  **"Deep Breath ready · 150/150 · Dive into the Sunken Arch"** with a thumbnail of the glinting passage from C6. If not
  affordable: "Deep Breath 110/150" (priority 3 threshold lowered to 50% for run 1) `[ASSUMED]`. Priority 5 in the slice:
  "Something glinted behind Veil Falls" if D-03 wasn't found.
- Buttons: **[RUN AGAIN]** (primary, bottom centre), objective card (opens the upgrade), Journal hidden in the slice.
- Revive is **not offered in Expedition 1** (the first death goes straight to results and the upgrade); from run 2 the
  GDD §11 revive applies `[ASSUMED]`.

### 9.3 Power-up for the slice: Shield `[ASSUMED]`
Chosen over Magnet because it creates a real decision before the gauntlet (jump for it or not) and reuses spec 101's
rule (AC-101-31). Pickup pad 0.50 m. Absorbs the next Minor, Bump or Crash (not Fall); max `shieldDuration` 30 s; bubble
VFX with a fading rim in the last 3 s; HUD icon with a timer ring. One per 600–900 m in endless runs (spec 102 §7).

### 9.4 First upgrade: Deep Breath `[ASSUMED]`
- **Opens:** underwater passages (`DeepDiveZone`). The player has already *seen* the Sunken Arch in C6 (glint,
  crystals in the dark, too deep for a normal dive). "Now I can reach that."
- **Cost:** 150 coins, 0 crystals. Deep Breath is ability #1 (150/0) and Trail Sense moves to #3 (1,200/4); decided
  2026-10-09 (option A, `DECISIONS.md`), GDD §13 updated `[ASSUMED]`.
- **Flow:** objective card → ability card (name, one line "Dive deep at shimmering water to reach sunken passages",
  3 s looping preview, cost) → **[LEARN]** (one tap) → 1.2 s unlock moment (sting, Success haptic) → back to results
  with RUN AGAIN highlighted. Death → running again with the ability: ≤ 20 s for a player who goes straight through.
- **How run 2 shows it:** (1) **Showcase rule** (§10.2): `R_Swim_Pool_01` is guaranteed among the first 5 director
  picks. (2) The DeepDiveZone gets an "ability-ready" cue: a ring of rising bubbles and a cyan surface shimmer, plus
  Pista's 1.0 s head-turn look-at 25 m before. (3) If the player swims past without diving, the shimmer pulses once
  more at 8 m (no glyph, no slow-time). (4) Inside: camera under water, blue light shafts, crystals, D-04 toast.

## 10. World Director in the slice

### 10.1 Expedition 1 (scripted)
- `ExpeditionScript` asset: the ordered list in §2 (chunk id, variant, enabled routes, cue intensity 1.00, fixed
  pickup layout incl. crystals and the Shield, fixed discovery assignments, slow-time help markers). Seed fixed
  (`expeditionSeed` = 1); streams exist but no random choice is used.
- The script **overrides** spec 102 §6.3 steps 1–3 (forced picks, weights, R1–R5) but **never the validator**:
  every scripted chunk/variant passes V1–V14 and W1–W5 at the speed it is met at ±0.5 m/s and at 0.80× (stumble).
- FTUE rules: slow-time help (GDD §16; done by the App layer stepping the simulation at 35% rate, so the simulation
  stays deterministic), health floor at 1 for the first 60 s, no gaps in the first 60 s, revive off, no mercy override
  inside the script (mercy may only insert a Recovery after C13).
- Vine slow-time help is different: on V1 only, time runs at 50% across the release window with a swipe-up glyph, and
  it never waits for input (auto-release still happens).

### 10.2 Endless continuation (after C13, and every later run)
- After C13 the next pick is **forced Recovery**, then spec 102 §6.3 runs normally (phase by distance, DDA `S`,
  abilities, cooldowns).
- **Phase 2 pool:** the 12 slice chunks plus `F_Recovery_Riverbank_01` (needed so the forced Recovery after C13 has a
  choice that passes R1, since the Meadow was C11), minus script-only variants (`Learn`, `Slice` variants are script-only; `B` and
  default variants are pooled). Small pool, so R1 stays "not within the last 6" and freshness does the rest.
- **Run 2+ opening:** `F_Start_RootGate_01` variant `Short` (no establishing shot, control from tick 0 of the start
  ramp), then the director.
- **Showcase rule (new, general):** in the first run after an ability unlock, the director forces one chunk containing
  a branch or zone that needs that ability within the first 5 picks (after the start chunk), choosing the earliest pick
  that passes V11. Applies once per ability.
- **Guaranteed early content (spec 102 §7):** runs 1–3 keep the creature and secret guarantees; Expedition 1 already
  satisfies both.

### 10.3 Data
`ExpeditionScript` (ScriptableObject), `ChunkDefinition` additions: `WaterVolumes`, `Currents`, `DeepDiveZones`,
`Vines`, `Beams` (landing assist flag), `CreatureSpawns`, `DiscoveryTriggers`, `SecretPassages`, `ScriptOnly` flag
per variant. Entry/exit types stay `Ground` (closed water and canopy sections, spec 102 §2.1).

## 11. Cues per beat (hooks; content comes later)
Hook ids are events the view layer subscribes to; audio content is Phase 3/4. Haptics: L = light impact,
M = medium, H = heavy, S = success notification. Max 1 haptic per 100 ms (GDD §19). Camera modifiers blend over 0.4 s
unless stated and respect Reduced Motion (spec 101 §5).

| Beat | Audio hook(s) | Haptic | Camera |
|---|---|---|---|
| 0 Establishing | `amb.forest`, `mus.theme.intro` | — | 4 s crane from the arch to behind Pista; any touch skips |
| 1 Steering | `sfx.coin` (pitch rises along a streak), `sfx.edgeBrush` | L at streak end | Base profile |
| 2 Obstacles | `sfx.jump`, `sfx.land`, `sfx.slide`, `sfx.dodge`, `sfx.hit.minor` | L landing ≥ 1 m, M hit | Spec 101 |
| 3 Fork | `amb.cue.risky` (left), `amb.cue.secret` (right, faint), `mus.layer.risky` +1 on the risky branch, `sfx.cleanLine` | L Clean Line | — |
| 4 Gap | `sfx.gap.whoosh` | — | — |
| 5 Ford | `sfx.step.shallow`, `amb.river`, `sfx.sailback.distant` | — | — |
| 6 Swim | `sfx.water.enter`, `sfx.swim.stroke`, `sfx.dive`, `sfx.surface`, `sfx.leap`, `sfx.bump.water`, `amb.rapids` | L enter, L leap splash-down, M bump | **Swim profile:** height −0.6 m, pitch −3°, back −0.5 m; lateral follow 0.75; slight bob ±0.05 m at 0.8 Hz (off with Reduced Motion). Dive: camera stays above water, surface ripple overlay |
| 6b Deep Breath | `sfx.deepDive`, `amb.underwater`, `mus.sting.discovery` | S on D-04 | Camera follows under water along the spline, FOV −4°, blue grade |
| 7 Vista | `amb.falls.roar` (swells), `mus.reveal.vista` | L on D-01 | **Vista beat:** FOV +4°, pitch up 3° for 2.0 s, then back. No slow-motion |
| 8 Vine | `sfx.vine.grab`, `sfx.vine.creak` (pitch follows θ), `sfx.vine.window` (soft tick at `perfectStart`), `sfx.release`, `sfx.perfect` | L grab, S Perfect | **Swing:** back +1.0 m, up +0.8 m, FOV +4°, vertical follow 40% of the arc; look-ahead +4 m on release |
| 8 Canopy | `amb.canopy.wind`, `sfx.branch.creak` | L landing on a beam | **Canopy:** height +0.4 m, lateral follow 0.85; fall: hold 0.8 s |
| 8 Creature | `sfx.sailback.chirp`, `sfx.sailback.sailFlare`, `mus.sting.discovery` | L on D-02 | No forced framing (discovery while running, Part XIII) |
| 9 Secret | `amb.secret.dripEcho` (panned), `sfx.curtain.pass`, `amb.grotto`, `sfx.crystal`, `mus.sting.secret` | L curtain, S on D-03, L per crystal | Droplet overlay 0.25 s at screen edges; slight FOV −3° inside |
| 11 Shield | `sfx.shield.pickup`, `sfx.shield.break` | L pickup, M break | — |
| 12–13 Gauntlet | `mus.layer.danger` +1, `sfx.hit.minor`, `sfx.crash`, `sfx.fall` | M hit, H crash (once) | Spec 101 shake rules only |
| Death | `mus.stop.soft`, `sfx.fail.*` | H on crash only | Fall: hold + fade; crash: 0.35 s shake then fade |
| Results | `ui.countUp`, `ui.newRecord`, `ui.objective` | L on objective reveal | — |
| Upgrade | `ui.learn`, `mus.sting.unlock` | S | — |

## 12. Feel and fun targets

### 12.1 Per-beat "feels good" metrics
| # | Beat | Metric | Target | How |
|---|---|---|---|---|
| F1 | All | Spec 101 M1–M7 still hold | as spec 101 | as spec 101 |
| F2 | Swim | Lateral onset / settle | ≥ 5 cm within 4 ticks; 2 m step within 0.10 m in ≤ 0.60 s | EditMode |
| F3 | Swim | Dive/leap fire on the swipe tick | 0 added ticks | EditMode |
| F4 | Swim | "Water feels different" | ≥ 4/5 testers say so unprompted or when asked to compare | Playtest |
| F5 | Vine | Grab never missed in a funnel | 100% (bots, all `x`) | Sim |
| F6 | Vine | Perfect rate | Average bot 25–45%, Novice 10–25%; testers: ≥ 3/5 get a Perfect by run 3 | Sim + playtest |
| F7 | Vine | Release latency | Release on the swipe's crossing tick | EditMode |
| F8 | Canopy | Beam falls are understood | 100% of canopy deaths answer "why did you fall?" correctly | Playtest |
| F9 | Creature | Discovery while running | Toast ≤ 1 tick after `observeTime`; no input blocked | EditMode + PlayMode |
| F10 | Secret | Found without help | 2–3 of 5 testers find Veil Grotto in runs 1–2 (not 0, not 5) | Playtest |
| F11 | Gauntlet | "Fair" | Every death names its obstacle and cause; ≥ 4/5 testers rate it "hard but fair" | Debug log + playtest |
| F12 | Loop | Death → running again | Results ≤ 1.0 s; RUN AGAIN → control ≤ 1.5 s; with the upgrade ≤ 20 s | PlayMode + stopwatch |
| F13 | Look | Keyframe match | Beats 1, 3, 5, 7 read as F1, F2, F3, F4/P1 (art-director sign-off) | Screenshot review |
| F14 | Perf | Frame pacing in the slice | 60 fps, no frame > 25 ms on the minimum device, every beat | performance-engineer |

### 12.2 Fun checklist (Blueprint Part LVI; the Phase 2 gate)
Five first-time testers, 15 minutes each, no coaching after "swipe and drag". Observers log quotes and actions.
| # | Signal | Pass |
|---|---|---|
| 1 | **"Let me try again"** | ≥ 4/5 tap RUN AGAIN within 10 s of results, unprompted, after run 1 and after run 2 |
| 2 | **"What happens if I take that route?"** | ≥ 3/5 take a different route at C3 or C9 in run 2 than in run 1, or say it aloud |
| 3 | **"I want to get there"** | ≥ 3/5 mention the Sunken Arch, the glint behind the falls or the upper vines without prompting |
| 4 | **"I almost made it"** | ≥ 3/5 die in the gauntlet past its midpoint or say it aloud; nobody calls a death unfair |
| 5 | Wonder | ≥ 3/5 react at the F4 vista (comment, slowdown in input, screenshot) |
| 6 | Mastery | Median hits per km falls from run 1 to run 3 |
| 7 | Upgrade pull | ≥ 4/5 learn Deep Breath before run 2, and ≥ 3/5 use it in run 2 |
| 8 | Owner | The owner plays the slice on the phone and says it is fun (Part LV) |
**Any fail on 1, 4 or 8 = no-go:** fix the gameplay, add no content (Part LV).

## 13. Simulation targets (balance-simulator; bots from spec 101 §6.2, plus a vine-release reaction model and a swim
current model)
Expedition 1, 1,000 runs per profile:
| Target | Perfect | Average | Novice |
|---|---|---|---|
| Reaches C12 | 100% | ≥ 85% | ≥ 60% |
| Clears C13 (2,820 m) | 100%, 0 hits | 40–60% | ≤ 25% |
| Mean hits in C6 (swim) | 0 | ≤ 1.0 | ≤ 1.8 |
| Can afford Deep Breath at results | 100% | ≥ 95% | ≥ 85% |
| Coins incl. discoveries, median | — | 300–450 | 200–320 |
- No single obstacle causes > 25% of Average deaths in Expedition 1; no obstacle before C12 causes > 5%.
- Deaths before C4 ≤ 2% (Average) and ≤ 5% (Novice).
- Runs 2–5 (director): Average median distance 1,200–2,500 m (spec 102 §11); Showcase chunk appears in run 2 in 100%.

## 14. Config ScriptableObjects (`Assets/_Game/Config/`)
| Asset | Fields |
|---|---|
| `Movement/SwimConfig` | §4.2, §4.3 tables, water obstacle forgiveness |
| `Movement/VineConfig` | §5.4 table |
| `Movement/CanopyConfig` | §6 table |
| `Creatures/SailbackConfig` | §7.3 table, spline speeds |
| `Discovery/DiscoveryEntry` ×4 | id, category, rarity, rewards, toast text |
| `Rewards/PickupConfig` | pads, values, Clean Line, Perfect rewards, first-discovery reward |
| `PowerUps/ShieldConfig` | duration, pad, i-frames (from spec 101) |
| `Progression/AbilityDefinition` (Deep Breath) | cost, `deepDiveDepth`, `deepDiveTime`, showcase flag |
| `World/ExpeditionScript` | §2 list, `expeditionSeed` |
| `UI/ResultsConfig` | timings, objective priorities and thresholds |
| `Camera/CameraModifiers` | swim, deep dive, vista, swing, canopy modifiers (§11) |

## 15. Acceptance criteria (EditMode unless stated)
Swimming
- **AC-103-01** Entering depth ≥ 0.9 m sets `Swimming`; depth < 0.6 m returns to `Running`; depth oscillating between 0.6 and 0.9 m causes no state flicker.
- **AC-103-02** Forward speed in water = 0.70 × `v(d)` + forward current, reached within 15 ticks of entry; back to `v(d)` within 18 ticks of exit.
- **AC-103-03** Swim lateral step +2.0 m from rest: ≥ 0.05 m after 4 ticks, within 0.10 m by 0.60 s, overshoot ≤ 0.03 m.
- **AC-103-04** A lateral current of +1.5 m/s with no input moves `xT` by 1.5 m per second; holding a drag of −1.5 m/s keeps `x` constant within 0.05 m.
- **AC-103-05** Dive: Down 6 ticks, Under 33 ticks at `y` −1.10, Up 12 ticks; `Submerged` is true exactly during Under.
- **AC-103-06** A FloatingLog full width is passed with 0 hits by a dive started 0.10–0.35 s before contact at 8.4 m/s, and by a leap started in the matching window; staying on the surface causes one Bump.
- **AC-103-07** A Snag is passed by a leap and hit by a dive; a LowBranch is passed by a dive and hit by a leap at apex.
- **AC-103-08** Leap apex 1.20 ±0.03 m, airtime 0.55 ±0.03 s, identical at every swim speed.
- **AC-103-09** Swipe up during Under rises at 2× and leaps on surfacing; under a FloatingLog it is held ≤ 21 ticks, then dropped with `InputDropped(Ceiling)`.
- **AC-103-10** Swipe down while leaping → `vy ≤ −14 m/s`, a full dive starts on splash-down.
- **AC-103-11** A buffered jump at water entry becomes a leap on the entry tick; a buffered slide or a fast-fall becomes a dive.
- **AC-103-12** No water contact ever produces `Crash` or `Fall`; Rock contact always produces Bump + push to the free side.
- **AC-103-13** Without Deep Breath, a dive inside a DeepDiveZone is a normal dive; with it, the dive follows the spline for 144 ticks, ignores lateral input and all obstacles, and surfaces at the exit point.
- **AC-103-14** Underwater coins are collected only while `Submerged`.
Vine
- **AC-103-15** Entering the grab zone grabs on that tick from running or airborne; commands queued or buffered before the grab are cleared.
- **AC-103-16** For every `x` reachable on a 2.4 m funnel, the runner grabs the vine (0 misses).
- **AC-103-17** `θ(p)` matches the formula within 0.1° at p = 0, 0.25, 0.5, 0.75, 1.0; the swing lasts exactly 60 ticks.
- **AC-103-18** Lateral input during the swing changes neither `x` nor `xT`, and the discarded delta is not applied after release.
- **AC-103-19** Swipe up before `p` 0.60 releases at tick 36 (Good); swipe up at ticks 45–54 → Perfect; at tick 44 or 55 → Good; no input → release at tick 60 (Good).
- **AC-103-20** Perfect launch speed = 13.5 m/s, Good = 10.0 m/s, along the arc tangent at the release tick.
- **AC-103-21** On C8's layout, every Good and Perfect release tick lands on the landing branch with 0 hits at 11 and 14 m/s entry speed; no Good arc collects a perfect-column item; every Perfect arc collects ≥ 1.
- **AC-103-22** Perfect credits 10 coins and emits `PerfectRelease`; two Perfects on V1 and V2 add 25 coins (Perfect Span).
- **AC-103-23** Swipe down or flick during the swing → `InputDropped(Swing)`, swing unchanged.
- **AC-103-24** Forward speed after landing blends from the launch `s` speed to `v(d)` within 18 ticks.
Canopy
- **AC-103-25** Over a beam gap, `xLim` is the envelope of both beams; landing with feet ≤ 0.25 m outside the next beam's edge snaps onto it; > 0.25 m → Fall (cause `Fall`).
- **AC-103-26** Beam edges never damage (soft-edge rules of spec 101 apply with the beam's width).
Creature and discovery
- **AC-103-27** Sailback states follow §7.2 for scripted `Δs` sequences; Pista within 2.0 m laterally of a perched sailback triggers Alert immediately.
- **AC-103-28** NEW DISCOVERY fires on the tick the cumulative observed time reaches 48 ticks, once per profile; a second encounter adds a sighting and no toast.
- **AC-103-29** A sailback never enters `y` < 2.5 m above the path and never produces a contact.
- **AC-103-30** Discovery rewards (+50 coins, +2 crystals, journal flag) apply once per entry per profile and persist after the run.
- **AC-103-31** (PlayMode) Toasts never block input; a second discovery within 2.0 s queues and shows 0.5 s after the first ends.
Secret
- **AC-103-32** In C9, `x` > +3.2 at `s` 110 selects the Secret branch; the curtain causes no contact; the grotto contains no gap or Crash-capable blocker.
- **AC-103-33** D-03 triggers at `s` 140 of the secret branch; the two grotto crystals are collectible by a runner on the branch centre line.
- **AC-103-34** Skipping the secret produces no damage, no dead end and no penalty.
Pickups, Shield, rewards
- **AC-103-35** Coin and crystal pads match §9.1 in every state (run, jump, slide, swim, dive, swing); a pickup and a fatal hit on the same tick both count.
- **AC-103-36** Clean Line adds 50% of the branch's collected coins at the merge only if no health was lost on the branch (Shield absorption counts as no loss).
- **AC-103-37** The C11 Shield is unreachable by a running body and reachable by a jump started within ±0.15 s of the ideal moment.
- **AC-103-38** Shield absorbs Minor, Bump and Crash, not Fall; expires after 1,800 ticks.
Script and director
- **AC-103-39** Expedition 1 produces the exact chunk/variant sequence of §2 and the authored pickups on every device and run.
- **AC-103-40** Every scripted chunk passes V1–V14 and W1–W5 at its met speed ±0.5 m/s and at 0.80× (offline validator, CI).
- **AC-103-41** In Expedition 1: health floors at 1 during the first 3,600 ticks; no gap exists before 60 s at the scripted speed; revive is not offered.
- **AC-103-42** The pick after C13 is a Recovery chunk; later picks follow spec 102 §6.3 with the Phase 2 pool and never pick a `ScriptOnly` variant.
- **AC-103-43** In the first run after unlocking an ability, a chunk needing it appears within the first 5 picks after the start chunk (1,000 seeds: 100%); never again by force.
- **AC-103-44** Same profile + seed + inputs → identical run (chunks, pickups, creature states, discoveries) when frames are split into 1–5 ticks.
Results and upgrade
- **AC-103-45** (PlayMode) Results appear ≤ 1.0 s after the death fade; RUN AGAIN → control ≤ 1.5 s.
- **AC-103-46** The highlighted objective follows GDD §17 priorities; with ≥ 150 coins and Deep Breath unowned it is "Deep Breath ready"; with 75–149 it is the progress line; the slice's priority-5 line appears only if D-03 is unfound and priorities 1–4 don't match.
- **AC-103-47** LEARN deducts 150 coins once, sets the ability, persists across app restarts, and the button is disabled below the cost.
- **AC-103-48** Run 1 shows no NEW RECORD banner; run 2+ shows it only when the best distance is beaten.
Performance and allocation
- **AC-103-49** (PlayMode) Swim, vine, canopy, creature and pickup updates allocate 0 bytes per tick after warm-up.
- **AC-103-50** (PlayMode) The analytics events `traversal_result` (types swim_dive, swim_leap, vine_good, vine_perfect, beam_gap), `creature_found`, `secret_found`, `power_up`, `ability_unlocked` fire with the GDD §22 properties.

## 16. Edge cases (summary)
| Situation | Result |
|---|---|
| Route split reached mid-jump or mid-slide | Branch by `x` at the divider front; state unchanged (spec 102 §3.3) |
| Swipe up while the jump carries her into a vine zone | Grab clears it; no instant release |
| Water entry mid-slide | Slide ends; swim starts on the entry tick |
| Water exit mid-dive | Up phase completes, then running resumes (W4 keeps obstacles 10 m away) |
| Hit while health 1 in water | Dead (cause `Health`), swept downstream, fade |
| Death during a toast | Toast finishes on the results screen's discovery list instead |
| Creature observed during the death fade | Not counted (observation stops at death) |
| Revive placement (run 2+) | Swim: last surface position ≥ 6 m before the hazard; vine: takeoff funnel 8 m before the lip; canopy: last beam point ≥ 6 m before the gap |
| Pause in swing, dive or deep dive | Frozen; resumes on the same tick after the 1.0 s ready beat |
| Orientation change mid-slice | Camera profile blend 0.4 s incl. active modifiers; simulation untouched |
| Missed Shield | Nothing; Shield slot not refilled in the script |

## 17. Assumptions `[ASSUMED]`
- Slice = Expedition 1 (first run), compressing Blueprint 4.2's 10-minute timeline to ~4 minutes; GDD §16 follows after the gate.
- New chunk `F_Start_RootGate_01` (MVP set 15); script-only variants `Learn`/`Slice`; Start variant `Short` from run 2.
- No drowning, no Crash/Fall in water; no breath meter; shallow water doesn't change speed.
- Swim numbers (0.70× speed, tau 0.14, dive 0.85 s, leap apex 1.20 m); current drifts the lateral target.
- Vine: automatic grab, funnels make misses impossible, fixed 1.00 s swing, 150 ms perfect window, held early release, safe auto-release, perfect column reward.
- Canopy: soft beam edges, 0.25 m beam landing assist.
- Sailback is the slice creature and reveals the secret; "Unknown Species" toast text for creatures.
- Secret = Veil Grotto behind Veil Falls (observation only).
- Shield is the slice power-up (Magnet and Explorer Vision in Phase 3); crystals never magnetized.
- **First ability = Deep Breath at 150 coins** (reorders GDD §13: Trail Sense moves to #3).
- No revive in Expedition 1; no NEW RECORD fanfare on run 1; results objective threshold 50% for run 1.
- Showcase rule for newly unlocked abilities.
- First-gap and vine-release slow-time help.
