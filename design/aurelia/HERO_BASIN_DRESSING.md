# Hero Basin Set-Dressing Map (F4_f, landscape)

**Owner:** art-director | **For:** scene builder (`Scripts/App/HeroBasin`, `HeroBasinConfigAsset`) |
**Target:** `design/aurelia/keyframes/F4_f_painterly_openai.jpg` | **Style rules:** `ART_DIRECTION_PAINTERLY.md` |
**Compared against:** landscape render `F4_compare_painterly.jpg` (middle panel), 2026-10-09.

Style and light now roughly match F4_f. The gap is **content density**. F4_f has almost no "empty" surface: every
stone top has a foliage cap, every water edge has a rock and fern, every ground patch has undergrowth. This doc
says what goes where and how much.

## 0. Coordinates and mapping

- Frame: landscape 2532×1170. Normalized screen coordinates `(u, v)`, with `u` 0 = left → 1 = right and `v` 0 = **top** → 1 = bottom.
- F4_f is 3:2 (1536×1024). It maps at **full height** onto the centre of the landscape frame:
  `u = 0.153 + 0.694 · x_f`, `v = y_f`. The side bands `u < 0.153` and `u > 0.847` are **extensions** (continue the
  left sunlit valley / the right cliff wall). Don't stretch or crop F4_f vertically. The arch crown and Pista's feet both need to stay in frame.
- **Placement method (recommended):** for each item, cast a ray from the landscape camera through `(u, v)`. Hit the
  arch/terrain/rock colliders and place the item at the hit point (plus the normal offset given). If nothing is hit, place it at
  the depth given for its layer. Seed all scatter with a fixed seed so the shot repeats exactly.
- **Depth layers** (distance from camera; Pista ≈ 1.7 m tall stands at ~6–8 m):
  - **FG** foreground: 0.5–5 m, framing plants, ground under Pista.
  - **MG** mid: 5–60 m, pools, cascades, rock islands, arch legs, canopy puffs.
  - **FAR**: 60–250 m, arch crown back face, tall fall, inner pillars, valley forest.
  - **BD** backdrop: >250 m, painted pillar cards, sky, clouds, sun.

## 1. Grid map (6 columns × 4 rows)

Columns: C1 0–0.167, C2 0.167–0.333, C3 0.333–0.5, C4 0.5–0.667, C5 0.667–0.833, C6 0.833–1.
Rows: R1 0–0.25, R2 0.25–0.5, R3 0.5–0.75, R4 0.75–1. Coverage is % of the cell area.

### Row 1 (top, v 0–0.25)

| Cell | Contents (coverage) | Layer | Color notes |
|---|---|---|---|
| C1 | Sun disc at (0.10, 0.12) partly behind leaves (5%). Sun-glow sky (30%). Palm/broad-leaf canopy hanging from the top-left corner (45%). Dark framing trunk at u 0.06–0.10 running full height (15%). | FG + BD | Glow `#FCF7C8`→`#EBC257`; leaves backlit, tips `#BDAD47`, cores `#1C2E0D` |
| C2 | Golden sky + cumulus (40%). Painted far pillar tips at (0.27, 0.22) and (0.32, 0.20) with foliage caps (15%). **God-ray origin**: 3 shafts fanning down-right (15%). Overhanging leaf tips from C1 (20%). | BD + FG | Sky `#F7DFB2`; pillar haze `#D6B273` at 60% |
| C3 | Arch **left shoulder** rising diagonally from (0.36, 0.25) to the crown (35%). Its top is ~65% covered by foliage clumps and hanging moss. Sky (40%). Far pillar (0.42, 0.20) (10%). | MG/FAR | Sandstone lit `#D6B273`, seams `#4D3A22`; caps `#5F600F`→`#BDAD47` |
| C4 | **Arch crown**: knotted braid where the strands cross (60%), ~70% of its top edge covered by foliage clumps. Vine/moss curtains hang 0.05–0.15 v from the underside (15%). Sky behind (20%). | FAR | Crown underside in teal-shifted shade `#655A3F`/`#3E5C66` |
| C5 | Arch right shoulder descending (55%), dense foliage on top (60% of the strand tops). Vine curtains (15%). Sky (20%). | MG/FAR | Lit stone toward upper-left faces only |
| C6 | Extension: far right cliff/pillar with foliage terraces (40%). Arch right leg outer strands (25%). Broad leaf from a foreground plant at the top right corner (15%). Sky (15%). | FAR + FG | Cliff `#AD914F` hazed |

