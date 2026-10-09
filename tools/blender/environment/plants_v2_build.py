"""Hero basin iteration 2 plants (F4_e): foreground framing clusters, hanging vines for the hero arch, canopy crowns.

Atlases (OpenAI, magenta key, unmixed with chroma_unmix --nocrop [--foliage]) in Plants/Textures/:
  FP_ElephantEar (4 big dark elephant-ear leaves), FP_Bellflower (2 orange bell stems, paddle-leaf fan, single leaf),
  FP_ArchVines (6 hanging vine / moss strips, hanging from the top of the image), FP_Canopy (4 crown masses).
Outputs (Plants/):
  FP_FrameLeft_Clump.fbx, FP_FrameRight_Clump.fbx  near-corner framing clusters, ~4 m; two material slots per mesh:
      slot 0 = FP_ElephantEar atlas, slot 1 = FP_Bellflower atlas (2 draws per cluster, both clusters share them)
  FP_ArchVines.fbx      vine and moss curtains in RS_HeroArch's local space: give it the arch's placement matrix
  FP_Canopy_Clump.fbx   a canopy wall segment (8 crowns in two depth rows, ~38 m wide) for the midground walls;
      its front faces Blender -Y = Unity local +Z: turn +Z toward the camera
  FP_CanopyCrown_A/B/C/D.fbx  single crowns (main card + two crossed cards), same facing (atlas FP_Canopy)
Card conventions as plants_build (ADR 0007): vertex colour R = wind weight, G = B = 0, A = ambient occlusion; normals
bent toward a dome; alpha clip ~0.5, cull off. LODs: _LOD0/1/2.
Usage: blender ... -P plants_v2_build.py -- [frames|vines|canopy ...]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from plants_build import OUT, TEX, preview_material, sprites  # noqa: E402

LOD_RULES = [(1.0, 1.0), (0.65, 0.6), (0.4, 0.34)]


class Builder:
    """Accumulates card geometry: verts, faces (with material slot), per-loop UVs via vertex UVs, colours."""

    def __init__(self):
        self.V, self.F, self.UV, self.C, self.M = [], [], [], [], []

    def grid(self, pts, uvs, cols, nl, nw, slot):
        base = len(self.V)
        self.V += pts; self.UV += uvs; self.C += cols
        for i in range(nl):
            for j in range(nw):
                a = base + i * (nw + 1) + j
                self.F.append((a, a + 1, a + nw + 2, a + nw + 1))
                self.M.append(slot)

    def card(self, s, L, base, yaw, pitch, arch, roll, fold, nl, nw, slot, cross=False, ao=(0.5, 1.0), wind=1.0):
        """Standing card: sprite bottom at `base`, rising at `pitch` from vertical, bending by `arch` along its length."""
        W = L * s['aspect']
        M = (Matrix.Rotation(yaw - math.pi / 2, 4, 'Z') @ Matrix.Rotation(-pitch, 4, 'X')
             @ Matrix.Rotation(roll, 4, 'Y') @ Matrix.Rotation(math.pi / 2, 4, 'X'))
        for c in ([0, math.pi / 2] if cross else [0]):
            Mc = M @ Matrix.Rotation(c, 4, 'Y')
            pts, uvs, cols = [], [], []
            for i in range(nl + 1):
                t = i / nl
                y = sum(math.cos(arch * (q + 0.5) / nl) for q in range(i)) * L / nl
                z = -sum(math.sin(arch * (q + 0.5) / nl) for q in range(i)) * L / nl
                for j in range(nw + 1):
                    u = j / nw
                    x = (u - 0.5) * W
                    f = fold * W * (1 - abs(u - 0.5) * 2) * math.sin(math.pi * min(1, t * 1.3))
                    pts.append(tuple(Mc @ Vector((x, y, z + f)) + base))
                    uvs.append((s['u0'] + (s['u1'] - s['u0']) * u, s['v0'] + (s['v1'] - s['v0']) * t))
                    cols.append((min(1, wind * t ** 1.5 * 1.1), 0, 0, ao[0] + (ao[1] - ao[0]) * min(1, t ** 0.7)))
            self.grid(pts, uvs, cols, nl, nw, slot)

    def hang(self, s, L, top, yaw, sway, nl, slot, ao=(0.55, 0.85), cross=False, widen=1.7):
        """Hanging card: sprite top at `top`, falling straight down with a slight outward sway. The vine strips are
        wispy, so they take a horizontal stretch (`widen`) without looking smeared."""
        W = L * s['aspect'] * widen
        d = Vector((math.cos(yaw), math.sin(yaw), 0))          # card plane normal (horizontal)
        side = Vector((-d.y, d.x, 0))
        for c in ([0, math.pi / 2] if cross else [0]):
            sd = side if c == 0 else d
            nd = d if c == 0 else -side
            pts, uvs, cols = [], [], []
            for i in range(nl + 1):
                t = i / nl
                p = top + Vector((0, 0, -L * t)) + nd * sway * L * t * t
                for j in range(2):
                    pts.append(tuple(p + sd * (j - 0.5) * W * (1 - 0.15 * t)))
                    uvs.append((s['u0'] + (s['u1'] - s['u0']) * j, s['v1'] - (s['v1'] - s['v0']) * t))
                    cols.append((min(1, t ** 1.3), 0, 0, ao[0] + (ao[1] - ao[0]) * t))
            self.grid(pts, uvs, cols, nl, 1, slot)

    def object(self, name, mats, dome_centre=None, dome=0.0):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.V, [], self.F)
        uvl = me.uv_layers.new(name='UVMap')
        for p in me.polygons:
            p.material_index = self.M[p.index]
            p.use_smooth = True
            for li in p.loop_indices:
                uvl.data[li].uv = self.UV[me.loops[li].vertex_index]
        ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
        ca.data.foreach_set('color', np.array(self.C, np.float32).ravel())
        for m in mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        E.link(ob)
        me.update()
        if dome_centre is not None and dome > 0:
            vn = []
            for v in me.vertices:
                dd = (v.co - dome_centre).normalized()
                n = v.normal.copy()
                if n.dot(dd) < 0:
                    n = -n
                vn.append(tuple((n * (1 - dome) + dd * dome).normalized()))
            me.normals_split_custom_set_from_vertices(vn)
        return ob


def mats_for(*atlases):
    return [preview_material(a) for a in atlases]


# ---------------------------------------------------------------- framing clusters
FRAMES = {
    # groups: (atlas slot, sprite ids, count, length m, pitch deg, arch deg, fold, radius, cross, seg)
    'FP_FrameLeft_Clump': dict(seed=11, groups=[
        (0, [0, 1, 2, 3], 12, (1.4, 2.5), (12, 52), (30, 80), 0.1, 0.55, False, (6, 4)),
        (0, [0, 1, 3], 6, (0.9, 1.4), (5, 25), (20, 45), 0.08, 0.2, False, (5, 3)),
        (1, [2, 3], 4, (1.6, 2.3), (5, 25), (12, 35), 0.06, 0.4, False, (5, 2)),
        (1, [0, 1], 3, (1.8, 2.4), (6, 18), (8, 20), 0.0, 0.35, True, (5, 1))]),
    'FP_FrameRight_Clump': dict(seed=13, groups=[
        (0, [0, 1, 2, 3], 7, (1.3, 2.2), (15, 55), (30, 75), 0.1, 0.5, False, (6, 4)),
        (0, [0, 1, 3], 5, (0.9, 1.4), (5, 25), (20, 45), 0.08, 0.2, False, (5, 3)),
        (1, [2, 3], 6, (1.8, 2.7), (4, 22), (10, 30), 0.06, 0.45, False, (5, 2)),
        (1, [0, 1], 6, (2.0, 2.8), (4, 16), (6, 18), 0.0, 0.4, True, (5, 1))]),
}


def build_frames(name):
    E.reset()
    spec = FRAMES[name]
    sp = [sprites(os.path.join(TEX, a + '_BaseColor.png')) for a in ('FP_ElephantEar', 'FP_Bellflower')]
    mats = mats_for('FP_ElephantEar', 'FP_Bellflower')
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        rng = np.random.default_rng(spec['seed'])
        B = Builder()
        for slot, ids, n, L, pitch, arch, fold, radius, cross, seg in spec['groups']:
            for k in range(n):
                r = rng.random(10)
                if k >= max(1, round(n * keep)):
                    continue
                s = sp[slot][ids[k % len(ids)]]
                yaw = 2 * math.pi * (k + 0.6 * r[0]) / n
                root = Vector((math.cos(yaw), math.sin(yaw), 0)) * radius * r[1]
                nl = max(2, round(seg[0] * segr))
                nw = seg[1] if seg[1] == 1 else max(2, 2 * round(seg[1] * segr / 2))
                B.card(s, L[0] + (L[1] - L[0]) * r[2], root, yaw, math.radians(pitch[0] + (pitch[1] - pitch[0]) * r[3]),
                       math.radians(arch[0] + (arch[1] - arch[0]) * r[4]), (r[5] - 0.5) * 0.6, fold, nl, nw, slot,
                       cross=cross, ao=(0.45, 1.0))
        objs.append(B.object(f'{name}_LOD{lod}', mats, Vector((0, 0, -0.6)), 0.35))
    for o in objs:
        print(o.name, 'tris', E.tris(o))
    E.export_fbx(objs, os.path.join(OUT, name + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', name + '_game.blend'), compress=True)
    print('SUMMARY', name, tuple(round(x, 2) for x in objs[0].dimensions), [E.tris(o) for o in objs])


# ---------------------------------------------------------------- hero arch vines
def build_vines():
    """Curtains under the arch's crown and inner faces, moss drapes on the strand sides, in RS_HeroArch space."""
    E.reset()
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', 'RS_HeroArch_game.blend'))
    arch = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.endswith('_LOD0')]
    mouth = bpy.data.objects['RS_HeroArch_WaterfallMouth'].location.copy()
    deps = bpy.context.evaluated_depsgraph_get()
    import bmesh
    bm = bmesh.new()
    for o in arch:
        bm.from_object(o, deps)
    bvh = BVHTree.FromBMesh(bm)
    cand = []
    for f in bm.faces:
        n, c = f.normal, f.calc_center_median()
        if c.z < 7:
            continue
        if n.z < -0.45:
            kind = 0                                     # underside: long curtains
        elif -0.45 <= n.z < 0.15 and n.y > -0.2:
            kind = 1                                     # strand sides facing the camera half: shorter drapes
        else:
            continue
        cand.append((c.copy(), n.copy(), kind, f.calc_area()))
    bm.free()
    for o in list(bpy.context.scene.objects):
        bpy.data.objects.remove(o)
    sp = sprites(os.path.join(TEX, 'FP_ArchVines_BaseColor.png'))
    mats = mats_for('FP_ArchVines')
    rng = np.random.default_rng(29)
    w = np.array([a for *_, a in cand])
    order = rng.choice(len(cand), size=len(cand), replace=False, p=w / w.sum())
    picked = []
    for i in order:
        c, n, kind, _ = cand[i]
        if abs(c.x - mouth.x) < 2.0 and abs(c.y - mouth.y) < 3.0 and c.z > mouth.z - 3:   # keep the fall's mouth clear
            continue
        if any((c - p[0]).length < (0.8 if kind == 0 else 1.3) for p in picked):
            continue
        picked.append((c, n, kind))
        if len(picked) >= 480:
            break
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        r2 = np.random.default_rng(31)
        B = Builder()
        for k, (c, n, kind) in enumerate(picked):
            r = r2.random(6)
            if r[5] > keep:
                continue
            top = c + n * 0.15 + Vector((0, 0, -0.1))
            hit = bvh.ray_cast(top + Vector((0, 0, -0.3)), Vector((0, 0, -1)), 40)
            room = (hit[3] if hit[0] is not None else 40) - 0.2
            if kind == 0:
                s = sp[[0, 1, 3, 5, 2][k % 5]]
                L = min(room, 3 + 12 * r[0] ** 1.2)
            else:
                s = sp[[1, 4, 3, 0][k % 4]]
                L = min(room, 1.8 + 3.2 * r[0])
            if L < 1.2:
                continue
            yaw = math.pi / 2 + (r[1] - 0.5) * math.radians(70)          # cards face the camera half (+Y)
            nl = max(2, round((3 + L / 3.0) * segr))
            B.hang(s, L, top, yaw, 0.04 + 0.06 * r[2], nl, 0, cross=(r[3] < 0.25 and lod == 0))
        objs.append(B.object(f'FP_ArchVines_LOD{lod}', mats))
    for o in objs:
        print(o.name, 'tris', E.tris(o))
    E.export_fbx(objs, os.path.join(OUT, 'FP_ArchVines.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', 'FP_ArchVines_game.blend'), compress=True)
    print('SUMMARY FP_ArchVines', len(picked), 'anchors', [E.tris(o) for o in objs])


