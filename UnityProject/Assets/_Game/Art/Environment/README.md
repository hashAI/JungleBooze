# Environment art: F4_e waterfall basin (look test v2)

Target: `design/aurelia/keyframes/F4_e_openai_medium.jpg` (primary), F4/P1/F1 picks in `keyframes_sheet.jpg`.
Owner: asset-pipeline. Integration: tech-architect. Regenerate everything with the scripts listed at the end;
sources, prompts and previews live in `art_source/environment/` (intermediates in `work/` are git-ignored).

Units are metres, Z-up in Blender, exported FBX for Unity (Y-up, +Z forward, scale 1). Pivots sit at ground level
(z = 0) at the piece centre. Root tails and debris run about 0.4 m below z = 0: sink pieces into terrain, don't lift them.

## Rootstone/ (procedural braided stone + basin ledges)
One FBX per piece with `_LOD0/_LOD1/_LOD2` meshes (Unity builds the LODGroup). One material per piece.
Textures in `Rootstone/Textures/`: `_BaseColor` (sRGB), `_Normal` (tangent, OpenGL +Y, MikkTSpace on triangulated
meshes; import as Normal map, let Unity calculate tangents), `_ARM` (R = AO incl. ground contact, G = roughness,
B = metallic 0; linear).
**UV0** = unique bake. **UV1** = world box projection, 1 unit = 4 m: put a tiling detail normal on it (CC0
`Art/CC0/Textures/rock_face_03` normal, about 0.3 strength). The big pieces need it up close: unique texel density is
only about 20 px/m on the hero arch and about 35–60 px/m on the others.

| Piece | Size (m) W×D×H | Tris LOD0 / 1 / 2 | Texture | Use |
|---|---|---|---|---|
| `RS_HeroArch` | 75.8 × 36.4 × 51.6 | 9,999 / 4,500 / 1,500 | 2048 | F4_e hero arch. Empty `RS_HeroArch_WaterfallMouth` marks the cave mouth under the crown (waterfall emitter) |
| `RS_PillarA` | 22.0 × 21.4 × 33.9 | 3,999 / 1,799 / 600 | 1024 | Tall braided pillar, knotted top |
| `RS_PillarB` | 22.2 × 18.9 × 31.0 | 3,998 / 1,800 / 600 | 1024 | Leaning pillar with an overhanging hook |
| `RS_ArchSmall` | 32.8 × 16.1 × 20.1 | 4,000 / 1,799 / 600 | 1024 | Mid/far arch (F1 skyline) |
| `RS_Outcrop` | 14.7 × 8.4 × 4.5 | 2,500 / 1,100 / 400 | 1024 | Root ridge for path edges |
| `RS_PoolTerrace_A` | 11.7 × 6.6 × 2.3 | 2,499 / 1,100 / 400 | 1024 | Terraced pool rim: boulder dam, basin floor behind the rim at z ≈ 0.9–1.05 (rim tops 1.4–2.3), so put the water plane at about 1.2. The rim bulges toward Blender -Y; check the facing after import |
| `RS_PoolTerrace_B` | 7.9 × 5.2 × 1.5 | 1,799 / 800 / 299 | 1024 | Smaller terrace (basin floor z ≈ 0.5–0.62, water plane at about 0.75) |
| `RS_LedgeLookout` | 5.3 × 4.3 × 1.8 | 2,500 / 1,099 / 399 | 1024 | Foreground mossy ledge Pista stands on (F4_e) |

Budgets (ENVIRONMENT_STRATEGY 7.1, ADR 0004): hero arch LOD0 ≤ 10k, 2k texture (task brief); pieces ≤ 4k with 1k
textures; ledges ≤ 2.5k. The full set at LOD0 is 31.3k tris, 8 draws, inside the L2 line (90k / 50 draws).
Suggested LOD screen heights: 0.35 / 0.12 / 0.03 (cull below 0.01; the hero arch never culls in the basin).
Import: ASTC 6×6 BaseColor, 5×5 Normal, 8×8 ARM; mip maps on; mesh compression Medium; Read/Write off.
Vertex colours: none exported (ADR 0007: painted at merge time).

## Plants/ (foreground framing cards, alpha-cut, double-sided)
Textures in `Plants/Textures/`: `_BaseColor` RGBA (straight alpha, colour bled into transparent texels) and
`_Normal` (derived in-house from the albedo). Use alpha clip about 0.5, cull off, flip back-face normals.
Vertex colour (ADR 0007): R = wind weight (0 at the base, 1 at the tips), G = 0, B = 0, A = ambient occlusion.
Normals are bent toward a dome over the clump, so lighting stays soft. Keep the custom normals on import.

| Plant | Size (m) W×D×H | Tris LOD0 / 1 / 2 | Atlas (1024²) |
|---|---|---|---|
| `FP_Broadleaf_Clump` | 3.2 × 3.0 × 1.4 | 660 / 256 / 80 | `FP_Broadleaf` (4 broad leaves) |
| `FP_Bellcap_Clump` | 1.6 × 1.7 × 1.5 | 256 / 60 / 20 | `FP_Bellcap` (orange bell stalks + strap leaves); risky-route orange `#F08A2C` |
| `FP_Fern_Clump` | 2.4 × 2.1 × 0.7 | 260 / 54 / 24 | `FP_Fronds` (2 fern fronds + 1 palm frond, the palm frond is unused for now) |

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

## Not delivered (yet)
- Stiltwoods: Meshy meshes exist (`art_source/environment/meshy/models/stilt_A/B/C.glb`), not cleaned/baked
  (the coordinator paused them on 2026-10-09).
- Palm frond clump: built and dropped (below the bar). Waterfall sheets, mist flipbooks and water are tech-architect's
  (shader work).

## Regenerate
`tools/blender/environment/run.sh <script> [args]` (Blender 4.2 headless):
`rootstone_high.py <piece>` → `ledges_high.py <piece>` → `rootstone_bake.py <piece>` → `preview_render.py <work blend> <prefix>`;
`plants_build.py [name]`. Images: `tools/assetgen/openai_images.py`, cut-outs with `tools/art/chroma_unmix.py`,
normals with `tools/art/normal_from_albedo.py`, mist with `tools/art/mist_band.py`. Meshy: `tools/assetgen/meshy_env.py`.
