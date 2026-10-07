# Environment art

Real low-poly art for the run views. Each view looks for a prefab by name in `Resources/EnvironmentArt/`
(`EnvironmentArt.cs`). If the prefab is missing the view keeps its gray-box primitive, so art can land one piece at a time.
Workflow: put models in `Models/` named exactly as below, optionally add `Materials/EnvAtlas.mat`, then run the menu
`JungleBooze > Art > Build Environment Prefabs` (checks triangle budgets, strips colliders, assigns the atlas).

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
