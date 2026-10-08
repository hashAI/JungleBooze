# AURELIA: Game Design Document (MVP)

**Owner:** game-designer | **Status:** v1, Phase 1 | **Last updated:** 2026-10-09
**Binding source:** `design/aurelia/BLUEPRINT.md` (never deviate). Owner decisions: `design/DECISIONS.md`.
Look: `design/aurelia/ART_DIRECTION.md`. Items marked `[ASSUMED]` are designer defaults waiting for owner review.
Superseded: everything in `archive/pre-aurelia/` (lane-based game). **No lanes, no lane switches, no Duko, no macaw.**

Detailed specs:
- `design/aurelia/specs/101-movement-and-camera.md`: Phase 1 Feel (movement, input, collisions, health, camera)
- `design/aurelia/specs/102-chunks-routes-world-director.md`: Phase 2+ (chunks, routes, generator, World Director, difficulty)

Units: metres (m), seconds (s), points (pt = iOS logical points). Simulation runs at 60 fixed ticks/s.

---

## 1. Product in one line
An endless exploration adventure disguised as a runner: Pista, a 16-year-old explorer, runs through one original,
realistic, sunlit forest ecosystem, chooses routes, finds creatures and secrets, and unlocks abilities that open
places she couldn't reach before (Blueprint Parts I, LXVII).

## 2. Pillars
Blueprint Part LXIV, in priority order. Every feature must serve at least one (Part LXVI north-star test).
1. **Movement.** Run, jump, slide, dodge, swim (MVP). Must feel excellent before anything else is built.
2. **Navigation.** Every run has real route decisions (safe / risky / secret), read from the world, not from labels.
3. **Discovery.** Creatures, plants, locations and secrets, logged in a journal while running.
4. **Progression.** Abilities that open new places ("Now I can reach that"), never "+5%".

Guard rails: readable at speed (the player always sees why they failed); one-thumb play, fair every time;
"a world much bigger than me", not an obstacle course; respect the player (no monetization before retention).

## 3. MVP scope (Blueprint Part LIII) and build order
**In:** Verdant Forest only (forest, river, waterfall, small canopy section) · run, jump, slide, dodge, swim ·
12–15 polished chunks · safe/risky/secret routes · 3 creatures · 14 discoveries · coins + crystals · 7 abilities ·
3 power-ups · basic dynamic difficulty · endless mode · results · basic journal.
**Out (post-MVP):** climbing, gliding, double-jump-heavy content, other biomes, events beyond one, missions, daily
expeditions, camp growth, cosmetics, leaderboards, ghosts, ads, purchases.

**Build order (Parts LIV, LXVIII; never reversed):** 1 movement → 2 camera → 3 one beautiful forest → 4 obstacles →
5 routes → 6 procedural chunks → 7 river/swim → 8 canopy/vine → 9 discovery → 10 progression → 11 dynamic
difficulty → 12 polish. Phase gates as in `docs/STATUS.md` (Phase 1 Feel → Phase 2 Vertical slice → Phase 3 MVP).

## 4. Core loop
- **Second-to-second:** steer, jump, slide, dodge; read the path 1.5–2.5 s ahead.
- **Minute:** a route fork every ~20–40 s after the Learning phase; choose safe / risky / secret.
- **Run (target median 3–5 min for a new player, 8+ min for experienced):** distance record, coins, crystals,
  discoveries → results → one compelling next objective → upgrade → run again (Part LXV).
- **Long term:** abilities open secret routes and deeper content; journal completion; distance records.

Restart must be fast: results → new run in ≤ 1 tap and ≤ 1.5 s to control.

## 5. Controls (one thumb; details in spec 101 §3)
**Hybrid "Steer + Flick", free horizontal movement, no lanes.**
| Gesture | Action |
|---|---|
| Drag horizontally anywhere | Steer: Pista follows the finger relatively (0.040 m per pt), smoothly, across the whole path width |
| Quick horizontal flick | Dodge: a fast 2.2 m sidestep (for swipe-trained players and emergencies) |
| Swipe up | Jump |
| Swipe down | Slide (in the air: fast-fall, then slide on landing) |
| Tap | Nothing in MVP (reserved) |
| Context (automatic) | Vine grab, water entry/exit, ledge step-ups: the world decides, the player times (Part V) |

