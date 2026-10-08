> **SUPERSEDED (2026-10-08).** Archived from the pre-AURELIA vision. Do not build from this file. Current sources: `design/aurelia/BLUEPRINT.md`, `design/aurelia/ART_DIRECTION.md`, `design/DECISIONS.md`. See `archive/pre-aurelia/README.md`.

# Style Board Prompts

**Status:** Draft, untested. Style C chosen at G2 (2026-10-06); A and B are archive. | See `design/STYLE_BOARDS.md` for the boards and `README.md` for settings.

Each board = 1 hero shot (vine swing) + 4 world shots (Jungle, River, Mountains, Ancient Ruins), 9:16 portrait,
over-the-shoulder runner camera. Lay the 5 best images out side by side as one board per style.

The figure in board images is a **generic placeholder kid seen from behind**, so the owner judges the style,
not the hero design.

---

## Style blocks (reusable tokens)

### {STYLE_A} Chunky Totem

```
stylized low-poly 3D game art, chunky faceted shapes, flat-shaded polygons, clean single-color faces with soft
gradients, toy-like carved wood diorama feel, crisp triangles on rocks and leaves, long low warm golden sunlight,
purple-tinted shadows, warm atmospheric haze in the distance, clean and graphic, mobile game, palette: sun gold
#F6B93B, warm sand #E8C891, jungle deep green #2F6B4F, leaf green #58A05C, river teal #2A8C9E, slate #6C7A89,
ochre stone #C08A4E, shadow plum #4A3352
```

### {STYLE_B} Golden Expedition

```
stylized 3D game art with hand-painted textures, visible soft brush strokes, rounded exaggerated organic forms,
lush and cinematic, painted light and ambient occlusion, sun shafts through mist, moss and worn stone, gold leaf
glints, adventure novel cover mood, rich warm colors with deep painted shadows, mobile game, palette: honey light
#F2A541, parchment #D9B98A, canopy emerald #1E5B43, moss #7A9A3A, river jade #3E8E7E, blue-gray #7D8FA3,
sandstone #B5835A, umber shadow #3B2A22
```

### {STYLE_C} Inkbound Pulp

```
bold toon-shaded 3D game art, thick dark ink outlines on characters and gameplay objects only, thin or no lines on
background scenery, flat colors with two or three hard shading bands, pulp comic book adventure energy, big
saturated sunset sky, silhouetted temples, simple clear exaggerated shapes, punchy and graphic, mobile game,
palette: pulp orange #F28C28, sun gold #FFC43D, cream path #F3DFB2, jungle green #3A8C3F, deep teal #1B4D4A,
river cyan #1FA2C7, lavender-gray #8E8DAA, terracotta #C4673A, ink #1E1A24
```

### {STYLE_C_CHAR} Inkbound Pulp, character sheets only (chosen style)

Use this instead of `{STYLE_C}` for character, expression, pose, and 3D hand-off sheets. It drops the sky and
temples so the neutral gray background is not fought by environment words. Rules: `design/STYLE_GUIDE.md`.

```
bold toon-shaded 3D game character art, thick dark ink #1E1A24 outer outline, thinner painted ink inner lines,
flat colors with three hard shading bands and colored shadows, a thin hard rim light, pulp comic book adventure
energy, simple clear exaggerated shapes, big shapes and small details, punchy and graphic, clean mobile game
character design
```

### {READABILITY} Shared readability block (pasted in place of the token in board shots 2–5)

```
three-lane running path leading straight into the distance, path lighter and warmer than the surroundings, one
low obstacle in the path with a bright red #D7263D top edge, a trail of spinning gold coins with turquoise #2EC4B6
gem centers floating over the path and glowing, clear separation between path, obstacles, coins and background
```

---

## Board shots

Replace `{STYLE}` with one of the style blocks. Keep everything else word-for-word.

### 1. Hero shot: vine swing (the clip moment)

```
{STYLE}, a barefoot wild jungle kid seen from behind and slightly to the side swinging on a thick glowing jungle
vine over a deep misty chasm, wide dramatic camera, huge arc, an arc of spinning gold coins with turquoise gem
centers in the air ahead, a colorful macaw flying above and behind the kid, warm golden late-afternoon light,
treasure-hunt pulp adventure mood, ancient stone ruins on the far cliff, portrait 9:16 composition
```

### 2. Jungle world

```
{STYLE}, view from behind a small barefoot kid running on a jungle path, over-the-shoulder runner game camera
about 6 meters behind and 3 meters above, giant trees, hanging vines, big leaves, a fallen log across one lane,
a low branch hanging over another lane with a striped underside, warm golden sunlight through the canopy,
{READABILITY}, portrait 9:16
```

### 3. River world

```
{STYLE}, view from behind a small barefoot kid running on a wooden plank path and stepping stones along a wide
jungle river, over-the-shoulder runner game camera, mist over the water, floating driftwood across one lane, a
rope net hanging over another lane, a waterfall in the distance, pale golden light through mist, {READABILITY},
portrait 9:16
```

### 4. Mountains world

```
{STYLE}, view from behind a small barefoot kid running on a narrow snowy mountain trail with a rope bridge ahead,
over-the-shoulder runner game camera, snow drift across one lane, an overhanging ice ledge above another lane,
rock pillars, cool white light with a warm golden rim from a low sun, distant peaks, {READABILITY}, portrait 9:16
```

### 5. Ancient Ruins world

```
{STYLE}, view from behind a small barefoot kid running down a long ancient temple causeway, over-the-shoulder
runner game camera, a broken column across one lane, a carved stone beam overhead, giant stone statues of
original made-up jungle animals, flickering torches, golden treasure glints, a pressure plate glowing in one lane,
warm orange torchlight, {READABILITY}, portrait 9:16
```

### Negative (all board shots)

```
first-person view, side-scrolling 2D, top-down view, realistic photo, cluttered screen, UI, HUD, text, logos,
dark murky colors, low contrast, coins blending into background, red used on scenery, gore, + global negative
```

## Test log

| Date | Style | Shot | Seed | Settings | Image path | Kept? | Notes |
|---|---|---|---|---|---|---|---|
| | | | | | | | |
