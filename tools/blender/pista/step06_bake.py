"""Step 6: bake the mobile texture set from the Meshy surface onto the cleaned mesh, with the art fixes.

Sources (selected-to-active, Cycles CPU):
  SRC      the Meshy mesh after rope removal (work/pista_02_norope.blend: original UVs + 4k PBR maps), with the
           treasure-map patch projected onto the backpack pocket in its material (base color, roughness, bump)
  SRC_MIR  the same mesh mirrored in X: clean trouser texture from the other leg, cloned over the hip where the
           rope was painted (mask = distance to the removed rope + rope-colored texels in the hip box)
Baked at 4096 (2x2 supersampling), composited in numpy, then downsampled to 2048 (game) and 1024 (comparison).

Outputs (art_source/pista/clean/textures/):
  T_Pista_BaseColor_2k.png / _1k.png   sRGB albedo, delit check applied, albedo floor 30 sRGB (ART_DIRECTION 10.2)
  T_Pista_Normal_2k.png / _1k.png      tangent space, OpenGL (+Y), MikkTSpace (Blender == Unity)
  T_Pista_ARM_2k.png / _1k.png         linear: R = ambient occlusion, G = roughness, B = metallic (ADR 0004)
                                       (R/G/B order matches glTF ORM, so the GLB can reuse it as-is)
Input:  work/pista_05_uv.blend, work/pista_02_norope.blend, work/rope_mask_world.npy, work/patch_*.png
Output: work/pista_06_baked.blend (targets with the final material), work/bake_*.npy (4k intermediates)
"""
import colorsys  # noqa: F401  (kept for readers comparing with step 2)
import os
import sys
import time

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

BAKE = int(os.environ.get('PISTA_BAKE_RES', 4096))
AO_RES = 2048
OUT_TEX = os.path.join(C.CLEAN, 'textures')

# Treasure-map patch on the backpack's lower pocket (located on a back-view grid render, 2026-10-08)
PATCH_CENTER_X, PATCH_CENTER_Z = 0.0, 1.075
PATCH_W, PATCH_H = 0.095, 0.095      # meters (image is square; the painted canvas fills ~91%)
PATCH_ROUGHNESS = 0.88

# Rope clone mask (meters)
ROPE_FULL, ROPE_ZERO = 0.025, 0.045
HIP_BOX_MIN, HIP_BOX_MAX = Vector((0.03, -0.09, 0.68)), Vector((0.23, 0.14, 0.875))

t0 = time.time()


def log(*a):
    print(f'[{time.time() - t0:6.0f}s]', *a, flush=True)


C.open_blend(C.work('pista_05_uv.blend'))
targets = [bpy.data.objects[C.BODY], bpy.data.objects[C.PONYTAIL]]

# ---------------------------------------------------------------- sources
with bpy.data.libraries.load(C.work('pista_02_norope.blend'), link=False) as (src_lib, dst_lib):
    dst_lib.objects = [C.BODY]
src = dst_lib.objects[0]
src.name = 'SRC'
bpy.context.scene.collection.objects.link(src)
mir = src.copy()
mir.data = src.data.copy()
mir.name = 'SRC_MIR'
bpy.context.scene.collection.objects.link(mir)
mir.scale.x = -1
C.set_active(mir)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
# outward check: on the mirrored mesh, normals must still point away from the body axis
me = mir.data
out_frac = np.mean([((p.center.x * p.normal.x + p.center.y * p.normal.y) > 0) for p in me.polygons
                    if 0.4 < p.center.z < 0.9])
log(f'mirror source: {out_frac:.2f} of leg faces point outward')
if out_frac < 0.5:
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.flip_normals(); bpy.ops.object.mode_set(mode='OBJECT')

# Side-seam smear: Meshy left a light streak with a dark crack down the outer side of BOTH hips (the hanging hands
# hid that strip in the input views). Fix: clone fabric from SEAM_ROT_DEG toward the back of the same thigh, by
# baking from a copy of the source rotated about each thigh's vertical axis.
SEAM_ROT_DEG = 35.0
SEAM_Z = (0.66, 0.88)
SEAM_HALF_ANGLE = 22.0      # degrees either side of straight out (+X / -X)
thigh_axis = {}
for side, sgn in (('R', 1), ('L', -1)):
    pts = [v.co for v in src.data.vertices if 0.74 < v.co.z < 0.84 and 0.03 < sgn * v.co.x < 0.20 and abs(v.co.y) < 0.15]  # thigh only, not the hands
    cx = sum(p.x for p in pts) / len(pts); cy = sum(p.y for p in pts) / len(pts)
    thigh_axis[side] = (cx, cy)
    rot = src.copy(); rot.data = src.data.copy(); rot.name = f'SRC_ROT_{side}'
    bpy.context.scene.collection.objects.link(rot)
    # move texture from the back (+Y) to the outer side: right leg turns clockwise (-), left leg counter-clockwise (+)
    from mathutils import Matrix
    ang = np.radians(SEAM_ROT_DEG) * (-1 if side == 'R' else 1)
    rot.matrix_world = (Matrix.Translation((cx, cy, 0)) @ Matrix.Rotation(ang, 4, 'Z') @
                        Matrix.Translation((-cx, -cy, 0)))
    log(f'thigh axis {side}: x={cx:.3f} y={cy:.3f}')
