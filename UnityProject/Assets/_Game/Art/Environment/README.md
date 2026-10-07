# Environment art

Real low-poly art for the run views. Each view loads a model by name from `Resources/EnvironmentArt/`
(`EnvironmentArt.cs`). If the model is missing the view keeps its gray-box primitive, so art can land one piece at a time.
Workflow: put the model (`<Name>.fbx`) and its base color texture (`<Name>_basecolor.png`) in `Resources/EnvironmentArt/`,
named exactly as below. There is no prefab build step: the FBX is instantiated directly. On import,
`Art/Environment/Editor/EnvironmentImportPostprocessor.cs` remaps each FBX to a URP Simple Lit material
(`Materials/<Name>_Mat.mat`) made from the PNG; if that ever did not run, `EnvironmentArt.cs` builds a textured
material at runtime from the same PNG. Colliders, cameras and lights are stripped at runtime. The menu
`JungleBooze > Art > Check Environment Models` logs which models load and their triangle counts. The `.glb` sources
stay in `Models/` (not in Resources). The run start logs which of the 17 names loaded (`[JungleBooze] Environment art`).

The simulation owns collision. Art is scaled by the view to the hitbox, so author it to the fit rule below
(pivot and size matter; export with Y up, meters, 1 unit = 1 m).

| Prefab name | Replaces | Fit rule |
|---|---|---|
| Obstacle_LowBarrier | low barrier (jump) | unit cube centered at origin (view scales to hitbox) |
| Obstacle_HighBarrier | high barrier (slide) | unit cube centered |
| Obstacle_FullBlock | lane block | unit cube centered |
| Obstacle_Boulder | rolling mover | unit sphere (diameter 1) |
| Hazard_ThornPatch | lane denial | unit cube centered |
| Hazard_StrikeColumn | lane strike column | unit cube centered |
| Pickup_Coin | coin | 0.5 m diameter, face toward +/-Z, turquoise gem center, centered |
| Pickup_Magnet / Pickup_Shield / Pickup_Boost | power-up icons | about 1 m across, facing -Z, centered, glow baked in |
| Ground_PathTile | path tile | 1 m wide x 1 m long (X,Z), top at y=0, scaled to path width x 12 m |
| Ground_RavineEdge | ravine lips | 1 m wide x 1 m deep, lip face at z=0, body toward -Z, top at y=0 |
| Prop_VineBranch | vine branch | unit cube centered (scaled lane-wide, 0.35 m thick) |
| Prop_Signpost | vine signpost | real size, base at y=0, about 2 m tall |
| Foliage_TreeA / TreeB / Bush | verge dressing | real size, base at y=0, spaced 9 m along each verge |

Budgets: props <= 1.5k tris (path tile <= 3k), shared atlas, textures <= 1024, ASTC. Style: design/STYLE_GUIDE.md
(Inkbound Pulp, hazard red only on hazards with ink stripes, coin gem #2EC4B6). Generation: tools/assetgen/.
