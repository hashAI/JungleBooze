# ADR 0004: Realistic look on mobile (AURELIA Phase 0 look test)

- Status: Accepted for Phase 0 (look test). The minimum device stays provisional until the owner's on-device check (P0-E)
- Date: 2026-10-08
- Deciders: tech-architect (owner direction 2026-10-08: realistic look, AI-made art plus free CC0 assets, no paid packs,
  quality first, minimum device may rise to iPhone 12 or 13 if a polished realistic look needs it)
- Hard to undo: **partly**. The URP settings can be changed at any time. The minimum device, the orientation and the
  lighting model (no lightmaps) shape every asset made later, so they are hard to change once production starts
- Supersedes for the realistic look: the "HDR off, no realtime shadows, blob shadow" parts of ADR 0001. URP stays
  (ADR 0001). The First Playable pipeline asset `URP-Mobile` stays as it is, so the portrait Run scene keeps working

## Context

The owner chose a realistic look for AURELIA (design/DECISIONS.md, 2026-10-08), made with AI-generated assets and free
CC0 assets (Poly Haven, ambientCG). Phase 0 is a look test: about 200 m of forest, river and waterfall with Pista
running through it, playable in the editor and on an iPhone. The owner judges whether it looks professional and runs
smoothly. The owner also said quality comes first: the minimum device may move from iPhone 11 to iPhone 12 or 13,
and the URP settings should give full quality on the recommended minimum device instead of the lowest common
denominator. Target: 60 fps on that device.

The runner streams its world in chunks (blueprint Part VIII). Anything that depends on a fixed, pre-baked world
(lightmaps, baked occlusion, large static batches) works badly with chunks that are recombined in a new order every run.

## Decision 1: Minimum device. Recommendation: iPhone 12 (A14) or newer, provisional until measured

| | iPhone 11 (A13) | iPhone 12 / 12 mini (A14) | iPhone 13 / 13 mini (A15) |
|---|---|---|---|
| Released | 2019 | 2020 | 2021 |
| GPU | Apple GPU family 6, 4 cores | Family 7, 4 cores, about +10% over A13 | Family 8, 4 cores, about +15 to 20% over A14 |
| CPU (single thread) | baseline | about +20% | about +35% |
| Memory | 4 GB (SE 2nd gen: **3 GB**) | 4 GB | 4 GB (SE 3rd gen: 4 GB, A15) |
| Native screen | 1792 × 828 (1.48 MP), LCD | 2532 × 1170 (2.96 MP), OLED (mini 2340 × 1080) | 2532 × 1170, OLED |
| Internal render resolution (look test) | native, render scale 1.0 (1.48 MP) | about 1.7 MP, render scale ≈ 0.76 | about 1.7 MP, render scale ≈ 0.76 (room for 0.85) |
| Sustained performance (10 min) | weakest; throttles first | medium | best of the three |
| Expected with the full settings below | 60 fps likely only with less vegetation or 30 fps; **to be measured** | 60 fps target; **to be measured** | 60 fps with headroom (expected) |

Reasons for recommending iPhone 12 (A14) as the floor:

1. **Same picture on every supported phone.** The look is defined by one set of settings (below). The GPU work per
   frame is set mainly by the internal resolution, which we hold at about 1.7 MP on every device (render scale chosen
   at run time). A14 has about the same GPU as A13 but about 20% more CPU, which pays for culling, many more
   objects and the simulation, so the same settings hold 60 fps with more margin.
2. **Memory.** Realistic textures, many vegetation meshes and the shadow map need a resident budget of about
   1 GB (section "Budgets"). Every A14+ iPhone has 4 GB. The A13 family includes the iPhone SE 2nd gen with 3 GB,
   where 1 GB is too close to the system's limit for apps; supporting "iPhone 11 but not SE 2" is confusing on the store.
3. **Thermals.** The look test asks for 60 fps sustained over a 10-minute run. The A13 iPhones throttle earliest.
4. **Age.** At a 2027 launch the iPhone 11 is 7 to 8 years old.

If the iPhone 12 cannot hold 60 fps in the 10-minute test with the full settings, the recommendation moves to
**iPhone 13 (A15)**, not to lower settings (owner rule: no visual trade-downs). If an iPhone 11 is available, it is
measured too: if it holds 60 fps, the owner may keep it as the floor at no cost. 30 fps stays a fallback the blueprint
allows (Part XLVI), but it is not the plan.

Not decided here: the owner picks the floor after P0-E (see "Open questions" in the report). iOS minimum stays 15.0
(ADR 0003); a higher device floor is enforced with the App Store "required device capabilities" and a launch check,
designed when the floor is final.

