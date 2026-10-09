# Environment art: F4_e waterfall basin (look test v2, hero basin iteration 2)

Target: `design/aurelia/keyframes/F4_e_openai_medium.jpg` (primary), F4/P1/F1 picks in `keyframes_sheet.jpg`.
Owner: asset-pipeline. Integration: tech-architect. Regenerate everything with the scripts listed at the end;
sources, prompts and previews live in `art_source/environment/` (intermediates in `work/` are git-ignored).

Units are metres, exported FBX for Unity (Y-up, scale 1). Blender -Y = Unity local +Z, Blender +X = Unity local -X.
Pivots sit at ground level (z = 0) at the piece centre. Root tails and debris run about 0.4 m below z = 0: sink
pieces into terrain, don't lift them.

**Facing (iteration 2 pieces):** the hero arch's best side (more texels, vines facing it) is Unity local **-Z**
(Blender +Y), which is what `EnvironmentKit.Span` shows the basin camera when foot A is on the camera's left (current
`HeroBasinConfig`). The travertine tiers, the lookout ledge's drop and the canopy wall face Unity local **+Z**: turn
+Z toward the camera.

## Rootstone/ (procedural braided stone, basin ledges, travertine tiers)
One FBX per piece with `_LOD0/_LOD1/_LOD2` meshes (Unity builds the LODGroup). Textures in `Rootstone/Textures/`:
`_BaseColor` (sRGB), `_Normal` (tangent, OpenGL +Y, MikkTSpace on triangulated meshes; import as Normal map, let Unity
calculate tangents), `_ARM` (R = AO incl. ground contact, G = roughness, B = metallic 0; linear).
**UV0** = unique bake. **UV1** = world box projection, 1 unit = 4 m: put a tiling detail normal on it (CC0
`Art/CC0/Textures/rock_face_03` normal, about 0.3 strength). The big pieces need it up close.

| Piece | Size (m) W×D×H | Tris LOD0 / 1 / 2 | Texture | Use |
|---|---|---|---|---|
| `RS_HeroArch` (v2) | 79.0 × 37.3 × 52.3 | 14,000 / 6,299 / 2,099 | **2 × 2048** | F4_e hero arch: three ropes of 4-5 separate strands, gaps and holes, splayed root legs. **Two meshes per LOD, one material each:** `RS_HeroArch_A_LODn` (Blender x < -1 = Unity local +X half) uses `RS_HeroArch_A_*`, `RS_HeroArch_B_LODn` uses `RS_HeroArch_B_*`. Empty `RS_HeroArch_WaterfallMouth` (-1.0, -0.6, 32.9 Blender) sits just under the crown's underside |
| `RS_PillarA` | 22.0 × 21.4 × 33.9 | 3,999 / 1,799 / 600 | 1024 | Tall braided pillar, knotted top |
| `RS_PillarB` | 22.2 × 18.9 × 31.0 | 3,998 / 1,800 / 600 | 1024 | Leaning pillar with an overhanging hook |
| `RS_ArchSmall` | 32.8 × 16.1 × 20.1 | 4,000 / 1,799 / 600 | 1024 | Mid/far arch (F1 skyline) |
| `RS_Outcrop` | 14.7 × 8.4 × 4.5 | 2,500 / 1,100 / 400 | 1024 | Root ridge for path edges |
| `RS_TravertineTiers` (new) | 29.4 × 24.5 × 7.8 | 5,000 / 2,199 / 800 | 1024 | Replaces the round saucer pools: four irregular limestone tiers stepping down toward Unity +Z, lobed curved lips, wet dark drop faces, mossy rim crests and rock blocks, 6 spill notches. Anchors below |
| `RS_TravertineTiers_Water` (new) | 30 × 21 | 6,318 (one LOD) | none (Water.shader) | One flat water sheet per tier, colours as the hero basin water: R = foam (shallow edges, rapids before each notch, impact at each foot), G = depth / 3 m, B = tier / 4, A = smoothstep(-0.4, 0.15, depth). UV0 = metres |
| `RS_PoolTerrace_A` / `_B` | 11.7 × 6.6 × 2.3 / 7.9 × 5.2 × 1.5 | 2,499 / 1,100 / 400; 1,799 / 800 / 299 | 1024 | v1 boulder-dam rims (kept for the look test; the basin should use the tiers) |
| `RS_LedgeLookout` (v2) | 7.6 × 7.6 × 2.0 | 2,499 / 1,100 / 400 | 1024 | Rocky, mossy lookout for Pista: slab top at about 1.36 m, strata, stepped slabs falling toward Unity +Z, faceted boulders, loose chips, patchy moss. Empty `RS_LedgeLookout_Stand` = Pista's standing point (0, 0, 1.36) |

