# Phase 0 Look Test Brief: forest and waterfall stretch

**Owner:** art-director (contents, sources, art budgets) · tech-architect (scene builder, rendering, final budgets) |
**Status:** Draft for P0-B and P0-C/D. `[ASSUMED]` items wait for owner review | **Last updated:** 2026-10-08

**Goal (owner check, phase 0):** "Looks professional on the phone and runs smoothly." One stretch of realistic forest
and waterfall, ~220 m long, with realistic Pista running through it on a loop. No gameplay systems are needed;
obstacles and coins are placed only so we can judge readability.

Read with `design/aurelia/ART_DIRECTION.md` (look rules, palette, lighting, camera, style-lock checklist).
Target device: **iPhone 12/13 class (A14/A15) at 60 fps, full quality** (owner update 2026-10-08). The scene also
runs on the iPhone 11 for comparison so the owner can decide the minimum device.

---

## 1. Layout (run direction = +Z)

The stretch is 200 m of content plus a 20 m seam that loops back to the start, so Pista can run indefinitely for the
smoothness test. Runnable width 6–8 m (free horizontal movement), with dressing out to ~40 m each side and backdrop
cards beyond.

| Section | Range | What the player sees | Purpose |
|---|---|---|---|
| **S1 Forest gate** | 0–40 m | Pista runs out from under the stilt roots of a giant stiltwood that arch over the path. Dense ferns and broad-leaf understory on both sides, 2–3 god rays from ahead-left, gold motes. At ~30 m, a gap in the canopy shows the distant rootstone range (first "wow") | First 10 seconds of the board: lush, sunlight, depth, landmark (blueprint 4.1) |
| **S2 Root run** | 40–95 m | A thick root crossing the path (jump-height obstacle at 52 m), a fallen log between two rocks at slide height (66 m), a line of coins (58–75 m). At 75–95 m a **route-fork tease**: center = safe moss path (green); left = a raised root ridge in direct sun lined with bellcaps (gold/orange, risky); right = a veilmoss curtain half-hiding a cool opening with duskbells and a faint cyan glint (violet, secret). All three merge again at 95 m | Hazard readability; route cues as environment (ART_DIRECTION 6) |
| **S3 Riverbank** | 95–150 m | The path meets a clear turquoise river on the right and follows it. At 120–128 m the path crosses a shallow ford (ankle-deep, stepping stones, splashes at each footfall). Two **sailbacks** glide across the river at ~110 m. **Whirlseeds** spin down through sun shafts | Water quality, reactive water, ambient life |
| **S4 Falls basin** | 150–200 m | A ~30 m waterfall bursts out of the underside of a rootstone arch ahead-right and drops into a plunge pool. The path runs along a wet rock ledge past the pool, through drifting spray (faint rainbow optional). At 185–200 m the forest opens: a **vista** over a valley of mist, river and falls toward the rootstone range | The board's hero shot from behind Pista; the "screenshot moment" |
| **S5 Loop seam** | 200–220 m | A short tunnel under a dense tangle of roots (darker, cool) that hides the cut back to S1 | Invisible loop |

### 1.1 Judge frames (capture on device)
F1: S1 at 30 m, landmark through the canopy gap. F2: S2 at 80 m, the three-way fork. F3: S3 at 124 m, Pista in the
ford with splashes. F4: S4 at 190 m, vista with the waterfall and the range. The owner judges these four frames on the
phone, plus a 60-second continuous run.

## 2. Terrain and path
- Path: packed warm-earth soil with leaf litter and embedded small stones, low micro-contrast in the central 4 m.
  Moss creeps in from the edges. Wet and darker near the river and falls.
- Terrain: hand-shaped meshes per section (not Unity Terrain) `[ASSUMED, tech-architect decides]`, vertex-color
  blended between 4 ground materials (path soil, mossy ground, forest litter, riverbed pebbles) plus a wetness mask.
- Banks drop 1–3 m to the river; low rootstone outcrops break up the edges.

## 3. Trees, flora, rocks

