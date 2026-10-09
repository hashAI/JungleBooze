# ADR 0009: Painterly style trial behind a style switch

- Status: Accepted for the trial (owner decision 2026-10-09, art style Option D; adopted only if the owner approves
  the hero basin result)
- Date: 2026-10-09
- Deciders: tech-architect
- Hard to undo: **no.** Everything sits behind `HeroBasinStyle` and a local shader keyword. Removing it means deleting
  the painterly config, scene and folder, plus the `_PAINTERLY` blocks.

## Context
The owner is trying a painterly / stylized-realistic look (target `design/aurelia/keyframes/F4_f_painterly_openai.jpg`,
art direction `design/aurelia/ART_DIRECTION_PAINTERLY.md`) on the hero basin. The realistic iteration 3 (ADR 0008)
must stay buildable so the two can be compared and the trial rolled back. asset-pipeline delivers hand-painted
variants in the same folders as the realistic kit.

## Decisions
1. **Style switch:** the `HeroBasinStyle` enum (Realistic, Painterly) is a field of `HeroBasinConfigAsset`. Each style
   has its own config (`HeroBasinConfig[_Painterly].asset`), scene (`HeroBasin[_Painterly].unity`), look/volume/lighting
   assets, and material and mesh folders (`Art/HeroBasin/Painterly/...`), all from `HeroBasinBuilder.StylePaths`.
   Building one style never writes the other's files. Batch: `-jbHeroStyle painterly`; menu *Build Painterly Scene*.
2. **One shader set, one local keyword:** `_PAINTERLY` (`shader_feature_local_fragment`) in Nature Lit, Water,
   Waterfall and Backdrop Card. Realistic materials never enable it, so their variants and output are unchanged.
   Shared math is in `JBPainterly.hlsl`:
   - wrapped diffuse with a soft ramp
   - shadows shifted toward teal at the same brightness
   - a warm terminator band
   - painted occlusion tinted warm brown, never black
   - one soft Blinn lobe in place of GGX + environment BRDF

   Photo textures are made paint-like in the shader: albedo mip bias, hue flattening toward a high mip, broad normals
   only, detail normals off. Painted (`_P`) sets skip that processing and are shaded as authored.
3. **Painterly values** live in `HeroPainterlyLook` (sRGB colours from the art direction palette), nested in the
   config. `HeroBasinPainterly.Apply` writes them into the painterly scene's own materials after the world is emitted.
4. **Pista restyle by material only:** `Character Painterly` reads her existing URP/Lit textures and does the following:
   - blurs and weakens the normal map (smoother skin, softer hair, simpler cloth)
   - wraps the diffuse at 0.5
   - adds a warm subsurface terminator on a hue-based skin mask (a heuristic)
   - adds an always-on rim of 0.6

   Only her scene instance gets the material: no file under `Art/Characters` is touched. It replaces the realistic
   Rim Overlay slot, so she costs one draw instead of two.
5. **Painted asset contract** (`EnvironmentAssetRules.IsPaintedVariant` / `WithoutPaintedToken`, EditMode tests): a
   painted variant carries a `_P` token after the base name (`RS_HeroArch_P`, `FP_Fern_P_Clump`, `FP_Canopy_P_*`).
   - `EnvironmentKit.Scan(false)` (realistic, and the look test) ignores painted models and texture sets.
   - `Scan(true)` replaces a base model with its painted twin, adds painted-only models, and maps a painted texture
     set over its base name.
   - Plant atlases and backdrops (`BD_F4f_*` in the painterly config) follow the same rule.
6. **Grade:** keyframe-matched LUT `HeroBasin_F4f_Lut.png` (`tools/art/match_lut.py` against F4_f, strength 0.6, made
   from a LUT-free render), after a lighter hand grade with warm golden fog.
7. **Capture pose:** humanoid idle clips are evaluated through a PlayableGraph on her Animator.
   `AnimationClip.SampleAnimation` left her in the bind (A) pose in both styles.

## Cost (editor, Apple M4; the device run is still owed)
- Geometry is identical: landscape 42 draws / 307k tris, portrait 36 / 305k, shadow pass 7 / 129k. Pista is one
  draw fewer in painterly.
- Per pixel, Nature Lit painterly replaces URP PBR (GGX, environment reflection, GI BRDF) with a wrap + one pow, and
  adds one low-mip albedo sample. Backdrop adds one sample. Water and Waterfall are about the same.
- Estimate: equal or slightly cheaper GPU than realistic. Overdraw from falls and mist is unchanged, and that is the
  real risk (ADR 0008).

## Consequences
- Delete `HeroBasinConfig_Painterly.asset` to restart it from the realistic config (style set to Painterly, class
  defaults for the painterly block).
- Rebuild the F4_f LUT after any lighting change (contribution 0, render, match, contribution 1).
- Tests: `PaintedVariantsCarryAPToken`, `PaintedTokenIsRemovedForTheBaseName`.

## Addendum (2026-10-09): set-dressing pass and frame-budget measurement
- **Dressing** (`HeroBasinDressing`, painterly only, `HeroBasinConfigAsset.Dressing`): runs after the world is built
  and before batches are emitted. Temporary mesh colliders come from the merged batches. Every item is placed by a
  ray from the landscape camera through the screen points of `design/aurelia/HERO_BASIN_DRESSING.md`, with a fixed
  seed. Sizes are screen fractions turned into metres at the hit distance. Items join the existing merged batches, so
  draws stay flat (43 landscape) and the pass costs triangles only. In dressed mode the world also drops the procedural
  arch crowns, hero-tree crowns, forest stick trunks, canopy-mass blobs and the stemless hanging leaf.
- **Measurement** (`HeroBasinSegmentation`, editor `SegmentId.shader`): an unlit ID pass per budget category (alpha
  cut like Nature Lit), plus foam and orange read from the beauty frame. Results go to `stats.txt` and `F4_mask.png`.
- **Shared atlas materials:** `LookTestMaterials.ShareEnvironmentSets`. Saving a second material under the same
  path deleted the first, so pieces sharing an atlas (canopy crowns, ferns, vine crests) silently fell back to
  stand-ins. On for painterly only. The realistic hero basin and the look test still rely on the old fallback, and
  turning it on there visibly changes their reviewed output (kit crowns on bare trunks). Enable it there only after a
  look review.
