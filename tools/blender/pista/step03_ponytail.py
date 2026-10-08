"""Step 3: separate the high ponytail into its own mesh (SK_Pista_Ponytail) for 3-4 swing bones.

Findings (cross-sections, 2026-10-08): below the crown the ponytail is a closed volume behind the head that does not
touch the skull; it joins the head mesh only at the crown (y ~0.12 m, z ~1.59-1.63 m). We fit an ellipsoid to the
skull+scalp hair, then flood-fill from the ponytail's most backward face through faces that lie outside
ELLIPSOID_MARGIN x the ellipsoid. The root stays on the head (it reads as the hair tie), the ponytail becomes its own
object, and both openings are capped. The cap on the ponytail sits inside the tie, so it is hidden.

Input:  work/pista_02_norope.blend
Output: work/pista_03_ponytail.blend (objects SK_Pista_Body and SK_Pista_Ponytail, shared material)
"""
import math
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

ELLIPSOID_MARGIN = 1.12

C.open_blend(C.work('pista_02_norope.blend'))
body = bpy.data.objects[C.BODY]

# Skull ellipsoid: fit to head vertices in front of the ponytail.
P = np.array([list(v.co) for v in body.data.vertices if v.co.z > 1.40 and v.co.y < 0.10 and abs(v.co.x) < 0.12])
back_y = 0.141  # back of the scalp hair at the ponytail root (cross-section), meters
cx, cy, cz = 0.0, (P[:, 1].min() + back_y) / 2, 1.52
rx = float(np.percentile(np.abs(P[P[:, 2] > 1.47][:, 0]), 99))
ry = (back_y - P[:, 1].min()) / 2
rz = 0.125


def e(c):
    return math.sqrt(((c.x - cx) / rx) ** 2 + ((c.y - cy) / ry) ** 2 + ((c.z - cz) / rz) ** 2)


bm = bmesh.new()
bm.from_mesh(body.data)
bm.faces.ensure_lookup_table()
seed = max((f for f in bm.faces if f.calc_center_median().z > 1.25), key=lambda f: f.calc_center_median().y)
pony = {seed}
stack = [seed]
while stack:
    f = stack.pop()
    for ed in f.edges:
        for g in ed.link_faces:
            c = g.calc_center_median()
            if g not in pony and c.z > 1.2 and e(c) > ELLIPSOID_MARGIN:
                pony.add(g)
                stack.append(g)
print(f'ponytail faces: {len(pony)} of {len(bm.faces)}')
idx = {f.index for f in pony}
bm.free()

# Separate with the operator so UVs/material stay intact.
C.set_active(body)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='DESELECT')
bpy.ops.object.mode_set(mode='OBJECT')
for p in body.data.polygons:
    p.select = p.index in idx
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
pt = next(o for o in bpy.context.scene.objects if o.type == 'MESH' and o is not body)
pt.name = C.PONYTAIL
pt.data.name = C.PONYTAIL

# Cap the openings at the cut (boundary loops near the crown only; other boundaries are left alone).
for o in (body, pt):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    cut = [ed for ed in bm.edges if ed.is_boundary
           and all(v.co.z > 1.45 and v.co.y > 0.04 for v in ed.verts)]
    res = bmesh.ops.holes_fill(bm, edges=cut, sides=0)
    bmesh.ops.triangulate(bm, faces=res['faces'])
    left = sum(1 for ed in bm.edges if ed.is_boundary and all(v.co.z > 1.45 and v.co.y > 0.04 for v in ed.verts))
    print(f'{o.name}: capped {len(res["faces"])} openings, open edges left at the cut: {left}')
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()

print(f'tris body {C.tri_count(body)}, ponytail {C.tri_count(pt)}')
C.save_blend(C.work('pista_03_ponytail.blend'))
