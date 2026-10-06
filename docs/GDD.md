# Game Design Document: JungleBooze (working title)

**Owner:** game-designer | **Status:** Fourth draft (after G0, G1, G2 and G6 name decisions, the quality-over-schedule rule, and alignment with the binding `design/STYLE_GUIDE.md`) | **Last updated:** 2026-10-06

This is the master design for a 3D endless runner on iOS. Every gameplay feature gets its own spec in
`docs/specs/<feature>.md` that refines the numbers here. When a spec and this document disagree, the spec wins
and this document is updated.

**Conventions in this document**
- The hero is **Pista**, the wild jungle girl (design H2 "Mapcloth"), and the companion is **Duko**, the macaw
  parrot (design M3 "Dusk") (owner decisions G1, G2 and G6, 2026-10-06; see section 1.1). "Mapcloth" and "Dusk"
  are design codenames, not names. Both names are **pending a proper trademark check** (section 17).
- Prose uses the names. `HERO` and `COMPANION` stay as **role labels** in tables, config assets, code and specs
  (for example `CompanionTuning`, the `HERO` shop slot), so nothing in code or config has to change if a name
  fails the trademark check.
- `[ASSUMED]` marks a default chosen by the designer so work can proceed. It stands until the owner decides otherwise.
- `[OWNER]` marks a decision the owner must make. Options are listed, nothing is decided.
- All tuning numbers are starting values. They live in ScriptableObjects under `Assets/_Game/Config` (section 16),
  never in code, and will be tuned with balance-simulator reports and owner feel checks.
- Units: meters (m), seconds (s), milliseconds (ms), meters per second (m/s), screen points (pt).

---

## 1. Vision in one paragraph

HERO, a barefoot, cat-like wild girl raised by the jungle's animals and dressed in clothes cut from an old
treasure map, runs, jumps and slides through an endless jungle that changes into a river, then mountains, then
ancient ruins the further she gets. It is a pulpy treasure-hunt adventure told in a bold, ink-outlined comic-book
style: warm golden light, pounding drums and brass, gold glinting in every ruin. The signature moment is
**vine swinging**: HERO leaps onto a vine, the camera pulls wide, and the player picks the perfect moment to let go
and fly over a chasm. COMPANION, a sly violet-and-orange macaw with a weakness for shiny coins, flies overhead,
calls out warnings ("Vine!", "Look out!") about what is coming, and fills up a meter that lets it swoop down and
lift HERO over trouble. Every session gives the player something:
coins, a finished mission, a step towards the next unlock, and a fair shot at beating their best score on Game Center.

### 1.1 Hero, companion, mood and art style (owner decisions, G1 and G2)

Full concept sheets: `design/HERO_CONCEPTS.md`.

| Item | Decision | What it means for design |
|---|---|---|
| **HERO** | Wild jungle kid (G1), design **H2 "Mapcloth"** (G2): a cat-like wild girl who grew up among animals, barefoot, fearless, playful. Clothes cut from an old canvas treasure map, with a **big "X" on her back**; round cloud of curly hair, teal sash, salvaged satchel. Name: `[OWNER]`, still open. | Moves like a young big cat: low, forward-leaning run with long strides, lane changes as sideways pounces, drops to hands and feet for a beat on landings, cat-like slide. Reacts to danger with a grin, not fear. Animations show joy (a whoop on a Perfect release, a roll on landing). Back-view landmark (seen 90% of the time): hair cloud plus cream map back with the "X". Because the run is already low, the slide pose must stay clearly lower than the run (1.8 m vs 0.8 m hitbox, section 6). Age reads as roughly 9–12; original design, no resemblance to famous jungle heroes. |
| **COMPANION** | Macaw parrot (G1), design **M3 "Dusk"** (G2): an original violet-and-orange macaw (violet body and wings, sunset-orange head and chest, teal tail tip), medium and sleek. Sly, greedy coin thief who loves anything shiny. **Speaks a few words** (G1), localized. Name: `[OWNER]`. | Flying fits the "above and behind" rule perfectly. Its short spoken call-outs are the main audio readability cue (section 15). Violet is complementary to the golden light, so it pops in every world and never clashes with hazard red or coin gold; no obstacle or hazard uses violet as a main color. The coin-thief personality is the story of Lift (it grabs every coin nearby, section 15.1). Medium size keeps it off the track during call-outs. |
| **Mood** | Pulpy adventure: warm golden light, drums and brass, treasure-hunt feel. | Every world is lit with a golden-hour rig (section 9.2). Coins are gold treasure (coins, gems, idols as visual variants with the same value). Music is one drums-and-brass theme rearranged per world. World transitions feel like discovering a new map area. |
| **Art style** | **C "Inkbound Pulp"** (G2): bold comic-book look with ink outlines. | Ink outlines strengthen silhouette readability at speed (pillar 1): hazards keep a thick outline plus the contrasting edge highlight (9.2). Kid proportions with a slightly larger head (about 1/4 of height). UI, banners and "Perfect!" pop-ups use comic-panel and lettering style. Outline rendering must fit the 60 fps budget on the lowest supported device (section 5.2). |
| **Platform** | iOS first, Android later. Lowest device for locked 60 fps: **iPhone 11 / iPhone SE (2nd gen)** (A13) (G0). iOS builds are compiled and signed on the owner's own Mac (G0). | All performance targets in this document are measured on an iPhone 11 / SE 2nd gen. |

---

## 2. Design pillars

1. **Readable at speed.** The player can always see why they died. Every hazard is telegraphed, and the game
   shows the cause after a death.
2. **One-thumb play, fair every time.** Four swipes and one tap gesture cover everything. No pattern is impossible,
   and forgiveness windows (buffer, coyote time, generous hitboxes) favor the player.
3. **Vine swinging is the signature moment.** It must look great in a 5-second clip: wide camera, big arc, a
   satisfying "Perfect!" release.
4. **Progress in every session.** Coins, missions, daily rewards and unlocks mean even a bad run moves the player
   forward.
5. **Respect the player.** No ads in the first session. Rewarded ads are always optional and always worth it.
   Nothing random is sold for money.

When two ideas conflict, the higher pillar wins.

---

## 3. Core loop (one run, 30 s to 5 min)

```
 Tap to play ─► Run: dodge, collect coins, grab power-ups
                  │
                  ├─► Vine section (every 35–70 s): grab, swing, release ─► coin shower + score bonus
                  │
                  ├─► COMPANION meter fills (near-misses, Perfect releases) ─► Lift (macaw carries HERO)
                  │
                  └─► Hit an obstacle ─► (optional) Continue ─► Results: score, coins, mission progress
                                                                         │
                                                                         └─► Play again (1 tap)
```

**What the player does every few seconds:** change lanes, jump or slide over/under an obstacle, line up a coin trail.
**Every 35–70 seconds:** a vine section, the high point of the run.
**Every 1,000–1,300 m:** a new world, with a short transition moment (section 9).

## 4. Meta loop (across sessions)

