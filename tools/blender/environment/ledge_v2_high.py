"""RS_LedgeLookout v2 (F4_e foreground): the rocky, mossy lookout Pista stands on (replaces the v1 grassy blob).

A main rock with a flattened, walkable top (about 1.35 m) is fused with stepped boulders that fall away toward the
basin (Blender -Y = Unity local +Z, the side the camera looks over); layered strata and chipped facets on the faces,
patchy moss on the top (grey rock and loose chips show
through, as under Pista's boots in the keyframe) (travertine_material, so the foreground rock matches the tiers).
Anchor RS_LedgeLookout_Stand = Pista's standing point on the top. Pivot: ground level (z = 0), piece centre.
Builds and bakes in one run (bakekit). Usage: blender ... -P ledge_v2_high.py
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
import bakekit as K  # noqa: E402
from ledges_high import join  # noqa: E402
from rootstone_high import rockify  # noqa: E402

NAME = 'RS_LedgeLookout'
SEED = 83
TOP = 1.35
LOD_TRIS = (2500, 1100, 400)       # ledge budget (README)


def strata(ob, seed):
    """Horizontal sedimentary steps on steep faces: a stair-like quantisation of the height, broken by noise."""
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    N = np.empty(n * 3); me.vertex_normals.foreach_get('vector', N); N = N.reshape(-1, 3)
    nz = E.Noise(seed)
    steep = np.clip((0.85 - np.abs(N[:, 2])) / 0.45, 0, 1)
    z = P[:, 2] + 0.08 * nz.n3(P / 1.5)
    layer = 0.3
    f = z / layer - np.floor(z / layer)
    step = (f - 0.5) * 0.22                       # each layer leans out at its top: little ledges
    mask = np.clip(nz.n3(P / 2.0 + 7) * 2 + 0.6, 0, 1)
    P = P + N * (step * steep * mask)[:, None]
    me.vertices.foreach_set('co', P.ravel())
    me.update()


def attributes(ob):
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    N = np.empty(n * 3); me.vertex_normals.foreach_get('vector', N); N = N.reshape(-1, 3)
    col = np.zeros((n, 4), np.float32)
    nz = E.Noise(SEED + 5)
    patch = np.clip(nz.n3(P / 0.9) * 2.2 + 0.35, 0, 1) * np.clip(nz.n3(P / 0.25 + 3) * 1.5 + 0.8, 0, 1)
    col[:, 1] = np.clip((P[:, 2] - 0.5) / 0.7, 0, 1) * np.clip((N[:, 2] - 0.4) / 0.4, 0, 1) * patch  # patchy moss
    col[:, 0] = np.clip((0.25 - P[:, 2]) / 0.5, 0, 1) * 0.5                                   # damp foot
    col[:, 3] = 0.5
    ca = me.color_attributes.new('trav', 'FLOAT_COLOR', 'POINT')
    ca.data.foreach_set('color', col.ravel())


def build():
    E.reset()
    rng = np.random.default_rng(SEED)
    from ledges_high import outline_lookout, prism
    from rootstone_high import chunk
    # angular build-up (not blobs): an irregular slab top, two stepped slabs falling toward the basin (-Y),
    # faceted boulders around the edges
    top = prism('top', outline_lookout(dict(W=6.0, D=4.4), rng), -0.6, TOP)
    for v in top.data.vertices:
        if v.co.z > 0:
            v.co.z += 0.06 * v.co.x - 0.04 * v.co.y
    parts = [top]
    for k, (dy, dx, h, sc) in enumerate([(-2.3, 0.4, 0.95, 0.7), (-3.2, -0.6, 0.5, 0.55)]):
        sl = prism(f'step{k}', [(x * sc + dx, y * sc * 0.7 + dy) for x, y in outline_lookout(dict(W=6.0, D=4.4), rng)],
                   -0.6, h)
        parts.append(sl)
    rocks = []
    for k in range(9):
        a = 2 * math.pi * k / 9 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(2.4, 3.2)
        sz = rng.uniform(0.6, 1.25)
        rocks.append(chunk(np.array([math.cos(a) * r * 1.05, math.sin(a) * r * 0.85, rng.uniform(0.0, 0.5)]), sz, rng))
    parts.append(E.mesh_from('rocks', rocks))
    # loose stone chips and small slabs on the top and at the foot (keyframe: broken rock under Pista's boots)
    from rootstone_high import chunk
    chips = []
    for k in range(26):
        a, r = rng.uniform(0, 2 * math.pi), rng.uniform(0.3, 2.6)
        on_top = k < 14
        x, y = math.cos(a) * r * (1.0 if on_top else 1.6), math.sin(a) * r * (0.75 if on_top else 1.5) + 0.3
        sz = rng.uniform(0.12, 0.32) if on_top else rng.uniform(0.25, 0.6)
        chips.append(chunk(np.array([x, y, (TOP - 0.02) if on_top else 0.0]), sz, rng))
    me = E.mesh_from('chips', chips)
    parts.append(me)
    ob = join(parts, NAME + '_high')
    for v in ob.data.vertices:                    # keep the top walkable: nothing pokes far above it
        if v.co.z > TOP + 0.15:
            v.co.z = TOP + 0.15 + (v.co.z - TOP - 0.15) * 0.3
    E.voxel_remesh(ob, 0.035)
    E.smooth(ob, 0.5, 1)
    E.displace(ob, E.tex('lumps', 'CLOUDS', noise_scale=0.7, noise_depth=2), 0.16)
    strata(ob, SEED)
    rockify(ob, 1.1, 0.6, 0.6, SEED, max_cut=0.26)        # broken, angular faces
    rockify(ob, 0.45, 0.9, 0.55, SEED + 1, max_cut=0.08)
    rockify(ob, 0.18, 1.0, 0.5, SEED + 2, max_cut=0.025)
    crack = E.tex('crack', 'VORONOI', noise_scale=0.35, distance_metric='DISTANCE',
                  weight_1=-1.0, weight_2=1.0, noise_intensity=1.0)
    E.displace(ob, crack, 0.03, mid=0.0)
    E.displace(ob, E.tex('grain', 'STUCCI', noise_scale=0.1, turbulence=4.0), 0.008)
    E.cut_below(ob, -0.4)
    attributes(ob)
    print(NAME, 'high tris', E.tris(ob))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_high.blend'), compress=True)

    from travertine_bake import travertine_material
    ob.data.materials.append(travertine_material(moss_boost=1.0))
    # Pista's standing point: highest flat spot near the middle of the top
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    best = None
    for x in np.linspace(-0.8, 0.8, 9):
        for y in np.linspace(-0.4, 0.8, 7):
            hit = bvh.ray_cast(Vector((x, y, 5)), Vector((0, 0, -1)))
            if hit[0] is not None and hit[1].z > 0.95 and (best is None or abs(x) + abs(y) < abs(best.x) + abs(best.y)):
                best = hit[0]
    assert best is not None
    stand = K.empty(NAME + '_Stand', (best.x, best.y, best.z))
    print('Stand', tuple(round(c, 2) for c in stand.location))
    K.bake_piece(NAME, ob, LOD_TRIS, res=1024, ext=0.15, ray=0.4, extras=[stand], ao_power=1.3)


if __name__ == '__main__':
    build()
