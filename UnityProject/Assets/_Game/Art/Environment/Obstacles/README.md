# Expedition obstacles, pickups and discovery markers (painterly, F4_f)

Owner: asset-pipeline. Integration: gameplay-engineer (views) + tech-architect (materials). Rules:
`design/aurelia/ART_DIRECTION_PAINTERLY.md`, gameplay sizes: spec 101 s2.6/s4.1, spec 103 s3-s9.
Regenerate: `tools/blender/environment/run.sh forest_kit.py [roots logs vines water stone hazard pickups]`, then
`python3 tools/blender/environment/forest_readme.py` (tables below). Previews and lineups:
`art_source/environment/previews/forest/` (local only).

## Conventions
- FBX, metres, Y-up, scale 1. One FBX per piece with `<name>_LOD0/1/2` (Unity builds the LODGroup; suggested screen
  heights 0.25 / 0.08 / 0.02). Mesh compression Medium, Read/Write off.
- **Pivot = the obstacle's authored front face, centred, on the floor** (Unity local Z = 0 is spec `s`, the piece extends
  toward +Z). Water pieces: pivot on the water surface. Pickups: pivot at the pickup point. Unity +X = path `x`.
- Visuals sit inside the authored box (measured column, Unity metres after export). The spec's forgiveness (tops -0.10,
  bottoms +0.10, x -0.10/side) stays in code. Where the visual is deeper than the default 0.6 m (roots ~1.0 m), use the
  visual depth for the box or keep 0.6 m: both read fine.
- **One material per texture set**: pieces of a set share `<Set>_BaseColor` (sRGB), `_Normal` (tangent, OpenGL +Y,
  broad forms only), `_ARM` (R = AO, G = roughness, B = metallic: 1 only on coin gold and brass). ASTC 6x6 BaseColor,
  5x5 Normal, 8x8 ARM; mips on. No vertex colours (painted AO is in the albedo).
- Shader: the painterly lit shader (wrapped diffuse, teal shadows). Coins and crystals want the self-lit sparkle from
  ART_DIRECTION s3.3 (emission = BaseColor x ~0.6 plus a sparkle), the Shield a slow fresnel rim.

## Readability (self-review, chase-camera test at 16 m/s)
- Jump obstacles = long low dark masses across the path with a warm worn top rim; slide obstacles = arches with a
  clear lit gap below 1.0 m (branch, vine curtain of leafy ribbons with a clean bottom line); blockers = tall leaning
  stacked rocks (darker and cooler than the path) or root walls; thorns = near-black tangles with crimson tips and
  crimson warning blooms (hazard crimson only here). Pickups are the brightest, most saturated things on screen.
- Test frames: `previews/forest/chase_test.jpg` (+ `_gray`, `_small` = phone size).

