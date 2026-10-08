# Environment Strategy: from "sunny park avenue" to the vision board, at 60 fps on iPhone 12

**Owner:** art-director | **Status:** Proposal, waiting for owner approvals (section 9) | **Date:** 2026-10-09
**Inputs:** `BLUEPRINT.md`, `vision_board.png`, `ART_DIRECTION.md`, `LOOK_TEST_BRIEF.md`, `docs/adr/0004-realistic-look-on-mobile.md`
(measured numbers), `design/DECISIONS.md`, look-test shots `/tmp/junglebooze-shots/` (2026-10-09 build).
**Note on a past decision:** the owner decided on 2026-10-08 to use "no paid asset packs". On 2026-10-09 the owner added that
quality is never cut to save money and that extra spend should be asked for with costs. This plan asks for a small
purchase (section 9). It does not assume the old rule has changed.

---

## 1. Why the look test reads as a park (diagnosis)

| # | Problem in the current frames | What the vision board does instead | Fix (section) |
|---|---|---|---|
| 1 | **Composition:** a dead-straight trail running to a vanishing point, with trees in two neat rows. This reads as a park avenue | Paths curve, climb and drop. Foliage frames the shot. The view opens up to a reveal | Curved, rising layout with enclosure, then a reveal (4.1) |
| 2 | **Empty sky:** 30–50% of the frame is flat blue sky, more in portrait | Sky is under 20% of the frame. The upper frame is full of canopy, cliffs, landforms and falls | Canopy roof + matte backdrop layers (4.2) |
| 3 | **Mown lawn:** flat green ground, sparse ferns, no layers | Six layers of vegetation: moss and groundcover, ferns, broadleaf, shrubs, trunks with vines, then canopy | Dense path walls built from merged clumps (4.3) |
| 4 | **No scale or identity:** thin, temperate-looking trees and no landmark | Giant structures, waterfalls, depth over kilometres | Rootstone, stiltwoods, a waterfall valley (5) |
| 5 | **Flat midday light:** no backlight, no shafts, no haze gradient | Sun into the camera, glowing leaf edges, mist, light shafts, haze that fades to pale | Lighting and atmosphere pass (4.4) |
| 6 | **Water is a grey strip** off to the side | Turquoise water is the hero of almost every view | Water moves into frame and gets a flow and foam upgrade (4.5) |
| 7 | **Path is uniform orange-brown** with a hard edge and is 6 m wide | Worn soil with roots, stone, moss creeping in and leaf litter | New path material and edge dressing (4.3) |

**Performance problem (ADR 0004, measured):** 810–930 draw calls and 0.71–0.81 M triangles against a budget of 250+100
draws and 350k+150k triangles. The cause is 560–700 separate renderers per view: every fern, plant and tree is its own
object, and the CC0 plant scans are dense. **The look and the budget have the same fix:** fewer, larger, art-directed
pieces (merged clumps, impostors, painted layers) instead of hundreds of small scanned objects.

## 2. Key finding from the market research

**No product on the market gives us the vision board on mobile.** Every realistic tropical pack is either built for HDRP
first or several years old, and none targets mobile URP (research in section 6). The board's identity also comes from
giant original landforms, and the Blueprint (Part II) requires those to be ours. So:

- **Buy** only what is generic and expensive to make well: tropical understory meshes with LODs, vines and leaf
  textures. Also buy one proven tool, an impostor baker.
- **Make** everything that defines AURELIA: rootstone, stiltwoods, bellcaps, the waterfall valley, the backdrops and
  the creatures. We make them with AI (Meshy, gpt-image), procedural Blender tools, and our own shaders.
- **Most of the "wow" comes from free techniques:** composition, layered backdrops, atmosphere and lighting. These come
  first in the build order, so we can tell early whether they work.

## 3. Constraint that shapes tool choices: agents must be able to drive the tool

The environment is built by agents working headless on the owner's Mac (Unity batch mode, Blender Python, HTTP APIs).
A tool that only works through a GUI can't go into the pipeline, however good it is. This rules out the SpeedTree
Modeler, Gaia, MicroVerse and similar hand-painting editors. Asset Store packs are fine: the owner buys and downloads
them once, and agents import from the local cache.

## 4. Techniques that create the "wow" cheaply on mobile

