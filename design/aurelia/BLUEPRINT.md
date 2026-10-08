# AURELIA: Master Game Design & Production Blueprint

**Source:** written by the owner, shared 2026-10-08 together with the vision board `design/aurelia/vision_board.png`.
This is the owner's own text, kept as the reference for the new direction. Agents turn it into the GDD and specs;
they don't edit this file.

- Working title: AURELIA
- Genre: endless adventure runner + exploration
- Platform: mobile (iOS / Android)
- Perspective: third-person
- Orientation: to be decided during prototype; landscape recommended for the wider traversal environment
- Business model: free-to-play, cosmetic-first monetization
- Core technology: game engine + procedural world/chunk system + lightweight backend for persistence, analytics and live ops

---

## Part I: The game in one page

**1.1 Concept.** An endless exploration runner set in an original alien ecosystem. The player controls an explorer
travelling continuously through a vast, beautiful, mysterious world. The player runs, jumps, slides, swims, climbs,
swings, glides, chooses routes, collects resources, discovers creatures, finds secrets, survives environmental events,
unlocks traversal abilities, and gradually reaches places that were previously inaccessible. The world is procedurally
assembled, so the journey can continue indefinitely. There is no "THE END". The world keeps unfolding.

**1.2 Core fantasy.** "I am exploring a world that is much bigger than I am." Not "I'm playing an obstacle course."
The runner mechanics are the vehicle for exploration.

**1.3 Core promise.** Every run makes the player wonder "What's around the next corner?" After an unlock:
"Where can I go now that I couldn't reach before?"

**1.4 Core loop.** Play → run → navigate → choose route → take risks → discover → collect → survive → fail/end run →
rewards → upgrade → unlock new possibility → run again.

## Part II: What AURELIA is not

It must be its own IP. It is NOT Avatar, Pandora, a Temple Run clone, a Subway Surfers clone, an open-world RPG,
a combat game, a horror game, or a generic sci-fi shooter. It may draw on broad concepts (beautiful alien ecosystems,
exploration, vertical environments, unusual creatures, giant natural structures, bioluminescence), but all characters,
creatures, locations, mythology, names, architecture, visual language, story, symbols and lore must be original.

## Part III: Design principles

1. **Beauty first.** The player should want to enter the world. The environment is a major reward.
2. **Movement must feel amazing.** If movement isn't satisfying, nothing else matters.
3. **Every run contains decisions.** The player shouldn't simply react; they choose.
4. **Risk produces meaningful reward.** Danger should be tempting.
5. **Never reveal everything.** Always leave something unexplored.
6. **Progression unlocks possibilities.** Prefer "Now I can reach that" over "+5% speed".
7. **Difficulty creates mastery.** "I'm becoming better", not "the game is cheating".
8. **The first version must be small.** One excellent ecosystem before ten mediocre ones.

## Part IV: Player experience

**4.1 First impression.** The first 10 seconds show lush vegetation, moving water, large-scale environment, unusual
creatures, distant landmarks, sunlight and depth. Then the character starts running. No long exposition or cinematic.

**4.2 First run teaches through play.** 0–30 s running · 30–60 s jumping · 60–90 s sliding and dodging ·
90–120 s first route choice · 2–3 min river · 3–4 min swimming · 4–5 min first traversal sequence ·
5–7 min first creature encounter · 7–10 min first secret · end: first upgrade.

## Part V: Core controls (validate in prototype)

- Horizontal movement: swipe/drag horizontally. Freedom across the traversable path rather than locked to three lanes.
- Jump: swipe up. Slide: swipe down. Dodge: horizontal swipe.
- Interaction: context-sensitive and automatic (approach a vine → the character grabs it). No tiny buttons.
- Special traversal is context-sensitive: the environment decides climb/swing/swim/glide; the player makes the
  important timing decisions.

## Part VI: Movement system

- **Running:** automatic forward motion; the player controls horizontal position, reactions, route selection, traversal timing.
- **Jumping:** roots, gaps, rocks, branches, obstacles, route transitions. Predictable arc, forgiving landing, optional double jump later.
- **Sliding:** low branches, fallen trees, rock formations, narrow passages. Can chain into a jump (slide → jump → land).
- **Swimming:** controls become swim direction, dive, surface, dodge. Water should feel different from running.
- **Climbing:** run → jump → grab → climb → jump off. Upgrades unlock harder climbing areas.
- **Swinging:** grab → swing → build momentum → release → land. The skill is timing the release; a perfect release gives bonus rewards.
- **Gliding (later):** launch from cliffs, trees, towers, platforms; choose high, low, collectible or shortcut route.
- **Traversal chaining:** run → jump → grab vine → swing → release → glide → land → slide → run. A major mastery mechanic.

