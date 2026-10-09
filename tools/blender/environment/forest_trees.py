"""Expedition forest kit: stiltwoods (ART_DIRECTION s5.1 #3) and mid trees, painterly (F4_f).

The Meshy stiltwood meshes from 2026-10-09 (art_source/environment/meshy/models/stilt_A/B/C.glb) were reviewed and not
used: A stands on a base plate with its roots fused into a skirt, B is a thin dead tree with a noisy root mat, C has
dozens of thin root tendrils. Painterly needs a few big, clean stilt arches you can run under (PAINTERLY s2), so the
trees are procedural: a fluted trunk lifted on 8-12 thick arched stilt roots, 4-5 heavy limbs, crowns of painted
canopy cards (FP_Canopy_P atlas) on the limb ends, leaf clumps where the limbs fork, ferns at the root feet.

Outputs (Forest/): FT_Stiltwood_A, FT_Stiltwood_B, FT_Stiltwood_Gate (roots only to the sides: the run passes under
the lifted trunk; clear box |x| <= 4.5 m, z <= 6.5 m), FT_MidTree_A, FT_MidTree_B.
Each FBX: <name>_LOD0..2 (bark + leaf clumps, kit paint baked to Forest/Textures/<set>_*), and
<name>_Crowns_LOD0..2 (alpha-clip cards, material FP_Canopy_P, vertex colour R = wind, A = AO; dome normals).
Texture sets: FT_Stiltwood_A / _B / _Gate (one 1024 each), FT_MidTrees (A + B share one 1024).
Usage: blender ... -P forest_trees.py -- [A B Gate mid]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
import forestlib as F  # noqa: E402
from forestlib import blob, sweep  # noqa: E402
from plants_build import TEX, sprites  # noqa: E402
from plants_v2_build import Builder, mats_for  # noqa: E402

FOR = os.path.join(F.ENV, 'Forest')
CANOPY = 'FP_Canopy_P'


def stilt_tree(H, r0, r1, stilt_h, n_roots, ground_r, limbs, lean=(0.0, 0.0), az_range=None, base_z=None, seed=0,
               crowns=None, crown_scale=1.0):
    """az_range: allowed root azimuths (rad intervals around the x axis) for the gate tree."""
    def fn(acc, nz, rng):
        crowns.clear()
        zb = base_z if base_z is not None else stilt_h[0] - 1.2
        zs = np.linspace(zb, H, 9)
        pts = [(lean[0] * (z - zb) ** 1.3 / H, lean[1] * (z - zb) ** 1.3 / H, z) for z in zs]
        rad = [r0 * 1.35, r0 * 1.15] + list(np.linspace(r0, r1, 7))
        acc.use('sbark')
        sweep(acc, nz, [(pts[0][0], pts[0][1], zb - r0 * 0.6)] + pts, [r0 * 0.7] + rad, 0.5, nt=16, seg=1.5,
              ridge=(13, 0.06, 0.03), wob=0.04)
        # stilt roots: leave the trunk between stilt_h, arch out and down into the ground
        azs = []
        arc = 2 * math.pi if az_range is None else sum(2 * w for _, w in az_range)
        gap = arc / n_roots * 0.55
        tries = 0
        while len(azs) < n_roots and tries < 20000:
            tries += 1
            a = rng.uniform(0, 2 * math.pi)
            if az_range is not None and not any(abs(((a - c + math.pi) % (2 * math.pi)) - math.pi) < w for c, w in az_range):
                continue
            if all(abs(((a - b + math.pi) % (2 * math.pi)) - math.pi) > gap for b in azs):
                azs.append(a)
        for a in azs:
            d = np.array([math.cos(a), math.sin(a), 0.0])
            h = rng.uniform(*stilt_h)
            R = rng.uniform(*ground_r)
            rr = rng.uniform(0.38, 0.5) * r0 / 1.5
            p0 = np.array([0, 0, h + 0.8]) + d * r0 * 0.4
            p1 = np.array([0, 0, h]) + d * (r0 + 0.6)
            p2 = np.array([0, 0, h * 0.72]) + d * (r0 + (R - r0) * (0.62 if az_range else 0.45)) + rng.normal(size=3) * [0.3, 0.3, 0]
            p3 = np.array([0, 0, h * 0.25]) + d * (R * 0.92)
            p4 = np.array([0, 0, -0.45]) + d * R
            sweep(acc, nz, [p0, p1, p2, p3, p4], [rr * 1.3, rr, rr * 0.85, rr * 0.85, rr * 1.15], rng.random(), nt=8,
                  seg=0.6, ridge=(5, 0.06, 0.15), wob=0.06)
            if az_range is None and rng.random() < 0.45:   # a fork: second leg off the outer half
                side = np.cross(d, [0, 0, 1]) * rng.choice([-1, 1])
                q0 = p2 * 0.6 + p3 * 0.4
                q2 = p4 + side * rng.uniform(1.2, 2.2) - d * 0.8
                sweep(acc, nz, [q0, (q0 + q2) / 2 + np.array([0, 0, h * 0.12]), q2], [rr * 0.65, rr * 0.55, rr * 0.7],
                      rng.random(), nt=7, seg=0.6)
            if az_range is None or rng.random() < 0.7:
                blob(acc, nz, tuple(p4 + d * 0.4 + np.array([0, 0, 0.2])), rng.uniform(0.6, 1.0), rng.random(), squash=0.7)
        # limbs + crowns
        top = np.array(pts[-1])
        for k in range(limbs):
            a = 2 * math.pi * k / limbs + rng.uniform(-0.3, 0.3)
            d = np.array([math.cos(a), math.sin(a), 0.0])
            z0 = H * rng.uniform(0.8, 0.95)
            base = np.array([pts[-2][0], pts[-2][1], z0])
            L = rng.uniform(5.0, 8.5) * crown_scale
            p1 = base + d * L * 0.45 + np.array([0, 0, L * 0.35])
            p2 = base + d * L + np.array([0, 0, L * 0.55])
            sweep(acc, nz, [base, p1, p2], [r1 * 0.75, r1 * 0.45, r1 * 0.2], rng.random(), nt=9, seg=0.9,
                  ridge=(7, 0.05, 0.05))
            for j in range(2):
                blob(acc, nz, tuple(p1 * (0.4 + 0.3 * j) + p2 * (0.6 - 0.3 * j) + np.array([0, 0, 0.6])),
                     rng.uniform(1.2, 1.8) * crown_scale, rng.random(), squash=0.7)
            crowns.append((tuple(p2 + np.array([0, 0, -1.0])), rng.uniform(0.85, 1.15) * crown_scale, rng.integers(4)))
        crowns.append((tuple(top + np.array([0, 0, 0.5])), 1.2 * crown_scale, rng.integers(4)))
        for j in range(3):
            blob(acc, nz, tuple(top + np.array([rng.uniform(-2, 2), rng.uniform(-2, 2), 0.8])), 1.8 * crown_scale,
                 rng.random(), squash=0.7)
    return fn


def mid_tree(H, r0, r1, limbs, bend, crowns):
    def fn(acc, nz, rng):
        crowns.clear()
        zs = np.linspace(-0.5, H, 8)
        pts = [(bend * math.sin(z / H * 2.4), 0.3 * bend * math.sin(z / H * 3.1), z) for z in zs]
        acc.use('sbark')
        sweep(acc, nz, pts, [r0 * 1.5, r0 * 1.05] + list(np.linspace(r0, r1, 6)), 0.4, nt=12, seg=1.4,
              ridge=(9, 0.06, 0.04), wob=0.04)
        for k in range(6):          # buttress roots
            a = 2 * math.pi * k / 6 + rng.uniform(-0.3, 0.3)
            d = np.array([math.cos(a), math.sin(a), 0.0])
            sweep(acc, nz, [np.array([0, 0, 2.2]) + d * r0 * 0.5, np.array([0, 0, 0.9]) + d * (r0 + 0.6),
                            np.array([0, 0, -0.4]) + d * (r0 + rng.uniform(1.4, 2.2))],
                  [r0 * 0.4, r0 * 0.35, r0 * 0.25], rng.random(), nt=6, seg=0.5, flat=0.55)
            if k % 2 == 0:
                blob(acc, nz, tuple(d * (r0 + 1.6) + np.array([0, 0, 0.2])), rng.uniform(0.5, 0.8), rng.random(), squash=0.7)
        top = np.array(pts[-1])
        for k in range(limbs):
            a = 2 * math.pi * k / limbs + rng.uniform(-0.4, 0.4)
            d = np.array([math.cos(a), math.sin(a), 0.0])
            base = np.array([pts[-2][0], pts[-2][1], H * rng.uniform(0.7, 0.9)])
            L = rng.uniform(3.0, 5.0)
            p2 = base + d * L + np.array([0, 0, L * 0.6])
            sweep(acc, nz, [base, base + d * L * 0.5 + np.array([0, 0, L * 0.3]), p2], [r1 * 0.8, r1 * 0.5, r1 * 0.2],
                  rng.random(), nt=7, seg=0.8)
            blob(acc, nz, tuple(p2 + np.array([0, 0, 0.3])), rng.uniform(0.9, 1.3), rng.random(), squash=0.75)
            crowns.append((tuple(p2 + np.array([0, 0, -0.8])), rng.uniform(0.55, 0.75), rng.integers(4)))
        crowns.append((tuple(top + np.array([0, 0, 0.2])), 0.8, rng.integers(4)))
    return fn


def crown_cards(name, crowns, base_w=9.0):
    """Star crowns: 3 vertical cards at 0/60/120 deg (LOD0), 2 (LOD1), 1 broadside card (LOD2), dome normals."""
    sp = sprites(os.path.join(TEX, CANOPY + '_BaseColor.png'))
    mats = mats_for(CANOPY)
    out = []
    for lod, yaws in enumerate(((0, 60, 120), (30, 120), (90,))):
        B = Builder()
        ctrs = []
        for k, (c, sc, sid) in enumerate(crowns):
            s = dict(sp[int(sid) % len(sp)])
            if k % 2:
                s['u0'], s['u1'] = s['u1'], s['u0']
            W = base_w * sc
            Hc = W / s['aspect']
            c = Vector(c)
            for yw in yaws:
                a = math.radians(yw + 17 * k)
                side = Vector((math.cos(a), math.sin(a), 0))
                pts, uvs, cols = [], [], []
                for i in range(3):
                    t = i / 2
                    for j in range(3):
                        u = j / 2
                        nrm = Vector((-side.y, side.x, 0))
                        bul = 0.07 * W * math.sin(math.pi * t) * math.sin(math.pi * u)
                        q = c + side * (u - 0.5) * W + Vector((0, 0, (t - 0.35) * Hc)) + nrm * bul
                        pts.append(tuple(q))
                        uvs.append((s['u0'] + (s['u1'] - s['u0']) * u, s['v0'] + (s['v1'] - s['v0']) * t))
                        cols.append((0.2 + 0.5 * t, 0, 0, 0.5 + 0.5 * t))
                B.grid(pts, uvs, cols, 2, 2, 0)
            ctrs.append(c)
        ob = B.object(f'{name}_Crowns_LOD{lod}', mats)
        me = ob.data
        vn = []
        for v in me.vertices:
            cc = min(ctrs, key=lambda q: (q - v.co).length)
            d = (v.co - cc + Vector((0, 0, 1.0))).normalized()    # dome centre 1 m below the crown centre
            vn.append(tuple((Vector(v.normal) * (1 if Vector(v.normal).dot(d) > 0 else -1) * 0.4 + d * 0.6).normalized()))
        me.normals_split_custom_set_from_vertices(vn)
        out.append(ob)
    return out


def make(name, fn_builder, lod=(7000, 3000, 1000), wz=None, base_w=9.0, **kw):
    crowns = []
    fn = fn_builder(crowns=crowns, **kw)
    return dict(name=name, fn=fn, lod=lod, drop=(0.3, 1.5), directional=False, cull=True, ground=0.0, cut=-0.3,
                wz=wz or (lambda z: 1.0 if z < 8 else (0.7 if z < 16 else 0.45)),
                post=lambda pc: crown_cards(pc['name'], crowns, base_w), crowns=crowns)


def gate_check(name):
    """Camera/run corridor under the gate tree: nothing inside |x| <= 4.5, |y| <= 6, z <= 6.5."""
    ob = bpy.data.objects.get(name + '_LOD0')
    if ob is None:
        return
    V = F.verts(ob)
    m = (np.abs(V[:, 0]) <= 4.5) & (np.abs(V[:, 1]) <= 6) & (V[:, 2] <= 6.5) & (V[:, 2] > 0.05)
    print('GATE corridor verts inside the clear box:', int(m.sum()), 'lowest z over |x|<=4.5:',
          round(float(V[(np.abs(V[:, 0]) <= 4.5) & (np.abs(V[:, 1]) <= 6) & (V[:, 2] > 0.05), 2].min()), 2))


TREES = {
    'A': ('FT_Stiltwood_A', [make('FT_Stiltwood_A', lambda crowns, **k: stilt_tree(34, 1.5, 1.05, (4.5, 7.5), 10, (5.0, 7.5), 5,
                                                                                     crowns=crowns, seed=1))]),
    'B': ('FT_Stiltwood_B', [make('FT_Stiltwood_B', lambda crowns, **k: stilt_tree(42, 1.7, 1.1, (6.0, 9.0), 12, (6.0, 9.0), 4,
                                                                                     lean=(1.5, 0.6), crowns=crowns,
                                                                                     crown_scale=1.1))]),
    'Gate': ('FT_Stiltwood_Gate', [make('FT_Stiltwood_Gate', lambda crowns, **k: stilt_tree(
        36, 1.6, 1.1, (8.6, 10.5), 8, (8.0, 10.0), 5, az_range=[(0.0, 0.5), (math.pi, 0.5)], base_z=8.4,
        crowns=crowns))]),
    'mid': ('FT_MidTrees', [make('FT_MidTree_A', lambda crowns, **k: mid_tree(20, 0.6, 0.38, 3, 0.6, crowns),
                                 lod=(3000, 1300, 450), base_w=7.0, wz=lambda z: 1.0 if z < 6 else 0.6),
                            make('FT_MidTree_B', lambda crowns, **k: mid_tree(24, 0.7, 0.42, 4, 1.2, crowns),
                                 lod=(3000, 1300, 450), base_w=7.0, wz=lambda z: 1.0 if z < 6 else 0.6)]),
}

if __name__ == '__main__':
    a = E.args() or list(TREES)
    for k in a:
        nm, pieces = TREES[k]
        F.bake_set(nm, pieces, 1024, FOR, os.path.join(FOR, 'Textures'), spacing=60.0)
        if k == 'Gate':
            gate_check('FT_Stiltwood_Gate')
