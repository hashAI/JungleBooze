"""RS_HeroArch v2, stage 1: high-poly sculpt of the F4_e hero arch as a braid of separate strands.

v1 read as one smooth blob (8 strands fused over a solid core). v2 follows the keyframe: three ropes twist around
the arch spine, each rope a braid of 4-6 round strands with dark gaps between them; the ropes splay apart in the legs
into separate root feet (sky shows between them), some strands loop away from their rope (holes), a few dive into
the bundle and end, thin roots wrap across the outside, and a recessed core under the crown keeps deep dark crevices.
Weathering (facets, chips, transverse cracks) comes from rootstone_high.

Usage: blender ... -P heroarch_v2_high.py [-- --quick]   (--quick: 30 cm voxels, no weathering, for silhouette checks)
Output: work/rootstone/RS_HeroArch_high.blend (object 'RS_HeroArch_high', float-color attr 'strand' as in
rootstone_high: R = strand random, G = metres along the strand, B/A = cos/sin around the strand).
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_high import chunk, cracks, keyed, rockify  # noqa: E402

NAME = 'RS_HeroArch'
SPEC = dict(
    # same footprint and crown as v1 (tech-architect's Span placement and the WaterfallMouth stay valid)
    spine=[(-21, 2, 0), (-21.5, 1.5, 12), (-19, 0.5, 25), (-12, 0, 36), (-2, -0.5, 41.5), (8, 0, 40),
           (16, 0.8, 31), (20, 1.5, 17), (22, 2.5, 0)],
    rb=[(0, 8.6), (0.08, 7.4), (0.25, 6.6), (0.42, 7.6), (0.5, 8.2), (0.6, 7.4), (0.78, 6.5), (0.92, 7.4), (1, 8.6)],
    ropes=[dict(n=5, splay=(1.25, 0.7)), dict(n=5, splay=(0.55, 1.2)), dict(n=4, splay=(0.9, 0.95))],
    rope_off=0.58, rope_r=0.5, rope_turns=0.5, strand_turns=0.9,
    secondary=3, voxel=0.12, seed=111,
    mouth=dict(c=(-1.0, -0.6, 35.6), r=(5.0, 4.4, 4.6)),
)


def splay(u, w=0.17):
    a = np.clip(1 - u / w, 0, 1) ** 2
    b = np.clip((u - (1 - w)) / w, 0, 1) ** 2
    return a, b


def build(quick=False):
    sp = dict(SPEC, voxel=0.3) if quick else SPEC
    E.reset()
    rng = np.random.default_rng(sp['seed'])
    nz = E.Noise(sp['seed'])
    spine = np.array(sp['spine'], float)
    length = float(np.sum(np.linalg.norm(np.diff(spine, axis=0), axis=1)))
    m = int(length / 0.35)
    C = E.catmull(spine, m)
    T, N, B = E.frames(C)
    u = np.linspace(0, 1, m)
    Rb = keyed(sp['rb'], u) * (1 + 0.08 * nz.fbm1(u * length / 14 + 3.1))
    sa, sb = splay(u)
    parts = []
    tails_done = []

    def tail(P, r, end, center, theta):
        """Planted end: the strand leaves its rope and runs out over the ground as a root."""
        e = P[0] if end == 0 else P[-1]
        d = np.array([e[0] - center[0], e[1] - center[1], 0.0])
        if np.linalg.norm(d) < 1e-3:
            d = np.array([math.cos(theta), math.sin(theta), 0])
        d /= np.linalg.norm(d)
        side = np.array([-d[1], d[0], 0])
        r0 = r[0] if end == 0 else r[-1]
        L = r0 * rng.uniform(2.0, 4.5)
        curl = rng.uniform(-0.6, 0.6)
        tp, tr = [], []
        for k in range(1, 15):
            t = k / 14
            p = e + d * L * t + side * L * curl * t * t
            p[2] = e[2] * (1 - t) ** 1.3 - r0 * 1.0 * t ** 1.5
            tp.append(p)
            tr.append(r0 * (1 - 0.45 * t))
        if end == 0:
            return tp[::-1] + list(P), tr[::-1] + list(r)
        return list(P) + tp, list(r) + tr

    # recessed core under the crown only: dark crevices behind the ropes, legs stay open
    core_sel = (u > 0.14) & (u < 0.86)
    cr = Rb[core_sel] * 0.3 * np.clip(np.minimum(u[core_sel] - 0.14, 0.86 - u[core_sel]) / 0.08, 0.15, 1)
    parts.append(E.tube(C[core_sel], cr, ring=16, cap=True, attr=(rng.random(), 0.0)))

    sid = 0
    for k, rope in enumerate(sp['ropes']):
        phi = 2 * math.pi * k / len(sp['ropes']) + 2 * math.pi * sp['rope_turns'] * u + 0.45 * nz.fbm1(u * 5 + k * 7.1)
        rho = Rb * (sp['rope_off'] + rope['splay'][0] * sa + rope['splay'][1] * sb)
        Rk = C + rho[:, None] * (np.cos(phi)[:, None] * N + np.sin(phi)[:, None] * B)
        Rk = Rk + Rb[:, None] * 0.05 * nz.vec3(Rk / 9 + k * 3.3)
        Rsub = Rb * sp['rope_r'] * (1 + 0.1 * nz.fbm1(u * length / 10 + k * 4.4))
        Tk, Nk, Bk = E.frames(Rk)
        nst = rope['n']
        sdir = 1 if k % 2 == 0 else -1
        for j in range(nst):
            sid += 1
            psi = (2 * math.pi * j / nst + rng.uniform(-0.2, 0.2) + 2 * math.pi * sp['strand_turns'] * sdir * u
                   + 0.28 * nz.fbm1(u * 7 + sid * 3.7))
            braid = 0.12 * np.sin(2 * math.pi * 3.0 * u + sid * 1.9)
            peel = np.zeros_like(u)
            if rng.random() < 0.45:          # a strand loops away from its rope and comes back (hole)
                uc, w, amp = rng.uniform(0.2, 0.8), rng.uniform(0.03, 0.06), rng.uniform(0.6, 1.1)
                peel += amp * np.exp(-((u - uc) / w) ** 2)
            sig = Rsub * (0.58 + braid + peel)
            rad = Rsub * rng.uniform(0.44, 0.54) * (1 + 0.16 * nz.fbm1(u * length / 6 + sid * 9.1))
            # some strands dive into the bundle and end (u0 > 0 or u1 < 1): sigma and radius ramp down
            u0, u1 = 0.0, 1.0
            r_ = rng.random()
            if r_ < 0.18:
                u1 = rng.uniform(0.55, 0.85)
            elif r_ < 0.32:
                u0 = rng.uniform(0.15, 0.45)
            sel = (u >= u0) & (u <= u1)
            ramp = np.ones_like(u)
            if u1 < 1:
                ramp = np.minimum(ramp, np.clip((u1 - u) / 0.07, 0, 1))
            if u0 > 0:
                ramp = np.minimum(ramp, np.clip((u - u0) / 0.07, 0, 1))
            sig = sig * (0.25 + 0.75 * ramp)
            rad = rad * (0.55 + 0.45 * ramp)
            P = Rk + sig[:, None] * (np.cos(psi)[:, None] * Nk + np.sin(psi)[:, None] * Bk)
            P = P + Rsub[:, None] * 0.06 * nz.vec3(P / 7 + sid * 5.1)
            P, r = P[sel], rad[sel]
            if u0 == 0:
                P, r = tail(P, r, 0, Rk[0], psi[0])
            if u1 == 1:
                P, r = tail(P, r, 1, Rk[-1], psi[-1])
            P, r = np.array(P), np.array(r)
            parts.append(E.tube(P, r, ring=22, groove=(0.085, 5, 0.22 / max(r.mean(), 0.2)), cap=True,
                                attr=(rng.random(), 0.0)))

    # thin roots wrapping across the outside of the bundle (counter-twist), some running to the ground
    for j in range(sp['secondary']):
        a = rng.uniform(0, 0.55)
        b = min(1.0, a + rng.uniform(0.3, 0.6))
        if rng.random() < 0.4:
            a = 0.0
        if rng.random() < 0.35:
            b = 1.0
        sel = (u >= a) & (u <= b)
        th = rng.uniform(0, 2 * math.pi) - 2 * math.pi * rng.uniform(0.8, 1.6) * u
        off = Rb * (sp['rope_off'] + sp['rope_r'] * 1.05 + 0.6 * sa + 0.6 * sb)
        P = C + off[:, None] * (np.cos(th)[:, None] * N + np.sin(th)[:, None] * B)
        P = P + Rb[:, None] * 0.05 * nz.vec3(P / 5 + 40 + j)
        r = Rb * rng.uniform(0.12, 0.17)
        P, r = P[sel], r[sel]
        nt = max(3, len(r) // 10)
        r[:nt] *= np.linspace(0.5, 1, nt) if a > 0 else 1
        r[-nt:] *= np.linspace(1, 0.5, nt) if b < 1 else 1
        parts.append(E.tube(P, r, ring=10, cap=True, attr=(rng.random(), 1.0)))

    # debris: faceted chunks half buried around both feet
    for end in (0, 1):
        c = C[0] if end == 0 else C[-1]
        rb0 = Rb[0] if end == 0 else Rb[-1]
        for k in range(5):
            a = rng.uniform(0, 2 * math.pi)
            dist = rb0 * rng.uniform(0.9, 2.3)
            sz = rb0 * rng.uniform(0.07, 0.22)
            parts.append(chunk(np.array([c[0] + math.cos(a) * dist, c[1] + math.sin(a) * dist, -0.35 * sz]), sz, rng))

    ob = E.mesh_from(NAME + '_high', parts)
    print(NAME, 'tube verts', len(ob.data.vertices))
    E.voxel_remesh(ob, sp['voxel'])
    print(NAME, 'remeshed faces', len(ob.data.polygons))
    if quick:                       # silhouette iteration: no weathering
        bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_quick.blend'), compress=True)
        return
    s = sp['voxel'] / 0.1
    E.smooth(ob, 0.5, 1)
    E.displace(ob, E.tex('lumps', 'CLOUDS', noise_scale=3.0 * s, noise_depth=2), 0.14 * s)
    # no broad fracture pass: on round strands it reads as low-poly planks (v2 iteration 1)
    rockify(ob, 2.6 * s, 1.0, 0.55, sp['seed'] + 1, max_cut=0.09 * s)   # chipped faces
    rockify(ob, 1.0 * s, 1.1, 0.5, sp['seed'] + 2, max_cut=0.05 * s)   # fine chipping
    cracks(ob, 3.6 * s, 0.07 * s, 0.07 * s, sp['seed'])   # sparse: dense transverse cracks read as bamboo nodes
    crack = E.tex('crack', 'VORONOI', noise_scale=1.1 * s, distance_metric='DISTANCE',
                  weight_1=-1.0, weight_2=1.0, noise_intensity=1.0)
    E.displace(ob, crack, 0.07 * s, mid=0.0)
    E.displace(ob, E.tex('grain', 'STUCCI', noise_scale=0.35 * s, turbulence=4.0), 0.025 * s)
    E.cut_below(ob, -0.5 * s)
    print(NAME, 'high tris', E.tris(ob))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_high.blend'), compress=True)


if __name__ == '__main__':
    build(quick='--quick' in E.args())
