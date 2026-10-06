# Style Guide: "Inkbound Pulp"

**Owner:** art-director | **Status:** Binding (G2 decided 2026-10-06) | **Last updated:** 2026-10-06

This is the binding style guide for every visual asset in the game: 3D models, textures, shaders, lighting, VFX,
UI, icons, and store art. If an asset breaks a rule here, it is rejected or the rule is changed here first.

Owner decisions this guide is built on (`design/DECISIONS.md`): art style **C "Inkbound Pulp"**, hero **H2
"Mapcloth"**, macaw **M3 "Dusk"**, pulpy-adventure mood, 4 worlds (Jungle, River, Mountains, Ancient Ruins).
Names (G6, 2026-10-06): the hero is **Pista**, the macaw is **Duko** (trademark check pending, see section 13).
"Mapcloth" and "Dusk" remain the design labels for the chosen concepts.

Items marked `[ASSUMED]` are art-director defaults that stand until the owner or a later decision changes them.

Related files: `design/STYLE_BOARDS.md` (why C was picked), `design/HERO_CONCEPTS.md` (design notes),
`design/prompts/` (prompt templates), `docs/ARCHITECTURE.md` section 10 (hard budgets), `docs/GDD.md`.

---

## 1. The style in one paragraph

A living pulp comic. Bold ink outlines on everything the player interacts with, flat colors with two or three
hard shading bands, big saturated sunsets behind silhouetted temples. Simple, exaggerated shapes: big shapes, small
details. Action moments pop like comic panels (speed lines, impact puffs, a stamped "PERFECT!"). Everything is
designed to be read in a split second on a small screen at 21 m/s.

**The three laws, in priority order** (when two rules conflict, the higher law wins):
1. **Readable at speed.** Hazards, coins, power-ups, and vines must read at their smallest on-screen size
   (section 7). Beauty never beats readability.
2. **Outlined = interact with this.** Thick ink goes only on the hero, the macaw, hazards, coins, power-ups,
   and vines. Scenery has no runtime outline.
3. **Flat beats detailed.** If a detail does not survive the squint test (section 7.1), remove it.

---

## 2. Palette

All colors are sRGB hex. The project renders in Linear color space; enter these values in sRGB color pickers
(Unity converts them). Pure black `#000000` and pure white `#FFFFFF` are banned except for the white sparkle on
coins and the white core of the vine glow.

### 2.1 Reserved gameplay colors (never used for anything else)

| Color | Hex | Role | Rule |
|---|---|---|---|
| **Hazard red** | `#D7263D` | Danger edge on hazards and danger warnings (lane-strike markers, gap flags, death-screen hazard icon) | **Hazards only.** Never on scenery, the hero, the macaw, coins, power-ups, vines, UI buttons, or text. Always paired with ink stripes or an ink edge so it is never color-only (color-blind safety). |
| Hazard ink stripe | `#1E1A24` | Alternating stripe with hazard red on high barriers and gap markers | Stripes 45 degrees, equal width, 4–6 stripes per barrier. |
| Coin gold | `#FFD23F` | Coin face | Only coins and coin UI use this exact gold at full brightness. |
| Coin rim | `#C98A12` | Coin edge band | |
| Coin gem | `#2EC4B6` | Turquoise gem center of every coin and treasure variant | The coin's signature. Every coin variant (coin, gem, idol) carries it. |
| Magnet glow | `#4FF0FF` | Magnet power-up halo and active effect | |
| Shield glow | `#7CC6FF` | Shield power-up halo and bubble | |
| Boost glow | `#FF3FA4` | Speed Boost halo and speed lines tint | |
| Lift violet | `#8A63C2` | Lift meter and Lift effects (ties to the macaw) | |
| Vine glow | white core `#FFFFFF` + sun gold pulse `#FFC43D` | Grab point of a vine, plus the ring icon (GDD 7.2) | The ring shape does the work, the color is support. |

### 2.2 Core style palette

| Name | Hex | Role |
|---|---|---|
| Ink | `#1E1A24` | All outlines, painted inner lines, darkest accents, UI borders, text |
| Pulp orange | `#F28C28` | Sunset skies, primary UI button, Duko's head and chest |
| Sun gold | `#FFC43D` | Key light tint, rim light in Mountains, UI highlights |
| Cream path | `#F3DFB2` | Running path base (lit) |
| Jungle green | `#3A8C3F` | Jungle foliage mid-tone |
| Deep canopy teal | `#1B4D4A` | Jungle background, far foliage, Jungle shadow tint base |
| River cyan | `#1FA2C7` | River water |
| Mountain lavender-gray | `#8E8DAA` | Mountain rock, cool shadows |
| Ruins terracotta | `#C4673A` | Ruins stone |
| Parchment | `#F7E9C6` | UI panels |
| Scenery gold (dull) | `#B8892E` | Gold on statues, idols, and temple trim. Darker and less saturated than coin gold, never sparkles, never spins. |

