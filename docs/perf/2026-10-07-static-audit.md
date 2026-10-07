# Run scene smoothness audit (static, nothing measured)

Source: performance-engineer report, 2026-10-07. No Unity or device was used: every number is counted from code or estimated.
Verdict: the per-frame hot path is clean (no allocations, pooled objects, cached materials). Risks are project settings,
first-use hitches, frame-time judder, and an art pipeline that will not scale to a dense curved jungle.

## Ranked fixes
| # | Fix | Where | Expected impact (estimate) |
|---|---|---|---|
| 1 | Enable GPU skinning | ProjectSettings.asset:105 | -0.5 to -1.5 ms CPU |
| 2 | Renderer intermediate texture: Auto | Config/Rendering/URP-Mobile-Renderer.asset:55 | -0.5 to -1.5 ms GPU, less bandwidth and heat |
| 3 | Shader variant prewarm + pool prewarm behind the menu | GraphicsSettings.asset:37, RunSceneBootstrap | removes 10-50 ms first-use hitches |
| 4 | Font glyph prewarm (HUD, gate title, vine stamp) | HudFactory, WorldThemeView:312, VineView:471 | removes 5-20 ms hitches |
| 5 | Snap delta time to the 60 Hz refresh (within about 1.5 ms) | RunDriver:397, FixedStepTimeSource.Accumulate | removes judder, lowers latency |
| 6 | Draw coins with RenderMeshInstanced | CoinView | -45 to -135 draws |
| 7 | Guard the world-switch Debug.Log with isDebugBuild | WorldThemeView:222 | removes a GC and log hitch per gateway |
| 8 | Zero out near-zero animation clip weights | RunnerView:315, CompanionView | -20 to -40% animation CPU |
| 9 | Nested canvas for HUD counters | HudView | -0.1 to -0.3 ms, 20-30x per second |
| 10 | Wall slots: swap MeshFilter.sharedMesh instead of instantiating every variant | GroundView:446-466,509 | fewer GameObjects, scales to many variants |
| 11 | Fog end about 75 m, camera far about 90 m | RunSceneBootstrap:38, look config | -15 to -25% environment cost |
| 12 | Strip shader variants, AnyShadowsSupported off | GraphicsSettings, URP asset | smaller build |
| 13 | Merge or limit overlay canvases | HudView and 3 others | small |
| 14 | Sky colours from a gradient texture/uniform | SkyView | small |
| 15 | Minimum iOS/device to match iPhone 11 / SE 2 | ProjectSettings.asset:199 | avoids A9/A10 surprises |

## Budget for the curved jungle path (estimates)
- Scenery <= 60-70k tris (90k at most) and about 40 draws; total frame <= 120 draws, <= 150k tris.
- Draw calls and CPU submission limit first, then overdraw (no alpha-cutout leaf cards), not triangles.
- Keep pre-merged scenery chunks with LOD by swapping the mesh (near <= 40 m); do not GPU-instance trees; instance only coins, pickups and small props.
- One shared atlas for scenery, one for gameplay objects; a custom simple environment shader (vertex colour x atlas x fog) rather than URP Lit.
- Curve: vertex-bend shader driven by the same function as the CPU placement (camera, gameplay objects), with widened cull bounds. Alternative: rigid chunks on a spline (needs 6 m overlapping segments, radius >= about 80 m).
- Canopy at least 6 m above the camera, no alpha fade. Outline hulls on gameplay objects only.
- Floating origin for long runs (float precision grows past about 65 km).

## Measure first on iPhone 11
Development build (IL2CPP, Release, Metal validation off): 15-minute run with Profiler and thermal state; hitch hunt at first coin/obstacle/vine/power-up/gateway/restart; GC per frame (expect 0 B); A/B for GPU skinning and intermediate texture; Frame Debugger draw counts; steps-per-frame histogram for judder; memory snapshot; build report.
