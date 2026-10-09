"""RS_HeroArch v2, stage 2: game mesh, two 2k texture sets, LODs, FBX.

Input:  work/rootstone/RS_HeroArch_high.blend (heroarch_v2_high.py)
Output: Rootstone/RS_HeroArch.fbx with RS_HeroArch_A_LOD0..2 and RS_HeroArch_B_LOD0..2 (A = Blender x < SPLIT_X =
        Unity local +X half; each half one material) and the empty RS_HeroArch_WaterfallMouth.
        Rootstone/Textures/RS_HeroArch_A_{BaseColor,Normal,ARM}.png and _B_ (2048 each, same channel rules as the kit).
LODs are decimated on the whole arch and then split, so the two halves meet without cracks at every LOD; custom
normals are set on the whole mesh before the split, so there is no shading seam where they meet.
Texel density is weighted toward what the F4_e camera sees (Blender +Y = Unity local -Z): faces facing away
(normal y < -0.3) get 0.55x, up-facing tops 0.7x, everything else 1x; times 0.4-1x by openness (deep crevices
between strands get fewer texels). Weights are rounded to 0.1 so charts stay large.
Usage: blender ... -P heroarch_v2_bake.py
"""
import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_bake import DETAIL_TILE_M, OUT, stone_material  # noqa: E402
from heroarch_v2_high import NAME, SPEC  # noqa: E402

LOD_TRIS = (14000, 6300, 2100)
RES = 2048
SPLIT_X = -1.0
HALVES = ('A', 'B')
SCALE = 1.1            # texture feature scale (rootstone_bake.stone_material)


def face_weight(n):
    if n.y < -0.3:
        return 0.55
    if n.z > 0.55:
        return 0.7
    return 1.0


def openness(ob, dirs=24, dist=8.0):
    """Per face: fraction of hemisphere rays (cosine-ish spread) that escape within `dist` m. Deep crevices between
    strands are dark and barely seen, so they get fewer texels."""
    deps = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(ob, deps)
    rng = np.random.default_rng(5)
    v = rng.normal(size=(dirs, 3))
    v /= np.linalg.norm(v, axis=1, keepdims=True)
    out = []
    for p in ob.data.polygons:
        n = np.array(p.normal[:])
        hits, cnt = 0, 0
        for d in v:
            if d @ n < 0:
                d = -d
            d = 0.6 * d + 0.4 * n
            d /= np.linalg.norm(d)
            cnt += 1
            if bvh.ray_cast(p.center + p.normal * 0.03, Vector(d), dist)[0] is not None:
                hits += 1
        out.append(1 - hits / cnt)
    return np.array(out)


def set_targets(low, imgs):
    for mat, img in zip(low.data.materials, imgs):
        nt = mat.node_tree
        node = nt.nodes.get('BAKE_TARGET') or nt.nodes.new('ShaderNodeTexImage')
        node.name = 'BAKE_TARGET'
        node.image = img
        nt.nodes.active = node


def bake2(high, low, kind, imgs, ext, ray, samples, **kw):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    E.rays_off(low)
    set_targets(low, imgs)
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = ext
    sc.render.bake.max_ray_distance = ray
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    E.set_active(low, high)
    args = dict(type=kind, use_clear=True, margin=8)
    args.update(kw)
    bpy.ops.object.bake(**args)


def split_halves(ob, lod):
    """Custom normals from the whole mesh, then one object per material: <NAME>_A_LODn, <NAME>_B_LODn."""
    me = ob.data
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set_from_vertices([v.normal[:] for v in me.vertices])
    E.set_active(ob)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')
    bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.context.scene.objects if o.type == 'MESH' and (o == ob or o.name.startswith(ob.name + '.'))]
    out = []
    for o in parts:
        h = 'A' if o.data.materials[0].name.endswith('_A') else 'B'
        o.name = o.data.name = f'{NAME}_{h}_LOD{lod}'
        E.box_uv2(o, DETAIL_TILE_M)
        out.append(o)
    return sorted(out, key=lambda o: o.name)