| Element | Description | Where |
|---|---|---|
| **Stiltwood** (hero tree) | 35–50 m tall, trunk ~3 m wide, standing on a fan of 8–12 stilt roots 4–8 m high that you can run under. Pale grey-brown bark with moss on the north side, canopy far above (mostly cards) | 2 near the path (one arches over S1), 6–10 mid-ground, many as impostors in the far forest |
| **Canopy fill** | Generic broad-leaf trees for the mid and far forest wall | Impostors and backdrop cards |
| **Understory** | Ferns, broad glossy leaves (anthurium-, calathea-like), braided-trunk shrubs | Both sides, dense, 0–15 m from the path |
| **Bellcap** (alien flora, risky cue) | Waist-to-shoulder-high stalks with drooping orange-gold bell flowers ~40 cm long; exhale a puff of gold pollen when Pista passes within 2 m | S2 left ridge (risky), 2–3 small clumps elsewhere in S4 sun |
| **Veilmoss** (alien flora, secret cue) | Hanging curtains of fine violet-blue moss strands, 3–6 m long, from roots and arches; sway and part | S2 right opening (secret), behind the waterfall edge in S4 |
| **Duskbell** (alien flora, secret cue) | Small clusters of violet bell flowers at ground level, slightly cool-toned | Around secret openings only |
| **Ribbon reeds** (alien flora) | River-edge reeds with long flat ribbon leaves, green top and turquoise underside, bow away as Pista passes | S3 river edges, S4 pool edge |
| **Moss** | Thick cushions on rocks, roots and path edges | Everywhere, densest in S1 and S4 |
| **Rocks** | Mossy river boulders, path-edge stones, cliff faces around the basin | S2–S4 |
| **Rootstone** (signature landform) | Twisted, braided rope-like stone; arches, pillars, outcrops (ART_DIRECTION 5.1) | One arch with the waterfall (S4), 2 pillars mid-ground, the far range |
| **Roots** | Exposed tangled roots at path edges, one crossing root obstacle | S1, S2, S5 tunnel |

## 4. River, waterfall, sky, landmark
- **River** (S3–S4): 8–15 m wide, clear turquoise shallows, deep teal center, flow from ahead to behind (toward the
  camera), foam around rocks, a sky reflection band at grazing angles. Shallow ford at 120–128 m.
- **Waterfall** (S4): ~30 m drop, ~6 m wide ribbon from a mouth in the rootstone arch underside, plus 2 thin side
  streams over the arch lip. Mist billow at the base, spray drifting across the path, wet dark rock around.
- **Sky:** late-morning HDRI sky dome with cumulus (section 5, S-01), graded to the palette. Sun ahead-left.
- **Distant landmark:** the **rootstone range**: a chain of colossal grounded rootstone pillars and arches 2–5 km
  away, some with waterfalls, hazed to ~70% haze color, with mist at their bases. Built from 2–3 layered matte cards
  plus a low-poly silhouette mesh for parallax. Visible in S1 (canopy gap), S3 (over the river) and S4 (vista).

## 5. Ambient creatures (easy to animate)

| Creature | Look | Behavior | Animation cost |
|---|---|---|---|
| **Whirlseed** (floater) | An oversized winged seed (~40 cm wing), matte tan-gold wing with rust veins, slightly translucent in backlight. Not glowing | Spins down slowly through sun shafts, drifting with the wind; 5–10 on screen in S1/S3 | None: one rigid mesh, spin + drift in a vertex shader or a pooled particle mesh |
| **Sailback** (glider) | A ~60 cm wingspan gliding lizard: bronze-olive scaled body, long tail, rib-supported side membranes in translucent turquoise with darker bars (glow-free), four legs, two eyes | Pairs glide across the river from tree to tree on a spline, 1–2 lazy membrane flutters per crossing, land on a trunk and fold. Startle and launch when Pista passes close (S3) | Low: 6–8 bones (body, tail ×2, membranes ×2, head), 3 clips (glide loop, flutter, land/fold) authored by hand in Blender |

Originality check: whirlseeds are matte, spin, and do not float upward (not glowing jellyfish seeds); sailbacks are
four-legged rib-membrane gliders based on real flying lizards (not a spinning fan creature, no glow, two eyes).

## 6. Asset list with sources

All Poly Haven and ambientCG items are **CC0** (public domain; no attribution required). Names below were verified
against the Poly Haven API (`https://api.polyhaven.com/assets`) and the ambientCG API on 2026-10-08. Poly Haven scan
models are very dense (polycounts below are the source mesh), so each one is decimated and normal-baked to the target
budget in Blender before import. Every imported or generated item gets a row in `docs/LICENSES.md` (CLAUDE.md rule 7).

