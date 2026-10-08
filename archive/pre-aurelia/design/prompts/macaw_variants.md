> **SUPERSEDED (2026-10-08).** Archived from the pre-AURELIA vision. Do not build from this file. Current sources: `design/aurelia/BLUEPRINT.md`, `design/aurelia/ART_DIRECTION.md`, `design/DECISIONS.md`. See `archive/pre-aurelia/README.md`.

# Macaw Variant Prompts

**Status:** **M3 "Dusk" chosen** at G2; macaw named **Duko** at G6 (both 2026-10-06, `design/DECISIONS.md`). Final prompts below are draft,
untested (no image key yet). M1 and M2 are kept as archive. | Designs: `design/HERO_CONCEPTS.md` |
Binding rules: `design/STYLE_GUIDE.md` | Settings and tips: `README.md`

Style for all final prompts: `{STYLE_C_CHAR}` from `style_boards.md`. Use `{STYLE_C}` only for in-world shots
and key art.

---

## CHOSEN: M3 "Dusk" (final, style C)

The macaw's name is **Duko** (trademark check pending); "Dusk" is the design label. Do not put any name in a
prompt, not even "Duko": generators tend to render it as stray text.

### Locked SUBJECT block

Paste word-for-word into every Dusk prompt. Change only the FORMAT block.

```
an original invented macaw species, sleek medium-sized body shaped like a teardrop, a long tapered tail, sly and
greedy treasure thief with a big personality, deep violet #5B3A8C body, back and upper wings, lighter violet #8A63C2
underwing lining, sunset orange #F28C28 head and chest with the orange wrapping round the nape and the back of the
head, cream #F5EBDD face patch, dark eyes with a gold #FFC43D eye ring, half-closed sly eyelids, dark gray #3A3540
curved beak and feet, a teal #2EC4B6 band at the tip of the tail, chunky feather groups with five to seven broad
primary feathers per wing
```

**Dusk negative** (add to the global negative in `README.md`)
```
pirate hat, eye patch, pirate, cartoon parrot from a famous film, Iago, Blu, Rio, Kevin, Zazu, rainbow plumage, tall
flightless bird, blue body, green body, red body, hyacinth macaw, scarlet macaw, realistic individual feathers,
hair-thin feathers, multiple birds, cage, perch stand with branding, bottle, dollar signs
```

### 1. Character model sheet (3:1)

```
{SUBJECT}, creature model sheet, the same bird shown at exactly the same scale in a row: perched front view, perched
side view, perched back view, flying view from behind and slightly below with wings fully spread (the game camera
angle), flying top view with wings fully spread, flying bottom view with wings fully spread, orthographic, beak
closed, a simple gray kid silhouette 1.6 meters tall at the end of the row for scale with the bird's wingspan about
0.6 of the kid's height, color swatches with the listed hex codes along the bottom, plain light gray #D9D9D9
background, flat even lighting, no cast shadows, {STYLE_C_CHAR}
```

Review: the view **from behind and slightly below** first (STYLE_GUIDE 6.2): orange nape visible, violet wings with
lighter lining, teal tail tip. Then the scale against the kid silhouette.

### 2. Expression sheet (3:2)

```
{SUBJECT}, creature expression sheet, head and upper body only, the same bird drawn nine times in a 3 by 3 grid at
the same scale, three-quarter front view: 1 neutral with beak closed, 2 sly half-lidded smirk, 3 greedy sparkling
eyes staring at a gold coin, 4 sharp warning squawk with beak wide open, 5 cheering with beak open and crest feathers
up, 6 proud puffed-up chest, 7 scared with feathers ruffled, 8 cackling laugh, 9 sympathetic soft look with head
tilted, expressive brow feathers above the eyes, thick ink outlines, plain light gray #D9D9D9 background, flat even
lighting, {STYLE_C_CHAR}
```

Use: menu portraits, Lift meter icon, call-out swoops (4 = danger call, 5 = cheer), death screen (9).

### 3. Action pose sheet (3:2)

```
{SUBJECT}, creature action pose sheet, the same bird shown eight times at the same scale, mostly seen from behind and
slightly below: 1 steady glide with wings spread, 2 strong wing flap with wings raised, 3 steep warning swoop diving
to one side, 4 rising swoop with a bright call, 5 carrying a kid by the wrists with its feet, wings beating hard, the
kid shown as a simple gray silhouette, 6 perched on top of a round cloud of curly hair, 7 loop-the-loop cheer with a
motion arc, 8 hiding a shiny gold coin under one wing with a sly look, strong clear silhouettes, plain light gray
#D9D9D9 background, even soft light, {STYLE_C_CHAR}
```

Pose 1 is the default in-run pose (upper third of the screen). Pose 5 is Lift (GDD 15.1).

### 4. 3D hand-off images (1:1, sent to the image-to-3D tool)

Generate with the **seed of the approved model sheet** and the model sheet as reference image. Two images:

**Perched** (main input for the mesh)
```
{SUBJECT}, single bird, full body, perched, front three-quarter view, wings folded close to the body, beak closed,
tail straight, centered, plain light gray #D9D9D9 background, flat even lighting, no cast shadows, chunky simple
feather groups, no individual thin feathers, {STYLE_C_CHAR}
```

