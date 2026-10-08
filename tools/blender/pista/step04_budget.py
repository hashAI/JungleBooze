"""Step 4: bring Pista under the 20k triangle budget (common.TRI_BUDGET) with collapse decimation.

The face (eyes, nose, mouth, ears) and the hands keep their triangles: they are listed in a vertex group and the
decimate modifier is told to leave them alone. Textures are re-baked in step 5 from the untouched Meshy mesh, so
UV distortion from decimation does not matter.

Input:  work/pista_03_ponytail.blend
Output: work/pista_04_budget.blend
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

BODY_TARGET = 17300
PONYTAIL_TARGET = 2600


def protect_group(obj):
    g = obj.vertex_groups.new(name='keep')
    keep = []
    for v in obj.data.vertices:
        c = v.co
        face = c.z > 1.36 and c.y < 0.03 and abs(c.x) < 0.10          # front half of the head: face and ears
        hands = c.z < 1.02 and abs(c.x) > 0.23                          # hands hang outside the hips in A-pose
        if face or hands:
            keep.append(v.index)
    g.add(keep, 1.0, 'REPLACE')
    return g, len(keep)


def decimate(obj, target, protect):
    C.set_active(obj)
    n = C.tri_count(obj)
    m = obj.modifiers.new('dec', 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.use_collapse_triangulate = True
    if protect:
        g, k = protect_group(obj)
        m.vertex_group = g.name
        m.invert_vertex_group = True     # weight 1 = protected (checked by the face/hand tri counts printed below)
        m.vertex_group_factor = 1.0
    # the protected part cannot shrink, so solve the ratio for the rest
    m.ratio = target / n
    for _ in range(12):
        dg = bpy.context.evaluated_depsgraph_get()
        ev = obj.evaluated_get(dg).to_mesh()
        ev.calc_loop_triangles(); got = len(ev.loop_triangles)
        obj.evaluated_get(dg).to_mesh_clear()
        if abs(got - target) < 60:
            break
        m.ratio = max(0.05, min(1.0, m.ratio * target / got))
    bpy.ops.object.modifier_apply(modifier=m.name)
    print(f'{obj.name}: {n} -> {C.tri_count(obj)} tris (ratio {m.ratio if m.name in obj.modifiers else "applied"})')


def region_tris(obj, test):
    me = obj.data; me.calc_loop_triangles()
    return sum(1 for t in me.loop_triangles if test(sum((me.vertices[i].co for i in t.vertices), me.vertices[0].co * 0) / 3))


C.open_blend(C.work('pista_03_ponytail.blend'))
body = bpy.data.objects[C.BODY]
pony = bpy.data.objects[C.PONYTAIL]
face = lambda c: c.z > 1.36 and c.y < 0.03 and abs(c.x) < 0.10
hand = lambda c: c.z < 1.02 and abs(c.x) > 0.23
print('before: face tris', region_tris(body, face), 'hand tris', region_tris(body, hand))
decimate(body, BODY_TARGET, protect=True)
decimate(pony, PONYTAIL_TARGET, protect=False)
print('after: face tris', region_tris(body, face), 'hand tris', region_tris(body, hand))
for o in (body, pony):
    if 'keep' in o.vertex_groups:
        o.vertex_groups.remove(o.vertex_groups['keep'])
total = C.tri_count(body) + C.tri_count(pony)
print(f'TOTAL tris {total} (budget {C.TRI_BUDGET}) -> {"OK" if total <= C.TRI_BUDGET else "OVER"}')
C.save_blend(C.work('pista_04_budget.blend'))
