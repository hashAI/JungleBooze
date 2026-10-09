"""Step 9: vertical-slice clips (spec 103): swim, dive, leap, water entry/exit, balance run, vine release, a stumble
death and a clean slide. Then export Pista.fbx with every clip.

Input: work/pista_08_rigged.blend from step 8 run with the extra sources (SRC_* actions):
  step08_rig.py -- rig/pista_rigged.glb anim/pista_anim_A.glb anim/pista_anim_B.glb anim/pista_anim_C.glb
                   motion/pista_balance_run.glb=T2M_Balance motion/pista_death_trip.glb=T2M_Death
                   motion/pista_water_entry.glb=T2M_Entry
Output: UnityProject/.../Pista/Pista.fbx, work/pista_09_clips.blend, work/clip_report_09.json

Conventions for the new clips (also in IMPORT_NOTES):
  - Land clips: feet on the ground (lowest skinned vertex at z 0 over the clip), in place.
  - Swim clips (Swim, Swim_Dive, Swim_Leap, and the swim ends of Water_Entry / Water_Exit): the root (z 0) is the
    water surface line. SWIM_HIPS_Z puts the Hips bone just under the surface, so head and shoulders ride above it.
  - In place: one-shots have the Hips' horizontal path removed per frame (Unity drops it too: root motion off, XZ not
    baked); loops have only the linear drift over the cycle removed (the sway stays), seam cross-faded.
  - Swim_Dive and Swim_Leap only pitch and flex the body: the simulation owns the depth (-1.10 m) and the leap arc
    (1.20 m), so import them with Keep Height off, like Jump.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

FPS = 30
UNITY_DIR = os.path.join(C.REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Characters', 'Pista')
SWIM_HIPS_Z = -0.05          # Hips bone mean height under the water surface (root = surface line)
SEAM_FRAMES = 4

C.open_blend(C.work('pista_08_rigged.blend'))
sc = bpy.context.scene
sc.render.fps = FPS
arm = bpy.data.objects['Armature']
mesh = bpy.data.objects['SK_Pista']
assert arm.matrix_world == Matrix.Identity(4), 'armature transform must be applied (step 8)'
BONES = [b.name for b in arm.pose.bones]
REST = {b.name: b.matrix_local.copy() for b in arm.data.bones}
HIPS_REST = REST['Hips']
report = {}


# ------------------------------------------------------------------ pose helpers (channels read from fcurves)
def sample(act, f):
    """{bone: [loc, quat, scale]} at frame f (bone-local channels)."""
    pose = {}
    for b in BONES:
        loc, q, s = Vector((0, 0, 0)), Quaternion((1, 0, 0, 0)), Vector((1, 1, 1))
        for i in range(3):
            fc = act.fcurves.find(f'pose.bones["{b}"].location', index=i)
            if fc:
                loc[i] = fc.evaluate(f)
            fc = act.fcurves.find(f'pose.bones["{b}"].scale', index=i)
            if fc:
                s[i] = fc.evaluate(f)
        for i in range(4):
            fc = act.fcurves.find(f'pose.bones["{b}"].rotation_quaternion', index=i)
            if fc:
                q[i] = fc.evaluate(f)
        q.normalize()
        pose[b] = [loc, q, s]
    return pose


def blend(pa, pb, w):
    out = {}
    for b in BONES:
        la, qa, sa = pa[b]
        lb, qb, sb = pb[b]
        if qa.dot(qb) < 0:
            qb = -qb
        out[b] = [la.lerp(lb, w), qa.slerp(qb, w), sa.lerp(sb, w)]
    return out


def copy(p):
    return {b: [v[0].copy(), v[1].copy(), v[2].copy()] for b, v in p.items()}


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def write_action(name, poses):
    """poses: list of pose dicts, one per frame from 0. Linear keys on every channel (like step 8's FBX bake)."""
    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    n = len(poses)
    for b in BONES:
        for path, idx, get in (('location', 3, lambda p: p[0]), ('rotation_quaternion', 4, lambda p: p[1]),
                               ('scale', 3, lambda p: p[2])):
            # keep quaternion signs continuous so linear interpolation never flips
            vals = [get(p[b]) for p in poses]
            if path == 'rotation_quaternion':
                for i in range(1, n):
                    if vals[i].dot(vals[i - 1]) < 0:
                        vals[i] = -vals[i]
            for i in range(idx):
                fc = act.fcurves.new(f'pose.bones["{b}"].{path}', index=i, action_group=b)
                fc.keyframe_points.add(n)
                co = np.zeros(2 * n, dtype=np.float32)
                co[0::2] = np.arange(n)
                co[1::2] = [v[i] for v in vals]
                fc.keyframe_points.foreach_set('co', co)
                fc.keyframe_points.foreach_set('interpolation', [1] * n)    # LINEAR
                fc.update()
    return act


def hips_world(p):
    loc, q, s = p['Hips']
    return HIPS_REST @ Matrix.LocRotScale(loc, q, s)


def set_hips_world(p, m):
    loc, q, s = (HIPS_REST.inverted() @ m).decompose()
    p['Hips'] = [loc, q, s]


def hips_pos(p):
    return hips_world(p).translation


def move_hips(p, d):
    m = hips_world(p)
    m.translation += d
    set_hips_world(p, m)


def pitch_body(p, deg):
    """Rotate the whole body about the Hips joint around world X. Positive = nose up (she faces -Y)."""
    m = hips_world(p)
    piv = m.translation.copy()
    w = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(-deg), 4, 'X') @ Matrix.Translation(-piv)
    set_hips_world(p, w @ m)


