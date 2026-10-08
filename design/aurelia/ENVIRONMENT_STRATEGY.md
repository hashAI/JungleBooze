# Environment Strategy: from "sunny park avenue" to the vision board, at 60 fps on iPhone 12

**Owner:** art-director | **Status:** Revision 2. Meshy + CC0 + in-house plan | **Date:** 2026-10-09
**Inputs:** `BLUEPRINT.md`, `vision_board.png`, `ART_DIRECTION.md`, `LOOK_TEST_BRIEF.md`, `docs/adr/0004-realistic-look-on-mobile.md`
(measured numbers), `design/DECISIONS.md`, look-test shots `/tmp/junglebooze-shots/` (2026-10-09 build).

> **Owner decision (2026-10-09, via the coordinator): no purchases at all.** No Asset Store packs, no impostor tool and
> no OpenAI credit. "Use Meshy." The budget is the **~938 remaining of the 1,000 pre-approved Meshy credits**, plus
> CC0 assets and in-house work (Blender procedural tools, our own impostor baker, Unity shaders). Revision 1 (paid packs
> + OpenAI) was declined; its market research is kept in section 11 for the record.

---

## 1. Why the look test reads as a park (diagnosis)

| # | Problem in the current frames | What the vision board does instead | Fix |
|---|---|---|---|
| 1 | **Composition:** a dead-straight trail to a vanishing point, with trees in two neat rows. Reads as a park avenue | Paths curve, climb and drop. Foliage frames the shot. The view opens up to a reveal | 4.1 |
| 2 | **Empty sky:** 30–50% of the frame (more in portrait) | Sky is under 20% of the frame. The upper frame is full of canopy, landforms and falls | 4.2 |
| 3 | **Mown lawn:** flat green ground, sparse ferns | Six layers of vegetation: moss, ferns, broadleaf, shrubs, trunks with vines, then canopy | 4.3 |
| 4 | **No scale or identity:** thin, temperate-looking trees and no landmark | Giant structures, waterfalls, depth over kilometres | 5 |
| 5 | **Flat midday light** | Sun into the camera, glowing leaf edges, mist, light shafts, haze that fades to pale | 4.4 |
| 6 | **Water is a grey strip** off to the side | Turquoise water in almost every view | 4.5 |
| 7 | **Uniform orange-brown path** with a hard edge | Worn soil, roots, stone, creeping moss, leaf litter | 4.3 |

**Performance (ADR 0004, measured):** 810–930 draw calls and 0.71–0.81 M triangles against a budget of 250+100 draws and
350k+150k triangles. The cause is 560–700 separate renderers per view and dense CC0 scans. **The look and the budget
have the same fix:** fewer, larger, art-directed pieces (merged clumps, impostors, painted layers).

## 2. Strategy in one paragraph