rot_r, rot_l = bpy.data.objects['SRC_ROT_R'], bpy.data.objects['SRC_ROT_L']

# Where is the pocket surface under the patch? (for the projection depth limit)
bvh = BVHTree.FromObject(src, bpy.context.evaluated_depsgraph_get())
hit = bvh.ray_cast(Vector((PATCH_CENTER_X, 1.0, PATCH_CENTER_Z)), Vector((0, -1, 0)))
patch_y = hit[0].y
log(f'patch surface at y={patch_y:.3f}')

# ---------------------------------------------------------------- source material
mat = src.data.materials[0]
mat.name = 'M_Source'
nt = mat.node_tree
nodes, links = nt.nodes, nt.links
tex_base = next(n for n in nodes if n.type == 'TEX_IMAGE' and n.image.colorspace_settings.name == 'sRGB')
tex_mr = next(n for n in nodes if n.type == 'TEX_IMAGE' and n.image.size[0] == 2048)
tex_n = next(n for n in nodes if n.type == 'TEX_IMAGE' and n not in (tex_base, tex_mr))
bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
out = next(n for n in nodes if n.type == 'OUTPUT_MATERIAL')
nmap = next(n for n in nodes if n.type == 'NORMAL_MAP')
sep_mr = nodes.new('ShaderNodeSeparateColor'); links.new(tex_mr.outputs['Color'], sep_mr.inputs[0])

geo = nodes.new('ShaderNodeNewGeometry')
sxyz = nodes.new('ShaderNodeSeparateXYZ'); links.new(geo.outputs['Position'], sxyz.inputs[0])
nxyz = nodes.new('ShaderNodeSeparateXYZ'); links.new(geo.outputs['Normal'], nxyz.inputs[0])