# ---------------------------------------------------------------- canopy crowns
CROWNS = {  # sprite, card width m (height follows the sprite)
    'A': dict(sprite=0, W=11.0),     # wide flat-topped emergent
    'B': dict(sprite=1, W=8.0),      # tall rounded
    'C': dict(sprite=2, W=9.0),      # with palm fronds
    'D': dict(sprite=3, W=10.0),     # broad, hanging vines below
}
# wall segment: (crown, x, depth y (Blender +Y = farther), z lift, scale, mirrored)
WALL = [('A', -12.5, 4.0, 1.4, 1.1, False), ('D', -2.5, 4.5, 1.8, 1.15, True), ('B', 7.0, 3.8, 1.2, 1.2, False),
        ('C', 14.0, 4.2, 1.0, 1.0, True), ('B', -15.0, 0.0, 0.0, 0.85, True), ('C', -7.5, -0.6, 0.0, 0.95, False),
        ('D', 2.0, 0.2, 0.0, 0.9, False), ('A', 11.0, -0.4, 0.0, 0.9, True)]


def crown(B, sp, key, offset, keep, segr, scale=1.0, mirror=False):
    """One crown: a main card facing the camera side (Blender -Y = Unity +Z) and two smaller crossed cards at +-68 deg
    set behind it (parallax and side views; nearer to the camera they showed as dark wedges); sprite bottom sunk 0.4 m into the ground. Cards bow a little toward the camera."""
    spec = CROWNS[key]
    s = dict(sp[spec['sprite']])
    if mirror:
        s['u0'], s['u1'] = s['u1'], s['u0']
    W = spec['W'] * scale
    H = W / s['aspect']
    seg = max(1, round(2 * segr))
    o = Vector(offset)
    cards = [(0.0, 1.0, 0.0), (math.radians(68), 0.62, 0.32), (math.radians(-68), 0.62, 0.32)]   # crossed: parallax only
    if keep < 0.5:
        cards = cards[:1]
    for yaw, k, back in cards:
        Wc, Hc = W * k, H * k
        side = Vector((math.cos(yaw), math.sin(yaw), 0))
        face = Vector((-math.sin(yaw), math.cos(yaw), 0)) * -1          # toward -Y for the main card
        base = o + Vector((0, back * W, -0.4))
        pts, uvs, cols = [], [], []
        for i in range(seg + 1):
            t = i / seg
            for j in range(seg + 1):
                u = j / seg
                bul = 0.08 * Wc * math.sin(math.pi * t) * math.sin(math.pi * u)
                q = base + side * (u - 0.5) * Wc + Vector((0, 0, t * Hc)) + face * bul
                pts.append(tuple(q))
                uvs.append((s['u0'] + (s['u1'] - s['u0']) * u, s['v0'] + (s['v1'] - s['v0']) * t))
                cols.append((0.15 + 0.35 * t, 0, 0, 0.5 + 0.5 * t))
        B.grid(pts, uvs, cols, seg, seg, 0)
    return o + Vector((0, 0, H * 0.35))


