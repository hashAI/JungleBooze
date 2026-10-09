"""Basin rock ledges (F4_e): terraced pool rims and the foreground lookout rock, same stone as the rootstone kit.

Prism from a plan outline -> (terraces) basin carved behind the rim -> voxel remesh -> rockify facets -> vertical
fluting on steep faces (drip curtains where water spills) -> weathering. Baked by rootstone_bake.py (same material:
moss collects on the flat tops). Output: work/rootstone/<piece>_high.blend.
Usage: blender ... -P ledges_high.py -- [piece ...]
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_high import rockify  # noqa: E402

LEDGES = {
    # W: width along the rim (m), D: depth upstream, h: step height, bulge: rim curvature toward -Y (downstream)
    'RS_PoolTerrace_A': dict(kind='terrace', W=11.0, D=4.5, h=1.4, bulge=1.6, rim=0.7, basin=0.35, voxel=0.035, feature=1.0,
                             seed=61),
    'RS_PoolTerrace_B': dict(kind='terrace', W=6.5, D=3.2, h=0.9, bulge=1.1, rim=0.55, basin=0.28, voxel=0.03, feature=1.0,
                             seed=62),
    'RS_LedgeLookout': dict(kind='lookout', W=4.6, D=3.4, h=1.3, voxel=0.03, feature=1.0, seed=63),
}


def outline_terrace(sp, n=64):
    W, D, b = sp['W'], sp['D'], sp['bulge']
    pts = []
    for i in range(n + 1):                       # front rim, convex downstream (-Y)
        x = -W / 2 + W * i / n
        pts.append((x, -b * (1 - (2 * x / W) ** 2)))
    for i in range(1, 12):                       # rounded back corners and the upstream edge
        a = math.pi * i / 12
        pts.append((W / 2 * math.cos(a), D * math.sin(a) * 0.9 + 0.1 * D))
    return pts


def outline_lookout(sp, rng, n=40):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        r = 1 + 0.12 * math.sin(3 * a + 1) + 0.07 * math.sin(7 * a) + rng.uniform(-0.04, 0.04)
        pts.append((sp['W'] / 2 * r * math.cos(a), sp['D'] / 2 * r * math.sin(a)))
    return pts


def prism(name, pts, z0, z1):
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z0)) for x, y in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
        v.co.z = z1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    E.link(ob)
    return ob


def flute(ob, amp, seed):
    """Vertical drip curtains on steep faces (travertine-like), strongest near the top lip."""
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    N = np.empty(n * 3); me.vertex_normals.foreach_get('vector', N); N = N.reshape(-1, 3)
    nz = E.Noise(seed)
    steep = np.clip((0.6 - np.abs(N[:, 2])) / 0.4, 0, 1)
    q = np.stack([P[:, 0] * 2.2, P[:, 1] * 2.2, P[:, 2] * 0.25], 1)
    d = nz.n3(q) * 0.7 + nz.n3(q * 2.1 + 5) * 0.3
    P = P + N * (d * amp * steep)[:, None]
    me.vertices.foreach_set('co', P.ravel())
    me.update()


def blob(loc, radii, rng, nz_seed):
    """Squashed noisy boulder (icosphere) as a separate object."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1.0, location=loc)
    b = bpy.context.active_object
    nz = E.Noise(nz_seed)
    for v in b.data.vertices:
        p = np.array(v.co[:])
        v.co = p * (1 + 0.22 * nz.n3(p * 1.3 + nz_seed))
    b.scale = radii
    b.rotation_euler = (rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15), rng.uniform(0, math.pi))
    return b


def join(objs, name):
    E.set_active(objs[0], *objs[1:])
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.active_object
    ob.name = ob.data.name = name
    return ob