No product would have given us the vision board on mobile anyway (section 11). The board's identity is its original
giant landforms, which the Blueprint requires us to make ourselves. So the plan puts **Meshy credits only where Meshy
is uniquely good**: original organic hero shapes (stiltwood root fans, bellcaps, creatures) and painted images
(keyframes, kilometre-scale backdrops, sprite sheets for flora that's hard to model). Everything that can be made
procedurally without losing quality is made procedurally: rootstone, broad tropical leaves, canopy, vines, impostors,
water, mist. **Composition, atmosphere and light come first** in the build order. They cost no credits, they're most
of the "wow", and they show early whether the plan works.

## 3. Tool constraint
Agents work headless on the owner's Mac: Unity batch mode, Blender Python and the Meshy HTTP API. GUI-only tools are
out. Free CC0 sources: Poly Haven (API) and ambientCG (API). Local open-source upscaling: Real-ESRGAN (BSD-3-Clause,
runs on the Mac, free).

## 4. Techniques that create the "wow" cheaply on mobile (all credit-free)

All of these fit ADR 0004 (Forward, no depth texture, no lightmaps). Costs are for an A14 at about 1.7 MP.

### 4.1 Composition
- **S-curved path** with 8–15° bends every 30–50 m and 1–4 m climbs and drops. The far path disappears behind foliage;
  the horizon is never a vanishing point.
- **Enclose, then reveal:** about 70% of the run is enclosed (foliage walls, canopy roof, root arches) and 30% opens up
  (river, canopy gap, falls vista).
- **Framing elements** in the near corners (a leaf mass, a root, a flower clump), as in the bottom left of the board.
- **4–5 m of visible trail** inside a 6–8 m runnable width, with edges overgrown by roots and moss.

### 4.2 Layered planes and budget
| Layer | Distance | Technique | Worst-view share |
|---|---|---|---|
| L0 Path and edge dressing | 0–15 m | 3D roots, stones, moss strips, merged per segment | 40 draws / 50k tris |
| L1 Foliage walls | 2–40 m | Leaf-card and broadleaf clumps (section 6), merged by material per 25 m segment, 3 shared 2048² atlases | 30 draws / 90k tris |
| L2 Trunks, stiltwoods, rootstone | 10–120 m | 3D, 3 LODs, dithered cross-fade | 50 draws / 90k tris |
| L3 Canopy roof | overhead | One merged leaf-card mesh per segment, colored by vertex color. **Doesn't cast shadows**; the dappled light comes from a scrolling **light cookie** on the sun | 10 draws / 25k tris |
| L4 Forest wall | 40–300 m | **Our own octahedral impostors** (4.6), instanced | 10 draws / 2k tris |
| L5 Backdrop | 300 m–3 km | 3–4 matte-painted cards with parallax, haze painted in, a slow mist card between them | 6 draws / <1k tris |
| L6 Sky | ∞ | CC0 HDRI with cumulus (kept), graded | 1 draw |
| Pista, pickups, FX | | | 40 draws / 35k tris |
| **Total** | | | **~190 main draws / ~300k tris** (budget 250 / 350k) |

### 4.3 Vegetation rendering
- **Merge per segment at build time** (ADR 0004 fix 1). Target ≤ 12 draws per 25 m segment.
- **Shared atlases:** understory, canopy, flowers and alien flora, each 2048² (ASTC 6x6 albedo, 5x5 normal).
- **Vertex colors do the lighting work.** Wind weights (ADR 0004) go in the color channels. Baked AO and "under the
  canopy" darkening go in UV2, baked per prefab in Blender. This replaces lightmaps and SSAO and gives the dark
  understory with bright gaps that makes a jungle read.
- **Big broad leaves are opaque geometry** (cheap: the GPU can skip hidden pixels). Alpha cards are kept for fronds,
  moss and fine leaves only. Cards are cut close to the leaf shape.
- Wind in the vertex shader (exists), with stronger flutter for broad leaves. Unity 6.3 import-time Mesh LOD for 3D
  props.

### 4.4 Light and atmosphere
- Sun ahead-left and low (25–35°), backlighting the foliage. Leaf translucency (already in `Nature Lit`) gives the
  gold-green rim.
- **Height fog + sun-direction in-scatter** in the custom fog: warm toward the sun, cool away from it, denser in hollows
  and over water. Shader maths only, no extra pass.
- 2–4 additive light-shaft cards per enclosed view (faded by angle), ≤ 60 dust motes, mist cards at the base of every
  landform and fall.
- **Color grade LUT built from the approved keyframes** (7.1), so the grade aims at a picture rather than at numbers.

### 4.5 Water
- River moves into frame (ford, crossings, bank path). Add a flow map, foam along banks and rocks painted into vertex
  color, and a sky reflection band (extends the existing shader).
- Waterfalls: 2–3 layered scrolling sheets (core, broken edges, spray streaks), a mist flipbook rendered in Blender, and
  a wetness mask on nearby rock. One hero waterfall per vista plus thin falls painted into the backdrops.

### 4.6 In-house impostor baker (replaces Amplify)
- **Blender side:** a Python tool renders an object from a hemi-octahedral grid of views (8×8 = 64 frames) into a
  2048² atlas set: albedo + alpha, normal, depth. It reuses the same camera and lighting setup for every tree.
- **Unity side:** our own impostor shader picks the 3 nearest frames from the view direction, blends them, and uses
  depth for a little parallax and to sit the impostor on the ground. Wind is a gentle UV sway.
- **Sources:** our stiltwoods and canopy trees, plus Poly Haven `island_tree_01/02/03` and `jacaranda_tree`. Those
  scans have 0.3–4.8 M triangles: unusable in real time but ideal impostor sources, free and CC0.
- Effort: about 1 week of agent time. **Fallback:** 8-view billboards with cross-fade, hidden by more fog and denser
  foliage walls in front of them.

## 5. What makes the world ours (signature assets)

| Asset | Method | Credits |
|---|---|---|
| **Rootstone kit** (arches, pillars, root-bridges, outcrops, cliff walls) | **Procedural in Blender**: a geometry-node braid generator (twisted strand curves → mesh), triplanar CC0 rock (`rock_face_03`) + strand normal detail + moss mask; LODs automatic. Meshy makes one hero arch as an A/B comparison | 0 (A/B: see 7.1) |
| **Stiltwood** (3 trunk-and-root variants) | Meshy image-to-3D, **mesh only** (our CC0 bark material, no Meshy texture), crowns from the canopy atlas, impostors far away | 7.1 |
| **Bellcap**, **cupleaf epiphyte** (frame-edge hero plant), **sailback**, **thorn bramble** | Meshy image-to-3D, textured | 7.1 |
| **Veilmoss, duskbell, ribbon reed, hanging vines** | Sprite sheets (Meshy text-to-image with background removal) or procedural, on Blender card meshes | 7.1 / 0 |
| **Whirlseed** | Hand-made card mesh, procedural texture | 0 |
| **Backdrops** (rootstone range, mist valley, far falls, forest silhouettes) | Meshy image-to-image matte paintings using vision-board crops as references, upscaled locally, cut into layers | 7.1 |

Originality (ART_DIRECTION 5.3) is checked on every output. That matters most here, because the board crops carry
Pandora-like motifs (section 7.2).

## 6. Leaf and foliage atlases without OpenAI

| Route | Gives | Quality | Credits |
|---|---|---|---|
| **A. Procedural leaf generator (Blender geometry nodes)**: leaf outline curve + midrib/vein displacement + hue/value variation + tip damage, rendered orthographically to albedo, normal, translucency and alpha | Broad tropical leaves (heart, lance, split, paddle), palm and feather fronds, ribbon reeds; canopy leaf clusters | **High.** Real renders with clean normals and translucency at any resolution; fully original. Broad leaves are the dominant jungle shape, so this route carries most of L1 and L3 | 0 |
| **B. CC0 scans baked to cards**: render Poly Haven `fern_02`, `anthurium_botany_01`, `calathea_orbifolia_01`, `pachira_aquatica_01`, `nettle_plant`, `periwinkle_plant`, `moss_01` orthographically into atlases | Photoreal fern fronds, glossy broadleaf, groundcover, moss | **High.** These scans are excellent up close; as baked cards they cost a fraction of today's dense meshes | 0 |
| **C. ambientCG atlases** (`LeafSet0xx`, `Foliage001–008`, `ScatteredLeaves001–008`) | Leaf litter, grass and weed tufts, small generic foliage | **Medium.** Temperate species, so they're used only on the ground and at the edges, never as the tropical identity | 0 |
| **D. Meshy text-to-image sprite sheets** (`remove_background: true`, transparent PNG), cleaned in-house; normals derived with a height-from-albedo bake | What procedural does badly: veilmoss strands, hanging vines with leaves, bromeliad-like rosettes, flowers (duskbells, coral and white ambient flowers) | **Medium-high.** Output resolution isn't documented (to be measured on the first image); alpha and normals are less clean than routes A and B | 7.1 item 4 |
| **E. Meshy 3D plants rendered to cards** (bellcap, cupleaf) for distant LODs and sprinkled card versions | Card versions of our own alien flora | High (matches the 3D version) | 0 extra (reuses item 6–7 meshes) |

## 7. Meshy plan

### 7.1 Prioritized shopping list (best value first, fits in 938)

Unit costs (Meshy API pricing, checked 2026-10-09):
- Text-to-image: `nano-banana` 3 credits, `nano-banana-2` 6, `nano-banana-pro` 9.
- Image-to-image: the same three models at 3 / 6 / 9 credits.
- Image-to-3D: `meshy-6-lite` mesh only 5; `latest` mesh only 20, with textures 30, ultra geometry +5.
- Retexture: 10. Failed tasks are refunded.

| # | Asset | Method | Credit math (incl. retries) | Credits | Running total | Budget in game |
|---|---|---|---|---|---|---|
| 1 | **5 keyframes** (F1–F4 + P1): direction, LUT target, owner gate | Image-to-image with board crops as references: explore with `nano-banana`, final with `nano-banana-pro` | 5 × (2 × 3) + 5 × 9 | **75** | 75 | n/a (reference) |
| 2 | **6 backdrop layers** (rootstone range far / mid, mist valley vista, far falls, forest silhouette ×2) | Image-to-image with board crops, `nano-banana` explore → `nano-banana-pro` final, upscaled locally, cut and haze-matched in-house | 6 × (2 × 3) + 6 × (2 × 9) | **144** | 219 | 2048×1024 cards, ASTC 8x8, 2–4 tris each |
| 3 | **Stiltwood ×3** | Concept: text-to-image `nano-banana` ×2 → image-to-3D `meshy-6-lite` mesh-only silhouette test ×2 → `latest` mesh-only + ultra. No Meshy texture | 3 × (6 + 10 + 25) | **123** | 342 | LOD0 8k / LOD1 3k / LOD2 1k, impostor beyond 60 m; CC0 bark triplanar 1024² + moss mask |
| 4 | **8 flora sprite sheets** (veilmoss, hanging vines ×2, rosettes, duskbells, ambient flowers ×2, moss edge strips) | Text-to-image `nano-banana-2`, background removed, 2 tries each | 8 × 2 × 6 | **96** | 438 | Packed into the 2048² flora atlas |
| 5 | **Rootstone hero arch** (A/B against procedural) | Concept `nano-banana` ×2 → `meshy-6-lite` ×2 → `latest` mesh-only + ultra | 6 + 10 + 25 | **41** | 479 | LOD0 10k; triplanar rock |
| 6 | **Bellcap ×2** (risky-route cue) | Concept (`nano-banana` ×2 + `nano-banana-2` final) → lite silhouette test → `latest` textured; 1 extra retry for the pair | 2 × (12 + 5 + 30) + 30 | **124** | 603 | LOD0 2k, 1024² (from 4K, baked down) |
| 7 | **Cupleaf epiphyte ×2** (frame-edge hero plant) | Same as 6, no extra retry | 2 × (12 + 5 + 30) | **94** | 697 | LOD0 2.5k, 1024² |
| 8 | **Sailback** | Concept 12 → lite 5 → `latest` textured + ultra 35, 1 retry 35 | 12 + 5 + 35 + 35 | **87** | 784 | LOD0 1.5k, 512²; manual 8-bone rig |
| 9 | **Thorn bramble hazard** | Concept 12 → lite 5 → `latest` textured 30 | 47 | **47** | 831 | LOD0 2k, 512² |
| | **Reserve** (failures, a second backdrop pass, small fixes) | | | **107** | 938 | |

Not used: **text-to-3D.** Its preview costs 20 credits for a mesh with no control over the shape. An image-to-3D lite
test from our own concept costs 5 credits and keeps the design under our control.

### 7.2 Using the vision board as input
- The board is 1536×1024, so the crops are small: the hero image is about 725×445 px and the panel tiles are about
  130–200 px. Crops are cut locally with Pillow (free) into `design/concepts/2026-10-09/board_crops/`.
- **Crops go in only as image-to-image references** (mood, light, palette, density, composition). Each prompt names
  the replacements explicitly: floating islands → grounded braided rootstone pillars wrapped in mist; planet → open
  sky with cumulus; banshee-like flyers → removed or a small sailback.
- **They are never direct image-to-3D inputs:** they're low-resolution, mix many objects, and carry motifs we must not
  copy. Image-to-3D inputs are clean single-object concepts on plain grey (`environment.md` {FORMAT_PROP}).
- Crop map: hero image → F4, P1 and backdrops 2a–2c. Verdant Forest tile → F1. Riverlands tile → F3. "Choose your
  route" tiles → F2. Canopy tile → canopy atlas mood. RUN frame → path composition.
- Every output passes the originality checks (ART_DIRECTION 5.3, checklist A2/C4) before use.

### 7.3 Saving credits
- **Cheap stage first:** exploring with `nano-banana` (3) and `meshy-6-lite` mesh-only (5) means a rejected idea costs
  3–5 credits instead of 9–35. Only winners go to `nano-banana-pro` and `latest`.
- **Preview before refine, Meshy's version of it:** mesh-only first. Assets that use our in-house materials (stiltwood,
  rootstone arch) never buy a Meshy texture. Assets that need one (bellcap, cupleaf, sailback, bramble) go straight to
  textured once the lite mesh passes, because mesh 20 + retexture 10 costs the same as textured 30.
