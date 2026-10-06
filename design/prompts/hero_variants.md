# Hero Variant Prompts

**Status:** Draft, untested | Designs: `design/HERO_CONCEPTS.md` | Settings and tips: `README.md`

Default style for G2: `{STYLE_C}` (recommended board). Swap in `{STYLE_A}` or `{STYLE_B}` from
`style_boards.md` only if the owner wants to see a variant in another style. Keep each SUBJECT block word-for-word
identical across the concept sheet, turnaround, and 3D hand-off.

---

## Shared FORMAT blocks

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

**{HANDOFF}** (single image for Tripo / Meshy, 1:1)
```
single character, full body, front view, orthographic, A-pose with arms 30 degrees away from the body, neutral
expression with closed mouth, centered, plain light gray #D9D9D9 background, flat even lighting, no cast shadows,
solid hair mass with no loose strands, chunky simple clothing shapes
```

**Hero negative** (add to the global negative in `README.md`)
```
adult, teenager, shoes, sandals, boots, hat brim, backpack straps that dangle, cape (except H3), jewelry chains,
red clothing, green clothing (except H3 leaf), loose flowing hair strands, multiple characters
```

---

## H1 "Topknot"

**SUBJECT**
```
an original wild jungle kid about 10 years old, playful fearless acrobat, compact springy body, warm brown skin
#8D5A3B, near-black hair #231A17 tied straight up with a twist of vine into a big spiky topknot, wearing an oversized
old expedition shirt as a sleeveless tunic in deep teal #1F7A80 with cream #F3E6C8 patches, belted with a braided
saffron #F2A900 vine belt, patched knee-length shorts, saffron cloth wraps on wrists and ankles, barefoot, a small
dented brass compass on a short cord around the neck, cheeky grin
```

- Concept sheet: `{SUBJECT}, {SHEET}, {STYLE_C}`
- Turnaround: `{SUBJECT}, {TURN}, {STYLE_C}`
- 3D hand-off: `{SUBJECT}, {HANDOFF}, {STYLE_C}`
- Extra negative: `ponytail hanging down, Mowgli, bare chest`

## H2 "Mapcloth"

**SUBJECT**
```
an original wild jungle girl about 11 years old, raised by jungle animals, playful and fearless, lean with long arms,
crouched forward-leaning cat-like stance, deep brown skin #6B4029, a big round cloud of curly dark brown hair #2B1B14,
two pale clay-colored dots on each cheek, wearing a wrap top and knee-length wrap shorts cut from an old canvas
treasure map in cream #EFE0BD with faded sepia #8A5A2B map lines and a dotted trail, a large sepia X printed on the
back between the shoulders, a teal #178F8A cloth sash across the chest holding a chunky leather satchel on one hip
with a saffron #F2A900 strap, simple woven ankle bands, barefoot, a small carved bamboo whistle tucked in the sash,
mischievous grin
```

- Concept sheet: `{SUBJECT}, {SHEET}, {STYLE_C}`
- Turnaround: `{SUBJECT}, {TURN}, {STYLE_C}`
- 3D hand-off: `{SUBJECT}, {HANDOFF}, {STYLE_C}`
- Extra negative: `red face paint, fur cape, mask, wolf, tapa cloth pattern, Polynesian patterns, readable words on the map`

## H3 "Leafcape"

**SUBJECT**
```
an original wild jungle kid about 8 years old, tiny fearless daredevil, short chunky body with a big head, light olive
skin #C49A6C, short messy black hair #1B1512, a huge lobed tropical leaf worn as a cape in lime #9BCB3C with sunset
orange #F28C28 tips, tied at the neck with a round seed-pod clasp, a sleeveless tunic made from off-white #EDE6D6
salvaged sailcloth, short rolled-up trousers, barefoot, a hollowed half-gourd helmet in ochre #C9923F slightly too big
for the head, wide brave grin
```

- Concept sheet: `{SUBJECT}, {SHEET}, {STYLE_C}`
- Turnaround: `{SUBJECT}, {TURN}, {STYLE_C}`
- 3D hand-off: `{SUBJECT}, {HANDOFF}, {STYLE_C}` (add: `cape hanging straight down close to the body`)
- Extra negative: `pith helmet, feathered cap, Peter Pan, green tunic, flying`

---

## Key-art combo prompt (after hero and macaw are chosen)

```
{CHOSEN_HERO_SUBJECT}, swinging on a thick jungle vine over a misty chasm, seen from slightly behind and to the side,
with {CHOSEN_MACAW_SUBJECT} flying just above and behind, gold coins with turquoise gem centers arcing ahead,
ancient ruins on the far cliff, warm golden light, dynamic pulp adventure poster composition, {CHOSEN_STYLE}
```

## Test log

| Date | Variant | Format | Seed | Settings | Image path | Kept? | Notes |
|---|---|---|---|---|---|---|---|
| | | | | | | | |