def build(name):
    sp = LEDGES[name]
    E.reset()
    rng = np.random.default_rng(sp['seed'])
    parts = []
    h = sp['h']
    if sp['kind'] == 'terrace':
        W, b = sp['W'], sp['bulge']
        # rubble dam along the rim: two rows of fused boulders, the front row a little lower and rounder
        x = -W / 2 + 0.5
        k = 0
        while x < W / 2 - 0.4:
            yr = -b * (1 - (2 * x / W) ** 2)
            r = rng.uniform(0.75, 1.3)
            parts.append(blob((x, yr + 0.55 * r, h * rng.uniform(0.35, 0.55)),
                              (r * 1.15, r * 0.9, h * rng.uniform(0.6, 0.78)), rng, sp['seed'] * 10 + k))
            if rng.random() < 0.7:
                parts.append(blob((x + rng.uniform(-0.4, 0.4), yr + 1.4 * r, h * 0.45),
                                  (r, r * 0.8, h * 0.62), rng, sp['seed'] * 10 + 50 + k))
            x += r * rng.uniform(1.0, 1.5)
            k += 1
        # pool floor slab behind the rim (the water surface sits on it)
        parts.append(prism('slab', outline_terrace(sp), -0.6, h - sp['basin'] - 0.15))
    else:
        for k in range(5):
            r = rng.uniform(0.9, 1.5)
            parts.append(blob((rng.uniform(-1.4, 1.4), rng.uniform(-0.8, 0.8), h * rng.uniform(0.2, 0.45)),
                              (r * 1.4, r, h * rng.uniform(0.55, 0.8)), rng, sp['seed'] * 10 + k))
        top = prism('top', outline_lookout(sp, rng), -0.6, h * 0.92)
        for v in top.data.vertices:
            if v.co.z > 0:
                v.co.z += 0.08 * v.co.x - 0.05 * v.co.y
        parts.append(top)
    ob = join(parts, name + '_high')
    E.voxel_remesh(ob, sp['voxel'])
    if sp['kind'] == 'terrace':
        inner = [(x * 0.9, y + sp['rim'] + 0.5) for x, y in outline_terrace(sp)]
        cut = prism('basin', inner, h - sp['basin'], h + 3)
        m = ob.modifiers.new('basin', 'BOOLEAN')
        m.operation, m.object, m.solver = 'DIFFERENCE', cut, 'EXACT'
        E.set_active(ob)
        bpy.ops.object.modifier_apply(modifier='basin')
        bpy.data.objects.remove(cut)
        E.voxel_remesh(ob, sp['voxel'])
    ca = ob.data.color_attributes.new('strand', 'FLOAT_COLOR', 'POINT')
    col = np.zeros((len(ob.data.vertices), 4), np.float32)
    col[:, 0] = rng.random()
    ca.data.foreach_set('color', col.ravel())
    f = sp.get('feature', 1.0)                     # weathering scale in metres (not tied to the voxel size)
    E.smooth(ob, 0.5, 2)
    E.displace(ob, E.tex('lumps', 'CLOUDS', noise_scale=1.4 * f, noise_depth=2), 0.18 * f)
    rockify(ob, 1.5 * f, 0.6, 0.6, sp['seed'], max_cut=0.14 * f)
    rockify(ob, 0.55 * f, 0.9, 0.5, sp['seed'] + 1, max_cut=0.04 * f)
    flute(ob, 0.07 * f if sp['kind'] == 'terrace' else 0.03 * f, sp['seed'])
    crack = E.tex('crack', 'VORONOI', noise_scale=0.45 * f, distance_metric='DISTANCE',
                  weight_1=-1.0, weight_2=1.0, noise_intensity=1.0)
    E.displace(ob, crack, 0.035 * f, mid=0.0)
    E.displace(ob, E.tex('grain', 'STUCCI', noise_scale=0.12 * f, turbulence=4.0), 0.01 * f)
    E.cut_below(ob, -0.4)
    print(name, 'high tris', E.tris(ob))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', name + '_high.blend'), compress=True)


if __name__ == '__main__':
    for nm in (E.args() or list(LEDGES)):
        build(nm)
