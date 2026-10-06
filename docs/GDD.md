# Game Design Document: JungleBooze (working title)

**Owner:** game-designer | **Status:** First draft (Week 0) | **Last updated:** 2026-10-06

This is the master design for a 3D endless runner on iOS. Every gameplay feature gets its own spec in
`docs/specs/<feature>.md` that refines the numbers here. When a spec and this document disagree, the spec wins
and this document is updated.

**Conventions in this document**
- `HERO` and `COMPANION` are placeholders. The owner has not yet chosen who they are (see section 17).
- `[ASSUMED]` marks a default chosen by the designer so work can proceed. It stands until the owner decides otherwise.
- `[OWNER]` marks a decision the owner must make. Options are listed, nothing is decided.
- All tuning numbers are starting values. They live in ScriptableObjects under `Assets/_Game/Config` (section 16),
  never in code, and will be tuned with balance-simulator reports and owner feel checks.
- Units: meters (m), seconds (s), milliseconds (ms), meters per second (m/s), screen points (pt).

---

## 1. Vision in one paragraph

HERO runs, jumps and slides through an endless jungle that changes into a river, then mountains, then ancient ruins
the further they get. The signature moment is **vine swinging**: HERO jumps onto a vine, the camera pulls wide,
and the player picks the perfect moment to let go and fly over a chasm. COMPANION, an animal friend, runs along,
cheers, and fills up a meter that triggers a helpful burst. Every session gives the player something: coins, a
finished mission, a step towards the next unlock, and a fair shot at beating their best score on Game Center.

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
                  ├─► COMPANION meter fills (near-misses, Perfect releases) ─► Assist burst
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
| Swipe recognized to first visible movement | Same frame (0 frames of delay) | PlayMode test: position changes on the frame of the input event |
| Lane switch duration | **120 ms** (ease-out), max 140 ms | PlayMode timing test |
| Input buffer (an action pressed too early still happens) | **150 ms** | EditMode test |
| Coyote time (jump after leaving a ledge / end of a platform) | **80 ms** | EditMode test |
| Jump: apex height / total airtime | **1.5 m / 0.60 s** at all speeds | EditMode physics test |
| Fast-fall: airborne to ground | ≤ **120 ms** from any height | EditMode test |
| Slide duration | **0.65 s** (can be cancelled by a jump) | EditMode test |
| Hitbox forgiveness | Player hitbox width 0.7 m vs 0.9 m visual; obstacle hitboxes 85% of visual | Fairness fuzzer + review |
| Lane change "edge forgiveness" | If the player is ≥ 60% of the way into a new lane, the old lane's obstacles cannot hit them | EditMode test |
| Frame rate | Locked 60 fps on the lowest supported iPhone | perf-engineer benchmark |
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

### 5.4 Haptics [ASSUMED]

Light: lane bump at edge, coin streak of 10. Medium: vine grab, Perfect release, power-up pickup.
Heavy: death. All haptics can be switched off in Settings.

---

## 6. Runner movement and camera

| Parameter | Value |
|---|---|
| Lanes | 3, width 2.4 m |
| Player capsule | 0.7 m wide, 1.8 m tall standing, 0.8 m tall sliding |
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
   the vine lane. COMPANION calls out (sound + gesture) 2.0 s before the grab zone.
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
- **Assist triggered during a swing:** queued and fires on landing.
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
| **Jungle** (green, warm light) | Fallen log | Low branch | Giant tree trunk | Rolling boulder down a lane | **Thorn bush patch:** spans 2 lanes, forces the remaining lane |
| **River** (blue, misty) | Floating driftwood | Hanging fishing net / rope | River rock | Drifting raft that slides between lanes | **Water spout:** erupts on a visible 1.5 s rhythm; pass when it is down |
| **Mountains** (gray, snow, cold light) | Snow drift | Overhanging ice ledge | Rock pillar | Rolling snowball | **Falling rocks:** shadow marks the lane 1.3 s before impact |
| **Ancient Ruins** (gold, stone, torchlight) | Broken column | Stone beam | Statue | Rolling stone disc | **Pressure-plate darts:** a lit plate in the lane warns 1.2 s before darts cross the lane |

Rules for every obstacle:
- The answer is visible at least **1.2 s** before impact at any speed.
- No obstacle is introduced for the first time without a solo "teaching" appearance (one obstacle, nothing else
  in view) the first time the player meets it in their life.