### 6.1 CC0 models (Poly Haven)
| ID | Use | Poly Haven asset | Source tris | Target LOD0 |
|---|---|---|---|---|
| M-01 | Fern clumps (4 variants in the file) | `fern_02` | 6.2k | 1.5k each clump |
| M-02 | Glossy broad-leaf understory | `anthurium_botany_01` | 4.1k | 1.5k |
| M-03 | Round broad-leaf understory | `calathea_orbifolia_01` | 16.7k | 2k |
| M-04 | Braided-trunk tropical shrubs (mid-ground) | `pachira_aquatica_01` | 76.9k | 3k |
| M-05 | Low leafy groundcover | `shrub_03` | 17.0k | 1k |
| M-06 | Moss cushions | `moss_01` | 246k | 0.5k (baked to shell mesh) |
| M-07 | Mossy rocks, river and path edge (set) | `rock_moss_set_01`, `rock_moss_set_02` | 63k / 58k (sets of several stones) | 0.8–1.5k per stone |
| M-08 | Boulder (ford stepping stones, fork split) | `boulder_01` | 124k | 1.5k |
| M-09 | Cliff faces around the falls basin | `rock_face_01`, `rock_face_02` | 20k / 30k | 4k each |
| M-10 | Basin back wall / large cliff | `mountainside` | 288k | 6k |
| M-11 | Exposed root tangles at path edges, S5 tunnel | `root_cluster_01`, `root_cluster_02` | 225k / 340k | 3k each |
| M-12 | Crossing root (jump obstacle, readability check) | `single_root` | 114k | 1.5k |
| M-13 | Fallen log (slide obstacle, readability check) | `dead_tree_trunk_02` | 156k | 2k |
| M-14 | Mid-forest canopy impostor source (optional) | `island_tree_02` | 1.76M | Impostor only (octahedral, 8–12 views) |

### 6.2 CC0 textures (Poly Haven and ambientCG), 1k or 2k downloads
| ID | Use | Source asset |
|---|---|---|
| T-01 | Path soil | Poly Haven `dirt_floor` (collection Verdant Trail); alt `forest_ground_04` |
| T-02 | Mossy ground | ambientCG `Ground037` (damp mossy forest ground); Poly Haven `mossy_rock` for moss on rocks |
| T-03 | Forest litter | Poly Haven `forest_leaves_04`; ambientCG `ScatteredLeaves009` |
| T-04 | Riverbed pebbles | Poly Haven `river_small_rocks`; alt `ganges_river_pebbles` |
| T-05 | River bank mud (wet) | Poly Haven `brown_mud_leaves_01` |
| T-06 | Rootstone base rock (tiling, triplanar) | Poly Haven `rock_face_03` (Verdant Trail); alt `lichen_rock`, ambientCG `Rock030` |
| T-07 | Stiltwood bark | Poly Haven `bark_brown_02`; alt ambientCG `Bark012` |
| T-08 | Moss detail | ambientCG `Moss002` |
| T-09 | Leaf atlases for canopy and understory cards | ambientCG `LeafSet009`, `LeafSet010` |
| T-10 | Grass/weed card atlases (path edge tufts) | ambientCG `Foliage001`, `Foliage005` |

### 6.3 CC0 HDRIs (Poly Haven)
| ID | Use | Asset |
|---|---|---|
| S-01 | Visible sky dome (graded, cumulus) | `kloofendal_48d_partly_cloudy_puresky`; alt `kloppenheim_05_puresky` |
| S-02 | Reference for forest ambient color and reflection-probe tests (not shown) | `rainforest_trail` (midday rainforest, dappled sun) |
| S-03 | Reference for waterfall-side lighting | `lauter_waterfall` |

### 6.4 AI-generated (Meshy image-to-3D from OpenAI concept images; prompts in `design/prompts/aurelia/`)
| ID | Item | Pipeline | Target LOD0 |
|---|---|---|---|
| A-01 | **Pista** (realistic) | OpenAI concept (P0-C, owner pick) → Meshy multi-image-to-3D → cleanup → Meshy rigging or manual humanoid rig → phone-video mocap | 25k, see 6.6 |
| A-02 | **Duko** (realistic) | OpenAI concept → Meshy multi-image-to-3D (wings-spread rig pose) → manual bird rig in Blender | 10k |
| A-03 | Stiltwood trunk base with stilt roots, 2 variants | OpenAI concept → Meshy → retexture with T-07 tiling bark + moss mask | 8k each (canopy is cards) |
| A-04 | Rootstone kit: arch with waterfall mouth, 2 pillars, 3 outcrops | OpenAI concept → Meshy → triplanar T-06 + braided-strand detail normal | Arch 10k, pillar 4k, outcrop 2k |
| A-05 | Bellcap clump, 2 variants | OpenAI concept → Meshy | 2k each |
| A-06 | Sailback | OpenAI concept → Meshy → manual 8-bone rig | 1.5k |
| A-07 | Whirlseed | OpenAI concept → Meshy (or hand-modeled plane + texture) | 0.15k |
| A-08 | Thorn bramble hazard (crimson tips), readability check only | OpenAI concept → Meshy | 2k |

