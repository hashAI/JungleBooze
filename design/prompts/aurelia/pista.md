# Realistic Pista: concept prompts (P0-C)

**Owner:** art-director | **Status:** Ready to run, untested | **Last updated:** 2026-10-08
Locked stylized reference: `design/concepts/2026-10-07/hero_take1_turnaround.png`. Look rules:
`design/aurelia/ART_DIRECTION.md` section 7. Settings and run order: `README.md`.

Assemble each prompt as: `{SUBJECT} {STYLE} {FORMAT} {TAKE} {AVOID}`.

## Locked blocks

### {SUBJECT} (word-for-word in every Pista prompt)
```
an original realistic girl about 12 years old who grew up in a remote jungle at an old abandoned expedition camp, a skilled, fearless and playful young jungle explorer, real human proportions for her age, lean and athletic with long limbs, deep brown skin #6B4029, a big round natural afro of tightly curled dark brown hair #2B1B14, full and wider than her shoulders, made of visible curl clumps, natural brown eyes, a confident curious expression, wearing clothes she made from an old heavy canvas treasure map: a sleeveless wrap top with wide shoulder straps that covers her whole torso front and back down to the waistband, and knee-length wrap shorts, the canvas sun-faded cream #EFE0BD with faded sepia #8A5A2B ink map lines, coastlines and a dotted trail that follow the fabric folds, hand-stitched hems, small patches, light mud and water stains near the hems, no readable words on the map, a large bold sepia X hand-painted in ink on the cloth high on the back between the shoulder blades, a teal #178F8A woven cotton sash wrapped twice around her chest just under the arms like a band, sitting directly below the X on the back and never covering it, knotted at her left side, a small carved bamboo whistle tucked into the sash at the front, a worn brown leather satchel with a small brass buckle on her right hip on a short saffron #F2A900 strap, simple woven fiber bands around both ankles, barefoot with dusty feet
```
The age phrase `about 12 years old` is the only token that changes if the owner picks another age (ART_DIRECTION 13, Q1).

### {STYLE}
```
photorealistic high-end real-time 3D game character, physically based materials, realistic skin with natural subsurface warmth and fine texture, realistic heavy woven canvas with visible weave and stitching, natural detailed curly hair, premium AAA game character art quality, sharp focus, natural colors
```

### {AVOID} (end of every Pista prompt)
```
Avoid: text, labels, captions, letters, numbers, watermark, logo, color swatches, cartoon, anime, toon shading, ink outlines, painterly style, extra fingers, extra toes, extra limbs, cropped head or feet, bare midriff, crop top, bandeau, tight or revealing clothing, makeup, jewelry, glamour pose, low camera angle looking up, adult proportions, shoes, boots, hat, cape, weapons, alcohol, red or green clothing, face paint, tribal patterns, tattoos, the X on skin, diagonal sash, sash at the waist, sash covering the X, straightened hair, braids, long loose hair strands, floating islands, planets in the sky, glowing plants, blue skin, any resemblance to famous film or game characters.
```

## Formats

### {FORMAT_TURNAROUND} (2400x1200, generations endpoint)
```
character turnaround sheet for a 3D modeler, the same girl shown three times side by side at exactly the same scale and height, evenly spaced, left: front view, middle: side view facing left, right: back view, orthographic, full body from the top of the hair to the soles of the feet with empty space around each figure, standing upright in a relaxed A-pose with arms 30 degrees away from the body, open hands with fingers together, neutral calm expression with closed mouth, feet flat and slightly apart, the back view clearly showing the round afro, the bold X between the shoulder blades and the teal sash band directly below it, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no rim light. Satchel side: the satchel hangs on her right hip, so in the front view it appears on the LEFT side of the image and in the back view on the RIGHT side of the image; in the side view facing left it is hidden behind her body.
```

