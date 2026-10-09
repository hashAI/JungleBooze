"""Step 8: put Meshy's auto-rig and animations on the clean mesh and export Pista.fbx for Unity.

Meshy rigged the exact mesh from step 7 (work/pista_upload_meshy.glb), so its skin weights transfer 1:1 by vertex
position. Our mesh keeps its own normals, UVs and material.

  1. Import Meshy's rigged GLB (24-bone humanoid, cm units, armature scale 0.01), apply the scale (bones in meters)
     and scale every location key by the same factor.
  2. Copy skin weights by vertex position (max distance printed; must be ~0).
  3. Ponytail: replace Meshy's weights with a 3-bone chain (Ponytail01-03, parented to Head). Clips don't key
     those bones, so the ponytail follows the head rigidly unless a runtime spring drives the chain in Unity.
  4. Import the animation GLBs (30 fps), rename the clips, make them in place: the Hips' horizontal drift (start to
     end) is removed, the sway and all vertical motion are kept. Each clip's natural ground speed (stance-foot speed)
     is measured and printed, so Unity can scale playback to the game's run speed.
  5. Export UnityProject/Assets/_Game/Art/Characters/Pista/Pista.fbx (mesh + armature + all clips as takes).

Usage: blender -b --python-use-system-env -P step08_rig.py -- <rig.glb> <anim1.glb> [<anim2.glb> ...] [<t2m.glb>=<Name> ...]
       [--review]   (review: keep unmapped clips, write work/pista_08_review.blend only)
With the vertical-slice sources (SRC_* clips) the FBX is written by step09_extra_clips.py instead.
Output: work/pista_08_rigged.blend, the FBX, work/clip_report.json
"""
import json
import os
import re
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

a = C.args()
REVIEW = '--review' in a          # keep unmapped clips under their Meshy names, skip the FBX (for clip review)
a = [x for x in a if x != '--review']
RIG_GLB, ANIM_GLBS = a[0], a[1:]
FPS = 30
UNITY_DIR = os.path.join(C.REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Characters', 'Pista')

# Meshy action name -> game clip name (Meshy library ids in brackets)
CLIP_NAMES = {
    'Idle_6': 'Idle',                                   # [246]
    'Idle': 'Idle_Alt',                                 # [0]
    'run_fast_2': 'Run',                                # [539] matches ART_DIRECTION 7.5 (lean, arms close); 5.0 m/s
    'RunFast': 'Run_Alt',                               # [16] arms fling wide (fails 7.5); 6.8 m/s, for top speeds
    # [509] Lean Forward Sprint and [538] Run Fast 10 were also bought and rejected (jog-like, 3.7 / 2.1 m/s)
    'Jump_Over_Obstacle_2': 'Jump',                     # [467] running hurdle jump: takeoff, air, land
    'Regular_Jump': 'Jump_Standing',                    # [466]
    'Fall2': 'Fall',                                    # [503] loop
    'Dive_Down_and_Land_2': 'Land',                     # [508]
    'slide_right': 'Slide',                             # [517]
    'ForwardLeft_Run_Fight': 'Strafe_Left',             # [9]
    'ForwardRight_Run_Fight': 'Strafe_Right',           # [10]
    'Hit_in_Back_While_Running': 'Stumble',             # [416]
    'Knock_Down_1': 'Death_Backward',                   # [190]
    # [184] Shot and Fall Forward was bought and rejected: it opens with a bullet-hit jerk (ART_DIRECTION 7.4)
    'Jump_and_Hang_on_Bar': 'Vine_Grab',                # [485]
    'Rope_Hang_Idle': 'Vine_Hang',                      # [477]
    'Grab_Bar_and_Swing_Forward': 'Vine_Swing',         # [495]
    # vertical-slice sources (batch C + text-to-motion); step09_extra_clips.py turns them into game clips and
    # removes them before the FBX export
    'Swim_Forward': 'SRC_Swim',                         # [569] breaststroke
    'swimming_to_edge': 'SRC_SwimToEdge',               # [570]
    'T2M_Balance': 'SRC_Balance',                       # text to motion, motion/balance_run.txt
    'T2M_Death': 'SRC_DeathTrip',                       # text to motion, motion/death_trip.txt
    'T2M_Entry': 'SRC_WaterEntry',                      # text to motion, motion/water_entry.txt
    'T2M_Vine': 'SRC_VineRelease',                      # text to motion, motion/vine_release.txt
    'T2M_Wade': 'SRC_Wade',                             # text to motion (swift), motion/water_wade.txt
    # [568] Swim Idle, [366] Falling Down (lands legs-up), [402] Run and Leap, [519] Sliding Stumble,
    # [494] Swing on Rope to Ground (leans back as if flung off): bought, rejected
}
LOOPS = {'Idle', 'Idle_Alt', 'Run', 'Run_Alt', 'Fall', 'Strafe_Left', 'Strafe_Right', 'Vine_Hang'}
# clips whose lowest toe should touch the ground (Meshy's runs hover ~4 cm)
GROUNDED = {'Idle', 'Idle_Alt', 'Run', 'Run_Alt', 'Strafe_Left', 'Strafe_Right', 'Jump', 'Jump_Standing', 'Land',
            'Slide', 'Stumble'}
SEAM_FIX_DEG, SEAM_FRAMES = 20.0, 4

C.open_blend(C.work('pista_07_joined.blend'))
sc = bpy.context.scene
sc.render.fps = FPS
mesh = bpy.data.objects['SK_Pista']

# ------------------------------------------------------------------ 1. rig
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=RIG_GLB)
new = [o for o in bpy.data.objects if o not in before]
arm = next(o for o in new if o.type == 'ARMATURE')
src = next(o for o in new if o.type == 'MESH' and o.vertex_groups)
for o in new:
    if o not in (arm, src):
        bpy.data.objects.remove(o, do_unlink=True)