## Decision 2: URP settings (`URP-Realistic`, created by JungleBooze > Look Test > Build Scene)

| Setting | Value | Why |
|---|---|---|
| Rendering path | **Forward** | One directional light; Forward+ only pays off with many lights and costs a light-list pass |
| HDR | **On**, 32-bit (R11G11B10) | Needed for tonemapping and bloom; 32-bit keeps the bandwidth of LDR RGBA8 on tile GPUs |
| Tonemapping | **ACES** (Neutral as the A/B alternative in the config) | Filmic highlight roll-off is most of what makes a photo-sourced scene read as "real" |
| Color grading | HDR grading mode, LUT 32, post exposure +0.25, contrast +12, saturation +8, white balance +4 | Cheap (one LUT lookup); all values in `LookTestConfig` |
| Bloom | On: threshold 1.1, intensity 0.35, scatter 0.6, high-quality filtering off | Sun glints on water, bright sky through leaves; low cost at reduced resolution |
| Vignette | 0.2 | Frames the runner; nearly free |
| Not used | SSAO, depth of field, motion blur, chromatic aberration, film grain, screen-space reflections, TAA/STP | See below |
| Anti-aliasing | **MSAA 4x** + **alpha-to-coverage** on foliage; no FXAA/SMAA/TAA | MSAA resolves in tile memory on Apple GPUs (cheap); alpha-to-coverage smooths leaf edges, which FXAA blurs and TAA ghosts at running speed |
| Render scale | Chosen at run time for about **1.7 MP** internal (0.6 to 1.0), upscaled by URP | Same GPU load on every device; on 460 ppi OLED screens 0.76 is not visible at arm's length. FSR 1 upscaling to be A/B tested on device |
| Main light | Per pixel, shadows on | The sun is the only realtime light |
| Shadows | **2048 map, 1 cascade, 45 m, soft shadows on** (URP default soft quality), depth/normal bias 1 | One cascade over 45 m gives about 2 cm texels near the runner, enough for contact under Pista; casters limited (below) |
| Additional lights | Off | Night or cave light comes from emissive and baked AO, not from extra lights |
| Depth texture / opaque texture | **Off** | No effect needs them (water has no refraction and no depth fade); saves a full-screen copy |
| SRP Batcher | On; GPU instancing allowed; dynamic batching off | Cheap draws for many unique materials |
| Static batching | Not used | Chunks move; static batches cannot |
| Skybox | HDRI cubemap (custom `JungleBooze/Sky HDRI` shader with exposure and rotation) | The same HDRI lights the scene, so sky and lighting always agree |
| Fog | Exponential squared, color matched to the HDRI horizon, density 0.011 | Atmospheric depth; hides the far end of the streamed world |

**Why no SSAO.** URP SSAO needs a depth-normals prepass (every opaque and alpha-tested object drawn a second time) plus a
blurred half-resolution pass: about 2 to 4 ms on A14 at our resolution, a third of the GPU budget. We get contact
darkening instead from (1) the ambient-occlusion channel of every CC0 texture set, (2) ambient occlusion baked into
vertex colors of generated meshes (trunk bases, boulder undersides, river banks) and of AI-made assets, (3) realtime
sun shadows. SSAO is re-evaluated only for a future "high" tier on A17 Pro and newer.

**Why no TAA/STP.** Temporal methods smear thin, fast-moving foliage at running speed and cost 1 to 2 ms; STP is
meant for compute-heavy desktop/console paths.

## Decision 3: Lighting for a streamed runner: realtime sun + baked sky ambient, no lightmaps

| Option | Verdict |
|---|---|
| Baked lightmaps per chunk | **No.** Lightmaps bake one arrangement of neighbours; chunks are recombined every run, so shadows and bounce light at seams would be wrong. Memory and download cost per chunk |
| Adaptive Probe Volumes (URP 17) | **No** on mobile: per-pixel probe sampling and large data |
| Light probes | **Not in Phase 0.** Later, optional: a few probes baked inside each chunk prefab for deep shade (caves, canopy tunnels) |
| **Realtime sun with shadows + ambient from the sky** | **Yes.** Lighting is independent of where a chunk lands. The sky HDRI is baked into the ambient light (spherical harmonics) and the default reflection probe with an environment-only bake (no lightmaps; `LookTestLighting.lighting` has baked and realtime GI off). Per biome later: switch the sky and blend the ambient probe at biome transitions |

Shadow casters: terrain and ground do not cast (they only receive). Tree trunks, canopy cards (dappled light is a big
part of the forest look), cliffs, rocks and Pista cast. Ferns and small plants do not cast (cost; they sit in shade).