### 6.5 AI-generated 2D (OpenAI images) and in-house
| ID | Item | Source |
|---|---|---|
| B-01 | Rootstone range matte cards (far, mid-far; 2–3 layers, transparent background) | OpenAI images, prompt in `design/prompts/aurelia/environment.md` |
| B-02 | Far forest wall card (canopy silhouette, transparent background) | OpenAI images |
| B-03 | Veilmoss curtain alpha cards (atlas) | OpenAI images on transparent background, cleaned by hand |
| B-04 | Ribbon reed and duskbell card atlas | OpenAI images on transparent background |
| B-05 | River mesh and shader, waterfall flow mesh, foam/flow maps, water normals | In-house (Blender procedural bakes + Shader Graph). No third-party license |
| B-06 | Mist, spray, splash flipbooks, pollen motes, god-ray cards | In-house (Blender renders/bakes) |
| B-07 | Coin (readability check) | In-house simple mesh, coin gold + turquoise gem |

### 6.6 Budgets for the look test (A14/A15 target)

These are the art direction's **asks**. The current `docs/ARCHITECTURE.md` section 10 numbers are `[ASSUMED]` values
written for an A12/A13 floor and a stylized look. Tech-architect (P0-B) confirms or changes them and updates the
architecture doc; the look test measures them on device.

| Metric | ARCHITECTURE.md today | Look-test ask `[ASSUMED]` |
|---|---|---|
| Triangles on screen (worst view, F4 vista) | ≤ 150k | **≤ 300k** |
| Draw calls (batches, SRP Batcher + instancing) | ≤ 120 | **≤ 150** |
| Hero | 15k tris, 1 material, 1024², ≤ 40 bones | **25k tris, 2 materials (body+cloth, hair), 2048² body set + 1024² hair, ≤ 60 bones** (humanoid + hair/satchel/sash secondary) |
| Companion | 8k tris, 1 material, 512² | **10k tris, 1 material, 1024²**, ≤ 40 bones |
| Prop LOD0 | ≤ 2k | ≤ 2k small props; large hero props per table 6.4 |
| Environment per 50 m section (visible) | ≤ 20k tris, ≤ 4 materials, atlas ≤ 2048² | **≤ 60k tris, ≤ 10 materials** (shared across sections), atlases/tiling sets ≤ 2048² |
| Texture memory, whole look-test scene | not set | **≤ 300 MB** (ASTC: 4x4 hero/UI, 6x6 near environment, 8x8 sky/backdrop) |
| Alpha-tested foliage overdraw | not set | ≤ 3× average over the screen in F1 (measure with the GPU frame debugger / Xcode GPU capture) |

**Worst-view triangle estimate (F4 vista):** Pista 25k + Duko 10k + terrain/path 30k + rocks/cliffs/rootstone 55k +
understory 60k (instanced) + stiltwoods near/mid 40k + impostors/canopy cards 20k + water/falls/particles 12k +
landmark and backdrop 8k + creatures/coins/cards 10k ≈ **270k**.

**Texture set plan (main items):** ground 4 × (albedo, normal, mask) 1024²; rootstone triplanar 2048² + detail
normal 1024²; bark 1024²; foliage atlases 2 × 2048²; veilmoss/reeds atlas 2048²; water normals 512², flow/foam
1024²; sky dome 4096×2048 (ASTC 8x8); backdrop cards 2048²; Pista 2048² + 1024²; Duko 1024².

## 7. Rendering asks to tech-architect (P0-B)
Art needs, for tech-architect to decide and record in an ADR:
1. HDR on, with subtle bloom (water sparkle, sun-lit edges) and Neutral or ACES tonemapping + a color-grading LUT
   (`docs/ARCHITECTURE.md` and the old guide had HDR/bloom off).
2. One realtime directional shadow for characters and nearby dynamic props; static shadows baked.
3. Baked lightmaps + light probes per section, a baked reflection probe per section.
4. Custom fog (distance + height, sun-direction tint).
5. Depth texture for water depth tint and soft particles.
6. Character-only fill and rim light (rendering layers).
7. Both orientations in the look-test scene, switchable at runtime, so the owner can compare (ART_DIRECTION 9).

## 8. Done when
- The four judge frames and the 60-second run pass the style-lock checklist (ART_DIRECTION 10.3) in an art review.
- iPhone 12/13: 60 fps sustained over a 10-minute loop, budgets in 6.6 met. iPhone 11: measured and reported.
- Every asset in section 6 has a `docs/LICENSES.md` row (tool, plan, license, commercial use).
