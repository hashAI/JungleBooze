# Hero Variant Prompts

**Status:** **H2 "Mapcloth" chosen** at G2 (2026-10-06, `design/DECISIONS.md`). Final prompts below are draft,
untested (no image key yet). H1 and H3 are kept as archive. | Designs: `design/HERO_CONCEPTS.md` |
Binding rules: `design/STYLE_GUIDE.md` | Settings and tips: `README.md`

Style for all final prompts: `{STYLE_C_CHAR}` from `style_boards.md` (style C without sky and temples, for sheets
on a neutral background). Use `{STYLE_C}` only for in-world shots and key art.

---

## CHOSEN: H2 "Mapcloth" (final, style C)

"Mapcloth" is a working label, not a name. Do not put any name in a prompt.

### Locked SUBJECT block

Paste word-for-word into every Mapcloth prompt. Change only the FORMAT block.

```
an original wild jungle girl about 11 years old, raised by jungle animals, playful and fearless, about four heads
tall, lean with long arms and slightly oversized hands and bare feet, crouched forward-leaning cat-like stance, deep
brown skin #6B4029, a big round cloud of curly dark brown hair #2B1B14 wider than her shoulders, big expressive eyes,
thick brows, two pale clay #E3C3A0 dots on each cheek, wearing a wrap top and knee-length wrap shorts cut from an old
canvas treasure map in cream #EFE0BD with faded sepia #8A5A2B map lines and a dotted trail, a large bold sepia X
printed high on the back between the shoulder blades, {SASH}, a chunky leather #8C5530 satchel on her right hip
on a short saffron #F2A900 strap, simple woven ankle bands #D4A85A, barefoot, a small carved bamboo whistle #C9B26B
tucked in the sash
```

**{SASH}** `[ASSUMED]` until the owner confirms (see `design/STYLE_GUIDE.md` 6.1):
- Default (A, readability fix): `a teal #178F8A cloth sash wrapped around her chest under the arms like a band, sitting
  just below the shoulder blades on the back so it never covers the X`
- Original concept (B): `a teal #178F8A cloth sash worn diagonally across the chest`

**Mapcloth negative** (add to the global negative in `README.md`)
```
adult, teenager, shoes, sandals, boots, hat, cape, red clothing, green clothing, red face paint, fur, fur cape,
mask, wolf, tapa cloth pattern, Polynesian patterns, tribal tattoos, readable words or letters on the map, thin
dangling straps, loose flowing hair strands, jewelry chains, sash covering the X on the back, multiple characters
```

### 1. Character model sheet (3:1, for the 3D team and every later prompt)

```
{SUBJECT}, character model sheet, the same character shown five times in a row at exactly the same scale: front
view, three-quarter front view, side view, three-quarter back view, back view, orthographic, relaxed A-pose with arms
30 degrees away from the body, neutral expression with closed mouth, feet flat, faint horizontal guide lines marking
head heights behind the figures (four heads tall plus the hair cloud), color swatches with the listed hex codes in a
row along the bottom, the back view clearly showing the round hair cloud, the bold X between the shoulder blades and
the teal sash below it, plain light gray #D9D9D9 background, flat even lighting, no cast shadows, {STYLE_C_CHAR}
```

Review: back view first (STYLE_GUIDE 6.1, landmarks 1–5), then proportions, then colors. Reject any set where the
five views disagree on hair shape, X size, or sash position.

### 2. Expression sheet (3:2)

```
{SUBJECT}, character expression sheet, head and shoulders only, the same character drawn nine times in a 3 by 3 grid
at the same scale, three-quarter front view: 1 neutral with closed mouth, 2 mischievous grin, 3 determined squint,
4 joyful whoop with open mouth, 5 surprised with wide eyes, 6 playful cat-like snarl-grin showing teeth, 7 laughing
with eyes closed, 8 sympathetic pout, 9 sniffing the air with eyes half closed, consistent hair cloud and cheek dots
in every panel, thick ink outlines, plain light gray #D9D9D9 background, flat even lighting, {STYLE_C_CHAR}
```

Use: texture-swap or blendshape targets (6–8 at most in game), UI portraits, death and results screens.
Expressions 4, 6, and 7 are the in-run ones (Perfect release, slide, idle).

### 3. Action pose sheet (3:2)

```
{SUBJECT}, character action pose sheet, the same character shown eight times at the same scale, mostly seen from
behind and slightly above like a runner game camera: 1 low cat-like run with long stride and forward lean, 2 tucked
jump with the hair cloud stretching up, 3 low slide on knees and one hand with a grin, the body much lower and flatter
than the run, 4 sideways pounce for a lane change, 5 hanging from a thick vine with one hand, 6 letting go of the vine
mid-air with arms spread and a joyful whoop, 7 landing on hands and feet, 8 idle scratching an ear with one foot,
strong clear silhouettes, plain light gray #D9D9D9 background, even soft light, {STYLE_C_CHAR}
```

Check: silhouettes 1, 2, and 3 must be distinguishable when filled solid black at 64 px tall (STYLE_GUIDE 7.1).

### 4. 3D hand-off images (1:1, sent to the image-to-3D tool)

Generate with the **seed of the approved model sheet** and the model sheet as reference image. Three images:

**Front** (main input)
```
{SUBJECT}, single character, full body, front view, orthographic, A-pose with arms 30 degrees away from the body,
fingers together and slightly apart from the thumb, neutral expression with closed mouth, centered, plain light gray
#D9D9D9 background, flat even lighting, no cast shadows, solid hair mass with no loose strands, chunky simple clothing
shapes, satchel sitting close to the hip, {STYLE_C_CHAR}
```