## Decision 4: Vegetation

- **Alpha-tested (cutout), never alpha-blended**; two-sided; alpha-to-coverage with MSAA. Cutout costs on Apple GPUs
  (it disables hidden-surface removal for those pixels), so: cards are trimmed close to the leaves, cutout materials
  render after opaque geometry (queue 2450), and alpha-tested pixels are budgeted (below).
- **Leaf translucency**: a cheap back-light term in the shader so leaves glow when the sun is behind them.
- **Wind in the vertex shader** (`JungleBooze/Nature Lit`, `_WIND_ON`): sway weighted by height above the object's pivot
  (squared, so the base stays planted), phase from world position so neighbours differ, two sine octaves plus flutter.
  For AI-made final assets the weights move into **vertex colors**: R = sway weight (0 at the base to 1 at the tip),
  G = phase offset, B = leaf flutter. Phase 0 uses the height weight because the CC0 models have no wind colors.
- **Trees in Phase 0**: procedural trunks (bark from CC0, buttress roots, baked vertex AO, about 500 triangles) with
  leaf-card crowns (Poly Haven leaf texture). Poly Haven's tree models have 0.3 to 17 million triangles and are not
  usable on mobile. Final trees come from AI generation (Meshy) or are decimated and carded in Blender: open question.
- **Distance culling**: every scattered plant or rock has a one-level `LODGroup` that culls it below a screen-size
  fraction (plants 3.5%, rocks 1.5%). Real LOD chains: AI-made assets get LOD1/LOD2 at export; for CC0 scans, Unity 6's
  import-time Mesh LOD generation is to be evaluated on the owner's Mac (not used by the builder).

## Decision 5: Water and waterfall without refraction

One alpha-blended pass (`JungleBooze/Water`): two scrolling normal maps (a seamless normal map generated by the editor
from integer-frequency waves), Fresnel blend between a tinted body color lit by the sky ambient and the sky reflection
from the reflection probe, sun specular with shadows, foam from vertex color R (banks, plunge pool) broken up by the
normals, soft edges from vertex color A. No refraction, no opaque or depth texture, no planar reflection (each would
cost a full-screen copy or a second scene render). The waterfall is a curved sheet with the same shader and a fast
scroll along the fall, plus at most 28 soft mist particles (overdraw is the cost). Later candidates if the owner wants
more: flow maps, a cheap depth-free shoreline foam texture, splash flipbooks.

## Decision 6: Textures and compression

| Map | iOS format | Size in the look test |
|---|---|---|
| Albedo (sRGB) | ASTC 6x6 | 2048 for ground and cliff, 1024 for props and plants |
| Normal (OpenGL convention, Poly Haven `nor_gl`) | ASTC 5x5 (6x6 shows block noise on normals) | same as albedo |
| ARM (AO, roughness, metal; linear) | ASTC 8x8 | same as albedo |
| Alpha mask (linear) | ASTC 6x6, mip maps preserve coverage | 1024 |
| HDRI sky | Cubemap, RGB9E5 (32 bits per pixel, HDR, works on every Metal GPU) | 2k lat-long source → 512 faces |
| Hero character (later) | ASTC 4x4 albedo, 5x5 normal | 2048 |
| UI | ASTC 4x4 | as needed |

Rules are applied on first import by `LookTestAssetImportRules` for everything under `Assets/_Game/Art/CC0/`.
Mip maps on for all 3D textures; anisotropic filtering 2 on ground textures only.

## Decision 7: Budgets (replace the initial ones in docs/ARCHITECTURE.md section 10 for the realistic look)

Per frame on the recommended minimum device (A14) at 60 fps, measured in a release (non-development) build:

| Metric | Budget | Was (stylized plan) |
|---|---|---|
| Frame | 16.6 ms, 60 fps sustained over 10 min (p95 ≥ 55 fps) | same |
| CPU main thread | ≤ 8 ms | ≤ 10 ms |
| CPU render thread | ≤ 8 ms | — |
| GPU | ≤ 12 ms (leaves thermal headroom) | ≤ 12 ms |
| Draw calls, main view (SRP-batched) | ≤ 250 | ≤ 120 |
| Draw calls, shadow pass | ≤ 100 | — |
| SetPass calls | ≤ 40 | — |
| Triangles, main view | ≤ 350k | ≤ 150k |
| Triangles, shadow pass | ≤ 150k | — |
| Alpha-tested foliage | ≤ 35% of screen pixels on average | — |
| Transparent overdraw | ≤ 1.5 layers on average (water + mist) | — |
| Texture memory, one biome resident | ≤ 150 MB (look test: about 50 MB) | — |
| Texture memory, total resident | ≤ 300 MB | — |
| Resident memory | ≤ 1.0 GB (hard ceiling 1.2 GB) | ≤ 600 MB |
| App download | ≤ 200 MB first install (first biome), other biomes as on-demand content | same |
| GC allocations | 0 per frame in game code | same |

