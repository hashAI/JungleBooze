# Realistic Pista: concept prompts (P0-C)

**Owner:** art-director | **Status:** Ready to run; first run blocked by an empty OpenAI credit balance (2026-10-08) | **Last updated:** 2026-10-08

**Owner decisions (2026-10-08):** Pista is **16**. She is redesigned after the explorer on
`design/aurelia/vision_board.png` ("THE EXPLORER" panel and the hero of the key art), with these rules: no crop top
or bare midriff (fitted athletic top that covers the torso), practical not glamour, original rather than a copy of any
known game heroine (no twin holsters, no tank top and shorts), no cheek dots. Look rules:
`design/aurelia/ART_DIRECTION.md` section 7. Settings and run order: `README.md`.

Assemble each prompt as: `{SUBJECT} {STYLE} {FORMAT} {TAKE} {AVOID}`.
Skin tone, ethnicity and face live in the `{LOOK}` token inside SUBJECT so they can be swapped without changing the
rest (owner question open; default follows the board's hero `[ASSUMED]`).

## Locked blocks

### {LOOK} (default `[ASSUMED]`)
```
warm light-tan skin, brown eyes, natural eyebrows, an original face that does not resemble any real person
```

### {SUBJECT} (word-for-word in every Pista prompt; `{LOOK}` is replaced by the LOOK block)
```
an original realistic 16-year-old girl, an athletic young outdoor explorer, fearless, curious and playful, realistic teenage proportions, about 1.65 m tall, lean athletic build, {LOOK}, dark brown hair #4A2E1E pulled back into a high ponytail that falls to between the shoulder blades, wearing a practical adventure outfit: a fitted athletic top with short sleeves that covers her whole torso down to the waistband, off-white #ECE8E1 with expedition-orange #E8742A shoulder and side panels and charcoal #2E3136 trim, charcoal technical trousers, straight and slightly loose, with reinforced knee patches, a tan-brown leather #7A5232 utility harness over the top with orange shoulder straps and a chest buckle, a belt with two small leather pouches, fingerless charcoal gloves, sturdy brown leather hiking boots, a compact technical backpack in charcoal and off-white with orange accents and orange straps, worn close to the back, a small brass compass clipped to the harness
```

### {STYLE}
```
photorealistic high-end real-time 3D game character, physically based materials, realistic skin with natural texture, realistic technical fabrics, leather and stitching, natural detailed hair, premium AAA game character art quality, sharp focus, natural colors
```

### {AVOID}
```
Avoid: text, labels, captions, letters, numbers, watermark, logo, brand names, color swatches, cartoon, anime, toon shading, ink outlines, painterly style, extra fingers, extra limbs, cropped head or feet, crop top, bare midriff, tank top, shorts, leggings, tight or revealing clothing, makeup, jewelry, glamour pose, sexualized pose, low camera angle looking up, adult woman, holsters, guns, knives, weapons, single long braid, red hair, face paint, cheek dots, tattoos, tribal patterns, floating islands, planets or moons in the sky, glowing plants, any resemblance to famous film or game characters, any resemblance to real people or celebrities.
```

## Formats

### {FORMAT_TURNAROUND} (2400x1200, generations endpoint)
```
character turnaround sheet for a 3D modeler, the same girl shown three times side by side at exactly the same scale and height, evenly spaced, left: front view, middle: side view facing left, right: back view, orthographic, full body from the top of the ponytail to the soles of the boots with empty space around each figure, standing upright in a relaxed A-pose with arms 30 degrees away from the body, open hands with fingers together, neutral calm expression with closed mouth, feet flat and slightly apart, the back view clearly showing the high ponytail, the backpack with its orange accents and the harness straps, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no rim light.
```

### {FORMAT_KEYART} (1824x1216, edits endpoint, reference = this take's turnaround)
```
Use the girl from the reference image exactly: same face, hair, outfit, colors and backpack. In-game hero shot: she runs along a packed-earth jungle path, seen from behind and slightly to her right, three-quarter back view, mid-stride, light and quick with a forward lean, arms pumping, the high ponytail swinging, the backpack and harness clearly visible, sunlight rim on her hair and left shoulder. Environment: a realistic lush emerald jungle with giant trees standing on tall stilt roots, broad glossy leaves and ferns, thick moss, a clear turquoise river on the right flowing over pebbles, ahead a tall waterfall bursting out from under a huge twisted arch of grey-ochre stone that looks like braided petrified tree roots, rising from the ground, mist at its base, warm late-morning sun ahead-left with soft god rays in humid air, gold pollen motes, distant grounded stone pillars fading into blue haze, a few towering cumulus clouds in a blue sky. She is small in the frame, about one fifth of the image height, lower center, the world is vast. Cinematic, photoreal, natural color grade with rich emerald, turquoise and gold.
```

## Takes (append one {TAKE} sentence)

| Take | Name | Intent |
|---|---|---|
| A | Board-faithful | Closest to the board's explorer, with the safety and originality rules applied |
| B | Rugged expedition | More worn and practical; the only take with a nod to the old Pista (map patch with an X on the backpack) |
| C | Sleek agile | Lightweight trail-runner kit, slimmest silhouette, cleanest back read |

**{TAKE_A}**
```
Interpretation: faithful to a classic modern adventure-explorer look, bold off-white and orange color blocking on the top, orange harness straps over both shoulders, knee pads built into the trousers, a mid-size backpack with orange side panels, clean and confident.
```
**{TAKE_B}**
```
Interpretation: rugged expedition, three-quarter sleeves pushed up the forearms, dust and wear on the fabrics, scuffed boots, a slightly larger backpack with a rolled tarp strapped under it and a faded treasure-map patch with a small sepia X stitched on the backpack lid, a coil of thin rope clipped to the belt, capable and resourceful.
```
**{TAKE_C}**
```
Interpretation: sleek and agile, a lightweight trail-runner kit, a slimmer fitted top with orange side panels, lighter trousers that taper at the ankle, low hiking boots, a slim vest-style backpack hugging the back with orange straps, fewer pouches, the cleanest athletic silhouette from behind.
```

## Ready-to-run list (6 images)
| # | Output file (`design/concepts/2026-10-08/`) | Blocks |
|---|---|---|
| 1 | `pista_real_takeA_turnaround.png` | SUBJECT + STYLE + FORMAT_TURNAROUND + TAKE_A + AVOID |
| 2 | `pista_real_takeB_turnaround.png` | same with TAKE_B |
| 3 | `pista_real_takeC_turnaround.png` | same with TAKE_C |
| 4 | `pista_real_takeA_keyart.png` | ref #1; SUBJECT + STYLE + FORMAT_KEYART + TAKE_A + AVOID |
| 5 | `pista_real_takeB_keyart.png` | ref #2; same with TAKE_B |
| 6 | `pista_real_takeC_keyart.png` | ref #3; same with TAKE_C |

## After the owner's pick: 3D input views (1024x1536, edits endpoint, reference = locked turnaround)
Three separate images, one per view, for Meshy multi-image-to-3D (`meshy.md`). Same {SUBJECT} {STYLE} and the
picked {TAKE}, then:

**{FORMAT_3D_FRONT}**
```
Use the girl from the reference image exactly. Single character, full body, front view, orthographic, centered, A-pose with arms 30 degrees away from the body, fingers together and slightly apart from the thumb, neutral expression with closed mouth, feet flat and slightly apart, empty space above the hair and below the boots, hair neat with no flyaway strands, backpack and pouches sitting close to the body, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no rim light, no reflections.
```
**{FORMAT_3D_BACK}**: same text with `front view` replaced by `back view, the high ponytail, the backpack and the harness straps clearly visible`.
**{FORMAT_3D_SIDE}**: same text with `front view` replaced by `side view facing left, the ponytail profile and the backpack depth visible`.

Check before Meshy: the three views agree on every detail (panels, harness, pouches, backpack), hands are clean,
nothing touches the image edge.

## Test log
| Date | Image | Model, size, quality | Result / retry reason |
|---|---|---|---|
| 2026-10-08 | Turnarounds A, B, C (3 requests, parallel) | `gpt-image-2`, 2400x1200, high | **Failed, no image:** HTTP 429 `insufficient_quota` / `credit_balance_exhausted` (the OpenAI account has no credits). Not counted against the 24-generation cap; nothing was billed |
| 2026-10-08 | Turnaround A (recheck) | `gpt-image-2`, 2400x1200, high | **Failed, no image:** HTTP 429 `credit_balance_exhausted` again. Not counted, nothing billed |