Swipes fire when the threshold is crossed, not on release. In landscape, a second thumb can swipe while the
first steers. Editor: keyboard and mouse (spec 101 §3.7).

## 6. Movement set (numbers in spec 101 §2)
| Move | MVP value (starting) | Notes |
|---|---|---|
| Forward run | 10.0 m/s → 16.0 m/s asymptote (half of the gain at ~2,080 m) | Automatic. Speed is one difficulty dimension, not the main one |
| Steering | max 11 m/s lateral, accel 150 m/s² | Full control on ground, in air, while sliding |
| Dodge | 2.2 m sidestep at up to 14 m/s | Can chain with steering |
| Jump | apex 1.40 m, airtime 0.60 s | Fixed, predictable arc; coyote 100 ms; buffer 150 ms |
| Slide | 0.65 s, hitbox 0.70 m tall | Jump cancels slide; swipe-down extends it |
| Fast-fall | ≥ 14 m/s down, then auto-slide | Swipe down in air |
| Swim (Phase 2) | spec 103 (to write) | Surface swim + dive; water must feel different |
| Vine swing (Phase 2) | spec 104 (to write) | Auto-grab, player times the release; perfect release bonus |
| Double jump | Ability (post-unlock only) | Never required on safe routes |

Steering never kills: path edges are soft walls. Deaths come only from gaps/falls and frontal crashes.

## 7. Camera (numbers in spec 101 §5)
Third-person follow from behind, low and wide so the world is the star: Pista ~14–18% of screen height
(landscape) / ~12% (portrait). Slight FOV widening with speed (+6°), small vertical follow on jump, slight dip on
slide, max 2° bank. Shake only on hard landings, hits and crashes, with hard limits and a Reduced Motion option.
**Orientation:** both landscape and portrait are built with their own camera profiles; the owner chooses on the
phone at the end of Phase 1 (decision 2026-10-08). Until then the build follows device rotation.

## 8. Routes (the defining system; spec 102 §3)
| Route | Difficulty | Reward | How it reads in the world (never UI labels) |
|---|---|---|---|
| **Safe** | Low (−2 vs chunk rating) | Coins ×1.0 | Emerald: wide flat packed-earth path, soft moss edges, calm ferns, even light |
| **Risky** | High (+2) | Coins ×2.0–2.5, crystal chance 15–25%, "Clean Line" bonus | Gold/orange: bellcap flowers, direct sun shafts, gold pollen, visible coin trails; narrow, raised or broken ground |
| **Secret** | Moderate, needs observation or an ability | Guaranteed discovery slot + 1–3 crystals; unknown until found | Blue/violet: veilmoss curtain, duskbells, cool mist drifting out, faint cyan mineral glint; half hidden (behind falls, under roots) |

- A fork is visible **≥ 2.5 s** before the split point; cue intensity peaks 20–40 m before it.
- Branch is chosen by Pista's lateral position at the split; fork dividers have a 0.6 m "nudge" zone so the
  player is never crashed by indecision (spec 102 §3.3).
- Every cue has a shape/light cue as well as a colour (colour-blind safe).
- **Clean Line** (Part XXVIII, MVP form): finish a risky branch with no damage → +50% of that branch's coins,
  a light haptic, a short gold trail VFX, counted for mastery.

## 9. Chunks and World Director (spec 102)
The world is a stream of hand-authored chunks (60–200 m) joined at standard seams. The World Director picks the next
chunk from distance, difficulty phase, skill estimate, abilities, recent history and cooldowns, using the seeded
`TrackGeneration` random stream. Every combination it can output was proven passable offline; nothing impossible is
ever generated (Part VIII). No chunk repeats within 6 chunks. Recovery chunks are mandatory (every 3–6 chunks by
phase and skill). MVP set: 14 chunks (spec 102 §9).

