# Realistic Art Direction: AURELIA direction with Pista and Duko

> **Update 2026-10-08:** the owner dropped Duko the macaw (`design/DECISIONS.md`). Ignore section 8 and every Duko mention below; companions come later as discoverable creatures (blueprint Part XIII) with their own original concepts. The violet and orange route-cue colors stay.

**Owner:** art-director | **Status:** Draft for Phase 0 (look test). Items marked `[ASSUMED]` are art-director defaults
waiting for owner review | **Last updated:** 2026-10-08

**Inputs:** `design/aurelia/BLUEPRINT.md` (Parts I–II, VII, XIII, XXXVII–XL), `design/aurelia/vision_board.png` (target
mood), `design/DECISIONS.md` (2026-10-08: realistic, AI tools + CC0, no paid packs; keep Pista, Duko and the game name).
**Supersedes:** `archive/pre-aurelia/design/STYLE_GUIDE.md` (Inkbound Pulp) for everything visual. The old guide stays as the archive for
shape language and readability rules, which this document carries over where they still apply (section 9).

**Target hardware (owner update, 2026-10-08):** full quality on an **iPhone 12/13-class device (A14/A15)**. The
owner accepts raising the minimum device from iPhone 11 if polish needs it. This document does not simplify the look
for the A13. Section 11 lists exactly where the A13 would force a cut, so the owner and tech-architect can set the
minimum device with that list in hand.

Related files: `design/aurelia/LOOK_TEST_BRIEF.md` (Phase 0 scene contents and budgets), `design/prompts/aurelia/`
(prompt library), `design/aurelia/ENVIRONMENT_STRATEGY.md` (environment plan for look test v2).

---

## 1. Visual pillars

1. **Sunlit wonder, grounded in the real.** Everything looks like it could be photographed: real light, real
   materials, real weight. The wonder comes from scale and a few impossible things, not from glow or fantasy
   rendering. Blueprint: "Familiar enough to understand. Strange enough to explore."
2. **Depth you can read.** Every frame has three clear planes: the near path (sharp, warm, lit), the mid world
   (lush, slightly softened by humidity) and the far landmark (pale, hazy, huge). Atmosphere is the main tool for
   both beauty and readability.
3. **Water is the hero of the scenery.** Moving, turquoise, sparkling water appears in almost every view: rivers,
   falls, mist, wet rock. It is the most alive thing on screen and what people remember from a screenshot.
4. **The world tells you where to go.** Route choice is read from the environment (light, color, plants), never
   from big UI labels (blueprint Part VII). Safe = green, risky = gold, secret = blue/violet.
5. **The player always pops.** Pista, Duko, hazards and pickups win every value and color contrast fight against
   the background, even when the background is photoreal. Realism never comes before readability at speed.

## 2. What "realistic" means here (mobile runner definition)

"Realistic" means **photoreal materials and light at believable scale, art-directed for readability, rendered with
mobile-friendly techniques.** It does not mean film-level geometry or a simulation of every leaf.

| We keep real | We direct (deliberately not real) | We simplify for performance |
|---|---|---|
| PBR materials from scanned CC0 sources (rock, bark, soil, moss): real albedo values, real roughness | Saturation +10–15% over a neutral grade; greens pushed toward emerald, water toward turquoise (palette, section 3) | Static lighting is baked (lightmaps + light probes per chunk); one realtime sun shadow for characters and nearby dynamic props only `[ASSUMED, tech-architect decides]` |
| Grounded human and bird proportions (no big heads, no cartoon hands) | Aerial perspective stronger than in reality, so planes separate at phone size | Foliage is cards and low-poly clumps with tight-cut alpha; far trees are impostors / billboards |
| Physically plausible sun, sky and bounce light | A character-only rim and fill light so Pista's back is never lost in shade | God rays are additive mesh cards, not volumetric lighting |
| Real water behavior: transparency in shallows, color by depth, foam where it is fast | Pickups are self-lit and the brightest thing on screen (an intentional game convention) | Water is a custom shader: scrolling normals, depth tint, flow-map foam; waterfalls are scrolling flow meshes + mist cards |
| Real-world scale: Pista is ~1.65 m, a door-size gap reads as a door-size gap | Path surfaces have reduced micro-contrast (high-frequency texture noise reads as "blur" at speed) | Distant landmarks and the far range are matte-painted cards and low-poly silhouettes |
| Wet, dry, mossy, sunlit and shaded versions of the same material | Hazards are slightly darker and more angular than nature would make them | No realtime GI, no SSR; reflection probes are baked; screen-space effects limited to bloom + tonemapping + color grade |