Costs are estimates for an A14 at about 1.7 MP. All of them fit ADR 0004 (Forward path, no depth texture, no lightmaps).

### 4.1 Composition (free, biggest effect)
- **S-curved path** with 8–15° bends every 30–50 m, plus climbs and drops of 1–4 m. The far path disappears behind
  foliage, so the horizon is never a vanishing point.
- **Enclose, then reveal:** 70% of the run is enclosed (foliage walls, overhead canopy, root arches) and 30% opens up
  (river, canopy gap, falls vista). Contrast creates the scale; open land everywhere reads as a park.
- **Framing elements** in the near corners: a leaf mass, a root or a flower clump overlapping the frame edge, as in the
  bottom left of the board.
- Tighter path: **4–5 m of visible trail** inside a 6–8 m runnable width, with roots and moss covering the edges.

### 4.2 Layered planes (each layer has one technique)
| Layer | Distance | Technique | Budget share (worst view) |
|---|---|---|---|
| L0 Path and edge dressing | 0–15 m | 3D: roots, stones, moss strips, merged per chunk | 40 draws / 50k tris |
| L1 Foliage walls | 2–40 m | Clumps of the bought understory, merged by material per 25 m segment, one foliage atlas | 30 draws / 90k tris |
| L2 Trunks, stiltwoods, rootstone | 10–120 m | 3D with 3 LODs and cross-fade | 50 draws / 90k tris |
| L3 Canopy roof | overhead | One merged mesh of leaf cards per segment, colored by vertex color. **It doesn't cast shadows.** The dappled light comes from a scrolling **light cookie** on the sun instead (much cheaper than a canopy in the shadow pass) | 10 draws / 25k tris |
| L4 Forest wall | 40–300 m | **Octahedral impostors** (1 quad per tree), instanced | 10 draws / 2k tris |
| L5 Backdrop | 300 m–3 km | **3–4 matte-painted cards** (rootstone range, mist valley, far falls) with parallax, haze baked into the paint, and a slow-scrolling mist card between them | 6 draws / <1k tris |
| L6 Sky | ∞ | HDRI or painted sky dome with towering cumulus, graded | 1 draw |
| Pista, pickups, FX | | | 40 draws / 35k tris |
| **Total** | | | **~190 main draws / ~300k tris** (budget 250 / 350k) |

### 4.3 Vegetation rendering
- **Merge per segment at build time** (ADR 0004 fix 1). Chunks move as whole units, so merging inside a chunk is safe.
  Target: at most 12 draws per 25 m segment.
- **One shared 2048² foliage atlas** for the understory, plus one for the canopy and one for flowers and alien flora,
  so merged meshes share a material.
- **Vertex colors do most of the lighting work:** R/G/B carry wind weights (ADR 0004). A second channel set (UV2)
  carries **baked AO and "under the canopy" darkening**, made in Blender per prefab. This gives the dark understory
  and bright gaps that make a jungle read as one, without lightmaps or SSAO.
- **Wind in the vertex shader** (exists in `Nature Lit`). Add stronger motion for broad leaves.
- **LOD:** 3 levels with dithered cross-fade (works with alpha-to-coverage). Unity 6.3 import-time Mesh LOD is used
  for the 3D props.
- **Overdraw control:** cards are cut close to the leaf shape. Big broad leaves are real geometry (opaque, so the GPU
  can skip hidden pixels), and alpha cards are kept for fronds and fine leaves only.

### 4.4 Light and atmosphere (cheap, high impact)
- **Sun ahead-left and low (25–35°), backlighting the foliage.** Leaf translucency (already in the shader) makes the
  gold-green rim of light the board has.
- **Height fog + sun-direction in-scatter** in the custom fog: warm haze toward the sun, cool haze away from it, and
  denser low fog in hollows and over water. This is maths in the shader only, with no extra pass.
- **Light shafts:** 2–4 additive mesh cards per enclosed view, faded by view angle. **Dust motes** (≤ 60 particles).
- **Color grade LUT built from the approved concept keyframes** (section 7, step 1), so the grade aims at a picture
  the owner signed off rather than at numbers.
- **Mist cards** at the base of every landform and falls, so giant structures sit in air.

### 4.5 Water
- River moves into frame (crossings, a ford, a path beside the bank). Add a **flow map**, foam along banks and rocks
  painted into vertex color, and a sky reflection band. This is the existing shader extended.