```
 Coins + mission rewards ─► Shop: unlock characters/outfits, upgrade power-ups
 Missions (3 active)     ─► Complete a set ─► permanent score multiplier +1
 Daily reward + daily challenge ─► reason to come back tomorrow
 Game Center leaderboards (all-time + weekly) ─► reason to beat friends
```

| Meta system | What it gives | Why it matters |
|---|---|---|
| Missions (section 13.2) | Coins and a permanent score multiplier | Gives each run a small goal beyond "go far" |
| Daily reward (13.3) | Coins, power-up head starts, outfit pieces on day 7 | Daily return habit |
| Daily challenge (13.3) | One special goal per day, bonus coins | Variety; a fresh reason to play today |
| Shop (13.4) | Characters, outfits, power-up upgrades | Long-term goals |
| Leaderboards (13.5) | Rank vs friends and everyone | Competitive players |

---

## 5. Controls and feel targets

### 5.1 Gestures

| Gesture | Action on ground | Action in the air | Action while swinging |
|---|---|---|---|
| Swipe left / right | Change lane | Change lane (air lane switch allowed) | Aim at the next vine in that lane (if there is one) |
| Swipe up | Jump | Buffered: jump on landing if within buffer | **Release** the vine |
| Swipe down | Slide | **Fast-fall**, then slide on landing | Ignored (buffered as slide for landing) |
| Double tap | Activate COMPANION Assist (when meter is full) | Same | Same |

- No virtual buttons on the play area. The whole screen is a swipe surface, except the pause button (top corner,
  safe area, mirrored for left-handed mode).
- Swipes are recognized **while the finger is still moving**, not on finger lift.
- Recognition: movement ≥ **28 pt** from touch start within **250 ms**. Direction is the dominant axis; diagonal
  swipes within ±15° of 45° use the axis with the larger component (ties go horizontal, because lane changes are
  the most common action).
- One touch makes at most one swipe. To swipe again the player lifts the finger, or keeps moving: a continuous
  drag re-arms after **28 pt more** in a new direction (allows fast left-then-up).
- Multi-touch: only the first active touch counts. Others are ignored.

### 5.2 Feel targets (measurable)

| Target | Value | How it is measured |
|---|---|---|
| Touch movement to swipe recognized | ≤ 1 frame after threshold crossed (≤ 17 ms at 60 fps) | EditMode test on gesture recognizer with timestamped samples |
| Swipe recognized to first visible movement | ≤ 1 rendered frame (≤ 17 ms at 60 fps); the simulation applies the first movement on the same tick it processes the command | PlayMode test (spec 001, AC-55) |
| Lane switch duration | **120 ms** authored = **7 ticks (117 ms)** at 60 Hz (ease-out), max 140 ms | PlayMode timing test |
| Input buffer (an action pressed too early still happens) | **150 ms = 9 ticks** | EditMode test |
| Coyote time (jump after leaving a ledge / end of a platform) | **80 ms** authored = **5 ticks (83 ms)** | EditMode test |
| Jump: apex height / total airtime | **1.5 m / 0.60 s (36 ticks)** at all speeds | EditMode physics test |
| Fast-fall: airborne to ground | ≤ **100 ms (6 ticks)** from any height (limit 120 ms) | EditMode test |
| Slide duration | **0.65 s (39 ticks)** (can be cancelled by a jump) | EditMode test |
| Hitbox forgiveness | Player hitbox width 0.7 m vs 0.9 m visual, depth 0.5 m; obstacle hitboxes 85% of visual | Fairness fuzzer + review |
| Lane change "edge forgiveness" | If the player is ≥ 60% of the way into a new lane, the old lane's obstacles cannot hit them | EditMode test |
| Frame rate | Locked 60 fps on the lowest supported device: iPhone 11 / iPhone SE (2nd gen), A13 (owner decision, G0) | perf-engineer benchmark |
| Death readability | 0.35 s hit-pause, then camera holds 0.8 s on the cause | PlayMode test + owner feel check |

### 5.3 Edge cases (rules every spec must follow)

1. **Swipe during a jump:** left/right switches lanes in the air. Up is buffered (150 ms) and fires on landing.
   Down triggers fast-fall.
2. **Swipe during a slide:** up cancels the slide into a jump immediately. Left/right switches lanes and keeps sliding.
   Down again restarts the slide timer.
3. **Two swipes in one lane switch:** the second lane switch queues and starts when the first is 50% done, so a
   fast double swipe goes two lanes in about 180 ms total.
4. **Swipe into a wall (edge lane):** no movement, a small 40 ms "bump" wobble and a light haptic so it never feels
   like a dropped input.
5. **Input buffer and hazards:** a buffered input is dropped if the action it would trigger is no longer possible
   (for example a buffered jump while now on a vine).
6. **Pause:** the game pauses on app background, incoming call, or Control Center. On resume, a 3-2-1 countdown
   (1.5 s total) plays before control returns. Buffers are cleared on pause.
7. **Ceiling during jump:** if a high obstacle would be hit at the top of a jump, the player dies (it is
   telegraphed). The generator never places a high obstacle where a jump is the only escape.
8. **Opposite swipe during a lane switch:** HERO reverses at once toward the lane she came from (a queued second
   switch is cancelled instead, if there is one).
9. **Collision categories [ASSUMED]:** running into the **front** of an obstacle (or rising into a high barrier from
   below) is lethal. Clipping the **side** of an obstacle during a lane switch, or coming down on **top** of a low
   barrier, is a **stumble**: HERO bounces back to the old lane (side) or scrambles over (top), and is dazed for 3 s.
   A second stumble while dazed is lethal ("Tripped twice"). Falling into a gap is lethal.

Full rules, tick values and tests: `docs/specs/001-player-movement.md`.

### 5.4 Haptics [ASSUMED]

Light: lane bump at edge, coin streak of 10. Medium: vine grab, Perfect release, power-up pickup, stumble.
Heavy: death. All haptics can be switched off in Settings.

---

## 6. Runner movement and camera

| Parameter | Value |
|---|---|
| Lanes | 3, width 2.4 m |
| Player hitbox | 0.7 m wide, 0.5 m deep, 1.8 m tall standing, 0.8 m tall sliding (simulation boxes, not physics colliders) |
| Camera | 6.0 m behind, 3.2 m above, looks 8 m ahead, FOV 60° (portrait) |
| Camera lane follow | Follows 70% of the player's lateral move, smooth time 90 ms |
| Visible distance | At least 1.6 s of track ahead at top speed (≥ 34 m at 21 m/s); fog starts beyond 45 m |
| Orientation | Portrait only [ASSUMED] |

---

## 7. Vine swinging (signature mechanic)

### 7.1 Player story and purpose

"I see glowing vines ahead, I jump, HERO catches the vine, the camera swings wide over a canyon, I let go at
exactly the right moment and fly through a ring of coins. I want to show someone that."
Vines break the rhythm of dodging, give the run a high point every minute, and create the shareable clip (pillar 3).