## 10. Difficulty
**Phases by distance (Part X):**
| Phase | Distance | Chunk rating band | Min gap between required actions | Forks |
|---|---|---|---|---|
| Learning | 0–500 m | 1–2 | 1.20 s | none (FTUE) / 1 optional |
| Rhythm | 500–1,500 m | 2–4 | 0.80 s | every 4–5 chunks |
| Decision | 1,500–3,000 m | 3–5 | 0.65 s | every 2–3 chunks |
| Challenge | 3,000–5,000 m | 4–7 | 0.55 s | every 2–3 chunks |
| Danger | 5,000–8,000 m | 6–8 | 0.50 s | every 2 chunks |
| Mastery | 8,000 m+ | 7–10 | 0.45 s | every 2 chunks |

Dimensions (reaction, navigation, traversal, risk, speed, environmental complexity) are rated per chunk and varied
independently (spec 102 §5).

**Dynamic difficulty (basic, Part 10.3):** a skill estimate `S` in [−1, +1] from the last 5 runs (distance vs
phase, hits per km, failed jumps/slides). It shifts the chunk band by up to ±1, recovery cadence by ±1 chunk and
route-cue intensity (0.85–1.30; struggling players get stronger cues). It changes at most 0.2 per run, applies at run
start, and never touches speed or movement numbers. In-run mercy: 2 hits within 15 s → the next chunk is a recovery chunk. Never manipulate a
death or a near-record (Part XXVII).

## 11. Health, failure, revive (spec 101 §4)
- **3 health segments.** Minor hit (trip on a low obstacle, head-clip a branch, side-clip a blocker, thorns) = −1
  segment, a stumble (speed ×0.80, recovers in 0.8 s) and 1.2 s invulnerability. Health 0 = run ends.
- **Major** (run ends at any health): falling into a gap/drop, frontal crash into a full-height blocker
  (lateral overlap > 0.45 m; anything less is a side-clip).
- **Regeneration:** +1 segment after 350 m without damage (max 3) `[ASSUMED]`.
- **Revive "Continue?":** costs crystals (1, then 2, then 4; max 3 per run), restores 3 segments, places Pista on
  the last safe ground with the next 30 m cleared, 2.0 s invulnerability. 4 s offer, skip always visible, never
  mandatory. Rewarded-ad revive only after retention is proven (Part XXXI) and never in MVP.
- Failure presentation: stumbles, splashes, slips out of frame, quick fades. No injury, no ragdoll, no pain faces.

## 12. Rewards and economy (starting values; balance-simulator tunes)
| Resource | Source | Use |
|---|---|---|
| **Coins** | Trails on all routes (~1 coin per 4.5 m on safe, ~2.2 per 4.5 m on risky) | Basic upgrades (power-up duration), ability unlocks (part) |
| **Crystals** (working name `[ASSUMED]`; lore name later) | ~1 per 1,000 m on safe flow, risky branches 15–25% chance, secrets 1–3, first-time discoveries 2 | Abilities, revive |
| First-time discovery | Journal entry + 50 coins + 2 crystals + ability progress | Part XVIII |

First run must afford the first ability (FTUE end): first ability costs 150 coins; a new player earns ~200–300 in
the scripted first run. Target: a new ability every 1–3 runs for the first 10 runs (sim target, spec 102 §8).

## 13. Progression: 7 abilities (Part XVI; "new possibility", never stats)
| # | Ability | Opens | Cost (coins / crystals) |
|---|---|---|---|
| 1 | Trail Sense | Secret cues pulse when within 60 m; first secret routes become findable | 150 / 0 |
| 2 | Vine Grip | Vine grabs in the canopy section → canopy risky route | 600 / 2 |
| 3 | Deep Breath | Dive under river obstacles → underwater secret passages | 1,200 / 4 |
| 4 | Root Vault | Vault onto raised roots (≤ 1.6 m) → high risky ledges | 2,000 / 6 |
| 5 | Creature Tracking | Tracks/sounds of nearby creatures; rare-creature encounters become possible | 3,000 / 8 |
| 6 | Shoulder Charge | Burst through brittle root walls while sliding → hidden chambers | 4,500 / 12 |
| 7 | Double Jump | Second jump in the air → upper canopy secret | 7,000 / 20 |

Coin-only basic upgrades: Magnet, Shield, Explorer Vision duration (5 levels each). Generator only offers chunks
whose required abilities the player owns; locked routes stay visible as "I want to get there" teasers.

