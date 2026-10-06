# Macaw Variant Prompts

**Status:** Draft, untested | Designs: `design/HERO_CONCEPTS.md` | Settings and tips: `README.md`

Default style for G2: `{STYLE_C}`. Keep each SUBJECT block word-for-word identical across all formats.

---

## Shared FORMAT blocks

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

**{HANDOFF}** (single image for Tripo / Meshy, 1:1)
```
single bird, full body, perched, front three-quarter view, wings folded close to the body, beak closed, tail straight,
centered, plain light gray #D9D9D9 background, flat even lighting, no cast shadows, chunky simple feather groups,
no individual thin feathers
```
Generate a second hand-off image with `wings fully spread, top view, orthographic` for the flight pose reference.

**Macaw negative** (add to the global negative in `README.md`)
```
pirate hat, eye patch, pirate, cartoon parrot from a famous film, realistic individual feathers, hair-thin feathers,
open beak in hand-off images, multiple birds, cage, perch stand with branding, bottle
```

---

## M1 "Blaze"

**SUBJECT**
```
an original scarlet-style macaw, lanky and theatrical show-off, oversized curved beak, red-orange body #E4572E, wings
with a yellow #FFC43D band and blue #2667B5 flight feathers, cream #F5EBDD face patch, a cheeky crest of three
feathers on top of the head, very long red-orange tail, large size, confident strutting pose
```
- Extra negative: `squat body, villain grin, Iago, deep crimson red, #D7263D`

## M2 "Goldbelly"

**SUBJECT**
```
an original blue-and-gold style macaw, small chubby round body, nervous and loyal, bright blue #1F6FB2 back and
wings, golden-yellow #FFC43D chest and belly, small green #5DBB63 forehead patch, cream #F5EBDD face, big worried eyes,
short-ish tail, small size, clutching its own wings anxiously
```
- Extra negative: `all-blue bird, Blu, Rio, spectacles, flightless`

## M3 "Dusk"

**SUBJECT**
```
an original invented macaw species, sleek medium-sized body, sly and greedy treasure thief, deep violet #5B3A8C body
and wings, sunset orange #F28C28 head and chest, teal #2EC4B6 band at the tip of a long tail, dark gray #3A3540 beak,
cream #F5EBDD face patch, half-closed sly eyes, holding a shiny gold coin under one wing
```
- Extra negative: `rainbow plumage, tall flightless bird, Kevin, blue body, hyacinth macaw, green body`

## Test log

| Date | Variant | Format | Seed | Settings | Image path | Kept? | Notes |
|---|---|---|---|---|---|---|---|
| | | | | | | | |
