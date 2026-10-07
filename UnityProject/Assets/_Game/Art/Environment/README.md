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
| Ground_PathTile | (retired from the run: stretched 8.4 x 12 m it smeared; kept for reference) | 1 m wide x 1 m long (X,Z), top at y=0 |
| Ground_RavineEdge | ravine lips | 1 m wide x 1 m deep, lip face at z=0, body toward -Z, top at y=0 |
| Prop_VineBranch | vine branch | unit cube centered (scaled lane-wide, 0.35 m thick) |
| Prop_Signpost | vine signpost | real size, base at y=0, about 2 m tall |
| Foliage_TreeA / TreeB / Bush | verge dressing fallback (only when no Jungle_Wall* exists) | real size, base at y=0, spaced 9 m along each verge |
| Tree_Trunk (scenery use) | giant trunks in the mid ring and, scaled thin (x/z 0.12 to 0.2, y 0.35 to 0.5), near-ring trunks | as for the swing anchor: base centre, native radius 1.5 m, about 34 m tall |
| Foliage_TreeA / TreeB (scenery use) | big mid-ring trees of `ScenerySystem` | real size, base at y=0, crown reach about 2.2 m at scale 1 |
| Foliage_Bush / Foliage_FernCluster / Prop_Rock / Prop_Root | near-ring bushes, ferns, rocks, root arches of `ScenerySystem` (Prop_Rock and Prop_Root are not made yet: a generated gray-box shape shows) | real size, base at y=0; Prop_Root about 1.8 x 0.6 x 0.6 m |
| Vine_Liana (scenery use) | thin non-gameplay hanging vines (scaled 0.45 to 0.7 wide, dimmed 40 percent) | hangs -Y from y=0 |
| Jungle_WallA / B / C | verge jungle walls, one 12 m segment per side per slot | x = 0 at the path edge, plants toward +x, ground at y=0, z -6..+6 m; built by `tools/blender/build_jungle_kit.py`; share `Jungle_Atlas_basecolor.png` |

Textures without a model (loaded by name, `<Name>_basecolor.png`):

| Texture | Used by | Layout |
|---|---|---|
| Ground_Trail | GroundView trail strip (tinted by the world's Path color) | U = trail cross-section x -5.4..+5.4 m (clamped; path 4.2 m half width + grassy edge), V repeats every 6 m |
| Ground_JungleFloor | GroundView floor strips (tinted by the world's Verge color) | repeats every 6 m in U and V |
| Jungle_Atlas | every Jungle_* model | 1024 atlas: 4x4 cells of 256 px, swatch strip under the rock cell |

Look pass (2026-10-07): trail + floor strips (3 draw calls), jungle walls (about 16 segments visible, one draw call
and about 3.4k triangles each), `SkyView` gradient dome + far canopy rings (2 draw calls, drawn first, unfogged),
trilight ambient, key light from the look config. Tuning: `EnvironmentLookConfig` (asset `Config/Resources/EnvironmentLook`).
Rebuild: `python3 tools/assetgen/make_ground_textures.py <EnvironmentArt>` and
`python3 tools/blender/build_jungle_kit.py Art/Environment/Models <EnvironmentArt>`. Preview without Unity:
`python3 tools/blender/run_mock.py <EnvironmentArt> out.png [--before] [--world dusk]` (Previews/look_before.png, look_after.png).

Budgets: props <= 1.5k tris (path tile <= 3k), wall segments <= 4k (a merged cluster of several props), shared atlas, textures <= 1024, ASTC. Style: design/STYLE_GUIDE.md
(Inkbound Pulp, hazard red only on hazards with ink stripes, coin gem #2EC4B6). Generation: tools/assetgen/.