def math(op, a, b=None, clamp=False):
    n = nodes.new('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = v
        else:
            links.new(v, n.inputs[i])
    return n.outputs[0]


# Projection seen from behind: image right = world -X, image up = world +Z.
u = math('ADD', math('DIVIDE', math('SUBTRACT', PATCH_CENTER_X, sxyz.outputs['X']), PATCH_W), 0.5)
v = math('ADD', math('DIVIDE', math('SUBTRACT', sxyz.outputs['Z'], PATCH_CENTER_Z), PATCH_H), 0.5)
puv = nodes.new('ShaderNodeCombineXYZ'); links.new(u, puv.inputs[0]); links.new(v, puv.inputs[1])
pimg = nodes.new('ShaderNodeTexImage'); pimg.image = bpy.data.images.load(C.work('patch_color.png'))
pimg.extension = 'CLIP'; pimg.interpolation = 'Cubic'; links.new(puv.outputs[0], pimg.inputs[0])
himg = nodes.new('ShaderNodeTexImage'); himg.image = bpy.data.images.load(C.work('patch_height.png'))
himg.image.colorspace_settings.name = 'Non-Color'; himg.extension = 'CLIP'; links.new(puv.outputs[0], himg.inputs[0])
facing = math('MULTIPLY', math('SUBTRACT', nxyz.outputs['Y'], 0.25), 4.0, clamp=True)       # normal toward +Y
front = math('GREATER_THAN', sxyz.outputs['Y'], patch_y - 0.03)                                # outer surface only
pmask = math('MULTIPLY', math('MULTIPLY', pimg.outputs['Alpha'], facing), front)

# ShaderNodeMix sockets share names across data types, so use indices: 0 factor, 4/5 vector A/B, 6/7 color A/B;
# outputs 1 vector, 2 color.
mix_c = nodes.new('ShaderNodeMix'); mix_c.data_type = 'RGBA'
links.new(pmask, mix_c.inputs[0]); links.new(tex_base.outputs['Color'], mix_c.inputs[6])
links.new(pimg.outputs['Color'], mix_c.inputs[7])
rough = math('ADD', math('MULTIPLY', sep_mr.outputs['Green'], math('SUBTRACT', 1.0, pmask)),
             math('MULTIPLY', pmask, PATCH_ROUGHNESS))
metal = math('MULTIPLY', sep_mr.outputs['Blue'], math('SUBTRACT', 1.0, pmask))
mix_n = nodes.new('ShaderNodeMix'); mix_n.data_type = 'VECTOR'
links.new(pmask, mix_n.inputs[0]); links.new(nmap.outputs['Normal'], mix_n.inputs[4])
links.new(geo.outputs['Normal'], mix_n.inputs[5])
bump = nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.8
bump.inputs['Distance'].default_value = 0.0015
links.new(math('MULTIPLY', himg.outputs['Color'], pmask), bump.inputs['Height'])
links.new(mix_n.outputs[1], bump.inputs['Normal'])
links.new(mix_c.outputs[2], bsdf.inputs['Base Color'])
links.new(rough, bsdf.inputs['Roughness'])
links.new(metal, bsdf.inputs['Metallic'])
links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
emit = nodes.new('ShaderNodeEmission'); emit.inputs['Strength'].default_value = 1.0
rm = nodes.new('ShaderNodeCombineColor'); links.new(rough, rm.inputs[0]); links.new(metal, rm.inputs[1])


def source_mode(mode):
    """'bsdf' for the normal bake, 'base' / 'rm' for emission bakes."""
    for l in list(out.inputs['Surface'].links):
        links.remove(l)
    if mode == 'bsdf':
        links.new(bsdf.outputs[0], out.inputs['Surface'])
    else:
        links.new(mix_c.outputs[2] if mode == 'base' else rm.outputs[0], emit.inputs['Color'])
        links.new(emit.outputs[0], out.inputs['Surface'])


# ---------------------------------------------------------------- target material + zone attributes
tm = bpy.data.materials.new('M_Pista')
tm.use_nodes = True
tnt = tm.node_tree
for o in targets:
    o.data.materials.clear(); o.data.materials.append(tm)
t_emit = tnt.nodes.new('ShaderNodeEmission')
t_attr = tnt.nodes.new('ShaderNodeAttribute')
t_out = tnt.nodes['Material Output']
t_bsdf = tnt.nodes['Principled BSDF']

rope_pts = np.load(C.work('rope_mask_world.npy'))
kd = KDTree(len(rope_pts))
for i, p in enumerate(rope_pts):
    kd.insert(p, i)
kd.balance()
for o in targets:
    me = o.data
    z1 = me.color_attributes.new('zones1', 'FLOAT_COLOR', 'POINT')
    z2 = me.color_attributes.new('zones2', 'FLOAT_COLOR', 'POINT')
    z3 = me.color_attributes.new('zones3', 'FLOAT_COLOR', 'POINT')
    is_pony = o.name == C.PONYTAIL
    for vtx in me.vertices:
        c = vtx.co
        d = kd.find(c)[2] if not is_pony else 1.0
        rope = float(np.clip((ROPE_ZERO - d) / (ROPE_ZERO - ROPE_FULL), 0, 1))
        hip = float(all(HIP_BOX_MIN[i] < c[i] < HIP_BOX_MAX[i] for i in range(3)))
        head = float(c.z > 1.33)
        arms = float(abs(c.x) > 0.205 and 0.70 < c.z < 1.40)   # forearms/hands, clear of the hip pouches
        belt = float(0.86 < c.z < 1.00)
        seam = []
        for side, sgn in (('R', 1), ('L', -1)):
            ax, ay = thigh_axis[side]
            a = np.degrees(np.arctan2(c.y - ay, sgn * (c.x - ax)))          # 0 = straight out to the side
            wa = np.clip((SEAM_HALF_ANGLE + 8 - abs(a)) / 8, 0, 1)
            wz = np.clip(min(c.z - SEAM_Z[0], SEAM_Z[1] - c.z) / 0.03, 0, 1)
            seam.append(0.0 if is_pony or sgn * c.x < 0.03 else float(wa * wz))
        z3.data[vtx.index].color = (seam[0], seam[1], 0, 1)
        z1.data[vtx.index].color = (rope, hip, head, 1)
        z2.data[vtx.index].color = (arms, belt, float(is_pony), 1)

# Bake onto one joined proxy: baking body and ponytail one after another into the same image lets the second
# bake's margin fill overwrite the first object's texels.
proxy_parts = []
for o in targets:
    c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c); proxy_parts.append(c)
for o in bpy.context.view_layer.objects:
    o.select_set(o in proxy_parts)
bpy.context.view_layer.objects.active = proxy_parts[0]
bpy.ops.object.join()
proxy = proxy_parts[0]
proxy.name = 'BAKE_PROXY'
for o in targets:
    o.hide_render = True