arm.name = arm.data.name = 'Armature'
S = arm.scale.x
C.set_active(arm)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)   # also applies to the child mesh
print(f'armature scale {S} applied; bones {len(arm.data.bones)}')


def scale_location_keys(act, s):
    for fc in act.fcurves:
        if fc.data_path.endswith('.location'):
            for k in fc.keyframe_points:
                k.co.y *= s; k.handle_left.y *= s; k.handle_right.y *= s


# ------------------------------------------------------------------ 2. weights
smw = src.matrix_world
kd = KDTree(len(src.data.vertices))
for v in src.data.vertices:
    kd.insert(smw @ v.co, v.index)
kd.balance()
names = {g.index: g.name for g in src.vertex_groups}
for n in names.values():
    if n not in mesh.vertex_groups:
        mesh.vertex_groups.new(name=n)
worst = 0.0
mmw = mesh.matrix_world
for v in mesh.data.vertices:
    co, idx, d = kd.find(mmw @ v.co)
    worst = max(worst, d)
    for g in src.data.vertices[idx].groups:
        if g.weight > 0:
            mesh.vertex_groups[names[g.group]].add([v.index], g.weight, 'REPLACE')
print(f'weights copied; worst vertex match distance {worst * 1000:.3f} mm')
assert worst < 1e-3, 'mesh does not match the rigged mesh'
bpy.data.objects.remove(src, do_unlink=True)

# ------------------------------------------------------------------ 3. ponytail chain
pm = mesh.vertex_groups['ponytail_mask'].index
pv = [v for v in mesh.data.vertices if any(g.group == pm for g in v.groups)]
P = [mmw @ v.co for v in pv]
# the ponytail hangs back and down from the crown: order points by height
ztop, zbot = max(p.z for p in P), min(p.z for p in P)


def centroid(z0, z1):
    sel = [p for p in P if z0 <= p.z <= z1]
    return sum(sel, Vector()) / len(sel)


H = ztop - zbot
joints = [centroid(ztop - 0.12 * H, ztop)] + [centroid(zbot + H * (1 - f) - 0.08 * H, zbot + H * (1 - f) + 0.08 * H)
                                               for f in (0.35, 0.68)] + [centroid(zbot, zbot + 0.1 * H)]
C.set_active(arm)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
parent = eb['Head']
chain = []
for i in range(3):
    b = eb.new(f'Ponytail0{i + 1}')
    b.head, b.tail = joints[i], joints[i + 1]
    b.parent = parent
    b.use_connect = i > 0
    b.roll = 0
    parent = b
    chain.append(b.name)
bpy.ops.object.mode_set(mode='OBJECT')
for n in chain:
    mesh.vertex_groups.new(name=n)
others = [g for g in mesh.vertex_groups if g.name not in chain and g.name != 'ponytail_mask']
seg = [(joints[i], joints[i + 1]) for i in range(3)]


def along(p):
    """0 at the root joint, 3 at the tip, by projection onto the chain."""
    best = (1e9, 0.0)
    for i, (h, t) in enumerate(seg):
        d = t - h
        u = max(0.0, min(1.0, (p - h).dot(d) / d.length_squared))
        dist = (h + d * u - p).length
        if dist < best[0]:
            best = (dist, i + u)
    return best[1]


