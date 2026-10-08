"""Step 2: remove the rope coil on Pista's hip (not in the final design).

Geometry facts (checked with cross-sections, 2026-10-08): Meshy fused the rope as thin closed tubes (~1 cm)
lying on top of an intact trouser surface; the trouser surface under the rope only carries rope *texture*.
So we:
  1. seed rope faces by texture color (tan, saturated) inside the hip box,
  2. keep only "thin" faces: a ray from the face along -normal hits the opposite tube wall within THIN_M,
  3. flood-fill through connected thin faces inside the box (catches shaded strands the color test missed),
  4. delete them, drop loose fragments, fill the small holes left where tubes touched the trousers.
The texture under the rope is fixed in step 4 (mirror clone from the other leg, mask saved here).

Input:  work/pista_01_prepared.blend
Output: work/pista_02_norope.blend, work/rope_mask_world.npy (world positions of removed rope, for the paint mask)
"""
import colorsys
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

BOX_MIN = Vector((0.03, -0.09, 0.68))   # the +X hip, from belt to mid thigh (meters, after step 1)
BOX_MAX = Vector((0.23, 0.14, 0.94))
THIN_M = 0.025

C.open_blend(C.work('pista_01_prepared.blend'))
obj = bpy.data.objects[C.BODY]
img = next(n.image for n in obj.data.materials[0].node_tree.nodes
           if n.type == 'TEX_IMAGE' and n.image.colorspace_settings.name == 'sRGB')
W, H = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)

bm = bmesh.new()
bm.from_mesh(obj.data)
bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv[0]
bvh = BVHTree.FromBMesh(bm)
tris_before = sum(len(f.verts) - 2 for f in bm.faces)


def in_box(p):
    return all(BOX_MIN[i] < p[i] < BOX_MAX[i] for i in range(3))


def face_hsv(f):
    uv = sum((l[uvl].uv for l in f.loops), Vector((0, 0))) / len(f.loops)
    pts = [uv] + [l[uvl].uv for l in f.loops]
    col = np.mean([px[min(H - 1, int(p.y * H)), min(W - 1, int(p.x * W)), :3] for p in pts], 0)
    return colorsys.rgb_to_hsv(*col)


def is_thin(f):
    c = f.calc_center_median()
    n = f.normal
    hit = bvh.ray_cast(c - n * 1e-4, -n, THIN_M)
    return hit[0] is not None


seeds = set()
for f in bm.faces:
    c = f.calc_center_median()
    if not in_box(c):
        continue
    h, s, v = face_hsv(f)
    if s > 0.22 and v > 0.25 and is_thin(f):
        seeds.add(f)

rope = set(seeds)
stack = list(seeds)
while stack:
    f = stack.pop()
    for e in f.edges:
        for g in e.link_faces:
            if g not in rope and in_box(g.calc_center_median()) and is_thin(g):
                rope.add(g)
                stack.append(g)
print(f'rope seeds {len(seeds)}, after flood fill {len(rope)}')

rope_pts = np.array([list(f.calc_center_median()) for f in rope], dtype=np.float32)
np.save(C.work('rope_mask_world.npy'), rope_pts)

bmesh.ops.delete(bm, geom=list(rope), context='FACES')

# Drop loose fragments (pieces of tube that lost their connection) – anything far smaller than the body.
bm.faces.ensure_lookup_table()
seen, comps = set(), []
for f in bm.faces:
    if f in seen:
        continue
    comp, st = [], [f]
    seen.add(f)
    while st:
        x = st.pop(); comp.append(x)
        for e in x.edges:
            for y in e.link_faces:
                if y not in seen:
                    seen.add(y); st.append(y)
    comps.append(comp)
dropped = 0
for comp in comps:
    centre = sum((f.calc_center_median() for f in comp), Vector()) / len(comp)
    if len(comp) < 200 and in_box(centre):
        bmesh.ops.delete(bm, geom=comp, context='FACES')
        dropped += len(comp)
print(f'dropped {dropped} faces in loose rope fragments')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

# Fill holes left in the trouser surface (boundary loops inside the box) and relax the patch.
bound = [e for e in bm.edges if e.is_boundary and in_box((e.verts[0].co + e.verts[1].co) / 2)]
filled = bmesh.ops.holes_fill(bm, edges=bound, sides=0)
new_faces = filled['faces']
tri = bmesh.ops.triangulate(bm, faces=new_faces)
print(f'filled {len(new_faces)} holes')
rest = [e for e in bm.edges if e.is_boundary and in_box((e.verts[0].co + e.verts[1].co) / 2)]
print('boundary edges left after holes_fill:', len(rest))

# holes_fill skips some long, non-planar loops (a 48-edge slit under the belt showed as a dark crack in the
# 2026-10-09 review). Fan-fill every remaining closed loop around its centroid (centroid pushed slightly outward
# so the patch follows the trouser curve). The bake textures the patch from the mirrored leg (rope mask).
rest_set = set(rest)
fans = 0
while rest_set:
    e0 = rest_set.pop()
    loop = [e0.verts[0], e0.verts[1]]
    used = {e0}
    closed = False
    while True:
        nxt = [e for e in loop[-1].link_edges if e in rest_set and e not in used]
        if not nxt:
            break
        e = nxt[0]; used.add(e); rest_set.discard(e)
        v = e.other_vert(loop[-1])
        if v is loop[0]:
            closed = True
            break
        loop.append(v)
    if not closed or len(loop) < 3:
        continue
    c = sum((v.co for v in loop), Vector()) / len(loop)
    n = sum((f.normal for v in loop for f in v.link_faces), Vector()).normalized()
    r = sum(((v.co - c).length for v in loop)) / len(loop)
    centre = bm.verts.new(c + n * r * 0.15)
    new = []
    for i in range(len(loop)):
        try:
            new.append(bm.faces.new((loop[i], loop[(i + 1) % len(loop)], centre)))
        except ValueError:
            pass
    for f in new:
        f.normal_update()
    if sum((f.normal for f in new), Vector()).dot(n) < 0:
        bmesh.ops.reverse_faces(bm, faces=new)
    fans += 1
rest = [e for e in bm.edges if e.is_boundary and in_box((e.verts[0].co + e.verts[1].co) / 2)]
print(f'fan-filled {fans} loops; boundary edges left in box: {len(rest)} (open strips at the box edge are fine)')

bm.to_mesh(obj.data)
bm.free()
obj.data.update()
print(f'tris {tris_before} -> {C.tri_count(obj)}')
C.save_blend(C.work('pista_02_norope.blend'))