def build_canopy():
    sp = sprites(os.path.join(TEX, 'FP_Canopy_BaseColor.png'))
    for key in CROWNS:
        E.reset()
        mats = mats_for('FP_Canopy')
        objs = []
        for lod, (keep, segr) in enumerate(LOD_RULES):
            B = Builder()
            c = crown(B, sp, key, (0, 0, 0), keep, segr)
            objs.append(B.object(f'FP_CanopyCrown_{key}_LOD{lod}', mats, c + Vector((0, 2, -3)), 0.6))
        E.export_fbx(objs, os.path.join(OUT, f'FP_CanopyCrown_{key}.fbx'))
        print('SUMMARY', f'FP_CanopyCrown_{key}', tuple(round(x, 1) for x in objs[0].dimensions), [E.tris(o) for o in objs])
    E.reset()
    mats = mats_for('FP_Canopy')
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        B = Builder()
        for key, x, y, z, sc, mir in WALL:
            if lod == 2 and y < 1:          # far LOD: back row only
                continue
            crown(B, sp, key, (x, y, z), keep, segr, sc, mir)
        objs.append(B.object(f'FP_Canopy_Clump_LOD{lod}', mats, Vector((0, 4, -2)), 0.6))
    E.export_fbx(objs, os.path.join(OUT, 'FP_Canopy_Clump.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', 'FP_Canopy_Clump_game.blend'), compress=True)
    print('SUMMARY FP_Canopy_Clump', tuple(round(x, 1) for x in objs[0].dimensions), [E.tris(o) for o in objs])


if __name__ == '__main__':
    a = E.args() or ['frames', 'vines', 'canopy']
    if 'frames' in a:
        for nm in FRAMES:
            build_frames(nm)
    if 'canopy' in a:
        build_canopy()
    if 'vines' in a:
        build_vines()