- **Free retries:** Meshy offers free retries in its **web app** (the number per task depends on the plan), not through
  the API that agents use. API runs are budgeted as fully paid. If the owner wants to save credits on a near miss, the
  coordinator can ask them to press "retry" in the web app. That saving isn't counted in this plan.
- Failed tasks are refunded automatically. Every spend is logged with a running total in `docs/STATUS.md`.
- **Licensing:** outputs are owned by the owner under the paid plan, as with Pista. Text-to-image and image-to-image run
  third-party image models inside Meshy, so **re-check Meshy's terms for those models before launch**. Every asset gets
  a row in `docs/LICENSES.md`.

## 8. Honest quality expectation per layer (with 938 credits)

| Layer | Expected quality on the phone | Main risk |
|---|---|---|
| Composition, light, atmosphere, grade | **Top**: no asset limit here, only craft | None beyond iteration time |
| L0 Path and edges | **High** (CC0 PBR textures, Blender roots, CC0 `single_root` / `dead_tree_trunk_02`) | — |
| L1 Foliage walls | **Medium-high.** Broad leaves and ferns are high (routes A and B), but there are only about 8–10 distinct species and some rosettes and vines come from sprite sheets. Up close in F1 and F4, the board's lushness depends on **variety** | **This is the layer that can miss "top" within 938** |
| L2 Stiltwoods, rootstone | **High** (procedural rootstone is strong; root fans are where Meshy is best) | Meshy cleanup time |
| L3 Canopy | **High** (procedural clusters + light cookie) | — |
| L4 Forest wall | **High** with our octahedral baker; **medium** if we fall back to billboards | Baker effort (about 1 week) |
| L5 Backdrops | **High** at phone size, if the Meshy image output is ≥ 1.5K on its long edge. If it's about 1K, the F4/P1 vista gets soft even after upscaling | Output resolution isn't documented: measure on the first image |
| L6 Sky | High (CC0 HDRI) | — |
| Water and falls | High (in-house shader + Blender flipbooks) | Iteration time |
| Alien flora and creatures | High for bellcap and cupleaf; medium-high for the sailback (manual rig) | — |

