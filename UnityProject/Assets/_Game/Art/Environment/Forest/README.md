# Expedition forest: path, terrain, canopy and trees (painterly, F4_f)

Owner: asset-pipeline. For the Verdant Forest, river and canopy beats of spec 103 (C1-C13). Conventions as
`../Obstacles/README.md` (FBX, metres, `_LOD0/1/2`, one material per texture set, BaseColor / Normal / ARM).
Regenerate: `tools/art/forest_finish.py ground backdrops` (Blender's Python), `run.sh forest_kit.py canopy platform
edges`, `run.sh forest_trees.py [A B Gate mid]`, then `forest_readme.py`.

## Trail ground (tiling)
`Textures/TR_TrailGround_P_BaseColor.png` (1024, tiles seamlessly; suggested 1 tile = 4 m, world or path-UV mapped)
and `_Normal.png` (gentle, derived). Painted packed earth, sparse pebbles and leaves, low contrast so it does not
flicker at 16 m/s (AD C D3). Albedo mean ~#A48A55 (lit by the warm sun it lands near AD s8 path earth #B99643).
Roughness constant 0.92 (no ARM).

## Canopy (C8)
Beams have their **walk top at Y = 0** and run from Z = 0 to Z = L (pivot = near end of the walk band, centre); the
branch continues ~0.9 m past both ends and tapers down, so lips read as branch ends. Chain beams over gaps; `Mid` has
a constant section for long runs (overlap 0.4 m). The platform is the takeoff funnel / landing pad with its trunk.
Pair with `../Obstacles/OB_VineAnchor` + `OB_VineRope_7m`, `OB_VineCurtain_3m`, `OB_BranchKnot_05`.

## Trees
Stiltwoods (ART_DIRECTION s5.1 #3) are procedural, not the Meshy stiltwood meshes (reviewed: base plate, fused or
noisy roots; painterly needs few big clean arches). Each tree: `<name>_LOD0..2` (bark, leaf clumps; own 1024 set,
upper trunk at ~0.45x texel weight) **plus** `<name>_Crowns_LOD0..2` (alpha-clip canopy cards, material
`Plants/Textures/FP_Canopy_P`, vertex colour R = wind, A = AO, dome normals; LOD0 3 cards per crown, LOD1 2, LOD2 1).
- `FT_Stiltwood_Gate`: the C1 opening tree; roots only to the sides, trunk lifted at 8.4 m. **Clear box |X| <= 4.5,
  |Z| <= 6, Y <= 6.5 is empty** (checked by the script: lowest geometry over the path 6.9 m), so the run and camera pass
  under it. Pivot = tree centre on the floor; put it on the path centre line.
- `FT_Stiltwood_A` (37 m) / `_B` (46 m): roadside giants (8-12 stilt roots, some forked); keep them >= 9 m from the
  path centre. Impostors beyond ~120 m are a later task (not delivered).
- `FT_MidTree_A/B` (21 / 24 m): slimmer buttressed trees for the mid ground, sharing `FT_MidTrees`.

## Undergrowth (reuse)
Use the hero-kit painterly clumps in `../Plants/`: `FP_Fern_P_Clump`, `FP_PalmFern_P_Clump`, `FP_Bellflower_P_Clump`
(orange = risky-route carrier only), `FP_FrameLeft/Right_P_Clump`, `FP_CanopyCrown_P_*`, `FP_Canopy_P_Clump`.

## Backdrops (`../Backdrops/`)
`BD_Forest_Far.png` (1536x560: hazy far canopy, emergent stiltwoods, rootstone pillars and an arch in golden haze),
`BD_Forest_Mid.png` (1536x723: mid forest wall, stilt-rooted trunks, two sun shafts), `BD_River_Valley.png`
(1536x714: river valley with falls from pillars, for C5-C7). Straight alpha, colour bled; ASTC 6x6, clamp. Layer far
behind mid; add warm fog per layer (AD s7).

## Assets
<!-- tables -->
| Asset | Texture set | Size W x H x D (m) | Tris LOD0 / 1 / 2 | Intended gameplay box | Measured |
|---|---|---|---|---|---|
| `TR_CanopyBeam_A_12m` | `TR_Canopy` 1024 (47 px/m) | 9.1 x 5.0 x 14.2 | 1,500 / 699 / 259 | walk top 0, 2.4 w, Z 0..12 (branch continues 0.9 m past each end) | top 0.02 m in box (intended 0.00) |
| `TR_CanopyBeam_B_10m` | `TR_Canopy` 1024 (47 px/m) | 8.7 x 4.3 x 12.2 | 1,400 / 649 / 240 | walk top 0, 2.0 w, Z 0..10 | top 0.02 m in box (intended 0.00) |
| `TR_CanopyBeam_Mid_8m` | `TR_Canopy` 1024 (47 px/m) | 9.1 x 4.5 x 10.7 | 1,100 / 500 / 200 | walk top 0, 2.4 w, Z 0..8, constant section (chain beams) | top 0.02 m in box (intended 0.00) |
| `TR_CanopyPlatform_20m` | `TR_CanopyPlatform` 1024 (31 px/m) | 12.3 x 28.1 x 27.7 | 2,500 / 1,099 / 399 | walk top 0, 7.4 w at Z 0 tapering to 2.9 w at Z 20 (C8 takeoff funnel), trunk behind | top 0.03 m in box (intended 0.00) |
| `TR_PathEdge_Roots_8m` | `TR_Edges` 1024 (54 px/m) | 2.6 x 0.8 x 9.1 | 1,500 / 700 / 259 | path edge strip, Z 0..8, X -0.4 (path side) .. +2.2 (out); left side = rotate 180 |  |
| `TR_PathEdge_Moss_8m` | `TR_Edges` 1024 (54 px/m) | 2.4 x 0.8 x 8.4 | 1,299 / 600 / 220 | as above, moss cushions + stones |  |
| `TR_Riverbank_10m` | `TR_Edges` 1024 (54 px/m) | 5.5 x 1.7 x 10.8 | 1,499 / 699 / 260 | pivot at the water line; path top at Y +1.0; bank faces +X (river side) |  |
| `TR_WaterEdge_10m` | `TR_Edges` 1024 (54 px/m) | 2.7 x 0.9 x 10.2 | 1,500 / 700 / 260 | pivot at the water surface; shallow edge stones + reeds |  |
| `FT_Stiltwood_A` | `FT_Stiltwood_A` 1024 (32 px/m) | 14.3 x 37.6 x 15.0 | 5,815 / 2,999 / 1,000 |  |  |
| `FT_Stiltwood_B` | `FT_Stiltwood_B` 1024 (28 px/m) | 22.0 x 46.4 x 17.7 | 6,631 / 3,000 / 1,000 |  |  |
| `FT_Stiltwood_Gate` | `FT_Stiltwood_Gate` 1024 (35 px/m) | 20.5 x 38.5 x 12.6 | 5,228 / 3,000 / 999 |  |  |
| `FT_MidTree_A` | `FT_MidTrees` 1024 (54 px/m) | 9.7 x 21.4 x 7.1 | 1,549 / 1,299 / 449 |  |  |
| `FT_MidTree_B` | `FT_MidTrees` 1024 (54 px/m) | 8.6 x 24.5 x 9.2 | 1,747 / 1,299 / 450 |  |  |
<!-- /tables -->
