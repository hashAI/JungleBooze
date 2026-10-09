# Spec 104: MVP Content (Phase 3): Verdant Forest, creatures, discoveries, economy, abilities, power-ups

**Owner:** game-designer | **Implementers:** gameplay-engineer (abilities, power-ups, creatures, discovery, director
rules, validator rules V15–V18), ui-engineer (journal, abilities/upgrades screen, results objectives, toasts), level
authors (chunk layouts §3), art-director + asset-pipeline (creature briefs §5.4, specimen plants, locations),
balance-simulator (§13), audio/VFX (hooks §12), qa-engineer (acceptance §15)
**Status:** v1, ready for implementation | **Last updated:** 2026-10-09
**Sources:** `BLUEPRINT.md` Parts VII–X, XIII–XIX, XXVI–XXVIII, XXXI–XXXII, **LIII (MVP list)**, **LIV (order)**,
LX–LXV; `GDD.md`; specs 101 (movement), 102 (chunks, director, validator), 103 (slice: swim, vine, canopy, sailback,
Shield, Deep Breath, Expedition 1); `ART_DIRECTION.md` §5 (bans, world language), `ART_DIRECTION_PAINTERLY.md`;
`DECISIONS.md` (painterly final; Deep Breath first; no-ask mandate).
**Builds on:** the gray-box vertical slice (spec 103). **Expedition 1 does not change** (script, `Slice`/`Learn`
variants, rewards). Everything here applies from run 2 unless stated.
**Not in scope (Phase 4+):** missions, daily expeditions, weekly events, environmental events, climbing, gliding,
camp, map, cosmetics, leaderboards, ghosts, ads, purchases, cloud save, other biomes.

Units: m, s, m/s, pt. 60 ticks/s. Path space `(s, x, y)` (spec 101 §2.1). Notation for layouts: spec 103 §3.0. All
numbers are starting values in ScriptableObjects (§14). `[ASSUMED]` = designer default awaiting owner review (owner
mandate 2026-10-09: no questions, recommended option, logged).

---

## 1. Player story and purpose

"Every run the forest is a little different. I know the river leads to the falls and the falls to the canopy, and
I know where the secrets *might* be. Today I finally caught the high vine with a perfect release and ran along the
top of the canopy. Tomorrow I'll have enough for Root Vault and I'll get onto that sunny ledge in the ravine. And
there's still a creature I've never seen."

Phase 3 turns the slice into a game people return to daily (Blueprint LIII, LX–LXV). Every addition serves a
pillar: **Movement** (vault, air-grab, double jump, shoulder charge: new verbs that open places), **Navigation**
(safe/risky/secret in every branch chunk), **Discovery** (15 journal entries, 3 behaving creatures, 7 secrets),
**Progression** (7 abilities, each opening a place the player has already *seen*). No feature is a stat bump.

### 1.1 USP first: "an endless exploration adventure disguised as a runner"
The loop this spec exists to deliver is **curiosity → discovery → new possibility** (Parts LXI, LXV). Running is how
you travel through the forest, not the point of the game. Every system below is judged by how it serves this loop. The
USP checklist (§16) holds the measurable targets.
| USP promise | How MVP delivers it | Where |
|---|---|---|
| **"I want to get there"**: every run shows places you can see but can't reach yet | Each of the 7 abilities has a *visible* locked place (underwater arch, high vines, sealed crack, sunny ledge, humming wall, crown nest, claw marks high on the trunks). **Locked-place guarantee:** the director shows ≥ 1 locked place by 1,500 m and ≥ 2 by 3,000 m while any ability is unowned | §4.2, §7.1 |
| **Secrets reward observation**, not icons | 2 observation-only secrets read purely from the world (creature behaviour, curtain thinness, glints, mist drift, flower language, sound). No map pins, no HUD markers, no arrows; Trail Sense, Creature Tracking and Explorer Vision only *strengthen world cues*. **Secret cadence:** ≥ 1 enterable secret opportunity per 1,500 m | §4.2, §6.4, §7.5, §9 |
| **Something always feels missing** | Journal with silhouettes, hints, rarity tiers (common / uncommon / rare), "Mysteries 2/?" and the "???" teaser | §6, §11.3 |
| **The next goal is always visible** | Results always show exactly one reachable next objective (P1–P6; P6 always matches) | §11.1 |
| **Route choice matters** | Safe / Risky / Secret have distinct, numbered risk and reward (§2.1) and read from the world | §2.1, §3 |
| **Abilities are new places, never stats** | All 7 abilities open a named place and a journal entry; the only stat-like items are optional power-up *durations* (§8.3), which never gate content | §7, §8.3 |

## 2. MVP content at a glance

| Item | MVP amount | Where |
|---|---|---|
| Biome | Verdant Forest: forest, river, waterfall, small canopy | §3 |
| Chunks | **15** (12 from the slice + 3 new layouts), 24 variants | §3 |
| Branch chunks with safe/risky/secret | 4 (StiltRoots, Ravine, Waterfall, Canopy) + Grotto (safe/secret/nested secret) + Swim Pool (safe/secret) | §3.1 |
| Secrets | 7 (2 observation-only, 5 ability-gated) | §6.4 |
| Creatures | 3: sailback, ripplekit, veilhanger | §5 |
| Discoveries | **15** (3 creatures, 4 plants, 6 locations, 2 mysteries) + "?" teaser | §6 |
| Currencies | Coins + Crystals | §8 |
| Abilities | 7 "new possibility" unlocks | §7 |
| Coin upgrades | 3 (power-up duration, 4 purchasable levels each) | §8.3 |
| Power-ups | 3: Magnet, Shield, Explorer Vision | §9 |
| Dynamic difficulty | basic (spec 102 §6.4 + §10 here) | §10 |
| Endless mode | director rules after the slice | §4 |
| Results objective, journal UI | §11 | |

Changes to earlier documents (applied by their owners; this spec wins on conflict):
- **Discovery set** becomes 15: Plants 4 (whirlseed stays ambient only), Locations 6 (Stiltwood Gate dropped; Sunspan
  Highline, Sunshelf, Crown Nest added), Mysteries 2 + "?" (GDD §15; `ResultsConfig` totals 3/4/6/2).
- **Ravine** routes: Safe / Risky (root bridge) / **Secret = Root Vault** (was "Risky high ledge: Root Vault",
  spec 102 §9). **Grotto**: Safe / Secret (observation) / nested Secret (Shoulder Charge). **Canopy** `Default`:
  Safe / Risky (Vine Grip high line) / Secret (Double Jump crown); no separate `Crown` variant.
- **Crystal flow** spacing 1,200–1,800 m (was ~1,000 m, min 600; spec 102 §7) `[ASSUMED]` (§8.2).
- Ability **prerequisites** added (§7.1); costs unchanged from GDD §13.