**Quality bar test:** a still frame of the look test, viewed on the phone at arm's length, should be mistakable for a
screenshot of a premium console or PC game scaled down. A frame grab at full resolution can show simplifications
(card foliage, baked light); the phone view must not.

## 3. Palette (blueprint Part XXXVII)

Hex values are **grading and material targets** for the dominant tone of a surface in final lit frames, not flat
fills. Use them to check screenshots (eyedropper on a mid-lit area) and to brief AI tools.

### 3.1 Primary
| Name | Hex | Where |
|---|---|---|
| Emerald (foliage, lit) | `#2E8A57` | Sunlit leaves, moss tops, canopy |
| Emerald deep (foliage, shade) | `#16402D` | Understory, inner tree mass, shadowed ferns |
| Turquoise (shallow water) | `#38BFAE` | River shallows, plunge-pool edges |
| Turquoise deep (pool) | `#11666E` | Deep river channels, pool center |
| Gold (sunlight) | `#F4C35A` | Sun-hit surfaces, sun shafts, backlit leaf edges |
| Warm earth (path) | `#8A6142` | Packed soil path, root bark in light |
| Rootstone (signature rock, section 5) | `#A39079` | Arches, pillars, cliffs, boulders |
| Sky blue (zenith) | `#6FB6E8` | Upper sky |
| Haze (horizon and far planes) | `#CFE4EA` | Aerial perspective color; far landmark fades toward this |

### 3.2 Secondary (used in small doses)
| Name | Hex | Reserved for |
|---|---|---|
| Violet | `#6A4FB0` | **Secret route cues** (veilmoss, duskbells), Duko's body |
| Cyan | `#3FD3E3` | Secret cues in deep shade (mineral glint), water sparkle highlights |
| Orange | `#F08A2C` | **Risky route cues** (bellcap flowers), Duko's head, discovery highlights |
| Magenta | `#C4407F` | Rare flora and creature accents (journal-worthy things). Never on the path, never a route cue |

### 3.3 Game-reserved colors
| Name | Hex | Rule |
|---|---|---|
| Coin gold | `#FFD23F` + gem `#2EC4B6` | Kept from the old style guide. Coins are realistic gold metal with a turquoise gem, plus self-lit sparkle so they always read. Scenery gold is duller and never sparkles |
| Hazard accent | `#9E2238` (deep crimson) | Only on hazards: thorn tips, splinter faces, warning-bloom flowers on harmful plants. Never on Pista, Duko, pickups or safe scenery. Always paired with a shape cue (sharp, dark) for color-blind safety |

### 3.4 Value structure (darkest → brightest)
Hazard bodies and inner forest shade → side scenery → **Pista and Duko (mid values with a bright rim)** → path →
water highlights and sky → pickups (brightest, self-lit). Check every look-test frame in grayscale.

### 3.5 Darker environments
Blueprint: "used selectively". In the forest biome, darkness only appears in secret routes (caves, behind falls)
and deep understory. Never a whole dark chunk in the look test.

## 4. Lighting, time of day, atmosphere

### 4.1 Time of day `[ASSUMED]`
**Late morning, clear sky with a few towering cumulus.** Sun elevation ~35°, sun **ahead-left of the run direction**
(azimuth ~45° off the forward axis).
- Why: the camera looks partly into the sun, so foliage is backlit (leaf translucency glows gold-green), god rays
  are visible in the humid air, water sparkles ahead, and Pista gets a rim of light on her hair and left shoulder.
  This is the vision-board look.