### 2.3 Hero palette: Pista (design "Mapcloth")

| Part | Hex |
|---|---|
| Map cloth (wrap top and shorts) | `#EFE0BD` |
| Map lines, dotted trail, the back "X" | `#8A5A2B` |
| Teal sash | `#178F8A` |
| Saffron satchel strap | `#F2A900` |
| Leather satchel | `#8C5530` |
| Skin | `#6B4029` |
| Hair | `#2B1B14` |
| Pale clay cheek dots | `#E3C3A0` |
| Woven ankle bands | `#D4A85A` |
| Bamboo whistle | `#C9B26B` |

Banned on the hero: hazard red, coin gold `#FFD23F`, jungle green as a main color, the power-up glow colors.

### 2.4 Macaw palette: Duko (design "Dusk")

| Part | Hex |
|---|---|
| Body, back, and upper wings | `#5B3A8C` |
| Head, nape, and chest (the orange must wrap round the back of the head) | `#F28C28` |
| Underwing lining | `#8A63C2` |
| Tail tip band | `#2EC4B6` |
| Beak and feet | `#3A3540` |
| Face patch | `#F5EBDD` |
| Eye | iris `#1E1A24` with a sun gold `#FFC43D` ring |

The tail tip shares the coin-gem turquoise on purpose: Duko is the treasure thief.

### 2.5 Value structure (every screen, every world)

From darkest to brightest: **hazard bodies and far canopy (dark) → side scenery (mid-dark) → hero and macaw
(mid, with bright rim) → path (light) → coins, power-ups, vine glow (brightest, unlit).**
The far track fades into warm fog. Pickups are the only things that never receive fog darkening or shadow.

---

## 3. Outlines and shading

### 3.1 Outline rules

| Object | Outline | Width (screen space) | How |
|---|---|---|---|
| Hero, macaw | Thick ink silhouette | 4 px at 1792 px screen height (scales with resolution); never under 2 px | Inverted hull (3.3) |
| Hazards (all archetypes, signature hazards, movers) | Thick ink silhouette | 4 px near, clamped to min 1.5 px, max 6 px | Inverted hull |
| Vines | Thick ink | 3 px | Inverted hull |
| Power-ups | Thick ink | 4 px | Inverted hull |
| Coins | Modeled ink rim | n/a | A dark ring modeled into the coin mesh (no extra draw) |
| Scenery near the path | None at runtime; inner lines painted into the texture | painted lines ≥ 3 texels wide | Texture |
| Far scenery, skyline | None; flat silhouette shapes | | Texture / vertex color |
| Inner lines (folds, hair clumps, feathers, face) | Painted in the texture in ink | ≥ 3 texels at 1024 px (hero), ≥ 2 texels at 512 px (macaw) | Texture |

- Outline color is ink `#1E1A24` everywhere. Outlines fog with their object, so a far hazard fades to the
  fog color instead of becoming a black blob.
- Outline width is per vertex (vertex color alpha, 0–1 multiplier on the material width). Thin it at fingers,
  toes, beak tip, and feather tips; keep it full on the outer silhouette.
- Hull normals: the pipeline bakes **smoothed normals** into a spare UV channel (UV3) so hard edges do not crack
  the hull. Assets without smoothed normals fail review.

### 3.2 Toon shading rules

- **Bands:** 3 hard bands from one key light: lit (100%), mid (N·L below 0.45), shadow (N·L below 0.05).
  Band edges are hard with a tiny smoothing (0.02) to avoid aliasing. Ramp is a 3-pixel-wide ramp per world
  or two threshold values; the shadow band is a **color multiply by the world shadow tint** (section 5), never
  plain darkening toward black.
- **Rim light:** hero and macaw only. A hard-edged Fresnel rim, color per world (section 5), width 0.25,
  strongest on the side facing away from the key light. This is what separates the hero from the cream path.
- **Specular:** none, except a single hard toon highlight on coins, the satchel buckle, and wet river rocks.
- **Shadows:** no realtime shadow maps (ADR 0001). The hero has a flat, hard-edged blob shadow: ink `#1E1A24`
  at 35% opacity, ellipse 0.9 m by 0.6 m, shrinking with jump height. The macaw has no ground shadow.
  Scenery and obstacles have shadows **painted** into their texture or vertex colors as flat shapes.
- **Environment lighting** is painted, not computed: track chunks and props use an unlit toon material with
  vertex-color or atlas shading plus a global world tint (section 3.3). Only the hero, macaw, hazards, and power-ups
  use the realtime key light.
- **No halftone dots on 3D surfaces in play** (they shimmer at speed). Halftone is allowed in UI, key art,
  and store art only.

### 3.3 How this is built in Unity URP on mobile `[ASSUMED; confirmed by performance-engineer in the benchmark scene]`