### 2.1 Route risk and reward (why the choice matters)
| | Safe | Risky | Secret |
|---|---|---|---|
| Difficulty | chunk rating −2 (min 1); ≥ 3.5 m wide; ≤ 2 required actions per branch | rating +2; 2.0–2.9 m wide; gaps, combos | ≈ chunk rating; **no gaps, no Crash blockers** (V10); needs observation or an ability |
| Coins | ×1.0 | ×2.2 + Clean Line (+50% of the branch's coins if undamaged) | ×1.0–1.3 |
| Crystals | 0 | 20% chance of 1 (+1 in the Canopy perfect column) | 1–3 guaranteed, every visit |
| Discovery | — | bellcap specimen (D-07) | guaranteed slot: location/mystery entry, veilmoss/duskbell specimens |
| Target risk (Average bot) | death ≤ 2% per branch, ≤ 0.15 hits | death 6–15% per branch, 0.4–0.9 hits | death ≤ 2%, ≤ 0.2 hits |
| Read from the world | emerald, wide, flat, even light | gold bellcaps, sun shafts, coin trails, narrow/raised/broken ground | violet/cyan: veilmoss, duskbells, mineral glint, mist drifting outward |
Per-branch values from the layouts (coins collected by an Average bot ≈ 75% of placed):
| Chunk | Safe | Risky (with Clean Line) | Secret |
|---|---|---|---|
| StiltRoots | ~25 coins | ~55 (+27) coins, 20% crystal | ~29 coins + 2 crystals (Trail Sense) |
| Ravine | ~22 coins | ~50 (+25) coins, 20% crystal | ~35 coins + 2 crystals (Root Vault) |
| Waterfall | ~30 coins | ~66 (+33) coins, crystal | ~18 coins + 2 crystals (observation) |
| Canopy | ~30 coins | ~40 (+20) coins + 1 crystal (Vine Grip) | ~13 coins + 3 crystals (Double Jump) |
Rule: Risky pays 2.0–3.0× Safe coins in expectation; a Secret pays crystals (the rare resource) plus discovery, so
the three routes reward different player goals (safety, coins, crystals and the journal).

## 3. Chunks: the 15-chunk Verdant Forest set

### 3.1 Catalog and variants (pool = the director may pick it; script = Expedition 1 only)
| # | Id | Category | Env | Variants (rating) | Routes (ability) | Layout |
|---|---|---|---|---|---|---|
| 0 | F_Start_RootGate_01 | Straight (opener) | Forest | `Full` script (1), `Short` opener run 2+ (1) | 1 | 103 §3.1 |
| 1 | F_Straight_Glade_01 | Straight | Forest | `Slice` script (3), `A` pool (3) | 1 | 103 §3.4, asset |
| 2 | F_Straight_Roots_01 | Straight | Forest | `Learn` script (2), `B` pool (3) | 1 | 103 §3.2, §3.10 |
| 3 | F_Recovery_Meadow_01 | Recovery | Forest | `Default` (1) | 1 | 103 §3.11 |
| 4 | F_Recovery_Riverbank_01 | Recovery | River | `Default` (1) **new** | 1 | §3.2 |
| 5 | F_Branch_StiltRoots_01 | Branch | Forest | `Slice` script (3), `Default` pool (4) **new delta** | Safe / Risky / Secret (Trail Sense) | §3.5 |
| 6 | F_Branch_Ravine_01 | Branch | Forest | `Default` (5) **new**, `H` (7) **new** | Safe / Risky / Secret (Root Vault) | §3.3 |
| 7 | F_Challenge_FallenGiants_01 | Challenge | Forest | `Default` pool = slice layout (6), `H` (8) **new** | 1 | 103 §3.12, §3.8 |
| 8 | F_Challenge_ThornRun_01 | Challenge | Forest | `Default` pool = slice layout (6), `H` (9) **new** | 1 | 103 §3.13, §3.9 |
| 9 | R_Transition_Ford_01 | Transition | River | `Default` (2) + ripplekit slot | 1 | 103 §3.5, §5.2 |
| 10 | R_Swim_Pool_01 | Straight (swim) | River | `Default` (4) + ripplekit slot | Safe / Secret (Deep Breath) | 103 §3.6, §5.2 |
| 11 | R_Branch_Waterfall_01 | Branch | Waterfall | `Slice` script (4), `Default` pool (5) | Safe / Risky / Secret (observation) | 103 §3.9 (pool: director crystals, specimens §6.3) |
| 12 | C_Canopy_VineSpan_01 | Branch (set piece) | Canopy | `Slice` script (4), `Default` pool (5) **new delta** | Safe / Risky (Vine Grip) / Secret (Double Jump) | §3.6 |
| 13 | D_Discovery_Grotto_01 | Discovery | Forest | `Default` (3) **new** | Safe / Secret (observation) / nested Secret (Shoulder Charge) | §3.4 |
| 14 | D_Discovery_Overlook_01 | Discovery | Waterfall | `Default` (1) + sailback flock slot | 1 | 103 §3.7 |

`H` variants have `PhaseRange` Danger–Mastery and exist so the 6–10 rating bands are not starved (§4.4). Pool
`Default` variants of slice chunks keep the slice geometry; the director (not the script) places crystals, power-ups
and discovery assignments (spec 102 §7). All layouts obey spec 102 §2.1 seams, V1–V14, W1–W5 and the new V15–V18 (§3.9).

### 3.2 N1 `F_Recovery_Riverbank_01` · Recovery · rating 1 · River · 120 m · routes: 1 · PhaseRange Learning–Mastery
A sunny bank path along the river (river on the left, ribbon reeds in the shallows), slower music layer, no required
action. The second Recovery chunk (spec 102 §6.3 step 5).
| `s` | Content |
|---|---|
| 0–14 | Path leaves the forest; river opens on the left beyond `x` −3.5 (soft edge with a water-splash `EdgeBrush`) |
| 10–110 | Coins Weave(10–110, 2.0, 40) ≈ 22 |
| 32 | **Specimen slot SP-RB1: ribbon reed (D-08)** at `x` −4.4 in the shallows; reeds bow away as she passes |
| 45–100 | **Creature slot CS-RB1: ripplekit** in the shallows `x` −5.0…−6.5 (§5.2), pacing her along the bank |
| 60 | **Power-up slot PU-RB1** at `x` 0, `y` 0.9 (running height; no jump needed) |
| 106–120 | Path bends right, away from the river; 7.0 m at the seam |
Coins ≈ 22. Zero obstacles. Audio `amb.river`, `mus.layer` −1.

### 3.3 N2 `F_Branch_Ravine_01` · Branch · rating 5 (Safe 3 / Risky 7 / Secret 4) · Forest · 240 m · PhaseRange Rhythm–Mastery
A mossy rootstone ravine with a stream at the bottom. The rim path splits: Safe drops into the ravine, Risky crosses a
broken natural root bridge, Secret (Root Vault) climbs onto a sunlit shelf on the right wall.
| `s` | Content |
|---|---|
| 6–20 | Rim path 7.0 m; ravine and mist ahead-left, root bridge silhouette ahead, sunlit shelf on the right wall. Coins Line(8–40, 0, 4.0): 9 |
| 20–50 | Widen to 9.0 m (`x` −4.5…+4.5). Carrier cues from 0: moss/ferns and even light left; bellcaps, gold pollen and a sun shaft centre; on the right, a 1.4 m rootstone step with veilmoss, duskbells and a cyan glint on the shelf lip (reads "too high to step") |
| 70–190 | **Divider D1** (Safe \| Risky): rootstone rib `x` −1.8…−0.6, front 70 (visible from `s` ≤ 30 = 2.5 s at 16 m/s) |
| **Right edge, locked** (no Root Vault) | Right edge narrows from +4.5 to +2.6 over 62–81 (0.10 m/m) and stays +2.6 to the bridge; the step face is scenery beyond the soft edge (V12) |
| **Right edge, open** (Root Vault) | Right edge widens from +4.5 to +6.0 over 66–81 (0.10 m/m). **Divider D2**: rootstone pillar `x` +2.6…+3.8, front 82 (visible from 42). Secret entrance `x` +3.8…+6.0 |
| Left = **Safe** | `x` −4.5…−1.8 at 70, widening outward to `x` −5.8…−1.8 (4.0 m) by 83. Ramp down −3.0 m over 75–100 (12%). Stream crossing 118–130 (`Surface = ShallowWater`). **Low 0.5, `x` −5.8…−3.8** at 125 (rock: steer right or jump). **Blk 1.4 @−4.6** at 150 (corridor `x` −3.9…−1.8, 2.1 m). Ramp up +3.0 m over 165–185 (15%). **Specimen SP-RV1: ribbon reed** at 128, `x` −6.4. Coins Line(80–175, −3.0, 4.5) ≈ 22 |
| Centre = **Risky** | Root bridge, level with the rim, the ravine as the drop below. `x` −0.6…+2.6 (3.2 m) narrowing to `x` −0.2…+2.2 (2.4 m) by 92 (0.10 m/m). Soft bridge edges (beam rules, spec 103 §6). **Gap 3.0 full** 100–103 · **High 1.0 full** 118 · **Low 0.6 full** 132 · **Gap 3.5 full** 146–149.5 · **Thorns `x` −0.2…+1.0** 160–163 (steer right or jump) · bridge lands on the far rim at 172. Crystal chance 20% (director, on the 132 arc, `y` 1.6). **Specimen SP-RV2: bellcap (D-07)** at 110, `x` +2.5. Coins ×2.2 Line(92–170, +1.0, 1.5) ≈ 50. Clean Line at merge |
| Right = **Secret** (Root Vault) | **Vault zone** 84–91.2 over the full secret width; step face at 92, height 1.4 m (§7.4). **Sunshelf** top `y` +1.4 from 92 to 168, `x` +3.8…+6.0 (2.2 m), no gaps, no blockers (V10). **2 crystals** at 115 and 150 (`x` +4.9, `y` 0.9 above the shelf). **D-12 Sunshelf** trigger at 130 (vista down the ravine; sailbacks glide below). Duskbells along the wall. Coins Line(96–165, +4.9, 2.0) ≈ 35. Shelf ramps down −1.4 m over 166–182 (9%) |
| 182–200 | All routes merge on the far rim; D1 ends 190, D2 ends 165 |
| 200–228 | 9.0 → 7.0 m (0.05 m/m). Coins Line(195–228, 0, 3.0): 11 |
Action spacing on Risky at 16 m/s: 100 → 118 (jump→slide) 1.13 s · 118 → 132 0.88 s · 132 → 146 jump→jump with 5.9 m
clear floor before the gap (≥ 0.35 s ✓). Safe: 125 → 150, 1.56 s. Coins ≈ 60 (Safe) / 90 (Risky) / 75 (Secret).

**Variant `H`** (rating 7; Danger–Mastery) = `Default` with: Risky adds **Gap 3.0 full** 123–126 and moves the Low to
136 (jump→jump 10 m, clear floor 5.6 m ✓ at 16 m/s); Safe adds **Blk 1.4 @−2.6** at 106 (corridor `x` −5.8…−3.4);
Secret unchanged. Coins unchanged.

### 3.4 N3 `D_Discovery_Grotto_01` · Discovery · rating 3 (Safe 2 / Secret 3 / Hollow 3) · Forest · 200 m · PhaseRange Rhythm–Danger
The path runs through a hollow under a giant stiltwood: root walls, light shafts through holes, veilmoss curtains,
mineral glints. Calm wonder with light obstacles. The observation secret is revealed by **environmental behaviour**:
whirlseeds blow *out* of a curtain on a cool draft.
| `s` | Content |
|---|---|
| 6–30 | Path enters the root cavern mouth; light dims to shafts. Coins Line(8–38, 0, 4.0): 8 |
| 40 | **Low 0.5 full** (root); Arc(40, 0, 3) |
| 55–80 | Left wall: a **veilmoss curtain** `x` −3.5…−5.5 swaying; **whirlseeds drift out** of it into the path on a draft, mist flows out, drip-echo panned left from 40 m (cues ×DDA intensity) |
| 60–70 | **Creature slot CS-GR1** on the cavern ceiling, `y` +7 m, `x` −2…+2: veilhanger (Creature Tracking only, §5.3) |
| 60–80 | Left edge widens from −3.5 to −5.5 (0.10 m/m) |
| 70 | **High 1.0, `x` −1.0…+3.5** (hanging root: slide, or steer left toward the curtain) |
| 80–150 | **Divider D1**: root column `x` −3.0…−1.8, front 80 (visible from 40) |
| Right = **Safe** | `x` −1.8…+3.5 (5.3 m). **Blk 1.4 @+1.5** at 105 · **Low 0.6 full** at 128 · **High 1.0 full** at 145. Coins Line(85–148, +0.5, 3.0) ≈ 22 + Arc(128) |
| Left = **Secret: Root Gallery** (observation) | Entrance `x` −5.5…−3.0 (2.5 m) through the curtain at 84–87 (no collision; curtain parts, `sfx.curtain.moss`). Dim passage with cyan mineral veins. **Specimen SP-GR1: veilmoss (D-09)** at 85, `x` −4.5 (passing through triggers it). **Specimen SP-GR2: duskbell (D-10)** at 100, `x` −4.8. **1 crystal** at 106 (`x` −4.2). Coins Line(90–148, −4.2, 2.0) ≈ 25 (until 112, then on the right part). No gaps, no blockers |
| 96–112 | Gallery widens left from −5.5 to −7.0 (0.094 m/m) |
| 112–150 | **BrittleWall BW1** `x` −7.0…−5.0, front 112, depth 1.0 (cracked root wall, warm light and a low hum leaking through). Gallery continues at `x` −5.0…−3.0 |
| Behind BW1 = **Secret: Singing Hollow** (Shoulder Charge, nested) | `x` −7.0…−5.0 from 113 to 150. Rootstone chamber whose walls resonate (`amb.hollow.hum`), mineral glints, faded old-expedition map marks scratched on a wall (clue to D-14). **2 crystals** at 125 and 140 (`x` −6.0). **D-15 Singing Hollow** trigger at 130. Coins Line(116–148, −6.0, 2.0) ≈ 17. Merges into the Gallery over 150–158 |
| 150–170 | Gallery merges into Safe; D1 ends 150; left edge back to −3.5 by 170 (0.10 m/m) |
| 170–194 | Exit to daylight; coins Line(172–192, 0, 4.0): 6 |
BW1 rules: §7.6. Without Shoulder Charge BW1 is a divider (nudge, never Crash) and the chamber is closed (V12).
Locked teaser visible from Safe too: BW1's warm light and hum leak through a slit in D1 at 110–125, so players who
never find the gallery still see "something behind the roots". Safe
spacing at 16 m/s: 105 → 128, 1.44 s; 128 → 145 (jump→slide) 1.06 s. Grotto counts toward discovery pacing, not the
fork cadence (§4.2).

### 3.5 `F_Branch_StiltRoots_01` variant `Default` (pool) · rating 4 · = `Slice` (103 §3.3) plus:
- Risky crystal: director chance 20% (replaces the scripted crystal); **Specimen SP-SR1: bellcap (D-07)** at 110 on
  the ridge edge, `x` −4.2.
- **Creature slot CS-SR1** (veilhanger, CT only): hanging in the stilt-root fans, `s` 30–50, `x` −6.0, `y` +5 m.
- **Secret: Old Marker Hollow (Trail Sense).** Without Trail Sense: the slice's locked teaser (veilmoss over a sealed
  crack) and Safe's right edge narrows from +4.5 to +3.4 over 87–98 (0.10 m/m), fusing with D2's rock. With Trail
  Sense: right edge widens from +4.5 to +6.6 over 77–98 (0.10 m/m); **Divider D2** stiltwood root `x` +3.4…+4.6, front
  98 (visible from 58); Secret entrance `x` +4.6…+6.6 (2.0 m) through a parting veilmoss screen at 100 (**Specimen
  SP-SR2: veilmoss (D-09)** at 100, `x` +5.0). Fissure 100–160: **D-14 Old Marker** trigger at 130 (a weathered
  triangulation cairn with an X-marked tin plate, compass rose motif); **2 crystals** at 120 and 145 (`x` +5.6); coins
  Line(102–158, +5.6, 2.0) ≈ 29. No gaps, no blockers. Merges into Safe over 160–170; D2 ends 160.
