# Obstacle wave 1 (Jungle): models and hitbox match

Spec 005 sections 5-9 and 15.2. Procedural bpy, zero API credits (Meshy untouched). Rebuild everything, then check it:

    python3 tools/blender/build_crossing_kit.py UnityProject/Assets/_Game/Art/Environment/Resources/EnvironmentArt --glb-dir <scratch>
    python3 tools/blender/verify_obstacle_wave1.py UnityProject/Assets/_Game/Art/Environment/Resources/EnvironmentArt
    python3 tools/assetgen/obstacles_sheet.py <out.png> <scratch>/Obst_*.glb     # hero-side contact sheet

Sources: `build_obstacle_wave1.py` (pieces + `SPEC`, the single numeric source), `build_crossing_kit.py` (palette atlas, exporter; it now
also builds wave 1; `--no-obstacles` skips). The atlas stays 512 px; rows were halved in height (16 px) to hold 32 colour rows, the 10
old rows keep their index and colour, 8 rows were appended (soil, pad, sand, heartwood, thorn, ink, ochre, pod).

## Conventions
- Unity axes, metres, FBX Y up / -Z forward, one `<Name>.fbx` + `<Name>_basecolor.png` per piece. Hero runs +z, so the lethal FRONT face is at -z.
- Origin = footprint centre of the hitbox, y = 0 = ribbon surface. Static bodies carry the G1 embed baked in (lowest vertex at -0.06;
  the constant is `EMBED` in `build_obstacle_wave1.py`, set 0 if the rig embeds instead). The part above y = 0 fills the hitbox.
- Hitboxes come from `ObstacleKitDesignValues.cs` (width x depth, y bottom..top): LowBarrier 2.04 x 0.6, 0..0.8 | HighBarrier 2.04 x 0.5, 1.1..3.0 |
  FullBlock 2.04 x 1.0, 0..3.0 | Mover 1.90 x 1.6, 0..1.9 | LaneDenial 2.04 x 4.0, 0..2.2 (per lane).
- Hazard red is the `ochre` atlas row (`#D7263D`): only as bands flanked by `ink` on hazard bodies (log ridge, mat bundle wrap, fin and stone
  hip band, barrel ring, furrow ticks, thorn cage posts). Thorn tips are `ink`. The verifier fails red on any other piece or red without ink.
- Flat decals (Wallow, Furrow, SandBar) sit at y 0.012..0.10 (a groove cannot be cut into the ribbon, so it is painted).

## Hitbox match (numbers from `verify_obstacle_wave1.py` after FBX re-import)
Ghost = distance along +z from the front plane to the first surface, sampled on an 8 cm grid over the whole front face (0.5 if none);
limit 0.15 static, 0.25 mover (spec 3.2). `p95/max` and the share of samples above the limit are printed per piece.