# ---------------------------------------------------------------- bake helpers
sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 1
sc.cycles.use_denoising = False
sc.render.bake.margin = 16
sc.render.bake.margin_type = 'EXTEND'
tex_node = tnt.nodes.new('ShaderNodeTexImage')
tnt.nodes.active = tex_node


def new_image(name, res, alpha=True):
    img = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    img.generated_color = (0, 0, 0, 0)
    return img


def bake(name, kind, source=None, res=BAKE, samples=1, emit_attr=None, extrusion=0.012, ray=0.04):
    img = new_image(name, res)
    tex_node.image = img
    sc.cycles.samples = samples
    for l in list(t_out.inputs['Surface'].links):
        tnt.links.remove(l)
    if emit_attr:
        t_attr.attribute_name = emit_attr
        tnt.links.new(t_attr.outputs['Color'], t_emit.inputs['Color'])
        tnt.links.new(t_emit.outputs[0], t_out.inputs['Surface'])
    else:
        tnt.links.new(t_bsdf.outputs[0], t_out.inputs['Surface'])
    for s in (src, mir, rot_r, rot_l):
        s.hide_render = s is not source
    for tgt in (proxy,):
        for o in bpy.context.view_layer.objects:
            o.select_set(o is tgt or o is source)
        bpy.context.view_layer.objects.active = tgt
        kw = dict(type=kind, target='IMAGE_TEXTURES', use_clear=False, margin=sc.render.bake.margin)
        if source is not None:
            kw.update(use_selected_to_active=True, cage_extrusion=extrusion, max_ray_distance=ray)
        if kind == 'NORMAL':
            kw.update(normal_space='TANGENT')
        bpy.ops.object.bake(**kw)
    a = np.array(img.pixels[:], np.float32).reshape(res, res, 4)
    np.save(C.work(f'bake_{name}.npy'), a)
    log(f'baked {name} ({kind}, {res}px)')
    return a


# ---------------------------------------------------------------- bakes
source_mode('base'); base = bake('base', 'EMIT', src)
base_m = bake('base_mir', 'EMIT', mir, extrusion=0.035, ray=0.09)   # legs are not exactly symmetric
source_mode('rm'); rmap = bake('rm', 'EMIT', src)
rmap_m = bake('rm_mir', 'EMIT', mir, extrusion=0.035, ray=0.09)
source_mode('bsdf'); nrm = bake('normal', 'NORMAL', src)
nrm_m = bake('normal_mir', 'NORMAL', mir, extrusion=0.035, ray=0.09)
rot = {}
for side, ob in (('R', rot_r), ('L', rot_l)):
    source_mode('base'); b_ = bake(f'base_rot{side}', 'EMIT', ob, extrusion=0.035, ray=0.09)
    source_mode('rm'); r_ = bake(f'rm_rot{side}', 'EMIT', ob, extrusion=0.035, ray=0.09)
    source_mode('bsdf'); n_ = bake(f'normal_rot{side}', 'NORMAL', ob, extrusion=0.035, ray=0.09)
    rot[side] = (b_, r_, n_)
zones3 = bake('zones3', 'EMIT', None, emit_attr='zones3')
zones1 = bake('zones1', 'EMIT', None, emit_attr='zones1')
zones2 = bake('zones2', 'EMIT', None, emit_attr='zones2')
src.hide_render = mir.hide_render = rot_r.hide_render = rot_l.hide_render = True
sc.world = sc.world or bpy.data.worlds.new('World')
sc.world.light_settings.distance = 0.15     # character-scale AO (meters)
sc.render.bake.margin = 8
ao = bake('ao', 'AO', None, res=AO_RES, samples=64)

# ---------------------------------------------------------------- composite (numpy, 4k, linear values)
import composite  # noqa: E402  (tools/blender/pista/composite.py)

composite.run(base, base_m, rmap, rmap_m, nrm, nrm_m, zones1, zones2, ao, OUT_TEX, log, rot=rot, zones3=zones3)

# ---------------------------------------------------------------- final material for export/previews
for s in (src, mir, rot_r, rot_l, proxy):
    bpy.data.objects.remove(s, do_unlink=True)
for o in targets:
    o.hide_render = False
tnt.nodes.remove(t_emit); tnt.nodes.remove(t_attr); tnt.nodes.remove(tex_node)
for o in targets:
    for name in ('zones1', 'zones2', 'zones3'):
        o.data.color_attributes.remove(o.data.color_attributes[name])
composite.build_material(tm, OUT_TEX, '2k')
C.save_blend(C.work('pista_06_baked.blend'))
log('done')
