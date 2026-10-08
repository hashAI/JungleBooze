# Meshy image-to-3D requests (P0-D and look-test props)

**Owner:** art-director (inputs and review) · asset-pipeline (runs and cleanup) | **Status:** Ready to run, untested |
**Last updated:** 2026-10-08

API parameters below were checked against the Meshy API docs on 2026-10-08 (`POST /openapi/v1/image-to-3d`,
`POST /openapi/v1/multi-image-to-3d`, `POST /openapi/v1/rigging`, base URL `https://api.meshy.ai`). Re-check before
running; Meshy changes model names often (`latest` = Meshy 7.1 at the time of writing; the `lowpoly` model type
retires 2026-10-30, so it is not used here).

**License:** Meshy's docs say Free-plan outputs are CC BY 4.0 (commercial use with attribution) and paid-plan outputs
are owned by the user with commercial rights. Confirm which plan the owner's key is on before generating anything we
ship, and record plan + license in `docs/LICENSES.md` per asset.

## Common rules
- Inputs are the 3D input views from `pista.md` / `duko.md` / `environment.md`: plain gray background, flat light,
  the whole subject inside the frame. Upload as base64 data URIs or public URLs.
- Always `enable_pbr: true` (base color, metallic, roughness, normal). Generate at `texture_resolution: "4k"` and bake
  down to the target size in Blender, which gives cleaner mips than a 2k generation.
- Keep `should_remesh: true` with a triangle target slightly above the budget, plus `save_pre_remeshed_model: true`
  so the dense mesh is available for normal baking.
- `moderation: false` (default). Formats: `["glb", "fbx"]`.
- Every result goes through the style-lock checklist (ART_DIRECTION 10.3), sections A, B and E, before import.

## A-01 Pista (multi-image-to-3D)
```json
POST /openapi/v1/multi-image-to-3d
{
  "image_urls": ["<pista front>", "<pista side>", "<pista back>"],
  "ai_model": "latest",
  "geometry_resolution": "standard",
  "pose_mode": "a-pose",
  "should_texture": true,
  "enable_pbr": true,
  "texture_resolution": "4k",
  "should_remesh": true,
  "topology": "triangle",
  "target_polycount": 28000,
  "save_pre_remeshed_model": true,
  "target_formats": ["glb", "fbx"]
}
```
If the face or hands are mushy, rerun once with `"geometry_resolution": "2k"` and compare.

Cleanup (Blender, asset-pipeline):
1. Scale to **1.50 m** tall (age ~12 `[ASSUMED]`), feet on the origin, facing +Z for rigging.
2. Retopology pass where the remesh failed (shoulders, elbows, knees, hips need loops for deformation). Final
   ≤ 25k triangles: body+cloth material and hair material.
3. Hair: keep a closed sculpted mass with curl-clump normal detail + a thin alpha card shell on the silhouette edge
   (≤ 2k tris). Bind rigidly to the head with 2–4 jiggle bones for bounce.
4. Re-bake textures to 2048² (body) and 1024² (hair) from the pre-remesh mesh; repaint the X, sash and map lines where
   the generation blurred them (these are identity marks and must be crisp).
5. Delight check: no baked shadows in base color (ART_DIRECTION 10.2).

Rigging:
```json
POST /openapi/v1/rigging
{ "model_url": "<cleaned, textured pista.glb, face toward +Z>", "height_meters": 1.50 }
```
Meshy rigs humanoids only. Use its skeleton if it maps cleanly to Unity Humanoid; otherwise rig manually in Blender
(Rigify or a game rig). Add secondary bones: hair (2–4), satchel (1), sash knot (1). Total ≤ 60 bones.
Animation source: phone-video AI motion capture retargeted to this rig (ART_DIRECTION 7.5).

## A-02 Duko (multi-image-to-3D, wings spread)
```json
POST /openapi/v1/multi-image-to-3d
{
  "image_urls": ["<duko front wings spread>", "<duko top>", "<duko side>"],
  "ai_model": "latest",
  "pose_mode": "",
  "should_texture": true,
  "enable_pbr": true,
  "texture_resolution": "4k",
  "should_remesh": true,
  "topology": "triangle",
  "target_polycount": 12000,
  "save_pre_remeshed_model": true,
  "target_formats": ["glb", "fbx"]
}
```
Cleanup: scale to 0.85 m beak to tail tip; final ≤ 10k triangles, one material, 1024² set; tail-tip and wing-tip
feathers as a few alpha cards; manual bird rig in Blender (spine, neck ×2, head, beak, wings ×3 per side, tail ×3,
legs ×2 per side, feet), ≤ 40 bones. Check the teal stays on the tail tips only.

## Look-test props (single image-to-3D)
One request per concept image from `environment.md`. Template:
```json
POST /openapi/v1/image-to-3d
{
  "image_url": "<concept image>",
  "ai_model": "latest",
  "should_texture": true,
  "enable_pbr": true,
  "texture_resolution": "2k",
  "should_remesh": true,
  "topology": "triangle",
  "target_polycount": <from table>,
  "save_pre_remeshed_model": true,
  "target_formats": ["glb", "fbx"]
}
```
| ID | Item | `target_polycount` | After generation |
|---|---|---|---|
| A-03 | Stiltwood trunk base (2 variants) | 10000 | Retexture with tiling bark T-07 + moss mask; canopy is cards |
| A-04 | Rootstone arch with waterfall mouth | 12000 | Triplanar rock T-06 + braided detail normal; separate mouth area for the flow mesh |
| A-04 | Rootstone pillar / outcrop (3) | 5000 / 2500 | Same material as the arch |
| A-05 | Bellcap clump (2) | 2500 | Bell petals keep generated texture; stalks retextured |
| A-06 | Sailback | 2000 | Membrane as separate double-sided part; manual 8-bone rig |
| A-07 | Whirlseed | 300 | Or hand-model: a curved plane + seed body |
| A-08 | Thorn bramble hazard | 2500 | Crimson tips `#9E2238` only on thorn tips |

## Test log
| Date | ID | Endpoint, model | Credits used | Result |
|---|---|---|---|---|
| — | — | — | — | Not run yet |