| Piece | Implementation | Notes |
|---|---|---|
| Character toon shader | Custom URP shader (hand-written HLSL or Shader Graph, SRP Batcher compatible): main light only, 3-band ramp, rim, one albedo texture with painted lines, optional mask (RGB = rim mask, spec mask, emissive) | Allowed by `docs/ARCHITECTURE.md` 10.2 ("custom mobile shaders"). No additional lights. |
| Environment toon shader | Unlit, vertex color shading × atlas × global `_WorldShadowTint` / `_WorldLightTint`, fog | Cheapest path. Retints the shared kit per world with no new textures. |
| Pickup shader | Unlit, self-lit, no fog darkening, scrolling sparkle on a mask channel | Coins are always the brightest thing on screen. |
| Outline | **Inverted hull via a URP Renderer Feature ("Render Objects")**: objects on an `Ink` layer are drawn a second time with one shared outline material (cull front, vertices pushed along the baked smoothed normal in clip space so width is constant in pixels, clamped by distance) | One material for every outline keeps the SRP Batcher effective. Alternative: a second `SRPDefaultUnlit` pass inside the toon shader; pick whichever profiles cheaper. |
| Post-process edge detection | **Not used.** | Needs the depth (and normals) texture that ADR 0001 keeps off, costs a full-screen pass on the A13, and would outline scenery too, which breaks law 2. |
| Sky | Unlit 3-color vertical gradient on a sky dome or camera-facing quad + flat silhouette cards (temples, peaks) | No skybox cubemap. |
| Fog | URP linear fog, color per world, start 45 m, end 90 m (GDD 6) | |
| Color grading | One small LUT per world, subtle (it nudges, it does not repaint) | Allowed by ADR 0001. |
| Bloom | **Off.** Coin glow, power-up halos, and vine glow are faked with additive sprite cards. | HDR is off (ADR 0001), so LDR bloom would also bloom the cream path and the hero's map cloth. This replaces the "bloom on gold" note in GDD 9.2; game-designer to update. |

### 3.4 Cost against the budgets (`docs/ARCHITECTURE.md` 10)

Estimate for a busy frame on the floor device (iPhone 11 / SE 2nd gen):

| Item | Draw calls | Triangles |
|---|---|---|
| Track chunks (2–3 visible, static batched, ≤ 4 materials each) | ~12 | ~45k |
| Side set pieces and props | ~10 | ~20k |
| Obstacles (~8 visible) + their hulls | ~16 | ~10k + ~10k hull |
| Coins (~30 visible, GPU instanced, modeled rim) | ~3 | ~6k |
| Power-up (1) + hull | 2 | ~1k + 1k |
| Vines (2) + hulls | 4 | ~2k + 2k |
| Hero (≤ 12k art target) + hull | 2 | ~12k + 12k |
| Macaw (≤ 6k art target) + hull | 2 | ~6k + 6k |
| Blob shadow, sky, silhouette cards | ~4 | < 1k |
| VFX (shared atlas) | ~8 | < 2k |
| UI (HUD) | ~15 | < 1k |
| **Total** | **~78 of 120** | **~137k of 150k** |

- **The hull doubles the triangles of every outlined object.** That is the main cost of this style. Rule: the
  outlined set on screen (hero + macaw + hazards + vines + power-up, before hulls) stays at or under **35k
  triangles**, so the hulls add at most 35k. If the frame goes over 150k, the first lever is a **hull LOD**:
  a decimated (about 50%) hull mesh for hazards beyond 20 m. Second lever: thinner environment chunks, never
  dropping outlines from gameplay objects.
- GPU cost of the hull pass is low (opaque, no blending, cheap fragment). Overdraw from transparent VFX is the
  bigger GPU risk: see section 10.
- Shader variants: three shaders plus the outline shader, at most 2 keywords each (`_RIM`, `_SPARKLE`). Strip
  everything else.

---

## 4. Shape language

| Family | Shapes | Why |
|---|---|---|
| **Hazards** | Angular, jagged, triangles, heavy at the base, leaning toward the player. Thorns, splinters, broken edges. | The only spiky things in a lane. Spiky means "do something." |
| **Scenery** | Rounded, lobed, leaning away from the path, soft silhouettes. Big leaves, fat roots, worn round stones. | Calm, never confused with hazards. |
| **Pickups** | Perfect circles (coins), simple bold icons (power-ups): a "U" magnet, a dome bubble, a chevron. | Instantly recognizable, even when tiny. |
| **Characters** | Circles and long S-curves. Pista: a round hair cloud on long lean lines. Duko: a teardrop body with a long tapered tail. | Alive and friendly, distinct from both scenery and hazards. |
| **Vines** | Thick, smooth, hanging curves with one glowing grab ring. | Inviting, never spiky. |

Proportions of the world: big shapes, small details. A track-side prop has at most 3 readable parts.
Exaggerate scale: trees are huge, ruins are giant, so the kid feels small and brave.

### 4.1 Hazard telegraph language (GDD 8.1)