### {FORMAT_KEYART} (1824x1216, edits endpoint, reference = this take's turnaround)
```
Use the girl from the reference image exactly: same face, hair, clothes, colors, X and sash. In-game hero shot: she runs along a packed-earth jungle path, seen from behind and slightly to her right, three-quarter back view, mid-stride, light and quick with a forward lean, arms pumping, the X and teal sash band clearly visible on her back, sunlight rim on her afro and left shoulder. Environment: a realistic lush emerald jungle with giant trees standing on tall stilt roots, broad glossy leaves and ferns, thick moss, a clear turquoise river on the right flowing over pebbles, ahead a tall waterfall bursting out from under a huge twisted arch of grey-ochre stone that looks like braided petrified tree roots, rising from the ground, mist at its base, warm late-morning sun ahead-left with soft god rays in humid air, gold pollen motes, distant grounded stone pillars fading into blue haze, a few towering cumulus clouds in a blue sky. She is small in the frame, about one fifth of the image height, lower center, the world is vast. Cinematic, photoreal, natural color grade with rich emerald, turquoise and gold.
```

## Takes (append one {TAKE} sentence)

| Take | Name | Intent |
|---|---|---|
| A | Faithful | Closest translation of the locked stylized take 1; keeps faint cheek dots for comparison |
| B | Expedition salvage | More practical survival detail; optional foot wraps; no cheek dots |
| C | Agile minimal | Clean, streamlined athlete silhouette, strongest back read; teal headband; no cheek dots |

**{TAKE_A}**
```
Interpretation: faithful and balanced, the closest realistic translation of a classic design, sleeveless map-canvas wrap top and knee-length wrap shorts with clean wrap lines, a big perfectly round afro, two small faint pale clay dots on each cheek, simple and iconic.
```
**{TAKE_B}**
```
Interpretation: practical expedition salvage, the map-canvas top has short cap sleeves of the same canvas, forearms wrapped in strips of plain undyed canvas, a short coil of thin rope clipped to the satchel, cloth wraps around the arches of the feet and the ankles that leave the toes and heels bare, slightly more wear and patching, a capable resourceful look, no face markings.
```
**{TAKE_C}**
```
Interpretation: agile and streamlined, the map-canvas wraps sit neat and close without being tight, fewer folds and patches, a narrow teal cloth headband at the hairline with the afro still fully round and wide behind it, an athletic runner's build, the clearest silhouette from behind, no face markings.
```
Note for take B: the AVOID line says "shoes, boots"; foot wraps are allowed by the take sentence. If the generator
adds sandals or shoes, retry once.

## Ready-to-run list (6 images)
| # | Output file | Blocks |
|---|---|---|
| 1 | `pista_real_takeA_turnaround.png` | SUBJECT + STYLE + FORMAT_TURNAROUND + TAKE_A + AVOID |
| 2 | `pista_real_takeB_turnaround.png` | SUBJECT + STYLE + FORMAT_TURNAROUND + TAKE_B + AVOID |
| 3 | `pista_real_takeC_turnaround.png` | SUBJECT + STYLE + FORMAT_TURNAROUND + TAKE_C + AVOID |
| 4 | `pista_real_takeA_keyart.png` | ref #1; SUBJECT + STYLE + FORMAT_KEYART + TAKE_A + AVOID |
| 5 | `pista_real_takeB_keyart.png` | ref #2; same with TAKE_B |
| 6 | `pista_real_takeC_keyart.png` | ref #3; same with TAKE_C |

## After the owner's pick: 3D input views (1024x1536, edits endpoint, reference = locked turnaround)
Three separate images, one per view, for Meshy multi-image-to-3D (`meshy.md`). Same {SUBJECT} {STYLE} and the
picked {TAKE}, then:

**{FORMAT_3D_FRONT}**
```
Use the girl from the reference image exactly. Single character, full body, front view, orthographic, centered, A-pose with arms 30 degrees away from the body, fingers together and slightly apart from the thumb, neutral expression with closed mouth, feet flat and slightly apart, empty space above the hair and below the feet, solid hair mass with no flyaway strands, satchel sitting close to the hip, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no rim light, no reflections.
```
**{FORMAT_3D_BACK}**: same text with `front view` replaced by `back view, the bold X between the shoulder blades and the teal sash band directly below it`.
**{FORMAT_3D_SIDE}**: same text with `front view` replaced by `side view facing left, the satchel hidden behind the far hip`.

Check before Meshy: the three views agree on every detail (sash height, X size, satchel, ankle bands), hands are
clean, feet have five toes, nothing touches the image edge.

## Test log
| Date | Image | Model, size, quality | Result / retry reason |
|---|---|---|---|
| — | — | — | Not run yet |