**Back** (same as Front, replace `front view` with `back view, the bold X between the shoulder blades and the teal sash
below it clearly visible`)

**Side** (same as Front, replace `front view` with `side view facing left`)

Hand-off notes for asset-pipeline:
- If the tool takes multiple views, send front, side, and back together; otherwise front only and use back and side
  for the texture repaint pass.
- Target: ≤ 12k triangles after cleanup (limit 15k), one material, one 1024² texture, rig ≤ 40 bones, mouth closed.
- Repaint pass: flat color regions from STYLE_GUIDE 2.3, painted ink inner lines, no baked light. Bake smoothed
  normals to UV3 for the outline hull.
- The map lines and the X are texture only, never geometry.

### 5. In-game readability check (9:16, after the 3D model exists, or as a concept test)

```
{STYLE_C}, view from behind a running wild jungle girl, over-the-shoulder runner game camera 6 meters behind and
3.2 meters above, {SUBJECT}, she is about one fifth of the screen height, low cat-like run on the cream path,
{WORLD_SHOT_DETAILS}, {READABILITY}, portrait 9:16
```

`{WORLD_SHOT_DETAILS}`: use the scene words from shots 2–5 in `style_boards.md` (Jungle, River, Mountains, Ancient
Ruins). Pass: the hair cloud, X, and four limbs read at 25% size in grayscale in every world.

---

## Key-art combo prompt

```
{SUBJECT}, swinging on a thick jungle vine over a misty chasm, seen from slightly behind and to the side, with
{MACAW_SUBJECT} flying just above and behind, gold coins with turquoise gem centers arcing ahead, ancient ruins on
the far cliff, warm golden light, dynamic pulp adventure poster composition, {STYLE_C}
```

`{MACAW_SUBJECT}` is the locked Dusk SUBJECT block in `macaw_variants.md`.

---

## Archive (not chosen at G2, kept for reference only)

### Shared FORMAT blocks used at G2

**{SHEET}** (concept sheet, 3:2)
```
character concept sheet, one character shown several times: large full-body hero pose in the center, small
back view, three action poses (running, jumping, sliding low), four facial expressions (grin, determined, surprised,
laughing), close-up of the signature item, color swatches with the listed hex codes along the bottom, plain light
gray #D9D9D9 background, even soft studio light, clean presentation
```

**{TURN}** (turnaround, 21:9)
```
character turnaround sheet, the same character shown five times in a row at the same scale: front view, three-quarter
front view, side view, three-quarter back view, back view, orthographic, A-pose with arms 30 degrees away from the
body, neutral expression with closed mouth, feet flat, evenly spaced, plain light gray #D9D9D9 background, flat even
lighting, no cast shadows
```

**{HANDOFF}** (single image for the image-to-3D tool, 1:1)
```
single character, full body, front view, orthographic, A-pose with arms 30 degrees away from the body, neutral
expression with closed mouth, centered, plain light gray #D9D9D9 background, flat even lighting, no cast shadows,
solid hair mass with no loose strands, chunky simple clothing shapes
```

**Hero negative (G2)**
```
adult, teenager, shoes, sandals, boots, hat brim, backpack straps that dangle, cape (except H3), jewelry chains,
red clothing, green clothing (except H3 leaf), loose flowing hair strands, multiple characters
```

### H1 "Topknot" (archive)

```
an original wild jungle kid about 10 years old, playful fearless acrobat, compact springy body, warm brown skin
#8D5A3B, near-black hair #231A17 tied straight up with a twist of vine into a big spiky topknot, wearing an oversized
old expedition shirt as a sleeveless tunic in deep teal #1F7A80 with cream #F3E6C8 patches, belted with a braided
saffron #F2A900 vine belt, patched knee-length shorts, saffron cloth wraps on wrists and ankles, barefoot, a small
dented brass compass on a short cord around the neck, cheeky grin
```
Extra negative: `ponytail hanging down, Mowgli, bare chest`

### H2 "Mapcloth" G2 concept version (archive; superseded by the locked block above)

```
an original wild jungle girl about 11 years old, raised by jungle animals, playful and fearless, lean with long arms,
crouched forward-leaning cat-like stance, deep brown skin #6B4029, a big round cloud of curly dark brown hair #2B1B14,
two pale clay-colored dots on each cheek, wearing a wrap top and knee-length wrap shorts cut from an old canvas
treasure map in cream #EFE0BD with faded sepia #8A5A2B map lines and a dotted trail, a large sepia X printed on the
back between the shoulders, a teal #178F8A cloth sash across the chest holding a chunky leather satchel on one hip
with a saffron #F2A900 strap, simple woven ankle bands, barefoot, a small carved bamboo whistle tucked in the sash,
mischievous grin
```

### H3 "Leafcape" (archive)

```
an original wild jungle kid about 8 years old, tiny fearless daredevil, short chunky body with a big head, light olive
skin #C49A6C, short messy black hair #1B1512, a huge lobed tropical leaf worn as a cape in lime #9BCB3C with sunset
orange #F28C28 tips, tied at the neck with a round seed-pod clasp, a sleeveless tunic made from off-white #EDE6D6
salvaged sailcloth, short rolled-up trousers, barefoot, a hollowed half-gourd helmet in ochre #C9923F slightly too big
for the head, wide brave grin
```
Extra negative: `pith helmet, feathered cap, Peter Pan, green tunic, flying`

---

## Test log

| Date | Variant | Format | Seed | Settings | Image path | Kept? | Notes |
|---|---|---|---|---|---|---|---|
| | | | | | | | |