| Archetype | Shape | Hazard-red placement |
|---|---|---|
| Low barrier (jump) | Low, wide, dark | A red band along the **top edge** facing the player |
| High barrier (slide) | Hanging, with a clear gap below | **Red and ink 45-degree stripes on the underside** |
| Full block (change lane) | Tall, solid, fills the lane | A red band at hip height (about 1 m) across the face |
| Mover | Rounded mass on a visible path | A red band or ring on the moving part + an ink ground marker in its path 1.2 s ahead |
| Gap | Visible edge, dark void | Red-and-ink striped flags or posts at the near edge, 1.0 s ahead; never red on the void itself |
| Lane strike (water spout, falling rocks, darts) | Ground warning in the lane | Pulsing red chevron or circle decal on the lane during the warning time |
| Thorn patch (lane denial) | Jagged mass across 2 lanes | Red-tinted thorn tips |

Hazard bodies are **darker and less saturated** than the path, in the world's own materials (wood, ice,
stone). Vines, coins, and scenery never carry red.

---

## 5. Lighting presets per world

One shared rig (GDD 9.2): one directional key light (realtime, characters and gameplay objects only), painted
environment, gradient sky, fog, LUT. Each world is a preset of the rig (a `LightingPreset` inside each `WorldSkin`
asset). Key light direction is given as yaw (from the run direction, positive = from the right) and pitch (down).

The key light comes **from ahead** of the runner, low, so the hero's back sits in the mid band with a bright rim.
That is the pulp backlit look, and it keeps the hero's mid value separate from the bright path.

| Setting | Jungle | River | Mountains | Ancient Ruins | Dusk loop (Jungle, 5,000 m+) |
|---|---|---|---|---|---|
| Mood | Golden late afternoon through the canopy | Pale gold through mist | Sunrise gold on snow, cool shadows | Orange sunset and torchlight | Last light, deep sky |
| Key light color | `#FFD27A` | `#FFE6B0` | `#FFF1DC` | `#FFB066` | `#FF9A5A` |
| Key yaw / pitch | -25° / 25° | -15° / 30° | -30° / 15° | 20° / 20° | -25° / 10° |
| Shadow band tint | `#2E5B57` | `#1F5A6B` | `#6B6A9A` | `#5A2E3A` | `#2B3A67` |
| Rim light (hero, macaw) | `#FFF1C2` | `#FFF6DA` | `#FFC43D` | `#7FE3D8` (cool, to lift the cream outfit off sandstone) | `#FFC43D` |
| Sky top / horizon | `#FFB347` / `#FFE3A3` | `#8FD3E6` / `#FFE8B8` | `#F6B26B` / `#FFE6C7` | `#F28C28` / `#FFD08A` | `#2B3A67` / `#FF8C42` |
| Fog color (start 45 m, end 90 m) | `#E9C98A` | `#CFE6DF` | `#E8DCEB` | `#E7B58A` | `#B0607A` |
| Path tint | cream `#F3DFB2` | sun-bleached plank `#E9D3A6` | packed snow `#F4F1F8`, shadow `#B9B6D3` | pale sandstone `#EBCB9E` | cream × key |
| Signature props | God-ray cards, hanging vines | Mist cards, waterfall silhouette | Distant peaks as flat silhouettes | Torches (flipbook flames), giant statues silhouetted against the sunset | Fireflies (sparse) |

Rules for every preset:
- **The upper third of the screen stays light or warm** (sky, sun, haze) in every world, because that is where
  the violet macaw flies. No violet or lavender skies, and no lavender rock in the macaw's screen area.
- The only exception is the dusk loop (navy sky top). There the macaw relies on its orange head and a sun gold rim,
  and it must pass the squint test against that sky before the preset ships.
- Snow is tinted lavender (`#F4F1F8` lit, `#B9B6D3` shadow), never pure white, so the cream outfit reads warmer.
- In Ruins, scenery gold is the dull `#B8892E`. Only coins are bright gold.

---

## 6. Characters

### 6.1 Pista (hero, design H2 "Mapcloth")

**Proportions** (style C):
- About **4 heads tall** to the top of the skull; the hair cloud adds about 0.4 head on top and is about 1.4 head
  widths wide. Model height to the top of the hair: **1.6 m** (fits the 1.8 m standing capsule, GDD 6).
- Long arms: fingertips reach the knee in a relaxed stand. Hands and feet about 1.2 times realistic size, so
  barefoot steps and grabs read.
- Lean torso, narrow waist, slightly long legs. Age reads as 10–12. No adult proportions, no sexualized posing.
- Run pose height about 1.35 m (crouched, forward lean 20–25 degrees). **Slide pose top ≤ 0.8 m** (GDD 6), and the
  slide silhouette must be visibly lower and flatter than the run; the run must never look like a slide.

**Back-view readability** (the camera sees her back 90% of the time):
1. The **round hair cloud** is the top landmark. It squashes on landings and stretches on jumps.
2. The **sepia "X"** sits between the shoulder blades, about 1/3 of the back's width, line weight like the ink
   outline. It must be readable at 64 px character height.