### 7.2 Section layout

A vine section is a pre-built chunk, 60–110 m long:

```
 [Approach 30 m: coins point to the vine lane, a signpost/glow marks it]
 [Grab zone]  vine 1 ─ swing arc ─ [release window] ─► (optional) vine 2 ─► vine 3
 [Landing pad 20 m: always safe, no obstacles for 1.0 s after landing]
```

- Each vine hangs over one lane. Its grab point is 3.0 m above the track, shown by a bright, color-blind-safe glow
  and a ring icon.
- Below the vine there is either **ground** (a "safe" vine: missing it only loses the bonus) or a **chasm** (missing
  it is a death). Early in a run and in the tutorial, only safe vines appear (section 11).
- Vine sections never spawn within 8 s of a world transition, and never while a speed boost is active (the
  generator waits until the boost ends).

### 7.3 Step by step

1. **Approach.** The approach strip has no obstacles in the vine lane for the last 1.2 s. Coin trails lead into
   the vine lane. COMPANION calls out 2.0 s before the grab zone: a bright spoken "Vine!" and a swoop over the
   vine lane (section 15.1).
2. **Grab.** The player swipes up (jump) so that HERO is airborne inside the grab zone (a box 2.0 m long,
   1.6 m wide, from 1.6 m to 3.6 m high, centered on the vine). Grab is **automatic** on entering the box while
   airborne. A jump started up to 0.45 s before the box counts, so there is a generous timing window.
   - Coyote rule applies: a jump within 80 ms after the approach ends still counts.
   - Being in the wrong lane = no grab. Lane switching in the air into the box still grabs.
3. **Swing.** HERO swings forward along a pendulum arc (visual radius 6 m) for **1.40 s** (swing phase 0.0 to 1.0).
   - Game speed stays the same in meters per second, but presentation slows to **0.8× for the first 0.3 s**
     (a "hang" moment) and the camera pulls back to FOV 70° and tilts 8°. (Turned off by Reduce Motion.)
   - The swing is **safe**: nothing can hit HERO while on a vine.
   - Swiping left/right during the swing **aims** at the next vine if one exists in that lane (shown by a glow).
     Without aiming, HERO aims at the next vine in the same lane, or the landing pad.
4. **Release.** The player swipes up during the release window:

   | Swing phase | Result | Effect |
   |---|---|---|
   | 0.00–0.45 | Too early | Ignored (buffered for 150 ms, so a slightly early swipe still lands in "Good") |
   | 0.45–0.66 | Good | Normal launch, coin shower, +150 score |
   | 0.66–0.80 | **Perfect** | Higher, longer launch through a bonus coin ring, +400 score, +25% COMPANION meter, gold flash, "Perfect!" |
   | 0.80–1.00 | Good | Normal launch |
   | No swipe by 1.00 | Auto-release | Short, safe launch, +50 score, no coin shower |

   The release window is visible: a ring around the hand fills and flashes gold in the Perfect band.
   Perfect band width: 0.14 of 1.40 s = **196 ms**.
5. **Chain.** If the next vine is ahead in the aimed lane, a Good/Perfect release launches into its grab zone
   automatically (no extra jump needed). Auto-release reaches the next vine only if it is a "safe" vine.
   Chains are up to 3 vines. Each Perfect in a chain raises the swing multiplier: ×1, ×1.5, ×2 on the score bonus.
6. **Landing.** HERO lands on the landing pad in the aimed lane. The landing pad is guaranteed clear for 1.0 s.
   A buffered swipe down on landing becomes a slide; a buffered up becomes a jump.

### 7.4 Edge cases

- **Missed vine over ground:** HERO lands normally and runs on. Score bonus lost, no penalty.
- **Missed vine over chasm:** HERO falls. 0.35 s hit-pause, camera shows the missed vine, death cause "Missed vine".
  The chasm edge has the same 80 ms coyote time as any ledge.
- **Power-up runs out while swinging:** timers keep counting, but the visible effect ends only after landing. A
  shield that expires mid-swing stays active until 0.5 s after landing (the player never loses a shield in the air).
- **Magnet during a swing:** pulls coins from the coin shower and the bonus ring.
- **Assist triggered during a swing:** queued and fires on landing (the macaw never grabs HERO off a vine).
- **Pause during a swing:** on resume the swing phase continues from where it was; the release ring is shown
  during the countdown so the player can re-time.
- **Continue after a chasm death:** HERO respawns on the landing pad, not on the vine.

### 7.5 Vine numbers

| Parameter | Starting value |
|---|---|
| Time between vine sections | 35–70 s of run time (random within seeded range) |
| First vine section in a run | 20–30 s after run start (after the tutorial: section 12) |
| Swing duration | 1.40 s |
| Perfect window | phase 0.66–0.80 (196 ms) |
| Good window | phase 0.45–0.66 and 0.80–1.00 |
| Grab timing window (jump earliness) | 0.45 s |
| Chain length | 1 to 3 vines (3 only at difficulty tier 4+) |
| Score bonus | Good 150 / Perfect 400 / Auto 50, × chain multiplier |
| Coin shower | Good 10 coins / Perfect 25 coins (ring) |
| Chasm vines allowed from | 600 m |

### 7.6 Simulation targets

- Average bot: vine grab success ≥ 90%; Perfect rate 25–40%.
- New bot: vine grab success ≥ 75%; "Missed vine" ≤ 15% of all deaths.
- Expert bot: Perfect rate ≥ 70%.

---

## 8. Obstacles

### 8.1 Archetypes (shared by all worlds)

Every obstacle in every world is one of these five types, so the player only has to learn five answers. Each world
reskins them. Color/shape language is consistent: **low = jump, high = slide, tall = change lane.**

| Type | Answer | Telegraph |
|---|---|---|
| Low barrier | Jump (or change lane) | Low, wide shape; warm highlight on top edge |
| High barrier | Slide (or change lane) | Hanging shape with a clear gap underneath; striped underside |
| Full block | Change lane | Tall, solid, fills the lane |
| Mover | Change lane timing | Moves between lanes on a fixed, visible path; shadow and sound cue 1.2 s ahead |
| Gap | Jump (or vine) | Visible edge, darker void, warning marker 1.0 s ahead |

### 8.2 Per world

| World | Low (jump) | High (slide) | Full block | Mover | World signature hazard |
|---|---|---|---|---|---|
| **Jungle** (deep green, golden light shafts) | Fallen log | Low branch | Giant tree trunk | Rolling boulder down a lane | **Thorn bush patch:** spans 2 lanes, forces the remaining lane |
| **River** (teal water, golden mist) | Floating driftwood | Hanging fishing net / rope | River rock | Drifting raft that slides between lanes | **Water spout:** erupts on a visible 1.5 s rhythm; pass when it is down |
| **Mountains** (sunrise gold on snow, cool shadows) | Snow drift | Overhanging ice ledge | Rock pillar | Rolling snowball | **Falling rocks:** shadow marks the lane 1.3 s before impact |
| **Ancient Ruins** (gold, stone, torchlight) | Broken column | Stone beam | Statue | Rolling stone disc | **Pressure-plate darts:** a lit plate in the lane warns 1.2 s before darts cross the lane |