- Safe's **Low 0.4** at 115 becomes `x` +0.5…+3.4 (D2 occupies the rest). Safe width with D2 ≥ 3.8 m ✓.

### 3.6 `C_Canopy_VineSpan_01` variant `Default` (pool) · rating 5 · = `Slice` (103 §3.8) plus:
**(a) Beam A knot.** Beam A widens on the left to `x` −2.8 at 148 and narrows back to its slice edge −1.2 by 164
(0.10 m/m); its right edge stays +1.2. Purpose: a player who steers left after V2 and misses the high vine lands
safely. **High 1.0** at 160 spans Beam A's full width at that `s`.

**(b) Risky: Sunspan Highline (Vine Grip).** Starting values; final geometry must pass V15 (§3.9):
| Element | Starting value |
|---|---|
| High vine **H1** | anchor `sA` 149.5, `x` −1.8, anchor height 11.5 m above Beam A, `L` 6.0 m; gold-lit, hanging in plain view from `s` 100 |
| H1 catch | **Air grab** (§7.3): catch point = H1's hand position at `p` 0.5 (`θ` 10°): `s` 150.5, hands `y` 5.6 (feet 3.7); catch zone ±1.2 m in `s`, ±1.0 m in hands `y`, `|x − xH1|` ≤ 1.0 |
| How to reach it | Release V2 late (Perfect window or later) and steer left on release. Early Good releases land before the zone |
| Upper limb **U** | `s` 156–240, top `y` +3.5 above Beam A, 2.4 m wide, `x` −4.0…−1.6; underside ≥ 2.9 m above anything walkable beneath (jump clearance 2.71 + margin) |
| H1 perfect column | `sH1` + 9.0…10.0 above U: 2 coins + **1 crystal** at `y` 3.4 |
| U content | **Gap 3.0** 182–185 · **Low 0.5 full** 205 · **D-11 Sunspan Highline** trigger at 195 (U breaks through the canopy roof: open sky, rootstone range, a sailback flock gliding below) · coins ×2.2 Line(162–225, −2.8, 1.5) ≈ 40 · Clean Line on landing at 240 |
| U exit | Ramp down −3.5 m over 222–240 (19%) onto the descent ramp, whose start widens to `x` −4.0…+1.4 at 240 |
Without Vine Grip, H1 is visible but ungrabbable; air steering after V2 is clamped to the Beam A knot (lands safe).

**(c) Secret: Crown Nest (Double Jump).** On the V1 landing branch (104–140, `x` −1.2…+1.2):
| Element | Value |
|---|---|
| Crown limb **CL** | `s` 114–134, top `y` +2.2 above the branch; `x` +1.4…+3.6 over 114–126, shifting to `x` +0.4…+2.6 by 134 |
| Access | **High-ledge side-step** (§7.8): while feet ≥ 1.95 m (top − 0.25), the lateral limit extends to include CL, so a double jump near apex plus a steer right lands on it. Single-jump apex 1.41 m never qualifies |
| Content | **D-13 Crown Nest** trigger at 124 (a sailback nesting colony on a woven limb crown, eggs, open view). **3 crystals** at 118, 124, 130 (centre of CL). Coins Line(115–133, CL centre, 1.5) ≈ 13. No obstacles |
| Exit | CL ends at 134 with a 2.2 m drop; while airborne after leaving CL the lateral limit is the branch below (`x` −0.9…+0.9 for the body), so she always lands on the branch (≥ 4.5 m before the V2 grab zone at 138.5) |
| Locked look | Crystals glint and sailbacks circle the nest from the moment the branch is visible ("how do I get up there?") |

**(d) Creature slots:** sailback perch (slice, 100% sailback); **CS-CV2** on the trunk during the descent, 245–260,
`x` +4.5, `y` +6: veilhanger (CT only).

### 3.7 `F_Challenge_FallenGiants_01` variant `H` · Challenge · rating 8 · Forest · 240 m · Danger–Mastery
Spacing rule for all `H` variants: jump↔slide ≥ 0.75 s and same-type ≥ 0.65 s at 16 m/s; no fast-fall-only combos
(fast-fall is never taught in MVP) `[ASSUMED]`.
| `s` | Content |
|---|---|
| 15 | **Low 0.9 full, Walk** (trunk, 5 m top) |
| 28 | **High 1.0 full** |
| 40 | **Low 0.6 full** |
| 51–55 | **Gap 4.0 full** |
| 66 | **Blk 2.0 @−1.0** + **Blk 2.0 @+2.5** (corridors `x` −3.5…−2.0 and 0…+1.5) |
| 76 | **High 1.0 full** |
| 88 | **Thorns `x` −3.5…0** + **Low 0.5 `x` 0…+3.5** |
| 100–104 | **Gap 4.0, `x` −3.5…+1.0** (floor stays on +1.0…+3.5) |
| 116 | **Low 0.6 full** |
| 128 | **High 1.0 full** |
| 139 | **Blk 1.6 @0** |
| 149–153.5 | **Gap 4.5 full** |
| 165 → 177 | **High 1.0 full** → **Low 0.6 full** |
| 189, 199 | **Blocker `x` −3.5…+0.5**, then **Blocker `x` −0.5…+3.5** |
| 210–213.5 | **Gap 3.5 full** |
| 224 | **High 1.0 full** |
Coins ≈ 70 (arcs over gaps, lines in corridors).

### 3.8 `F_Challenge_ThornRun_01` variant `H` · Challenge · rating 9 · Forest · 200 m · Mastery
| `s` | Content |
|---|---|
| 15–60 | Path narrows to 3.0 m. **Thorns** `x` −1.5…0 at 22, 0…+1.5 at 30, −1.5…0 at 38, 0…+1.5 at 46 (shift 1.5 m per 0.5 s ≤ V3 3.2 m) |
| 56 | **High 1.0 full** |
| 68–71 | **Gap 3.0 full** |
| 78–100 | Back to 7.0 m (0.09 m/m) |
| 88, 96, 104 | **Blk 1.4 @−2.0**, **@+1.0**, **@−1.0** |
| 116 → 128 | **Low 0.8 full, Walk** → **High 1.0 full** |
| 140–144.5 | **Gap 4.5 full** |
| 155 | **Thorns full**, height 0.6 |
| 167 → 179 | **High 1.0 full** → **Low 0.6 full** |
Coins ≈ 50.