3. **The sash does not cross the X.** (Owner decision, G2 2026-10-06.) The teal sash is worn as a band around
   the chest, under the arms, sitting just below the X on the back, so it underlines the X and never covers it.
   The satchel hangs from it on a short saffron strap on the right hip. The diagonal sash from the H2 concept is
   retired: never draw or model it, because it cuts through the X from behind.
4. Bare dark skin on arms and legs against the cream cloth gives four clear limb shapes: lane changes, jumps,
   and slides read from the limbs alone.
5. Satchel bounce is secondary motion on the right hip only; it never sits above the waist.

**Faces:** big eyes (whites visible), thick brows, small nose, painted mouth shapes. Base model has a closed mouth;
expressions use texture swaps or 6–8 blendshapes (only if the bone/skin budget allows).

**Budget:** art target ≤ 12k triangles (hard limit 15k), 1 material, 1024² texture, ≤ 40 bones.

### 6.2 Duko (macaw, design M3 "Dusk")

**Size** `[ASSUMED]`: wingspan **1.0 m** (about 0.6 of the hero's height), body plus tail 0.85 m (tail is half).
That is a bit smaller than the concept's 3/4 ratio so the bird stays inside its screen area. It still reads as
big enough to lift the hero in Lift, because Lift is a cartoon moment; during Lift the wings may scale up to
1.2 times for the dive and carry.

**Screen placement** (GDD 15.1, upper third):
- The macaw lives in the **upper third of the screen**, horizontally within the center 60%, and never overlaps
  the hero's head or the track ahead.
- With wings fully spread it covers **at most 25% of the screen width and 10% of the screen height**.
- `[ASSUMED]` Interpreting "above and behind HERO" as *behind in screen depth* (farther from the camera, higher in
  the frame). With the GDD camera (6 m behind, 3.2 m up, 60 degree FOV, portrait), a working default is **2 m ahead of
  the hero and 4.2 m above the track**: that puts the bird about 20 degrees above screen center (inside the upper
  third) at roughly 23% of screen width wingspan. A bird placed behind the hero in world space would sit 3–5 m
  from the camera and fill 35–60% of the screen width. game-designer and gameplay should confirm the world offset;
  the screen rules above are what art reviews against.
- Warning swoops stay at 4.0 m or higher (GDD 15.1) and may enter the middle third for at most 0.5 s.

**Readability from the game camera** (seen from behind and slightly below):
- The orange wraps round the **nape and back of the head**, so the head reads orange from behind.
- Violet back plus the lighter violet underwing lining makes every wing beat visible.
- The teal tail tip band flicks on every turn.
- Violet against warm sky is the core contrast; see section 5 for the sky rule.

**Budget:** art target ≤ 6k triangles (hard limit 8k), 1 material, 512² texture, ≤ 30 bones. Feathers are
chunky groups (5–7 primaries per wing modeled as one shape each), never single thin feathers.

### 6.3 Animation style (both characters)

- Pose to pose with strong key poses, snappy timing, and held anticipation (2–3 frames) before jumps and the vine
  release. Overshoot and settle on landings.
- Squash and stretch on the hair cloud and the macaw's body (volume preserved), not on the hero's limbs.
- Menu and celebration clips may use 12 fps "on twos" stepping for a comic feel. **Gameplay animation stays
  smooth at 60 fps** (stepped animation hurts timing reads).

---

## 7. Readability rules (coins, power-ups, hazards)

On-screen sizes on the floor device with the GDD camera: a 1 m object at the far end of the 34 m look-ahead (about
40 m from the camera) is about **39 px tall on an iPhone 11 (1792 px screen) and about 29 px on an SE 2nd gen
(1334 px)**. The hero is about **20% of the screen height**.

### 7.1 The squint test (mandatory for every gameplay asset)

Render the asset in-game (or over a world screenshot), scale it to its far look-ahead size (**28 px tall**
as the worst case), convert to grayscale, blur 1 px. It passes if:
- a hazard still separates from the path and the background, and its archetype (low / high / tall) is clear;
- a coin still reads as the brightest dot;
- a power-up's shape (U, dome, chevron) is still identifiable at 48 px.

### 7.2 Coins

- Unlit, always the brightest and most saturated object. Face `#FFD23F`, rim `#C98A12`, modeled ink rim, turquoise
  gem `#2EC4B6`, a white 4-point sparkle that blinks every 1–2 s (offset per coin).
- Diameter **0.5 m**, spinning around the vertical axis at 1 turn per second. Coin trails form clear lines or
  arcs, never random clouds.
- Treasure variants (gem, small idol, GDD 1.1) keep the same size, the gold-and-turquoise pairing, and the sparkle.

### 7.3 Power-ups

- At least **1.6 times coin size** (0.8 m), bob 0.15 m at 1 Hz, slow spin, a halo card behind in their glow
  color, thick ink outline.
- Unique silhouettes: **Magnet** = a "U" shape; **Shield** = a dome bubble; **Speed Boost** = a double chevron.
  Shape first, color second (color-blind safe).
- Cool glows only (cyan, sky blue, magenta), the opposite of the warm worlds.

### 7.4 Hazards

- Darker than the path, outlined, with the hazard-red edge from section 4.1. The answer (jump / slide / change lane)
  is visible at least 1.2 s ahead (GDD 8).
- Hitboxes come from the shared archetype prefab (GDD 8.3); the visual mesh must stay within 10 cm of that hitbox
  on the side the player interacts with. Never visually bigger than the hitbox on that side, so a pass that looks
  safe is safe.

### 7.5 Vines

- Thick, smooth, outlined. The grab point has the white-core glow and a ring icon, pulsing at 2 Hz. Material changes
  per world (jungle vine, river creeper, mountain rope, ruins chain); the glow and ring never change.

---

## 8. UI style direction

### 8.1 Look

- **Comic panels.** UI cards are parchment `#F7E9C6` panels with a 3 pt ink border, a hard ink drop shadow offset
  4 pt down-right (no blur), and corner radius 10 pt. Panels may be rotated -2 to +2 degrees for energy (never text
  fields or lists).
- **Halftone and speed lines** are allowed as panel decoration (corners, headers), at low contrast.
- **Banners** for world names and records: a ribbon or torn-paper strip with ink border, pulp orange fill.
- **Stamps** ("PERFECT!", "GOOD", "NEW BEST!"): rotated 8–12 degrees, rough ink edge, punch-in scale 1.3 to 1.0 over
  120 ms. "PERFECT!" uses pulp orange with ink border and a sun gold inner line. Never hazard red.
- **HUD** stays minimal and out of the upper-center area (macaw and far track): score top-left, coins top-right with
  a coin icon, pause button in a top corner (mirrored in left-handed mode), Lift meter as a violet feather gauge
  under the score. Everything inside the safe area.

### 8.2 Buttons

| Type | Fill | Border / shadow | Text |
|---|---|---|---|
| Primary (Play, Continue, Buy) | Pulp orange `#F28C28` | 3 pt ink border, 4 pt hard ink shadow | Parchment `#F7E9C6`, ink outline 2 pt |
| Secondary | Teal `#178F8A` | Same | Parchment |
| Neutral / close / cancel | Parchment | Same | Ink |
| Rewarded ad | Sun gold `#FFC43D` with a small play-triangle icon | Same | Ink |

- Pressed state: shadow drops to 0 and the button moves 4 pt down-right (it "stamps" into the page).
- Minimum touch target 44 pt; the main Play button at least 88 pt tall.
- Destructive actions use the neutral style with an ink icon, **never hazard red**.
- Text sizes follow GDD 20: minimum 15 pt, key HUD numbers 20 pt.

### 8.3 Fonts (suggestions; owner can veto)

All three are free for commercial use in apps under the **SIL Open Font License 1.1** (OFL). OFL lets us embed
the font in the app and generate font atlases; we may not sell the font file by itself, and a modified version
may not use the original font name. Keep the license text in the project next to the font file and add a
`docs/LICENSES.md` row when the font is imported. Script coverage below is from each font's published metadata.

| Use | Font | License | Coverage | Notes |
|---|---|---|---|---|
| Stamps, banners, world names (short words only) | **Bangers** | OFL 1.1 | Latin, Latin Extended, Vietnamese | Comic-book capitals, the most "pulp" option. Capitals only: never for body text. |
| Headlines, buttons | **Lilita One** | OFL 1.1 | Latin, Latin Extended | Chunky and very legible at small sizes. Alternative: **Titan One** (OFL 1.1, Latin + Latin Extended). |
| Body text, numbers, settings, store fine print | **Nunito** (ExtraBold / Black for HUD numbers) | OFL 1.1 | Latin, Latin Extended, Cyrillic, Vietnamese | Rounded, friendly, wide language coverage. |

- **Avoid Luckiest Guy** for in-game text: it covers basic Latin only (missing Polish, Czech, Hungarian, and other
  EU accented letters).
- If launch languages later include Japanese, Korean, or Chinese, a separate OFL fallback font is needed; flag
  to ui-engineer before localization starts.
- HUD numbers must not jitter as they change: use the font's tabular figures if present, otherwise fixed-width
  digit spacing in the text component.

### 8.4 Icons

- Thick ink line (same weight across the set), 2 flat fills plus one highlight, on a 96 pt grid. Power-up icons
  reuse the in-game silhouettes (U, dome, chevron). Death-screen hazard icons may use hazard red (they depict
  the hazard).

---

## 9. App icon and store art (direction only; options come at G6)

- Icon: hero and macaw faces or the back-"X" plus the macaw, on a pulp sunset; no text, no "Booze", no alcohol
  reference (GDD 17.1). Three options at G6.
- Store screenshot 1 and the first 3 seconds of the preview video show the **vine swing**: wide camera, big arc,
  stamped "PERFECT!".

---

## 10. VFX style

- **Comic, flat, shape-based.** Effects are drawn shapes, not soft smoke: ink-outlined puff clouds, 4-point
  sparkle stars, impact bursts, radial speed lines, dust kicks as 3–5 flat blobs.
- **Flipbooks** of 4–8 frames at 12 fps on one shared VFX atlas (1024², ASTC). Particle systems are pooled
  (`docs/ARCHITECTURE.md` 10.3).
- **Overdraw:** prefer small, mesh-shaped particles (geometry cut to the shape) over big transparent quads.
  Avoid alpha-test (`clip`/`discard`) on large surfaces; it defeats the hidden-surface removal on iPhone GPUs.
  Target ≤ 150 live particles and ≤ 2x full-screen overdraw from VFX in the worst frame.
- **Colors** come from the palette. Hazard red appears only in hazard warnings and death impacts.

| Moment | Effect |
|---|---|
| Coin pickup | White 4-point star burst + small gold ring, 0.25 s |
| Perfect vine release | Stamp "PERFECT!" (UI) + radial speed lines for 0.4 s + a sun gold trail on the hero |
| Speed Boost | Magenta-tinted screen-edge speed lines, wind streaks past the hero |
| Shield | Sky blue dome with an ink rim and 2 hard highlight arcs; on hit, shatters into flat shards |
| Magnet | Cyan ring pulses around the hero, coins leave short cyan streaks |
| Lift | Violet feather puff on the dive, two violet wind ribbons during the carry |
| Landing | Ink-outlined dust puff, 3 blobs |
| Death | Ink "impact star" with a hazard red center, hero in a cartoon tumble; no blood, no injury |

**Reduce Motion** (GDD 19/20): no screen-edge speed lines, no screen shake, smaller stamps without the punch-in.

---

## 11. Do and don't

| Do | Don't |
|---|---|
| Ink outlines on gameplay objects only | Outline scenery at runtime, or use a full-screen outline effect |
| Hard toon bands with colored shadows | Soft gradients, black shadows, realistic ambient occlusion |
| Keep hazard red for hazards, always with ink stripes or edges | Red flowers, red birds, red UI buttons, red clothing |
| Make coins the brightest thing, with the turquoise gem | Use bright gold on scenery (use dull `#B8892E`) |
| Big shapes, few details, 3 readable parts per prop | Busy textures, noise, photo textures, tiny repeated detail |
| Spiky = hazard, round = safe | Spiky scenery next to the path |
| Light, warm sky in the upper third | Violet or lavender skies (the macaw disappears) |
| Paint inner lines and shadows into textures | Bake directional sunlight into character textures |
| Check the back view first | Approve a hero asset from a front view only |
| Original shapes and made-up animal statues | Real sacred symbols, real tribal patterns, headdresses, war paint |
| Clothed, kid-proportioned, playful hero | Loincloth, "noble savage" clichés, adult or sexualized proportions |
| Chunky feather groups, solid hair mass | Thin strands, single feathers, see-through parts |
| Treasure and adventure props (coins, maps, ropes) | Bottles, drinks, bars, anything alcohol-related |
| Cartoon tumble on death | Blood, injury, gore |
| Original designs | Anything resembling Tarzan, Mowgli, Moana, San, Crash, Lara Croft, Indiana Jones, Iago, Blu, or any brand mascot |

---

## 12. Asset review checklist (used by asset-pipeline before an asset enters `Assets/_Game/Art/`)

Copy this list into the asset's review note. Every box must be ticked or the asset goes back. art-director signs
off on characters, hazards, pickups, icons, and anything new; asset-pipeline may sign off on routine props.

**A. Style**
- [ ] Colors are from section 2 (hero / macaw / world palette), with no pure black or pure white except the
      allowed sparkle and vine glow.
- [ ] Hazard red `#D7263D` appears only on hazards, in the placement from section 4.1.
- [ ] Shading is flat with painted ink inner lines; no baked directional light, no photo texture, no noise.
- [ ] Shape language matches its family (section 4).
- [ ] Outline setup is correct for its type (section 3.1): on the `Ink` layer if outlined, smoothed normals baked to
      UV3, width in vertex color alpha. Scenery is **not** on the `Ink` layer.

**B. Readability**
- [ ] Squint test passed (section 7.1) in the world it belongs to, on a screenshot, in grayscale at 28 px.
- [ ] Silhouette reads clearly from the game camera angle (behind and above), not only in a front view.
- [ ] Hazards: the answer (jump / slide / change lane) is obvious from shape alone; visual matches the archetype
      hitbox within 10 cm and is never bigger on the interaction side.
- [ ] Coins and power-ups: brightest objects in the test screenshot; sizes from sections 7.2–7.3.
- [ ] Characters: back-view landmarks visible (hero: hair cloud, X, clear limbs; macaw: orange nape, violet wings,
      teal tail tip). Slide pose ≤ 0.8 m.
- [ ] Macaw: within its screen area and size limits (section 6.2) in every world preset including the dusk loop.

**C. Budgets (`docs/ARCHITECTURE.md` 10.2)**
- [ ] Triangles within budget (hero ≤ 15k, art target 12k; macaw ≤ 8k, target 6k; obstacle/prop ≤ 2k; chunk
      ≤ 20k visible). Hull doubles outlined meshes: counted in the frame estimate (section 3.4).
- [ ] Material count (characters 1; chunk ≤ 4) and texture size (hero 1024², macaw 512², chunk atlas ≤ 2048²),
      ASTC (4x4 hero and UI, 6x6 default), mipmaps on for 3D.
- [ ] Bones (hero ≤ 40, macaw ≤ 30); animations keyframe-reduced.
- [ ] Uses one of the allowed shaders (character toon, environment toon, pickup, outline); SRP Batcher compatible.

**D. Technical**
- [ ] Scale 1 unit = 1 m; pivot at ground center (characters: between the feet); faces +Z (run direction).
- [ ] Clean mesh: no holes, no flipped normals, no interior faces, no floating parts, no fused fingers or extra
      limbs (common in generated models).
- [ ] Naming follows the pipeline convention; prefab uses the shared archetype prefab where one exists.
- [ ] Tested in the benchmark scene: no budget regression.

**E. Originality and license**
- [ ] Does not resemble any known character, brand, logo, or real sacred or cultural symbol (section 11 list,
      plus a reverse-image check if it is a character, icon, or key art).
- [ ] Tool, plan, and commercial-use license recorded in `docs/LICENSES.md`, with the prompt file, seed, and date
      logged in the matching `design/prompts/` test log.

---

## 13. Identity note: names

**Decided (G6, 2026-10-06, `design/DECISIONS.md`): the hero is Pista, the macaw is Duko.** Both are pending a
proper trademark check (see the end of this section). Use the names in player-facing text and art direction;
technical docs may keep the GDD placeholders `HERO` and `COMPANION`. "Mapcloth" and "Dusk" stay as the design
labels of the chosen concepts (H2 and M3). Never put either name into an image prompt (generators tend to
render it as stray text).

### Name-pair shortlist (archive)

The list the owner chose from, kept for the record and as fallbacks if the trademark check fails.

Criteria: original; two syllables or close to it; spelled the way it sounds in English, Spanish, Italian,
French, German, Portuguese, and Polish (no "th", "w", "j", "ch", "c" before e/i); no bad meaning in those
languages; nothing alcohol-related; no known game, film, toy, or mascot character with that name found.

| # | Hero | Macaw | Meaning / why | Quick search result (2026-10-06) |
|---|---|---|---|---|
| 1 (CHOSEN) | **Pista** | **Duko** | Pista = "trail / clue" in Spanish, "track" in Italian: a treasure hunter on the run. Duko echoes "ducat", an old gold coin, for the coin thief. | No game, toy, or character use found for either. Pista is also a Hungarian nickname for the male name István (harmless). |
| 2 | **Fera** | **Rubo** | Fera = "wild beast" in Portuguese and Latin roots, a nod to the wild kid. Rubo = "I steal" in Italian. | No character or trademark found for either. A game called "Fer.al" exists (different name, different spelling). |
| 3 | **Selva** | **Florin** | Selva = "jungle" in Spanish, Italian, Portuguese. Florin is an old gold coin. | No game or toy use found. Florin is also a common man's name in Romania (harmless); Selva is used by unrelated furniture brands. |
| 4 | **Ilka** | **Orito** | Ilka is a short, bright Central European girl's name. Orito = "little bit of gold" in Spanish. | No game, toy, or character use found for either. |
| 5 | **Tamsa** | **Tinko** | Invented, playful, alliterative pair that says "duo". Tinko sounds like a clink of coins. | No exact match found. Tinko is close to "Tink" (a protected Disney nickname for Tinker Bell), so it is the weakest pair. |

**Chosen by the owner (G6): Pista and Duko** (also the art-director recommendation). Both are short, sound the
same in every launch language, and carry the treasure-hunt story (trail and gold coin) without explaining it.

Names dropped after the quick search (for the record): Tavi (clothing marks, "Rikki-Tikki-Tavi"), Ilo
("ilomilo" game character), Kiko (several cartoon characters), Pako (parrot plush and TV parrot "Paco"), Atla
(a 10-year-old anime character), Mapi (Spanish TV character), Zekko (a game fashion brand), Rimba (two kids' TV
series), Marga (a 1940s jungle-comic character), Lupa and Mira (recent game and film characters), Kirra (a
best-selling doll), Picaro (children's entertainment mark), Bruma (sounds like a famous manga heroine).

**The search above was a quick web search only, not a trademark clearance.** Before launch, Pista and Duko still
need a proper check in the US, EU, and UK trademark registers (classes 9, 28, 41) and on the App Store
(appstore-compliance, GDD 17.1). If either name fails, the owner picks again from the archive above.