for v, p in zip(pv, P):
    for g in others:
        g.remove([v.index])
    s = along(p)
    w = {'Head': max(0.0, 1 - s / 0.35)}          # root stays on the head (hair tie)
    # linear blend between neighbouring bones along the chain
    for i in range(3):
        c = i + 0.5
        w[chain[i]] = max(0.0, 1 - abs(s - c))
    tot = sum(w.values())
    for n, x in w.items():
        if x > 1e-4:
            mesh.vertex_groups[n].add([v.index], x / tot, 'REPLACE')
mesh.vertex_groups.remove(mesh.vertex_groups['ponytail_mask'])
print(f'ponytail: {len(pv)} verts on Head + {chain}; joints z ' + ', '.join(f'{j.z:.3f}' for j in joints))

# parent mesh to the armature with an armature modifier
for m in list(mesh.modifiers):
    mesh.modifiers.remove(m)
mesh.parent = arm
mesh.matrix_parent_inverse = arm.matrix_world.inverted()
md = mesh.modifiers.new('Armature', 'ARMATURE')
md.object = arm

# max influences per vertex for Unity (4): keep the strongest 4, renormalize
cut = 0
for v in mesh.data.vertices:
    gs = sorted([(g.weight, g.group) for g in v.groups if g.weight > 0], reverse=True)
    if len(gs) > 4:
        cut += 1
        for w_, gi in gs[4:]:
            mesh.vertex_groups[gi].remove([v.index])
        tot = sum(w_ for w_, _ in gs[:4])
        for w_, gi in gs[:4]:
            mesh.vertex_groups[gi].add([v.index], w_ / tot, 'REPLACE')
print(f'verts limited to 4 influences: {cut}')

# ------------------------------------------------------------------ 4. clips
for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)
for spec in ANIM_GLBS:
    # '<file.glb>=<Name>': single text-to-motion clip; Meshy names it 'retarget_clip' plus a 1-frame base layer stub
    path, _, rename = spec.partition('=')
    before, acts_before = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=path)
    for o in [o for o in bpy.data.objects if o not in before]:
        bpy.data.objects.remove(o, do_unlink=True)
    for act in [x for x in bpy.data.actions if x not in acts_before]:
        if rename and 'retarget_clip' in act.name:
            act.name = rename + '_Armature'
        elif rename:
            bpy.data.actions.remove(act)
for d in (bpy.data.armatures, bpy.data.meshes):
    for x in list(d):
        if x.users == 0:
            d.remove(x)