### 8.3 Shared obstacle kit (how four worlds fit in seven weeks)

Four worlds are only possible on this schedule because the worlds share almost everything except their look.

- **One gameplay prefab per archetype.** Each of the 5 archetypes has one prefab with a fixed hitbox, height and
  telegraph. A world swaps only the visual mesh and material on top of it. Hitboxes never differ between worlds,
  so fairness tests and bot results carry over from one world to the next.
- **Restyle, don't remodel.** Per world and archetype: 1 visual variant at launch (20 obstacle visuals total),
  built from a shared base shape where possible (the log, ice ledge, stone beam and branch share one blockout).
  Extra variants per world are `C` (post-launch).
- **Two hazard behaviors, four skins.** The signature hazards are built from only two behaviors:
  - *Lane denial* (static, spans 2 lanes): Jungle thorn patch. It is the full-block prefab placed across 2 lanes.
  - *Telegraphed lane strike* (a warning in a lane, then danger for a fixed time): River water spout, Mountains
    falling rocks, Ruins pressure-plate darts. One component with tunable warning time, active time and rhythm.
- **Same chunk library everywhere.** The 40 m track chunks are world-neutral layouts. The generator places
  archetypes, and the current world decides the skin. Only the 4 gateway chunks (section 9) and the signature
  hazard chunks are world-specific.
- **Same vine.** One vine rig and swing animation set; per world only the material changes (jungle vine, river
  creeper, mountain rope, ruins chain).

Rules for every obstacle:
- The answer is visible at least **1.2 s** before impact at any speed.
- No obstacle is introduced for the first time without a solo "teaching" appearance (one obstacle, nothing else
  in view) the first time the player meets it in their life.
- Signature hazards first appear 150 m into their world, never in the first 10 s after a transition.
- Death screen shows the obstacle icon and name ("Hit: Low branch", or "Tripped twice: Low branch" after a stumble).

---

## 9. Worlds and transitions

The run passes through the worlds in a fixed order, then loops with a higher difficulty tier.

| World | Run distance [ASSUMED] |
|---|---|
| Jungle | 0–1,100 m |
| River | 1,100–2,300 m |
| Mountains | 2,300–3,600 m |
| Ancient Ruins | 3,600–5,000 m |
| Loop: Jungle (dusk lighting variant) | 5,000 m+, then River, and so on |

- **Transition:** a 4 s "gateway" chunk with no obstacles (a cave mouth, waterfall, rope bridge, temple gate),
  a banner with the world name, and a music crossfade. It always contains a coin arc and often a vine.
- With the distance targets in section 11, a new player sees Jungle, an average player usually reaches the River,
  and good players reach Mountains and Ruins. This gives a clear "how far can I get" goal.
- **Launch scope (owner decision, G1):** all four worlds ship at launch.

### 9.1 What each world gets at launch (and what is simplified)

| Per world | At launch | Simplified / shared |
|---|---|---|
| Obstacles | 5 archetype restyles + 1 signature hazard skin | Shared prefabs and hitboxes (section 8.3) |
| Environment | Modular kit: 1 ground tile set, 2 side set-piece modules, ~8 ambient props, skybox/gradient, fog color | No unique geometry per chunk; side dressing is placed by rules, not by hand |
| Lighting | One preset of the shared golden-hour rig (sun color, fog, ambient, bloom) | The dusk loop variant is a lighting preset only, no new art |
| Gateway | 1 unique gateway chunk (cave mouth, waterfall, rope bridge, temple gate) | Shares the transition script and banner UI |
| Music | 1 arrangement of the main drums-and-brass theme (stem swap) | One theme, four arrangements, not four separate tracks |
| Gameplay rules | Same as every world | No world-only mechanics at launch (no swimming, no ice sliding) |

Build order: **Jungle** (vertical slice, sets the quality bar), then **River** and **Ruins** (strongest treasure-hunt
mood), then **Mountains** (largest palette shift, most new materials). If quality is at risk, Mountains is the world
that gets the least extra dressing, never fewer gameplay features.

### 9.2 Mood and lighting

- One shared lighting rig with a warm key light (low sun, golden), soft fog and bloom on gold. Each world is a
  preset of this rig, so performance cost is the same in every world.
- Gold (coins, treasure, idols) is the brightest warm color in every world. Hazards use the shape language of 8.1
  plus a contrasting edge highlight and the bold ink outline of the "Inkbound Pulp" style (1.1) so they never blend
  with the warm light.
- Performance: only the current world's kit plus the next one are in memory. The next world loads in the
  background during the last 300 m of the current world; the previous world unloads after the gateway.

---

## 10. Power-ups

Power-ups appear as floating pickups in a lane, at most one on screen at a time, on average every **25 s**.
Picking one up while the same power-up is active **resets** its timer to full (no stacking). Different power-ups
can be active together.

| Power-up | Effect | Duration (level 1 → level 5) | Ends with |
|---|---|---|---|
| **Magnet** | Pulls coins from all 3 lanes within 10 m | 10 s → 20 s | Simple fade, 1 s warning flash |
| **Shield** | Absorbs one hit: the obstacle shatters, 1.0 s invulnerability after | 20 s → 40 s, or until hit | Bubble pops; 1 s warning flicker before timeout |
| **Speed Boost** | HERO dashes at 1.6× speed, invulnerable, auto-collects coins in the current lane, obstacles are smashed | 4 s → 8 s | 1.0 s slowdown with invulnerability, then a guaranteed 1.5 s clear stretch |

Edge cases:
- **Runs out mid-air:** Magnet and Shield end normally (shield rule in 7.4 applies on vines). Speed Boost never ends
  in the air: if it would, it extends until landing.
- **Speed Boost and gaps:** HERO auto-jumps gaps during a boost.
- **Speed Boost and vines:** a vine section is not spawned while a boost is active (7.2).
- **Shield and gaps/chasms:** a shield does **not** save from falling. Readability: the shield bubble does not glow
  over chasms. [ASSUMED; an option is to let the shield bounce HERO back up once.]
- **Upgrades:** each power-up has 5 levels bought with coins (section 14).

---

## 11. Difficulty ramp

Difficulty has three dials: **speed**, **density** (obstacles per 100 m), and **pattern tier** (which chunk
patterns may appear).

### 11.1 Speed by distance (linear between rows)

| Distance | Speed |
|---|---|
| Tutorial | 8.0 m/s |
| 0 m | 10.0 m/s |
| 500 m | 12.0 m/s |
| 1,100 m | 13.5 m/s |
| 2,300 m | 15.5 m/s |
| 3,600 m | 17.5 m/s |
| 5,000 m | 19.0 m/s |
| 7,000 m+ | 21.0 m/s (cap) |