## 14. Power-ups (3 for MVP, Part XIX)
| Power-up | Effect | Base duration |
|---|---|---|
| Magnet | Pulls coins within 4.0 m lateral / 8 m ahead | 10 s (upgrades to 18 s) |
| Shield | Absorbs the next minor hit or frontal crash (not falls) | Until used, max 30 s |
| Explorer Vision | Secret cues glow strongly, secret entrances get a cyan glint trail, discovery slots ping | 12 s (upgrades to 20 s) |
Spawn ~ every 600–900 m, more often on risky branches. Exciting, never required (Part XIX). Surge, Air Boost and
Water Dash are post-MVP.

## 15. Discoveries and journal (MVP size)
14 entries plus a "?" teaser. Categories: **Creatures 3/3** (e.g. sailback; two more from art-director),
**Plants 5/5** (bellcap, veilmoss, duskbell, ribbon reed, whirlseed), **Locations 4/4** (e.g. Falls Basin, Stiltwood
Gate, a hidden grotto, the canopy span), **Mysteries 2/?**. Working labels only; lore names are the owner's call.
- Discovery happens while running: a 2.0 s toast "NEW DISCOVERY · Unknown Species · Added to Journal", a short
  musical reveal, a light haptic. No stop, no slow-mo.
- Rarity: common/uncommon/rare in MVP; rare creature only with Creature Tracking.
- Journal entry: picture, habitat, rarity, behaviour, where/when discovered. Undiscovered entries show silhouettes.
- Creatures behave (move, react, flee, feed); one is helpful (its flight path reveals a secret entrance).

## 16. FTUE: the first run teaches through play
No text walls, no registration, no menu before the first run. App open → 3–5 s establishing shot (Part 4.1: lush
forest, moving water, distant rootstone landmark) → Pista starts running. The first run is a fixed seed, hand-ordered
chunk sequence:
| Time | Teaches | How |
|---|---|---|
| 0–30 s | Steering | Coin trails weaving across the wide path |
| 30–60 s | Jump | Roots; first one has slow-time help |
| 60–90 s | Slide, dodge | Low branches, a staggered rock pair |
| 90–120 s | First route choice | Wide safe vs gold-cued risky |
| 2–3 min | River | Ford, then riverside path |
| 3–4 min | Swim | Gentle swim section (Phase 2+) |
| 4–5 min | Traversal | First vine (Phase 2+) |
| end | Death or finish → first upgrade | Results points straight at Trail Sense |

**Slow-time help:** the first instance of each move (jump, slide, dodge, steer around a blocker) slows the world to
35% speed 0.6 s before contact if the player hasn't acted, shows a wordless animated hand glyph, and resumes on the
correct gesture. Max once per move, first run only. In the first 60 s, hits never end the run (health floors at 1).
Creature and secret beats (Part 4.2, 5–10 min) are spread over runs 2–3 via guaranteed early placement.

## 17. Results screen and next objective
"EXPEDITION COMPLETE": distance, coins, crystals, discoveries, new creature, NEW RECORD/best, then exactly **one**
highlighted next objective chosen by priority (first match wins):
1. Ability affordable now → "Trail Sense ready: 150/150" (button goes to the unlock).
2. Within 10% of the best distance → "4,870 / 5,000 m".
3. Next ability ≥ 60% funded → "Vine Grip 80%".
4. An unseen journal entry known to exist in reached territory → "1 unknown creature near the falls".
5. Secrets found in this biome → "Secrets 2/5".
Buttons: [RUN AGAIN] (primary, bottom centre, thumb reach), Upgrade, Journal. Results appear ≤ 1.0 s after the death
fade; RUN AGAIN → control in ≤ 1.5 s. Never shout "PLAY AGAIN" copy (Part LXIII).

## 18. Home screen
Title AURELIA (working title) · **[START RUN]** dominant · Best distance · Journal · Abilities · Settings.
Map, Gear, Daily Expedition, Camp are post-MVP slots (not shown as empty buttons). The live forest scene is the
background; Pista idles on the path. First-ever launch skips Home and goes straight into the run (§16).