### 3.9 New validator rules (added to spec 102 §4.2)
| # | Rule |
|---|---|
| V15 | **Air-grab vines:** (a) the catch zone is reached by every release of the feeding vine from `p` 0.75 to auto-release combined with a steer toward `xVine` starting on the release tick at the human lateral rate (8 m/s, V3); (b) no release before `p` 0.70 reaches it; (c) every release of the air-grabbed vine (all window ticks + auto) lands on its platform with 0 hits; (d) every arc that misses the zone lands on a walkable surface; (e) undersides of raised segments are ≥ 2.85 m above any walkable surface beneath them |
| V16 | **Vault zones:** zone ≥ 6.0 m long, spans the full branch width, face height 1.0–1.6 m; every grounded or airborne entry into the zone with Root Vault ends on the ledge top with 0 hits; without Root Vault the zone is unreachable (V12) |
| V17 | **Brittle walls:** front visible ≥ 2.0 s; slide-start window ≥ 0.30 s; when not bursting, the wall behaves as a fork divider (nudge, never Crash); the chamber behind obeys V10 |
| V18 | **High-ledge side-steps:** single-jump apex + 0.10 m < access height; double-jump apex (second jump at first apex) ≥ access height + 0.30 m; the drop back off the ledge always lands on the segment below |
| V19 | **Ability configurations:** each chunk is validated for every subset of the abilities its routes reference (≤ 4 configurations in MVP); the main line passes with none |

## 4. Endless mode (run 2+)

### 4.1 Run structure
`F_Start_RootGate_01` `Short` (control from tick 0) → World Director (spec 102 §6.3) with the 15-chunk pool and
pool variants (§3.1) → death → results → upgrade → RUN AGAIN. No end, no biome change. Revive from run 2 (GDD §11).

### 4.2 Director additions
| Rule | Value |
|---|---|
| **Environment affinity** (world logic: "the river leads to the falls, the falls to the canopy") | Weight ×3.0 for `R_Swim_Pool_01` right after `R_Transition_Ford_01`; ×2.0 for `F_Recovery_Riverbank_01` after any River chunk; ×3.0 for `C_Canopy_VineSpan_01` and ×2.0 for `R_Branch_Waterfall_01` after `D_Discovery_Overlook_01` |
| **Set-piece spacing** | `SetPiece` chunks (Canopy) and closed-water chunks (Swim Pool) ≥ 3 chunks apart; at least one of them within any 8 consecutive chunks after 500 m |
| **Owned-ability weight** | Chunks containing content for an owned ability: weight ×1.3 (abilities get used) |
| **Teaser weight** | Chunks whose locked content needs the cheapest unowned ability: ×1.2 (keeps "I want to get there" fresh) |
| **Locked-place guarantee** (USP) | While any ability is unowned: if no chunk with *visible locked content* has been picked by 1,000 m, the next eligible pick is forced to one (prefer content of the cheapest unowned ability whose prerequisites are met); same again if fewer than 2 by 2,500 m. Forced picks still pass the filter and V11. A locked place counts as "shown" only if its teaser is on screen ≥ 1.5 s (camera check) |
| **Secret cadence** (USP) | If no chunk with an *enterable* secret (observation, or ability owned) appeared in the last 7 picks, those chunks get weight ×2.0 until one does |
| **Fork cadence** | Branch chunks count; Grotto and Swim Pool (secret-only forks) don't |
| **Showcase** | Spec 103 §10.2, for every ability (Creature Tracking's showcase = a chunk with a veilhanger slot, spawn forced 100%) |
| **Discovery assignment** | Spec 102 §7 plus §5–§6 slot rules and pity timers |
| **Hard variants** | `H` variants only in Danger/Mastery; weight ×1.5 there when `S` > 0.5 |

### 4.3 Distance milestones and the best-distance marker `[ASSUMED]`
- **Best marker:** a painted expedition pennant on a pole at the player's best distance (`x` at the path edge, from
  run 2). Passing it: small toast "NEW RECORD", `mus.sting.record`, light haptic. It drives "almost" without UI clutter
  (Part XXVII). Never moved, hidden or delayed by DDA.
- **Milestones** 1,000 / 2,500 / 5,000 / 8,000 / 10,000 m: a cairn marker in the world + a 1.0 s distance toast the
  first time per profile only.

### 4.4 Content depth by phase (honest limit)
Rating bands 6–10 (Danger, Mastery) have 5–6 eligible ids plus `H` variants; R1 (no id within 6) will sometimes need
the fallback "band ±1" (spec 102 §6.3 step 5). Accepted for MVP; sim target §13 caps fallback use. More hard content
is post-MVP content scale (Part LXVIII step 11).

## 5. Creatures (3)

### 5.1 Sailback (existing, spec 103 §7): **helpful**, common
Unchanged behaviour. Pool slots: C8 perch (100%), C9 D2 perch (100%, reveals Veil Grotto), Overlook flock 100–140
(`x` ±6, `y` 8; 60%), Meadow log perch at 70 (`x` +5; 40%). Discovery `observeTime` 0.8 s.

### 5.2 Ripplekit (new): **curious**, uncommon · river
**Design intent:** a small river animal that *plays* with Pista: it paces her along the bank and copies her leaps.
"It's racing me!"

| State | Enter when | Behaviour |
|---|---|---|
| Hidden | spawn | Under the surface at the slot start |
| Surface | Pista `Δs` ≤ 30 m to the slot start | Pops up 6–10 m ahead with a spray shake (`sfx.ripplekit.chirr`) |
| Pace | after Surface (0.4 s) | Moves at Pista's forward speed, `Δs` 4–10 m ahead, along the slot's lateral spline (always ≥ 1.0 m outside the path's soft edge, or ≥ 1.0 m outside the river's swim width), for `paceTime` |
| Mimic | Pista jumps or leaps while it is in Pace and `Δs` ≤ 15 m | 0.3 s later it **skips** across the surface (fan-tail skip, 1.2 m high) and returns to Pace. Max 3 mimics per encounter |
| Play bonus | 3 mimics in one encounter | Happy spin, `+20 coins`, event `creature_play`; once per encounter |
| Dive | `paceTime` ends, or Pista takes a hit within 20 m, or slot end | Dives with a tail slap |
| Gone | after Dive | — |
- Slots: Ford CS-FD1 100–150 (`x` +5.0…+6.0, river side; 40%), Swim Pool CS-SW1 140–200 (bank shallows `x` ±5.5, side
  by seed; 60%), Riverbank CS-RB1 (50%). Never in Expedition 1.
- **Guarantee:** run 3 forces 100% in the first eligible slot if undiscovered; afterwards 100% in every slot until
  discovered (it gates Creature Tracking, §7.1).
- Never collides, never enters the path or swim corridor, never crosses Pista's line of sight to an obstacle
  (lateral spline outside the corridor by rule).

| Name | Value | Unit |
|---|---|---|
| `paceTime` | 6.0 | s |
| `surfaceAhead` | 6–10 | m |
| `mimicDelay` / `mimicRange` / `mimicMax` | 0.3 / 15 / 3 | s / m / count |
| `playBonusCoins` | 20 | coins |
| `observeTime` | 1.0 | s |

### 5.3 Veilhanger (new): **rare**, shy · canopy and stilt roots · needs Creature Tracking
**Design intent:** the "did I just see that?" creature. A large, slow tree-dweller so overgrown with veilmoss that it
looks like a hanging curtain until it opens its eyes and turns its head.

| State | Enter when | Behaviour |
|---|---|---|
| Hanging | spawn | Motionless, eyes closed; only its moss sways (indistinguishable from veilmoss at a glance) |
| Noticed | `Δs` ≤ 40 m | Opens amber eyes, head tracks Pista, low wooden **knock-hum** (`sfx.veilhanger.knock`) |
| Retreat | `Δs` ≤ 12 m, or a hit/hard landing within 30 m | Climbs up into the canopy over 2.0 s, moss curtain swinging |
| Gone | Retreat done | — |
- **Locked teaser (without Creature Tracking):** its slots show fresh claw scratches high on the trunk, a swaying moss
  curtain and a single distant knock-hum, but no animal ("something lives up there").
- Spawns **only with Creature Tracking.** Slots: StiltRoots CS-SR1, Grotto CS-GR1, Canopy CS-CV2. Chance 30% per
  eligible slot; **showcase** run after unlock 100%; **pity:** undiscovered after 3 eligible slots → 100%.
- Observed = state Noticed or Hanging, `Δs` ∈ [5, 45] m, `|Δx|` ≤ 12 m; discovery after `observeTime` 1.2 s.
  Retreat still counts as observed. It is always ≥ 5 m above the path (never in the corridor).

### 5.4 Creature design briefs for art-director (original designs; owner reviews later `[ASSUMED]`)
Rules for all: familiar animal logic + one impossible twist; four limbs or wings; two eyes; no glow on skin; no
breathing holes; no braid links; not rideable; painterly style (ART_DIRECTION_PAINTERLY: bold simplified shapes,
hand-painted albedo, teal-shifted shadows). Silhouette must read at 20–40 m on a phone. Budgets are starting values
for performance-engineer to confirm on iPhone 12.
| | Ripplekit | Veilhanger |
|---|---|---|
| Size | 0.7 m body + 0.4 m tail | 2.0 m body, 3.0 m arm span |
| Anatomy | Sleek semi-aquatic mammal, four webbed feet, round ears, large dark eyes, whiskers | Slow arboreal mammal, long arms with hook claws, short muzzle, large amber eyes |
| The twist | A broad **fan tail** it uses to skip across the water like a thrown stone | Its back and arms grow living **veilmoss** in long strands: hanging, it *is* a moss curtain |
| Palette | Warm umber fur (darkens when wet), teal-sheened guard hairs, cream belly; turquoise `#2EC4B6` fleck on the tail fan | Moss greens matching veilmoss; bark-grey face; one rare accent: magenta `#C4407F` inner ears/throat (matte, ART_DIRECTION rare-creature accent) |
| Must not resemble | Real otter photo-likeness, cartoon "water starter" monsters | Real sloths too literally, Totoro-like round spirits, the board's mossy tortoise |
| Animations | swim-pace loop, surface shake, skip (mimic), happy spin, dive | hang idle (moss sway), eyes open, head track (procedural), climb-retreat |
| Audio identity | bright chirr, splash skip | wooden knock on hollow limbs, low hum |
| Budget (start) | LOD0 ≤ 4k tris, 1 material, 1024² atlas, ≤ 24 bones | LOD0 ≤ 6k tris (moss as 2–4 card strands), 1 material, 1024², ≤ 30 bones |
| Journal art | Painted portrait 1024×1024 + silhouette | same |