## Part VII: Route system (the defining system)

- **Safe:** low difficulty, low reward. **Risky:** high difficulty, high reward. **Secret:** needs observation,
  ability or exploration; unknown reward.
- Example: canopy (high risk) above, forest and river to the sides, cave (secret) below; the player chooses.
- Readability: the player understands enough to decide, but not everything is obvious. Green = safe,
  gold/orange = high reward, blue/purple = mysterious/secret, shown as environmental cues, not big UI labels.

## Part VIII: Endless world

No final map or level. The world is built from reusable chunks.

**8.1 Chunk data:** entrance, exit, biome, difficulty, traversal requirements, obstacles, collectible configuration,
possible secrets, event compatibility, reward profile. Example `Forest_River_03`: entrance ground, exit water,
difficulty 3/10, routes safe/risky/secret, traversal jump + swim, possible event Creature Migration.

**8.2 Chunk categories:** straight, branch (route choice), challenge (high skill), discovery (hidden content),
transition (between biomes), event, recovery (lower difficulty; essential).

**8.3 Generation rules:** never create impossible gameplay. Every path must be reachable by the current player,
else rejected. The generator knows player abilities, required movement, difficulty, biome, speed, previous chunk.

**8.4 Difficulty-aware:** at difficulty 2, never "triple jump + wind + moving platform + underwater transition"
if those abilities aren't unlocked.

**8.5 Repetition prevention:** track recent chunks; avoid Forest → Forest → Forest unless intended.

## Part IX: Biomes

| Biome | Features | Difficulty |
|---|---|---|
| Verdant Forest (start) | dense vegetation, streams, giant roots, natural bridges, wildlife, sunlight | 1–4 |
| Riverlands | rivers, rapids, waterfalls, swimming, diving, underwater sections | 2–6 |
| Canopy | enormous branches, vines, climbing, swinging, huge drops | 4–7 |
| Sky Reaches | cliffs, airborne ecosystems, gliding, wind, aerial creatures | 6–9 |
| Deep Earth | caves, underground rivers, crystals, strange organisms, ancient structures | 6–10 |
| Ancient Wilds | ruins, mysterious structures, environmental puzzles, rare discoveries | 8–10 |

## Part X: Difficulty

Difficulty is not just "faster = harder". It comes from reaction speed, route complexity, traversal combinations,
hazards, visibility, decision pressure, risk/reward and event intensity.

**10.1 Phases:** Learning 0–500 m (simple obstacles) · Rhythm 500–1,500 m (combinations) · Decision 1,500–3,000 m
(more route choices) · Challenge 3,000–5,000 m (advanced traversal) · Danger 5,000–8,000 m (high-risk routes, events) ·
Mastery 8,000 m+ (very difficult combinations).

**10.2 Dimensions,** varied independently: reaction, navigation, traversal, risk, speed, environmental complexity
(e.g. "slow but difficult navigation" vs "fast but simple").

**10.3 Dynamic difficulty:** quietly learn the player's ability (average distance, collision frequency, missed jumps,
successful chains, route choices, death locations, restart frequency) and adjust future runs. Struggling players get
more recovery sections, clearer routes, fewer hard combinations; experts get more advanced combinations and riskier
routes. The player should not feel manipulated.

**10.4 Flow:** too easy = boring, too hard = frustration. Repeatedly move the player slightly beyond their comfort zone.

## Part XI: Obstacles (by biome)

- Forest: roots, rocks, fallen trees, gaps, branches, mud.
- River: rocks, rapids, submerged obstacles, currents, whirlpools.
- Canopy: broken branches, gaps, moving branches, hanging obstacles.
- Sky: wind, airborne debris, creatures, narrow landing areas.
- Cave: falling rocks, narrow passages, water, environmental hazards.
- Difficulty comes from combinations: root (jump) → low branch (slide) → gap (swing).

## Part XII: Environmental events

Rare, high-impact, visually spectacular, mechanically meaningful, different by biome: creature migration, flood,
storm, forest collapse, rockslide, giant creature encounter, ancient activation.

## Part XIII: Creatures

Part of the ecosystem, not all enemies. Categories: passive, curious, helpful (reveal info/routes), dangerous,
rare, giant (major events).