- Signature hazards first appear 150 m into their world, never in the first 10 s after a transition.
- Death screen shows the obstacle icon and name ("Hit: Low branch").

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
- Scope note: four worlds is a lot of art for a 7-week plan. If the owner chooses a smaller launch, the order of
  priority is Jungle, River, Ruins, Mountains (see section 23, question 4).

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
| ~0 s of run | Run starts at 8 m/s. Empty track, HERO runs, COMPANION joins with a short gesture. |
| 3 s | A log in the middle lane. A ghost hand shows **swipe left or right**. The game slows to 30% until the player swipes. |
| 8 s | A low log across all lanes. Ghost hand: **swipe up**. Same slow-down. |
| 13 s | A low branch across all lanes. Ghost hand: **swipe down**. |
| 18 s | Coin trail across lanes. Text: "Grab coins!" |
| 23 s | Magnet pickup. Coins fly in. |
| 30 s | First vine (safe, over shallow water). Ghost hand on swipe up to grab, then a large release ring and "Swipe up when it glows!" with 30% slow-down at the Perfect band. |
| 40 s | COMPANION meter shown filled, ghost double tap: Assist. |
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
  - Session 1 only: one free continue from COMPANION ("COMPANION pulls you back!") so the player learns it exists.
- On continue: 2 s invulnerability, the obstacle that killed HERO is removed, 1.5 s clear stretch.

---

## 15. COMPANION

### 15.1 Role

COMPANION is a friend, not a second character to control. It does three jobs:

1. **Reads the track for the player (readability).** COMPANION reacts to big events ahead: it calls out vines
   2.0 s early, chatters before a signature hazard, and cheers on Perfect releases. These cues are sound plus a
   visible gesture, never text.
2. **Assist meter (one deeper mechanic).** The meter fills from near-misses (+5%), coin streaks of 25 (+5%),
   Good releases (+10%), Perfect releases (+25%). When full, a double tap triggers **Assist**:
   COMPANION grabs every coin in all lanes for **4 s** and clears the next obstacle in HERO's lane.
   If the player never double taps, the meter stays full (no waste, no auto-trigger) [ASSUMED].
3. **Emotional anchor.** Celebrates new records, reacts to deaths with sympathy, appears on menus. This is the
   character players get attached to.