## 6. Discoveries and secrets (15 entries + "?")

### 6.1 Rules
- **Creature:** observed for its `observeTime` (§5). **Plant:** pass a **specimen** (a larger hero cluster) with
  `|x − xSpecimen|` ≤ `specimenRadius` 3.0 m on the tick `s` crosses it; the plant reacts (bellcap puff, reeds bow,
  veilmoss parts, duskbells ring). **Location/Mystery:** enter its trigger on the route that holds it.
- First time per profile: toast "NEW DISCOVERY · <Unknown Species | New Plant | New Place | Mystery> · Added to
  Journal" (2.0 s, queue gap 0.5 s), reveal sting, light haptic, reward, `discovery_found` event. Later: sighting +1,
  no toast.
- **Rewards:** common and uncommon +50 coins +2 crystals; rare +100 coins +4 crystals. Credited at once and listed on
  the results.
- Specimens of undiscovered plants are present at 100% in their slots; after discovery 50% (flavour).

### 6.2 Entry list (working labels `[ASSUMED]`; lore names are the owner's call later)
| Id | Entry | Cat. | Rarity | Where / how found | Ability | Hint (shown once the habitat chunk was visited) |
|---|---|---|---|---|---|---|
| D-01 | Falls Basin | Location | common | Overlook trigger `s` 90 | — | "A great fall pours from the rootstone." |
| D-02 | Sailback | Creature | common | C8/C9 perches, Overlook, Meadow (§5.1) | — | "Something glides between the stiltwoods." |
| D-03 | Veil Grotto | Location | uncommon | Waterfall secret, `s` 140 (follow the sailbacks into the falls) | — | "The sailbacks fly into the falls… and don't come out." |
| D-04 | Sunken Arch | Location | uncommon | Swim Pool deep dive | Deep Breath | "A passage glints deep under the river." |
| D-05 | Ripplekit | Creature | uncommon | Ford, Swim Pool, Riverbank (§5.2) | — | "Something skips along the river banks." |
| D-06 | Veilhanger | Creature | rare | StiltRoots, Grotto, Canopy (§5.3) | Creature Tracking | "Some of the moss curtains have eyes." |
| D-07 | Bellcap | Plant | common | Risky routes: StiltRoots 110, Ravine bridge 110, Waterfall stepping rocks 120 (`x` −4.4) | — | "Golden flowers grow where the way is daring." |
| D-08 | Ribbon Reed | Plant | common | Ford 70 (`x` +4.4), Riverbank 32, Ravine stream 128 | — | "Reeds that bow away from you." |
| D-09 | Veilmoss | Plant | uncommon | Secret entrances: Waterfall D2 face 112 (`x` +4.0), Grotto curtain 85, StiltRoots crack 100 | — | "Curtains of moss hide what's behind them." |
| D-10 | Duskbell | Plant | uncommon | Waterfall right edge 95 (`x` +4.8, i.e. hug the right side), Grotto gallery 100 | — | "Violet bells ring near hidden places." |
| D-11 | Sunspan Highline | Location | uncommon | Canopy high line, `s` 195 | Vine Grip | "High vines above the canopy path." |
| D-12 | Sunshelf | Location | uncommon | Ravine secret, `s` 130 | Root Vault | "A sunny ledge on the ravine wall." |
| D-13 | Crown Nest | Location | rare | Canopy crown limb, `s` 124 | Double Jump | "Crystals glint in a nest above the branch." |
| D-14 | Old Marker | Mystery | uncommon | StiltRoots secret, `s` 130 | Trail Sense | "Someone mapped this forest before you." |
| D-15 | Singing Hollow | Mystery | rare | Grotto chamber behind BW1, `s` 130 | Shoulder Charge | "The root wall in the grotto hums." |
Counts: Creatures 3 · Plants 4 · Locations 6 · Mysteries 2/? = 15. Rarity: 4 common, 8 uncommon, 3 rare.
**"?" teaser:** once D-14 and D-15 are both found, Mysteries shows a third silhouette "???" with "The marks in the
Hollow match the Old Marker's map. They point beyond the forest." Not an entry, not counted (post-MVP hook).
Every ability opens at least one entry: "ability progress" (Part XVIII).

### 6.3 Pool-variant specimen additions
`R_Branch_Waterfall_01` `Default` and `R_Transition_Ford_01` `Default` get the specimen slots in §6.2. Expedition 1
keeps its slice content (no specimens) `[ASSUMED]`, so its toast density stays as tested.

### 6.4 Secrets (7) and the biome secret count
| # | Secret | Chunk | Opened by |
|---|---|---|---|
| 1 | Veil Grotto | Waterfall | observation (sailbacks, thin curtain, glint, mist, duskbells, drip echo) |
| 2 | Root Gallery | Grotto | observation (whirlseeds blowing out of the curtain, draft, drip echo) |
| 3 | Sunken Arch | Swim Pool | Deep Breath |
| 4 | Old Marker Hollow | StiltRoots | Trail Sense |
| 5 | Sunshelf | Ravine | Root Vault |
| 6 | Singing Hollow | Grotto (inside #2) | Shoulder Charge |
| 7 | Crown Nest | Canopy | Double Jump |
Results line "Secrets n/7" counts secrets entered at least once. Secret crystals respawn every run (they are the
recurring reward for knowing the forest).

## 7. Abilities (7 "new possibility" unlocks)

### 7.1 Tree, costs, prerequisites
| # | Ability | Opens (content already seen locked) | Cost (coins / crystals) | Prerequisites |
|---|---|---|---|---|
| 1 | Deep Breath | Sunken Arch (D-04) | 150 / 0 | — |
| 2 | Vine Grip | Sunspan Highline (D-11) | 600 / 2 | Deep Breath |
| 3 | Trail Sense | Old Marker Hollow (D-14) + secret-cue pulse everywhere | 1,200 / 4 | Deep Breath |
| 4 | Root Vault | Sunshelf (D-12) | 2,000 / 6 | Vine Grip **or** Trail Sense |
| 5 | Creature Tracking | Veilhanger (D-06) + creature tracks everywhere | 3,000 / 8 | Trail Sense; D-02 and D-05 discovered |
| 6 | Shoulder Charge | Singing Hollow (D-15) | 4,500 / 12 | Root Vault |
| 7 | Double Jump | Crown Nest (D-13) | 7,000 / 20 | Vine Grip and Root Vault; ≥ 8 journal entries |
Totals: 18,450 coins, 52 crystals. Every requirement is earnable by play; no timers, energy, ads or purchases.
Locked cards show their prerequisites in plain words ("Needs: Trail Sense · Discover 2 creatures (1/2)").
Unlock flow for all: spec 103 §9.4 (ability card, 3 s preview, LEARN, 1.2 s unlock moment, Showcase in the next run).

### 7.2 Deep Breath (spec 103 §4.5; unchanged)

### 7.3 Vine Grip: catch vines in mid-air
- Without it, vines are grabbed only from their takeoff funnel (spec 103 §5.2). With it, **air-grab vines** (flag
  `AirGrab`) become catchable when the hands enter the vine's catch zone while airborne (any airborne state,
  including vine air). On the catch tick: buffered commands cleared, lateral locked to `xVine`, she blends
  (`grabSnapTime` 0.10 s) onto the standard pendulum at **`p` 0.5** (`θ` 10°) and swings the remaining 30 ticks;
  release rules, Perfect window (ticks 45–54 of the full swing = 15–24 after the catch) and auto-release unchanged.
- Without Vine Grip, air-grab vines are scenery: no catch, and air steering is clamped to the walkable envelope below.
- Event `traversal_result(vine_air_grab)`.

### 7.4 Root Vault: vault onto raised roots
- A **vault zone** (§3.3) sits before a ledge face 1.0–1.6 m high. With Root Vault, the first tick Pista is inside the
  zone (grounded, or airborne with feet below the ledge top) she **vaults** automatically (context action, Part V):
  `vaultTime` 0.45 s along an authored arc to the ledge top, landing `vaultLandOffset` 2.0 m past the face, then runs.
  A swipe up inside the zone also starts the vault on that tick. Airborne above the ledge top: she simply lands on it.
- During the vault: invulnerable to Minor contacts (none can be authored there, V16); swipes are buffered 150 ms and
  run on landing; lateral locked to the zone centre line.
- Without Root Vault the zone is outside the path (locked geometry, V12).

### 7.5 Trail Sense: notice hidden ways
- Every secret entrance within `trailSenseRange` 60 m pulses (cue intensity ×1.5 for 0.4 s every 1.2 s, plus
  `sfx.trailSense.ping` panned toward it). Applies to observation secrets too (it helps players who miss them).
- Trail-Sense secrets (flag `TrailSense`) exist as passable geometry only with the ability (§3.5); without it the
  locked teaser shows.

