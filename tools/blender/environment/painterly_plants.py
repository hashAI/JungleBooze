"""Painterly (F4_f, option D trial) plants for the hero basin, next to the realistic ones (same card rules as
plants_v2_build: vertex colour R = wind, A = AO; normals bent toward a dome; alpha clip ~0.5, cull off).

Atlases (OpenAI painted with F4_f as style reference, magenta key, tools/art/painterly_finish.py) in Plants/Textures/:
  FP_BigLeaf_P (4 glossy broad leaves, dark core -> light tips), FP_Bellflower_P (2 orange bell stems, 2 paddle
  leaves), FP_Fronds_P (3 palm fronds, 3 fern fronds), FP_Canopy_P (4 crowns, same order as FP_Canopy),
  FP_ArchVines_P (6 hanging leafy vine strips).
Outputs (Plants/):
  FP_FrameLeft_P_Clump, FP_FrameRight_P_Clump   near-corner framing clusters (layout as the realistic ones);
      two slots: 0 = FP_BigLeaf_P, 1 = FP_Bellflower_P
  FP_Bellflower_P_Clump   small bell-flower clump (bells + paddle leaves, 1 slot FP_Bellflower_P) for scattering
  FP_PalmFern_P_Clump     palm + fern clump (~3 m, FP_Fronds_P);  FP_Fern_P_Clump  low fern clump (FP_Fronds_P)
  FP_Canopy_P_Clump, FP_CanopyCrown_P_A..D   canopy wall segment and single crowns (FP_Canopy_P), facing as before
  FP_ArchVines_P          leafy curtains (slot 0 FP_ArchVines_P) and leafy crests on top (slot 1 FP_Canopy_P) on
                          RS_HeroArch_P, in its local space (needs painterly_rock.py RS_HeroArch first)
Usage: blender ... -P painterly_plants.py -- [frames|clumps|canopy|vines ...]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from plants_build import OUT, TEX, sprites  # noqa: E402
from plants_v2_build import CROWNS, FRAMES, LOD_RULES, WALL, Builder, crown, mats_for  # noqa: E402

BIG, BELL, FRONDS, CANOPY, VINES = 'FP_BigLeaf_P', 'FP_Bellflower_P', 'FP_Fronds_P', 'FP_Canopy_P', 'FP_ArchVines_P'

# (slot, sprite ids, count, length m, pitch deg, arch deg, fold, radius, cross, seg[, lift m])  as plants_v2_build.FRAMES;
# lift raises the card bases (palm fronds spring from a short hidden crown, not from the ground)
CLUMPS = {
    'FP_Bellflower_P_Clump': dict(seed=17, atlases=[BELL], dome=0.4, groups=[
        (0, [2, 3], 6, (1.0, 1.5), (8, 30), (12, 35), 0.06, 0.25, False, (5, 2)),
        (0, [0, 1], 4, (1.2, 1.7), (4, 16), (6, 18), 0.0, 0.2, True, (5, 1))]),
    'FP_PalmFern_P_Clump': dict(seed=19, atlases=[FRONDS], dome=0.45, groups=[
        (0, [0, 1, 2], 9, (1.9, 2.6), (12, 45), (55, 95), 0.08, 0.15, False, (6, 2), 0.15),
        (0, [0, 1, 2], 4, (1.4, 1.8), (2, 12), (25, 45), 0.06, 0.1, False, (5, 2), 0.1),
        (0, [3, 4, 5], 9, (0.9, 1.4), (30, 70), (35, 75), 0.06, 0.55, False, (5, 2))]),
    'FP_Fern_P_Clump': dict(seed=23, atlases=[FRONDS], dome=0.5, groups=[
        (0, [3, 4, 5], 13, (0.8, 1.3), (35, 72), (40, 85), 0.06, 0.08, False, (5, 2))]),
}


def clump(name, atlases, groups, seed, dome, dome_z=-0.6):
    E.reset()
    sp = [sprites(os.path.join(TEX, a + '_BaseColor.png')) for a in atlases]
    for a, s in zip(atlases, sp):
        print(a, 'sprites', len(s))
    mats = mats_for(*atlases)
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        rng = np.random.default_rng(seed)
        B = Builder()
        for grp in groups:
            slot, ids, n, L, pitch, arch, fold, radius, cross, seg = grp[:10]
            lift = grp[10] if len(grp) > 10 else 0.0
            for k in range(n):
                r = rng.random(10)
                if k >= max(1, round(n * keep)):
                    continue
                s = sp[slot][ids[k % len(ids)]]
                yaw = 2 * math.pi * (k + 0.6 * r[0]) / n
                root = Vector((math.cos(yaw), math.sin(yaw), 0)) * radius * r[1] + Vector((0, 0, lift))
                nl = max(2, round(seg[0] * segr))
                nw = seg[1] if seg[1] == 1 else max(2, 2 * round(seg[1] * segr / 2))
                B.card(s, L[0] + (L[1] - L[0]) * r[2], root, yaw, math.radians(pitch[0] + (pitch[1] - pitch[0]) * r[3]),
                       math.radians(arch[0] + (arch[1] - arch[0]) * r[4]), (r[5] - 0.5) * 0.6, fold, nl, nw, slot,
                       cross=cross, ao=(0.45, 1.0))
        objs.append(B.object(f'{name}_LOD{lod}', mats, Vector((0, 0, dome_z)), dome))
    E.export_fbx(objs, os.path.join(OUT, name + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', name + '_game.blend'), compress=True)
    print('SUMMARY', name, tuple(round(x, 2) for x in objs[0].dimensions), [E.tris(o) for o in objs])


def build_frames():
    for nm, spec in FRAMES.items():
        clump(nm.replace('_Clump', '_P_Clump'), [BIG, BELL], spec['groups'], spec['seed'], 0.35)


def build_clumps():
    for nm, spec in CLUMPS.items():
        clump(nm, spec['atlases'], spec['groups'], spec['seed'], spec['dome'])


def build_canopy():
    sp = sprites(os.path.join(TEX, CANOPY + '_BaseColor.png'))
    print(CANOPY, 'sprites', len(sp))
    for key in CROWNS:
        E.reset()
        mats = mats_for(CANOPY)
        objs = []
        for lod, (keep, segr) in enumerate(LOD_RULES):
            B = Builder()
            c = crown(B, sp, key, (0, 0, 0), keep, segr)
            objs.append(B.object(f'FP_CanopyCrown_P_{key}_LOD{lod}', mats, c + Vector((0, 2, -3)), 0.6))
        E.export_fbx(objs, os.path.join(OUT, f'FP_CanopyCrown_P_{key}.fbx'))
        print('SUMMARY', f'FP_CanopyCrown_P_{key}', tuple(round(x, 1) for x in objs[0].dimensions), [E.tris(o) for o in objs])
    E.reset()
    mats = mats_for(CANOPY)
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        B = Builder()
        for key, x, y, z, sc, mir in WALL:
            if lod == 2 and y < 1:
                continue
            crown(B, sp, key, (x, y, z), keep, segr, sc, mir)
        objs.append(B.object(f'FP_Canopy_P_Clump_LOD{lod}', mats, Vector((0, 4, -2)), 0.6))
    E.export_fbx(objs, os.path.join(OUT, 'FP_Canopy_P_Clump.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', 'FP_Canopy_P_Clump_game.blend'), compress=True)
    print('SUMMARY FP_Canopy_P_Clump', tuple(round(x, 1) for x in objs[0].dimensions), [E.tris(o) for o in objs])


def build_vines(count=600, crests=330):
    """F4_f: lush leafy curtains under the crown and over the strand sides facing the camera (as FP_ArchVines, on the
    painterly arch), plus leafy crests (slot 1, FP_Canopy_P crowns as small bushes) sitting on the up-facing tops of
    the crown and upper strands, the way F4_f's arch carries shrubs and palms on top.
    Vine atlas sprites (left -> right): 0, 1, 4, 5 leafy curtains/creepers; 2, 3 lianas; 6, 7 thin strands."""
    E.reset()
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', 'RS_HeroArch_P_game.blend'))
    arch = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.endswith('_LOD0')]
    mouth = bpy.data.objects['RS_HeroArch_P_WaterfallMouth'].location.copy()
    deps = bpy.context.evaluated_depsgraph_get()
    import bmesh
    bm = bmesh.new()
    for o in arch:
        bm.from_object(o, deps)
    bvh = BVHTree.FromBMesh(bm)
    cand = []
    for f in bm.faces:
        n, c = f.normal, f.calc_center_median()
        if c.z < 6:
            continue
        if n.z < -0.45:
            kind = 0                                     # underside: long curtains
        elif -0.45 <= n.z < 0.35 and n.y > -0.2:
            kind = 1                                     # strand sides facing the camera half: drapes
        elif n.z >= 0.55 and c.z > 14:
            kind = 2                                     # tops of the crown and upper strands: leafy crests
        elif n.z >= 0.1 and c.z > 30 and n.y > 0.2:
            kind = 2                                     # upper crown face toward the camera: F4_f's leafy crown
        else:
            continue
        cand.append((c.copy(), n.copy(), kind, f.calc_area()))
    bm.free()
    for o in list(bpy.context.scene.objects):
        bpy.data.objects.remove(o)
    sp = sprites(os.path.join(TEX, VINES + '_BaseColor.png'))
    print(VINES, 'sprites', len(sp))
    spc = sprites(os.path.join(TEX, CANOPY + '_BaseColor.png'))
    mats = mats_for(VINES, CANOPY)
    rng = np.random.default_rng(37)
    w = np.array([a for *_, a in cand])
    order = rng.choice(len(cand), size=len(cand), replace=False, p=w / w.sum())
    picked, tops = [], []
    for i in order:
        c, n, kind, _ = cand[i]
        if kind == 2:
            if len(tops) < crests and all((c - p[0]).length > 2.0 for p in tops):
                tops.append((c, n, kind))
            continue
        if abs(c.x - mouth.x) < 2.0 and abs(c.y - mouth.y) < 3.0 and c.z > mouth.z - 3:
            continue
        if len(picked) >= count or any((c - p[0]).length < (0.9 if kind == 0 else 1.1) for p in picked):
            continue
        picked.append((c, n, kind))
    objs = []
    for lod, (keep, segr) in enumerate(LOD_RULES):
        r2 = np.random.default_rng(41)
        B = Builder()
        for k, (c, n, kind) in enumerate(picked):
            r = r2.random(6)
            if r[5] > keep:
                continue
            top = c + n * 0.15 + Vector((0, 0, -0.1))
            hit = bvh.ray_cast(top + Vector((0, 0, -0.3)), Vector((0, 0, -1)), 40)
            room = (hit[3] if hit[0] is not None else 40) - 0.2
            if kind == 0:
                sid = [0, 4, 1, 5, 0, 4, 2, 7][k % 8]
                L = min(room, 3 + 10 * r[0] ** 1.2)
            else:
                sid = [1, 4, 5, 0][k % 4]
                L = min(room, 2.0 + 4.0 * r[0])
            if L < 1.2:
                continue
            s = sp[sid]
            yaw = math.pi / 2 + (r[1] - 0.5) * math.radians(70)
            nl = max(2, round((3 + L / 3.0) * segr))
            B.hang(s, L, top, yaw, 0.04 + 0.06 * r[2], nl, 0, cross=(r[3] < 0.25 and lod == 0),
                   widen=2.0 if sid in (0, 1, 4, 5) else 1.25)
        for k, (c, n, _) in enumerate(tops):
            r = r2.random(4)
            if r[3] > keep:
                continue
            q = dict(spc[k % len(spc)])
            if r[0] < 0.5:
                q['u0'], q['u1'] = q['u1'], q['u0']
            Wc = 3.8 + 4.2 * r[1]
            Hc = Wc / q['aspect']
            yaw = (r[2] - 0.5) * math.radians(50)            # card faces the camera half (+Y)
            base = c - Vector((0, 0, 0.3 * Hc))               # sunk into the strand top
            for cross in ((0.0,) if lod else (0.0, math.radians(75))):
                sd = Vector((math.cos(yaw + cross), math.sin(yaw + cross), 0))
                pts = [tuple(base + sd * (u - 0.5) * Wc + Vector((0, 0, t * Hc))) for t in (0, 1) for u in (0, 1)]
                uvs = [(q['u0'] + (q['u1'] - q['u0']) * u, q['v0'] + (q['v1'] - q['v0']) * t)
                       for t in (0, 1) for u in (0, 1)]
                cols = [(0.3 * t, 0, 0, 0.55 + 0.45 * t) for t in (0, 1) for u in (0, 1)]
                B.grid(pts, uvs, cols, 1, 1, 1)
        objs.append(B.object(f'FP_ArchVines_P_LOD{lod}', mats))
    print('crests', len(tops))
    for o in objs:
        print(o.name, 'tris', E.tris(o))
    E.export_fbx(objs, os.path.join(OUT, 'FP_ArchVines_P.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('plants', 'FP_ArchVines_P_game.blend'), compress=True)
    print('SUMMARY FP_ArchVines_P', len(picked), 'anchors', [E.tris(o) for o in objs])


if __name__ == '__main__':
    a = E.args() or ['frames', 'clumps', 'canopy', 'vines']
    if 'frames' in a:
        build_frames()
    if 'clumps' in a:
        build_clumps()
    if 'canopy' in a:
        build_canopy()
    if 'vines' in a:
        build_vines()