### 11.2 Tiers

| Tier | From | Obstacles / 100 m | Min time between required actions | New in this tier |
|---|---|---|---|---|
| 1 | 0 m | 4 | 0.90 s | Single obstacles, safe vines only |
| 2 | 300 m | 6 | 0.75 s | Two-lane blocks, coin trails over obstacles |
| 3 | 600 m | 7 | 0.65 s | Movers, chasm vines, 2-vine chains |
| 4 | 1,500 m | 8 | 0.55 s | Combos (jump then slide), signature hazards everywhere, 3-vine chains |
| 5 | 3,000 m | 9 | 0.50 s | Mixed movers and combos |
| 6 | 5,000 m | 10 | 0.45 s | Full pattern pool |

- Chunks are 40 m pre-authored pieces, chosen by a seeded generator weighted by tier. The generator never picks a
  chunk that fails the solver (fairness fuzzing).
- **Breather:** after every 25–35 s of dense chunks, one 2 s chunk with only coins.
- **No spike rule:** density may not rise by more than one tier step within 200 m.

### 11.3 Simulation targets

- New-player bot median first run: **25–45 s**. Average bot median: **90–150 s**. Expert bot median: ≥ 300 s.
- 0 impossible segments in 100,000 generated.
- No single death cause > **35%** of deaths (per skill level).
- No 200 m stretch where average-bot death rate is more than 2× the curve trend (no spikes).

---

## 12. Onboarding: the first 60 seconds

Goal: playing within **10 s** of opening the app, no menus, no sign-up, no ads, no tracking prompt.

| Time | What happens |
|---|---|
| 0–8 s | Cold start, splash, title with HERO and COMPANION. "Tap to run." (No account, no settings, no ATT prompt.) |
| ~0 s of run | Run starts at 8 m/s. Empty track, HERO runs, COMPANION swoops down with a loud squawk and settles overhead. Its first call-out ("Vine!") is heard at the 30 s vine, so the player links word, swoop and event. |
| 3 s | A log in the middle lane. A ghost hand shows **swipe left or right**. The game slows to 30% until the player swipes. |
| 8 s | A low log across all lanes. Ghost hand: **swipe up**. Same slow-down. |
| 13 s | A low branch across all lanes. Ghost hand: **swipe down**. |
| 18 s | Coin trail across lanes. Text: "Grab coins!" |
| 23 s | Magnet pickup. Coins fly in. |
| 30 s | First vine (safe, over shallow water). Ghost hand on swipe up to grab, then a large release ring and "Swipe up when it glows!" with 30% slow-down at the Perfect band. |
| 40 s | COMPANION meter shown filled, ghost double tap: Assist. The macaw lifts HERO over a short row of logs. |
| 45 s | "You're on your own!" Speed rises to 10 m/s, normal tier 1 run starts. |

Tutorial rules:
- **No deaths in the tutorial.** A hit during the tutorial rewinds 2 s with a gentle hint ("Swipe up to jump!").
- The tutorial is skippable after it has been completed once, and replayable from Settings.
- First real death target: not before ~60 s total play time for a new player.
- The first **Results** screen highlights the first mission and the first daily reward. The shop opens later
  (after run 2) so the first session is about playing, not menus.
- The ATT pre-prompt is shown at the start of session 2, never in session 1 (owned by monetization-engineer).

---

## 13. Meta systems

### 13.1 Score

- Score = (distance in m × 1 + bonuses) × **score multiplier**.
- Bonuses: vine releases (section 7), near-miss +20 (passing within 0.35 m of an obstacle without contact),
  coin streak of 25 without missing +50.
- Score multiplier starts at ×1 and rises by +1 for each completed mission set, up to ×30.
- Coins and score are separate. Score is for leaderboards; coins are for spending.

### 13.2 Missions

- 3 missions active at a time, from a pool of templates with tiered targets.
- Completing a mission gives coins right away and the slot refills at the next Results screen.
- Completing all 3 missions of a **set** gives +1 score multiplier and a chest of coins (fixed contents, no random
  items).
- Mission examples: "Collect 300 coins in one run", "Swing on 5 vines", "Get 3 Perfect releases",
  "Run 1,000 m", "Slide under 20 branches", "Use 2 Shields", "Reach the River".
- Mission reward: 50 coins (set 1) rising by 10 per set, cap 250. Set reward: 200 coins + 50 per set number, cap 1,000.

### 13.3 Daily reward and daily challenge

- **7-day calendar**, claimed on the Home screen once per calendar day (local time):
  Day 1: 100 coins, Day 2: 150, Day 3: Head Start ×1, Day 4: 250, Day 5: Shield start ×1,
  Day 6: 400, Day 7: 750 coins + an exclusive outfit piece for the current week's theme.
- **Missing a day pauses the calendar; it does not reset.** (Pillar 5.) [ASSUMED]
- Rewarded ad on the daily reward: optional "double it" button (not in session 1).
- **Daily challenge:** one seeded goal per day (same for everyone), e.g. "Get 4 Perfect releases in one run".
  Reward: 300 coins.

### 13.4 Shop and unlocks

| Item | Price in coins | Also buyable with money |
|---|---|---|
| Character 2 (first unlock) | 2,500 | Yes |
| Characters 3–5 | 8,000 / 15,000 / 25,000 | Yes |
| Outfits (per character, 2 each) | 3,000–6,000 | Yes, some are money-only premium |
| Power-up upgrade level 2 / 3 / 4 / 5 | 500 / 1,500 / 4,000 / 10,000 | No (coins only, to keep upgrades fair) |
| Head Start (consumable: start with a 300 m dash) | 300 | No |
| Shield start (consumable) | 250 | No |

- Characters are **cosmetic only.** No stat differences, so leaderboards stay fair.
- No loot boxes and no random paid items.
- Character concepts are `[OWNER]`. Placeholder slots: HERO (default), CHARACTER_2 to CHARACTER_5.

### 13.5 Game Center

- Leaderboards: **Best Score (all-time)**, **Best Score (weekly, resets Monday)**, **Longest Distance**.
- Achievements (about 20): first vine, 10 Perfect releases, reach each world, 10,000 coins total, 7-day streak, etc.
- Game Center sign-in is offered after the first run, not forced. The game works fully without it.

---

## 14. Economy (starting numbers)

### 14.1 Coin income

| Source | Starting value |
|---|---|
| Coin density | About 3.5 coins per second of running at tier 1, rising to 5/s at tier 6 |
| Coin value | 1 (2 with the "Coin Doubler" option, if the owner wants it as a purchase) |
| Missions | 50–250 per mission, 200–1,000 per set |
| Daily reward | 100–750 per day (about 2,000 per week) |
| Daily challenge | 300 per day |
| Rewarded ad "double coins" at Results | Doubles the coins collected in that run (not mission rewards) |

### 14.2 Expected progress (to be proven by balance-simulator)