### 7.6 Shoulder Charge: burst through brittle root walls
- A **BrittleWall** is a divider-like wall with a chamber behind it. With Shoulder Charge, if Pista is **Sliding** on
  the tick her hitbox reaches the wall front and her `x` is inside the wall's span, the wall **bursts** (splinter VFX,
  `sfx.charge.burst`, medium haptic) and she slides into the chamber. Not sliding, or without the ability: the wall
  acts as a fork divider (spec 102 §3.3 nudge to the open side, never Crash) and no damage happens.
- A slide that ends inside the burst opening continues as running. A buffered slide that fires within the last
  `brittleSlideGrace` 0.10 s before contact counts.

### 7.7 Creature Tracking: read the wildlife
- Enables rare spawns (veilhanger). **Tracks:** when an occupied creature slot is within 150 m ahead, its sign shows on
  the path edge (wet prints for ripplekit, claw scratches on trunks for veilhanger, shed sail scales for sailback)
  and its call plays panned toward it at −12 dB. No HUD element.

### 7.8 Double Jump
- Swipe up while airborne (not swinging, not vaulting, not in water) and the double jump is unused → second jump on
  that tick: `vy = doubleJumpVelocity` 8.64 m/s (apex +1.20 m above that point, spec 101 gravity incl. apex hang).
  Resets on landing, on vine release and after a vault.
- **Near-ground rule:** if `vy` < 0 and feet are ≤ `djGroundBuffer` 0.35 m above the floor below, the swipe is buffered
  as a landing jump instead (protects the existing buffered-jump feel).
- Coyote window: a swipe up is the normal (first) jump.
- **High-ledge side-step** (V18): a raised segment flagged `SideStep` adds its `x` range to the lateral limit while the
  feet are ≥ its top − `ledgeAssistDrop` (0.25 m).
- Never required on any main line; never changes safe routes (V19).

| Name | Value | Unit |
|---|---|---|
| `airGrabRadius` (hands, `s`/`y`) / lateral | 1.2, 1.0 / 1.0 | m |
| `vaultTime` / `vaultLandOffset` / face height | 0.45 / 2.0 / 1.0–1.6 | s / m / m |
| `trailSenseRange` / pulse | 60 / ×1.5 every 1.2 s | m |
| `brittleSlideGrace` | 0.10 | s |
| `trackRange` | 150 | m |
| `doubleJumpVelocity` / `djGroundBuffer` | 8.64 / 0.35 | m/s / m |

## 8. Economy: coins and crystals

### 8.1 Sources
| Source | Coins | Crystals |
|---|---|---|
| Trails | 1 per 4.5 m placed on Safe/main; Risky ×2.2; RewardProfile ×0.7/1.0/1.3 | — |
| Clean Line / Perfect / Perfect Span | +50% branch coins / +10 / +25 | — |
| Ripplekit play bonus | +20 | — |
| Flow crystals | — | 1 per 1,200–1,800 m (Pickups stream; never in seam zones) `[ASSUMED]` |
| Risky branch | — | 20% chance of 1 |
| Secrets | — | as authored (Veil Grotto 2, Gallery 1, Sunken Arch 2, Old Marker 2, Sunshelf 2, Hollow 2, Crown 3) |
| Perfect columns | — | V2 column 1 (slice), H1 column 1 |
| First discovery | +50 (rare +100) | +2 (rare +4) |
Spends: abilities (§7.1), coin upgrades (§8.3), revive 1/2/4 crystals (run 2+). Nothing else in MVP.

### 8.2 Earn-rate targets (collected, per run, excluding first-discovery rewards)
| Profile | Coins per 1,000 m | Crystals per run (no revives) | Collection efficiency (placed coins) |
|---|---|---|---|
| Novice | 120–160 | 0.6–1.2 | 55–65% |
| Average | 180–230 | 1.3–2.2 | 70–80% |
| Expert | 250–320 | 3.0–5.0 | 85–95% |
Design intent: **coins pace the unlocks; crystals are the choice resource** (revive now, or bank toward the next
ability). Crystals should bind only for players who revive often, and only near abilities 6–7.

### 8.3 Coin upgrades (power-up durations; level 1 owned from the start)
| Upgrade | L1 | L2 | L3 | L4 | L5 | Prices L2 / L3 / L4 / L5 |
|---|---|---|---|---|---|---|
| Magnet duration | 10 s | 12 s | 14 s | 16 s | 18 s | 250 / 500 / 900 / 1,500 |
| Shield max duration | 30 s | 35 s | 40 s | 45 s | 50 s | 250 / 500 / 900 / 1,500 |
| Explorer Vision duration | 12 s | 14 s | 16 s | 18 s | 20 s | 250 / 500 / 900 / 1,500 |
Total 3,150 each, 9,450 all. Upgrades are optional comfort, never needed for any content.

## 9. Power-ups (3)
| | Magnet | Shield | Explorer Vision |
|---|---|---|---|
| Effect | Pulls coins within 4.0 m lateral, 8.0 m ahead, 1.0 m behind, on Pista's **current route segment** only; pull speed 22 m/s (a coin reaches her ≤ 0.35 s) | Spec 103 §9.3 (absorbs Minor, Bump, Crash; not Fall) | Secret cues ×2.0 (overrides DDA, min 2.0); a cyan glint trail leads from 60 m to each open secret entrance; discovery slots within 80 m ping (specimens shimmer, creature call); locked ability secrets show one soft glint (teaser only, nothing opens) |
| Ignores | Crystals, `SkillPlaced` coins (perfect columns, underwater coins), coins across a divider | — | — |
| Duration | 10–18 s (§8.3) | until used, max 30–50 s | 12–20 s |
| HUD | Icon + timer ring (top, thumb-safe side) | same | same |
- **Spawn:** one slot every 600–900 m (DDA §10), never in seam zones, never within 1 s of a required action, 60% of
  branch-chunk slots go on the Risky branch. Type weights Magnet 40 / Shield 35 / EV 25; EV ×2.5 when a chunk with an
  open secret is among the next 2 planned picks; Shield ×1.5 when `S` < −0.5 or a Challenge chunk is next.
- Picking up the type that is already active refreshes it to full duration. Different types run together.
- **Intro:** run 2's first slot is a Magnet; the first EV is placed before a secret-bearing chunk. First pickup of each
  type shows a 1.5 s icon toast with one line ("MAGNET · pulls coins", "VISION · reveals hidden ways").
- Pad 0.50 m; collectable in every state. Power-ups end at death; never carried between runs.

## 10. Basic dynamic difficulty
Spec 102 §6.4 is binding (`S` from distance, hits per km, traversal success; ±0.2 per run; start −0.3; mercy). MVP
additions (applied at run start, never mid-run except mercy):
| `S` | Band shift | Recovery every | Cue intensity | Power-up interval | Hard variants |
|---|---|---|---|---|---|
| < −0.5 | −1 | phase − 1 (min 3) | 1.30 | 600–750 m | weight ×0.5 |
| −0.5…0.5 | 0 | phase | 1.00 | 650–850 m | ×1.0 |
| > 0.5 | +1 | phase + 1 | 0.85 | 750–900 m | ×1.5 |
`travScore` now includes vine Good/Perfect, air grabs, vaults, beam gaps, swim dives/leaps. **Never** changed by
DDA: speed, movement numbers, coin density, prices, the best marker, creature/discovery odds, ability-gated content.

## 11. Results, abilities screen, journal

### 11.1 Results next objective (exactly one highlighted; first match wins) `[ASSUMED]`
| P | Condition | Copy (template) | Tap goes to |
|---|---|---|---|
| 1 | An ability is affordable now (prereqs met). If several: the one whose locked content was **seen this run**, else the cheapest | "Vine Grip ready · 600/600 · Catch the high vines" + thumbnail of the content | Ability card |
| 2 | Run ≥ 90% of best, best ≥ 1,000 m, record not beaten | "4,870 / 5,000 m" | — (RUN AGAIN pulses) |
| 3 | Next ability (cheapest with prereqs met) ≥ 60% funded on **both** currencies (min of the two ratios) | "Trail Sense · 74%" (+ "2 crystals short" when crystals are the limit) | Ability card |
| 4 | A missing prerequisite discovery, or an undiscovered entry whose habitat chunk was visited this run and whose ability is owned | Its hint (§6.2), e.g. "Something skips along the river banks" | Journal entry (silhouette) |
| 5 | Secrets found < 7 and a secret chunk was visited this run | "Secrets 3/7 in the Verdant Forest" | Journal, Locations |
| 6 | Always | "Next milestone: 2,500 m" | — |
Rotation: an objective shown in the last 2 results is skipped if a lower priority (P3–P6) matches, except P1/P2.
Run 1 keeps spec 103 §9.2. Events `objective_shown {priority, objective_id}`, `objective_tapped`.

### 11.2 Abilities and upgrades screen
From Home ("Abilities") and the results card. A tree of 7 cards (§7.1 order, branches drawn as dotted map trails);
states **Locked** (prerequisites listed with progress), **Available** (coins and crystals bars), **Owned**. Card:
name, one line, 3 s looping preview, cost, [LEARN] (disabled below cost, never a purchase prompt). Below: 3 upgrade
rows with 5 pips and [UPGRADE n coins]. All reachable with one thumb; Dynamic Type.

### 11.3 Journal (basic)
- Header "Journal · Verdant Forest · 7/15". Tabs: **Creatures 2/3 · Plants 3/4 · Locations 2/6 · Mysteries 0/?**.
- Grid (3 columns portrait, 5 landscape). Found: painted thumbnail, name, rarity pips (● common, ●● uncommon,
  ●●● rare), NEW badge until opened. Unfound: silhouette + "?"; hint line if the habitat was visited, else
  "Not seen yet".
- Entry page: art, name, category, rarity, habitat, behaviour (≤ 2 lines), "First found: run 6 · 1,240 m · Veil Falls
  · 9 Oct", sightings ×N, the reward it gave, and for mysteries a clue line.
