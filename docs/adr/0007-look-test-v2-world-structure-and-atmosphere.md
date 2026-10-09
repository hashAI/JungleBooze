# ADR 0007: Look test v2 world structure, mesh merging and atmosphere

- Status: Accepted for the look test (build-order steps 2 and 3 of `design/aurelia/ENVIRONMENT_STRATEGY.md`)
- Date: 2026-10-09
- Deciders: tech-architect
- Hard to undo: **partly**. Every later asset follows the vertex-color convention and the merge rule below, and
  every project shader uses the shared atmosphere include.

## Context
The v1 look test read as a park avenue (straight trail, flat light) and drew 810–930 calls per view (ADR 0004).
The strategy asks for an S-curved, climbing trail with an enclose → reveal cadence, a draw/triangle budget skeleton
measured before art, and light and atmosphere strong enough that a grey world already feels deep (Checkpoint A).

## Decisions
1. **Path space world.** The trail is a periodic curve (`LookTestPath`: heading and height keys over a 225 m loop).
   The ground, river and scatter are built as functions of `(s, d)` (`LookTestStretchLayout`). Each loop is the
   previous one shifted by `LoopOffset`, so segments are moved by whole loop offsets (`LookTestWorldView`). This is
   the same frame spec 101 uses for the simulation (`s`, `x`), so look-test chunks carry over to production chunks.
   Rule: the radius of any bend stays above the strip half width (52 m), or the ground strip folds.
2. **Merge per 25 m segment** into one mesh per (budget layer, material, shadow mode) (`LookTestBatchSet`).
   Renderer names start with the budget layer (`L0`…`L6`, `W`, `FX`), so the screenshot tool can add up draws and
   triangles per layer (`LookTestBudgetTally`). Dense near detail (verge scans) sits in a per-segment `Detail` group
   that is switched on only within 50 m ahead.
3. **Vertex color convention for merged Nature Lit meshes:** R = wind weight, G = free, B = canopy cover,
   A = ambient occlusion. Bought or AI-made assets are painted at merge time, so source assets need no colors.
4. **Atmosphere in every project shader** (`JBAtmosphere.hlsl`, globals from `LookTestAtmosphere`): analytic
   height fog with a warm in-scatter toward the sun and a cool haze away from it (the sky uses the same function for
   its horizon band). Also a **canopy light**: under cover B the sun reaches surfaces only through a tiling dapple
   pattern projected along the sun, and the ambient drops and turns green. This replaces Unity fog and a shadow-
   casting canopy (no extra pass, no depth texture). Light shafts and mist are camera-facing cards merged per
   segment (`AtmosCard.shader`).
5. **Backdrop rides with the run** (moves by `LoopOffset × s / loop`): no translation parallax, never pops. Near
   landmarks (basin arch, pillars) stay in segments with real parallax.
6. **Grade** = URP's internal LUT (ACES, split toning, shadows/midtones/highlights, white balance). An external
   keyframe LUT is not possible now (no purchased or AI keyframes); hand values stay in `LookTestConfigAsset`.

## Measured (editor estimate, Apple M4, 9 shots incl. 2 portrait)
Main view 59–95 draws (budget 250), 206k–277k triangles (350k). Shadow pass 10–14 draws (100), 26k–52k triangles
(150k). Per layer the strategy lines are missed for L0 (CC0 fern scans, about 3× its 50k line), L4 and L5
(geometry stand-ins for impostors and matte paintings). Real counters need the device run (ADR 0004 protocol).

## Consequences
- Generated meshes are large YAML assets (about 54 MB). Recommended: git-ignore `Art/LookTest/Meshes` and the scene,
  and rebuild them with `LookTestBatch.BuildScene` (deterministic).
- A stale `LookTestConfig.asset` keeps old values: delete it to take new defaults.
- The environment bake must run with the sky's horizon fog off (done by the builder); a NaN ambient turns every lit
  surface black (the builder reports it).