Placement: COMPANION travels **above and behind HERO** (flying, riding on HERO's shoulder, or swinging above),
never in front of HERO's lane, so it never blocks the view. This works for any species the owner picks.

### 15.2 Numbers

| Parameter | Value |
|---|---|
| Assist duration | 4 s |
| Meter to fill (at average play) | about 40–60 s |
| Vine call-out lead time | 2.0 s |
| Hazard call-out lead time | 1.5 s |

### 15.3 Identity

`[OWNER]` Species, name, personality, look. Extra companions as unlocks are possible later (cosmetic only).

---

## 16. Config assets (ScriptableObjects in `Assets/_Game/Config`)

| Asset | Holds |
|---|---|
| `InputTuning` | Swipe threshold, recognition window, buffer, coyote time, double tap window (300 ms) |
| `RunnerTuning` | Lane width, lane switch time, jump height/airtime, slide time, fast-fall, hitboxes |
| `SpeedCurve` | Distance → speed table |
| `DifficultyTiers` | Tier thresholds, density, min action spacing, chunk weights |
| `VineTuning` | All of 7.5 |
| `PowerUpTuning` | Durations per level, spawn rate, Speed Boost multiplier |
| `CompanionTuning` | Meter gains, Assist duration, call-out lead times |
| `EconomyConfig` | Prices, mission rewards, daily calendar, continue costs |
| `MissionPool` | Mission templates and tiered targets |
| `WorldSequence` | World order, lengths, transitions |

---

## 17. Identity placeholders (owner decides)

| Item | Status | Notes for options |
|---|---|---|
| HERO name, look, personality | `[OWNER]` | Must be original IP. No resemblance to famous jungle heroes. |
| COMPANION species, name, personality | `[OWNER]` | Any jungle animal works with the "above and behind" placement. |
| Art style | `[OWNER]` at G2 | art-director brings 3 style boards. |
| Music mood | `[OWNER]` | audio-director brings samples after the tone is chosen. |
| Overall tone | `[OWNER]` | Comedy cartoon / pulpy adventure / mysterious wonder. |
| App name | `[OWNER]` at G6 | Note: "Booze" means alcohol in English. It may affect the age rating and how the store and ad networks classify the app. Compliance should check before G6. |

---

## 18. Monetization placements

Principles: pillar 5. Nothing interrupts a run. No ads in session 1. Rewarded ads always optional and visibly worth it.

| Placement | Type | When | Caps |
|---|---|---|---|
| Continue | Rewarded | Continue screen after a death | Once per run; from session 2 |
| Double coins | Rewarded | Results screen | Once per run; from session 2 |
| Double daily reward | Rewarded | Daily reward claim | Once per day; from session 2 |
| Free Head Start | Rewarded | Pre-run button on Home | 2 per day |
| Between runs | Interstitial (`[OWNER]` whether to use at all) | After the Results screen, before Home | From session 2; at most 1 per 3 runs; ≥ 180 s apart; never after a run under 30 s, a purchase, or a rewarded ad |
| Remove Ads | IAP non-consumable | Shop + Settings | Removes all interstitials. Rewarded ads stay optional. [ASSUMED option: Remove Ads also grants rewards without watching, owner to decide] |
| Characters / outfits | IAP non-consumable | Shop | Prices from StoreKit only |
| Coin packs | IAP consumable (`[OWNER]` whether to sell) | Shop | Fixed contents, no random |
| Restore Purchases | Button | Shop and Settings | Required by Apple |

Price points are `[OWNER]` at G5 (monetization-engineer and balance-simulator bring projections).

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
- Settings: music, sound, haptics, left-handed UI, Reduce Motion, color-blind mode (adds shape markers),
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
| 1 | Gesture input (swipes, buffer, coyote), lanes, jump, slide, fast-fall, camera, deterministic core | M | `specs/input.md`, `specs/runner-movement.md` | G2 |
| 1 | Bot input provider with skill levels | M | `specs/bot-player.md` | |
| 2 | Track chunks + seeded generator, 5 obstacle archetypes (gray-box), coins, speed curve, tiers | M | `specs/track-generation.md`, `specs/obstacles.md`, `specs/difficulty.md` | |
| 2 | Death + death cause display, basic Results, first simulations and fairness fuzzer | M | `specs/death-and-results.md` | G3 |
| 3 | **Vine swinging** (grab, swing, release grades, chains, chasms) | M | `specs/vine-swing.md` | |
| 3 | Power-ups: Magnet, Shield, Speed Boost | M | `specs/power-ups.md` | |
| 3 | COMPANION: follow, call-outs, Assist meter | M | `specs/companion.md` | |
| 3 | Continue flow (coins + free first-session continue) | M | `specs/continue.md` | G4 |
| 4 | Home, Shop, character/outfit unlocks, power-up upgrades | M | `specs/shop-and-unlocks.md` | |
| 4 | Missions, daily reward, daily challenge | M / M / S | `specs/missions.md`, `specs/daily.md` | |
| 4 | Save + iCloud backup, Game Center leaderboards | M | `specs/save.md`, `specs/game-center.md` | |
| 4 | World transitions; Jungle + River art | M | `specs/worlds.md` | G5 |
| 5 | Ads (rewarded, capped interstitial), IAP, Restore, ATT + consent | M | `specs/monetization.md` | |
| 5 | Onboarding tutorial (section 12) | M | `specs/onboarding.md` | |
| 5 | Audio, VFX, haptics, Settings, accessibility options | M | `specs/settings-accessibility.md` | |
| 5 | Ruins and Mountains art; Game Center achievements | S / S | `specs/worlds.md` | G6 |
| 6 | Performance pass, device matrix, beta tuning from analytics | M | — | G7 |
| 7 | Bug fixes, compliance, submit | M | — | G8 |
| Later | Extra companions, seasonal events, world start-point selection, Android | C | — | |

---

## 23. Open questions for the owner

These are collected by the coordinator; answers go to `design/DECISIONS.md`.

1. Who is HERO? (look, age, personality)
2. Which animal is COMPANION, and what is its personality?
3. What overall tone and mood: comedy cartoon, pulpy adventure, or mysterious wonder?
4. Launch with all four worlds, or two worlds plus free updates?
5. How many ads are acceptable: rewarded only, or rewarded plus light interstitials?
6. Prices for Remove Ads and characters, and whether to sell coin packs.

Smaller defaults marked `[ASSUMED]` in this document (portrait only, daily calendar pauses instead of resetting,
Assist by double tap, shield does not save from chasms) stand unless the owner wants them changed.