**Overall:** the 5 shots can match the board's **mood, depth and scale**. The likely gap is **near-foliage variety and
detail** in F1 and F4, plus possibly **vista sharpness**. That's what the extra ask below covers.

## 9a. Build order for look test v2 (credits per step)
1. **Board crops + keyframes** (item 1, 75 credits). The coordinator shows the owner 3 options per shot with a
   recommendation; under the current mandate, take the recommendation `[ASSUMED]` and log it.
2. **Graybox v2 layout:** S-curved 220 m layout, enclosure and reveal cadence, camera, segment merging, budget skeleton.
   Measure draws and triangles before adding any art. 0 credits.
3. **Light and atmosphere on the graybox:** sun angle, height fog + in-scatter, canopy cookie, shafts, mist, LUT from
   the keyframes. **Checkpoint A:** the grey world already feels deep. 0 credits.
4. **Backdrops** (item 2, 144 credits): measure the output resolution on the first image. **Checkpoint B:** F1 and F4
   against the keyframes. If the vista is soft, ask for E2.
5. **Rootstone procedural kit** in Blender + hero arch A/B (item 5, 41 credits).
6. **Stiltwoods** (item 3, 123 credits) + **impostor baker** + canopy atlas (route A).
7. **Understory:** procedural leaf generator (route A), CC0 scan bakes (route B), ambientCG ground (route C), sprite
   sheets (item 4, 96 credits). Merged per segment, vertex AO baked. **Checkpoint C:** F1/F2 foliage variety; if it's
   below the bar, ask for E1.