def apply_pose(p):
    for b in BONES:
        pb = arm.pose.bones[b]
        pb.rotation_mode = 'QUATERNION'
        pb.location, pb.rotation_quaternion, pb.scale = p[b]
    bpy.context.view_layer.update()


def world_heads(p, names):
    arm.animation_data.action = None
    apply_pose(p)
    return {n: arm.pose.bones[n].head.copy() for n in names}


def rotate_bone_world(p, bone, deg, axis='X'):
    """Rotate one bone (and its children) about its head around a world axis; returns the new pose dict."""
    arm.animation_data.action = None
    apply_pose(p)
    pb = arm.pose.bones[bone]
    m = pb.matrix.copy()
    piv = m.translation.copy()
    pb.matrix = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-piv) @ m
    bpy.context.view_layer.update()
    out = copy(p)
    out[bone] = [pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()]
    return out


def lowest_vertex(poses, step=1):
    """Lowest skinned vertex (world z) per sampled frame."""
    arm.animation_data.action = None
    dg = bpy.context.evaluated_depsgraph_get()
    res = []
    for i in range(0, len(poses), step):
        apply_pose(poses[i])
        dg.update()
        ev = mesh.evaluated_get(dg)
        me = ev.to_mesh()
        co = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get('co', co)
        z = co.reshape(-1, 3)[:, 2].min() + mesh.matrix_world.translation.z
        res.append((i, float(z)))
        ev.to_mesh_clear()
    return res


def pose_distance(pa, pb):
    """Largest bone rotation difference in degrees (the loop-seam metric of step 8)."""
    worst = 0.0
    for b in BONES:
        a = pa[b][1].rotation_difference(pb[b][1]).angle
        worst = max(worst, min(a, 2 * math.pi - a))
    return math.degrees(worst)


def cut(act, a, b):
    return [sample(act, f) for f in range(a, b + 1)]


def inplace_per_frame(poses):
    """Remove the Hips' horizontal path frame by frame (keeps the frame-0 position)."""
    p0 = hips_pos(poses[0])
    for p in poses:
        h = hips_pos(p)
        move_hips(p, Vector((p0.x - h.x, p0.y - h.y, 0)))
    return poses


def inplace_linear(poses):
    """Remove only the linear start-to-end drift (loops keep their sway)."""
    d = hips_pos(poses[-1]) - hips_pos(poses[0])
    n = len(poses) - 1
    for i, p in enumerate(poses):
        move_hips(p, Vector((-d.x * i / n, -d.y * i / n, 0)))
    return poses


def center_xy(poses):
    """Mean Hips XY to the origin, so loops sit on the root like the existing clips."""
    m = sum((hips_pos(p) for p in poses), Vector()) / len(poses)
    for p in poses:
        move_hips(p, Vector((-m.x, -m.y, 0)))
    return poses


