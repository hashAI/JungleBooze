# Pista: realistic 3D hero (AURELIA)

Design: take B "Rugged expedition", simplified (no rope coil, no bedroll, map patch with an X on the backpack), age 16,
torso covered (`design/DECISIONS.md`, `design/aurelia/ART_DIRECTION.md` section 7).

## Source
- `meshy/pista.glb` (28.9k tris, 4k PBR) and `meshy/pista_pre_remeshed.glb`: Meshy multi-image-to-3D from our take B
  turnaround (`pistaB_*.png`), task in `meshy/task.json` (30 credits, 2026-10-08).
- `meshy/rig/pista_rigged.glb`: Meshy auto-rig of our clean mesh (5 credits, 2026-10-09).
- `meshy/anim/pista_anim_A.glb`, `pista_anim_B.glb`: Meshy library animations on that rig (57 credits).
- `meshy/calls.jsonl`: every paid Meshy call with credits and balance. Licenses: `docs/LICENSES.md`.

## Pipeline (headless Blender 4.2 LTS, `tools/blender/pista/`)
Setup once: Blender 4.2 at `~/Applications/Blender42.app`, then
`~/Applications/Blender42.app/Contents/Resources/4.2/python/bin/python3.11 -m pip install --target "$HOME/Library/Application Support/Blender/4.2/scripts/modules" xatlas scipy Pillow`.
Run every step as
`PYTHONPATH="$HOME/Library/Application Support/Blender/4.2/scripts/modules" ~/Applications/Blender42.app/Contents/MacOS/Blender -b --factory-startup --python-use-system-env -P tools/blender/pista/<step>.py [-- args]`
(Blender ignores PYTHONPATH without `--python-use-system-env`). Intermediates go to `work/` (git-ignored).

| Step | Script | What it does |
|---|---|---|
| 1 | `step01_prepare.py` | import, weld, scale to 1.65 m, feet at the origin, face Unity +Z |
| 2 | `step02_remove_rope.py` | delete the hip rope coil, fill the holes (fan fill for loops `holes_fill` skips) |
| 3 | `step03_ponytail.py` | split the ponytail into its own part |
| 4 | `step04_budget.py` | decimate to 19.9k tris (face and hands protected) |
| 5 | `step05_uv.py` | clear custom normals, new xatlas UV layout (face at 1.5x density) |
| – | `paint_map_patch.py work 1024` | paint the map-patch canvas with the sepia X (run with Blender's python3.11) |
| 6 | `step06_bake.py` | bake 4k from the Meshy surface, rope/seam clone, roughness zones, then 2k + 1k maps |
| 7 | `step07_export.py` | join into one mesh/material, validate, `clean/pista_clean.glb` + Meshy upload GLB |
| – | `tools/assetgen/meshy.py rig/animate/fetch` | Meshy rig + clips (key from `~/.config/junglebooze/secrets.env`) |
| 8 | `step08_rig.py -- <rig.glb> <anim A.glb> <anim B.glb>` | weights onto our mesh, ponytail chain, in-place/grounded/looped clips, `Pista.fbx` |
| – | `render_previews.py -- <file> <out_prefix> [clip frames]` | turnaround sheet or 8-frame clip contact sheet |

## Result and budgets
| | Value | Budget |
|---|---|---|
| Triangles | 19,890 (one skinned mesh) | 20k `[ASSUMED]` (see below) |
| Materials / draw calls | 1 | 1 |
| Textures | 2048² BaseColor, Normal (OpenGL), ARM (AO/rough/metal); URP copies in Unity | 2048 (ADR 0004) |
| Bones | 27 (24 Meshy humanoid + 3 ponytail) | ≤ 40 |
| Skin influences | ≤ 4 per vertex | 4 |
| Texel density | ~900 px/m mean, face 1.5x (UV coverage 0.46) | ~1200 px/m target (ART_DIRECTION 10.2): **missed** |
| Height / pivot | 1.65 m, feet at the origin, faces +Z | |

Budget note: `docs/ARCHITECTURE.md` 10.2 says 15k tris / 1024² for the hero, but that table is the stylized plan.
ADR 0004 replaced the texture size (2048) and has no triangle number for the hero, so 20k is used `[ASSUMED]`.

Previews: `previews/pista_3d_v2_sheet.jpg` (+ `_front/_34/_side/_back/_face/_backpack.png`) and
`previews/pista_run_contact.jpg`. Clip table and Unity settings: `UnityProject/Assets/_Game/Art/Characters/Pista/IMPORT_NOTES.md`.

## Self-review (ART_DIRECTION 10.3), 2026-10-09
- A1 pass: high ponytail, white/orange/charcoal, torso covered, harness, belt pouches, fingerless gloves, boots,
  backpack with the map patch and X. Rope coil removed.
- A2: the face is original as far as this reviewer can tell. It still needs the owner's resemblance check (A2 asks for one).
- A3 pass: no text or logos.
- B1 pass (1.65 m). B2 pass: skin roughness 0.51 median, hair 0.68, delight check slope 0.02 (no baked lighting),
  albedo 35–217 sRGB.
- B4: fixed in this pass were faceted face highlights (custom normals), crack-like lines on the face/neck (normal map
  faded 85% on skin), a hole under the belt (fan fill), and 9 duplicate faces (validate).
- E1/E2 pass: see the budgets above. Texel density misses the target. E4 pass.

## Known issues
- Texel density ~900 px/m against a 1200 target: Meshy's noisy remesh caps xatlas at ~1600 charts (coverage 0.46).
  Fine at the gameplay camera. A manual retopo/UV (or Meshy's quad remesh) would be needed for menu close-ups.
- The hair is a solid sculpted mesh (no hair cards). It is slightly glossy and has a jagged silhouette at the crown, and
  the back of the ponytail is stringy (as in Meshy's own render). A few loose strands hang at the temples and one
  crosses the neck.
- A short dark slash remains under the belt where the rope was. It reads as a pocket opening.
- No finger bones: hands keep their sculpted pose in every clip.
- The ponytail swings only if a spring component drives `Ponytail01-03`. Without one it follows the head rigidly.
- `Run` matches the ground at 5.0 m/s. At the game's 8–10 m/s, see the speed note in IMPORT_NOTES. A real sprint clip
  (Mixamo with the owner's Adobe login, or the planned phone-video mocap, ART_DIRECTION 7.5) would match better.