## 19. Audio, haptics, camera feel targets
| Item | Target |
|---|---|
| SFX for jump/slide/dodge | starts on the tick of the action (≤ 1 rendered frame late) |
| Footsteps | surface-dependent (soil, moss, shallow water, wood) and synced to the run cycle |
| Ambience | forest layers (leaves, insects, birds, distant water) cross-fade by chunk; river layer near water |
| Music | adventure theme with intensity layers: +1 layer on risky branch, −1 in recovery, short reveal sting on discovery |
| Haptics | light: landing from ≥ 1.0 m, coin streak end, discovery; medium: minor hit; heavy: crash (once). Max 1 haptic per 100 ms. Toggle in settings |
| Camera | see §7 and spec 101 §5; no shake while running |

## 20. Accessibility (Part XLVI)
Steering sensitivity 0.5×–2.0× · left/right-handed (pause and HUD side swap) · Reduced Camera Shake (default on for
Reduced Motion users) · Reduced Motion (no shake, half FOV kick, no bank, no slow-time zoom) · haptics toggle ·
separate music/SFX/ambience volume · audio cues for forks and secrets · colour-safe route cues (shape + light) ·
Dynamic Type-aware UI text (min 13 pt, scales to 200%) · all actions reachable with one thumb.

## 21. App Store-safe design
- Age rating target **9+** `[ASSUMED]` (mild peril, no violence, no gore, no user content, no chat).
- No tracking, no ATT prompt (owner decision 2026-10-06); no third-party analytics or ad SDK in MVP.
- No loot boxes or paid random rewards, ever. No real-money purchases in MVP.
- Rewarded ads only after retention is proven, opt-in only, never in the first session, never for kids-targeted audiences.
- No registration or login wall; Game Center optional later.
- Pista is 16: art rules in `ART_DIRECTION.md` §7.4 apply to every screen and store asset.
- appstore-compliance checks every data, ad or purchase feature against `docs/APP_STORE_CHECKLIST.md`.

## 22. Analytics events (Parts XLIV–XLV; local-first, no PII)
Stored on device in MVP; uploaded to an owner-controlled endpoint only for TestFlight builds `[ASSUMED]`, declared in
the privacy labels. Every event carries `build`, `session_id` (random per launch), `run_id`, `t_ms`.
| Event | Properties |
|---|---|
| first_launch | device_class, orientation |
| tutorial_started / tutorial_completed | step reached, seconds |
| run_started | run_index, seed, abilities_mask, skill_S, orientation |
| run_ended | distance_m, duration_s, coins, crystals, hits, cause, revives |
| death | cause (fall/crash/health), obstacle_id, chunk_id, distance_m, phase |
| restart | seconds_from_results |
| route_selected / route_completed | chunk_id, route (safe/risky/secret), clean, decision_time_ms |
| secret_found / creature_found / artifact_found | entry_id, first_time, chunk_id |
| ability_unlocked / upgrade_purchased | id, cost, run_index |
| traversal_result | type (jump/slide/dodge/vine), success, obstacle_id |
| power_up | id, picked/expired/used |
| revive_offered / revive_used | cost, index |
| daily_completed, rewarded_ad_watched, cosmetic_purchased | reserved, post-MVP |
KPIs: D1 ≥ 40%, D7 ≥ 15% (Phase 4 gate), runs/session, restart rate, median distance, route mix, discovery rate,
first-session completion (reached first upgrade).

## 23. Assumptions in this document
- `[ASSUMED]` Hybrid Steer + Flick controls; relative drag 0.040 m/pt.
- `[ASSUMED]` Steering never kills (soft path edges); only falls and frontal crashes are major.
- `[ASSUMED]` +1 health per 350 m undamaged; revive costs 1/2/4 crystals, max 3.
- `[ASSUMED]` MVP power-ups: Magnet, Shield, Explorer Vision.
- `[ASSUMED]` The 7 abilities, names and costs in §13 (working names).
- `[ASSUMED]` Rare resource working name "Crystals".
- `[ASSUMED]` Age rating target 9+.
- `[ASSUMED]` FTUE slow-time help and health floor in the first 60 s.
- `[ASSUMED]` Analytics local-first; TestFlight-only upload to an owner endpoint.