### Row 2 (v 0.25–0.5)

| Cell | Contents (coverage) | Layer | Color notes |
|---|---|---|---|
| C1 | Framing trunk (15%). God rays crossing (20%). Hazy sunlit valley: layered canopy puffs at 3 depths (40%). Hazed far pillar silhouettes (15%). | FG/FAR/BD | Strong warm haze; far puffs `#9C9A6A` at 50% fog |
| C2 | Valley canopy puffs (45%), round crowns, no ferns. God rays (15%). Far pillar bases fading into haze (20%). Arch **left leg foot** begins at the right edge (10%). | FAR | Gold-green, low contrast |
| C3 | **Arch left leg** (5–8 chunky strands) sweeping from (0.34, 0.47) up-right (45%). Strand tops carry clumps; 4–6 vine curtains drop from it (10%). Canopy puffs in front of the leg base (30%). | MG | Leg in partial shade, rim-lit edges `#F7DFB2` |
| C4 | **Arch opening**: sky + cloud (25%). Inner far pillars at (0.45, 0.35) and (0.55, 0.38), foliage-capped (25%). **Tall fall** at u 0.57–0.63 (20%). Hanging curtains framing the opening edges (10%). Crown underside (15%). | FAR/BD | Fall top `#EFD4B0`, shaded edges `#BDAE99`; pillars hazed |
| C5 | Arch right leg strands (50%), foliage climbing up them (30%), curtains (10%), small gap to haze (10%). | MG | Warm lit faces left, teal shade right |
| C6 | Extension: right leg outer side + cliff wall fully overgrown (60%). FG banana/heliconia plant with bellcap flowers enters at the bottom (25%). | MG + FG | Bellcaps `#E5A45A`/`#C28036` |

### Row 3 (v 0.5–0.75)

| Cell | Contents (coverage) | Layer | Color notes |
|---|---|---|---|
| C1 | FG big broad leaves (philodendron/banana) rising from the bottom (55%). Mid canopy puffs behind (35%). | FG | Dark-core leaves, lit rims; no flat-lit greens |
| C2 | FG leaves (30%). 2 bellcap flower stems at (0.25, 0.72) and (0.27, 0.70) (3%). Mid bank: mossy boulders + shrubs (50%). | FG/MG | Bellcaps are the only orange here |
| C3 | **Pista** at u 0.34–0.39, v 0.55–0.97 (12%). Behind her: a dense bank of shrubs and rocks dropping to the water (60%). Light mist from the falls drifting left (10%). | FG/MG | She must be the strongest value contrast in the cell |
| C4 | **Middle cascade**: a wide stepped fall at u 0.50–0.62, v 0.59–0.68 (30%). Base mist (15%). Mossy rock lips and shrubs at both ends (35%). Tall-fall plunge mist above (10%). | MG | Foam bands `#FCF0CB`; pools `#278677` between lips |
| C5 | Right bank: dense foliage mound with boulders (55%). Upper cascade tier rocks (20%). Little water (10%). | MG | |
| C6 | FG banana plant + bellcap cluster (4–6 hanging bells) (55%). Bank foliage behind (35%). | FG/MG | Bells backlit, glowing edges |

### Row 4 (bottom, v 0.75–1)

| Cell | Contents (coverage) | Layer | Color notes |
|---|---|---|---|
| C1 | FG broad leaves fill the corner (80%). Ground under them dark (10%). | FG | Darkest corner of the frame (frame vignette) |
| C2 | FG leaves + ferns (50%). **Ledge**: rocky earth, cobbled stones with moss in the cracks (35%). | FG | Path earth `#B99643`/`#5E4B18`; no smooth green |
| C3 | Pista's legs/boots (10%). Rocky ledge + moss (45%). Low ferns/grass tufts at the ledge lip (20%). Water visible past the lip (15%). | FG | |
| C4 | **Lower cascade field**: 6–8 mossy rock islands, 4–6 small falls/spillways between them, foam bands (55%). Open turquoise water in between (35%). | MG | Shallow `#4EAE92` at rock rims → `#278677` |
| C5 | Rock islands with shrub caps (40%). Small spillways (15%). Water (30%). FG ferns entering from the right (10%). | MG/FG | |
| C6 | FG banana leaves, bellcap stems, ferns (70%). Boulder at the bank (15%). | FG | |