arm.animation_data_create()
hips = arm.pose.bones['Hips']
rest = arm.data.bones['Hips'].matrix_local.to_3x3()
report = {}
for act in list(bpy.data.actions):
    base = re.sub(r'_Armature(\.\d+)?$', '', act.name)   # glTF importer names actions <clip>_<armature object>
    if base not in CLIP_NAMES and not REVIEW:
        print('unmapped action, removed:', act.name)
        bpy.data.actions.remove(act)
        continue
    name = CLIP_NAMES.get(base, base)
    act.name = name
    act.use_fake_user = True
    scale_location_keys(act, S)
    arm.animation_data.action = act
    f0, f1 = (int(round(x)) for x in act.frame_range)

    # horizontal Hips drift (armature space), measured on the evaluated pose
    def hips_pos(f):
        sc.frame_set(f)
        return arm.matrix_world @ hips.head

    def foot(f, side):
        sc.frame_set(f)
        return arm.matrix_world @ arm.pose.bones[f'{side}Foot'].head

    p0, p1 = hips_pos(f0), hips_pos(f1)
    drift = Vector((p1.x - p0.x, p1.y - p0.y, 0))
    secs = (f1 - f0) / FPS
    # stance-foot speed relative to the hips = ground speed with no sliding
    speeds = []
    for side in ('Left', 'Right'):
        fr = [(f, foot(f, side), hips_pos(f)) for f in range(f0, f1 + 1)]
        zmin = min(x[1].z for x in fr)
        for (fa, pa, ha), (fb, pb, hb) in zip(fr, fr[1:]):
            if pa.z < zmin + 0.03 and pb.z < zmin + 0.03:
                rel = (pb - hb) - (pa - ha)
                speeds.append(rel.y * FPS)         # character faces -Y: stance foot moves +Y relative to hips
    ground = sorted(speeds)[len(speeds) // 2] if speeds else 0.0

    # remove the drift: rewrite Hips location keys (bone-local) per frame
    fcs = {i: act.fcurves.find('pose.bones["Hips"].location', index=i) for i in range(3)}
    if all(fcs.values()) and drift.length > 1e-4:
        inv = rest.inverted()
        frames = sorted({k.co.x for i in range(3) for k in fcs[i].keyframe_points})
        new = []
        for fr_ in frames:
            loc = Vector([fcs[i].evaluate(fr_) for i in range(3)])
            t = (fr_ - f0) / max(1e-6, (f1 - f0))
            new.append((fr_, loc - inv @ (drift * t)))
        for i in range(3):
            fc = fcs[i]
            fc.keyframe_points.clear()
            fc.keyframe_points.add(len(new))
            for k, (fr_, loc) in zip(fc.keyframe_points, new):
                k.co = (fr_, loc[i]); k.interpolation = 'LINEAR'
            fc.update()
    # loop seam: largest bone rotation difference (degrees) between the first and last frame
    def pose_q(f):
        sc.frame_set(f)
        return {b.name: b.matrix.to_quaternion() for b in arm.pose.bones}
    qa, qb = pose_q(f0), pose_q(f1)
    seam = max(min(a_, 6.28319 - a_) for a_ in (qa[n].rotation_difference(qb[n]).angle for n in qa)) * 57.2958
    seam_fixed = False
    if name in LOOPS and seam > SEAM_FIX_DEG:
        # cross-fade the last SEAM_FRAMES frames toward frame 0 (all channels), so the loop closes
        for fc in act.fcurves:
            v0 = fc.evaluate(f0)
            for k in fc.keyframe_points:
                if k.co.x > f1 - SEAM_FRAMES:
                    w = ((k.co.x - (f1 - SEAM_FRAMES)) / SEAM_FRAMES) ** 2
                    k.co.y = k.co.y * (1 - w) + v0 * w
                    k.interpolation = 'LINEAR'
            fc.update()
        qa, qb = pose_q(f0), pose_q(f1)
        seam = max(min(a_, 6.28319 - a_) for a_ in (qa[n].rotation_difference(qb[n]).angle for n in qa)) * 57.2958
        seam_fixed = True
    ground_offset = 0.0
    if name in GROUNDED:
        rest_toe = min((arm.matrix_world @ arm.data.bones[f'{sd}ToeBase'].head_local).z for sd in ('Left', 'Right'))
        low = 1e9
        for f in range(f0, f1 + 1):
            sc.frame_set(f)
            low = min(low, *((arm.matrix_world @ arm.pose.bones[f'{sd}ToeBase'].head).z for sd in ('Left', 'Right')))
        ground_offset = low - rest_toe
        if abs(ground_offset) > 0.005 and all(fcs.values()):
            d = rest.inverted() @ Vector((0, 0, -ground_offset))
            for i in range(3):
                for k in fcs[i].keyframe_points:
                    k.co.y += d[i]; k.handle_left.y += d[i]; k.handle_right.y += d[i]
                fcs[i].update()
    report[name] = dict(ground_offset_removed_m=round(ground_offset, 3), seam_crossfaded=seam_fixed,
                        loop_seam_deg=round(seam, 1), meshy_action=base, frames=[f0, f1], seconds=round(secs, 3), loop=name in LOOPS,
                        removed_drift_m=round(drift.length, 3),
                        drift_speed_mps=round(drift.length / secs, 2) if secs else 0,
                        stance_foot_speed_mps=round(ground, 2))
    print(f'{name:16s} {base:28s} {f1 - f0:4d}f  drift {drift.length:5.2f} m  stance foot {ground:5.2f} m/s  seam {seam:5.1f} deg  ground {ground_offset*100:+.1f} cm')

arm.animation_data.action = bpy.data.actions.get('Idle')
sc.frame_set(0)
if REVIEW:
    C.save_blend(C.work('pista_08_review.blend'))
    sys.exit(0)
with open(C.work('clip_report.json'), 'w') as f:
    json.dump(report, f, indent=1)
C.save_blend(C.work('pista_08_rigged.blend'))

# ------------------------------------------------------------------ 5. FBX for Unity
if any(a_.name.startswith('SRC_') for a_ in bpy.data.actions):
    print('SRC_ clips present: the FBX is exported by step09_extra_clips.py')
    sys.exit(0)
os.makedirs(UNITY_DIR, exist_ok=True)
for o in bpy.context.view_layer.objects:
    o.select_set(o in (arm, mesh))
bpy.context.view_layer.objects.active = arm
out = os.path.join(UNITY_DIR, 'Pista.fbx')
bpy.ops.export_scene.fbx(
    filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=False,
    mesh_smooth_type='FACE', use_tspace=False, use_mesh_modifiers=False,
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_use_all_bones=True,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    path_mode='STRIP', embed_textures=False)
print('exported', out, os.path.getsize(out))