**Travertine anchors** (empties; `EnvironmentKit` keys them by the text after the last underscore):
`Pool<t>` (t = 0 top/back .. 3 front; z = that tier's water level: 6.33 / 4.48 / 2.88 / 1.38; put the basin water at
about 0.3), `Lip<i>P00..P13` (lip i's edge polyline, left to right, at crest height), `Cascade<k>L` / `R` (ends of
cascade k's spill notch on the lip edge, at the upper water level) and `Cascade<k>F` (its foot on the next water
level down). Six cascades, 1.1-1.85 m drops, 2.2-4.6 m wide: two on the front lip, two on lip 1, one each on lips 0
and 2 (a central staircase plus two side spills). Everywhere else the rims stand about 0.23 m above the water.

Texel density (unique UV0, weighted to what the camera sees): hero arch about **25 px/m** on camera-facing faces
(back faces 0.55×, tops 0.7×, deep crevices down to 0.4×). The ~40 px/m asked for is not reachable on this shape with
2 × 2048: separate strands have about 3× the surface of the v1 blob. At the hero shot the arch covers about 19
screen px per metre (arch scaled ~4.9×), so 25 px/m is just over 1:1; the UV1 detail normal carries close-ups. Tiers
about 23 px/m (underwater floors 0.4×, upstream faces 0.6×), ledge about 72 px/m.

Budgets (ENVIRONMENT_STRATEGY 7.1, ADR 0004): hero arch LOD0 ≤ 14k with 2 × 2k (approved for iteration 2, +1 draw);
pieces ≤ 4k with 1k textures; ledges ≤ 2.5k. **Over the line:** `RS_TravertineTiers` is 5,000 LOD0 (a whole tier
complex, 30 × 25 m; it replaces several mounds and rims). Full rock set at LOD0 (arch, 2 pillars, small arch,
outcrop, tiers, ledge, v1 rims) is 40.3k tris in 10 draws (+1 water sheet), inside the L2 line (90k / 50 draws).
Suggested LOD screen heights: 0.35 / 0.12 / 0.03 (cull below 0.01; the hero arch never culls in the basin).
Import: ASTC 6×6 BaseColor, 5×5 Normal, 8×8 ARM; mip maps on; mesh compression Medium; Read/Write off.
Vertex colours: none on rock (ADR 0007: painted at merge time); the water sheet carries its own (above).