| Player | Coins per run | Runs per session | Coins per session (incl. missions, daily) |
|---|---|---|---|
| New (session 1–3) | 100–160 | 4 | ~700 |
| Average | 350–600 | 4 | ~2,000 |
| Expert | 1,200+ | 4 | ~5,000 |

### 14.3 Economy targets

- First character unlock (2,500): **3–5 sessions** with no ads and no purchases; 2–3 sessions with optional
  rewarded ads.
- First power-up upgrade (500): within the **first session**.
- All characters by coins only: about 60–90 days for an average daily player (the long goal).
- Continue costs never exceed one average run's coins.

### 14.4 Continue

- After a death, a **Continue** screen shows for 5 s (with a skip button):
  - Once per run: watch a rewarded ad to continue (not in session 1).
  - Or pay coins: 300 for the first continue, 600 for the second, max 2 continues per run.
  - Session 1 only: one free continue from COMPANION (the macaw swoops in, grabs HERO by the wrists and flaps her
    back onto the track) so the player learns continues exist.
- On continue: 2 s invulnerability, the obstacle that killed HERO is removed, 1.5 s clear stretch.

---

## 15. COMPANION

### 15.1 Role

COMPANION is a macaw parrot (design M3 "Dusk", section 1.1): violet and orange, loud, sly, greedy for anything
shiny, and HERO's oldest friend. It warns her because it does not want its treasure-finder hurt. It is a friend,
not a second character to control. It does three jobs:

1. **Calls out warnings (readability).** The macaw flies ahead a little and calls out big events: vines 2.0 s
   early, signature hazards and movers 1.5 s early. Each call-out is a **short spoken word with a squawk accent**
   (a parrot voice: the word is clipped and ends or starts in a squawk) plus a visible swoop over the lane that
   matters. Never on-screen text. There are only **3 call-out types** so players can learn them by ear:
   - *Vine call* (bright, rising): **"Vine!"**; the macaw swoops over the vine lane.
   - *Danger call* (sharp, urgent): **"Look out!"**; the macaw flaps over the dangerous lane.
   - *Cheer* (happy chatter): a short cheer word such as **"Shiny!"** or **"Wow!"** [ASSUMED wording] for a Perfect
     release, new record, mission complete.
   Rules for the words:
   - Each call-out is **1–2 words**, and the spoken part is **≤ 0.6 s** long so it never overlaps the next event.
   - The Vine and Danger calls use **one fixed word each**, always the same, so they work as learnable signals.
     Only Cheer and ambient chatter have 2–3 variants.
   - The squawk accent alone must still tell the three types apart (different pitch and rhythm), so the cue works
     for players who do not know the language.
   - Call-outs are an extra cue, never the only one: every hazard is still readable with sound off (pillar 1).
   - **Localization:** every word is localized into every launch language (the vine and danger words must stay
     short in every language; the localizer may pick a different short word rather than translate literally).
     With the "Companion voice" slider at 0, or for a language without recordings, a squawk-only fallback plays.
   - **Audio-director needs:** voice recording of all call-out words per launch language (one voice actor
     doing a parrot voice, or one voice processed into a parrot sound), plus squawk-only fallback versions of all
     3 types, plus ambient chatter lines. List of words and languages: `docs/specs/companion.md` (to be updated).