**Flight reference** (for the wing shape and rig, not for meshing)
```
{SUBJECT}, single bird, wings fully spread flat, tail fanned slightly, top view, orthographic, beak closed, centered,
plain light gray #D9D9D9 background, flat even lighting, no cast shadows, chunky simple feather groups, {STYLE_C_CHAR}
```

Hand-off notes for asset-pipeline:
- Target: ≤ 6k triangles after cleanup (limit 8k), one material, one 512² texture, rig ≤ 30 bones (wings get
  3 bones each plus 2 for the primary group; tail 3).
- Primaries are 5–7 chunky shapes per wing, each with a little thickness (no single-sided planes; the outline hull
  needs closed volumes).
- Repaint pass: flat color regions from STYLE_GUIDE 2.4, painted ink inner lines for feather groups. Bake smoothed
  normals to UV3.
- In-game wingspan 1.0 m (STYLE_GUIDE 6.2).

### 5. In-game placement check (9:16)

```
{STYLE_C}, over-the-shoulder runner game camera view, a small wild jungle girl running away from the camera on a
three-lane path, {SUBJECT} gliding with wings spread in the upper third of the frame above the far path, the bird
covering less than a quarter of the screen width, warm light sky behind the bird, {WORLD_SHOT_DETAILS},
{READABILITY}, portrait 9:16
```

Pass: the bird reads against the sky in every world (and the dusk loop preset), never overlaps the girl's head or
the track ahead.

---

## Archive (not chosen at G2, kept for reference only)

### Shared FORMAT blocks used at G2

**{SHEET}** (concept sheet, 3:2)
```
creature concept sheet, one bird shown several times: large hero pose with wings spread in the center, view from
above and behind in flight (the game camera angle), perched pose, three expressions (squawking warning, proud, scared
or sly), color swatches with the listed hex codes along the bottom, small size comparison next to a simple gray kid
silhouette, plain light gray #D9D9D9 background, even soft studio light
```

**{TURN}** (turnaround, 3:1)
```
creature turnaround sheet, the same bird shown in a row at the same scale: perched front view, perched side view,
perched back view, flying top view with wings fully spread, flying bottom view with wings fully spread, orthographic,
beak closed, plain light gray #D9D9D9 background, flat even lighting, no cast shadows
```

**{HANDOFF}** (single image for the image-to-3D tool, 1:1)
```
single bird, full body, perched, front three-quarter view, wings folded close to the body, beak closed, tail straight,
centered, plain light gray #D9D9D9 background, flat even lighting, no cast shadows, chunky simple feather groups,
no individual thin feathers
```

**Macaw negative (G2)**
```
pirate hat, eye patch, pirate, cartoon parrot from a famous film, realistic individual feathers, hair-thin feathers,
open beak in hand-off images, multiple birds, cage, perch stand with branding, bottle
```

### M1 "Blaze" (archive)

```
an original scarlet-style macaw, lanky and theatrical show-off, oversized curved beak, red-orange body #E4572E, wings
with a yellow #FFC43D band and blue #2667B5 flight feathers, cream #F5EBDD face patch, a cheeky crest of three
feathers on top of the head, very long red-orange tail, large size, confident strutting pose
```
Extra negative: `squat body, villain grin, Iago, deep crimson red, #D7263D`

### M2 "Goldbelly" (archive)

```
an original blue-and-gold style macaw, small chubby round body, nervous and loyal, bright blue #1F6FB2 back and
wings, golden-yellow #FFC43D chest and belly, small green #5DBB63 forehead patch, cream #F5EBDD face, big worried eyes,
short-ish tail, small size, clutching its own wings anxiously
```
Extra negative: `all-blue bird, Blu, Rio, spectacles, flightless`

### M3 "Dusk" G2 concept version (archive; superseded by the locked block above)

```
an original invented macaw species, sleek medium-sized body, sly and greedy treasure thief, deep violet #5B3A8C body
and wings, sunset orange #F28C28 head and chest, teal #2EC4B6 band at the tip of a long tail, dark gray #3A3540 beak,
cream #F5EBDD face patch, half-closed sly eyes, holding a shiny gold coin under one wing
```
Extra negative: `rainbow plumage, tall flightless bird, Kevin, blue body, hyacinth macaw, green body`

---

## Test log

| Date | Variant | Format | Seed | Settings | Image path | Kept? | Notes |
|---|---|---|---|---|---|---|---|
| 2026-10-07 | M3 final | Turnaround: 3 perched views + flying top (take 1 Classic sleek) | n/a (API has no seed) | gpt-image-2, high, 2800x1200 | design/concepts/2026-10-07/macaw_take1_turnaround.png | Candidate | Iris gold instead of ink with a gold ring |
| 2026-10-07 | M3 final | Same (take 2 Rogue angular) | n/a | gpt-image-2, high, 2800x1200 | design/concepts/2026-10-07/macaw_take2_turnaround.png | Candidate | Needle-thin tail |
| 2026-10-07 | M3 final | Same (take 3 Plush chunky) | n/a | gpt-image-2, high, 2800x1200 | design/concepts/2026-10-07/macaw_take3_turnaround.jpg | Candidate | Less sleek than the locked block |
