"""Step 5: new UV layout shared by SK_Pista_Body and SK_Pista_Ponytail (one texture set, one material).

Meshy's atlas is hundreds of tiny charts with poor packing (bleeds in mips/ASTC), and Blender's Smart UV Project
fragments the noisy remesh into ~4k islands (17% coverage), so we use xatlas (pip package `xatlas`, MIT license;
a build-time tool, nothing ships). The face gets 1.5x texel density: its triangles are handed to xatlas as a
separate sub-mesh scaled 1.5x. xatlas also packs both objects into one sheet (5 px padding), which is scaled uniformly into
0-1 (bake margins fill the gutters). Step 6 re-bakes every map from the Meshy surface, so the old UVs can go.

Input:  work/pista_04_budget.blend
Output: work/pista_05_uv.blend
Needs:  numpy and xatlas importable by Blender's Python (see art_source/pista/README.md).
"""
import os
import sys

import bpy
import bmesh
import numpy as np
import xatlas

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

FACE_DENSITY = 1.5
RESOLUTION = 2048
PADDING_PX = 5          # at the xatlas sheet size (~2150); ~4.8 px at 2048, ~2.4 px at 1024
XATLAS_TPU = 900
UV = 'UVMap'


def is_face_region(c):
    return c.z > 1.36 and c.y < 0.03 and abs(c.x) < 0.10


C.open_blend(C.work('pista_04_budget.blend'))
objs = [bpy.data.objects[C.BODY], bpy.data.objects[C.PONYTAIL]]

parts = []   # (object, polygon indices, scale)
for o in objs:
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(o.data); bm.free()
    me = o.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name=UV)
    for p in me.polygons:
        p.use_smooth = True
    if 'sharp_face' in me.attributes:
        me.attributes.remove(me.attributes['sharp_face'])
    if o.name == C.BODY:
        face = [p.index for p in me.polygons if is_face_region(p.center)]
        fs = set(face)
        parts.append((o, [p.index for p in me.polygons if p.index not in fs], 1.0))
        parts.append((o, face, FACE_DENSITY))
    else:
        parts.append((o, [p.index for p in me.polygons], 1.0))

atlas = xatlas.Atlas()
for o, polys, scale in parts:
    me = o.data
    co = np.array([v.co[:] for v in me.vertices], np.float32) * scale
    idx = np.array([me.polygons[i].vertices[:] for i in polys], np.uint32)
    atlas.add_mesh(co, idx)
co_opt = xatlas.ChartOptions()
co_opt.max_iterations = 4
pk = xatlas.PackOptions()
# One unconstrained near-square sheet, then uniform scale into 0-1. Tested 2026-10-08: a fixed 2048 page spills
# a second page even at 800 texels/m, and brute-force packing gives tall sheets (~736 px/m effective).
pk.resolution = 0
pk.texels_per_unit = XATLAS_TPU
pk.padding = PADDING_PX
pk.bilinear = True
pk.bruteForce = False
pk.rotate_charts = True
atlas.generate(co_opt, pk)
print(f'xatlas charts: {atlas.chart_count}, sheet {atlas.width}x{atlas.height}, utilization {atlas.utilization}')
for m, (o, polys, scale) in enumerate(parts):
    vmap, tri, uvs = atlas[m]
    uvd = o.data.uv_layers[UV].data
    for t, pi in enumerate(polys):
        p = o.data.polygons[pi]
        for k, li in enumerate(p.loop_indices):
            assert vmap[tri[t][k]] == p.vertices[k]
            uvd[li].uv = (uvs[tri[t][k]][0] * atlas.width / max(atlas.width, atlas.height),
                           uvs[tri[t][k]][1] * atlas.height / max(atlas.width, atlas.height))

total_area = 0.0
for o in objs:
    bm = bmesh.new(); bm.from_mesh(o.data)
    uvl = bm.loops.layers.uv[UV]
    us = np.array([l[uvl].uv[:] for f in bm.faces for l in f.loops])
    area = sum(abs((f.loops[1][uvl].uv - f.loops[0][uvl].uv).cross(f.loops[2][uvl].uv - f.loops[0][uvl].uv)) / 2
               for f in bm.faces)
    print(f'{o.name}: uv u {us[:, 0].min():.4f}..{us[:, 0].max():.4f}, v {us[:, 1].min():.4f}..{us[:, 1].max():.4f}, '
          f'coverage {area:.3f}')
    total_area += area
    bm.free()
surf = sum(p.area for o in objs for p in o.data.polygons)
print(f'total coverage {total_area:.3f}; mean texel density ~{RESOLUTION * (total_area / surf) ** 0.5:.0f} px/m '
      f'(face x{FACE_DENSITY}); target ~1200 px/m (ART_DIRECTION 10.2)')
C.save_blend(C.work('pista_05_uv.blend'))