- **Discovery:** "NEW DISCOVERY · Unknown Species · Added to Journal", during movement, no need to stop.
- **Behavior:** move, react, interact, migrate, flee, investigate, sleep, feed. Not decoration.
- **Companions (later):** secret detection, collectible detection, route hints, cosmetic presence. Never pay-to-win.

## Part XIV: Discovery system (second pillar after movement)

Categories: creatures, plants, artifacts, locations, ruins, environmental phenomena, mysteries.

- **Journal example:** Creatures 18/60 · Plants 32/100 · Artifacts 11/50 · Locations 21/40 · Mysteries 7/?
  (the "?" creates curiosity).
- **Rarity:** common, uncommon, rare, epic, legendary, unknown (extremely rare).
- **Chains:** artifact A → clue → location B → creature C → ancient structure → mystery.

## Part XV: Secrets

Reward observation, not random collectibles: hidden cave, route behind a waterfall, branch hidden in vegetation,
underwater passage, unusual creature trail, ancient doorway, a path revealed by environmental behavior.
Goal: "I wouldn't have noticed that if I wasn't paying attention."

## Part XVI: Progression

- **Ability categories:** Movement (improved jump, double jump, dash, wall-run) · Water (deeper dive, faster swim,
  underwater endurance) · Air (glide, air dash, extended glide) · Exploration (secret detection, creature tracking,
  artifact detection).
- **Philosophy:** new possibility over small stat increase ("Unlock underwater caves", not "+3% swim speed").
- **Tree:** Explorer → Land (dash → wall run → advanced movement) · Water (dive → deep dive → underwater exploration) ·
  Air (glide → air dash → long glide exploration).

## Part XVII: Health and failure

- Minor collision: lose health/protection. Major fall: death. Severe event: damage or death by situation.
- 3 health segments; some power-ups restore/protect them.
- **Revive:** "Continue?" with earned resource, rewarded ad, or premium option later. Never mandatory.
- **Results screen:** EXPEDITION COMPLETE: distance, coins, crystals, discoveries, new creature, NEW RECORD, best,
  [RUN AGAIN]. Restart must be extremely fast.

## Part XVIII: Rewards

- **Coins:** common; basic upgrades, cosmetics, small utility items.
- **Crystals:** rare; advanced unlocks, premium cosmetics, special progression.
- **First-time discoveries:** currency, experience, journal progress, cosmetic rewards, ability progress.

## Part XIX: Power-ups

Magnet (attract coins), Shield (one collision), Surge (speed boost), Air Boost (longer glide), Water Dash
(fast swimming), Explorer Vision (highlight nearby secrets). Exciting, never mandatory.

## Parts XX–XXII: Missions, daily expeditions, weekly events

- **Missions:** movement (jump 30 times), exploration (discover 2 secrets), risk (take 3 risky routes),
  environment (swim 500 m), creature (encounter 2 rare creatures), distance (reach 5,000 m).
- **Daily expedition:** curated challenge, e.g. reach 5,000 m, collect 3 Golden Seeds, at most one health loss;
  exclusive cosmetic/resource reward.
- **Weekly event:** e.g. "The Great Migration" for seven days: discover creatures, complete challenges,
  collect event resources, leaderboard.

## Parts XXIII–XXV: Meta, collection, map

- **Expedition Camp** (home base, grows with progress): journal, gear, ability tree, creature sanctuary, map, workshop.
- **Collection:** each journal entry shows appearance, habitat, rarity, behavior, discovery location and date.
  The collection is a secondary game.
- **Personal exploration map:** known regions, discovered locations, biome progress, secrets found, unknown areas.
  Never reveal the whole procedural world: "There's still something over there."

## Parts XXVI–XXIX: Replay drivers, "almost", perfect routes, mastery

- **One-more-run reasons:** score, distance milestone, next ability, missing creature, journal completion,
  mastering a route, unknown region, beating a friend.
- **"Almost":** goals the player nearly reaches (4,870 / 5,000 m). Never artificially manipulate failure.
- **Perfect route:** a full traversal chain executed cleanly gives a bonus multiplier, rare currency, visual feedback,
  mastery progress. High skill ceiling.
- **Mastery tiers:** Beginner, Explorer, Adventurer, Expert, Master, Elite.

## Part XXX: Social

Initial: friends leaderboard (distance, expedition score, weekly performance). Later: ghost runs.
No real-time multiplayer early.

## Parts XXXI–XXXII: Monetization and seasons (only after retention is proven)