- Waterfalls: 2–3 layered scrolling sheets (core, broken edges, spray streaks), a mist billow flipbook at the base, and
  a wetness mask on nearby rock. One hero waterfall per vista plus 3–6 thin falls in the backdrop paint.

## 5. What makes the world ours (signature asset plan)

| Asset | Method | Why this method |
|---|---|---|
| **Rootstone kit** (arches, pillars, root-bridges, outcrops, cliff walls) | **Procedural in Blender** (a geometry-node "braid" generator: twisted strand curves → mesh → triplanar CC0 rock + strand normal detail + moss mask) | Fully original, unlimited variants, clean LODs and UVs, controllable silhouettes. Meshy makes one hero arch as an A/B comparison |
| **Stiltwood** (3 trunk-and-root variants) | Meshy image-to-3D from our concepts → cleanup and retexture (CC0 bark + moss), crowns from the canopy card atlas, impostors for distant trees | Organic root fans are where Meshy is strong; crowns as cards keep the triangle budget |
| **Bellcap, veilmoss, duskbell, ribbon reed** | Bellcap: Meshy. The others: gpt-image transparent atlases on simple Blender card meshes | Meshy can't make thin strands or alpha cards; atlases can |
| **Hero epiphytes** (2 large broadleaf "cupleaf" clumps for frame edges) | Meshy | Big opaque leaves read as rich and cost little overdraw |
| **Sailback**, thorn bramble hazard | Meshy, then a manual rig (sailback) | As in the look-test brief |
| **Whirlseed** | Hand-made card mesh + gpt-image texture | 150 triangles; Meshy isn't needed |
| **Backdrops** (rootstone range, mist valley, far falls, forest silhouettes) | gpt-image matte paintings, upscaled locally (Real-ESRGAN, BSD license), cut into layers | The fastest way to kilometres of original scale |

Originality checks (ART_DIRECTION 5.3) apply to every concept: no floating land, no planets, no glowing flora. The
"floating" feel of the board comes from mist around grounded pillars.

## 6. Market research (checked 2026-10-09)

