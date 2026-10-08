# Realistic Duko: concept prompts (P0-C)

**Owner:** art-director | **Status:** Ready to run, untested | **Last updated:** 2026-10-08
Locked stylized reference: `design/concepts/2026-10-07/macaw_take1_turnaround.png`. Look rules:
`design/aurelia/ART_DIRECTION.md` section 8. Settings and run order: `README.md`.

Assemble each prompt as: `{SUBJECT} {STYLE} {FORMAT} {TAKE} {AVOID}`.

## Locked blocks

### {SUBJECT}
```
an original invented species of macaw parrot, realistic bird anatomy, sleek medium build, about 85 cm from beak to tail tip including a long tapered tail, deep violet #5B3A8C body, back and wings with a subtle blue-purple iridescent sheen in light, slightly lighter violet inner wing coverts, a sunset-orange #F28C28 head, nape and chest, grading from deep orange on the chest to brighter orange on the crown, a soft feathered transition where the orange meets the violet, a cream #F5EBDD bare facial patch around the eyes crossed by fine lines of tiny dark feathers, pale yellow iris, a strong hooked dark gray #3A3540 beak, dark gray zygodactyl feet with two toes forward and two back, long violet tail feathers with teal #2EC4B6 tips on only the last part of the tail
```

### {STYLE}
```
photorealistic high-end real-time 3D game creature, physically based materials, realistic layered feathers with fine barbs, natural iridescence, wildlife-photography accuracy of anatomy, premium AAA game art quality, sharp focus, natural colors
```

### {AVOID}
```
Avoid: text, labels, letters, numbers, watermark, logo, color swatches, cartoon, anime, toon shading, ink outlines, painterly style, human-like eyes, eyebrows, smiling beak, extra wings, extra legs, extra toes, two heads, cropped tail, cropped wingtips, all-violet head, red feathers, green feathers, yellow eye ring, teal on the body or wings, scarlet macaw colors, blue-and-gold macaw colors, hyacinth macaw, glowing feathers, rider, saddle, collar, floating islands, planets in the sky, any resemblance to famous film or game characters.
```

## Formats

### {FORMAT_TURNAROUND} (2800x1200, generations endpoint)
```
creature turnaround sheet for a 3D modeler, the same bird shown four times side by side at exactly the same scale, evenly spaced, from left to right: perched front view, perched side view facing left, perched back view, flying top view with wings fully spread and tail fanned slightly, perched views standing upright on an invisible flat perch with feet visible, orthographic, the whole bird visible with empty space around each view including the full tail, closed beak, calm alert expression, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no rim light.
```

### {FORMAT_KEYART} (1824x1216, edits endpoint, reference = this take's turnaround)
```
Use the bird from the reference image exactly: same colors, face and proportions. In-game shot: the macaw flies low over a clear turquoise jungle river, seen from behind and above, wings in a strong downstroke, the long tail with teal tips trailing, warm late-morning sunlight ahead-left catching the violet sheen and the orange head, a realistic lush emerald jungle with giant trees on tall stilt roots along the banks, a tall waterfall in the distance bursting out from under a huge twisted grey-ochre stone arch of braided petrified roots rising from the ground, soft god rays and mist, blue sky with cumulus. The bird is about one sixth of the image width, upper center. Cinematic, photoreal, natural color grade.
```

## Takes
| Take | Name | Intent |
|---|---|---|
| A | True macaw | Natural macaw proportions; closest to the locked take 1 |
| B | Sleek long-tail | Slimmer, longer tail, glossier iridescence; most elegant in flight |
| C | Characterful | Slightly larger head and beak, fuller cheek feathers, small nape crest, sly look; most personality |

**{TAKE_A}**
```
Interpretation: true to real macaw proportions, balanced and natural, clean color blocks, the closest realistic translation of a classic design.
```
**{TAKE_B}**
```
Interpretation: sleek and elegant, a slimmer body and a noticeably longer, narrower tail, glossy plumage with a stronger violet-blue iridescent sheen, long pointed wings, the most graceful flight silhouette.
```
**{TAKE_C}**
```
Interpretation: characterful and clever, a slightly larger head and heavier beak within realistic limits, fuller orange cheek feathers, a small tuft of nape feathers raised as if alert, a sly half-lidded eye, the most personality while staying a real bird.
```

## Ready-to-run list (6 images)
| # | Output file | Blocks |
|---|---|---|
| 1 | `duko_real_takeA_turnaround.png` | SUBJECT + STYLE + FORMAT_TURNAROUND + TAKE_A + AVOID |
| 2 | `duko_real_takeB_turnaround.png` | same with TAKE_B |
| 3 | `duko_real_takeC_turnaround.png` | same with TAKE_C |
| 4 | `duko_real_takeA_keyart.png` | ref #1; SUBJECT + STYLE + FORMAT_KEYART + TAKE_A + AVOID |
| 5 | `duko_real_takeB_keyart.png` | ref #2; same with TAKE_B |
| 6 | `duko_real_takeC_keyart.png` | ref #3; same with TAKE_C |

## After the owner's pick: 3D input views (rig pose)
Duko flies most of the time, so the 3D model is built **wings spread** (the bird equivalent of an A-pose); folded
wings come from the rig. Three images, 1536x1024, edits endpoint, reference = locked turnaround, plus the picked {TAKE}:

**{FORMAT_3D_FRONT}**
```
Use the bird from the reference image exactly. Single bird, front view, orthographic, centered, wings spread fully and straight out to the sides in a flat glide pose, primary feathers slightly separated, tail straight back and slightly fanned, legs tucked with feet visible under the body, beak closed, eyes open, whole bird visible with empty space to every edge, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows.
```
**{FORMAT_3D_TOP}**: same text with `front view` replaced by `top view looking straight down at the back`.
**{FORMAT_3D_SIDE}**: same text with `front view` replaced by `side view facing left, wings seen edge-on`.

Meshy treats the first image as the front. Use front, top, side in that order (`meshy.md`).

## Test log
| Date | Image | Model, size, quality | Result / retry reason |
|---|---|---|---|
| — | — | — | Not run yet |