- Primary: cosmetics (outfits, backpacks, gliders, companion skins, trails, animations, cosmetic equipment).
- Optional rewarded ads (continue, double rewards, bonus discovery reward); the player chooses.
- Premium currency for cosmetics, optional convenience, seasonal content. No pay-to-win.
- Seasons later: cosmetic collection, limited creatures, events, challenges, environmental variation
  (e.g. "The Great Bloom").

## Parts XXXIII–XXXVI: Audio, music, haptics, camera

- **Audio** communicates the world: forest (leaves, insects, birds, distant water, footsteps), river (water, splashes,
  current), canopy (wind, branches, distant creatures), cave (echoes, dripping, low ambience); every creature has
  distinctive sounds.
- **Dynamic music:** adventure theme; intensity up for risk and danger; musical reveal for discovery; cinematic
  transition for giant creatures; relaxes in recovery.
- **Haptics (subtle):** landing, collision, rare item, perfect traversal, creature encounter, discovery, major event.
- **Camera:** third-person follow; slight FOV widening at speed; slight vertical adjust on jump; pulls back when
  gliding; widens for giant creatures; brief cinematic emphasis on discovery; avoid excessive shake.

## Parts XXXVII–XL: Art direction, world language, animation, environmental life

- **Visual target:** bright, cinematic, lush, colorful, natural, mysterious, hopeful.
- **Palette:** primary emerald, turquoise, gold, warm earth, sky blue; secondary violet, cyan, orange, magenta.
  Darker environments used selectively.
- **World language:** familiar (trees, water, mountains, clouds, plants, animals) + alien (impossible scale, unusual
  biological structures, strange creatures, reactive plants, ancient technology, unknown phenomena).
  "Familiar enough to understand. Strange enough to explore."
- **Character animations:** idle, run, sprint, jump, fall, land, slide, dodge, swim, dive, climb, grab, swing,
  release, glide, collision, damage, death, discovery reaction. Creature animation is equally important.
- **Living world:** plants react, move, open, close, release particles; animals migrate, flee, investigate, feed,
  interact; weather changes; water reacts to movement. "The world existed before the player arrived."

## Parts XLI–XLIII: Procedural architecture

- Game loop → **World Director** → biome / difficulty / events → chunk selector → route generator →
  obstacle generator → reward generator → world stream.
- **Chunk model:** ID, biome, difficulty, entry type, exit type, required ability, route options, obstacles,
  collectibles, discovery slots, event compatibility, reward profile, environment set.
- **World Director** inputs: distance, biome, player skill, abilities, recent chunks, recent difficulty,
  event cooldowns, discoveries, run state. Output: next chunk + route configuration + event probability.

## Parts XLIV–XLV: Analytics and difficulty telemetry

- Events: first_launch, tutorial_started, tutorial_completed, run_started, run_ended, death, restart, route_selected,
  route_completed, secret_found, creature_found, artifact_found, ability_unlocked, upgrade_purchased, daily_completed,
  rewarded_ad_watched, cosmetic_purchased.
- Metrics: D1 and D7 retention, session length, runs per session, average distance, restart rate, discovery rate,
  route selection rate, first-session completion.
- Difficulty telemetry: where players die, what killed them, route selected, time between decisions, failed and
  successful traversal, ability usage.

## Parts XLVI–L: Accessibility, performance, save, backend, cheat resistance

- **Accessibility:** left/right-handed options, sensitivity, reduced camera shake, reduced motion, audio cues,
  readable UI, color-safe route indicators, scalable text.
- **Performance:** 60 fps preferred, 30 fps supported on lower hardware; LOD, pooling, streaming, occlusion,
  optimized vegetation, texture compression, limited physics, efficient particles.
- **Save:** progression, abilities, currencies, cosmetics, journal, discoveries, achievements, best scores, settings.
  Run data is temporary.
- **Backend (small):** account, cloud save, leaderboard, analytics, remote config, seasonal content, anti-cheat
  validation for competitive scores.
- **Cheat resistance:** validate impossible distances, scores, traversal combinations, suspicious durations.
  Don't trust the client for competitive scores.

## Parts LI–LII: Home screen and first-time experience

- **Home:** AURELIA · [START RUN] · Best · Journal / Gear / Map · Daily Expedition · Camp. The main action is always RUN.
- **FTUE:** start immediately → learn movement → first route choice → first biome transition → first creature →
  first discovery → die naturally → first upgrade → run again. No registration wall before play.

## Part LIII: MVP (only this)

One forest biome · traversal: run, jump, slide, dodge, swim · 10–15 polished chunks · safe + risky + secret routes ·
environments: forest, river, waterfall, small canopy section · 3 creatures · 10–15 discoveries · coins + one rare
resource · 5–8 abilities/upgrades · 3 power-ups · basic dynamic difficulty · endless mode · results · basic journal.