## 2. Frame budgets (hard targets, check by mask)

| Surface | F4_f | Current render (est.) | Budget |
|---|---|---|---|
| Open water with no rock, foam or reflection break within 0.08 u | ~8% | ~25% | **≤ 10%** |
| Sky (including the arch opening) | ~14% | ~15% | **10–16%**, at least 1/3 of it within 0.2 u of the sun (glow) |
| Visible bare arch stone (strand surface with no foliage/curtain) | ~18% | ~25%, almost all of it bare | **≤ 20%**, and **≤ 35% of the strand tops** bare |
| Smooth "lawn" ground / untextured terrain | 0% | ~12% | **0%** |
| Straight man-made-looking edges (slab rims, disc lips) | 0 | many | **0 visible** |
| Foliage (all layers) | ~45% | ~25% | **40–50%** |
| Orange | ~1.5% (bellcaps + Pista shoulder) | ~1% | 1–2%, bellcaps and Pista only |

## 3. Prioritized dressing list

Priority 1 is the largest visual gap. Counts are for the landscape shot. Portrait reuses the same set (it crops the sides).

1. **Arch overgrowth (top priority).** ~40 foliage clumps (mixed sizes 60/30/10: ~6 large 2–3 m, ~12 medium, ~22 small)
   along the **upward-facing strand tops**, placed by sampling arch mesh vertices with normal·up > 0.5. Cover **60–70% of
   the strand tops**, densest on the crown knot and both shoulders. Leave the strand undersides and ~30% of the side faces
   bare, so the braids still read. Add ~25 vine/moss curtains (tapered, clumped in 3s, lengths 2–12 m): ~8 framing the
   opening's inner edge (they must not cover the tall fall, keep the cleared band), ~10 on the outer sides of the legs,
   ~7 under the crown. Plus ~15 climbing-ivy decals/cards running up the legs from the ground.
2. **Kill the flat water plane.** Replace the large empty basin read with a **rock-island field**: 14–18 mossy lozenge
   boulders in 3 sizes (0.8 / 2 / 4 m) in the band v 0.70–0.95, u 0.45–0.85, each with a shrub or moss dome on top (shrubs on
   ≥ 60% of them). Add a cream foam band at every rock/water contact. Add 6–8 small spillways (0.3–1 m drops) between
   islands. Keep open water only in channels between rocks (budget §2).
3. **Pool rims and terraces: no slabs.** Every terrace/travertine edge becomes an irregular chain of boulders + fern ring:
   per terrace rim ~8–12 boulders (overlapping, varied 0.5–1.5 m) + ~10 fern/shrub clumps. Rims must break the silhouette
   at least every 1.5 m. The disc/plate at the top of the tall fall gets the same treatment, or is hidden behind a mossy rock lip
   and shrubs (zero straight lines visible).
4. **Foreground ground: 0% lawn.** Pista's ledge = rocky earth: ~20–30 flat cobbles/stones (0.2–0.6 m) set into path earth with
   moss in the cracks, painted path texture. Around it: dense undergrowth (~25 fern/broad-leaf clumps, 0.5–1.5 m) covering
   everything except a ~1.5 m-wide patch of rock under and in front of Pista. Remove or bury the smooth green mounds.
5. **Framing plants.** Left: 4–6 giant broad leaves (2–3 m) from below frame, u 0–0.25, v 0.55–1, plus 1 bellcap plant
   (2 stems, 3–4 bells) at (0.25, 0.71). Right: 1 banana/heliconia plant u 0.82–1.0, v 0.40–1, with 5–6 hanging bellcaps
   between v 0.48 and 0.80, plus 3–4 ferns at the bottom right. Top-left: 1 tree trunk (u 0.06–0.10) + 6–10 backlit
   leaf clusters hanging from the corner, leaving the sun disc 40–60% visible through gaps.