2. **Assist meter: "Lift" (one deeper mechanic, and the coin thief's favorite moment).** The meter fills from near-misses (+5%), coin streaks of 25
   (+5%), Good releases (+10%), Perfect releases (+25%). When full, a double tap triggers **Lift**:
   1. The macaw dives, grabs HERO's wrists and lifts her to a glide height of **2.5 m** above the track for
      **4.0 s**. HERO is invulnerable and passes over every ground obstacle.
   2. During Lift, left/right swipes still change lanes (steer for coins); up/down swipes are ignored. A magnet
      effect pulls coins from all 3 lanes within 10 m, so the player still feels in control and rewarded.
   3. In the last **0.6 s** the macaw descends and sets HERO down. The track is guaranteed clear for **1.0 s**
      after touchdown, and HERO keeps 0.5 s invulnerability after landing.
   If the player never double taps, the meter stays full (no waste, no auto-trigger) [ASSUMED].
3. **Emotional anchor.** Celebrates new records with a loud cheer and a loop-the-loop, lands on HERO's head after
   a death with a sympathetic squawk, hides a coin under a wing when idle on menus. This is the character players get attached to.

Placement: the macaw flies **above and behind HERO**, in the upper third of the screen, never between the camera
and the next 34 m of track at lane height, so it never blocks the view. Warning swoops stay at ≥ 4.0 m height
and last ≤ 0.5 s before it returns to its place.

Lift edge cases:
- **Double tap during a vine swing:** queued, fires on landing (7.4).
- **Lift would end over a gap or chasm:** extends until there is ground under HERO, then descends.
- **Lift and Speed Boost:** cannot overlap. Double tap during a boost is queued until the boost's clear stretch ends.
- **Lift and Shield:** the shield is not used up during Lift.
- **Vine section ahead during Lift:** the generator does not start a vine section during Lift; it waits, like
  Speed Boost (7.2).
- **Death:** impossible during Lift. If a death happens in the same frame as the double tap, the death wins
  (the input is not "rescued" after the fact).
- **Pause during Lift:** remaining time continues after the countdown.

Voice volume: the macaw has its own "Companion voice" slider in Settings (at 0, call-outs fall back to squawks
on the sound effects channel). Ambient chatter (not call-outs) is
limited to at most once every 8 s so the parrot stays charming, not annoying.

### 15.2 Numbers

| Parameter | Value |
|---|---|
| Lift duration (including descent) | 4.0 s |
| Lift glide height | 2.5 m |
| Lift descent | last 0.6 s |
| Clear stretch after touchdown | 1.0 s |
| Invulnerability after touchdown | 0.5 s |
| Lift coin pull radius | 10 m, all lanes |
| Meter to fill (at average play) | about 40–60 s |
| Vine call-out lead time | 2.0 s |
| Hazard / mover call-out lead time | 1.5 s |
| Spoken call-out length | 1–2 words, ≤ 0.6 s |
| Warning swoop height / duration | ≥ 4.0 m / ≤ 0.5 s |
| Ambient chatter cooldown | 8 s |

Feel targets: Lift starts (macaw visibly diving) on the same frame as the recognized double tap; HERO leaves the
ground within 150 ms. Acceptance criteria and bot simulation targets live in `docs/specs/companion.md`.
Simulation target: average bot uses Lift 2–4 times per 3-minute run; Lift never causes a death within 2 s after
touchdown in 100,000 generated segments.

### 15.3 Identity

Species (G1), look and personality (G2, M3 "Dusk") and voice (a few spoken words, G1) decided (section 1.1).
Name: `[OWNER]`. Extra companions as unlocks are possible
later (cosmetic only, they reuse the Lift mechanic, so they would need to be flying or carrying animals).

---

## 16. Config assets (ScriptableObjects in `Assets/_Game/Config`)

| Asset | Holds |
|---|---|
| `InputTuning` | Touch layer only: swipe threshold, recognition window, re-arm distance, tap limits, double tap window (300 ms) |
| `RunnerTuning` | Lane width, lane switch time, jump height/airtime, slide time, fast-fall, hitboxes, input buffer, coyote time, stumble (buffer and coyote live here because the simulation applies them for touch, bots and replays alike) |
| `RunnerPresentationTuning` | Camera offsets, follow and smoothing, hit-pause, death camera hold, bump wobble, stumble shake, run animation rate |
| `SpeedCurve` | Distance → speed table |
| `DifficultyTiers` | Tier thresholds, density, min action spacing, chunk weights |
| `VineTuning` | All of 7.5 |
| `PowerUpTuning` | Durations per level, spawn rate, Speed Boost multiplier |
| `CompanionTuning` | Meter gains, Lift duration/height/descent, clear stretch, call-out lead times, swoop limits, chatter cooldown |
| `EconomyConfig` | Prices, mission rewards, daily calendar, continue costs |
| `MissionPool` | Mission templates and tiered targets |
| `WorldSequence` | World order, lengths, transitions, preload distance (300 m) |
| `WorldSkin` (one per world) | Archetype visual variants, signature hazard skin, environment kit, lighting preset, music arrangement, vine material |
| `HazardTuning` | Lane-strike warning time, active time, rhythm per signature hazard |

---

## 17. Identity status

| Item | Status | Notes |
|---|---|---|
| HERO concept | **Decided** (G1): wild jungle kid | See 1.1. Must be original IP. No resemblance to famous jungle heroes. |
| HERO design | **Decided** (G2): H2 "Mapcloth", cat-like wild girl, treasure-map clothes, big "X" on her back | See 1.1 and `design/HERO_CONCEPTS.md`. Keep cheek dots pale clay, never red, and no fur (originality watch point). |
| HERO name | `[OWNER]` | Placeholder `HERO` until chosen. |
| COMPANION species | **Decided** (G1): macaw parrot | See 1.1 and 15. |
| COMPANION design, personality | **Decided** (G2): M3 "Dusk", violet-and-orange coin thief | See 1.1 and 15. Keep it a sleek macaw, not a tall rainbow bird (originality watch point). |
| COMPANION voice | **Decided** (G1): speaks a few words ("Vine!", "Look out!"), localized | See 15.1. audio-director plans voice recording per launch language plus squawk-only fallbacks. |
| COMPANION name | `[OWNER]` | Placeholder `COMPANION` until chosen. |
| Overall tone | **Decided** (G1): pulpy adventure | Warm golden light, treasure-hunt feel. |
| Music mood | **Decided** (G1): drums and brass | audio-director brings samples of the main theme and one world arrangement. |
| Art style | **Decided** (G2): C "Inkbound Pulp", bold comic-book look with ink outlines | See 1.1 and 9.2. |
| Lowest supported device | **Decided** (G0): iPhone 11 / iPhone SE (2nd gen), A13 | Locked 60 fps target (5.2). |
| iOS build machine | **Decided** (G0): owner's own Mac | Builds compiled and signed there. |
| App name | `[OWNER]` at G6 | See 17.1. |

### 17.1 Name risk

"JungleBooze" is the repository and working title only. "Booze" means alcohol in English. A public app name with
alcohol words can raise the age rating, change how the store and ad networks classify the app, limit which ads
can be shown, and clash with a game that stars a kid. **The public app name, store listing, icon and in-game
title must not contain "Booze" or any alcohol reference.** The repo name can stay. Compliance checks the
shortlist before G6.

---

## 18. Monetization placements

Principles: pillar 5. Nothing interrupts a run. No ads in session 1. Rewarded ads always optional and visibly worth it.

**Status:** the owner deferred ad and price decisions to week 4. Everything below is the designer's
recommendation and is `[ASSUMED]` until then. Recommendations [ASSUMED]:
- Launch with **rewarded ads only**; keep the interstitial row built but switched off by remote config, and decide
  after beta data. A kid hero and a "respect the player" pillar both argue against interstitials.
- Remove Ads: only meaningful if interstitials are on. If they stay off, sell a "Supporter Pack" instead
  (rewards without watching + a cosmetic outfit).
- No coin packs at launch; revisit after the economy is proven by simulation and beta.

| Placement | Type | When | Caps |
|---|---|---|---|
| Continue | Rewarded | Continue screen after a death | Once per run; from session 2 |
| Double coins | Rewarded | Results screen | Once per run; from session 2 |
| Double daily reward | Rewarded | Daily reward claim | Once per day; from session 2 |
| Free Head Start | Rewarded | Pre-run button on Home | 2 per day |
| Between runs | Interstitial (off at launch [ASSUMED]; owner decides in week 4) | After the Results screen, before Home | From session 2; at most 1 per 3 runs; ≥ 180 s apart; never after a run under 30 s, a purchase, or a rewarded ad |
| Remove Ads | IAP non-consumable | Shop + Settings | Removes all interstitials. Rewarded ads stay optional. [ASSUMED option: Remove Ads also grants rewards without watching, owner to decide] |
| Characters / outfits | IAP non-consumable | Shop | Prices from StoreKit only |
| Coin packs | IAP consumable (not at launch [ASSUMED]; owner decides in week 4) | Shop | Fixed contents, no random |
| Restore Purchases | Button | Shop and Settings | Required by Apple |

Price points are `[OWNER]`, deferred to week 4 (monetization-engineer and balance-simulator bring projections).
Starting recommendation [ASSUMED]: characters and outfits at the lowest two or three store price tiers;
Remove Ads or Supporter Pack one tier above the cheapest character.

---

## 19. Session flow

```
App launch (cold start < 5 s)
  └─ Session 1: Title ─► "Tap to run" ─► Tutorial run ─► Results ─► Play again ...
  └─ Session 2+: Title ─► (ATT pre-prompt, once, session 2) ─► Home
        Home: [Play] big button | Missions | Daily reward | Shop | Leaderboards | Settings
        Play ─► Run ─► Death ─► Continue? (5 s) ─► Results (score, coins, missions, double-coins)
              ─► [Play again] (1 tap, back in a run in < 2 s) or [Home]
```

- Restart from Results to running: **under 2 s.**
- Results screen shows: score, best, coins, death cause with icon, mission progress bars, "next unlock" progress bar.
- Settings: music, sound, Companion voice, haptics, left-handed UI, Reduce Motion, color-blind mode (adds shape markers),
  replay tutorial, Restore Purchases, privacy policy, credits.

---

## 20. Accessibility and readability

- Danger is coded by **shape and position** (low/high/tall), not color alone. Color-blind mode adds icons.
- Reduce Motion: no slow-motion, no camera tilt, smaller FOV change.
- All UI inside safe areas on every iPhone; left-handed mode mirrors the pause button and menus.
- Text minimum 15 pt; key HUD numbers 20 pt.

---

## 21. Analytics (minimum, after consent)

`session_start`, `run_end` (distance, score, coins, duration, world reached, death cause), `vine_result`
(grab/miss, release grade), `mission_complete`, `unlock`, `ad_rewarded`, `ad_interstitial`, `purchase`.
Used for tuning and the death-cause distribution check.

---

## 22. Feature list by week

Priorities: **M** = must have for launch, **S** = should have, **C** = could slip to an update.

| Week | Features | Priority | Spec file | Gate |
|---|---|---|---|---|
| 0 | GDD (this document), creative brief questions | M | `docs/GDD.md` | G1 |
| 1 | Gesture input (swipes, buffer, coyote), lanes, jump, slide, fast-fall, camera, deterministic core | M | `specs/001-player-movement.md` | G2 |
| 1 | Bot input provider with skill levels | M | `specs/bot-player.md` | |
| 2 | Track chunks + seeded generator, 5 obstacle archetypes as shared prefabs (gray-box), coins, speed curve, tiers | M | `specs/track-generation.md`, `specs/obstacles.md`, `specs/difficulty.md` | |
| 2 | `WorldSequence` + `WorldSkin` system and gateway transitions in gray-box: all 4 worlds playable as tinted gray-box (fog/lighting preset only) | M | `specs/worlds.md` | |
| 2 | Death + death cause display, basic Results, first simulations and fairness fuzzer | M | `specs/death-and-results.md` | G3 |
| 2 | Art: HERO and macaw models + core animation list started; Jungle environment kit started | M | `specs/worlds.md` | |
| 3 | **Vine swinging** (grab, swing, release grades, chains, chasms) | M | `specs/vine-swing.md` | |
| 3 | Power-ups: Magnet, Shield, Speed Boost | M | `specs/power-ups.md` | |
| 3 | COMPANION: flight follow, 3 spoken call-outs (placeholder recordings in one language, squawk-only fallback), Assist meter, Lift | M | `specs/companion.md` | |
| 3 | Signature hazards: lane-denial + telegraphed lane-strike behaviors (both, gray-box) | M | `specs/obstacles.md` | |
| 3 | Continue flow (coins + free first-session continue) | M | `specs/continue.md` | |
| 3 | Art: **Jungle complete** (vertical slice, quality bar); golden-hour lighting rig; main theme | M | `specs/worlds.md` | G4 |
| 4 | Home, Shop, character/outfit unlocks, power-up upgrades | M | `specs/shop-and-unlocks.md` | |
| 4 | Missions, daily reward, daily challenge | M / M / S | `specs/missions.md`, `specs/daily.md` | |
| 4 | Save + iCloud backup, Game Center leaderboards | M | `specs/save.md`, `specs/game-center.md` | |
| 4 | Art: **River + Ruins** restyles, environment kits, gateways, music arrangements; world streaming (preload/unload) | M | `specs/worlds.md` | |
| 4 | Owner decisions: ads, prices (section 18) | M | `specs/monetization.md` | G5 |
| 5 | Ads (rewarded; interstitial only if owner approves), IAP, Restore, ATT + consent | M | `specs/monetization.md` | |
| 5 | Onboarding tutorial (section 12) | M | `specs/onboarding.md` | |
| 5 | Audio, VFX, haptics, Settings, accessibility options; final localized COMPANION voice recordings | M | `specs/settings-accessibility.md` | |
| 5 | Art: **Mountains** restyle, kit, gateway, arrangement; dusk loop lighting preset | M | `specs/worlds.md` | |
| 5 | Game Center achievements | S | `specs/game-center.md` | G6 |
| 6 | Performance pass with all 4 worlds (memory per world, streaming hitches), device matrix, beta tuning | M | — | G7 |
| 7 | Bug fixes, compliance, submit | M | — | G8 |
| Later | Extra obstacle variants per world, world-only mechanics, extra companions, seasonal events, world start-point selection, Android | C | — | |

### 22.1 Four-world schedule risk (honest assessment)

The 7-week plan holds **only** with the simplifications in 8.3 and 9.1. Even then, these are the real risks:

1. **Art throughput is the critical path.** Weeks 4 and 5 each need full world kits (two worlds in week 4) while
   menus, shop and monetization UI also need art. If Jungle (week 3) takes longer than planned, every later world
   slips. Early warning: if Jungle is not at quality by the end of week 3, the plan is already a week behind.
2. **Mountains lands in week 5, leaving one week of tuning and performance work** before submission. Its first
   real beta feedback comes late.
3. **Performance and memory.** Four environment kits plus streaming between them is the most likely cause of
   hitches on the lowest supported iPhone (iPhone 11 / SE 2nd gen), together with the ink-outline rendering of the
   chosen art style. The streaming rule (9.2) must be built in week 4, not week 6.
4. **Test matrix grows ×4.** Every obstacle restyle needs a readability check (does the answer still read at
   1.2 s?) in every world. Shared prefabs keep the fairness tests valid, but visual readability must be checked
   by hand per world.
5. **Localized voice.** The talking macaw needs voice recordings in every launch language. Each extra language
   adds recording, editing and a timing check (≤ 0.6 s per call-out). The squawk-only fallback keeps a missing
   language from blocking launch.

Fallback options if a world is behind at the end of week 5 (owner chooses; see section 23): (a) slip launch by
one week, or (b) ship the late world with lighter dressing (fewer ambient props, simpler skybox), with the same
gameplay, and polish it in the first update. Cutting a world is not proposed, since the owner chose four.

---

## 23. Open questions for the owner

These are collected by the coordinator; answers go to `design/DECISIONS.md`.

Answered (2026-10-06, see `design/DECISIONS.md`):
- Platform: iOS first, Android later.
- G0: lowest supported device iPhone 11 / iPhone SE (2nd gen); iOS builds compiled and signed on the owner's own
  Mac; CI uses the owner's free Personal engine license for now.
- G1: HERO (wild jungle kid), COMPANION (macaw parrot), mood (pulpy adventure), launch worlds (all four), the macaw
  speaks a few localized words (section 15.1).
- G2: art style C "Inkbound Pulp", HERO design H2 "Mapcloth", COMPANION design M3 "Dusk" (section 1.1).

Still open:
1. Names for HERO and COMPANION (placeholders stay until then).
2. Ads (rewarded only, or plus light interstitials) and prices for Remove Ads / characters / coin packs:
   deferred by the owner to week 4. Designer recommendations are in section 18, marked `[ASSUMED]`.
3. If a world is behind schedule at the end of week 5, which fallback does the owner prefer: slip launch by one
   week, or ship that world with lighter dressing and polish it in the first update (section 22.1)?
4. Launch languages: which languages ship at launch? This sets how many COMPANION voice recordings are needed
   (section 15.1).

Smaller defaults marked `[ASSUMED]` in this document (portrait only, daily calendar pauses instead of resetting,
Assist by double tap, Lift as the Assist effect, shield does not save from chasms, rewarded-only ads at launch,
cheer wording)
stand unless the owner wants them changed.
