"""Foreground framing plants (F4_e basin): leaf/flower cards from the OpenAI atlases.

Atlases: UnityProject/Assets/_Game/Art/Environment/Plants/Textures/<atlas>_BaseColor.png (RGBA, alpha cut) + _Normal.
Each sprite is found by connected components of alpha; cards are bent (arch), folded along the midrib and placed
around the clump centre. Vertex colours (ADR 0007): R = wind weight (base 0 -> tip 1), G = 0, B = 0 (canopy cover is
painted at merge time), A = ambient occlusion (dark at the base). Normals are blended toward a dome around the clump
(soft, leafy shading). Output: Plants/<name>.fbx with <name>_LOD0/1/2.
Usage: blender ... -P plants_build.py -- [name ...]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402

OUT = os.path.join(E.UNITY_ENV, 'Plants')
TEX = os.path.join(OUT, 'Textures')

PLANTS = {
    # sprites: indices into the atlas sprite list (sorted left->right, top->bottom); n: card count
    'FP_Broadleaf_Clump': dict(atlas='FP_Broadleaf', seed=3, groups=[
        dict(sprites=[0, 1, 2, 3], n=11, length=(0.75, 1.45), pitch=(30, 72), arch=(25, 55), fold=0.09,
             base_h=(0.15, 0.85), radius=0.25, petiole=True, seg=(5, 3))], dome=0.25),
    'FP_Bellcap_Clump': dict(atlas='FP_Bellcap', seed=5, groups=[
        dict(sprites=[0, 1], n=6, length=(0.95, 1.55), pitch=(4, 22), arch=(8, 22), fold=0.0, base_h=(0, 0),
             radius=0.22, cross=True, seg=(4, 1)),
        dict(sprites=[2, 3, 4], n=10, length=(0.55, 1.05), pitch=(18, 55), arch=(20, 50), fold=0.05,
             base_h=(0, 0), radius=0.15, seg=(4, 2))], dome=0.45),
    'FP_Fern_Clump': dict(atlas='FP_Fronds', seed=7, groups=[
        dict(sprites=[0, 2], n=13, length=(0.8, 1.35), pitch=(35, 70), arch=(40, 85), fold=0.06, base_h=(0, 0.05),
             radius=0.08, seg=(5, 2))], dome=0.5),
}
LOD_RULES = [(1.0, 1.0), (0.7, 0.6), (0.45, 0.34)]      # (card keep ratio, segment ratio)


def sprites(img_path):
    from PIL import Image
    from scipy import ndimage
    a = np.asarray(Image.open(img_path).convert('RGBA'))[..., 3] > 40
    lab, n = ndimage.label(ndimage.binary_dilation(a, iterations=2))
    H, W = a.shape
    boxes = []
    for sl in ndimage.find_objects(lab):
        ys, xs = sl
        if (ys.stop - ys.start) * (xs.stop - xs.start) < 2000:
            continue
        boxes.append((xs.start, ys.start, xs.stop, ys.stop))
    # rows first (by vertical centre band), then left -> right
    boxes.sort(key=lambda b: (round(((b[1] + b[3]) / 2) / (H / 2.2)), b[0]))
    out = []
    for x0, y0, x1, y1 in boxes:
        sub = a[y0:y1, x0:x1]
        bottom = np.where(sub.any(1))[0].max()
        stem_x = int(np.mean(np.where(sub[max(0, bottom - 6):bottom + 1].any(0))[0]))
        out.append(dict(u0=x0 / W, u1=x1 / W, v0=1 - y1 / H, v1=1 - y0 / H, aspect=(x1 - x0) / (y1 - y0),
                        stem_u=(x0 + stem_x) / W))
    return out


def build_mesh(name, spec, sp, keep, segr):
    rng = np.random.default_rng(spec['seed'])
    V, F, UV, COL = [], [], [], []

    def add_quad_grid(pts, uvs, cols, nl, nw):
        base = len(V)
        V.extend(pts); UV.extend(uvs); COL.extend(cols)
        for i in range(nl):
            for j in range(nw):
                a = base + i * (nw + 1) + j
                F.append((a, a + 1, a + nw + 2, a + nw + 1))

    for g in spec['groups']:
        if 'trunk' in g:
            # one slim stem from the ground to the crown, textured with the frond's stem texels
            th, tr = g['trunk']
            s0 = sp[g['sprites'][0]]
            ring = 7 if segr > 0.5 else 4
            pts, uvs, cols = [], [], []
            for i in range(5):
                t = i / 4
                c = Vector((0.08 * math.sin(t * 2.2), 0.05 * t * t, th * t))
                for j in range(ring + 1):
                    a = 2 * math.pi * j / ring
                    pts.append(tuple(c + Vector((math.cos(a), math.sin(a), 0)) * tr * (1.25 - 0.4 * t)))
                    uvs.append((s0['stem_u'] + (j / ring - 0.5) * 0.003, s0['v0'] + 0.004 + 0.02 * t))
                    cols.append((0.0, 0, 0, 0.6 + 0.4 * t))
            add_quad_grid(pts, uvs, cols, 4, ring)
        for k in range(g['n']):
            r = rng.random(12)        # draw every random number so LODs keep the same placement
            if k >= max(1, round(g['n'] * keep)):
                continue
            s = sp[g['sprites'][k % len(g['sprites'])]]
            L = g['length'][0] + (g['length'][1] - g['length'][0]) * r[0]
            W = L * s['aspect']
            yaw = 2 * math.pi * (k + r[1] * 0.6) / g['n']
            pitch = math.radians(g['pitch'][0] + (g['pitch'][1] - g['pitch'][0]) * r[2])
            arch = math.radians(g['arch'][0] + (g['arch'][1] - g['arch'][0]) * r[3])
            bh = g['base_h'][0] + (g['base_h'][1] - g['base_h'][0]) * r[4]
            root = Vector((math.cos(yaw) * g['radius'] * r[5], math.sin(yaw) * g['radius'] * r[5], 0))
            base = root + Vector((math.cos(yaw), math.sin(yaw), 0)) * bh * math.sin(pitch) * 0.5 + Vector((0, 0, bh))
            nl = max(1, round(g['seg'][0] * segr))
            nw = max(1, round(g['seg'][1] * segr)) if g['seg'][1] > 1 else 1
            if nw % 2 == 1 and g['fold'] > 0 and nw > 1:
                nw += 1
            roll = (r[6] - 0.5) * 0.6
            M = (Matrix.Rotation(yaw - math.pi / 2, 4, 'Z') @ Matrix.Rotation(-pitch, 4, 'X')
                 @ Matrix.Rotation(roll, 4, 'Y') @ Matrix.Rotation(math.pi / 2, 4, 'X'))   # card starts vertical
            for card in ([0, math.pi / 2] if g.get('cross') else [0]):
                Mc = M @ Matrix.Rotation(card, 4, 'Y')       # crossed card: rotate about the card's length axis
                pts, uvs, cols = [], [], []
                for i in range(nl + 1):
                    t = i / nl
                    # arch: bend the leaf down progressively along its length
                    ang = arch * t
                    y = sum(math.cos(arch * (q + 0.5) / nl) for q in range(i)) * L / nl
                    z = -sum(math.sin(arch * (q + 0.5) / nl) for q in range(i)) * L / nl
                    for j in range(nw + 1):
                        u = j / nw
                        x = (u - 0.5) * W
                        fold = g['fold'] * W * (1 - abs(u - 0.5) * 2) * math.sin(math.pi * min(1, t * 1.3))
                        p = Mc @ Vector((x, y, z + fold)) + base
                        pts.append(tuple(p))
                        uvs.append((s['u0'] + (s['u1'] - s['u0']) * u, s['v0'] + (s['v1'] - s['v0']) * t))
                        cols.append((min(1, t ** 1.5 * 1.1), 0, 0, 0.55 + 0.45 * min(1, (t + bh) ** 0.7)))
                add_quad_grid(pts, uvs, cols, nl, nw)
            if g.get('petiole') and bh > 0.05:
                # thin stem from the ground to the leaf base, textured with the sprite's stem texels
                ring = 5 if segr > 0.5 else 3
                rad = 0.012 + 0.01 * L
                p0, p1 = root, base
                d = (p1 - p0).normalized()
                n1 = d.cross(Vector((0, 0, 1))).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
                n2 = d.cross(n1)
                pts, uvs, cols = [], [], []
                for i in range(3):
                    t = i / 2
                    c = p0.lerp(p1, t) + Vector((0, 0, 0.04 * math.sin(math.pi * t)))
                    for j in range(ring + 1):
                        a = 2 * math.pi * j / ring
                        pts.append(tuple(c + (n1 * math.cos(a) + n2 * math.sin(a)) * rad * (1 - 0.3 * t)))
                        uvs.append((s['stem_u'] + (j / ring - 0.5) * 0.002, s['v0'] + 0.004 + 0.01 * t))
                        cols.append((0.0, 0, 0, 0.5 + 0.3 * t))
                add_quad_grid(pts, uvs, cols, 2, ring)
    me = bpy.data.meshes.new(name)
    me.from_pydata(V, [], F)
    uvl = me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        for li in p.loop_indices:
            uvl.data[li].uv = UV[me.loops[li].vertex_index]
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    ca.data.foreach_set('color', np.array(COL, np.float32).ravel())
    ob = bpy.data.objects.new(name, me)
    E.link(ob)
    # dome normals: blend face normals toward the direction from a point below the clump centre
    me.update()
    centre = Vector((0, 0, -0.4 if spec['dome'] < 0.45 else -0.8))
    blend = spec['dome']
    vn = []
    for v in me.vertices:
        d = (v.co - centre).normalized()
        n = v.normal.copy()
        if n.dot(d) < 0:
            n = -n
        vn.append(tuple((n * (1 - blend) + d * blend).normalized()))
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set_from_vertices(vn)
    bm_tris = sum(len(p.vertices) - 2 for p in me.polygons)
    return ob, bm_tris


def preview_material(atlas):
    m = bpy.data.materials.new('MI_' + atlas)
    m.use_nodes = True
    nt = m.node_tree
    bs = nt.nodes['Principled BSDF']
    tb = nt.nodes.new('ShaderNodeTexImage')
    tb.image = bpy.data.images.load(os.path.join(TEX, atlas + '_BaseColor.png'))
    tn = nt.nodes.new('ShaderNodeTexImage')
    tn.image = bpy.data.images.load(os.path.join(TEX, atlas + '_Normal.png'))
    tn.image.colorspace_settings.name = 'Non-Color'
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(tb.outputs['Color'], bs.inputs['Base Color'])
    nt.links.new(tb.outputs['Alpha'], bs.inputs['Alpha'])
    nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    bs.inputs['Roughness'].default_value = 0.55
    bs.inputs['Subsurface Weight'].default_value = 0.15
    bs.inputs['Transmission Weight'].default_value = 0.0
    m.blend_method = 'CLIP'
    return m


def build(name):
    E.reset()
    spec = PLANTS[name]
    sp = sprites(os.path.join(TEX, spec['atlas'] + '_BaseColor.png'))
    print(name, 'sprites', len(sp), [round(s['aspect'], 2) for s in sp])
    mat = preview_material(spec['atlas'])
    objs = []
    for i, (keep, segr) in enumerate(LOD_RULES):
        ob, t = build_mesh(f'{name}_LOD{i}', spec, sp, keep, segr)
        ob.data.materials.append(mat)
        objs.append(ob)
        print(ob.name, 'tris', E.tris(ob))
    E.export_fbx(objs, os.path.join(OUT, name + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', name + '_game.blend'), compress=True)
    print('SUMMARY', name, tuple(round(x, 2) for x in objs[0].dimensions), [E.tris(o) for o in objs])


if __name__ == '__main__':
    a = E.args()
    for nm in (a or list(PLANTS)):
        build(nm)
