"""Sailback (spec 103 s7, ART_DIRECTION s5.2 #3): Meshy model -> game creature with a light procedural rig and clips.

Source: OpenAI concept art_source/environment/openai/forest/f_sailback_c1.png -> Meshy image-to-3D (latest, textured,
remeshed 8k) art_source/environment/meshy/models/sailback_A.glb. Original design: a small glider lizard (four legs,
two eyes, rib-supported side membranes, no feathers, not rideable), not a winged mount.

Steps: scale to a 0.80 m sail span (spec: <= 1 m), pivot at the body centre, head toward Blender -Y (Unity +Z);
LOD0/1/2 = 1,500 / 600 / 250 tris (decimated, Meshy UVs kept); Meshy's texture downscaled to one 512 BaseColor, no
normal map (PAINTERLY s3: form is in the paint);
vertex colour R = sail membrane mask (1 on the sails: back-lit translucency, PAINTERLY s5), G = B = 0, A = 1.
Rig (12 bones): root, chest, head, tail_01..03, sail_L/R, leg_FL/FR/BL/BR; weights from the mesh layout
(procedural, by region with soft blends). Clips (24 fps): Glide (48 f loop), Flap (16 f loop), Perch (48 f loop,
sails half folded, legs down), Alert (12 f, sail flare from Perch).
Outputs: Creatures/CR_Sailback.fbx (armature + CR_Sailback_LOD0..2, 4 actions), Creatures/Textures/CR_Sailback_*.png,
work/creatures/CR_Sailback_game.blend.
Usage: blender ... -P forest_sailback.py
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402

SRC = os.path.join(E.SRC, 'meshy', 'models', 'sailback_A.glb')
OUT = os.path.join(E.UNITY_ENV, 'Creatures')
SPAN = 0.80
RES = 512


def import_src():
    bpy.ops.import_scene.gltf(filepath=SRC)
    ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
    for o in list(bpy.context.scene.objects):
        if o is not ob and o.type != 'MESH':
            bpy.data.objects.remove(o)
    ob.parent = None
    E.set_active(ob)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


# ------------------------------------------------------------------ rig layout (Meshy units, head at -Y)
BONES = {  # name: (head, tail, parent)
    'root': ((0, -0.25, 0.0), (0, -0.45, 0.0), None),
    'chest': ((0, -0.45, 0.0), (0, -0.68, 0.01), 'root'),
    'head': ((0, -0.68, 0.01), (0, -0.95, 0.05), 'chest'),
    'tail_01': ((0.01, -0.06, -0.06), (0.09, 0.25, -0.08), 'root'),
    'tail_02': ((0.09, 0.25, -0.08), (0.08, 0.58, -0.105), 'tail_01'),
    'tail_03': ((0.08, 0.58, -0.105), (0.02, 0.95, -0.125), 'tail_02'),
    'sail_L': ((0.07, -0.44, -0.03), (0.66, -0.44, -0.03), 'chest'),
    'sail_R': ((-0.07, -0.44, -0.03), (-0.66, -0.44, -0.03), 'chest'),
    'leg_FL': ((0.06, -0.79, -0.04), (0.26, -0.8, -0.06), 'chest'),
    'leg_FR': ((-0.06, -0.79, -0.04), (-0.26, -0.8, -0.06), 'chest'),
    'leg_BL': ((0.06, -0.15, -0.06), (0.29, -0.13, -0.07), 'root'),
    'leg_BR': ((-0.06, -0.15, -0.06), (-0.29, -0.13, -0.07), 'root'),
}


def ss(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def weights(V):
    """Region weights per vertex (Meshy units). Returns {bone: array}."""
    x, y = V[:, 0], V[:, 1]
    ax = np.abs(x)
    W = {b: np.zeros(len(V)) for b in BONES}
    # trunk along y: head / chest / root / tail chain
    head = ss(-0.62, -0.72, y)
    chest = ss(-0.25, -0.45, y) * (1 - head)
    tail = ss(-0.12, 0.02, y)
    root = np.clip(1 - head - chest - tail, 0, 1)
    t1 = tail * (1 - ss(0.2, 0.32, y))
    t2 = tail * ss(0.2, 0.32, y) * (1 - ss(0.52, 0.64, y))
    t3 = tail * ss(0.52, 0.64, y)
    trunk = dict(head=head, chest=chest, root=root, tail_01=t1, tail_02=t2, tail_03=t3)
    # limbs and sails take over away from the body axis
    lat = ss(0.065, 0.13, ax)
    sail_zone = ss(-0.66, -0.6, y) * (1 - ss(-0.27, -0.21, y))
    front = ss(-0.92, -0.88, y) * (1 - ss(-0.66, -0.6, y))
    back = ss(-0.27, -0.21, y) * (1 - ss(-0.04, 0.02, y))
    L = x > 0
    limb = {'sail_L': lat * sail_zone * L, 'sail_R': lat * sail_zone * ~L, 'leg_FL': lat * front * L,
            'leg_FR': lat * front * ~L, 'leg_BL': lat * back * L, 'leg_BR': lat * back * ~L}
    lsum = sum(limb.values())
    for k, v in trunk.items():
        W[k] = v * (1 - lsum)
    for k, v in limb.items():
        W[k] = v
    tot = sum(W.values()) + 1e-9
    return {k: v / tot for k, v in W.items()}


def build_rig(scale):
    arm = bpy.data.armatures.new('CR_Sailback_Rig')
    ao = bpy.data.objects.new('CR_Sailback', arm)
    E.link(ao)
    E.set_active(ao)
    bpy.ops.object.mode_set(mode='EDIT')
    eb = {}
    for n, (h, t, p) in BONES.items():
        b = arm.edit_bones.new(n)
        b.head = Vector(h) * scale
        b.tail = Vector(t) * scale
        b.roll = 0.0
        eb[n] = b
    for n, (h, t, p) in BONES.items():
        if p:
            eb[n].parent = eb[p]
            eb[n].use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return ao


def skin(ob, ao, Vsrc):
    W = weights(Vsrc)
    for n in BONES:
        g = ob.vertex_groups.new(name=n)
        for i, w in enumerate(W[n]):
            if w > 0.01:
                g.add([i], float(w), 'REPLACE')
    m = ob.modifiers.new('arm', 'ARMATURE')
    m.object = ao
    ob.parent = ao


def key(ao, f, pose):
    """pose: {bone: dict(rx=deg, ry=deg, rz=deg, sy=scale along bone, loc=(x,y,z))} (local, XYZ euler)."""
    for n, p in pose.items():
        pb = ao.pose.bones[n]
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (math.radians(p.get('rx', 0)), math.radians(p.get('ry', 0)), math.radians(p.get('rz', 0)))
        pb.scale = (1, p.get('sy', 1.0), 1)
        pb.location = p.get('loc', (0, 0, 0))
        pb.keyframe_insert('rotation_euler', frame=f)
        pb.keyframe_insert('scale', frame=f)
        pb.keyframe_insert('location', frame=f)


def clip(ao, name, n, fn, cyclic=True):
    act = bpy.data.actions.new(name)
    ao.animation_data_create()
    ao.animation_data.action = act
    for f in range(0, n + 1, 2 if n > 20 else 1):
        key(ao, f, fn(f / n))
    if cyclic:
        for fc in act.fcurves:
            fc.modifiers.new('CYCLES')
    act.use_fake_user = True
    act['frame_range'] = (0, n)
    return act


def S(t, ph=0.0):
    return math.sin(2 * math.pi * (t + ph))


def glide(t):
    th = 5 + 3 * S(t)
    return dict(root=dict(ry=3 * S(t, 0.1), loc=(0, 0.004 * S(t, 0.25), 0)), chest=dict(rz=1.5 * S(t, 0.3)),
                head=dict(rz=-2 * S(t, 0.35), rx=2), sail_L=dict(rx=th), sail_R=dict(rx=th),
                tail_01=dict(rz=5 * S(t, 0.2)), tail_02=dict(rz=7 * S(t, 0.3)), tail_03=dict(rz=9 * S(t, 0.4)),
                leg_FL=dict(rz=-8), leg_FR=dict(rz=8), leg_BL=dict(rz=10), leg_BR=dict(rz=-10))


def flap(t):
    th = 8 + 28 * S(t)
    return dict(root=dict(loc=(0, 0, 0.02 * S(t, 0.5)), ry=0), chest=dict(rx=3 * S(t, 0.1)), head=dict(rx=2 - 3 * S(t, 0.15)),
                sail_L=dict(rx=th), sail_R=dict(rx=th), tail_01=dict(rx=-4 * S(t, 0.25)), tail_02=dict(rx=-6 * S(t, 0.35)),
                tail_03=dict(rx=-8 * S(t, 0.45)), leg_FL=dict(rz=-8), leg_FR=dict(rz=8), leg_BL=dict(rz=10), leg_BR=dict(rz=-10))


def perch(t):
    b = 1 + 0.015 * S(t)
    return dict(root=dict(), chest=dict(rx=4 * b), head=dict(rx=10 + 3 * S(t, 0.2), rz=6 * S(t * 0.5)),
                sail_L=dict(rx=-58, sy=0.45), sail_R=dict(rx=-58, sy=0.45), tail_01=dict(rz=-6), tail_02=dict(rz=-10),
                tail_03=dict(rz=-14 + 4 * S(t, 0.3)), leg_FL=dict(rx=-35), leg_FR=dict(rx=-35), leg_BL=dict(rx=-30),
                leg_BR=dict(rx=-30))


def alert(t):
    e = math.sin(min(1.0, t * 1.4) * math.pi / 2)
    p = perch(0)
    p['sail_L'] = dict(rx=-58 + 66 * e, sy=0.45 + 0.55 * e)
    p['sail_R'] = dict(rx=-58 + 66 * e, sy=0.45 + 0.55 * e)
    p['head'] = dict(rx=10 + 12 * e)
    p['chest'] = dict(rx=4 + 8 * e)
    return p


def main():
    E.reset()
    sc = E.gpu_cycles(16)
    src = import_src()
    Vsrc = np.array([v.co[:] for v in src.data.vertices])
    s = SPAN / (Vsrc[:, 0].max() - Vsrc[:, 0].min())
    src.data.transform(__import__('mathutils').Matrix.Scale(s, 4))
    Vsc = Vsrc * s
    print('scale', round(s, 4), 'size m', (Vsc.max(0) - Vsc.min(0)).round(3))
    # game LOD0: copy, decimate, new UVs, bake from the Meshy mesh
    low = E.copy_obj(src, 'CR_Sailback_LOD0')
    import bmesh
    bm = bmesh.new()                      # glTF import splits vertices on every UV seam: weld first (UVs stay per loop),
    bm.from_mesh(low.data)                # or the decimator tears the surface apart at the seams
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bm.to_mesh(low.data)
    bm.free()
    E.decimate_to(low, 1500)          # collapse keeps Meshy's UVs (a cage rebake left black misses on the thin shell)
    E.triangulate(low)
    srcimg = next(n.image for n in src.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE')
    big = E.pixels(srcimg)
    k = big.shape[0] // RES
    small = big.reshape(RES, k, RES, k, 4).mean((1, 3))      # box downscale 2048 -> 512
    nrm = None
    # sail mask into BaseColor alpha: bake a vertex-colour emission of the sail weight
    Vl = np.array([v.co[:] for v in low.data.vertices]) / s
    Wl = weights(Vl)
    mask = Wl['sail_L'] + Wl['sail_R']
    ca = low.data.color_attributes.new('sail', 'FLOAT_COLOR', 'POINT')
    col = np.zeros((len(mask), 4), np.float32)
    col[:, 0], col[:, 3] = np.clip(mask * 1.4, 0, 1), 1.0
    ca.data.foreach_set('color', col.ravel())
    px = small.copy()
    px[..., 3] = 1.0
    base = E.new_image('CR_Sailback_BaseColor_out', RES)
    E.set_pixels(base, px)
    os.makedirs(os.path.join(OUT, 'Textures'), exist_ok=True)
    E.save_png(base, os.path.join(OUT, 'Textures', 'CR_Sailback_BaseColor.png'))
    mt = bpy.data.materials.new('MI_CR_Sailback')
    mt.use_nodes = True
    nt = mt.node_tree
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = base
    nt.links.new(tb.outputs[0], nt.nodes['Principled BSDF'].inputs['Base Color'])
    nt.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.6
    low.data.materials.clear()
    low.data.materials.append(mt)
    lods = [low]
    for i, t in ((1, 600), (2, 250)):   # LODs keep the 'sail' colour (R = membrane mask, for back-lit translucency)
        lo = E.copy_obj(low, f'CR_Sailback_LOD{i}')
        E.decimate_to(lo, t)
        lods.append(lo)
    bpy.data.objects.remove(src)
    ao = build_rig(s)
    import bmesh
    for lo in lods:                       # decimation flipped ~22 % of the body faces inward (dark patches, light slivers)
        bm = bmesh.new()
        bm.from_mesh(lo.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(lo.data)
        bm.free()
    for lo in lods:
        lo.data.normals_split_custom_set_from_vertices([v.normal[:] for v in lo.data.vertices])
        skin(lo, ao, np.array([v.co[:] for v in lo.data.vertices]) / s)
    acts = [clip(ao, 'Glide', 48, glide), clip(ao, 'Flap', 16, flap), clip(ao, 'Perch', 48, perch),
            clip(ao, 'Alert', 12, alert, cyclic=False)]
    ao.animation_data.action = acts[0]
    path = os.path.join(OUT, 'CR_Sailback.fbx')
    for o in list(sc.objects):
        o.select_set(o in lods or o is ao)
    bpy.context.view_layer.objects.active = ao
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             use_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             primary_bone_axis='Y', secondary_bone_axis='X', path_mode='STRIP', colors_type='LINEAR',
                             embed_textures=False)
    for im in (base,):
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('creatures', 'CR_Sailback_game.blend'), compress=True)
    print('SUMMARY CR_Sailback', [E.tris(x) for x in lods], 'bones', len(BONES), 'clips', [a.name for a in acts],
          'size', (Vsc.max(0) - Vsc.min(0)).round(3), os.path.getsize(path))


if __name__ == '__main__':
    main()