6. **Mid canopy puffs.** ~30 round canopy crowns (painterly "balls") on the left bank and valley (u 0.15–0.45, v 0.30–0.75)
   at 3 depths, and ~15 on the right bank (u 0.70–0.95, v 0.45–0.75). Replace the fern-card grids seen in the current mid-left
   (they read as flat repeated cards).
7. **Sunburst and god rays.** Sun disc visible at about (0.10, 0.12). 3–4 additive ray cards fanning from it down-right toward
   (0.30, 0.50), `#F1AF5D` at 0.25 alpha, with the longest reaching v ≈ 0.55. Bloom must flare the sun through the leaf gaps.
8. **Middle cascade.** One wide stepped fall (3 lips) between the tall-fall base and the island field at u 0.50–0.62,
   v 0.59–0.68. Mossy rock lips with shrubs at both ends and a base mist card. This replaces the current single
   straight-edged fall ledge.
9. **Tall fall mist + inner pillars.** Plunge mist (2–3 additive cards) at the base of the tall fall, v 0.55–0.62. Two
   foliage-capped pillars visible inside the arch opening at (0.45, 0.35) and (0.55, 0.38), hazed 50–60%.
10. **Backdrop pillars.** 4–6 painted pillar cards on the left valley (u 0.15–0.35, v 0.15–0.40) and 1–2 large cliff
    pillars at the right edge (u 0.88–1.0, v 0–0.35), each with foliage caps and warm haze.
11. **Particles.** ≤40 golden pollen motes in the sunlit left half; spray at the cascades.

## 4. Remove or hide in the current render

- **Bare smooth arch strands.** Keep the meshes; cover them per item 1. The current crown reads as plain clay.
- **Large empty water plane** (u 0.15–1.0, v 0.50–0.90). Fill it with islands/rims, or raise the islands so the water is broken up.
- **Slab terraces with straight edges and paper-flat white foam rings.** Replace them with boulder chains (item 3). Foam becomes shaped
  bands that hug rocks, not rings that outline slabs.
- **Flat disc/plate under the tall fall.** Hide it behind a rock lip + shrubs, or remove it.
- **Smooth green lawn mounds** in the foreground and around Pista (left and bottom). Bury them or replace them with rock + undergrowth.
- **Repeated fern cards in a grid** on the left mid bank. Replace them with canopy puffs.
- **Floating banana leaf at the top right** with no visible stem. Anchor it to the right framing plant, or drop it lower (v ≥ 0.40).
- **Dark tree trunk blocking the sun** at the top-left. Keep the trunk but move it so the sun disc and rays are visible.
- **Pista framing.** She currently sits at u ≈ 0.50. F4_f puts her at **u ≈ 0.36** (the left third), facing the arch, which gives the
  falls the right of frame. `[ASSUMED]` Move her and the ledge, not the camera, if the arch stays framed.

## 5. Acceptance (squint check against F4_f)

1. Grayscale mask: no uniform area larger than 6% of the frame other than the sky glow.
2. The budgets in §2 pass, measured with a quick ID/color mask capture.
3. The arch reads as "overgrown braids": at a 64 px-tall thumbnail you see both foliage on top and braids underneath.
4. You can't trace a straight horizontal line along any water edge.
5. The sun and rays are visible top-left, and the left valley is golden and hazy. The right side is denser and darker.
6. Pista is still the highest-contrast element in C3 and isn't hidden by undergrowth (keep a 1 m clear radius).
7. Frame time is still within budget. If foliage cost exceeds it, cut small clumps first, never the large ones (60/30/10 rule).

## Assumptions

- `[ASSUMED]` Grid coverage values are estimates made by eye from F4_f at 1536×1024. They're good enough for scatter density, not exact.
- `[ASSUMED]` The side extensions (u < 0.153, u > 0.847) continue F4_f's content (left valley haze, right overgrown cliff),
  because F4_f is 3:2 and the game shot is 19.5:9.
- `[ASSUMED]` Counts are starting values. The scene builder should expose them in `HeroBasinConfigAsset`, not hard-code them.