- Risk: Pista's back is on the shade side. Fix: character fill light (soft, sky-colored, from camera direction,
  ~25% of sun intensity) affecting only the character layer, plus a warm rim from the sun side.
- Color temperature: sun ~5200 K (slightly warm), sky fill ~9000 K (cool). Shadows are cool blue-green, never black.

Alternative times for later biomes or events: golden hour (warmer, longer shadows) for "risky" set pieces, overcast
mist for Riverlands. Night is not used in the forest (section 5.2: no Pandora-style night glow).

### 4.2 Sky
- HDRI-based sky dome (Poly Haven CC0, picked in the look-test brief), color graded toward the palette, with
  painted cards for the distant landmark and far range.
- Cumulus clouds are part of the HDRI or painted cards. No planets, no rings, no visible second sun (section 5).

### 4.3 Atmosphere
| Element | Rule | Technique (mobile) |
|---|---|---|
| Aerial haze | Starts ~40 m, objects at 300 m+ reach ~70% haze color `#CFE4EA`. Haze is lighter and warmer toward the sun | Exponential distance fog + height fog in the shader (URP custom fog function), color gradient toward the sun direction |
| Ground mist | Low mist around water and in hollows, 0–2 m high | Soft particle cards, few and large, fade near camera |
| God rays | 2–4 shafts visible in forest views, angled from the sun side, never crossing the path's near 10 m (readability) | Additive mesh cards with a noise mask, fade by view angle and distance |
| Pollen / dust motes | Sparse gold motes in sun shafts | GPU particles, ≤ 60 on screen |
| Waterfall mist and spray | Big soft billow at the base, fine spray drifting with the wind, wet sheen on nearby rock | Flipbook particle cards + wetness mask on rock material |
| Rainbow | Optional, in the waterfall spray when the sun is behind the camera relative to the spray. Faint | One additive card. Owner can veto as "too sweet" |

### 4.4 Water
- **River:** clear turquoise shallows over pebbles, deep teal center, a bright sky reflection band at grazing angles,
  white foam lines where it moves fast or meets rocks. Flow direction always readable (scrolling normals + flow map).
- **Waterfall:** a white-turquoise ribbon with visible streaks and broken edges, a mist cloud at the base, a churned
  plunge pool, wet dark rock behind. Falls come out of rootstone (section 5.1), never from floating rock.
- **Reactivity:** Pista's footfalls in shallows spawn splashes and rings (blueprint: "water reacts to movement").

## 5. Original world visual language (not Avatar/Pandora)

Blueprint Part II rules out Avatar/Pandora explicitly. The vision board borrows several Pandora signatures. We keep
the board's **mood** (lush, sunlit, waterfalls, vast scale, wonder) and replace its **motifs** with our own.

### 5.1 Our signature motifs
1. **Rootstone.** The defining landform. Colossal roots of trees that died ages ago have turned to stone: twisted,
   rope-like strands of warm grey-ochre rock, braided like a tree's root flare. They form arches, pillars, natural
   bridges and cliff walls, coated in moss and ferns. **Always grounded:** rootstone grows out of the earth and
   returns to it. It explains the giant arches on the board without floating rock.
2. **Water from the stone.** Rootstone is partly hollow (old root channels), so springs and falls burst out of arch
   undersides and cliff faces, not only over edges. Familiar (springs exist) but strange at this scale.
3. **Stiltwoods.** The living forest: tall trees (30–60 m) standing on fans of stilt roots, so you can see and run
   between and under the roots. Based on real stilt-root trees, scaled up.
4. **Plants that breathe, not plants that glow.** Reactive plants (blueprint Part XL) react with **movement and
   particles**, not light: bellcaps exhale a puff of gold pollen as Pista passes, ribbon reeds bow away from her,
   veilmoss curtains sway and part. No glowing touch-reactive ground.
5. **Creatures built on familiar animal logic plus one twist.** Four limbs or wings, two eyes, real anatomy, and one
   impossible trait (scale, a gliding membrane, a seed that flies). Section 5.3 lists the bans.