def build():
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', NAME + '_high.blend'))
    sc = E.gpu_cycles(8)
    high = bpy.data.objects[NAME + '_high']
    for p in high.data.polygons:
        p.use_smooth = True
    high.data.materials.clear()
    high.data.materials.append(stone_material(SCALE, crevice=2.2, streak=2.2, moss=1.6))

    low = E.copy_obj(high, NAME + '_LOD0')
    E.decimate_to(low, LOD_TRIS[0])
    E.triangulate(low)
    for a in list(low.data.color_attributes):
        low.data.color_attributes.remove(a)
    low.data.materials.clear()
    mats = []
    for h in HALVES:
        m = bpy.data.materials.new(f'MI_{NAME}_{h}')
        m.use_nodes = True
        low.data.materials.append(m)
        mats.append(m)
    me = low.data
    half = [[], []]
    wts = [[], []]
    op = openness(low)
    print('openness mean', round(float(op.mean()), 2), 'share < 0.4:', round(float(np.mean(op < 0.4)), 2))
    for p in me.polygons:
        k = 0 if p.center.x < SPLIT_X else 1
        p.material_index = k
        half[k].append(p.index)
        wts[k].append(round(face_weight(p.normal) * (0.4 + 0.6 * min(1.0, op[p.index] / 0.7)), 1))
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name='UVMap')
    dens = [E.xatlas_faces(low, half[k], RES, padding_px=6, weights=wts[k]) for k in range(2)]
    print('LOD0 tris', E.tris(low), 'faces per half', [len(h) for h in half], 'density px/m', [round(d, 1) for d in dens])

    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = 'bake_ground'
    ext, ray = 0.5, 1.2

    def imgs(tag, non_color=False):
        return [E.new_image(f'{NAME}_{h}_{tag}', RES, non_color=non_color) for h in HALVES]

    base = imgs('BaseColor')
    bake2(high, low, 'DIFFUSE', base, ext, ray, 16, pass_filter={'COLOR'})
    nrm = imgs('Normal', True)
    bake2(high, low, 'NORMAL', nrm, ext, ray, 8, normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y',
          normal_b='POS_Z')
    ao = imgs('ao', True)
    sc.world = bpy.data.worlds.new('w')
    keep = list(high.data.materials)          # AO through a colour bake (see bakekit.bake_ao): the AO pass is unreliable
    aom = bpy.data.materials.new('ao_src'); aom.use_nodes = True
    aon = aom.node_tree.nodes.new('ShaderNodeAmbientOcclusion')
    aon.samples, aon.only_local = 32, False
    aon.inputs['Distance'].default_value = 3.0
    aom.node_tree.links.new(aon.outputs['AO'], aom.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    high.data.materials.clear(); high.data.materials.append(aom)
    bake2(high, low, 'DIFFUSE', ao, ext, ray, 96, pass_filter={'COLOR'})
    high.data.materials.clear()
    for k in keep:
        high.data.materials.append(k)
    rgh = imgs('rough', True)
    bake2(high, low, 'ROUGHNESS', rgh, ext, ray, 4)
    bpy.data.objects.remove(ground)

    tdir = os.path.join(OUT, 'Textures')
    arms = []
    for k, h in enumerate(HALVES):
        a = E.pixels(ao[k])[..., 0]
        r = E.pixels(rgh[k])[..., 0]
        arm = E.new_image(f'{NAME}_{h}_ARM', RES, non_color=True)
        o = np.zeros((RES, RES, 4), np.float32)
        o[..., 0] = np.clip(a, 0, 1) ** 1.5
        o[..., 1] = r
        o[..., 3] = 1
        E.set_pixels(arm, o)
        arms.append(arm)
        E.save_png(base[k], os.path.join(tdir, f'{NAME}_{h}_BaseColor.png'))
        E.save_png(nrm[k], os.path.join(tdir, f'{NAME}_{h}_Normal.png'))
        E.save_png(arm, os.path.join(tdir, f'{NAME}_{h}_ARM.png'))

    # preview materials (baked maps) on the game mesh
    for k, mat in enumerate(mats):
        nt = mat.node_tree
        nt.nodes.remove(nt.nodes['BAKE_TARGET'])
        bsdf = nt.nodes['Principled BSDF']
        tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = base[k]
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = nrm[k]
        ta = nt.nodes.new('ShaderNodeTexImage'); ta.image = arms[k]
        nm = nt.nodes.new('ShaderNodeNormalMap')
        sp = nt.nodes.new('ShaderNodeSeparateColor')
        aom = nt.nodes.new('ShaderNodeMix'); aom.data_type = 'RGBA'; aom.blend_type = 'MULTIPLY'
        aom.inputs[0].default_value = 1.0
        nt.links.new(tb.outputs[0], aom.inputs[6]); nt.links.new(ta.outputs[0], sp.inputs[0])
        cg = nt.nodes.new('ShaderNodeCombineColor')
        for i in range(3):
            nt.links.new(sp.outputs[0], cg.inputs[i])
        nt.links.new(cg.outputs[0], aom.inputs[7])
        nt.links.new(aom.outputs[2], bsdf.inputs['Base Color'])
        nt.links.new(tn.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bsdf.inputs['Normal'])
        nt.links.new(sp.outputs[1], bsdf.inputs['Roughness'])

    # waterfall mouth: just under the crown's underside above the v1 mouth centre (ray up from the ground)
    deps = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(low, deps)
    c = SPEC['mouth']['c']
    best = None
    for dx in (0, -1.5, 1.5, -3, 3):
        hit = bvh.ray_cast(Vector((c[0] + dx, c[1], 1.0)), Vector((0, 0, 1)))
        if hit[0] is not None and 20 < hit[0].z < 45:
            best = hit[0]
            break
    assert best is not None, 'no crown underside above the mouth'
    em = bpy.data.objects.new(NAME + '_WaterfallMouth', None)
    em.location = (best.x, best.y, best.z - 0.4)
    E.link(em)
    print('WaterfallMouth', tuple(round(v, 2) for v in em.location))

    lods = [E.copy_obj(low, f'{NAME}_L{i}') for i in (1, 2)]
    for o, t in zip(lods, LOD_TRIS[1:]):
        E.decimate_to(o, t)
    bpy.data.objects.remove(high)
    objs = []
    for i, o in enumerate([low] + lods):
        objs += split_halves(o, i)
    for o in objs:
        print(o.name, 'tris', E.tris(o))
    objs.append(em)
    E.export_fbx(objs, os.path.join(OUT, NAME + '.fbx'))
    for im in base + nrm + arms:
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_game.blend'), compress=True)
    lod0 = [o for o in objs if o.name.endswith('_LOD0')]
    pts = [o.matrix_world @ Vector(v) for o in lod0 for v in o.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 1) for i in range(3)]
    print('SUMMARY', NAME, size, [sum(E.tris(o) for o in objs if o.type == 'MESH' and o.name.endswith(f'_LOD{i}'))
                                  for i in range(3)], 'px/m', [round(d, 1) for d in dens])


if __name__ == '__main__':
    build()
