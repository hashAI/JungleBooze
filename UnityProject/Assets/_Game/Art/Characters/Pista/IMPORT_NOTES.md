# Pista: Unity import notes

Source pipeline and review: `art_source/pista/README.md`. The coordinator verifies the import in Unity. These files
have not been opened in Unity by the asset pipeline yet.

## Files
| File | Content | Import settings |
|---|---|---|
| `Pista.fbx` | `SK_Pista` skinned mesh (19,890 tris, 1 material `M_Pista`) + `Armature` (27 bones) + 16 clips | see below |
| `T_Pista_BaseColor.png` | 2048², sRGB albedo | sRGB on, ASTC 4x4 (ADR 0004), max 2048, mips on |
| `T_Pista_Normal.png` | 2048², tangent space, OpenGL +Y, MikkTSpace | Texture Type Normal map, ASTC 5x5 |
| `T_Pista_MetallicSmoothness.png` | R = metallic, A = smoothness (1 - roughness) | sRGB **off**, ASTC 6x6 |
| `T_Pista_Occlusion.png` | AO in G (grayscale) | sRGB **off**, ASTC 8x8 |

Material: URP/Lit `M_Pista` (extract from the FBX or create): Base Map = BaseColor, Metallic Map = MetallicSmoothness
(Smoothness source: Metallic Alpha, smoothness slider 1.0), Normal Map = Normal (scale 1), Occlusion = Occlusion.

## Model tab
- Scale Factor 1, **Convert Units on** (FBX written in meters, 1 unit = 1 m). Height 1.65 m, feet at the origin,
  faces +Z. Check `Bake Axis Conversion` is not needed: the root should import with no rotation.
- Read/Write off, Mesh Compression Low, Optimize Mesh on, Normals: **Import**, Tangents: Calculate Mikktspace.
- Blend shapes: none. Weights: max 4 per vertex (already limited).

## Rig tab
- Animation Type **Humanoid**, Avatar Definition: Create From This Model.
- Bone names follow Meshy's rig (Mixamo-like). Expected mapping: Hips = `Hips`, Spine = `Spine02`, Chest = `Spine01`,
  UpperChest = `Spine` (Meshy numbers the spine top-down), Neck = `neck`, Head = `Head`, shoulders/arms/legs/feet/toes
  by name (`LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand`, `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase`).
  No finger bones, no jaw, no eyes. `head_end`, `headfront`, `Ponytail01-03` stay unmapped (extra transforms).
- The rest pose is an **A-pose**: in Configure Avatar use Pose > Enforce T-Pose before applying.
- `Ponytail01-03` (children of `Head`) carry the ponytail. No clip keys them, so it follows the head rigidly. A small
  spring/verlet component on that chain can add swing later. Keep the bones in the avatar's skeleton
  (Optimize Game Objects: expose `Ponytail01` if the spring script needs it).

## Animation tab
- Take names come in as `Armature|<Clip>` (Blender FBX exporter). Rename to the clip name in the clip list.
- **Root motion off** (Apply Root Motion off on the Animator). The clips are already **in place**: forward/sideways
  drift of the hips was removed in Blender (`removed_drift_m` below), and the sway and all vertical motion were kept.
  For every clip: Root Transform Rotation Bake Into Pose on, Root Transform Position (Y) Bake Into Pose on, based upon
  Original; Root Transform Position (XZ) Bake Into Pose on.
- Anim. Compression: Optimal; rotation/position error 0.5 / 0.5 (default). Clips are 30 fps.

| Clip | Frames (30 fps) | Loop | Notes |
|---|---|---|---|
| Idle | 221 | yes | relaxed standing idle (Meshy library #246) |
| Idle_Alt | 120 | yes | alternative idle (#0) |
| Run | 16 | yes | main run (#539). Natural no-slide ground speed 5.0 m/s. Loop seam cross-faded over the last 4 frames |
| Run_Alt | 14 | yes | harder, wide-arm sprint (#16), no-slide speed 6.8 m/s |
| Strafe_Left / Strafe_Right | 18 | yes | diagonal runs for lane changes (#9 / #10), ~4 m/s |
| Jump | 26 | no | running hurdle jump: takeoff, tuck, land (#467). The hips rise in the clip, so the controller's jump arc should account for it |
| Jump_Standing | 55 | no | standing jump (#466) |
| Fall | 20 | yes | airborne fall loop (#503) |
| Land | 92 | no | crouch landing then stand (#508). For a running landing, use frames 0–~20 and blend back to Run |
| Slide | 51 | no | feet-first slide on the hip, back up at the end (#517) |
| Stumble | 75 | no | hit/stumble forward while running and recover (#416) |
| Death_Backward | 98 | no | knocked back onto the ground (#190). Pair it with a quick fade (ART_DIRECTION 7.4) |
| Vine_Grab | 50 | no | jump and grab overhead (#485) |
| Vine_Hang | 77 | yes | hanging idle (#477) |
| Vine_Swing | 74 | no | grab and swing forward, then drop (#495) |

**Run speed vs. the game's 8–10 m/s:** Run's stride matches 5.0 m/s at 1.0x. Playing it at 1.6–2.0x to match the
ground would put the cadence at 6–7.5 steps/s, which looks frantic. Recommended start: Run speed multiplier **1.3**
(4.9 steps/s, ~6.5 m/s stride). The remaining mismatch is along the camera axis, so it is hard to see from the chase
camera. Run_Alt at 1.3x matches ~8.8 m/s if the faster look is preferred. Tune it on device.
