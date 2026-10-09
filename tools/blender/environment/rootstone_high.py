"""Rootstone kit, stage 1: procedural high-poly sculpt (braided petrified-root strands -> voxel union -> weathering).

ART_DIRECTION 5.1: colossal roots turned to stone, twisted rope-like strands of warm grey-ochre rock, braided like a
root flare, always grounded. Keyframe targets: F1 (distant arches), F4/P1 (hero arch with waterfall mouth, pillars).

Usage: blender ... -P rootstone_high.py -- <piece|all>
Output: art_source/environment/work/rootstone/<piece>_high.blend (object '<piece>_high', float-color attr 'strand':
R = per-strand random, G = position along the strand 0..1, B = 1 for small wrapping roots).
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402

# spine: control points (m, Z up, the run looks along +Y in Blender = Unity +Z after export)
# rb: (u, radius) keys of the bundle radius; n: main strands; turns: twist turns over the length
# ground: which ends are planted (0 = start, 1 = end); voxel: remesh size (m); mouth: cavity ellipsoid
PIECES = {
    'RS_HeroArch': dict(
        spine=[(-21, 2, 0), (-21.5, 1.5, 12), (-19, 0.5, 25), (-12, 0, 36), (-2, -0.5, 41.5), (8, 0, 40),
               (16, 0.8, 31), (20, 1.5, 17), (22, 2.5, 0)],
        rb=[(0, 9.0), (0.08, 6.6), (0.25, 5.4), (0.42, 6.4), (0.5, 7.2), (0.6, 6.2), (0.78, 5.3), (0.92, 6.6), (1, 9.0)],
        n=8, turns=1.4, ground=(0, 1), voxel=0.13, seed=11, secondary=7, peel=0.6, radk=(0.34, 0.6), rock=(8, 0.3, 3, 0.1, 9, 1.2), tail=(1.0, 2.2),
        mouth=dict(c=(-1.0, -0.6, 35.6), r=(5.0, 4.4, 4.6))),
    'RS_PillarA': dict(
        spine=[(0, 0, 0), (0.6, 0.3, 8), (-0.5, -0.4, 17), (0.8, 0.2, 26), (0.2, 0.6, 33)],
        rb=[(0, 6.2), (0.1, 4.4), (0.3, 3.9), (0.45, 4.5), (0.6, 3.7), (0.8, 4.2), (0.95, 3.8), (1, 3.0)],
        n=7, turns=1.1, ground=(0,), voxel=0.09, seed=21, secondary=5, tail=(1.0, 2.0), ragged_top=True),
    'RS_PillarB': dict(
        spine=[(0, 0, 0), (0.5, 0, 9), (1.8, 0, 18), (4.5, 0, 25), (8.5, 0, 27.5), (11.5, 0, 25.5), (12.5, 0, 21.5)],
        rb=[(0, 5.0), (0.1, 3.4), (0.4, 3.0), (0.6, 2.8), (0.8, 2.4), (1, 1.6)],
        n=6, turns=1.3, ground=(0,), voxel=0.08, seed=31, secondary=4, tail=(1.0, 2.0), ragged_top=True),
    'RS_ArchSmall': dict(
        spine=[(-8.5, 0, 0), (-9, 0, 7), (-7, 0.3, 13.5), (-2, 0.6, 17.5), (3, 0.3, 17.5), (7.5, 0, 13),
               (9, -0.2, 6.5), (8.5, 0, 0)],
        rb=[(0, 3.6), (0.08, 2.4), (0.3, 1.9), (0.5, 1.7), (0.7, 1.9), (0.92, 2.4), (1, 3.6)],
        n=6, turns=1.6, ground=(0, 1), voxel=0.05, seed=41, secondary=3, peel=0.5, tail=(1.1, 2.3)),
    'RS_Outcrop': dict(
        spine=[(-5.5, -0.5, -0.3), (-3, 0.2, 1.4), (0, 0.5, 2.3), (3, 0, 1.6), (5.5, -0.6, -0.3)],
        rb=[(0, 1.2), (0.2, 1.5), (0.5, 1.7), (0.8, 1.4), (1, 1.1)],
        n=5, turns=0.9, ground=(0, 1), voxel=0.03, seed=51, secondary=2, tail=(0.9, 1.8),
        extra=[dict(spine=[(-1.5, -2.8, -0.3), (-0.5, -1.0, 1.0), (0.8, 1.0, 1.2), (1.6, 3.0, -0.3)],
                    rb=[(0, 0.8), (0.5, 1.05), (1, 0.8)], n=5, turns=1.3, ground=(0, 1), seed=52, secondary=2,
                    tail=(0.9, 1.6))]),
}


def keyed(keys, u):
    k = np.array(keys)
    return np.interp(u, k[:, 0], k[:, 1])


def bundle(spec, noise, rng, scale_hint):
    """Strand tubes for one bundle: list of (V, F, A)."""
    spine = np.array(spec['spine'], float)
    length = np.sum(np.linalg.norm(np.diff(spine, axis=0), axis=1))
    rbmax = max(r for _, r in spec['rb'])
    m = int(max(60, length / (rbmax * 0.12)))
    C = E.catmull(spine, m)
    T, N, B = E.frames(C)
    u = np.linspace(0, 1, m)
    Rb = keyed(spec['rb'], u)
    # bulges ("stacked" knots of the keyframe pillars)
    Rb = Rb * (1 + 0.10 * noise.fbm1(u * length / (rbmax * 2.5) + 3.1))
    parts = []
    n = spec['n']
    ground = spec.get('ground', ())

    def strand(theta, rad_k, off_k, turns, wob, u0, u1, secondary, sid, peels=0):
        sel = (u >= u0) & (u <= u1)
        uu = u[sel]
        ph = theta + 2 * math.pi * turns * uu + 0.6 * noise.fbm1(uu * 6 + sid * 3.7)
        braid = 0.14 * np.sin(2 * math.pi * (turns * 1.5) * uu + sid * 1.9)       # over/under weave
        peel = np.zeros_like(uu)
        for _ in range(peels):                       # a strand loops away from the bundle and returns (F4_e)
            uc, w, amp = rng.uniform(0.12, 0.88), rng.uniform(0.04, 0.09), rng.uniform(0.25, 0.6)
            peel += amp * np.exp(-((uu - uc) / w) ** 2)
        rho = Rb[sel] * (off_k + braid + peel)
        P = C[sel] + rho[:, None] * (np.cos(ph)[:, None] * N[sel] + np.sin(ph)[:, None] * B[sel])
        P = P + Rb[sel][:, None] * wob * noise.vec3(P / (rbmax * 2.2) + sid * 5.1)
        r = Rb[sel] * rad_k * (1 + 0.18 * noise.fbm1(uu * length / (rbmax * 1.6) + sid * 9.1))
        P, r = list(P), list(r)
        # planted ends: strands leave the bundle and run out over the ground as roots
        tl = spec.get('tail', (1.0, 2.0))
        for end in (0, 1):
            if end not in ground or (end == 0 and u0 > 0.02) or (end == 1 and u1 < 0.98):
                continue
            e = P[0] if end == 0 else P[-1]
            c = C[0] if end == 0 else C[-1]
            d = np.array([e[0] - c[0], e[1] - c[1], 0.0])
            if np.linalg.norm(d) < 1e-3:
                d = np.array([math.cos(theta), math.sin(theta), 0])
            d /= np.linalg.norm(d)
            side = np.array([-d[1], d[0], 0])
            L = Rb[0 if end == 0 else -1] * rng.uniform(*tl) * (0.6 if secondary else 0.7)
            r0 = r[0] if end == 0 else r[-1]
            tail_p, tail_r = [], []
            curl = rng.uniform(-0.5, 0.5)
            for k in range(1, 13):
                t = k / 12
                p = e + d * L * t + side * L * curl * t * t
                p[2] = e[2] * (1 - t) ** 1.2 - r0 * 1.1 * t ** 1.5
                tail_p.append(p)
                tail_r.append(r0 * (1 - 0.4 * t))
            if end == 0:
                P, r = tail_p[::-1] + P, tail_r[::-1] + r
            else:
                P, r = P + tail_p, r + tail_r
        # free ends taper (and curl a little)
        if end_free := [e for e in (0, 1) if e not in ground]:
            nt = max(4, len(r) // 10)
            for k in range(nt):
                w = (k + 1) / nt
                if 1 in end_free:
                    r[-nt + k] *= (1 - 0.45 * w ** 2)
                if 0 in end_free:
                    r[nt - 1 - k] *= (1 - 0.45 * w ** 2)
        P = np.array(P)
        rr = np.array(r)
        ring = 14 if secondary else 30
        groove = (0.0, 3, 0.0) if secondary else (0.025, 2, 0.4 / max(rr.mean(), 0.2))
        return E.tube(P, rr, ring=ring, groove=groove, attr=(rng.random(), 1.0 if secondary else 0.0))

    # core keeps the bundle solid
    # recessed core keeps the bundle solid but leaves dark gaps between the big strands
    parts.append(strand(0, spec.get('core', 0.5), 0.0, spec['turns'], 0.02, 0, 1, False, 0))
    ragged = spec.get('ragged_top', False)
    rk, ok = spec.get('radk', (0.28, 0.52)), spec.get('offk', (0.62, 0.8))
    for i in range(n):
        th = 2 * math.pi * i / n + rng.uniform(-0.25, 0.25)
        u1 = rng.uniform(0.93, 1.0) if ragged else 1.0
        parts.append(strand(th, rng.uniform(*rk), rng.uniform(*ok), spec['turns'] * rng.uniform(0.85, 1.15),
                            0.06, 0, u1, False, i + 1, peels=int(rng.random() < spec.get('peel', 0.4))))
    for j in range(spec.get('secondary', 0)):
        th = rng.uniform(0, 2 * math.pi)
        a = rng.uniform(0, 0.6)
        b = min(1, a + rng.uniform(0.25, 0.6))
        if rng.random() < 0.45:
            a = 0.0
        if 1 in ground and rng.random() < 0.35:
            b = 1.0
        parts.append(strand(th, rng.uniform(0.13, 0.2), rng.uniform(0.95, 1.05),
                            spec['turns'] * rng.uniform(-1.2, 1.2), 0.04, a, b, True, 100 + j))
    # debris: faceted chunks half buried around planted ends (blends the piece into the ground)
    for end in ground:
        c = C[0] if end == 0 else C[-1]
        rb0 = Rb[0] if end == 0 else Rb[-1]
        for k in range(spec.get('debris', 7)):
            a = rng.uniform(0, 2 * math.pi)
            dist = rb0 * rng.uniform(0.9, 2.2)
            sz = rb0 * rng.uniform(0.08, 0.26)
            parts.append(chunk(np.array([c[0] + math.cos(a) * dist, c[1] + math.sin(a) * dist, -0.35 * sz]),
                               sz, rng))
    return parts


def chunk(pos, size, rng):
    """Irregular rock chunk: a squashed, randomly scaled icosphere (faceted later by rockify)."""
    t = (1 + 5 ** 0.5) / 2
    V = np.array([(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
                  (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)], float)
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10),
         (8, 6, 7), (9, 8, 1)]
    V /= np.linalg.norm(V, axis=1, keepdims=True)
    V *= rng.uniform(0.75, 1.25, (len(V), 1))
    V *= np.array([rng.uniform(0.8, 1.4), rng.uniform(0.8, 1.4), rng.uniform(0.45, 0.8)]) * size
    A = np.zeros((len(V), 4)); A[:, 0] = rng.random()
    return V + pos, F, A


def rockify(ob, cell, quantile_k, strength, seed, max_cut=1e9):
    """Voronoi facet shaving: in each cell the outermost bulge is pushed onto the cell's plane, which gives flat
    fracture faces and sharp creases between cells (rock, not tubes)."""
    from scipy.spatial import cKDTree
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    Nv = np.empty(n * 3); me.vertex_normals.foreach_get('vector', Nv); Nv = Nv.reshape(-1, 3)
    rng = np.random.default_rng(seed)
    # jittered grid sites over the surface
    key = np.floor(P / cell + rng.random(3)).astype(np.int64)
    _, first = np.unique(key, axis=0, return_index=True)
    sites = P[first] + rng.normal(0, cell * 0.2, (len(first), 3))
    lab = cKDTree(sites).query(P)[1]
    m = len(sites)
    cnt = np.bincount(lab, minlength=m).astype(float) + 1e-9
    ctr = np.stack([np.bincount(lab, P[:, i], m) for i in range(3)], 1) / cnt[:, None]
    nrm = np.stack([np.bincount(lab, Nv[:, i], m) for i in range(3)], 1)
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9
    d = np.einsum('ij,ij->i', P - ctr[lab], nrm[lab])
    mean = np.bincount(lab, d, m) / cnt
    var = np.bincount(lab, d * d, m) / cnt - mean ** 2
    k = quantile_k * rng.uniform(0.6, 1.4, m)            # some cells cut deep, some barely
    level = mean + k * np.sqrt(np.maximum(var, 0))
    over = d - level[lab]
    # depth clamp: thin wrapping roots keep their round section (no flat 'planks')
    P = P - nrm[lab] * np.minimum(np.maximum(over, 0) * strength, max_cut)[:, None]
    me.vertices.foreach_set('co', P.ravel())
    me.update()


def cracks(ob, spacing, depth, width, seed):
    """Transverse cracks across the strands (petrified-wood checks), from the along-strand metres in 'strand'.G."""
    me = ob.data
    n = len(me.vertices)
    col = np.empty(n * 4, np.float32); me.color_attributes['strand'].data.foreach_get('color', col)
    col = col.reshape(-1, 4)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    Nv = np.empty(n * 3); me.vertex_normals.foreach_get('vector', Nv); Nv = Nv.reshape(-1, 3)
    nz = E.Noise(seed)
    jitter = nz.n3(P / (spacing * 1.5)) * 0.35
    f = col[:, 1] / (spacing * (0.7 + 0.6 * col[:, 0])) + col[:, 0] * 7.3 + jitter
    dist = np.abs(f - np.round(f)) * spacing
    g = np.exp(-(dist / width) ** 2)
    mask = np.clip(nz.n3(P / (spacing * 2.0) + 11.0) * 2.5 + 0.3, 0, 1)   # cracks are partial, not rings
    P = P - Nv * (depth * g * mask)[:, None]
    me.vertices.foreach_set('co', P.ravel())
    me.update()


def build(name):
    spec = PIECES[name]
    E.reset()
    noise = E.Noise(spec['seed'])
    rng = np.random.default_rng(spec['seed'])
    parts = bundle(spec, noise, rng, spec['voxel'])
    for ex in spec.get('extra', []):
        parts += bundle(ex, E.Noise(ex['seed']), np.random.default_rng(ex['seed']), spec['voxel'])
    ob = E.mesh_from(name + '_high', parts)
    print(name, 'tube verts', len(ob.data.vertices))
    E.voxel_remesh(ob, spec['voxel'])
    print(name, 'remeshed faces', len(ob.data.polygons))
    if 'mouth' in spec:
        mo = spec['mouth']
        bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=mo['c'])
        cut = bpy.context.active_object
        cut.scale = mo['r']
        cn = E.Noise(spec['seed'] + 7)
        for v in cut.data.vertices:
            p = np.array(v.co[:])
            v.co = p * (1 + 0.18 * cn.n3(p * 2.5 + 4))
        mod = ob.modifiers.new('mouth', 'BOOLEAN')
        mod.operation, mod.object, mod.solver = 'DIFFERENCE', cut, 'EXACT'
        E.set_active(ob)
        bpy.ops.object.modifier_apply(modifier='mouth')
        bpy.data.objects.remove(cut)
        E.voxel_remesh(ob, spec['voxel'])
        print(name, 'mouth cut, faces', len(ob.data.polygons))
    s = spec['voxel'] / 0.1      # scale weathering with the piece
    E.smooth(ob, 0.5, 1)
    E.displace(ob, E.tex('lumps', 'CLOUDS', noise_scale=3.5 * s, noise_depth=2), 0.18 * s)
    c1, m1, c2, m2, cs = spec.get('rock', (14, 0.22, 5, 0.08, 16))[:5]
    rockify(ob, c1 * s, 0.8, 0.55, spec['seed'], max_cut=m1 * s)            # big fracture faces
    rockify(ob, c2 * s, 1.0, 0.5, spec['seed'] + 1, max_cut=m2 * s)         # smaller chips
    if len(spec.get('rock', ())) > 5:
        rockify(ob, spec['rock'][5] * s, 1.1, 0.5, spec['seed'] + 2, max_cut=0.04 * s)   # fine chipping
    cracks(ob, cs * s, 0.08 * s, 0.09 * s, spec['seed'])
    crack = E.tex('crack', 'VORONOI', noise_scale=1.2 * s, distance_metric='DISTANCE',
                  weight_1=-1.0, weight_2=1.0, noise_intensity=1.0)
    E.displace(ob, crack, 0.06 * s, mid=0.0)
    E.displace(ob, E.tex('grain', 'STUCCI', noise_scale=0.35 * s, turbulence=4.0), 0.025 * s)
    E.cut_below(ob, -0.6 * s)
    print(name, 'high tris', E.tris(ob))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', name + '_high.blend'), compress=True)


if __name__ == '__main__':
    a = E.args()
    names = list(PIECES) if not a or a[0] == 'all' else a
    for nm in names:
        build(nm)