8. **Water and waterfall** upgrade. 0 credits.
9. **Signature flora and life:** bellcap, cupleaf, sailback, bramble (items 6–9, 352 credits), plus veilmoss, duskbell,
   ribbon reeds and whirlseeds.
10. **Pista in, art review** (ART_DIRECTION 10.3, grayscale and squint tests), then the **iPhone 12 device run** (ADR
    0004 protocol). Pass: ≤ 250 + 100 draws, ≤ 350k + 150k triangles, 60 fps sustained for 10 minutes.

## 9b. Separate ask: extra Meshy credits (beyond the 1,000; owner approval needed)

| # | Item | Why it's needed for top quality | Credit math | Credits |
|---|---|---|---|---|
| E1 | **6 more hero understory species** (original tropical-alien broadleaf and shrub clumps, textured, plus card versions via route E) | Raises L1 from about 9 to about 15 species. Variety is what makes a wall of foliage read as jungle rather than repeated clumps | 6 × (12 concept + 5 lite + 30 textured) | **282** |
| E2 | **Tiled high-resolution vistas** for F4/P1 and the F3 valley (each vista built from 4 overlapping image-to-image tiles at `nano-banana-pro`, 2 tries) | Only needed if the Meshy output is about 1K: keeps the hero vista sharp on a 460 ppi screen | 2 vistas × 4 tiles × 2 × 9 | **144** |
| E3 | **Retry reserve** for E1/E2 and the second-pass fixes the art review asks for | Lets reviews ask for a fix without stopping to ask for credits | — | **150** |
| | **Total extra** | | | **576 (round up to 600)** |

