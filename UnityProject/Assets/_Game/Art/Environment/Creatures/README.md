# Creatures: the sailback (spec 103 s7)

Owner: asset-pipeline. Original design (ART_DIRECTION s5.2 #3, checklist C4): a small glider lizard, four legs, two
eyes, rib-supported side membranes amber at the body to teal at the scalloped edge, bronze-olive matte skin, cream
belly, low dorsal frill. No feathers, no wings on the arms, no glow, not rideable (not a winged mount).

Pipeline: OpenAI concept (`art_source/environment/openai/forest/f_sailback_c1.png`) -> Meshy image-to-3D (latest,
textured, 30 credits) -> `tools/blender/environment/forest_sailback.py` (scale, weld + decimate keeping Meshy's UVs,
512 texture, procedural rig + clips). Pose sheet: `art_source/environment/previews/forest/CR_Sailback_poses.jpg`.

| Asset | Size (m) | Tris LOD0 / 1 / 2 | Texture | Rig / clips |
|---|---|---|---|---|
| `CR_Sailback.fbx` | sail span 0.80, length 1.13 (with tail), 0.16 thick | 1,500 / 600 / 249 | `Textures/CR_Sailback_BaseColor.png` 512 (no normal map, no ARM: roughness ~0.6 constant) | 12 bones (`root`, `chest`, `head`, `tail_01..03`, `sail_L/R`, `leg_FL/FR/BL/BR`); clips **Glide** (48 f loop @24 fps), **Flap** (16 f loop), **Perch** (48 f loop, sails folded, legs down), **Alert** (12 f, sail flare from Perch, play once then Launch) |

- Pivot at the body centre; head toward Unity +Z. One skinned mesh per LOD (all on the same armature).
- **Vertex colour R = sail membrane mask** (1 on the sails, 0 on the body): use it for back-lit translucency
  (PAINTERLY s5 `#C9D040`-style transmission, here amber/teal) so the sails glow when the sun is behind them.
- Spec mapping: Perched -> `Perch`, Alert -> `Alert`, Launch -> blend `Alert` -> `Flap` (2-3 cycles), Glide ->
  `Glide` with an occasional `Flap` (1-2 lazy flutters per crossing, LOOK_TEST_BRIEF), Gone -> `Perch` at the landing
  point. Never below path Y 2.5 m (spec).
- Hitbox: none (passive). Budget 1.5k tris (ENVIRONMENT_STRATEGY item 8).

Known gaps: Launch/Land transitions are blends of the clips above, not authored; weights are procedural (regions with
soft blends): the sail roots crease a little at full flap; fine at gameplay distance (>= 8 m).