### 6.1 Asset Store and other paid options
| Option | Price | Covers | URP / Unity 6 / mobile | Generic look risk | Verdict |
|---|---|---|---|---|---|
| **Jungle – Tropical Vegetation** (SeedMesh Studio) | **$39.99** | 11 plant species / 49 variations, 12 climbing-plant (vine) variations, 18 tree variations, billboards, LODs, 4K textures, moss shader; 2.8 GB; v3.4.0, **updated 24 Jan 2026** | URP compatible on **6000.3.2f1** (store table). Built for HDRP: the URP conversion loses thickness-map translucency, and reviews mention dark LODs and "pink until converted". We re-material everything into `Nature Lit` anyway, so that drawback mostly disappears. 21 reviews, mostly positive. Not built for mobile: we'd downsample the 4K textures to 1–2K and merge | **Medium:** a popular pack, but understory plants are generic by nature. Identity comes from section 5 | **Recommended** as the understory and vine library |
| **Tropical Forest Pack** (Baldinoboy) | $24 | 13 trees (banana, coconut, kapok…), 12 groundcovers, rocks; 500–3,000 triangles each with LODs and billboards; v1.3.6 (Dec 2024); 101 ratings | URP on 6000.0.23f1. Low triangle counts suit mobile, but the art dates from 2017 | **High:** recognisable Earth species (coconut, banana) read as "beach resort", and the art is dated | Fallback only |
| **Amplify Impostors** (Amplify Creations) | **$60** / seat | Octahedral and spherical impostor baker and shaders; v1.0.4 (28 May 2026); 112 reviews | URP listed. The store table lists up to 2022.3; Unity 6 support appears in the changelog. **Verify on 6000.3 before purchase** | None (a tool) | **Recommended:** gives L4 with true parallax at 1 quad per tree |
| GPU Instancer Pro (GurBu) | $64 on sale ($128 list) | GPU-driven instancing and culling | Unity 6 + URP; Metal supported per FAQ | None | **Not now.** Merging (4.3) should reach the budget. Fallback if draw calls stay above 250 |
| The Visual Engine (BOXOPHOBIC) | $90 | Vegetation shader and wind framework (successor to TVE, v22.1.0, Sep 2026) | Unity 6; pipeline and mobile support not stated on the page | None | Not needed: `Nature Lit` already covers wind and translucency |
| SpeedTree Indie + Library | $19/mo + **$999/yr** Library | Best tree authoring and library | Unity-native | Medium | **Rejected:** GUI-only (agents can't drive it) and the Library costs too much for what we'd use |
| Fab / Megascans plants | Per item, no longer free | Scanned plants and vines (e.g. Tropical Climbing Plants Set 02: 37 vines, 5–6 LODs, 2K) | FBX works in Unity under the Fab Standard License | Medium | Optional later, per item, if SeedMesh vines fall short |
| Stylized or low-poly packs (Synty POLYGON Tropical, etc.) | $15–60 | — | Mobile-ready | — | **Rejected:** wrong style (owner chose realistic) |

License: Unity Asset Store Standard EULA. Commercial games are fine. Source files must not be redistributed, so they
**stay in a private repository** and are never published. Both recommended items are licensed per seat (owner = 1 seat).
Asset Store purchases are generally **not refundable once downloaded**.

### 6.2 AI services
| Service | Unit cost (verified) | Use |
|---|---|---|
| Meshy image-to-3D (`latest` / meshy-7.1) | 30 credits with 4K textures, +5 for 2K "ultra" geometry; mesh only 20; retexture 10; rigging 5 | Signature 3D props (section 5) |
| Meshy text-to-image (GPT Image model) | 9 credits per image | Fallback route for concept images if OpenAI spend isn't approved (size and transparency options to be verified) |
| OpenAI gpt-image (high quality, 1536×1024) | about $0.17 (gpt-image-2, third-party estimate) to $0.20–0.25 (gpt-image-1.5 / 1, OpenAI model pages); edits with reference images cost more. **Budgeted at $0.30 per image** | Keyframes, prop concepts, matte paintings, foliage atlases |

## 7. The recommended plan: "Original landforms + bought understory + painted depth"

### 7.1 Cost breakdown
| Item | Cost | What it buys |
|---|---|---|
| SeedMesh Jungle – Tropical Vegetation | **$39.99** + tax | Understory, vines and leaf textures with LODs and billboards. Replaces the houseplant-looking CC0 scans |
| Amplify Impostors | **$60** + tax | Forest wall (L4) and distant stiltwoods at about 1 draw call per tree type |
| OpenAI gpt-image | **expected ~$45, cap $75** | See 7.2 |
| Meshy | **~610 credits expected, from the pre-approved 1,000** (≈ 938 left); ~330 stay in reserve | See 7.3 |
| Everything else (Blender procedural rootstone, shaders, merging, fog, water, impostor setup, upscaling) | $0 | Built in-house |
| **Total new money** | **≈ $100 for packs + ≤ $75 OpenAI = at most ~$175 plus sales tax** | |

### 7.2 OpenAI gpt-image estimate (per asset)
| Use | Images (incl. retries) | ≈ $ |
|---|---|---|
| 4 mood keyframes + 1 portrait (paint-over targets, 3 options each for the owner) | 15 + 10 edit rounds | 7.50 |
| Prop concepts for Meshy (stiltwood ×3, hero arch, bellcap ×2, epiphyte ×2, sailback, bramble) | 30 | 9.00 |
| Matte-painting backdrop layers (rootstone range far/mid, mist valley, far falls, forest silhouette ×2, vista) | 28 | 8.40 |
| Foliage and flora atlases (canopy clusters, hanging vines, veilmoss, ribbon reeds, duskbells, fronds, moss edge strips, petals) | 36 | 10.80 |
| Sky with cumulus, light-shaft and mist sprites, rootstone strand detail and jungle path albedo references | 24 | 7.20 |
| **Total** | **~143** | **~$43** (cap $75 for extra retries) |

### 7.3 Meshy credit estimate (per asset)
| Asset | Generations × credits | Credits |
|---|---|---|
| Stiltwood trunk-and-roots ×3 (ultra geometry, 2 attempts each) | 6 × 35 | 210 |
| Rootstone hero arch, A/B against procedural (2 attempts) | 2 × 35 | 70 |
| Bellcap clump ×2 (+1 retry) | 3 × 30 | 90 |
| Hero epiphyte clump ×2 | 2 × 30 | 60 |
| Sailback (2 attempts) | 2 × 35 | 70 |
| Thorn bramble hazard | 1 × 30 | 30 |
| Retexture passes to the shared material look | 8 × 10 | 80 |
| **Total** | | **610** (free retries used first; every spend logged in `docs/STATUS.md`) |

### 7.4 Why cheaper options fall short
- **CC0 only (today's build):** Poly Haven's plants are photo scans of potted plants (dense, few species, no vines, no
  tropical canopy), and its leaf atlases read as a temperate forest. The screenshots show the result. There are no
  vines, no epiphytes and no game-ready LOD chains.
- **AI only for vegetation:** Meshy can't make alpha-card foliage. Thin leaves come out as solid blobs, so a jungle
  built from Meshy plants would be both heavy and lumpy. gpt-image atlases help with cards but not with LOD-ready
  meshes.
- **No impostor tool:** we could bake billboards in Blender, but flat billboards visibly rotate at the 40–150 m
  distances a runner camera sees for seconds at a time. Octahedral impostors hold up there, and writing our own is
  weeks of shader work for a $60 problem.
- **Money doesn't fix composition and light.** That's why steps 2–3 below come before any purchased asset goes in.

### 7.5 Build order for look test v2
1. **Keyframes (gpt-image, about 1 day).** Paint the 5 shots in section 10 from the vision board's mood, in our world
   language, with 3 options each. The coordinator shows the owner. These become the targets for the LUT and composition.
2. **Graybox v2 layout** (tech-architect + art-director): the S-curved, rising 220 m layout, enclosure and reveal
   cadence, camera, segment merging, budget skeleton. Measure draws and triangles **before** adding art.
3. **Light and atmosphere on the graybox:** sun angle, height fog + in-scatter, canopy light cookie, shafts, mist, LUT.
   **Checkpoint A:** if a grey world doesn't already feel deep and moody, fix it here.
4. **Backdrop L5/L6:** matte layers + sky. **Checkpoint B:** frames F1 and F4 against the keyframes.
5. **Rootstone procedural kit** (Blender) + Meshy hero-arch A/B → pick one.
6. **Stiltwoods** (Meshy) + canopy atlas + **impostors** (Amplify) for L4.
7. **Understory** from SeedMesh, re-materialed into `Nature Lit`, atlased, vertex-AO baked, merged per segment.
8. **Water and waterfall** upgrade (flow map, foam, layered falls, mist).
9. **Signature flora and life:** bellcaps, veilmoss, duskbells, ribbon reeds, whirlseeds, sailbacks, hazard.
10. **Pista in, art review** (ART_DIRECTION 10.3 checklist, grayscale read, squint test), then the **device run on
    iPhone 12** (ADR 0004 protocol). Budget: ≤ 250 + 100 draws, ≤ 350k + 150k triangles, 60 fps for 10 minutes.

## 8. Fallback plan (if the owner declines the purchases)
**CC0 + AI + in-house only:** gpt-image atlases on Blender-made procedural clumps replace SeedMesh (ferns, broadleaf,
vines). Our own 8-view billboard baker replaces Amplify, with an accepted risk of visible billboard turning at 40–80 m;
we compensate with more foliage walls and fog. Cost: OpenAI cap stays $75. Meshy rises to about 700 credits (more
foliage concepts), still inside the pre-approved 1,000. Risks: about +1–2 weeks of agent time and lower understory
fidelity. I'd expect this route to reach "good", not "board-level", for the near foliage.
**If OpenAI is also declined:** concept images go through Meshy text-to-image (GPT Image, 9 credits each, ≈ 140
images ≈ 1,260 credits). That **goes past the pre-approved 1,000**, so it would need a Meshy approval instead. It's
cheaper to approve the OpenAI credit.
**Escalation if v2 still misses the bar:** a freelance human environment concept and matte artist for the keyframes
and backdrops (rough market range $1,500–5,000). Not requested now; it would come as a separate owner question
after look test v2.

## 9. Owner approval list (one message)
| # | Item | Cost | Recommendation |
|---|---|---|---|
| 1 | Revisit the 2026-10-08 "no paid asset packs" rule for these two items only | — | Yes |
| 2 | Buy **Jungle – Tropical Vegetation** (SeedMesh Studio), Unity Asset Store | **$39.99** + tax, non-refundable once downloaded | Yes |
| 3 | Buy **Amplify Impostors** (Amplify Creations), Unity Asset Store, after an agent confirms Unity 6000.3 support from the publisher's notes | **$60** + tax | Yes |
| 4 | Add OpenAI API credit for gpt-image (the account is out of credit) with a spend cap | **$75 cap** (expected ~$45) | Yes |
| 5 | Use about 610 of the pre-approved Meshy credits on the section 7.3 list | 0 new money (inside the 1,000) | For information only |
| | **Total new money** | **≈ $175 + tax** | |

The owner would buy items 2–3 with their Unity account. Agents then import them from the local Asset Store cache and
record each in `docs/LICENSES.md` (tool, plan, license: Standard Unity Asset Store EULA, per seat).

## 10. Look test v2 shot list (vision-board mood targets)

Mood for all shots: late morning, sun low ahead-left, humid air, emerald + turquoise + gold, sky ≤ 20% of frame.
Pista ~14–18% of screen height (landscape) in the lower-centre third. Every frame passes the grayscale read: Pista and
pickups win.

**F1: "Out of the roots" (landscape, 25 m).** *Foreground:* Pista runs out from under the arched stilt roots of a
giant stiltwood. Roots frame the top and left edges, and a leaf mass with a coral flower clump (not orange: orange is kept for risky cues)
overlaps the bottom left. *Midground:* curving trail climbing slightly right, dense foliage walls with vines, 2–3 gold light
shafts crossing behind Pista, dust motes. *Background:* through a canopy gap, the hazed rootstone range with two
thin falls. That's the first "wow" and the only bright sky in the shot.

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
Portrait is where the vertical scale of falls and pillars pays off.

---

## Sources (checked 2026-10-09)
- [Jungle – Tropical Vegetation, Unity Asset Store](https://assetstore.unity.com/packages/3d/environments/jungle-tropical-vegetation-178966) and [reviews](https://assetstore.unity.com/packages/3d/environments/jungle-tropical-vegetation-178966/reviews); contents per [80.lv](https://80.lv/articles/unity-deals-working-on-tropical-vegetation/); [ArtStation listing](https://www.artstation.com/marketplace/p/9z8x/jungle-tropical-vegetation-unity-package)
- [Tropical Forest Pack, Unity Asset Store](https://assetstore.unity.com/packages/3d/environments/tropical-forest-pack-49391); [release thread](https://discussions.unity.com/t/released-tropical-forest-pack/675386)
- [Amplify Impostors, Unity Asset Store](https://assetstore.unity.com/packages/tools/utilities/amplify-impostors-119877)
- [GPU Instancer Pro](https://assetstore-fallback.unity.com/packages/tools/utilities/gpu-instancer-pro-290293)
- [The Visual Engine](https://marketplace.unity.com/packages/tools/utilities/the-visual-engine-286827); [TVE Mobile module (deprecated)](https://assetstore.unity.com/packages/vfx/shaders/the-vegetation-engine-mobile-shaders-module-192669)
- [SpeedTree pricing (Unity)](https://unity.com/kr/products/speedtree); [SpeedTree Library subscription](https://support.unity.com/hc/en-us/articles/49699142648980-How-do-I-receive-my-SpeedTree-Library-purchase)
- [Megascans on Fab after 2024](https://www.cgchannel.com/2024/10/epic-games-has-made-megascans-free-to-all-but-only-until-the-end-of-2024/); [Tropical Climbing Plants Set 02 on Fab](https://www.fab.com/listings/8aad7490-6a5d-409f-b5bb-744944104d72)
- [POLYGON Tropical Jungle (Synty)](https://syntystore.com/products/polygon-tropical-jungle-nature-biome); [Mobile Tree Bundle](https://marketplace.unity.com/packages/3d/vegetation/trees/mobile-tree-bundle-254384)
- [Meshy API pricing](https://docs.meshy.ai/en/api/pricing)
- [OpenAI GPT Image 1.5](https://developers.openai.com/api/docs/models/gpt-image-1.5), [GPT Image 1](https://developers.openai.com/api/docs/models/gpt-image-1), [gpt-image-2 estimate (GMI Cloud)](https://docs.gmicloud.ai/model-quickstarts/image/gpt-image-2-generate.md)
- [Unity 6 GPU Resident Drawer / Fantasy Kingdom mobile](https://unity.com/en/blog/engine-platform/unity-6-preview-release)
