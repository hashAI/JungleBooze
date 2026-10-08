# Look-test environment prompts (concepts for Meshy, and 2D cards)

**Owner:** art-director | **Status:** Ready to run, untested | **Last updated:** 2026-10-08
Contents and IDs follow `design/aurelia/LOOK_TEST_BRIEF.md` section 6. World language: ART_DIRECTION section 5.

## Shared blocks

### {STYLE_ENV}
```
photorealistic, physically based materials, natural daylight, premium AAA game environment art quality, sharp focus, natural colors with rich emerald, turquoise and gold
```
### {FORMAT_PROP} (1024x1024, for Meshy image-to-3D)
```
single object, isolated and centered, the whole object visible with empty space to every edge, three-quarter view from slightly above, plain light gray #D9D9D9 background, flat even studio lighting, no cast shadows, no ground plane, no other objects
```
### {FORMAT_CARD} (transparent background, generations endpoint, `background: transparent`)
```
isolated on a fully transparent background, no ground, no sky, evenly lit from the front, clean edges for a game texture cutout
```
### {AVOID_ENV}
```
Avoid: text, letters, watermark, logo, cartoon, painterly, floating islands, floating rocks, planets or moons in the sky, glowing plants, bioluminescence, spiral plants, jellyfish, six legs, four eyes, people, buildings, any resemblance to famous film worlds.
```

## Meshy concept images (append {STYLE_ENV} {FORMAT_PROP} {AVOID_ENV})
| ID | Subject text |
|---|---|
| A-03 | `the base of a giant jungle tree standing on a fan of ten tall curved stilt roots, the trunk about three meters wide rising out of the roots and cut off at the top of the image, pale grey-brown bark with moss on one side, open space between the roots big enough for a person to run under` |
| A-04a | `a huge natural arch of grey-ochre stone that looks like braided twisted petrified tree roots, thick rope-like stone strands twisted together, rooted in the ground at both ends, a dark round opening in the underside of the arch like a hollow root channel where water would pour out, patches of moss and small ferns on top` |
| A-04b | `a tall pillar of grey-ochre stone made of braided twisted petrified tree roots, rope-like stone strands spiraling gently upward, rooted in the ground with a flared base, moss and ferns on ledges` |
| A-04c | `a low outcrop of grey-ochre stone made of a few thick braided petrified roots breaking out of the ground, worn smooth, moss in the grooves` |
| A-05 | `a clump of tall jungle flowers, five slender green stalks about one meter tall each ending in a large drooping bell-shaped flower about forty centimeters long, petals orange #F08A2C deepening to gold at the rim, waxy natural petals, a few broad leaves at the base` |
| A-06 | `a small gliding lizard with a sixty centimeter wingspan, four legs, two eyes, a long thin tail, bronze-olive scales, wide flat side membranes supported by long ribs spread out like wings, membranes translucent turquoise with darker bars, seen from above in a glide pose` |
| A-07 | `a single oversized winged seed like a maple samara, about forty centimeters long, one curved papery wing with rust-colored veins and a round seed at the base, matte tan-gold color, slightly translucent wing` |
| A-08 | `a dense low tangle of dark woody thorny vines about one meter high, sharp long thorns with deep crimson #9E2238 tips, dark brown-grey stems, a menacing angular silhouette` |

## 2D cards (append {STYLE_ENV} {FORMAT_CARD} {AVOID_ENV} unless noted)
| ID | Size | Subject text |
|---|---|---|
| B-01 far | 2048x1024 | `a distant mountain range made of colossal grounded pillars and arches of braided grey-ochre stone like petrified giant tree roots, several thin waterfalls falling from openings in the stone, mist at their bases, seen from three kilometers away through humid air, strong atmospheric haze fading toward pale blue-grey #CFE4EA, soft late-morning light from the left` |
| B-01 mid | 2048x1024 | same as B-01 far but `seen from one kilometer away, jungle canopy covering the lower slopes, less haze` |
| B-02 | 2048x1024 | `a continuous wall of dense jungle canopy treetops seen from the side at a distance of two hundred meters, varied crowns, a few giant trees taller than the rest, soft humid haze, emerald greens` |
| B-03 | 1024x1536 | `three hanging curtains of fine long moss strands, each three meters long, violet-blue #6A4FB0 grading to pale lilac at the tips, delicate and swaying, side by side with space between them` |
| B-04 | 1536x1024 | `an atlas of jungle riverside plants laid out separately with space between them: four clumps of long flat ribbon-like reed leaves, green on top and turquoise on the underside, and three small clusters of violet bell-shaped flowers on short stems` |

Check every card: no ground plane or shadow baked in, edges clean, no Pandora drift (ART_DIRECTION 5.3). The far
range (B-01) must read as grounded: every pillar visibly meets the land or the mist layer at its base.

## Test log
| Date | ID | Model, size | Result |
|---|---|---|---|
| — | — | — | Not run yet |