- Entry text (behaviour lines, working copy):
  sailback "Basks with its sail half open; glides when startled." · ripplekit "Paces runners along the banks and
  copies their leaps." · veilhanger "Hangs still as moss; opens its eyes only when you pass." · bellcap "Puffs gold
  pollen as you brush by." · ribbon reed "Bows away from anything that runs past." · veilmoss "Sways apart for a
  moment, then closes again." · duskbell "Rings faintly in cool air." · locations/mysteries: their hint as subtitle.
- Opened from Home and the results; read-only; no currency actions inside.

## 12. Audio, haptics, camera hooks (content in Phase 4 polish)
| Beat | Audio | Haptic | Camera |
|---|---|---|---|
| Vault | `sfx.vault`, `sfx.land.ledge` | L | none |
| Air grab | `sfx.vine.grab.air` | L | spec 103 swing modifier |
| Brittle burst | `sfx.charge.burst` | M | 0.15 s 0.03 m shake (off with Reduced Motion) |
| Double jump | `sfx.jump.double` | — | spec 101 jump follow |
| Ripplekit | `sfx.ripplekit.chirr`, `.skip`, `.dive` | L on play bonus | — |
| Veilhanger | `sfx.veilhanger.knock`, `.climb` | L on discovery | — |
| Magnet / EV | `sfx.magnet.loop`, `sfx.vision.on/off`, `sfx.trailSense.ping` | L pickup | EV: 0.3 s colour-grade lift |
| Best marker | `mus.sting.record` | L | — |
| Location triggers (D-11, D-12, D-13, D-15) | `mus.reveal.vista` / `amb.hollow.hum` | L | vista beat (spec 103 §11) for D-11 and D-12 |

## 13. Balance and simulation targets (balance-simulator)

### 13.1 Run length by skill (bots: spec 101 §6.2 profiles + ability usage + release reaction model)
| Profile | Runs 2–5 median | Runs 6–15 median | Day 7 median |
|---|---|---|---|
| Novice | 600–1,200 m (1:00–1:55) | 900–1,600 m | 1,200–2,200 m (1:55–3:15) |
| Average | 1,200–2,500 m (1:55–3:40) | 1,800–3,200 m (2:50–4:35) | 2,500–4,500 m (3:40–6:05) |
| Expert | 3,000–5,000 m (4:15–6:40) | 4,500–8,000 m | 6,000–10,000 m (8:00–12:10) |
Times from spec 101 `v(d)` plus ~4% for swim sections. Perfect bot never dies before 8,000 m.

### 13.2 Unlock pacing, days 1–7 (reference player: day 1 = 8 runs, days 2–7 = 5 runs/day)
| Ability | Cum. coins | Average, ability-first policy | Average, balanced (25% of coins to upgrades) | Novice | Expert | Play time to unlock (Average) |
|---|---|---|---|---|---|---|
| Deep Breath | 150 | run 1 (day 1) | run 1 | run 1 | run 1 | ~5 min |
| Vine Grip | 750 | runs 2–4 (day 1) | runs 3–5 | runs 4–6 | run 2 | ~12 min |
| Trail Sense | 1,950 | runs 5–7 (day 1) | runs 6–9 | day 2 | runs 3–4 | ~20 min |
| Root Vault | 3,950 | runs 8–11 (day 1–2) | day 2 | day 3 | day 1 | ~35 min |
| Creature Tracking | 6,950 | day 2–3 | day 3 | day 4–5 | day 2 | ~55 min |
| Shoulder Charge | 11,450 | day 4 | day 4–5 | day 6–7 | day 2–3 | ~90 min |
| Double Jump | 18,450 | day 5–7 | day 7–9 | after day 7 | day 3–4 | ~140 min |
Spec 102 §11 still holds: Average bot gains a new ability every 1–3 runs during runs 1–10.

### 13.3 Targets (1,000 profile-series per bot, seeds varied)
| # | Target |
|---|---|
| T1 | Run length and pacing tables §13.1–13.2 within their ranges |
| T2 | Crystals: Average ability-first bot with revive policy "revive once when run ≥ 60% of best and crystals ≥ cost": crystals block an otherwise affordable ability for ≤ 2 runs, and only for abilities 6–7 |
| T3 | Risky taken 30–50% of forks (Average, expected-value chooser); Clean Line on 40–60% of Risky attempts |
| T4 | Sunspan Highline: Average bot with Vine Grip catches H1 on 20–40% of canopy visits; Expert ≥ 60% |
| T5 | Crown Nest: Average bot with Double Jump reaches CL on 30–50% of attempts; Expert ≥ 75% |
| T6 | Discovery: Average reaches 8 entries by run 12 and 12 entries by day 5; veilhanger found within 3 runs of unlocking Creature Tracking in ≥ 90% of series; ripplekit by run 4 in 100% |
| T7 | No single chunk causes > 20% of Average deaths in any phase; Ravine Risky and Canopy U each < 12% |
| T8 | Director fallback "band ±1" used on ≤ 30% of Mastery picks and ≤ 10% of Danger picks |
| T9 | DDA: a bot that improves mid-series reaches stable `S` within 4 runs, oscillation ≤ 0.3 |
| T10 | Power-ups: ≥ 1 per 900 m in 100% of runs; EV precedes an open secret in ≥ 60% of EV spawns |
| T11 | Results: P1 or P3 shown in ≥ 50% of results during days 1–3; the same objective never 3 times in a row except P1/P2 |

## 14. Config ScriptableObjects (`Assets/_Game/Config/`)
| Asset | Fields |
|---|---|
| `Progression/AbilityDefinition` ×7 | cost, prerequisites (abilities + discovery ids + entry count), copy, preview, showcase flag, ability parameters (§7 table) |
| `Progression/UpgradeDefinition` ×3 | per-level values and prices (§8.3) |
| `PowerUps/MagnetConfig`, `ShieldConfig`, `ExplorerVisionConfig`, `PowerUpSpawnConfig` | §9 |
| `Creatures/RipplekitConfig`, `VeilhangerConfig` (+ existing `SailbackConfig`) | §5 tables, spawn chances, pity |
| `Discovery/DiscoveryEntry` ×15 | id, category, rarity, reward, hint, behaviour line, toast text, art refs |
| `Discovery/DiscoveryConfig` | `specimenRadius`, toast timings, rarity rewards, specimen presence after discovery |
| `Rewards/PickupConfig` | crystal flow interval 1,200–1,800 m, risky crystal 20%, play bonus |
| `World/WorldDirectorConfig` | affinity table, set-piece spacing, ability/teaser weights, H-variant weights, DDA table §10 |
| `World/MilestoneConfig` | milestones, best marker |
| `UI/ResultsConfig` | objective priorities, thresholds, rotation, totals 3/4/6/2, secret total 7 |
| `Chunks/*` | §3 layouts, new traversal data: `AirGrabVines`, `VaultZones`, `BrittleWalls`, `SideStepLedges`, `SpecimenSlots`, `CreatureSlots`, locked/open geometry per ability |