## Part LIV: MVP development order

1 Movement (make running feel excellent) · 2 Camera · 3 Basic environment (one beautiful forest) · 4 Obstacles ·
5 Route system · 6 Procedural chunks · 7 River/swimming · 8 Canopy/vertical traversal · 9 Discovery (creatures,
secrets) · 10 Progression · 11 Dynamic difficulty · 12 Polish (audio, VFX, animation, haptics, UI, performance).

## Part LV: The 5-minute vertical slice (go/no-go gate)

Forest → obstacle → route choice → river → swimming → waterfall → vine → canopy → creature → secret →
difficult section → death → reward → upgrade → replay. **If this is not fun: stop, fix the gameplay, add no content.**

## Part LVI: MVP success criteria

Testers naturally say "Let me try again", "What happens if I take that route?", "I want to get there",
"I almost made it". These matter more than graphical fidelity.

## Parts LVII–LIX: Post-MVP, future, and what not to build early

- **Post-MVP:** more biomes, climbing, swinging, gliding, more creatures and secrets, daily expeditions, missions,
  expanded journal, camp, cosmetics, leaderboards, ghost runs.
- **Only after retention is proven:** seasonal events, season pass, advanced social, clubs, larger world systems,
  special modes, more traversal, larger mysteries, co-op, maybe multiplayer.
- **Do not build early:** real-time multiplayer, complex combat, weapons, an enormous open world, dozens of
  currencies, crafting, a complicated story, hundreds of creatures, player housing, a complicated backend, PvP.

## Parts LX–LXIII: Retention and emotional design

- **Time scales:** second-to-second movement mastery · minute route decisions · run-to-run score/rewards ·
  daily expedition · weekly challenges/leaderboard · long-term abilities/collection/exploration ·
  very long-term world mystery.
- **Loop:** curiosity → exploration → challenge → success → reward → progression → new possibility → curiosity.
- **Emotional journey:** wonder ("Beautiful") → learning ("I'm getting this") → excitement ("I can take that route")
  → mastery ("I can chain these movements") → curiosity ("What is that?") → attachment ("This is my world").
- **After every run show at least one compelling next objective** (record 4,870/5,000 m, next ability 80%,
  1 unknown creature, secrets 2/5, daily 1 remaining). Don't shout "PLAY AGAIN"; the information creates the desire.

## Part LXIV: Four pillars

1 **Movement** (run, jump, swim, climb, swing, glide) · 2 **Navigation** (multiple routes, meaningful choices) ·
3 **Discovery** (creatures, secrets, locations, mysteries) · 4 **Progression** (new abilities allow deeper
exploration). Everything else supports these.

## Part LXV: Complete loop

Start → enter world → run → challenge → choose route (safe → reward / risky → big reward / secret → discovery) →
keep exploring → environment changes → creature/event → go deeper → difficulty rises → fail/end → results
(rewards, discovery, record) → upgrade → new ability/goal → run again.

## Part LXVI: North star

Every feature must answer at least one: Does it make movement more fun? Exploration more interesting? Create
meaningful decisions? Curiosity? Mastery? A reason to return? If not, it probably doesn't belong in the core game.

## Part LXVII: Product in one paragraph

AURELIA is an endless mobile adventure where players run through a beautiful original alien ecosystem that
continuously changes around them. They make route decisions, cross rivers, climb enormous natural structures, swing
through forests, glide across open skies and discover creatures, ruins and secrets. As players progress, they unlock
new traversal abilities that open previously unreachable areas, while dynamic difficulty keeps challenging them
without feeling unfair. Each run combines skill, risk, discovery and progression: the player isn't just chasing a
score, they are gradually learning and exploring an enormous living world.

## Part LXVIII: Build strategy (never reverse this order)

1 Fun movement → 2 fun 60-second run → 3 fun 5-minute run → 4 route decisions → 5 procedural endless run →
6 difficulty → 7 discovery → 8 progression → 9 retention → 10 monetization → 11 content scale.
No monetization before retention, no 10 biomes before movement is fun, no multiplayer before the single-player
loop works, no hundreds of collectibles before players care about discovering them.

## Final definition

An endless exploration adventure disguised as a runner. The player runs because they want to know
"What's beyond there?", and once they find out, the game gives them another reason to go farther.
Movement gets the player started. Navigation keeps them engaged. Discovery makes them curious.
Progression gives them goals. Mastery makes them better. The endless world gives them somewhere to go.