## Assets
<!-- tables -->
| Asset | Texture set | Size W x H x D (m) | Tris LOD0 / 1 / 2 | Intended gameplay box | Measured |
|---|---|---|---|---|---|
| `OB_RootLow_05` | `OB_Roots` 1024 (72 px/m) | 9.5 x 0.7 x 1.0 | 1,070 / 600 / 220 | Low top 0.5, full 7.0 w x 0.6 d (visual root band ~1.0 m deep) | top 0.47 m in box (intended 0.50) |
| `OB_RootLow_06` | `OB_Roots` 1024 (72 px/m) | 9.5 x 0.8 x 1.1 | 1,088 / 650 / 239 | Low top 0.6, full 7.0 w x 0.6 d | top 0.59 m in box (intended 0.60) |
| `OB_RootLow_Half_05` | `OB_Roots` 1024 (72 px/m) | 5.6 x 0.7 x 0.9 | 648 / 399 / 160 | Low top 0.5, 4.0 w x 0.6 d centred on the pivot (also Low 0.4 x +0.5..+4.5: scale Y 0.8) | top 0.45 m in box (intended 0.50) |
| `OB_BranchHigh_10` | `OB_Roots` 1024 (72 px/m) | 10.8 x 3.0 x 1.6 | 1,500 / 700 / 260 | High bottom 1.0, full 7.0 w x 0.8 d, up to 3.0 | lowest 1.10 m over the span (intended bottom 1.00) |
| `OB_BranchKnot_05` | `OB_Roots` 1024 (72 px/m) | 3.4 x 0.8 x 1.3 | 370 / 370 / 140 | Low top 0.5, Walk, 2.4 w x 0.8 d (on a canopy beam) | top 0.51 m in box (intended 0.50) |
| `OB_LogWalk_09` | `OB_Logs` 1024 (37 px/m) | 10.2 x 1.1 x 3.3 | 762 / 699 / 260 | Low top 0.9, Walk, full 7.0 w x 2.0 d (C2 s70; C13 Low 0.8: scale Y 0.89) | top 0.89 m in box (intended 0.90) |
| `OB_LogWalk_Giant_09` | `OB_Logs` 1024 (37 px/m) | 12.1 x 3.5 x 6.4 | 1,037 / 700 / 279 | Low top 0.9, Walk, full 7.0 w x 5.0 d (C12 s15) | top 0.86 m in box (intended 0.90) |
| `OB_Driftwood_05` | `OB_Logs` 1024 (37 px/m) | 8.5 x 0.6 x 2.4 | 656 / 380 / 139 | Low top 0.5, Walk, full 7.0 w x 0.6 d (C5 ford, in shallow water) | top 0.50 m in box (intended 0.50) |
| `OB_RootWall_4m` | `OB_Logs` 1024 (37 px/m) | 4.1 x 4.3 x 1.9 | 1,499 / 699 / 260 | Blocker 4.0 w (x -2..+2 about the pivot) x 1.6 d, full height (C2/C12 half-path blockers) | width 4.00 m at 0.15-1.8 m, x -2.00..2.00, height 4.05 |
| `OB_RootWall_Divider_10m` | `OB_Logs` 1024 (37 px/m) | 1.3 x 4.2 x 11.3 | 1,499 / 699 / 279 | Divider / blocker 1.2 w x 10 d per segment (tile along Z, overlap 0.4 m) | width 1.09 m at 0.15-1.8 m, x -0.55..0.54, height 3.89 |
| `OB_VineCurtain_7m` | `OB_Vines` 1024 (47 px/m) | 12.3 x 4.6 x 1.6 | 1,500 / 699 / 260 | High bottom 1.0 (visual bottom 1.06-1.3), full 7.0 w x 0.6 d | lowest 1.15 m over the span (intended bottom 1.00) |
| `OB_VineCurtain_3m` | `OB_Vines` 1024 (47 px/m) | 5.3 x 6.4 x 1.1 | 899 / 420 / 159 | High bottom 1.0, 2.4 w x 0.6 d (canopy beam, C8 s160) | lowest 1.14 m over the span (intended bottom 1.00) |
| `OB_VineAnchor` | `OB_Vines` 1024 (47 px/m) | 17.7 x 6.8 x 2.0 | 1,499 / 699 / 259 | no collision; pivot = rope attach point (anchor sA, hA) |  |
| `OB_VineRope_7m` | `OB_Vines` 1024 (47 px/m) | 0.4 x 7.0 x 0.5 | 700 / 300 / 120 | no collision; pivot = top (attach), hangs to -7.0; scale Y to the vine length L; empty _Grip at -6.7 |  |
| `OB_FloatLog_9m` | `OB_Water` 1024 (66 px/m) | 9.8 x 0.7 x 2.0 | 786 / 500 / 200 | FloatingLog -0.4..+0.4 about the water surface, 9.0 w x 0.8 d | top 0.37 m in box (intended 0.40) |
| `OB_DebrisMat_5m` | `OB_Water` 1024 (66 px/m) | 5.4 x 0.6 x 1.9 | 800 / 599 / 219 | FloatingLog (debris mat) 5.0 w (x -2.5..+2.5) x 0.8 d | top 0.32 m in box (intended 0.40) |
| `OB_Snag_9m` | `OB_Water` 1024 (66 px/m) | 9.7 x 1.7 x 1.2 | 1,163 / 699 / 260 | Snag riverbed..+0.3, 9.0 w x 0.8 d (leap only) | top 0.23 m in box (intended 0.30) |
| `OB_LowBranch_Water_55` | `OB_Water` 1024 (66 px/m) | 8.9 x 2.9 x 1.9 | 998 / 600 / 219 | LowBranch bottom +0.5 above water, 5.5 w centred on the pivot (C6: x -1.0..+4.5 -> pivot x +1.75) | lowest 0.51 m over the span (intended bottom 0.50) |
| `OB_RiverRock_14` | `OB_Water` 1024 (66 px/m) | 1.7 x 3.4 x 1.5 | 444 / 444 / 160 | Rock 1.4 w x 1.4 d, full height (top 1.6 above water, base 1.7 below) | width 1.40 m at 0.15-1.8 m, x -0.70..0.70, height 1.63 |
| `OB_SteppingRock_A` | `OB_Water` 1024 (66 px/m) | 2.9 x 1.6 x 2.8 | 418 / 320 / 120 | walk top 0 (wet stepping rock 2.6 x 2.7, C9 risky) | top -0.01 m in box (intended 0.00) |
| `OB_SteppingRock_B` | `OB_Water` 1024 (66 px/m) | 2.6 x 1.6 x 3.9 | 375 / 320 / 120 | walk top 0 (2.3 x 3.6) | top 0.02 m in box (intended 0.00) |
| `OB_Boulder_12` | `OB_Stone` 1024 (42 px/m) | 1.3 x 2.8 x 1.3 | 691 / 500 / 179 | Blocker 1.2 w x 1.2 d, height 2.55 | width 1.20 m at 0.15-1.8 m, x -0.60..0.60, height 2.55 |
| `OB_Boulder_14` | `OB_Stone` 1024 (42 px/m) | 1.6 x 3.1 x 1.6 | 681 / 498 / 179 | Blocker 1.4 w x 1.4 d, height 2.8 | width 1.40 m at 0.15-1.8 m, x -0.70..0.70, height 2.82 |
| `OB_Boulder_16` | `OB_Stone` 1024 (42 px/m) | 1.7 x 3.3 x 1.7 | 673 / 500 / 180 | Blocker 1.6 w x 1.6 d, height 3.0 | width 1.60 m at 0.15-1.8 m, x -0.80..0.80, height 3.04 |
| `OB_Boulder_20` | `OB_Stone` 1024 (42 px/m) | 2.1 x 3.5 x 2.0 | 715 / 499 / 180 | Blocker 2.0 w x 2.0 d, height 3.3 | width 2.00 m at 0.15-1.8 m, x -1.00..1.00, height 3.30 |
| `OB_WetRockLow_05` | `OB_Stone` 1024 (42 px/m) | 8.4 x 0.6 x 1.2 | 1,300 / 600 / 220 | Low top 0.5, full 7.0 w x 0.6 d (C7 wet rock) | top 0.48 m in box (intended 0.50) |
| `OB_GapLip_8m` | `OB_Stone` 1024 (42 px/m) | 8.5 x 6.5 x 3.9 | 1,499 / 700 / 260 | Gap edge: lip at Z = 0, floor (top -0.02) for Z -3..0, drop 6.3 m; far lip = rotate 180 deg | top -0.02 m in box (intended -0.02) |
| `OB_GapLip_4m` | `OB_Stone` 1024 (42 px/m) | 5.1 x 6.4 x 3.9 | 999 / 450 / 180 | Gap edge for partial gaps (4.5 w), as above | top -0.02 m in box (intended -0.02) |
| `OB_Thorns_15` | `OB_Hazard` 512 (71 px/m) | 1.6 x 0.7 x 1.2 | 615 / 345 / 134 | Thorns 1.5 w x 1.2 d, top 0.6 | top 0.48 m in box (intended 0.62) |
| `OB_Thorns_25` | `OB_Hazard` 512 (71 px/m) | 2.5 x 0.8 x 1.0 | 978 / 575 / 225 | Thorns 2.5 w x 1.2 d, top 0.6 | top 0.53 m in box (intended 0.62) |
| `OB_Thorns_35` | `OB_Hazard` 512 (71 px/m) | 3.5 x 0.8 x 1.2 | 1,329 / 805 / 314 | Thorns 3.5 w x 1.2 d, top 0.6 (full width = 2 side by side) | top 0.59 m in box (intended 0.62) |
| `PK_Coin` | `PK_Pickups` 512 (117 px/m) | 0.5 x 0.5 x 0.1 | 420 / 160 / 64 | Coin point at the pivot (centre); faces Z; spin about Y |  |
| `PK_Crystal` | `PK_Pickups` 512 (117 px/m) | 0.4 x 0.6 x 0.3 | 72 / 72 / 60 | Crystal point at the pivot |  |
| `PK_Shield` | `PK_Pickups` 512 (117 px/m) | 0.6 x 0.6 x 0.1 | 624 / 300 / 110 | Shield pickup at the pivot; faces -Z (toward the runner) |  |
| `DM_Cairn` | `PK_Pickups` 512 (117 px/m) | 0.9 x 1.6 x 0.8 | 988 / 450 / 159 | Discovery marker (off path), pivot on the ground |  |
| `DM_SurveyPost` | `PK_Pickups` 512 (117 px/m) | 0.8 x 1.8 x 0.4 | 600 / 280 / 100 | Discovery marker (off path) |  |
<!-- /tables -->

Texture set per piece (the material to assign): `OB_Roots` roots, branch, knot; `OB_Logs` logs, driftwood, root walls;
`OB_Vines` curtains, anchor, rope; `OB_Water` swim obstacles, river rock, stepping rocks; `OB_Stone` boulders, wet low
rock, gap lips; `OB_Hazard` thorns (512); `PK_Pickups` coin, crystal, Shield, cairn, survey post (512).
Budgets: props/obstacles <= 1.5k tris (all LOD0 within it except none); pickups: coin 420 (many on screen; LOD1 160).

## Not delivered / known gaps
- Divider D1 for C9 (waterfall pillar) and D2 rock pillar: use `Rootstone/RS_*_P` slab pieces or a later pass.
- Veil Grotto / Sunken Arch set pieces, veilmoss and duskbell (secret carriers), whirlseeds: not in this task.
- Coin spin, pickup bob and sparkle, Shield bubble: VFX/shader work (tech-architect).
- The bushes on top of obstacles are painted lumpy shells (the PAINTERLY 'bush' recipe), not leaf cards: they read as
  round leaf clumps from the chase camera; up close they look soft.
