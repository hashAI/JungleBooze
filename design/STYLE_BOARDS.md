# Style Boards (Gate G2): pick 1 of 3

**Owner:** art-director | **Status:** Options for the owner, nothing chosen | **Last updated:** 2026-10-06

Fixed by the owner (see `design/DECISIONS.md`): pulpy adventure mood (warm golden light, drums and brass,
treasure-hunt feel), wild jungle kid hero, macaw companion, 4 worlds (Jungle, River, Mountains, Ancient Ruins).
All three boards below fit that mood. They differ in **how** the world is drawn.

Image prompts for each board are in `design/prompts/style_boards.md`. No images exist yet: this environment
has no image-generation key. Run the prompts once the key from G0 is available, then review the images with
this document side by side.

---

## Rules every board follows (readability first)

These rules hold no matter which board is chosen, so gameplay is not affected by the choice.

| Element | Rule | Why |
|---|---|---|
| Hazards | Darker and less saturated than the path, plus one **hazard accent**: hazard red `#D7263D` on the edge that matters (top edge of low barriers, striped underside of high barriers). Shape tells the answer (low = jump, high = slide, tall = change lane, GDD 8.1). | One danger color for the whole game. Never use hazard red on scenery, coins, or the hero. |
| Coins | Brightest, most saturated thing on screen. Gold disc with a **turquoise gem center** `#2EC4B6` and a white sparkle, always spinning. | Warm golden light and the gold-heavy Ruins world would swallow a plain gold coin. The cool gem center keeps it popping. |
| Power-ups | Floating, glowing, with a cool colored halo (magnet = cyan, shield = sky blue bubble, boost = magenta streak). Bigger than coins, bob up and down. | Cool glow is the opposite of the warm world, so it reads instantly. |
| Hero | Never wears hazard red or plain jungle green as a main color. Must be readable **from behind** (the camera sits 6 m behind, so 90% of play shows the hero's back). | The hero is the thing the eye tracks. |
| Path | Lighter and warmer than the edges of the screen; the far distance fades into warm haze (fog from 45 m, GDD 6). | Pulls the eye down the lane. |
| Value structure | Background mid-to-dark, path light, hazards dark with a red edge, pickups brightest. | Squint test: at 25% size in grayscale, hazards and coins must still separate. |

Mobile budgets (from `docs/ARCHITECTURE.md`): hero up to 15k triangles, 1 material, 1024 px textures, 40 bones;
companion up to 8k triangles, 512 px textures, 30 bones; obstacle/prop up to 2k triangles on shared atlases;
track chunk up to 20k visible triangles, 4 materials, 2048 px atlas; up to 150k triangles and 120 draw calls on screen.
Art target is well under these (about half), leaving room for effects. Toon outlines (Board C) must fit inside the
draw-call and triangle limits, and shaders must be the mobile URP shaders allowed there.

---

## Board A: "Chunky Totem" (stylized low-poly)

**Description.** A world carved from chunky, faceted shapes, like a toy jungle cut from painted wood: big blocky
leaves, crisp triangles on every rock, and long golden light raking across clean flat-colored faces. Every surface is
a single color or a soft gradient, so the screen is calm and graphic even at full speed. It feels like a pulp
adventure poster rebuilt as a handsome tabletop diorama.

**Palette**

| Role | Hex |
|---|---|
| Sun gold (key light, highlights) | `#F6B93B` |
| Warm sand (path) | `#E8C891` |
| Jungle deep green (background) | `#2F6B4F` |
| Leaf mid green | `#58A05C` |
| River teal | `#2A8C9E` |
| Mountain slate | `#6C7A89` |
| Ruins ochre stone | `#C08A4E` |
| Shadow plum | `#4A3352` |
| Hazard red (shared) | `#D7263D` |
| Coin gold / gem | `#FFD23F` / `#2EC4B6` |

**Shape language.** Big chunky polygons, flat facets, no smooth curves. Scenery is wide and squat; hazards are the
only tall, spiky, or sharply angular shapes in a lane. Hero and companion use rounder facets than the world so they
feel alive.

**Lighting.** One strong low warm sun from front-left, purple-tinted shadows, flat ambient. Baked vertex lighting
for scenery, one real-time light for hero and pickups. Each world shifts the sun color (Jungle golden, River
pale gold through mist, Mountains cool white with warm rim, Ruins orange torch glow).

**How hazards and coins read.** Very well on the scenery side: flat colors make the red hazard edge and the
gem-center coin stand out cleanly. Risk: with no outlines, a dark log in front of a dark tree line can merge, so
hazards need a light rim highlight on their top edge.

**Production cost.** Lowest cost of the three. Models from Tripo or Meshy are decimated and recolored with a small
palette texture (one 256 px swatch for the whole world), which hides most generation flaws and keeps every
asset consistent. Very light on phones. Weakness: characters with flat facets are less expressive; faces and hands
look stiff in close-ups, so clips are pretty but less charming.

**Vibe references (mood only, nothing copied).** Lara Croft GO, Alto's Odyssey, Firewatch.

---

## Board B: "Golden Expedition" (painterly hand-textured)

**Description.** Soft, rounded forms painted with visible brush strokes, like the cover of an old adventure novel
come to life: sun shafts through mist, moss on every stone, gold leaf glinting on ruined idols. Colors are rich and
warm with deep, painted shadows and glowing highlights. It is the most "cinematic" option, lush and atmospheric, a
treasure map you can run through.

**Palette**

| Role | Hex |
|---|---|
| Honey light | `#F2A541` |
| Parchment path | `#D9B98A` |
| Canopy emerald | `#1E5B43` |
| Moss | `#7A9A3A` |
| River jade | `#3E8E7E` |
| Mountain blue-gray | `#7D8FA3` |
| Ruins sandstone | `#B5835A` |
| Painted shadow umber | `#3B2A22` |
| Hazard red (shared) | `#D7263D` |
| Coin gold / gem | `#FFD23F` / `#2EC4B6` |

**Shape language.** Soft, slightly exaggerated organic forms: rounded leaves, bulging roots, chunky stones with
worn edges. Detail lives in the painted texture, not in geometry. Hazards get sharper, darker silhouettes than
the soft scenery.

**Lighting.** Painted-in light (highlights and ambient occlusion painted into textures), plus warm fog and light
shafts as cheap camera-facing cards. Real-time light only on the hero, companion, and pickups.

**How hazards and coins read.** Hardest of the three. Painted textures add visual noise, and at speed a mossy
log can blend into a mossy background. Needs discipline: low-detail textures on hazards, a red edge, and darker
background values. Coins are fine thanks to the gem and sparkle.

**Production cost.** Medium to high. Tripo and Meshy produce painterly textures well, but each asset comes out
with a slightly different brush feel and light direction baked in, so most assets need a texture clean-up or
repaint pass to look like one world. Textures must use the full allowed size (1024 px for characters, 2048 px atlases for worlds),
which costs memory and download size. Most attractive in still screenshots.

**Vibe references (mood only, nothing copied).** Sea of Thieves, Spyro Reignited Trilogy, Uncharted (for the
treasure-hunt mood only, not the realism).

---

## Board C: "Inkbound Pulp" (bold toon with outlines)

**Description.** A living pulp comic: bold ink outlines, flat colors with two or three crisp shading bands, and big
saturated sunsets behind silhouetted temples. Action moments pop like comic panels, with speed lines on boosts and a
punchy "PERFECT!" stamp when the hero lets go of a vine. It is the boldest, most graphic option, made to be read
in a split second on a small screen.

**Palette**

| Role | Hex |
|---|---|
| Pulp sunset orange | `#F28C28` |
| Sun gold | `#FFC43D` |
| Cream path | `#F3DFB2` |
| Jungle green | `#3A8C3F` |
| Deep canopy teal | `#1B4D4A` |
| River cyan | `#1FA2C7` |
| Mountain lavender-gray | `#8E8DAA` |
| Ruins terracotta | `#C4673A` |
| Ink | `#1E1A24` |
| Hazard red (shared) | `#D7263D` |
| Coin gold / gem | `#FFD23F` / `#2EC4B6` |

**Shape language.** Simple, clear forms with exaggerated proportions; big shapes, small details. Triangles and
jagged edges are reserved for hazards; scenery is rounded and leaning. **Selective ink rule:** only things that
matter to gameplay (hero, companion, hazards, coins, power-ups, vines) get the thick ink outline. Scenery gets a
thin line or none. This both saves performance and tells the player "outlined = interact with this."

**Lighting.** Toon shading with 2–3 hard bands from one warm key light, a colored rim light on the hero, sky
gradients per world. Shadows are flat color shapes, not soft blur.

**How hazards and coins read.** Best of the three. Ink outlines separate gameplay objects from the background
even at 21 m/s, and the selective outline rule makes hazards and pickups stand out by design. Flat colors keep
the red hazard edge unmistakable.

**Production cost.** Low to medium. Tripo and Meshy geometry works well because the toon shader flattens their
textures into a few color regions, which hides generation flaws and keeps assets consistent. Outlines cost extra
draw work, which is why only gameplay objects get them (inverted-hull outlines, no full-screen effect).
Characters stay expressive because faces and poses are drawn with clear shapes and lines. Strong in clips: it looks
like nothing else in the runner genre on the store.

**Vibe references (mood only, nothing copied).** The Legend of Zelda: The Wind Waker, Hi-Fi Rush, Sly Cooper.

---

## Side-by-side

| | A: Chunky Totem | B: Golden Expedition | C: Inkbound Pulp |
|---|---|---|---|
| Readable at speed | Good | Fair (needs discipline) | **Best** |
| Fits "pulpy adventure" | Good | **Best mood** | **Best "pulp" energy** |
| AI 3D generation fit (Tripo/Meshy) | **Best** | Fair (repaint passes) | Good |
| Phone performance | **Lightest** | Heaviest | Light to medium |
| Character charm in clips | Fair | Good | **Best** |
| Stands out on the store | Fair (common look) | Good | **Best** |
| Effort to keep 4 worlds consistent | **Low** | High | Low |

## Recommendation: Board C, "Inkbound Pulp"

1. **Gameplay readability (pillar 1).** Outlines plus flat colors give the clearest hazard and coin reading at speed on
   a small screen, and the selective ink rule turns readability into part of the art style.
2. **AI-generation feasibility.** The toon shader hides the small texture inconsistencies Tripo and Meshy produce, so
   four worlds can be built fast and still look like one game. Much cheaper to keep consistent than Board B.
3. **Clip appeal (pillar 3).** Comic-style vine release with speed lines and a stamped "PERFECT!" makes the
   strongest 3-second hook, and the look is distinctive in a genre full of glossy realism and plain low-poly.

Runner-up: Board A if budget and speed matter most (cheapest, safest). Choose Board B if the owner values lush
atmosphere above everything else and accepts more art clean-up time.