## 15. Acceptance criteria (EditMode unless stated)
Chunks and validator
- **AC-104-01** The catalog holds exactly the 15 ids and the variants of §3.1; `ScriptOnly` variants never appear in director output (10,000 picks).
- **AC-104-02** Riverbank, Ravine (`Default`, `H`), Grotto, StiltRoots `Default`, Canopy `Default`, FallenGiants `H`, ThornRun `H` pass V1–V19 and W1–W5 at their PhaseRange speed extremes in every ability configuration (CI).
- **AC-104-03** Without the relevant ability, every ability route is physically closed: the Perfect bot steering at full rate toward it ends on the main line with 0 hits (V12, V19).
- **AC-104-04** Each branch chunk (StiltRoots, Ravine, Waterfall, Canopy) exposes Safe, Risky and Secret routes when all abilities are owned; route selection emits `route_selected` with the right type.
Abilities
- **AC-104-05** LEARN is disabled unless coins, crystals and all prerequisites (abilities, discoveries, entry count) are met; it deducts both costs once and persists across restarts.
- **AC-104-06** Air grab: with Vine Grip, a scripted V2 release at tick 45–60 plus a steer toward H1 starting on the release tick catches H1; releases before tick 42 never do; the catch enters the swing at `p` 0.5 and the remaining swing lasts 30 ticks.
- **AC-104-07** Without Vine Grip, no input sequence catches H1, and every V2 release lands on Beam A with 0 Falls.
- **AC-104-08** Every H1 release tick (window + auto) lands on U with 0 hits at 11 and 15 m/s entry speed.
- **AC-104-09** Vault: with Root Vault, entering the vault zone grounded at any `x` of the zone starts the vault on that tick; 27 ticks later she is on the ledge top 2.0 m past the face; a swipe during the vault executes on landing.
- **AC-104-10** Trail Sense: secret cues within 60 m pulse every 72 ticks; the StiltRoots secret is passable only with the ability.
- **AC-104-11** Shoulder Charge: sliding into BW1 with the ability bursts it and enters the Hollow; running into it, or sliding without the ability, nudges to the gallery with 0 damage; a slide buffered ≤ 6 ticks before contact counts.
- **AC-104-12** Creature Tracking: veilhangers spawn only with it; tracks appear for occupied slots within 150 m.
- **AC-104-13** Double jump fires on the swipe tick with `vy` 8.64 m/s once per airtime; resets on landing, vine release and vault; a swipe with `vy` < 0 and feet ≤ 0.35 m above the floor buffers a landing jump instead.
- **AC-104-14** Crown Nest: reachable with a double jump started at first-jump apex ±0.10 s plus a steer right; unreachable with a single jump; leaving CL at 134 always lands on the branch.
- **AC-104-15** In the first run after any unlock, a chunk with that ability's content appears within 5 picks after the start chunk (1,000 seeds, 100%), with spawn forced for Creature Tracking.
Creatures and discovery
- **AC-104-16** Ripplekit follows §5.2 states for scripted `Δs`; never inside the path or swim corridor; 3 mimics in one encounter credit 20 coins once.
- **AC-104-17** Ripplekit appears in run 3's first eligible slot if undiscovered, and in every slot until discovered.
- **AC-104-18** Veilhanger: Retreat on `Δs` ≤ 12 m or a nearby hit; pity forces it after 3 empty eligible slots.
- **AC-104-19** Discoveries fire per §6.1 (creature observe 0.8 / 1.0 / 1.2 s; plant within 3.0 m on the crossing tick; location triggers), once per profile, with rewards by rarity (50/2 or 100/4).
- **AC-104-20** The journal shows 15 entries in 4 tabs with totals 3, 4, 6 and "2/?", silhouettes and hints for unfound entries, and the "?" teaser only after D-14 and D-15.
- **AC-104-21** Every entry is obtainable: an all-abilities bot finds all 15 within 40 runs in 100% of 1,000 series.
- **AC-104-22** Secret crystals respawn each run; "Secrets n/7" counts distinct secrets ever entered.
Economy and power-ups
- **AC-104-23** Flow crystals spawn 1,200–1,800 m apart (never in seam zones); risky-branch crystal chance 20%.
- **AC-104-24** Upgrade prices and values match §8.3; buying L(n) requires L(n−1); max L5.
- **AC-104-25** Magnet pulls only coins on the current route segment within its box, never crystals or `SkillPlaced` coins; a pulled coin is collected ≤ 21 ticks after entering the box.
- **AC-104-26** Explorer Vision sets secret-cue intensity ≥ 2.0 for its duration and restores the DDA value after; it never opens a locked route.
- **AC-104-27** Same-type pickup refreshes duration; power-ups clear at death; run 2's first power-up slot is a Magnet.
- **AC-104-28** Power-up spacing follows the §10 interval for the run's `S`, with ≥ 1 per 900 m.
Director, DDA, endless
- **AC-104-29** Affinity, set-piece spacing, owned-ability and teaser weights apply as §4.2 (weight log in debug builds); set pieces are never < 3 chunks apart.
- **AC-104-30** `H` variants appear only in Danger/Mastery.
- **AC-104-31** DDA follows §10; nothing in the "never changed" list differs between `S` = −1 and +1 for the same seed and inputs.
- **AC-104-32** The best marker sits at the best distance from run 2, and passing it fires the record toast once per run.
- **AC-104-33** Same profile + seed + inputs → identical run including creatures, specimens, power-ups and discoveries when frames are split into 1–5 ticks.
Results, UI
- **AC-104-34** The results objective follows §11.1 priorities and rotation for scripted profile states (one test per priority).
- **AC-104-35** (PlayMode) Abilities screen: locked cards list missing prerequisites with progress; LEARN and UPGRADE never show a store or price in money.
- **AC-104-36** (PlayMode) Journal opens from Home and Results in ≤ 0.5 s; NEW badges clear on open; Dynamic Type 200% keeps all text readable.
Performance and events
- **AC-104-37** (PlayMode) Creature, specimen, power-up and new-ability updates allocate 0 bytes per tick after warm-up; ≤ 2 creatures active at once.
- **AC-104-38** (PlayMode) Events fire with GDD §22 properties plus: `discovery_found {entry_id, category, rarity}`, `traversal_result` types `vine_air_grab`, `vault`, `charge_burst`, `double_jump`, `creature_play`, `objective_shown`, `objective_tapped`, `upgrade_purchased {id, level, cost}`.
- **AC-104-39** Expedition 1 is unchanged: AC-103-39 still passes and no ripplekit, specimen, Magnet or EV appears in it.
USP rules
- **AC-104-40** Locked-place guarantee: for 1,000 seeds with ≥ 1 unowned ability, ≥ 1 chunk with visible locked content is picked by 1,500 m in 100% of runs and ≥ 2 by 3,000 m in 100% of runs reaching it; `locked_content_seen {ability, chunk_id}` fires when a teaser has been on screen 1.5 s.
- **AC-104-41** Secret cadence: no run contains 8 consecutive picks without an enterable secret unless the filter is empty (logged).
- **AC-104-42** No HUD or UI element points at a secret, specimen or creature slot (PlayMode UI tree scan in every run state).

## 16. USP checklist (QA, balance-simulator and playtests verify; any miss blocks the Phase 3 gate)
Playtests: 5 first-time testers, 5 runs each over 2 sessions, no coaching after "swipe and drag" (same protocol as
spec 103 §12.2). Sim: Average/Novice bots, 1,000 series.
| # | USP promise | Measure | Target | How |
|---|---|---|---|---|
| U1 | Visible-but-unreachable places | Runs (run 2+, ≥ 1 ability unowned) showing ≥ 1 locked place on screen ≥ 1.5 s | ≥ 95% of runs; ≥ 2 places in ≥ 70% of runs reaching 2,500 m | Sim + `locked_content_seen` events |
| U2 | Time to first new possibility | Deep Breath learned | ≤ 6 min from first launch for ≥ 4/5 testers; sim 100% affordable after Expedition 1 | Playtest stopwatch + sim |
| U3 | Steady new places | Runs between unlocks during runs 1–10 | 1–3 (Average bot) | Sim |
| U4 | Discovery early | Journal entries after the first 5 runs | Average median 6–9, Novice ≥ 4; testers ≥ 5 | Sim + save data |
| U5 | Something always missing | Journal at day 7 (Average, reference player) | median 12–14 of 15; full journal never before day 5 | Sim |
| U6 | Secret density | 1,500 m windows containing ≥ 1 enterable secret | ≥ 95%; mean secret opportunities per Average run 1.5–3 | Sim |
| U7 | Observation, not icons | HUD/UI elements that point at secrets or discovery slots | 0 (power-up timers and toasts only) | QA screenshot review of every screen state |
| U8 | Observation secrets get found | Testers who enter Veil Grotto or Root Gallery unaided | 2–3/5 by run 2, ≥ 4/5 by run 5 | Playtest |
| U9 | Next goal always visible | Results with exactly one objective | 100%; ability-related (P1/P3) ≥ 50% on days 1–3; ≥ 4/5 testers can name their next goal when asked | AC-104-34 + playtest |
| U10 | Meaningful routes | Risky share of forks; testers changing a fork choice between runs; decisions made ≥ 1.0 s before the split | 30–50%; ≥ 3/5; ≥ 60% of forks (`decision_time_ms`) | Sim + analytics + playtest |
| U11 | Abilities are places | Abilities that open a named place + journal entry; ability copy with numbers or percentages | 7/7; 0 | QA text check |
| U12 | The feeling | Unprompted "I want to get there" naming a specific locked place; "What's that?" about a creature or place | ≥ 3/5 each by run 5 | Playtest quotes |

## 17. Edge cases
| Situation | Result |
|---|---|
| Swipe up during a vault | Buffered 150 ms, runs on landing (jump; or double jump only if airborne after) |
| Hit by nothing during a vault, Shield active | Shield untouched (no contacts possible, V16) |
| Death during a vault, air grab, or burst | Revive point: vault zone start − 8 m; V2 takeoff funnel; gallery 8 m before BW1 |
| Air-grab catch on the same tick as a Perfect column coin | Coin counts, then the catch |
| Steer left after V2 without Vine Grip | Clamped to the Beam A knot; lands safe |
| Double jump used, then a vine grab | Grab works (grabs never need jumps); DJ resets on release |
| Double jump into a fork split | Branch decided by `x` at the divider front as always |
| Slide into BW1 with a Shield, without Shoulder Charge | Nudge, Shield not consumed (no contact) |
| Explorer Vision ends inside a secret | Nothing changes; the secret was already entered |
| Magnet active across a fork | Pulls only coins on the branch she is on after the split tick |
| Ripplekit pacing when Pista dies | Dives at once; observation stops at death |
| Veilhanger and a toast together | Toast queue (spec 103 AC-103-31) |
| Ability unlocked mid-session while a run is paused? | Impossible: LEARN exists only outside runs |
| Player owns Double Jump but not Vine Grip | Impossible by prerequisites |
| Crystal short for an affordable-coins ability | Results P3 copy shows "2 crystals short"; revive offer shows the crystal balance after the cost |

## 18. Assumptions `[ASSUMED]`
- USP rules: locked-place guarantee (1 by 1,000 m checked, 2 by 2,500 m), secret cadence (7 picks), USP checklist
  thresholds U1–U12.
- 15 discoveries: Plants 4 (whirlseed ambient only), Locations 6 (Stiltwood Gate dropped), Mysteries 2 + "?" teaser.
- Creatures: ripplekit (curious, river, mimics leaps, play bonus 20) and veilhanger (rare, CT-only); working labels and
  designs pending owner review.
- Ravine secret = Root Vault Sunshelf; Grotto nested secret = Shoulder Charge; Canopy `Default` holds both the Vine
  Grip high line (air grab, late release) and the Double Jump crown (high-ledge side-step); no `Crown` variant.
- Ability prerequisites §7.1, including discovery gates for Creature Tracking (2 creatures) and Double Jump (8 entries).
- Vault, air grab and brittle burst are context actions; brittle walls nudge, never crash.
- Rare discoveries give 100 coins + 4 crystals.
- Crystal flow 1,200–1,800 m (was ~1,000 m); coin upgrades with 4 purchasable levels at 250/500/900/1,500.
- Power-up weights 40/35/25 and DDA-driven spawn intervals.
- Environment affinity, set-piece spacing, best-distance pennant and milestone cairns.
- Results objective priorities P1–P6 with rotation.
- Reference player for pacing: 8 runs on day 1, 5 runs per day after.
- `H` variants with no fast-fall-only combos.