**Recommendation:** approve **+600 as a conditional reserve.** The art-director spends it only if Checkpoint B (section 9a
step 4, for E2) or Checkpoint C (step 7, for E1) shows the layer below the bar. Every spend is logged, so unused
credits stay unused.

## 10. Look test v2 shot list (vision-board mood targets)

Mood for all shots: late morning, sun low ahead-left, humid air, emerald + turquoise + gold, sky ≤ 20% of frame.
Pista ~14–18% of screen height (landscape) in the lower-centre third. Every frame passes the grayscale read: Pista and
pickups win.

**F1: "Out of the roots" (landscape, 25 m).** *Foreground:* Pista runs out from under the arched stilt roots of a
giant stiltwood. Roots frame the top and left edges. A cupleaf mass with a coral flower clump (not orange: orange is
kept for risky cues) overlaps the bottom left. *Midground:* curving trail climbing slightly right, dense foliage walls
with hanging vines, 2–3 gold light shafts crossing behind Pista, dust motes. *Background:* through a canopy gap, the
hazed rootstone range with two thin falls: the first "wow", and the only bright sky in the shot.

**F2: "The fork" (landscape, 80 m).** *Foreground:* a root ridge rising to the left, moss path ahead, coins on the
centre line. *Midground:* three readable routes: **left** a sunlit raised root ridge lined with orange bellcaps and
pollen (risky); **centre** a wide soft moss path in even light (safe); **right** a violet veilmoss curtain half-hiding
a cool dark opening with duskbells and a cyan glint (secret). *Background:* a stiltwood trunk 30 m tall in haze, and
the canopy roof closing overhead.

