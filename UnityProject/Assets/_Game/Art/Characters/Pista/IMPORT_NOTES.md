# Pista: Unity import notes

Source pipeline and review: `art_source/pista/README.md`. The coordinator verifies the import in Unity. These files
have not been opened in Unity by the asset pipeline yet.

## Files
| File | Content | Import settings |
|---|---|---|
| `Pista.fbx` | `SK_Pista` skinned mesh (19,890 tris, 1 material `M_Pista`) + `Armature` (27 bones) + 27 clips (16 base + 11 vertical-slice, below) | see below |
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

### Vertical-slice clips (spec 103), added 2026-10-09
Built by `tools/blender/pista/step09_extra_clips.py` (report: `art_source/pista/work/clip_report_09.json`). Contact sheets:
`art_source/pista/previews/pista_<clip>_contact.jpg` (swim clips are rendered with a water plane at the root).
**Swim convention:** for swim clips the root (y 0) is the **water surface line**; the Hips sit 5 cm under it, so the head
and shoulders ride on the surface. Put the model root at the surface height while `Swimming`.
`Keep Height` = keep the clip's vertical body motion (Root Y baked into pose). "off" = the simulation owns height (as for
Jump): import like Jump/Fall in `PistaImportSetup.Clips`.

| Clip | Frames (30 fps) | Loop | Keep Height | Notes |
|---|---|---|---|---|
| Swim_Surface | 64 | yes | yes | surface **breaststroke** (Meshy #569; the library has no crawl). One stroke = 2.1 s at 1.0x, so play it ~1.6–2.0x at swim speed. Wide arm sweep and frog kick read from behind |
| Swim_Dive | 26 | no | off | duck-dive, streamlined glide with dolphin kicks, surface (authored from the Swim_Surface glide). 26 f = 0.87 s ≈ the 51-tick dive; scale with StateRate if the timing changes |
| Swim_Underwater | 24 | yes | off | streamlined glide loop with a steady dolphin kick, same pose as Swim_Dive's middle (for longer Under phases / Deep Breath) |
| Swim_Leap | 24 | no | off | dolphin leap: kick, nose up out of the water, flat apex, head-first entry (pitch only; the arc is the simulation's 1.20 m). Frames 6–20 ≈ the 0.55 s airtime. Blend to Swim_Surface or Swim_Dive on splash-down. Body is long and straight, not a tuck (hitbox in spec 103 §4.3 assumes a tuck) |
| Water_Wade | 22 | yes | yes | high-knee run through shallow water (text to motion). Stance-foot speed 0.8 m/s at 1.0x: play ≥2x, the slide is hidden by the water |
| Water_Entry | 32 | no | yes | last running step, racing dive, flat glide, ends on Swim_Surface frame 0. Starts with feet on the ground, ends at the swim convention. Optional (not in the coordinator's list) |
| Water_Exit | 70 | no | yes | last stroke, legs swing down, rises upright and steps into Run frame 0. Starts at the swim convention, ends on the ground. Optional |
| Balance_Run | 36 | yes | yes | narrow-beam run, arms held out wide, feet on one line (text to motion). Stance-foot speed 1.76 m/s at 1.0x: a light jog, play ~2–2.5x on the canopy |
| Vine_Release | 12 | no | off | from Vine_Swing's forward swing (leaning back, legs forward), lets go and rotates forward into Jump's airborne tuck with a reach; ends in the air: blend to Fall/Land. Authored from Vine_Swing 37–40 + Jump 8–16 |
| Death_Stumble | 93 | no | yes | **replaces Death_Backward**: running stride, trips, catches herself on hands and knees, slides down face-first and lies still. No pain pose, no legs-up (ART_DIRECTION 7.4). Pair with the quick fade |
| Slide_Clean | 51 | no | yes | **replaces Slide**: same slide with the raised arm removed (that arm goes from the run swing straight to the trailing hand) |

Old `Death_Backward` and `Slide` stay in the FBX unchanged, so nothing breaks until the controllers switch. All new
one-shots are in place frame by frame (horizontal body path removed), loops keep their sway with the seam cross-faded
(0.0 deg). The base 16 clips were regenerated by the same step 8 and are unchanged.

**Run speed vs. the game's 8–10 m/s:** Run's stride matches 5.0 m/s at 1.0x. Playing it at 1.6–2.0x to match the
ground would put the cadence at 6–7.5 steps/s, which looks frantic. Recommended start: Run speed multiplier **1.3**
(4.9 steps/s, ~6.5 m/s stride). The remaining mismatch is along the camera axis, so it is hard to see from the chase
camera. Run_Alt at 1.3x matches ~8.8 m/s if the faster look is preferred. Tune it on device.

## Unity import, verified 2026-10-09 (gameplay-engineer)
Applied by `JungleBooze > Characters > Configure Pista Import` / `Build Pista Prefab` (`Assets/_Game/Editor/Characters/`):
- **Humanoid** avatar is valid. The bone map is set explicitly (Meshy spine order) and the reference pose is a computed
  T-pose (arms out, legs straight), the scripted equivalent of Enforce T-Pose. Optimize Game Objects stays **off**:
  the procedural lean and the ponytail spring write bone transforms after the Animator.
- Clips renamed (no `Armature|`). Loops keep their in-place sway; one-shots (Jump, Slide, Stumble, Land, Death) drop
  horizontal body drift (Root XZ not baked, root motion off), Jump/Fall also drop vertical drift (the simulation's arc
  owns height). Extra sub-clip `Land_Run` = Land frames 0–20 (used for hard landings).
- `M_Pista` (URP/Lit) is remapped onto the FBX: Base, Normal, MetallicSmoothness (smoothness from metallic alpha),
  Occlusion (G). Data maps imported with sRGB off; iOS ASTC 4x4 / 5x5 / 6x6 / 8x8, max 2048.
- Runtime prefab: `Assets/_Game/Prefabs/Characters/Pista.prefab` (`AnimatedRunnerAvatar` + `Pista.controller`).
  Tuning: `Assets/_Game/Config/Movement/RunnerAnimationConfig.asset`.
- Run vs Run_Alt: Run is used at 10–16 m/s with a contact-phase time warp (planted foot measured at 0.2–0.5 m/s world
  speed, 5.6–6.4 steps/s, `PistaSkateProbe`). Run_Alt is a wide-armed, deeply pitched sprint that doesn't match
  ART_DIRECTION 7.5; it can be mixed in with `SprintBlendMax`.
