# ADR 0008: Layered water and mist without a depth texture; environment asset integration

- Status: Accepted for the look test (Checkpoint A gaps; ENVIRONMENT_STRATEGY 4.4, 4.5, 5)
- Date: 2026-10-09
- Deciders: tech-architect
- Hard to undo: **partly.** The asset-pipeline contract (folders, names, LOD and anchor conventions) and the vertex
  color meanings below are used by every environment asset from now on.

## Context
Checkpoint A left five gaps against the keyframes: flat white waterfall slabs, mist that did not read, black stilt
roots, a portrait (P1) frame cut off by the basin arch, and a milky grey-white ford. ADR 0004 rules out a depth or
opaque texture, so soft particles, refraction and depth-based shore foam are not available. Asset-pipeline also
started delivering the rootstone kit, stiltwoods and backdrop layers into `Assets/_Game/Art/Environment/`, and those
need a fixed path into the merged segments of ADR 0007.

## Decisions
1. **Layered waterfalls** (`Waterfall.shader`, `LookTestMeshFactory.FallLayers`): every fall is up to four
   sheets: core, ragged front veil, and two side spray streaks. One shader samples a generated 256² water FX texture
   (`WaterFx.png`: R falling streaks, G lacy foam, B breakup) in meter-space UVs, so all falls share texel density.
   Vertex color: R foam (lip and plunge), G layer speed, B pattern seed, A edge coverage. Ragged edges come from
   thresholding coverage with the streaks, not from alpha cards. All sheets of a segment merge into the existing
   `Falls` batch, so there are no extra draws.
2. **Fall foot** (`LookTestLandmarks.Fall`): impact foam is a disc in the pool batch (same material, drawn after the
   pool surface in the same mesh). Spray and mist are Atmos Card cards in the mist batch; UV1.w = card kind
   (0 mist, 1 spray). A fall is recorded as a `FallSpan`, and rock near it is painted **wet** in vertex color G
   (Nature Lit `_WetFromVertexG`: darker, glossier). G is free on non-layered Nature Lit meshes (ADR 0007).
3. **Soft cards without depth:** mist cards are pulled toward the camera by part of their width (so they stand in
   front of what they hug), their base fades out, and they fade when seen edge-on or from close up. That is the
   depth-fade substitute. Never place mist cards flat over walkable water (the ford read as milky).
4. **Water depth in vertex color G** (`Water.shader`): baked from the layout (water height minus ground). Shallow
   water is turquoise and clear (`_ShallowOpacity`); deep water is darker and more opaque. Shoreline foam is where
   depth is under about 0.2 m, plus rapids before a fall, thresholded by the lacy foam texture. The sky reflection
   is tinted (`_ReflectionTint`) and scaled, so grazing angles stay turquoise instead of turning white.
5. **Bark and rock response** (Nature Lit, per material, no extra textures): moss on upward faces, ground bounce on
   downward faces (lifts root and arch undersides), and a rim sheen lit by the ambient and the dappled sun.
6. **P1 scale:** the hero arch is set farther out and lower (`BasinArchFeet`, `BasinArchControlY`) so its top sits
   about 3° or more under the portrait frame top (EditMode test). Tall pillars (`BasinPillars`) rise behind it,
   and the plunge mesa is raised so the pool clears the near bank.
7. **Environment integration contract** (`Editor/Scenery`, namespace `JungleBooze.Editor.Scenery`; not
   `...Environment`, which would shadow `System.Environment` in the editor assembly):
   - Folders: `Rootstone/` (`[RS_]Pillar*`, `[RS_]HeroArch*`, `[RS_]Arch*`, `Bridge*`, `Outcrop*`, `Cliff*`),
     `Stiltwoods/` (`[SW_]Stiltwood*`, `[SW_]LeafCards*`), `Backdrops/` (`backdrop_NN_name.png`, 01 = farthest).
   - Models: +Y up, meters; arches span local X with a foot at each end. `*_LOD1..` meshes are skipped (the merge
     uses LOD0). An empty `*_WaterfallMouth` is the fall origin of an arch.
   - Textures: `<piece>_BaseColor|_diff`, `_Normal|_nor_gl` (OpenGL), `_ARM`, `_alpha`, next to the model or in
     `Textures/`. A piece uses its own set (it gets an `Env_<piece>` Nature Lit material set up by role), else the
     kit set (`rootstone_*`), else the CC0 stand-in material, so it shares the stand-in's batch.
   - Import (`EnvironmentAssetImportRules`, first import only): no materials, Mikk tangents, medium compression, no
     generated Mesh LOD; textures ASTC 6x6 / 5x5 normal / 8x8 ARM and backdrops, max 2048.
   - Placement hooks: stiltwoods (`Stand`, replaces trunk and stilt roots; the hero's arching roots stay
     procedural for the spec 101 corridor), the basin hero arch (`Span`, fall from the mouth anchor), basin and far
     range pillars (`Stand`, footprint scaled toward the stand-in radius within 0.6–1.6x), outcrops on the ledge
     and in the ford, and backdrop layers (cylinder strips at `BackdropDistancesM`, `Backdrop Card` shader, partial
     project fog). Every piece is appended to the segment's merge batches: merged per segment, one draw per material.
   - With no assets, every hook falls back to its procedural stand-in, so the art can land piece by piece.

8. **Hero scene** (`Scenes/HeroBasin.unity`, `JungleBooze.Editor.HeroBasin.HeroBasinBatch.BuildAndCapture`): the F4_e
   keyframe built once at full quality with the same machinery. Every number is in `HeroBasinConfigAsset`; the look
   values the shared shaders read are copied into a generated `HeroBasinLook.asset`, and materials go to
   `Art/HeroBasin/Materials`, so building it never changes the look test. Painted layers are sphere strips around
   the camera (UV linear in azimuth and elevation, sized from the painting's *source* pixel size: NPOT scaling off).
   Plant atlases carry the cutout in albedo A (Nature Lit `_AlphaFromBaseA`). Capture writes the landscape and
   portrait renders and a side-by-side comparison with the keyframe.
9. **Far placements keep stand-ins** until LOD2 or impostors are wired. Kit LOD0 in the 20 far-range slots took the
   loop from about 315k to about 400k triangles.

## Measured (editor, Apple M4, 8 shots)
Main view 62–94 draws (budget 250), 217k–286k triangles (350k), shadow pass 10–14 draws, ≤ 52k triangles. Waterfalls
add about 1k triangles per hero fall (four sheets); the W layer line (12k) is exceeded in basin views. The total
budget still holds. The real cost is overdraw from the layered sheets and cards, which needs the iPhone 12 run
(ADR 0004 protocol).

## Consequences
- The config asset keeps old values when defaults change: delete `LookTestConfig.asset` before Build Scene.
- `RS_Outcrop.fbx` was first imported while the rule still generated Mesh LODs; its .meta has
  `generateMeshLods: 1`. That is harmless (the merge uses LOD0) but should be reimported once with the current rule.