6. **Map and compass motifs for anything made by people.** Pista's world has an old expedition history. Human-made
   traces (camp ruins, markers, the X) use cartographic motifs: compass roses, dotted trails, triangulation cairns.
   Ancient non-human ruins (later biome) get their own language, designed at that time, not copied from real
   cultures.

### 5.2 Board elements we change

| # | Vision-board element | Why it must change | What we use instead |
|---|---|---|---|
| 1 | **Floating mountains with waterfalls** pouring off them (hero image, Sky Reaches, Riverlands) | The single most recognizable Pandora image ("Hallelujah Mountains") | **Grounded rootstone pillars and arches** rising out of the forest and river, connected by natural root-bridges, wrapped in mist at their base so they *feel* weightless without floating. Falls come out of them (5.1, #2). Sky Reaches later: tall pillars and wind, still grounded |
| 2 | **Big blue planet / gas giant in the sky** (top of hero image) | Pandora's sky signature (Polyphemus) | **No planet.** A familiar blue sky with towering cumulus. Strangeness comes from the land (rootstone range on the horizon). No moon either (owner had no preference; art-director recommendation `[ASSUMED]`) |
| 3 | **Winged mount** (large blue-orange flying creature, rider implied) | Pandora's banshee/ikran and the bonded-rider fantasy | **No mounts.** Flight is Pista's own: the future glide ability uses a compact **wingsuit-kite glider** from her backpack, in her white/orange/charcoal colors. Ambient flyers are small (≤ 1 m wingspan) and non-rideable: the sailback glider lizard (look-test brief) |
| 4 | **Giant whale creature** (Dynamic Events: Giant Creature Encounter) | Pandora's tulkun (Avatar 2) | **A land giant** for later events, working label "mossback": a hill-sized, slow grazer with a rootstone-like armored back carrying its own garden, rainwater streaming off it. Not in Phase 0; concept later with owner options |
| 5 | **Bioluminescent purple/cyan forest** (Deep Earth tile, secret cave, mysteries tile) | Pandora's night forest with glowing, touch-reactive plants | Glow is **rare and mineral**, not plant-based: small cyan/violet mineral glints in secret spots and caves. No glowing footprints, no glowing jungle at night |
| 6 | **Spiral plants, fan lizards, floating seed jellyfish** (board details, companion panel) | Pandora's helicoradian, fan lizard, woodsprites | No spiral plants that retract. No spinning disc creatures. No glowing jellyfish seeds. Our floaters are **whirlseeds** (oversized winged seeds that spin as they fall, matte, tan-gold) |
| 7 | **Six-legged animals, double eyes, neck breathing holes, braid "bond" link** (risk in AI outputs, common in Pandora fan art) | Core Pandora creature anatomy | Banned in every creature prompt and review (section 10, checklist C4) |
| 8 | **Adult female explorer in tactical gear** ("The Explorer" panel) | Not our hero; also close to Lara Croft / generic AAA explorer | **Pista** (section 7) |
| 9 | **Companion creatures panel** (winged fox-dragon, glowing axolotl-floater, mossy tortoise) | Not our companion; fox-dragon and glowing floater sit close to Pandora and to Spyro/Pokemon looks | **Duko**, the violet-and-orange macaw (section 8). More companions are post-MVP and get original concepts |
| 10 | **AURELIA logo and UI chrome** (neon-blue glass panels) | The game keeps its current name; neon-glass sci-fi UI fights a sunlit natural world | UI direction is a later task: warm parchment/map materials + clean modern type is the starting idea `[ASSUMED]` |

**What we keep from the board:** the composition of the hero image (explorer seen from behind on a ledge, looking
over a vast valley of falls), turquoise water, golden light, emerald foliage, colorful flowers, scale, layered mist,
the route trio (safe/risky/secret), and gameplay frames (run, jump, slide, swing, swim).

### 5.3 Originality bans (all assets, all prompts)
Floating islands or mountains; planets, rings or two suns in the sky; blue-skinned or tall cat-like humanoids;
six-legged or four-eyed creatures; neck/chest breathing holes; hair-braid bonding links; rideable flying creatures;
glowing touch-reactive ground or plants; spiral retracting plants; glowing jellyfish-like floating seeds; giant whales;
"tree of souls" style glowing willow strands; the word "Pandora" or "Na'vi" anywhere in prompts or files. Also from
the old guide: no Tarzan, Mowgli, Moana, Lara Croft, Indiana Jones, Rio, Iago looks.

## 6. Route color cues as environment (blueprint Part VII)

Cues are **ambient and diegetic**: the player should understand them without a legend after one or two runs, and
they must still work in grayscale and for color-blind players (each cue has a shape/light cue, not only a hue).

| Route | Color | Environmental carriers | Non-color cue (accessibility) |
|---|---|---|---|
| **Safe** | Emerald green | Thick soft moss on the path edges, broad calm ferns, open even light, a wide packed-earth path | Wide, flat, open; rounded shapes; low contrast |
| **Risky** | Gold / orange | **Bellcap flowers** (orange-gold bells) lining the route, direct sun shafts hitting it, golden pollen in the air, more coins visible ahead | Narrow, elevated or broken (root ridges, gaps); high contrast hard light; visible drops |
| **Secret** | Blue / violet | **Veilmoss** (hanging violet-blue curtains) and **duskbells** (small violet flowers) around a half-hidden opening, cooler light, a faint cyan mineral glint deeper inside, cool air movement (mist drifting out) | Partly hidden (behind falls, under roots, through a curtain); a sound cue (deeper ambience) at Phase 2 |

Rules:
- Only route carriers use saturated orange or violet in a branch chunk. Ambient flowers elsewhere use other hues
  (white, pale yellow, magenta, coral) so the cue colors stay meaningful.
- Cue intensity scales with the player's distance to the fork: strongest 20–40 m before it.
- **Duko is violet.** That overlaps the secret color. We use it on purpose `[ASSUMED]`: Duko is the treasure finder,
  and later can hint at secrets (blueprint Part XIII: companions do secret detection). His orange head keeps him
  distinct from violet foliage. Review in the look test.

## 7. Pista in realism

**Owner decision (2026-10-08):** Pista keeps her name but is redesigned after the explorer on the vision board
("THE EXPLORER" panel and the hero of the key art), aged **16**. The stylized map-cloth design
(`archive/pre-aurelia/design/concepts/2026-10-07/hero_take1_turnaround.png`) is retired. At most a small nod to it survives as an
optional detail in one concept take.

### 7.1 Identity (what every take keeps)
- An athletic 16-year-old outdoor explorer. Fearless, curious and playful. She reads as a capable young expedition
  runner, not a soldier and not a glamour heroine.
- **Brown hair in a high ponytail.** It is the main back-view landmark and swings with every stride and jump.
- **Outfit color blocking: white / orange / charcoal.** A fitted athletic top with white and orange panels that covers
  the whole torso, charcoal technical trousers, a tan-brown leather utility harness with orange shoulder straps,
  a belt with pouches, fingerless gloves and sturdy brown hiking boots.
- **A technical backpack with orange accents**, compact and close to the back. This is the second back-view landmark.
  The camera sees it 90% of the time.
- Color targets: off-white `#ECE8E1`, expedition orange `#E8742A`, charcoal `#2E3136`, leather `#7A5232`, brown hair
  `#4A2E1E`. Her orange is a deeper, earthier tone than the bellcap risky cue `#F08A2C` and sits on a person, so it
  does not read as a route cue. Hazard crimson is banned on her.

### 7.2 What we change from the board (rules)
| Board explorer | Pista | Why |
|---|---|---|
| Crop top with a bare midriff | **Fitted athletic top that covers the whole torso** down to the waistband, short or three-quarter sleeves | She is 16; App Store safety; practical |
| Capri leggings that hug the body | **Technical trousers** (straight, slightly loose, knee patches or pads) | Practical, not glamour |
| Close to generic AAA explorers | Design choices that keep her original: high ponytail (never a single long braid), no thigh or twin holsters, no tank top and shorts, no weapons, an orange-and-white color story, a technical (not military) look | Avoids the Lara Croft read (braid, tank top, shorts, twin holsters) and also Aloy (red braids, tribal gear), Ellie (The Last of Us) and Chloe Frazer (Uncharted) |
| Face of the board hero | **An original face** that must not resemble a real actor, model or celebrity. Realistic AI faces can drift toward famous faces, so every face gets a resemblance check (checklist A2) | Originality and likeness rights |

The cheek dots are dropped everywhere. Of the old Pista design, only take B keeps a small optional nod: a faded
treasure-map patch with a small sepia X stitched on the backpack lid.

### 7.3 Skin tone, ethnicity, face
Default `[ASSUMED]`: follow the board's hero (warm light-tan skin, brown eyes, dark brown hair). This is an owner
question with options (agent report). The prompt keeps the skin and hair phrases in one token so they can be swapped
without changing anything else.

### 7.4 Age and App Store safety
- Age **16** (owner, 2026-10-08). Real adult-like proportions, ~1.65 m tall, athletic build.
- She is still a minor, so the rules from the stylized version stay: no bare midriff, no tight-fitting emphasis,
  no makeup or glamour lighting, no low upward camera angles, no posing that emphasizes the body. Key art shows
  action and exploration.
- **Failure presentation:** deaths are stumbles, splashes, slips out of frame and quick fades. No injury, no gore,
  no ragdoll limb-bending, no held pain expressions. This keeps the age rating low; appstore-compliance should confirm
  against `docs/APP_STORE_CHECKLIST.md`.
- No weapons. The utility harness carries rope, a compass and pouches, never holsters or knives.

### 7.5 Motion direction (for the phone-video mocap in P0-D)
Run: an athletic trail-runner's gait, forward lean, long strides, arms pumping close to the body, ponytail swinging.
Jump: a compact tuck with a reach. Slide: a low slide on one hip with one hand trailing. Landing: soft, absorbs with the
knees, one hand may touch the ground. A teen or adult athlete of similar height (~1.6–1.7 m) can be the actor, so
retargeting needs little scaling.

## 8. Duko in realism

### 8.1 Identity (locked)
An original, invented macaw species: **deep violet body and wings** `#5B3A8C`, **sunset-orange head and chest**
`#F28C28`, cream bare face patch `#F5EBDD`, dark gray beak `#3A3540`, **teal tail tip** `#2EC4B6`, long tail, sleek
medium build. Reference: `archive/pre-aurelia/design/concepts/2026-10-07/macaw_take1_turnaround.png`.

### 8.2 Realistic translation
- Real macaw anatomy: ~85 cm from beak to tail tip, ~1.0–1.1 m wingspan (about 3/4 of Pista's height), strong
  hooked beak, zygodactyl feet, bare cream facial skin with fine lines of tiny feathers (a real macaw trait that
  sells realism and is distinct from known species).
- Plumage: the violet has a slight blue-purple iridescent sheen in sun; inner wing coverts slightly lighter violet;
  orange grades from deep orange on the chest to brighter orange on the crown; the violet/orange border is a soft
  feathered transition, not a hard line. The teal is only on the last ~15% of the long tail feathers.
- Eyes: pale yellow iris (as in the locked concept), sly half-lidded expression in idle animations, not a cartoon
  face. Personality comes from head tilts, beak clicks and body language.
- Originality check: hyacinth macaw (all cobalt, yellow eye ring), Spix's macaw (Rio's Blu), scarlet macaw (Iago)
  are all clearly different. Watch item: keep the orange head; an all-violet bird drifts toward hyacinth macaw.
- 3D: feathers by normal map + a few alpha cards on wing tips and the tail end only. Flying is his main state in
  game, so the wings-spread silhouette is the primary read.

### 8.3 Readability
Seen from behind and above while he flies ahead of or above Pista. Violet reads against emerald (hue) and against
the sky (value), and orange head marks his direction of travel. He must not cover the path ahead in the lower 60% of
the screen during play (same rule as the old guide).

## 9. Camera framing (blueprint Part XXXVI)

| Parameter | Look-test default `[ASSUMED]` | Reason |
|---|---|---|
| Position | ~5.5 m behind, ~2.4 m above Pista's feet | Lower than the old 6 m / 3.2 m pulp camera: more horizon, more "world bigger than me" |
| Pitch | 8–10° down | Horizon in the upper third so the far landmark is visible |
| FOV | 55° vertical in landscape (~85° horizontal on a 19.5:9 screen); widens +6° at top speed | Cinematic, not fish-eye |
| Pista on screen | ~14–18% of screen height, horizontally centered, feet at ~28% from the bottom | **Small on screen**: the world is the star; still large enough to read poses |
| Duko | Ahead and above, upper-middle third, 25–40% from the top | Visible but off the path |
| Look-ahead | Path readable for ≥ 35 m (≈ 1.5–2 s at run speed) | Readability at speed |
| Damping | Soft lateral follow (Pista moves within the frame before the camera follows), no roll except a tiny bank on fast turns | Freedom-of-movement feel without nausea |
| Shake | None while running; small on hard landings only; off with "reduced motion" | Blueprint accessibility |

**Orientation (owner, 2026-10-08):** the look test is built in **both** orientations, switchable at runtime; the owner
decides on the phone. The framing above is for landscape. Portrait framing: Pista at ~12% of screen height, horizon at
35% from the top, FOV 65° vertical.

## 10. Consistency rules for AI-generated assets

### 10.1 Pipeline rules
1. **One locked reference per character.** After the owner picks a realistic take, that sheet becomes the reference
   image for every follow-up (key art, expressions, 3D input). Use the image edit endpoint with the locked sheet as
   input; never re-describe the character from scratch.
2. **Word-locked SUBJECT blocks.** The character text block is identical in every prompt (see
   `design/prompts/aurelia/README.md`). Only FORMAT and INTERPRETATION change.
3. **Neutral light for 3D inputs.** Turnarounds and 3D input images: flat, even, shadowless studio light on plain
   gray `#D9D9D9`. Golden light only in key art and environment concepts.
4. **Texture realism comes from CC0 scans where possible.** AI-generated textures are used only where no CC0 scan
   fits (invented plants, rootstone detail, creature skin), and always checked against a real reference value range.
5. **Everything ends up re-materialed in Unity** with the project's shared material set (section 10.2), so assets
   from different tools sit in the same light.

### 10.2 Material value ranges (PBR sanity)
| Property | Range |
|---|---|
| Albedo (sRGB) | Darkest natural 30–50 (wet soil, dark bark); brightest natural 200–240 (cream cloth, pale stone). Nothing pure black or pure white |
| Albedo lighting | No baked shadows, AO or highlights in albedo (Meshy `remove_lighting` / delighting step) |
| Roughness | Dry rock 0.75–0.95, wet rock 0.25–0.45, leaves 0.45–0.65, skin 0.45–0.6, canvas 0.8–0.95, leather 0.5–0.7, water 0.02–0.08 |
| Metallic | 0 everywhere except brass buckle/whistle cap and coins |
| Texel density | Path and near props: 512 px/m; mid scenery: 256 px/m; hero: ~1200 px/m (2048² over ~1.65 m tall); Duko: ~1000 px/m |

### 10.3 Style-lock checklist (every asset is reviewed against this)
An asset passes only if every applicable line is "yes". Record the result in the asset's review note.

**A. Identity and originality**
- A1. Character assets match the locked reference: Pista's high ponytail, white/orange/charcoal outfit, torso fully
  covered, harness, belt pouches, fingerless gloves, boots, backpack with orange accents (section 7.1); Duko's
  violet/orange/teal-tip pattern.
- A2. Nothing resembles a known character, creature, brand or franchise (section 5.3 bans; Tarzan, Mowgli, Moana,
  Lara Croft, Rio, Iago; Pandora list).
- A3. No readable text, logos, real-world flags, real tribal or sacred patterns.

**B. Realism**
- B1. Real-world scale checked against a 1.65 m Pista proxy.
- B2. PBR values inside section 10.2 ranges; no lighting baked into albedo.
- B3. Materials match neighbors: rootstone, bark, moss and soil look like the same world (same grade, same wetness
  logic).
- B4. No AI artifacts: melted geometry, extra fingers/toes/claws, fused feathers, smeared texture seams,
  asymmetric faces where symmetry is expected.

**C. Palette and world language**
- C1. Dominant tones sit near the palette targets (section 3) after the scene grade.
- C2. Route cue colors are used only by route carriers (orange = risky carriers, violet = secret carriers).
- C3. Hazard crimson appears only on hazards.
- C4. Creatures: four limbs or wings, two eyes, no breathing holes, no glow on skin, no braid links.
- C5. Shape language: scenery rounded and leaning away from the path; hazards angular, darker, leaning toward the
  player.

**D. Readability (in the look-test camera, on the phone)**
- D1. Squint test: at 64 px tall in grayscale, Pista's silhouette (high ponytail + backpack) reads.
- D2. Pickups are the brightest elements; hazards are darker than the path.
- D3. Nothing in the near 15 m of path has high-frequency texture noise that flickers at speed.
- D4. Duko never covers the path in the lower 60% of the frame.

**E. Technical**
- E1. Triangle, material and texture budgets (`LOOK_TEST_BRIEF.md` section 6) met; LODs present for anything visible
  beyond 40 m.
- E2. Pivot at the base, real-world scale (1 unit = 1 m), +Z forward, clean naming (`PREFIX_Name_Variant`).
- E3. Alpha-tested foliage cut tight to the leaf shape (overdraw), no large empty alpha areas.
- E4. Source, tool, plan and commercial-use license recorded in `docs/LICENSES.md` before the asset is committed.

## 11. Where the minimum device matters

Target is full quality on iPhone 12/13 (A14/A15). If the owner kept iPhone 11 (A13) as the 60 fps floor, these are
the art-visible cuts it would need. None are made in this direction.

| Feature | A14/A15 (target) | What A13 would need |
|---|---|---|
| Foliage density and overdraw | Dense layered understory with alpha-tested cards | ~30–40% fewer foliage cards in the near 30 m; visibly sparser forest edges |
| HDR + bloom on water sparkle and sun | On (subtle) | HDR off or bloom at quarter resolution; water loses its sparkle highlights |
| Realtime character shadow | One cascade, soft | Blob shadow only; Pista looks less grounded |
| Water depth tint / soft edges | Uses the depth texture | Vertex-painted depth only; harder shorelines |
| Render scale | ~0.85–0.9 of native | ~0.75; softer image |
| Hero texture set | 2048² | 1024²; face detail in menus suffers |

Tech-architect (P0-B) owns the final numbers and the device decision record.

## 12. Assumptions in this document
- `[ASSUMED]` Late-morning sun, ahead-left, 35° elevation.
- `[ASSUMED]` Pista's skin tone, ethnicity and face follow the board's hero (owner question open).
- `[ASSUMED]` Only take B keeps a nod to the old Pista (map patch with an X on the backpack lid).
- `[ASSUMED]` Duko's violet overlaps the secret cue on purpose.
- `[ASSUMED]` Camera 5.5 m back / 2.4 m up / 55° vertical FOV in landscape; portrait framing in section 9.
- `[ASSUMED]` Earth-like sky with no planet and no moon (owner had no preference).
- `[ASSUMED]` Hazard accent crimson `#9E2238` (replaces hazard red `#D7263D` in realism).
- `[ASSUMED]` Working labels: rootstone, stiltwood, bellcap, veilmoss, duskbell, ribbon reed, whirlseed, sailback,
  mossback. Lore names are the owner's call later.

## 13. Owner decisions and open questions
Decided 2026-10-08: Pista is 16, redesigned after the board's explorer; no cheek dots; look test in both
orientations; sky without a moon.
Open: Pista's skin tone, ethnicity and face (see the agent report); minimum device (section 11).