| Piece | Tris/budget | Extents x / y / z | Hitbox x / y / z | Ghost p95 / max, >limit |
|---|---|---|---|---|
| Obst_Log_Trunk | 302/1400 | +-1.17 / -0.06..0.80 / -0.47..0.51 | +-1.02 / 0..0.8 / +-0.3 | 0.06 / 0.10, 0% |
| Obst_Log_TrunkB | 602/1400 | +-2.36 / -0.06..0.80 / -0.47..0.51 | +-2.22 / 0..0.8 / +-0.3 | 0.06 / 0.10, 0% |
| Obst_Log_TrunkC | 862/1400 | -3.56..3.57 / -0.06..0.80 / -0.47..0.51 | +-3.42 / 0..0.8 / +-0.3 | 0.06 / 0.10, 0% |
| Obst_HangMat | 922/1200 | +-1.08 / 1.05..3.10 / -0.34..0.27 | +-1.02 / 1.1..3.0 / +-0.25 | 0.06 / 0.50, 0.7% |
| Obst_HangMatB | 882/1200 | +-1.08 / 1.06..3.10 / -0.28..0.25 | same | 0.20 / 0.50, 5.3% |
| Obst_HangMatC | 922/1200 | +-1.08 / 1.05..3.29 / -0.33..0.38 | same | 0.07 / 0.50, 4.5% |
| Obst_ButtressFin | 376/1600 | +-1.02 / -0.06..3.93 / -0.50..0.62 | +-1.02 / 0..3.0 / +-0.5 | 0.06 / 0.08, 0% |
| Obst_ButtressFinB | 376/1600 | +-1.02 / -0.06..3.88 / -0.50..0.62 | same | 0.05 / 0.07, 0% |
| Obst_ButtressFinC | 376/1600 | +-1.02 / -0.06..3.88 / -0.50..0.62 | same | 0.06 / 0.07, 0% |
| Obst_StandingStone | 418/800 | +-1.23 / -0.06..3.00 / +-0.79 | same | 0.05 / 0.10, 0% |
| Obst_StandingStoneB | 418/800 | +-1.24 / -0.06..3.00 / +-0.79 | same | 0.05 / 0.10, 0% |
| Obst_WedgedSlab | 188/900 | -1.48..1.86 / -0.06..3.00 / -0.50..0.67 | same | 0.04 / 0.50, 1.9% |
| Obst_BarrelBoulder | 556/900 | +-0.95 / -0.03..1.87 / +-0.80 | +-0.95 / 0..1.9 / +-0.8 | in disc 0.13 / 0.50, 2.5% |
| Obst_BarrelBoulderB | 556/900 | +-0.95 / -0.03..1.87 / +-0.80 | same | in disc 0.13 / 0.50, 2.2% |
| Obst_ThornCage | 1616/1800 | +-1.05 / -0.06..2.27 / -2.24..2.02 | +-1.02 / 0..2.2 / +-2.0 | 0.06 / 0.50, 4.0% |
| Obst_ThornCageB | 1596/1800 | +-1.05 / -0.07..2.27 / -2.18..2.13 | same | 0.33 / 0.50, 5.2% |
| Obst_ThornCageC | 1789/1800 | -1.05..1.01 / -0.06..2.28 / -2.21..1.99 | same | 0.06 / 0.50, 4.7% |

Context and decal pieces (no hitbox; extents checked against the size in the spec): RootPlate 2.8 x 2.24 tall (B 2.3 x 1.56), Stump 1.85 tall,
RockHump 0.72 tall, Limb 5.0 / LimbB 9.6 m (origin = trunk end, +x toward the path, underside at y 4.6), LianaTie 1.6 m (origin = top),
RootWeb (<= 0.35 tall, only beyond |x| 1.5 above 0.12), Wallow 2.37 x 2.14 (rim 0.08), Chock, Furrow 2.4 x 0.8 (ticks every 1 m),
SandBar 2.36 x 2.16 (rim 0.10), CaneWall 2.04 x 0.97 x 2.32 (front panel), CaneWallB 0.94 x 4.0 x 2.32 (side panel, long axis z),
RootCrown 2.47 tall, ThornLitter <= 0.06 tall.

## Known deviations (read before the art review)
- Barrel: a cylinder cannot fill a square face. 30 percent of the 1.9 x 1.9 front face (the four corners outside the silhouette disc) is void by
  geometry; inside the disc (r <= 0.91, minus the 0.04 m lump sliver) the ghost is <= 0.15 for 97.5 percent of samples. Centre y = 0.92 so the top is 1.87 (0.03 embed).
- HangMat: 0.5 ghost samples are gaps between strands near the ragged tips; B is the loosest (5.3 percent above 0.15).
- ThornCage: the 0.5 samples are gaps between canes and the ragged front-top edge; thorns poke up to 0.24 m out of the front plane.
- Log: soil lip (z -0.47) and branch stubs (z +0.51) leave the 0.6 m box slightly; both ends overhang 0.15 m as in the spec. WedgedSlab leans +x 1.5 degrees (top +0.08 m).
- StandingStone: vines +0.065 m over the box, ferns reach about 0.28 m past it at the base.
- Fin top is ragged to y 3.9 (spec 3.0 to 4.0), behind the 1.0 m box depth it also goes to z +0.62.
- Not built (not in the requested list): Obst_Log_Crown, Obst_BoughMass, Obst_PlankRootArch, Obst_TrunkBehind, Obst_DustPuff.