**Integration notes for tech-architect (iteration 2):**
- `EnvironmentKit` loads one texture set per piece by file name, so `RS_HeroArch` now finds no `RS_HeroArch_*` set
  and falls back to the kit/stand-in material until parts are matched to `RS_HeroArch_A_*` / `_B_*` by mesh-name
  prefix. The old single-set `RS_HeroArch_*` textures were deleted (they don't match the v2 UVs).
- `RS_TravertineTiers`, `RS_TravertineTiers_Water`, `FP_ArchVines` and `FP_CanopyCrown_*` deliberately match no kit
  role yet (`RoleOf` returns Unknown), so nothing picks them up until they're wired.
- `FP_ArchVines` is in the arch's local space: append it with the arch's own placement matrix.

## Plants/ (alpha-cut, double-sided cards)
Textures in `Plants/Textures/`: `_BaseColor` RGBA (straight alpha, colour bled into transparent texels) and
`_Normal` (derived in-house from the albedo). Use alpha clip about 0.5, cull off, flip back-face normals.
Vertex colour (ADR 0007): R = wind weight (0 at the base or hanging point, 1 at the tips), G = 0, B = 0,
A = ambient occlusion. Normals are bent toward a dome over the clump, so lighting stays soft. Keep custom normals.

| Plant | Size (m) W×D×H | Tris LOD0 / 1 / 2 | Atlas (1024²) | Use |
|---|---|---|---|---|
| `FP_FrameLeft_Clump` (new) | 4.1 × 4.5 × 2.1 | 956 / 236 / 80 | slot 0 `FP_ElephantEar`, slot 1 `FP_Bellflower` | Near-left framing: big dark elephant-ear leaves, paddle leaves, 3 orange bell stems. **Two material slots** (submesh 0 / 1) |
| `FP_FrameRight_Clump` (new) | 4.1 × 4.4 × 2.6 | 776 / 212 / 72 | same two slots | Near-right framing: bell stems up front (keyframe right edge), paddle leaves, elephant ears |
| `FP_ArchVines` (new) | in RS_HeroArch space | 4,432 / 1,304 / 566 | `FP_ArchVines` | 480 hanging vine and moss curtains (3-15 m under the crown and inner faces, 2-5 m drapes on strand sides), cards facing the camera half (Unity -Z ± 35°), a quarter crossed at LOD0. Keeps the fall's mouth clear |
| `FP_Canopy_Clump` (new) | 37.0 × 12.8 × 11.2 | 192 / 48 / 8 | `FP_Canopy` | Midground canopy wall segment: 8 crown cards in two depth rows (mirrored variants), faces Unity +Z. Replaces the repeating tree crowns on the basin walls |
| `FP_CanopyCrown_A/B/C/D` (new) | 8-11 wide, 6.6-8.2 high | 24 / 6 / 2 each | `FP_Canopy` | Single crowns (main card + two crossed at ±68° behind it), same facing, for custom placement and arch tops |
| `FP_Broadleaf_Clump` | 3.2 × 3.0 × 1.4 | 660 / 256 / 80 | `FP_Broadleaf` | v1 |
| `FP_Bellcap_Clump` | 1.6 × 1.7 × 1.5 | 256 / 60 / 20 | `FP_Bellcap` | v1; risky-route orange `#F08A2C` |
| `FP_Fern_Clump` | 2.4 × 2.1 × 0.7 | 260 / 54 / 24 | `FP_Fronds` | v1 |

Import: ASTC 6×6 BaseColor (alpha), 5×5 Normal, mip maps preserve coverage.

## Backdrops/ (painted cards for the F4_e view, back to front)
| File | Size | Alpha | Layer |
|---|---|---|---|
| `BD_F4e_Sky.png` | 1536×1024 | no | Sky band: sun glow upper left, cumulus (can replace or overlay the HDRI horizon) |
| `BD_F4e_FarRange.png` | 1536×684 | yes | Far stone pillar range, mist at the base |
| `BD_F4e_FarFalls.png` | 698×1428 | yes | Tall distant waterfall cliff, seen through the arch (bottom fades out) |
| `BD_MistBand.png` | 1024×256 | yes | Procedural mist band, tiles in X: place between layers |
| `BD_F4e_MidPillars.png` | 1536×959 | yes | Mid-distance pillars with jungle and falls |
| `BD_F4e_JungleWall.png` | 1536×626 | yes | Mid-far canopy wall; hides the bases of everything behind it |
| `BD_F4e_Plate.png` | 1536×1024 | no | Fallback: the whole far valley on one card (if layering costs too much) |

Composite test: `art_source/environment/previews/BD_F4e_stack_test.jpg`. Straight alpha, colour bled into
transparent texels. Import as sprite-free textures: ASTC 8×8 (opaque) / 6×6 (alpha), clamp, no mips needed beyond
2 levels. The painted haze is light, so the scene fog should add the rest per layer. Source resolution is the
generator's native 1536 px: don't upscale.

## Previews (iteration 2), `art_source/environment/previews/`
`RS_HeroArch_v2_hero_compare.jpg` (keyframe crop | arch + vines from the hero side), `RS_HeroArch_views.jpg`,
`RS_TravertineTiers_compare.jpg` (keyframe | downstream view with water sheets | top view: anchors yellow = lips,
red = cascades, cyan = pools), `RS_LedgeLookout_v2_compare.jpg`, `RS_LedgeLookout_views.jpg`,
`FP_FrameLeft_Clump_views.jpg`, `FP_FrameRight_Clump_views.jpg`, `FP_Canopy_Clump_compare.jpg`, `FP_v2_atlases.jpg`.
The Blender previews use neutral light and a flat preview water material; the real look is the Unity build.

## Painterly variants (F4_f trial, option D), `_P`
Target: `design/aurelia/keyframes/F4_f_painterly_openai.jpg`; rules: `design/aurelia/ART_DIRECTION_PAINTERLY.md`. The
realistic pieces above are untouched. Every painterly asset sits next to its realistic twin with `_P` in the name
(clumps: `_P_Clump`, so the role rules still match), so the scene switches by name. Same units, axes, pivots, facing,
LOD naming, texture channels (BaseColor sRGB, Normal OpenGL, ARM = AO/rough/0) and card vertex colours as above.
**Integration:** `EnvironmentKit` searches the kit folders recursively and matches by prefix, so it now sees both
`RS_HeroArch` and `RS_HeroArch_P` (etc.) as candidates for one role: the builder needs a style switch that keeps only
`_P` names (painterly) or drops them (realistic). Mesh names are unique (`..._P_LODn`), so the mesh cache does not collide.

| Asset | Size (m) W×D×H | Tris LOD0 / 1 / 2 | Texture | Notes |
|---|---|---|---|---|
| `RS_HeroArch_P` | 79.8 × 45.7 × 49.7 | 14,999 / 6,300 / 2,099 | 2 × 2048 (`RS_HeroArch_P_A_*`, `_B_*`) | v2 (`painterly_arch_strands.py`): 8 clean, round, evenly spaced strands (ropes 3/3/2, constant radius, soft ~7 m plate bulges, more twist), built directly as the game mesh (no voxel merge, buried faces culled), paint baked onto itself. Same spine/footprint/root tails; halves split at Blender x = -1. **`RS_HeroArch_P_WaterfallMouth` moved to (-1.0, -0.6, 31.6) Blender** (crown underside 1.7 m lower than v1). ~25 px/m |
| `RS_PillarA_P` | 13.4 × 10.7 × 35.1 | 4,000 / 1,800 / 600 | 1024 | Stacked-slab generator (`painterly_slabs.py`): 4 columns of tall rounded blocks, bushy crown spilling over the rim. ~21 px/m |
| `RS_PillarB_P` | 17.2 × 11.1 × 31.7 | 3,860 / 1,800 / 600 | 1024 | Leaning tower, top blocks step out into an overhanging hook (+X), two buttress columns |
| `RS_ArchSmall_P` | 34.0 × 10.9 × 23.4 | 3,905 / 1,800 / 599 | 1024 | Two stacked-block legs, span of two bent strata bands, bushes on top |
| `RS_Outcrop_P` | 15.9 × 7.8 × 5.5 | 1,294 / 1,100 / 399 | 1024 | Overlapping soft lozenges with mossy domes (pool rocks / path edges). ~55 px/m |
| `RS_TravertineTiers_P` | 29.6 × 24.5 × 7.8 | 5,000 / 2,200 / 800 | 1024 | Smoothed only (no remesh), so water levels and all anchors match: use the realistic `RS_TravertineTiers_Water` with it. Anchors renamed `RS_TravertineTiers_P_*` (same suffixes) |
| `RS_LedgeLookout_P` | 7.6 × 7.6 × 2.0 | 2,500 / 1,100 / 400 | 1024 | `RS_LedgeLookout_P_Stand` copied from v2 (the top is within a few cm). ~96 px/m |
| `FP_FrameLeft_P_Clump` / `FP_FrameRight_P_Clump` | as realistic | 956 / 236 / 80; 776 / 212 / 72 | slot 0 `FP_BigLeaf_P`, slot 1 `FP_Bellflower_P` | Same layout as the realistic frames, glossy painted leaves (dark core, light tips) |
| `FP_Bellflower_P_Clump` (new) | 1.7 × 1.7 × 1.6 | 200 / 84 / 32 | `FP_Bellflower_P` | Bell stems + paddle leaves, for scattering (bellcap orange stays the risky-route cue) |
| `FP_PalmFern_P_Clump` (new) | 3.9 × 4.1 × 2.0 | 476 / 204 / 80 | `FP_Fronds_P` | Palm fronds over a fern ring |
| `FP_Fern_P_Clump` | 2.3 × 2.3 × 0.7 | 260 / 96 / 40 | `FP_Fronds_P` | Low fern clump |
| `FP_Canopy_P_Clump`, `FP_CanopyCrown_P_A..D` | as realistic | 192 / 48 / 8; 24 / 6 / 2 | `FP_Canopy_P` | Painted crown blobs, same layout/facing |
| `FP_ArchVines_P` | in `RS_HeroArch_P` space | 5,212 / 1,690 / 824 | slot 0 `FP_ArchVines_P`, slot 1 `FP_Canopy_P` | 600 leafy curtains/drapes + 246 bigger leafy crests on the crown tops and the crown face toward the camera (v1: 140). Arch + vines LOD0 20,211 tris (v1 19,256, +5 %) |

Backdrops (straight alpha, same import rules as the F4_e set): `BD_F4f_Sky` 1536×1024 (no alpha), `BD_F4f_FarRange`
1536×813, `BD_F4f_FarFalls` 795×1453, `BD_F4f_MidPillars` 1536×973, `BD_F4f_JungleWall` 1536×689, `BD_F4f_Plate`
1536×1024 (no alpha, fallback). `BD_MistBand` is shared. Composite: `previews/BD_F4f_stack_test.jpg`. Haze is light
and warm in the paint; AD s7 wants warm golden fog per layer on top.

Paint: big warm-to-cool gradient (warm cream-tan top and up-facing planes, cooler grey-mauve low and down-facing),
broad tonal patches, painted warm-brown AO in seams, light edge highlights on convex top edges, broad painted moss on
up-facing faces (OpenAI painted moss swatch), OpenAI painted rock swatch as a ~10 % brush-stroke overlay; no cracks
inside strands (AD s2); normals carry broad forms only; roughness flat (stone 0.85, moss 0.9, wet travertine ~0.35).
Albedo is a little less saturated than the AD s8 hexes (those are picked from the lit image; as albedo under the warm
sun they rendered mustard). No UV1 detail normal is needed (AD s3: detail normals off).
Previews: `RS_HeroArch_P_hero_compare.jpg` (F4_f crop | arch + vines), `RS_*_P_views.jpg`,
`RS_TravertineTiers_P_view.jpg`, `FP_*_P_Clump_views.jpg`, `FP_P_atlases.jpg`, `BD_F4f_stack_test.jpg`. Blender
previews use neutral light and Principled shading; the painterly look depends on the AD s4 shader (wrapped diffuse,
teal shadows, warm terminator), which is tech-architect's.

## Not delivered (yet)
- Slab pieces: tops are lumpy painted bush shells, not leaf cards; from near they read as mossy mounds. For F4_f's
  tree crowns on the towers, place `FP_CanopyCrown_P_*` on the pillar tops in the scene. Texel density is low for a
  near view (~20 px/m at 1024): they are mid/far pieces.
- Hero arch: strands are now round, even and cleanly seamed, crown leafier; F4_f still shows more (thinner) strands
  and more crossings and a fully green crown face. AD s2 caps strands at 5–8; judge in Unity with the AD s4 shader.
- Stiltwoods: Meshy meshes exist (`art_source/environment/meshy/models/stilt_A/B/C.glb`), not cleaned/baked
  (the coordinator paused them on 2026-10-09).
- Palm frond clump: built and dropped (below the bar). Waterfall sheets, mist and water shading are tech-architect's.
- Travertine tiers: each tier's drop is a fairly even wall band; the keyframe's drops are more broken into boulders.
  Worth one more pass once it's seen in engine with the cascade sheets.

## Regenerate
`tools/blender/environment/run.sh <script> [args]` (Blender 4.2 headless):
- Hero arch v2: `heroarch_v2_high.py [--quick]` → `heroarch_v2_bake.py`; vines need the arch first:
  `plants_v2_build.py vines`.
- Travertine: `travertine_high.py [--quick]` → `travertine_bake.py`; preview `travertine_view.py <blend> <out.jpg>`.
- Ledge v2: `ledge_v2_high.py` (builds and bakes).
- Other rock pieces: `rootstone_high.py <piece>` → `ledges_high.py <piece>` → `rootstone_bake.py <piece>`.
- Plants: `plants_v2_build.py [frames|canopy|vines]` (iteration 2), `plants_build.py [name]` (v1).
- Previews: `preview_render.py <game blend> <prefix>`, `hero_view.py <blend> <out.jpg> [cam xyz] [target xyz] [lens]
  [keyframe crop]` (env `HV_APPEND=<blend>` adds objects, e.g. the vines on the arch).
- Painterly (`_P`): images `tools/assetgen/openai_images.py edit` with F4_f as reference and prompts
  `art_source/environment/openai/prompts/p_*.txt` → `chroma_unmix.py` (atlases `--nocrop [--foliage]`) →
  `tools/art/painterly_finish.py [atlases|backdrops|swatches]` (kit textures, mist despill, tileable bake swatches) →
  `painterly_arch_high.py` (hero arch high) → `painterly_rock.py <piece|all>` (simplify, paint, bake, LODs, FBX) →
  `painterly_plants.py [frames|clumps|canopy|vines]` (vines need the painterly arch). Since 2026-10-09 the painterly
  arch is `painterly_arch_strands.py` and the pillars/small arch/outcrop are `painterly_slabs.py <piece|all>` (both
  build the game mesh directly and bake the paint onto it; `painterly_rock.py` still makes the travertine and ledge);
  compare sheet `previews/RS_Slabs_P_compare_F4f.jpg` → `tools/art/painterly_stack.py`
  (backdrop composite, atlas sheet). Previews: `HV_KEYFRAME=design/aurelia/keyframes/F4_f_painterly_openai.jpg
  hero_view.py ...`; `PV_SAMPLES=20` speeds up `preview_render.py` / `travertine_view.py` drafts.
- Images: `tools/assetgen/openai_images.py`, cut-outs with `tools/art/chroma_unmix.py` (`--nocrop`, `--foliage` for
  green/brown-only sheets), normals with `tools/art/normal_from_albedo.py`, mist with `tools/art/mist_band.py`. Run
  the `tools/art` scripts with Blender's Python 3.11 (it has numpy/scipy/Pillow). Meshy: `tools/assetgen/meshy_env.py`.
- Bake notes: bake targets are hidden from all ray types (`envlib.rays_off`). AO is baked as a colour bake of an
  Ambient Occlusion node (`bakekit.bake_ao`, also used by the hero arch), because the Cycles AO pass under
  selected-to-active left large open areas at 0. The v1 pieces (`rootstone_bake.py`) still use the AO pass: they
  were re-baked with `rays_off`, and their remaining dark AO is mostly buried root tails.