Per device (estimates until measured in P0-E):

| | iPhone 11 | iPhone 12 | iPhone 13 |
|---|---|---|---|
| Internal resolution | 1.48 MP (native) | 1.7 MP | 1.7 MP (2.0 MP if headroom) |
| GPU ms expected with full settings | 12 to 15 (over budget likely) | 10 to 12 | 8 to 10 |
| Triangles, main view | ≤ 300k | ≤ 350k | ≤ 450k |
| Draw calls, main view | ≤ 220 | ≤ 250 | ≤ 300 |
| Resident memory | ≤ 1.0 GB (≤ 700 MB on SE 2) | ≤ 1.0 GB | ≤ 1.2 GB |

## Decision 8: Orientation for the look test

**[ASSUMED] Landscape** (left and right) for the look test, as the blueprint recommends for wide traversal views. The
portrait Run scene keeps working: `JungleBooze > Look Test > Use Look Test iOS Build Settings` switches the build to
landscape with the look test first; `Restore Run Build Settings` switches back. The owner decides the final orientation.
The look-test camera uses a 47° vertical field of view (equal to about 85° horizontal on a 19.5:9 screen).

## Decision 9: CC0 sourcing and licenses

- Poly Haven is the primary source (CC0 1.0, no attribution required, API at https://api.polyhaven.com). The curated
  list (`tools/assets/cc0_assets.json`) was checked against the API on 2026-10-08; `tools/assets/fetch_cc0.py`
  downloads it (about 70 MB, MD5-checked) and appends each asset with source URL, authors and license to
  `docs/LICENSES.md`. ambientCG (also CC0) is the second source for textures Poly Haven lacks.
- Downloaded files are binaries in Git LFS. They count toward the LFS quota (about 70 MB for the look test).
- No paid asset packs (owner). No asset with a non-CC0 license enters the repository without an entry in
  `docs/LICENSES.md` and a tech-architect check.

## Measurement protocol for P0-E (owner on iPhone)

Release build (Development Build off), landscape, Low Power Mode off, phone at room temperature, brightness at half.
Run for 10 minutes. Read the overlay at 1, 5 and 10 minutes: FPS, CPU main, GPU, "slowest 1%", hitches, draws,
triangles, memory. Then use the right-hand buttons one at a time (Post, Shadows, MSAA, HDR, Plants, Water, 30/60 fps,
Scale) and note the GPU time change for each. Screenshots of the overlay are enough. Counters that show "n/a" in a
release build (draws, triangles) are read once in a Development build.

## Options considered

| Option | Pros | Cons |
|---|---|---|
| **Realistic URP, full settings on an A14 floor** (chosen) | Owner's quality bar; one look everywhere; simple to reason about | Excludes iPhone 11 / SE 2 unless measurements say otherwise |
| Realistic URP with quality tiers (lower settings on A13) | Wider device reach | Owner rule against trading visuals down; two looks to test and art-direct |
| Keep the iPhone 11 floor, 30 fps | Wider reach, more GPU per frame | Running at 30 fps feels worse in a fast runner; blueprint prefers 60 |
| HDRP | Highest fidelity | Not supported on mobile |
| Lightmapped chunks | Best static lighting quality | Wrong at chunk seams, large data, slow iteration |

## Consequences

- Positive: the look test shows the realistic target honestly at production settings; lighting works for streamed
  chunks; budgets now match a realistic scene and are measurable with the overlay.
- Negative: higher device floor (if accepted); custom shaders (`Nature Lit`, `Water`, `Sky HDRI`, `Mist`) must be
  maintained across URP upgrades; trees need an asset pipeline that CC0 cannot fill.
- Follow-ups:
  - P0-E: owner measures on iPhone; tech-architect and performance-engineer update this ADR with real numbers and the
    final floor.
  - Choose the final tree pipeline (open question).
  - Update `docs/ARCHITECTURE.md` section 10 budgets after P0-E (done provisionally now).
  - Wind vertex-color convention goes into the asset-pipeline export checklist.
  - Evaluate Unity 6 import-time Mesh LOD on CC0 scans; FSR 1 upscaling A/B on device.