**F3: "The ford" (landscape, 124 m).** *Foreground:* Pista mid-stride in ankle-deep turquoise water with splash and
ripple rings, stepping stones, ribbon reeds bending away. *Midground:* the river curving away to the right, foam lines
around boulders, two sailbacks gliding across, whirlseeds spinning down in a sun shaft. *Background:* the river valley
opening to a rootstone pillar with a waterfall bursting from its side, wrapped in mist.

**F4: "The basin" (landscape, 190 m). The board's hero shot from behind.** *Foreground:* a wet rock ledge with
drifting spray, Pista seen from behind and slightly above, with big framing leaves at the bottom left. *Midground:*
the hero waterfall pouring from the underside of a rootstone arch into a churning turquoise plunge pool, a faint
rainbow in the spray, terraced pools stepping down. *Background:* a vast mist valley of falls and stacked rootstone
pillars and arches receding into pale haze, with towering cumulus above. It should feel like a world much bigger
than Pista.

**P1: "The basin, portrait" (portrait, 190 m).** Same place as F4, recomposed vertically: Pista at ~12% of screen
height in the bottom third, ledge and spray in the foreground; the hero waterfall drop fills the middle third
vertically; rootstone pillars and the cumulus fill the top third. The sky shows only between landforms.

## 11. Record: market research behind revision 1 (declined by the owner, 2026-10-09)
Checked 2026-10-09, for the record only:
- SeedMesh "Jungle – Tropical Vegetation": $39.99, HDRP-first, URP on 6000.3.
- Baldinoboy "Tropical Forest Pack": $24, 2017 art.
- Amplify Impostors: $60.
- GPU Instancer Pro: $64 on sale.
- BOXOPHOBIC "The Visual Engine": $90.
- SpeedTree Library: $999/yr, GUI only.
- Megascans on Fab: paid per item.
- OpenAI gpt-image: about $0.17–0.25 per high-quality image.

None was mobile-targeted. All are declined; nothing was bought. If the near foliage (L1) still misses the bar after
E1, a vegetation pack is the cheapest remaining lever, and it would come back to the owner as a new question.

## Sources (checked 2026-10-09)
- [Meshy API pricing](https://docs.meshy.ai/en/api/pricing), [Meshy text-to-image API](https://docs.meshy.ai/en/api/text-to-image), [Meshy image-to-image API](https://docs.meshy.ai/en/api/image-to-image), [Meshy web app pricing (failed generations not charged)](https://docs.meshy.ai/en/webapp/pricing), [Meshy credits guide](https://www.meshy.ai/tutorials/meshy-credits-guide)
- Poly Haven model list via `https://api.polyhaven.com/assets?t=models`; ambientCG via `https://ambientcg.com/api/v2/full_json`
- Revision 1 research: [Jungle – Tropical Vegetation](https://assetstore.unity.com/packages/3d/environments/jungle-tropical-vegetation-178966) ([reviews](https://assetstore.unity.com/packages/3d/environments/jungle-tropical-vegetation-178966/reviews)), [Tropical Forest Pack](https://assetstore.unity.com/packages/3d/environments/tropical-forest-pack-49391), [Amplify Impostors](https://assetstore.unity.com/packages/tools/utilities/amplify-impostors-119877), [GPU Instancer Pro](https://assetstore-fallback.unity.com/packages/tools/utilities/gpu-instancer-pro-290293), [The Visual Engine](https://marketplace.unity.com/packages/tools/utilities/the-visual-engine-286827), [SpeedTree](https://unity.com/kr/products/speedtree), [SpeedTree Library](https://support.unity.com/hc/en-us/articles/49699142648980-How-do-I-receive-my-SpeedTree-Library-purchase), [Megascans on Fab](https://www.cgchannel.com/2024/10/epic-games-has-made-megascans-free-to-all-but-only-until-the-end-of-2024/), [OpenAI GPT Image 1.5](https://developers.openai.com/api/docs/models/gpt-image-1.5)