def seam(poses):
    """Cross-fade the last SEAM_FRAMES frames toward frame 0 (same rule as step 8), returns the seam angle."""
    n = len(poses) - 1
    for i in range(n - SEAM_FRAMES + 1, n + 1):
        w = ((i - (n - SEAM_FRAMES)) / SEAM_FRAMES) ** 2
        poses[i] = blend(poses[i], poses[0], w)
    return pose_distance(poses[0], poses[-1])


def ground(poses):
    low = min(z for _, z in lowest_vertex(poses, 2))
    for p in poses:
        move_hips(p, Vector((0, 0, -low)))
    return low


def best_cycle(act, lo, hi, pmin, pmax):
    """Frames (a, b) in [lo, hi] with b - a in [pmin, pmax] whose poses match best (loop cut)."""
    poses = {f: sample(act, f) for f in range(lo, hi + 1)}
    best = (1e9, lo, lo + pmin)
    for a in range(lo, hi - pmin + 1):
        for b in range(a + pmin, min(hi, a + pmax) + 1):
            d = pose_distance(poses[a], poses[b])
            if d < best[0]:
                best = (d, a, b)
    return best


def stance_speed(poses):
    """Median stance-foot speed relative to the hips (m/s), the no-slide ground speed (as step 8)."""
    sp = []
    for side in ('Left', 'Right'):
        fr = []
        for p in poses:
            h = world_heads(p, [f'{side}Foot', 'Hips'])
            fr.append((h[f'{side}Foot'], h['Hips']))
        zmin = min(x[0].z for x in fr)
        for (pa, ha), (pb, hb) in zip(fr, fr[1:]):
            if pa.z < zmin + 0.03 and pb.z < zmin + 0.03:
                sp.append(((pb - hb) - (pa - ha)).y * FPS)
    return sorted(sp)[len(sp) // 2] if sp else 0.0


def finish(name, poses, loop, keep_height, src, frames, notes, **extra):
    act = write_action(name, poses)
    report[name] = dict(source=src, source_frames=frames, frames=len(poses) - 1,
                        seconds=round((len(poses) - 1) / FPS, 3), loop=loop, keep_height=keep_height, notes=notes,
                        **extra)
    print(f'{name:14s} {len(poses) - 1:4d}f  {src} {frames}  ' + ' '.join(f'{k}={v}' for k, v in extra.items()))
    return act


A = bpy.data.actions

# ------------------------------------------------------------------ Swim (breaststroke loop)
d, a, b = best_cycle(A['SRC_Swim'], 10, 120, 45, 80)
swim = cut(A['SRC_Swim'], a, b)
inplace_linear(swim)
center_xy(swim)
hz = sum(hips_pos(p).z for p in swim) / len(swim)
for p in swim:
    move_hips(p, Vector((0, 0, SWIM_HIPS_Z - hz)))
sd = seam(swim)
finish('Swim_Surface', swim, True, True, 'Meshy #569 Swim Forward', [a, b],
       'surface breaststroke (the library has no crawl), root = water surface', cycle_match_deg=round(d, 1), loop_seam_deg=round(sd, 1))

# glide frame: arms reached furthest forward with the legs together (start/end pose of dive and leap)
def glide_score(p):
    h = world_heads(p, ['Hips', 'LeftHand', 'RightHand', 'LeftFoot', 'RightFoot'])
    reach = h['Hips'].y - (h['LeftHand'].y + h['RightHand'].y) / 2          # forward is -Y
    return reach - (h['LeftFoot'] - h['RightFoot']).length


gi = max(range(len(swim)), key=lambda i: glide_score(swim[i]))
report['Swim_Surface']['glide_frame'] = gi
# streamline: the breaststroke glide still has the hips flexed (shins hang down), so the legs go back to the rest
# pose, which lies in line with the spine; arms, torso and pointed feet stay from the glide frame
glide = copy(swim[gi])
for sd in ('Left', 'Right'):
    for bn in (f'{sd}UpLeg', f'{sd}Leg'):
        glide[bn] = [Vector((0, 0, 0)), Quaternion((1, 0, 0, 0)), Vector((1, 1, 1))]


def knee_flex_sign():
    """Sign of a world-X knee rotation that lifts the foot (flexion) in the glide pose."""
    base = world_heads(glide, ['LeftFoot'])['LeftFoot'].z
    t = world_heads(rotate_bone_world(glide, 'LeftLeg', 20), ['LeftFoot'])['LeftFoot'].z
    return 1 if t > base else -1


KS = knee_flex_sign()


def kick(p, deg):
    """Dolphin kick: both knees flex by deg (legs together)."""
    for leg in ('LeftLeg', 'RightLeg'):
        p = rotate_bone_world(p, leg, KS * deg)
    return p


def timeline(keys, n):
    """keys: [(frame, pitch_deg, kick_deg, drop_m)], smooth interpolation, one pose per frame from the glide pose.
    Kicks are knee flexion only (>= 0): the glide pose is the straightest the legs get."""
    out = []
    for f in range(n + 1):
        for (f0, *v0), (f1, *v1) in zip(keys, keys[1:]):
            if f0 <= f <= f1:
                w = smooth((f - f0) / max(1, f1 - f0))
                pitch, kd, drop = (x0 + (x1 - x0) * w for x0, x1 in zip(v0, v1))
                break
        p = kick(copy(glide), kd) if abs(kd) > 0.5 else copy(glide)
        pitch_body(p, pitch)
        move_hips(p, Vector((0, 0, -drop)))
        out.append(p)
    return out


def bridge(poses, target, nblend, at_start):
    """Blend the first (or last) nblend frames from/to a target pose so the clip joins Swim."""
    n = len(poses)
    for i in range(nblend):
        w = smooth(1 - i / nblend) if at_start else smooth((i + 1) / nblend)
        j = i if at_start else n - nblend + i
        poses[j] = blend(poses[j], target, w)
    return poses


# ------------------------------------------------------------------ Swim_Dive (0.85 s: down 0.10, under 0.55, up 0.20)
dive = timeline([(0, 0, 0, 0.0), (4, -32, 20, 0.15), (9, -12, 0, 0.25), (14, -6, 25, 0.25), (19, -4, 0, 0.25),
                 (22, 18, 12, 0.12), (26, 0, 0, 0.0)], 26)
bridge(dive, swim[gi], 3, True)
bridge(dive, swim[gi], 3, False)
finish('Swim_Dive', dive, False, False, 'authored from the Swim glide pose', [gi],
       'duck-dive and streamlined glide with dolphin kicks; depth comes from the simulation')

# ------------------------------------------------------------------ Swim_Underwater (streamline glide loop)
UW = 24
under = []
for f in range(UW + 1):
    t = f / UW
    p = kick(copy(glide), 12 + 12 * math.sin(2 * math.pi * t))
    pitch_body(p, -4 + 3 * math.sin(2 * math.pi * t + 1.2))
    move_hips(p, Vector((0, 0, -0.25 + 0.02 * math.sin(2 * math.pi * t))))
    under.append(p)
finish('Swim_Underwater', under, True, False, 'authored from the Swim_Surface glide pose', [gi],
       'streamlined glide with a steady dolphin kick, matches the middle of Swim_Dive; depth comes from the '
       'simulation', loop_seam_deg=round(pose_distance(under[0], under[-1]), 1))

# ------------------------------------------------------------------ Swim_Leap (airtime 0.55 s + push and splash)
leap = timeline([(0, 0, 0, 0.0), (3, 10, 40, 0.05), (6, 35, 5, 0.0), (11, 8, 25, 0.0), (16, -18, 10, 0.0),
                 (20, -38, 0, 0.0), (24, -42, 0, 0.0)], 24)
bridge(leap, swim[gi], 3, True)
finish('Swim_Leap', leap, False, False, 'authored from the Swim glide pose', [gi],
       'dolphin leap: kick, nose up out of the water, flat apex with a light knee bend, head-first splash; the arc '
       'comes from the simulation. Blend to Swim (or Swim_Dive) on splash-down')

# ------------------------------------------------------------------ Water_Entry (run -> racing dive -> swim)
src = A['SRC_WaterEntry']
ENTRY = (28, 52)
entry = inplace_per_frame(cut(src, *ENTRY))
low = min(z for _, z in lowest_vertex(entry[:10], 1))   # running part touches the ground
for p in entry:
    move_hips(p, Vector((0, 0, -low)))
# lower the flat end into the water: ramp the Hips height to the swim convention over the last third
n = len(entry)
z_end = hips_pos(entry[-1]).z
for i, p in enumerate(entry):
    w = smooth((i - n * 0.55) / (n * 0.45))
    move_hips(p, Vector((0, 0, (SWIM_HIPS_Z - z_end) * w)))
s0 = copy(swim[0])
h = hips_pos(entry[-1])
move_hips(s0, Vector((h.x - hips_pos(s0).x, h.y - hips_pos(s0).y, 0)))
tail = [blend(entry[-1], s0, smooth((i + 1) / 8)) for i in range(8)]
entry += tail
center = hips_pos(entry[0])
for p in entry:
    move_hips(p, Vector((-center.x, -center.y, 0)))
finish('Water_Entry', entry, False, True, 'Meshy text to motion (motion/water_entry.txt)', list(ENTRY),
       'last running step, push-off, shallow racing dive, flat glide, ends on Swim frame 0. Feet on the ground '
       'at the start, root = water surface at the end')

# ------------------------------------------------------------------ Water_Exit (stroke -> stand up -> run)
src = A['SRC_SwimToEdge']
EXIT = (30, 92)
ex = inplace_per_frame(cut(src, *EXIT))
n = len(ex)
start_off = SWIM_HIPS_Z - hips_pos(ex[0]).z
end_low = min(z for _, z in lowest_vertex(ex[-6:], 1))
for i, p in enumerate(ex):
    w = smooth((i - n * 0.25) / (n * 0.6))
    move_hips(p, Vector((0, 0, start_off * (1 - w) - end_low * w)))
r0 = sample(A['Run'], 0)
h = hips_pos(ex[-1])
move_hips(r0, Vector((h.x - hips_pos(r0).x, h.y - hips_pos(r0).y, 0)))
ex += [blend(ex[-1], r0, smooth((i + 1) / 8)) for i in range(8)]
center = hips_pos(ex[-1])
for p in ex:
    move_hips(p, Vector((-center.x, -center.y, 0)))
finish('Water_Exit', ex, False, True, 'Meshy #570 Swimming to Edge', list(EXIT),
       'last stroke, legs swing down, rises upright and steps into Run frame 0. Starts at the swim convention '
       '(root = surface), ends with the feet on the ground')

# ------------------------------------------------------------------ Balance_Run (loop)
d, a, b = best_cycle(A['SRC_Balance'], 15, 85, 20, 45)
bal = cut(A['SRC_Balance'], a, b)
spd = stance_speed(bal)
inplace_linear(bal)
center_xy(bal)
low = ground(bal)
sd = seam(bal)
finish('Balance_Run', bal, True, True, 'Meshy text to motion (motion/balance_run.txt)', [a, b],
       'narrow-beam run, arms out wide, feet on one line', cycle_match_deg=round(d, 1), loop_seam_deg=round(sd, 1),
       stance_foot_speed_mps=round(spd, 2), ground_offset_removed_m=round(low, 3))

# ------------------------------------------------------------------ Vine_Release (authored from Vine_Swing + Jump)
# Meshy #494 (Swing on Rope to Ground) and a text-to-motion release were tried and rejected in review (#494 leans back
# as if flung off the rope; the text-to-motion clip never lets go). This one starts on Vine_Swing's forward swing
# (leaning back, legs forward, hands on the vine), lets go and rotates forward into Jump's airborne tuck with a reach.
# It ends in the air: blend to Fall or Land. The arc comes from the simulation.
VS, VA, VB = A['Vine_Swing'], 37, 40     # forward swing, body leaning back 27-48 deg (spec 103 release ~42-53 deg)
JP, JA, JB = A['Jump'], 8, 16            # airborne tuck with the reach (feet still 0.4 m up at frame 16)
peak = [sample(VS, f) for f in range(VA, VB + 1)]
jump = [sample(JP, f) for f in range(JA, JB + 1)]
rel = list(peak)
for k, jp in enumerate(jump):
    rel.append(blend(peak[-1], jp, smooth((k + 1) / 8)))
inplace_per_frame(rel)
c = hips_pos(rel[0])
for p in rel:
    move_hips(p, Vector((-c.x, -c.y, 0)))
finish('Vine_Release', rel, False, False, 'Vine_Swing (Meshy #495) + Jump (Meshy #467)',
       [f'Vine_Swing {VA}-{VB}', f'Jump {JA}-{JB}'],
       'forward swing on the vine, let go, rotate forward into a tuck with a reach; ends airborne')

# ------------------------------------------------------------------ Water_Wade (loop)
d, a, b = best_cycle(A['SRC_Wade'], 10, 85, 18, 45)
wade = cut(A['SRC_Wade'], a, b)
spd = stance_speed(wade)
inplace_linear(wade)
center_xy(wade)
low = ground(wade)
sd = seam(wade)
finish('Water_Wade', wade, True, True, 'Meshy text to motion, swift (motion/water_wade.txt)', [a, b],
       'high-knee run through shallow water', cycle_match_deg=round(d, 1), loop_seam_deg=round(sd, 1),
       stance_foot_speed_mps=round(spd, 2), ground_offset_removed_m=round(low, 3))

# ------------------------------------------------------------------ Death_Stumble (trip -> hands and knees -> prone)
DEATH = (17, 110)
dth = inplace_per_frame(cut(A['SRC_DeathTrip'], *DEATH))
low = ground(dth)
lows = dict(lowest_vertex(dth, 1))
n = len(dth)
end_low = min(lows[i] for i in range(n - 10, n))
# the source's prone pose floats above its own hand plant: lower it from the hand plant (first contact) to the end
contact = next(i for i in range(n) if lows[i] < 0.02 and i > 5)
LIE = min(n - 1, contact + 25)
for i, p in enumerate(dth):
    move_hips(p, Vector((0, 0, -end_low * smooth((i - contact) / (LIE - contact)))))
report.setdefault('_death_fix', {})['prone_lowered_m'] = round(end_low, 3)
c = hips_pos(dth[0])
for p in dth:
    move_hips(p, Vector((-c.x, -c.y, 0)))
finish('Death_Stumble', dth, False, True, 'Meshy text to motion (motion/death_trip.txt)', list(DEATH),
       'running stride, trips, catches herself on hands and knees, slides down face-first and lies still. '
       'No pain pose (ART_DIRECTION 7.4); pair with the quick fade', ground_offset_removed_m=round(low, 3))

# ------------------------------------------------------------------ Slide_Clean (Slide without the raised arm)
src = A['Slide']
f0, f1 = (int(round(x)) for x in src.frame_range)
sl = cut(src, f0, f1)
raised = {}
for side in ('Left', 'Right'):
    fr = [i for i, p in enumerate(sl)
          if (lambda h: h[f'{side}Hand'].z > h['neck'].z - 0.05)(world_heads(p, [f'{side}Hand', 'neck']))]
    raised[side] = fr
side = max(raised, key=lambda s: len(raised[s]))
fr = raised[side]
ARM = [f'{side}Shoulder', f'{side}Arm', f'{side}ForeArm', f'{side}Hand']
s_, e_ = max(0, fr[0] - 4), min(len(sl) - 1, fr[-1] + 6)
pa, pb = sl[s_], sl[e_]
for i in range(s_, e_ + 1):
    w = smooth((i - s_) / (e_ - s_))
    for bn in ARM:
        qa, qb = pa[bn][1], pb[bn][1]
        if qa.dot(qb) < 0:
            qb = -qb
        sl[i][bn] = [pa[bn][0].lerp(pb[bn][0], w), qa.slerp(qb, w), pa[bn][2].lerp(pb[bn][2], w)]
finish('Slide_Clean', sl, False, True, 'Slide (Meshy #517) with the arm raise removed', [f0, f1],
       f'{side.lower()} arm interpolated from frame {s_} to {e_} (was raised above the neck in frames '
       f'{fr[0]}-{fr[-1]}), so it goes from the run swing straight to the trailing hand')

# ------------------------------------------------------------------ clean up, save, export
for act in list(A):
    if act.name.startswith('SRC_'):
        A.remove(act)
arm.animation_data.action = A.get('Idle')
for b in BONES:
    pb = arm.pose.bones[b]
    pb.location, pb.rotation_quaternion, pb.scale = Vector(), Quaternion(), Vector((1, 1, 1))
sc.frame_set(0)
with open(C.work('clip_report_09.json'), 'w') as f:
    json.dump(report, f, indent=1)
C.save_blend(C.work('pista_09_clips.blend'))
if '--no-fbx' in C.args():
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
print('exported', out, os.path.getsize(out), 'clips', sorted(a_.name for a_ in A))
