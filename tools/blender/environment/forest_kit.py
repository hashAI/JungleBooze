"""Expedition forest kit (painterly, F4_f): gameplay obstacles, pickups, discovery markers, path/terrain pieces.

Sizes follow spec 101 s4.1 / s2.6 and spec 103 s3-s9 (hitbox per piece in the README). Shape language (ART_DIRECTION
s5/C5, PAINTERLY s2): obstacles are bold, few-part silhouettes; jump obstacles are long horizontal masses on the
floor, slide obstacles are arches with a clear lit gap under them, blockers are tall leaning stacked rocks or root
walls, thorns are dark tangles with crimson tips (hazard colour only on hazards). Obstacle bodies are darker than the
golden path and carry a bright worn top rim, so the edge that matters reads at 16 m/s.

Sets (each = one 1024 or 512 texture set + one material, pieces share it):
  OB_Wood   roots, logs, branches, vine curtain, vine anchor + rope, root walls, swim logs/debris/snag/low branch
  OB_Stone  blocker boulders, river rock, wet low rocks, gap lips, stepping rocks
  OB_Hazard thorn patches (512)
  PK_Pickups coin, crystal, Shield, discovery cairn + survey post (512)
  TR_Canopy canopy beams, branch knot, canopy platform (Forest/)
  TR_Edges  path edges (roots / moss), riverbank, shallow-water edge (Forest/)
Pivot: the obstacle's front face centre on the floor (Unity local z = 0 is the authored front face `s`, the piece
extends toward +Z), water pieces at the water surface, beams at the walk top of their near end.
Usage: blender ... -P forest_kit.py -- [wood stone hazard pickups canopy edges]
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
import forestlib as F  # noqa: E402
from forestlib import blob, cone, facet_prism, lathe, moss_cap, rock, sweep  # noqa: E402

OBS = os.path.join(F.ENV, 'Obstacles')
FOR = os.path.join(F.ENV, 'Forest')
SMALL = (0, 0.9)
F_MK_CLOTH = 17


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


# ================================================================== wood
def ground_root(acc, nz, rng, x0, x1, r, top, yc, meander=0.08, flat=1.3, nt=8, kind='bark', rise=0.7):
    """A root lying across the path: dives into the ground at both ends, top never above `top`."""
    acc.use(kind)
    xs = np.linspace(x0, x1, 10)
    ph = rng.uniform(0, 6.3)
    pts = []
    for x in xs:
        e = float(np.clip(min(x - x0, x1 - x) / rise, 0, 1))
        e = e * e * (3 - 2 * e)
        z = (-r * 0.9) * (1 - e) + (top - r - 0.025 - 0.035 * (0.5 + 0.5 * math.sin(x * 1.3 + ph))) * e
        pts.append((x, yc + meander * math.sin(x * 0.9 + ph), z))
    sweep(acc, nz, pts, [r * 0.8, r, r, r * 0.95, r * 0.8], rng.random(), nt=nt, seg=0.35, flat=flat, p=2.2,
          wob=0.05, ridge=(5, 0.06, 0.5))


def p_root_low(top, strands, end_bushes=True):
    def fn(acc, nz, rng):
        for (x0, x1, r, t, yc) in strands:
            ground_root(acc, nz, rng, x0, x1, r, t, yc)
        if end_bushes:
            for sx in (-1, 1):
                blob(acc, nz, (sx * 4.35, -0.3, 0.05), 0.45, rng.random(), squash=0.7)
                blob(acc, nz, (sx * 4.0, -0.05, -0.05), 0.3, rng.random(), squash=0.6)
    return fn


def p_root_half(acc, nz, rng):
    ground_root(acc, nz, rng, -2.05, 2.45, 0.26, 0.5, -0.25, rise=0.5)
    ground_root(acc, nz, rng, -1.2, 2.6, 0.17, 0.38, -0.47, rise=0.5)
    blob(acc, nz, (-2.15, -0.3, -0.05), 0.42, 0.3, squash=0.45, kind='earth')
    moss_cap(acc, nz, (-2.2, -0.3, 0.02), 0.32, 0.26, 0.4, h=0.12)
    blob(acc, nz, (2.55, -0.3, 0.05), 0.4, rng.random(), squash=0.7)


def stub(acc, nz, base, d, L, r, rnd):
    d = np.asarray(d, float) / np.linalg.norm(d)
    b = np.asarray(base, float)
    sweep(acc, nz, [b - d * 0.2, b + d * L * 0.5, b + d * L], [r, r * 0.75, r * 0.4], rnd, nt=6, seg=0.25,
          caps=(False, True), cap_mk=F.MK['endgrain'])


def p_log_walk(acc, nz, rng):
    acc.use('bark')
    pts = [(-4.3, -1.0, -0.1), (-1.5, -0.95, -0.1), (1.5, -1.05, -0.1), (4.3, -1.0, -0.1)]
    sweep(acc, nz, pts, [0.96, 1.0, 1.0, 0.97], 0.4, nt=14, seg=0.55, flat=1.0, p=2.3, wob=0.03,
          ridge=(11, 0.035, 0.12), cap_mk=F.MK['endgrain'])
    acc.use('bark')
    stub(acc, nz, (-3.9, -0.1, 0.2), (0.0, 1.0, 0.25), 0.7, 0.16, 0.2)
    stub(acc, nz, (3.6, -1.9, 0.25), (0.3, -1.0, 0.2), 0.8, 0.15, 0.7)
    stub(acc, nz, (1.2, -1.95, 0.1), (0.0, -1.0, -0.1), 0.6, 0.13, 0.5)
    for sx in (-1, 1):
        blob(acc, nz, (sx * 4.55, -0.6, 0.15), 0.55, rng.random(), squash=0.75)
        blob(acc, nz, (sx * 4.4, -1.6, 0.05), 0.42, rng.random(), squash=0.7)
    moss_cap(acc, nz, (-3.2, -1.0, 0.72), 0.55, 0.6, 0.3, h=0.12)


def p_log_giant(acc, nz, rng):
    acc.use('bark')
    pts = [(-4.9, -2.5, -0.3), (-1.0, -2.45, -0.3), (2.5, -2.55, -0.3), (4.75, -2.5, -0.3)]
    sweep(acc, nz, pts, [1.17, 1.2, 1.2, 1.2], 0.6, nt=16, seg=0.7, flat=2.1, p=4.5, wob=0.025,
          ridge=(14, 0.03, 0.08), caps=(True, False), cap_mk=F.MK['endgrain'])
    # root plate at +x (the uprooted base): a thick earth disc with root ends radiating out of it
    acc.use('earth')
    sweep(acc, nz, [(4.6, -2.5, 0.0), (5.2, -2.5, 0.0)], [2.7, 2.7], 0.3, nt=14, seg=0.3, flat=1.0, p=2.0,
          wob=0.12, mk=F.MK['earth'])
    acc.use('bark')
    for k in range(9):
        a = 2 * math.pi * k / 9 + 0.3
        c0 = np.array([4.9, -2.5, 0.0])
        d = np.array([0.0, math.cos(a), math.sin(a)])
        p1 = c0 + d * 2.3 + np.array([0.3, 0, 0])
        p2 = c0 + d * 3.2 + np.array([0.9 + 0.3 * rng.random(), 0, 0])
        if p2[2] < -0.3:
            p2[2] = -0.3
        sweep(acc, nz, [c0 + d * 1.2, p1, p2], [0.32, 0.22, 0.08], rng.random(), nt=6, seg=0.35)
    for k in range(4):
        blob(acc, nz, (5.0 + rng.uniform(-0.2, 0.3), -2.5 + rng.uniform(-1.6, 1.6), 2.3 + rng.uniform(0, 0.5)),
             rng.uniform(0.4, 0.65), rng.random(), squash=0.8)
    moss_cap(acc, nz, (-2.8, -0.6, 0.62), 0.9, 0.5, 0.2, h=0.18)
    moss_cap(acc, nz, (0.9, -4.3, 0.6), 1.1, 0.5, 0.6, h=0.18)
    blob(acc, nz, (-5.1, -1.2, 0.2), 0.6, 0.4, squash=0.75)
    blob(acc, nz, (-5.0, -3.8, 0.1), 0.5, 0.8, squash=0.7)


def p_driftwood(acc, nz, rng):
    acc.use('drift')
    pts = [(-4.2, -0.25, 0.04), (-2.0, -0.4, 0.2), (0.5, -0.33, 0.2), (2.5, -0.27, 0.18), (4.15, -0.45, 0.03)]
    sweep(acc, nz, pts, [0.16, 0.29, 0.3, 0.27, 0.14], 0.5, nt=10, seg=0.35, flat=1.05, p=2.1, wob=0.04,
          ridge=(7, 0.05, 0.3))
    acc.use('drift')
    sweep(acc, nz, [(-1.4, -0.55, 0.22), (-1.0, -1.1, 0.25), (-0.4, -1.6, 0.12)], [0.12, 0.08, 0.04], 0.3, nt=6,
          seg=0.25)
    sweep(acc, nz, [(2.2, -0.1, 0.25), (2.6, 0.4, 0.3), (3.3, 0.7, 0.18)], [0.1, 0.07, 0.03], 0.6, nt=6, seg=0.25)


def p_branch_high(acc, nz, rng):
    acc.use('bark')
    pts = [(-4.8, -0.35, -0.5), (-4.4, -0.35, 0.65), (-3.88, -0.38, 1.47), (-2.0, -0.3, 1.52), (0.0, -0.4, 1.5),
           (2.0, -0.3, 1.53), (3.88, -0.36, 1.48), (4.4, -0.35, 0.65), (4.8, -0.35, -0.5)]
    sweep(acc, nz, pts, [0.6, 0.48, 0.42, 0.41, 0.42, 0.42, 0.48, 0.6], 0.3, nt=10, seg=0.4, flat=1.05, p=2.2,
          wob=0.03, ridge=(6, 0.05, 0.4))
    sweep(acc, nz, [(-4.5, -0.15, -0.2), (-3.6, -0.1, 1.6), (-1.0, -0.05, 2.1), (2.0, -0.6, 2.05), (3.7, -0.55, 1.75),
                    (4.6, -0.6, -0.2)], [0.2, 0.17, 0.16, 0.15, 0.17, 0.2], 0.7, nt=7, seg=0.4)
    for k in range(7):
        x = -3.4 + k * 1.13 + rng.uniform(-0.2, 0.2)
        blob(acc, nz, (x, -0.35 + rng.uniform(-0.25, 0.15), 2.05 + rng.uniform(0, 0.25)), rng.uniform(0.45, 0.72),
             rng.random(), squash=0.8)
    acc.use('moss')
    for k in range(7):
        x = -3.0 + k * 1.0 + rng.uniform(-0.25, 0.25)
        z0 = 1.72
        sweep(acc, nz, [(x, 0.06, z0), (x + 0.03, 0.1, z0 - 0.25), (x - 0.02, 0.08, 1.16 + rng.uniform(0, 0.12))],
              [0.07, 0.05, 0.025], rng.random(), nt=5, seg=0.12, mk=F.MK['moss'])
    for sx in (-1, 1):
        blob(acc, nz, (sx * 4.75, -0.2, 0.25), 0.6, rng.random(), squash=0.8)


def curtain_strands(acc, nz, rng, xs, y, top, bottom_min=1.06):
    """Leafy hanging ribbons (flat bands painted as leaves) in clumps of 3, bottoms 1.06-1.3 m: a dense, readable
    curtain with a clear bottom line (slide under)."""
    for x in xs:
        for k in range(3):
            xx = x + rng.uniform(-0.18, 0.18)
            yy = y + rng.uniform(-0.15, 0.15)
            zb = bottom_min + rng.uniform(0.02, 0.24)
            sw = rng.uniform(-0.08, 0.08)
            acc.use('bush')
            sweep(acc, nz, [(xx, yy, top), (xx + sw, yy + 0.03, (top + zb) / 2), (xx + sw * 0.5, yy + 0.06, zb + 0.06)],
                  [0.05, 0.045, 0.03], rng.random(), nt=4, seg=0.5, flat=4.0, wob=0.0, mk=F.MK['bush'],
                  roll=math.pi / 4)
            blob(acc, nz, (xx + sw * 0.5, yy + 0.06, zb + 0.15), rng.uniform(0.13, 0.17), rng.random(), squash=0.8,
                 res=(6, 4))


def p_curtain7(acc, nz, rng):
    """A root arch over the path (legs planted outside the 7 m path) carrying a curtain of vines."""
    acc.use('bark')
    pts = [(-5.5, -0.45, -0.45), (-4.9, -0.42, 1.7), (-4.1, -0.36, 3.3), (-2.0, -0.3, 3.5), (0.0, -0.35, 3.42),
           (2.0, -0.3, 3.5), (4.1, -0.36, 3.32), (4.9, -0.42, 1.7), (5.5, -0.45, -0.45)]
    sweep(acc, nz, pts, [0.62, 0.48, 0.36, 0.3, 0.3, 0.3, 0.36, 0.48, 0.62], 0.5, nt=9, seg=0.45,
          ridge=(5, 0.06, 0.3))
    sweep(acc, nz, [(-5.0, -0.1, -0.3), (-4.4, -0.05, 2.6), (-2.0, -0.05, 3.95), (1.5, -0.6, 3.9), (4.3, -0.55, 2.8),
                    (5.1, -0.6, -0.3)], [0.24, 0.2, 0.16, 0.16, 0.2, 0.24], 0.7, nt=7, seg=0.45)
    curtain_strands(acc, nz, rng, np.linspace(-3.4, 3.4, 9), -0.3, 3.3)
    for k in range(6):
        blob(acc, nz, (-3.9 + k * 1.56, -0.35, 3.8), rng.uniform(0.5, 0.72), rng.random(), squash=0.75, res=(8, 5))
    for sx in (-1, 1):
        blob(acc, nz, (sx * 5.6, -0.1, 0.2), 0.6, rng.random(), squash=0.75)


def p_curtain3(acc, nz, rng):
    acc.use('bark')
    sweep(acc, nz, [(-2.4, -0.35, 7.4), (-1.9, -0.32, 4.6), (-0.6, -0.35, 3.45), (1.6, -0.3, 3.45), (2.5, -0.35, 3.9)],
          [0.38, 0.32, 0.28, 0.24, 0.12], 0.5, nt=8, seg=0.45, ridge=(5, 0.05, 0.3))
    curtain_strands(acc, nz, rng, np.linspace(-1.1, 1.1, 4), -0.3, 3.35)
    for x in (-1.2, 0.4, 1.7):
        blob(acc, nz, (x, -0.35, 3.75), rng.uniform(0.45, 0.6), rng.random(), squash=0.75, res=(8, 5))


def p_root_wall(acc, nz, rng):
    rock(acc, nz, (0, -0.95, -0.3), 1.85, 0.75, 3.2, 0.2, kind='dark', p=2.6, bt=0.4, dome=0.05)
    acc.use('bark')
    for k, x in enumerate(np.linspace(-1.75, 1.75, 6)):
        r = rng.uniform(0.27, 0.36)
        sweep(acc, nz, [(x * 0.8, -1.3, 3.6), (x * 0.95, -0.6, 2.6), (x, -0.25, 1.2), (x * 1.04, -0.25, -0.35)],
              [r * 0.8, r, r * 1.05, r * 1.15], rng.random(), nt=8, seg=0.35, ridge=(5, 0.06, 0.4))
    for z0 in (0.9, 2.1):                 # weaving roots (over / under the verticals), not a crate-like X
        pts = [(-2.1, -0.15, z0 + 0.3)]
        for k, x in enumerate(np.linspace(-1.4, 1.4, 5)):
            pts.append((x, -0.05 if k % 2 else -0.45, z0 + 0.25 * math.sin(x * 1.7 + z0)))
        pts.append((2.1, -0.2, z0 - 0.2))
        sweep(acc, nz, pts, [0.16, 0.2, 0.2, 0.18, 0.16], rng.random(), nt=7, seg=0.3)
    for k in range(5):
        blob(acc, nz, (-1.7 + k * 0.85, -0.9 + rng.uniform(-0.3, 0.3), 3.2 + rng.uniform(0, 0.3)), rng.uniform(0.55, 0.8),
             rng.random(), squash=0.8)
    blob(acc, nz, (0.2, -0.3, 0.0), 0.5, 0.3, squash=0.45, kind='earth')


def p_divider(acc, nz, rng):
    rock(acc, nz, (0, -5.0, -0.3), 0.5, 5.3, 3.0, 0.2, kind='dark', p=2.6, bt=0.4, dome=0.05)
    acc.use('bark')
    for k in range(9):
        y = -0.4 - k * 1.15 + rng.uniform(-0.2, 0.2)
        r = rng.uniform(0.24, 0.32)
        s = 1 if k % 2 else -1
        sweep(acc, nz, [(-0.75 * s, y, -0.35), (-0.55 * s, y - 0.1, 1.6), (0.0, y, 3.25), (0.55 * s, y + 0.1, 1.6),
                        (0.75 * s, y, -0.35)], [r * 1.15, r, r * 0.9, r, r * 1.15], rng.random(), nt=7, seg=0.4,
              ridge=(5, 0.06, 0.4))
    for s in (-1, 1):
        sweep(acc, nz, [(0.35 * s, 0.4, -0.3), (0.4 * s, -0.5, 2.4), (0.3 * s, -5, 2.9), (0.4 * s, -9.6, 2.3),
                        (0.35 * s, -10.4, -0.3)], [0.28, 0.25, 0.25, 0.25, 0.28], rng.random(), nt=7, seg=0.6)
    for k in range(6):
        blob(acc, nz, (rng.uniform(-0.2, 0.2), -0.8 - k * 1.7, 3.1 + rng.uniform(0, 0.3)), rng.uniform(0.55, 0.8),
             rng.random(), squash=0.8)


def p_vine_anchor(acc, nz, rng):
    acc.use('bark')
    pts = [(-9.5, -0.1, 4.8), (-6.5, 0.0, 1.9), (-2.5, 0.05, 0.78), (2.0, -0.05, 0.68), (6.0, 0.0, 1.1), (7.6, 0.1, 1.6)]
    sweep(acc, nz, pts, [0.85, 0.6, 0.46, 0.4, 0.26, 0.12], 0.4, nt=10, seg=0.5, ridge=(6, 0.05, 0.3))
    acc.use('vine')
    ring = [(0.08 * math.sin(a * 3), 0.55 * math.cos(a), 0.71 + 0.55 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 13)]
    sweep(acc, nz, ring, [0.08], 0.5, nt=5, seg=0.12, caps=(False, False), wob=0.0, mk=F.MK['vine'])
    sweep(acc, nz, [(0.12, 0.0, 0.6), (0.06, 0.0, 0.25), (0.0, 0.0, -0.05)], [0.09, 0.08, 0.07], 0.3, nt=5, seg=0.1,
          wob=0.0, mk=F.MK['vine'])
    for k in range(8):
        x = -5.5 + k * 1.6 + rng.uniform(-0.3, 0.3)
        zt = float(np.interp(x, [p[0] for p in pts], [p[2] for p in pts])) + 0.45
        blob(acc, nz, (x, rng.uniform(-0.3, 0.3), zt + 0.2), rng.uniform(0.55, 0.85), rng.random(), squash=0.8)
    acc.use('vine')
    for x in (-4.0, -2.0, 3.2, 4.6):
        zt = float(np.interp(x, [p[0] for p in pts], [p[2] for p in pts]))
        L = rng.uniform(1.2, 2.4)
        sweep(acc, nz, [(x, 0.1, zt), (x + 0.05, 0.12, zt - L / 2), (x, 0.15, zt - L)], [0.05, 0.04, 0.025],
              rng.random(), nt=4, seg=0.3, mk=F.MK['vine'])
        blob(acc, nz, (x, 0.15, zt - L + 0.05), 0.14, rng.random(), squash=0.8, res=(6, 4))


def p_vine_rope(acc, nz, rng):
    acc.use('vine')
    zs = np.linspace(0.0, -7.0, 40)
    for s in (0, math.pi):
        pts = [(0.045 * math.cos(z * 7.5 + s), 0.045 * math.sin(z * 7.5 + s), z) for z in zs]
        sweep(acc, nz, pts, [0.05, 0.045, 0.04], rng.random(), nt=5, seg=0.18, wob=0.0, smooth=False,
              mk=F.MK['vine'])
    blob(acc, nz, (0, 0, -6.95), 0.11, 0.5, squash=1.1, res=(7, 5), kind='vine')
    for z in (-0.6, -1.9, -3.1, -4.4, -5.5):
        a = rng.uniform(0, 6.3)
        blob(acc, nz, (0.1 * math.cos(a), 0.1 * math.sin(a), z), rng.uniform(0.12, 0.18), rng.random(), squash=0.9,
             res=(6, 4))


def p_float_log(acc, nz, rng):
    acc.use('bark')
    pts = [(-4.9, -0.35, 0.0), (-1.5, -0.45, 0.0), (1.8, -0.3, 0.0), (4.9, -0.4, 0.0)]
    sweep(acc, nz, pts, [0.32, 0.38, 0.37, 0.3], 0.5, nt=10, seg=0.45, ridge=(8, 0.04, 0.2),
          caps=(True, True), cap_mk=F.MK['endgrain'])
    acc.use('bark')
    stub(acc, nz, (-2.6, -0.75, 0.05), (0.2, -1.0, 0.25), 0.7, 0.11, 0.4)
    stub(acc, nz, (2.9, 0.0, 0.05), (0.3, 1.0, 0.15), 0.6, 0.1, 0.6)
    moss_cap(acc, nz, (0.6, -0.33, 0.24), 0.7, 0.24, 0.5, h=0.1)
    moss_cap(acc, nz, (-3.4, -0.37, 0.22), 0.5, 0.22, 0.2, h=0.1)


def p_debris(acc, nz, rng):
    acc.use('bark')
    sweep(acc, nz, [(-2.7, -0.3, 0.0), (0.0, -0.35, 0.02), (2.6, -0.25, -0.02)], [0.24, 0.3, 0.22], 0.3, nt=8,
          seg=0.4, ridge=(7, 0.04, 0.2), cap_mk=F.MK['endgrain'])
    sweep(acc, nz, [(-2.3, -1.0, -0.05), (0.0, -0.45, 0.0), (2.4, 0.1, -0.06)], [0.17, 0.22, 0.15], 0.6, nt=7,
          seg=0.4)
    acc.use('drift')
    for k in range(7):
        c = np.array([rng.uniform(-2.2, 2.2), rng.uniform(-0.9, 0.2), rng.uniform(0.05, 0.12)])
        a = rng.uniform(0, math.pi)
        d = np.array([math.cos(a), math.sin(a), 0.02])
        L = rng.uniform(0.9, 1.8)
        sweep(acc, nz, [c - d * L / 2, c, c + d * L / 2], [0.06, 0.05, 0.03], rng.random(), nt=4, seg=0.3)
    for k in range(6):
        blob(acc, nz, (rng.uniform(-2.2, 2.2), rng.uniform(-0.9, 0.2), 0.12), rng.uniform(0.3, 0.45), rng.random(),
             squash=0.32, res=(8, 4))


def p_snag(acc, nz, rng):
    acc.use('bark')
    sweep(acc, nz, [(-4.7, -0.4, -1.0), (0.0, -0.5, -1.1), (4.7, -0.35, -0.95)], [0.3, 0.35, 0.3], 0.4, nt=7, seg=0.7)
    for k in range(13):
        x = -4.4 + k * 0.73 + rng.uniform(-0.15, 0.15)
        y = rng.uniform(-0.8, 0.0)
        tip = (x + rng.uniform(-0.4, 0.4), y + rng.uniform(-0.3, 0.3), rng.uniform(0.14, 0.29))
        base = (x, y - 0.2, -1.1)
        mid = ((base[0] + tip[0]) / 2 + rng.uniform(-0.15, 0.15), (base[1] + tip[1]) / 2, -0.45)
        sweep(acc, nz, [base, mid, tip], [0.11, 0.08, 0.02], rng.random(), nt=5, seg=0.25, caps=(False, True))
        if rng.random() < 0.5:
            sweep(acc, nz, [mid, (mid[0] + rng.uniform(-0.6, 0.6), mid[1] + 0.2, -0.2),
                            (mid[0] + rng.uniform(-0.8, 0.8), mid[1] + 0.3, rng.uniform(0.05, 0.22))],
                  [0.06, 0.04, 0.015], rng.random(), nt=4, seg=0.25, caps=(False, True))


def p_low_branch_water(acc, nz, rng):
    acc.use('bark')
    pts = [(-5.2, -0.3, 3.2), (-3.7, -0.3, 1.35), (-2.3, -0.35, 0.87), (0.5, -0.3, 0.84), (2.6, -0.35, 0.88),
           (3.3, -0.3, 1.0)]
    sweep(acc, nz, pts, [0.45, 0.36, 0.3, 0.29, 0.22, 0.1], 0.5, nt=8, seg=0.4, ridge=(6, 0.05, 0.3))
    sweep(acc, nz, [(-1.0, -0.35, 1.0), (0.0, -0.9, 1.25), (1.2, -1.2, 1.45)], [0.12, 0.09, 0.04], 0.3, nt=5, seg=0.3)
    for k in range(6):
        x = -2.4 + k * 1.05 + rng.uniform(-0.2, 0.2)
        blob(acc, nz, (x, -0.3 + rng.uniform(-0.3, 0.3), 1.35 + rng.uniform(0, 0.2)), rng.uniform(0.4, 0.6),
             rng.random(), squash=0.75)
    blob(acc, nz, (1.2, -1.2, 1.55), 0.35, 0.4, squash=0.8)


def p_knot(acc, nz, rng):
    acc.use('bark')
    sweep(acc, nz, [(-1.45, -0.5, -0.25), (-0.6, -0.48, -0.02), (0.6, -0.52, -0.02), (1.45, -0.5, -0.25)],
          [0.35, 0.5, 0.5, 0.35], 0.4, nt=10, seg=0.3, flat=1.1, p=2.3, wob=0.08, ridge=(6, 0.07, 0.8))
    blob(acc, nz, (-0.5, -0.85, 0.05), 0.32, 0.6, squash=0.9, kind='bark')
    blob(acc, nz, (0.55, -0.2, 0.0), 0.3, 0.2, squash=0.9, kind='bark')
    moss_cap(acc, nz, (0.1, -0.5, 0.33), 0.5, 0.3, 0.6, h=0.1)
    blob(acc, nz, (1.55, -0.6, -0.05), 0.3, 0.3, squash=0.75)


WOOD_ALL = [
    dict(name='OB_RootLow_05', fn=p_root_low(0.5, [(-4.3, 4.3, 0.25, 0.5, -0.22), (-3.6, 4.4, 0.18, 0.4, -0.45),
                                                   (-4.4, -0.8, 0.12, 0.3, -0.05)]),
         lod=(1300, 600, 220), drop=SMALL, align_front=True, hit=dict(kind='low', x=(-3.5, 3.5), y=(0, 0.5))),
    dict(name='OB_RootLow_06', fn=p_root_low(0.6, [(-4.4, 4.3, 0.3, 0.6, -0.3), (-4.2, 2.0, 0.2, 0.45, -0.05),
                                                   (-1.5, 4.4, 0.16, 0.38, -0.52)]),
         lod=(1400, 650, 240), drop=SMALL, align_front=True, hit=dict(kind='low', x=(-3.5, 3.5), y=(0, 0.6))),
    dict(name='OB_RootLow_Half_05', fn=p_root_half, lod=(900, 400, 160), drop=SMALL, align_front=True,
         front_x=2.0, hit=dict(kind='low', x=(-2.0, 2.0), y=(0, 0.5))),
    dict(name='OB_LogWalk_09', fn=p_log_walk, lod=(1500, 700, 260), drop=SMALL, align_front=True,
         hit=dict(kind='walk', x=(-3.5, 3.5), y=(0, 0.9))),
    dict(name='OB_LogWalk_Giant_09', fn=p_log_giant, lod=(1500, 700, 280), drop=SMALL, align_front=True,
         hit=dict(kind='walk', x=(-3.5, 3.5), y=(0, 0.9))),
    dict(name='OB_Driftwood_05', fn=p_driftwood, lod=(800, 380, 140), drop=SMALL, align_front=True, water=True,
         hit=dict(kind='walk', x=(-3.5, 3.5), y=(0, 0.5))),
    dict(name='OB_BranchHigh_10', fn=p_branch_high, lod=(1500, 700, 260), drop=SMALL, align_front=True,
         hit=dict(kind='high', x=(-3.5, 3.5), y=(1.0, 3.0))),
    dict(name='OB_VineCurtain_7m', fn=p_curtain7, lod=(1500, 700, 260), drop=(0.25, 1.2), align_front=True,
         hit=dict(kind='high', x=(-3.5, 3.5), y=(1.0, 3.5))),
    dict(name='OB_VineCurtain_3m', fn=p_curtain3, lod=(900, 420, 160), drop=(0.25, 1.2), align_front=True,
         front_x=1.2, hit=dict(kind='high', x=(-1.2, 1.2), y=(1.0, 3.5))),
    dict(name='OB_RootWall_4m', fn=p_root_wall, lod=(1500, 700, 260), drop=SMALL, align_front=True, fit_w=4.0,
         front_x=2.0, hit=dict(kind='block')),
    dict(name='OB_RootWall_Divider_10m', fn=p_divider, lod=(1500, 700, 280), drop=SMALL, fit_w=1.2,
         hit=dict(kind='block')),
    dict(name='OB_VineAnchor', fn=p_vine_anchor, lod=(1500, 700, 260), drop=(0.3, 1.0), ground=None),
    dict(name='OB_VineRope_7m', fn=p_vine_rope, lod=(700, 300, 120), drop=(0.0, 0.5), ground=None, directional=False,
         cull=False, empties=dict(Grip=(0, 0, -6.7))),
    dict(name='OB_FloatLog_9m', fn=p_float_log, lod=(1100, 500, 200), drop=SMALL, water=True, ground=None,
         align_front=True, front_x=4.5, hit=dict(kind='float', x=(-4.5, 4.5), y=(-0.4, 0.4))),
    dict(name='OB_DebrisMat_5m', fn=p_debris, lod=(1300, 600, 220), drop=(0.0, 0.8), water=True, ground=None,
         align_front=True, front_x=2.5, hit=dict(kind='float', x=(-2.5, 2.5), y=(-0.4, 0.4))),
    dict(name='OB_Snag_9m', fn=p_snag, lod=(1500, 700, 260), drop=(0.0, 0.4), water=True, ground=None,
         align_front=True, front_x=4.5, hit=dict(kind='snag', x=(-4.5, 4.5), y=(-1.5, 0.3))),
    dict(name='OB_LowBranch_Water_55', fn=p_low_branch_water, lod=(1300, 600, 220), drop=SMALL, ground=None,
         align_front=True, front_x=2.75, hit=dict(kind='lowbranch', x=(-2.75, 2.75), y=(0.5, 2.0))),
]



_W = {p['name']: p for p in WOOD_ALL}
CUT = -0.25          # buried root/log ends: no texels below the floor
for _n in ('OB_RootLow_05', 'OB_RootLow_06', 'OB_RootLow_Half_05', 'OB_LogWalk_09', 'OB_LogWalk_Giant_09',
           'OB_BranchHigh_10', 'OB_VineCurtain_7m', 'OB_RootWall_4m', 'OB_RootWall_Divider_10m'):
    _W[_n]['cut'] = CUT
ROOTS = [_W[n] for n in ('OB_RootLow_05', 'OB_RootLow_06', 'OB_RootLow_Half_05', 'OB_BranchHigh_10')]
LOGS = [_W[n] for n in ('OB_LogWalk_09', 'OB_LogWalk_Giant_09', 'OB_Driftwood_05', 'OB_RootWall_4m',
                        'OB_RootWall_Divider_10m')]
VINES = [_W[n] for n in ('OB_VineCurtain_7m', 'OB_VineCurtain_3m', 'OB_VineAnchor', 'OB_VineRope_7m')]
WATER = [_W[n] for n in ('OB_FloatLog_9m', 'OB_DebrisMat_5m', 'OB_Snag_9m', 'OB_LowBranch_Water_55')]


# ================================================================== stone
def p_boulder(w, h):
    def fn(acc, nz, rng):
        lean = 0.07
        rock(acc, nz, (0, -w * 0.5, -0.3), w * 0.5, w * 0.48, h * 0.55, rng.random(), rot=rng.uniform(-0.3, 0.3),
             p=2.6, bt=0.32, bb=0.15, dome=0.08, tilt=(lean, rng.uniform(-0.05, 0.05)))
        rock(acc, nz, (rng.uniform(-0.08, 0.08) * w, -w * 0.45, h * 0.5 - 0.35), w * 0.43, w * 0.4, h * 0.42,
             rng.random(), rot=rng.uniform(-0.4, 0.4), p=2.5, bt=0.35, dome=0.08, tilt=(lean * 1.4, rng.uniform(-0.08, 0.08)))
        rock(acc, nz, (rng.uniform(-0.1, 0.1) * w, -w * 0.5, h * 0.82 - 0.3), w * 0.3, w * 0.28, h * 0.2,
             rng.random(), rot=rng.uniform(-0.5, 0.5), p=2.4, bt=0.45, dome=0.12, tilt=(lean, 0.05))
        moss_cap(acc, nz, (0, -w * 0.5, h - 0.12), w * 0.29, w * 0.27, 0.5, h=0.16)
        blob(acc, nz, (w * 0.12, -w * 0.62, h + 0.05), w * 0.22, rng.random(), squash=0.75)
        blob(acc, nz, (-w * 0.45, -w * 0.15, 0.0), 0.28, rng.random(), squash=0.7)
    return fn


def p_river_rock(acc, nz, rng):
    rock(acc, nz, (0, -0.7, -1.7), 0.72, 0.7, 2.6, 0.3, kind='wet', p=2.6, bt=0.32, dome=0.1, tilt=(0.05, 0.03))
    rock(acc, nz, (0.08, -0.72, 0.55), 0.55, 0.52, 0.8, 0.7, kind='wet', p=2.5, bt=0.45, dome=0.12, tilt=(0.06, -0.05))
    moss_cap(acc, nz, (0.08, -0.72, 1.28), 0.42, 0.4, 0.4, h=0.18)
    blob(acc, nz, (-0.1, -0.85, 1.48), 0.22, 0.5, squash=0.7)


def p_wet_low(acc, nz, rng):
    xs = [-3.55, -2.25, -0.85, 0.55, 1.95, 3.45]
    for k, x in enumerate(xs):
        a, b = rng.uniform(0.62, 0.8), rng.uniform(0.38, 0.5)
        h = rng.uniform(0.5, 0.57)
        rock(acc, nz, (x, -0.45 + rng.uniform(-0.08, 0.08), -0.12), a, b, h, rng.random(), rot=rng.uniform(-0.4, 0.4),
             kind='wet', p=2.6, bt=0.48, bb=0.2, dome=0.1)
    for x in (-2.9, 1.25):
        rock(acc, nz, (x, -0.2, -0.1), 0.35, 0.3, 0.3, rng.random(), kind='wet', p=2.4, bt=0.5)


def p_gap_lip(w):
    def fn(acc, nz, rng):
        acc.use('earth')
        from painterly_slabs import slab
        slab(acc, nz, (0, 1.55, -6.3), w / 2 + 0.25, 1.65, 6.28, 0.4, False, 0.0, [(3, 0.02, 0.4), (5, 0.015, 1.1)],
             bt=0.025, bb=0.05, bulge=-0.03, dome=0.0, p=3.6, wob=0.04, lo_nt=24)
        for k in range(int(w * 0.8)):
            x = -w / 2 + 0.5 + (w - 1.0) * (k + rng.uniform(0.2, 0.8)) / int(w * 0.8)
            z = rng.uniform(-4.5, -1.5)                 # keep the rock tops below the floor (lip top -0.02)
            s_ = rng.uniform(0.35, 0.7)
            rock(acc, nz, (x, 0.15, z - s_ * 0.5), s_, s_ * 0.7, s_ * 1.1, rng.random(), rot=rng.uniform(-0.3, 0.3),
                 p=2.6, bt=0.4, dome=0.08)
        acc.use('bark')
        for k in range(int(w * 0.9)):
            x = -w / 2 + 0.4 + (w - 0.8) * (k + rng.uniform(0.2, 0.8)) / int(w * 0.9)
            L = rng.uniform(1.8, 4.2)
            sweep(acc, nz, [(x, 0.5, -0.24), (x + 0.05, -0.02, -0.26), (x + rng.uniform(-0.2, 0.2), -0.24, -0.7),
                            (x + rng.uniform(-0.4, 0.4), -0.2, -0.6 - L)], [0.13, 0.12, 0.09, 0.03], rng.random(),
                  nt=6, seg=0.3, caps=(False, True))
        for k in range(int(w / 1.6)):
            x = -w / 2 + 0.8 + (w - 1.6) * (k + rng.uniform(0.2, 0.8)) / int(w / 1.6)
            blob(acc, nz, (x, -0.22, -0.62), rng.uniform(0.3, 0.42), rng.random(), squash=0.6)
    return fn


def p_step(rxx, ryy):
    def fn(acc, nz, rng):
        rock(acc, nz, (0, -ryy, -1.6), rxx, ryy, 1.6, rng.random(), kind='wet', p=3.3, bt=0.1, bb=0.2, dome=0.0,
             lobes=[(3, 0.04, rng.uniform(0, 6)), (2, 0.04, rng.uniform(0, 6))])
        for k in range(3):
            a = rng.uniform(0, 2 * math.pi)
            moss_cap(acc, nz, (math.cos(a) * rxx * 0.85, -ryy + math.sin(a) * ryy * 0.85, -0.2), 0.45, 0.3,
                     rng.random(), rot=a, h=0.14)
    return fn


STONE = [
    dict(name=f'OB_Boulder_{int(w * 10):02d}', fn=p_boulder(w, h), lod=(1100, 500, 180), drop=SMALL, fit_w=w,
         align_front=True, front_x=w, seed=300 + k, hit=dict(kind='block'))
    for k, (w, h) in enumerate(((1.2, 2.3), (1.4, 2.5), (1.6, 2.7), (2.0, 2.9)))
] + [
    dict(name='OB_RiverRock_14', fn=p_river_rock, lod=(1000, 450, 160), drop=SMALL, fit_w=1.4, align_front=True,
         front_x=1.4, water=True, ground=None, cut=-1.8, hit=dict(kind='rock')),
    dict(name='OB_WetRockLow_05', fn=p_wet_low, lod=(1300, 600, 220), drop=SMALL, align_front=True, water=False,
         hit=dict(kind='low', x=(-3.5, 3.5), y=(0, 0.5))),
    dict(name='OB_GapLip_8m', fn=p_gap_lip(8.0), lod=(1500, 700, 260), drop=(0.0, 0.8), ground=None, cull=True, clamp_z=-0.02,
         hit=dict(kind='walk', x=(-3.5, 3.5), z=(-3.0, 0.0), y=(0, -0.02))),
    dict(name='OB_GapLip_4m', fn=p_gap_lip(4.5), lod=(1000, 450, 180), drop=(0.0, 0.8), ground=None, clamp_z=-0.02,
         hit=dict(kind='walk', x=(-2.25, 2.25), z=(-3.0, 0.0), y=(0, -0.02))),
    dict(name='OB_SteppingRock_A', fn=p_step(1.3, 1.35), lod=(700, 320, 120), drop=SMALL, ground=None, water=True,
         hit=dict(kind='walk', x=(-1.2, 1.2), y=(0, 0.0))),
    dict(name='OB_SteppingRock_B', fn=p_step(1.15, 1.8), lod=(700, 320, 120), drop=SMALL, ground=None, water=True,
         hit=dict(kind='walk', x=(-1.0, 1.0), y=(0, 0.0))),
]


# ================================================================== hazard
def p_thorns(w, d=1.2, top=0.62):
    def fn(acc, nz, rng):
        for k in range(max(2, int(w / 0.9))):
            x = -w / 2 + 0.35 + (w - 0.7) * (k + 0.5) / max(2, int(w / 0.9))
            blob(acc, nz, (x, -d / 2 + rng.uniform(-0.15, 0.15), -0.08), rng.uniform(0.38, 0.5), rng.random(),
                 squash=0.5, res=(8, 4), kind='thorn')
        n = int(w * 4.2)
        for k in range(n):
            xa = -w / 2 + 0.1 + (w - 0.2) * (k + rng.random()) / n
            span = rng.uniform(0.5, 1.0)
            ang = rng.uniform(-0.6, 0.6) + (0 if k % 2 else math.pi / 2)
            xb = float(np.clip(xa + math.cos(ang) * span, -w / 2 + 0.05, w / 2 - 0.05))
            ya = rng.uniform(-d + 0.1, -0.1)
            yb = float(np.clip(ya + math.sin(ang) * span, -d + 0.05, -0.05))
            hz = rng.uniform(0.3, top - 0.12)
            pts = [(xa, ya, -0.1), (xa * 0.7 + xb * 0.3, ya * 0.7 + yb * 0.3, hz * 0.8), ((xa + xb) / 2, (ya + yb) / 2, hz),
                   (xa * 0.3 + xb * 0.7, ya * 0.3 + yb * 0.7, hz * 0.75), (xb, yb, -0.1)]
            C = sweep(acc, nz, pts, [0.045, 0.038, 0.032, 0.038, 0.045], rng.random(), nt=3, seg=0.11, wob=0.0,
                      mk=F.MK['thorn'], caps=(False, False))
            for i in range(1, len(C) - 1, 2):
                if C[i][2] < 0.06:
                    continue
                T_ = C[i + 1] - C[i - 1]
                T_ /= np.linalg.norm(T_)
                dvec = rng.normal(size=3) + np.array([0, 0.9, 0.5])
                dvec -= T_ * dvec.dot(T_)
                dvec /= np.linalg.norm(dvec)
                L = rng.uniform(0.07, 0.11)
                tipz = C[i][2] + dvec[2] * (L + 0.03)
                if tipz > top:
                    dvec[2] -= (tipz - top) / L
                    dvec /= np.linalg.norm(dvec)
                cone(acc, C[i] + dvec * 0.025, dvec, L, 0.022, rng.random(), sides=3, mk=F.MK['thorntip'])
            if k % 3 == 0:
                blob(acc, nz, (pts[2][0], pts[2][1], min(hz + 0.05, top - 0.07)), 0.075, 0.5, squash=0.6, res=(6, 3),
                     kind='bloom')
    return fn


HAZARD = [
    dict(name=f'OB_Thorns_{int(w * 10):02d}', fn=p_thorns(w), lod=(int(w * 470), int(w * 230), int(w * 90)),
         drop=(0.0, 0.1), align_front=True, front_x=w, seed=500 + k, cull=False, w=1.0, fit_w=w,
         hit=dict(kind='thorn', x=(-w / 2, w / 2), y=(0, 0.62)))
    for k, w in enumerate((1.5, 2.5, 3.5))
]


# ================================================================== pickups + discovery markers
def p_coin(acc, nz, rng):
    R = rx(math.pi / 2)            # coin faces +-Y (the runner)
    body = [(0.0, 0.025), (0.165, 0.025), (0.185, 0.034), (0.212, 0.034), (0.23, 0.018), (0.23, -0.018),
            (0.212, -0.034), (0.185, -0.034), (0.165, -0.025), (0.0, -0.025)]
    lathe(acc, body, nt=20, rot=R, mk=F.MK['gold'], hl=[0, 0, 1, 1, 0.5, 0.5, 1, 1, 0, 0])
    for s in (1, -1):
        lathe(acc, [(0.0, s * 0.031), (0.155, s * 0.031), (0.155, s * 0.024)], nt=24, rot=R, mk=F.MK['gold'],
              hl=[1, 1, 0], star=(4, 0.72))
        lathe(acc, [(0.0, s * 0.052), (0.034, s * 0.047), (0.05, s * 0.034), (0.05, s * 0.025)], nt=12, rot=R,
              mk=F.MK['gem'])


def p_crystal(acc, nz, rng):
    facet_prism(acc, (0, 0, -0.26), (0.02, -0.03, 1), 0.55, 0.1, rnd=0.3, mk=F.MK['crystal'])
    facet_prism(acc, (0.03, 0.0, -0.24), (0.55, 0.05, 1), 0.36, 0.07, rnd=1.2, mk=F.MK['crystal'])
    facet_prism(acc, (-0.03, 0.01, -0.24), (-0.5, 0.15, 1), 0.3, 0.06, rnd=2.1, mk=F.MK['crystal'])
    facet_prism(acc, (0.0, 0.03, -0.25), (0.05, 0.6, 1), 0.26, 0.055, rnd=0.7, mk=F.MK['crystal'])


def p_shield(acc, nz, rng):
    R = rx(-math.pi / 2)
    lathe(acc, [(0.0, 0.072), (0.11, 0.066), (0.21, 0.048), (0.262, 0.03)], nt=24, rot=R, mk=F.MK['teal'],
          hl=[1, 0.6, 0.2, 0])
    lathe(acc, [(0.255, 0.034), (0.29, 0.042), (0.315, 0.026), (0.315, -0.02), (0.29, -0.03), (0.0, -0.03)], nt=24,
          rot=R, mk=F.MK['brass'], hl=[0, 1, 0.6, 0, 0, 0])
    lathe(acc, [(0.0, 0.11), (0.045, 0.1), (0.07, 0.075), (0.075, 0.06)], nt=16, rot=R, mk=F.MK['cream'],
          hl=[1, 0.8, 0.2, 0])
    lathe(acc, [(0.0, 0.08), (0.2, 0.064), (0.2, 0.05)], nt=16, rot=R, mk=F.MK['cream'], star=(4, 0.85), hl=[1, 1, 0])
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        blob(acc, nz, (0.29 * math.cos(a), 0.045, 0.29 * math.sin(a)), 0.018, 0.5, squash=1.0, res=(5, 3),
             kind='cream')


def flag(acc, root, L=0.42, H=0.2, droop=0.08):
    """Cream cloth pennant: a flat two-sided triangle with a soft wave, pointing to -X (Unity +X)."""
    r = np.asarray(root, float)
    V = [r, r + (0, 0, -H), r + (-L * 0.5, -0.04, -H * 0.55 - droop * 0.4), r + (-L, 0.02, -H * 0.4 - droop)]
    F = [(0, 2, 1), (0, 3, 2), (1, 2, 0), (2, 3, 0)]
    A = np.array([(0, 0, 0.5, 0), (0, 0, 0.5, 0), (0.5, 0, 0.5, 0), (1, 0, 0.5, 0)], float)
    acc.add(np.array(V[:2] + V[2:]), F[:2], A, mk=F_MK_CLOTH)
    acc.add(np.array(V) + np.array([0, 0.004, 0]), [(0, 1, 2), (0, 2, 3)], A, mk=F_MK_CLOTH)


def p_cairn(acc, nz, rng):
    zs = 0.0
    for k, (r_, h) in enumerate(((0.42, 0.32), (0.36, 0.28), (0.29, 0.25), (0.21, 0.2))):
        rock(acc, nz, (rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), zs - 0.02), r_, r_ * 0.85, h, rng.random(),
             rot=rng.uniform(0, 3), p=2.4, bt=0.45, bb=0.3, dome=0.15, tilt=(rng.uniform(-0.06, 0.06), 0))
        zs += h * 0.95
    moss_cap(acc, nz, (0.0, 0.0, 0.28), 0.4, 0.34, 0.3, h=0.08)
    acc.use('bark')
    sweep(acc, nz, [(0, 0, zs - 0.2), (0, 0, zs + 0.55)], [0.045, 0.04], 0.5, nt=6, seg=0.2,
          cap_mk=F.MK['endgrain'])
    lathe(acc, [(0.0, 0.025), (0.16, 0.025), (0.16, -0.01), (0.0, -0.01)], nt=20, rot=rx(-math.pi / 2),
          c=(0, 0.06, zs + 0.45), mk=F.MK['brass'], hl=[1, 1, 0, 0])
    lathe(acc, [(0.0, 0.034), (0.13, 0.03), (0.13, 0.022)], nt=24, rot=rx(-math.pi / 2), c=(0, 0.06, zs + 0.45),
          mk=F.MK['cream'], star=(4, 0.75), hl=[1, 1, 0])
    flag(acc, (-0.04, 0.0, zs + 0.4))


def p_post(acc, nz, rng):
    acc.use('bark')
    sweep(acc, nz, [(0, 0, -0.3), (0.02, 0, 0.8), (0, 0.01, 1.45)], [0.075, 0.07, 0.065], 0.5, nt=7, seg=0.25,
          cap_mk=F.MK['endgrain'], ridge=(4, 0.08, 0.0))
    lathe(acc, [(0.0, 0.03), (0.11, 0.03), (0.12, 0.0), (0.0, 0.0)], nt=16, c=(0.0, 0.01, 1.45),
          mk=F.MK['brass'], hl=[1, 1, 0, 0])
    lathe(acc, [(0.0, 0.036), (0.1, 0.034), (0.1, 0.028)], nt=24, c=(0.0, 0.01, 1.45), mk=F.MK['cream'], star=(4, 0.75),
          hl=[1, 1, 0])
    for z in (0.55, 0.75, 0.95):
        blob(acc, nz, (0.0, 0.07, z), 0.025, 0.5, squash=1.0, res=(5, 3), kind='brass')
    flag(acc, (-0.07, 0.0, 1.32))
    rock(acc, nz, (0.12, 0.05, -0.1), 0.18, 0.15, 0.18, 0.3, p=2.4, bt=0.5)
    rock(acc, nz, (-0.13, -0.05, -0.1), 0.15, 0.13, 0.15, 0.7, p=2.4, bt=0.5)


PICKUPS = [
    dict(name='PK_Coin', fn=p_coin, lod=(420, 160, 64), ground=None, directional=False, cull=False, w=1.6),
    dict(name='PK_Crystal', fn=p_crystal, lod=(200, 120, 60), ground=None, directional=False, cull=False, w=1.6),
    dict(name='PK_Shield', fn=p_shield, lod=(700, 300, 110), ground=None, directional=False, cull=False, w=1.6),
    dict(name='DM_Cairn', fn=p_cairn, lod=(1000, 450, 160), drop=(0.0, 0.08), w=1.0),
    dict(name='DM_SurveyPost', fn=p_post, lod=(600, 280, 100), drop=(0.0, 0.08), w=1.0),
]


# ================================================================== canopy (Forest/)
def beam(acc, nz, rng, L, half_w, R=1.0, ends=True, extra=True):
    """Walk top at z = 0 from y = 0 to y = -L; the branch continues ~0.9 m past both ends (tapering down)."""
    ys = np.linspace(0.9, -(L + 0.9), 30)
    def taper(y):
        d = max(0.0, y, -(L + y))
        return max(0.45, 1 - (d / 0.9) ** 1.5 * 0.55) if ends else 1.0
    rad = [R * taper(y) for y in ys]
    pts = [(0.12 * math.sin(y * 0.35 + 0.7), y, -R) for y in ys]
    acc.use('sbark')
    sweep(acc, nz, pts, rad, 0.5, nt=14, seg=0.6, flat=half_w / R, p=4.0, wob=0.015, ridge=(9, 0.025, 0.04),
          smooth=False)
    if not extra:
        return
    for k in range(int(L / 2.5)):
        y = -1.0 - k * 2.5 + rng.uniform(-0.6, 0.6)
        s = 1 if k % 2 else -1
        moss_cap(acc, nz, (s * (half_w - 0.05), y, -0.33), 0.55, 0.32, rng.random(), rot=math.pi / 2, h=0.24)
        if rng.random() < 0.6:
            blob(acc, nz, (s * (half_w + 0.25), y + rng.uniform(-0.5, 0.5), -0.55), rng.uniform(0.4, 0.6), rng.random(),
                 squash=0.8)
    acc.use('vine')
    for k in range(int(L / 3)):
        y = -1.5 - k * 3.0 + rng.uniform(-0.8, 0.8)
        x = rng.uniform(-half_w * 0.6, half_w * 0.6)
        Lh = rng.uniform(1.5, 3.5)
        sweep(acc, nz, [(x, y, -1.7 * R), (x + 0.05, y, -1.7 * R - Lh / 2), (x, y + 0.05, -1.7 * R - Lh)],
              [0.05, 0.04, 0.025], rng.random(), nt=4, seg=0.4, mk=F.MK['vine'])
        blob(acc, nz, (x, y + 0.05, -1.7 * R - Lh + 0.05), 0.14, rng.random(), squash=0.8, res=(6, 4))
    acc.use('sbark')
    for k in range(2):
        y = -L * (0.3 + 0.4 * k)
        s = -1 if k else 1
        b0 = np.array([s * half_w * 0.8, y, -0.9 * R])
        sweep(acc, nz, [b0, b0 + np.array([s * 1.4, -0.3, -0.5]), b0 + np.array([s * 2.6, -0.8, -0.4])],
              [0.32, 0.2, 0.08], rng.random(), nt=6, seg=0.4)
        blob(acc, nz, tuple(b0 + np.array([s * 2.7, -0.8, -0.2])), 0.7, rng.random(), squash=0.8)


def p_beam(L, hw, ends=True):
    def fn(acc, nz, rng):
        beam(acc, nz, rng, L, hw, ends=ends)
    return fn


def p_platform(acc, nz, rng):
    L = 20.0
    ys = np.linspace(2.0, -(L + 0.9), 34)
    hw = [3.7 + (1.45 - 3.7) * min(1, max(0, -y) / L) ** 0.8 for y in ys]
    R = 1.2
    rad = [R * (max(0.5, 1 - (max(0, -(L + y)) / 0.9) ** 1.5 * 0.5)) for y in ys]
    acc.use('sbark')
    sweep(acc, nz, [(0.15 * math.sin(y * 0.3), y, -R) for y in ys], rad, 0.5, nt=16, seg=0.7,
          flat=[h / R for h in hw], p=4.0, wob=0.015, ridge=(11, 0.025, 0.04), smooth=False)
    sweep(acc, nz, [(0.0, 4.0, -14.0), (0.3, 3.8, -2.0), (0.0, 4.2, 12.0)], [2.6, 2.4, 2.1], 0.3, nt=16, seg=1.2,
          ridge=(12, 0.04, 0.05))
    sweep(acc, nz, [(-1.5, 3.0, 2.0), (-5.0, 2.0, 5.0), (-8.0, 0.5, 8.5)], [1.0, 0.7, 0.35], 0.6, nt=10, seg=0.8)
    for k in range(9):
        y = -1.5 - k * 2.1
        hw_ = 3.7 + (1.45 - 3.7) * min(1, -y / L) ** 0.8
        s = 1 if k % 2 else -1
        moss_cap(acc, nz, (s * (hw_ - 0.1), y, -0.36), 0.7, 0.4, rng.random(), rot=math.pi / 2, h=0.26)
        blob(acc, nz, (s * (hw_ + 0.3), y + rng.uniform(-0.6, 0.6), -0.7), rng.uniform(0.5, 0.75), rng.random(),
             squash=0.8)
    for k in range(5):
        blob(acc, nz, (rng.uniform(-1.8, 1.8), 4.0 + rng.uniform(-1.8, -1.2), rng.uniform(0.4, 6.0)),
             rng.uniform(0.5, 0.9), rng.random(), squash=0.8)


CANOPY = [
    dict(name='TR_CanopyBeam_A_12m', fn=p_beam(12.0, 1.45), lod=(1500, 700, 260), drop=(0.3, 1.0), ground=None,
         hit=dict(kind='walk', x=(-1.2, 1.2), z=(0.0, 12.0), y=(0, 0.0))),
    dict(name='TR_CanopyBeam_B_10m', fn=p_beam(10.0, 1.25), lod=(1400, 650, 240), drop=(0.3, 1.0), ground=None,
         hit=dict(kind='walk', x=(-1.0, 1.0), z=(0.0, 10.0), y=(0, 0.0))),
    dict(name='TR_CanopyBeam_Mid_8m', fn=p_beam(8.0, 1.45, ends=False), lod=(1100, 500, 200), drop=(0.3, 1.0),
         ground=None, hit=dict(kind='walk', x=(-1.2, 1.2), z=(0.0, 8.0), y=(0, 0.0))),
    dict(name='TR_CanopyPlatform_20m', fn=p_platform, lod=(2500, 1100, 400), drop=(0.3, 1.2), ground=None,
         hit=dict(kind='walk', x=(-1.2, 1.2), z=(0.0, 20.0), y=(0, 0.0))),
    dict(name='OB_BranchKnot_05', fn=p_knot, lod=(800, 380, 140), drop=SMALL, ground=None, align_front=True,
         front_x=1.2, cut=-0.3, hit=dict(kind='walk', x=(-1.2, 1.2), y=(0, 0.5))),
]


# ================================================================== path edges, banks (Forest/)
def p_edge_roots(acc, nz, rng):
    rock(acc, nz, (-0.95, -4.0, -0.35), 0.75, 4.4, 0.62, 0.4, kind='moss', p=2.4, bt=0.6, dome=0.1,
         lobes=[(7, 0.05, 1.0), (11, 0.04, 2.0)])
    acc.use('bark')
    for k in range(4):
        y0 = -0.6 - k * 2.0 + rng.uniform(-0.3, 0.3)
        sweep(acc, nz, [(-2.1, y0 + 0.4, 0.35), (-1.0, y0, 0.24), (-0.1, y0 - 0.5, 0.06), (0.35, y0 - 0.9, -0.2)],
              [0.2, 0.17, 0.12, 0.07], rng.random(), nt=6, seg=0.3, ridge=(5, 0.06, 0.4))
    sweep(acc, nz, [(-0.55, 0.3, -0.05), (-0.45, -2.5, 0.17), (-0.6, -5.5, 0.15), (-0.5, -8.3, -0.05)],
          [0.12, 0.17, 0.16, 0.12], 0.5, nt=6, seg=0.4)
    for k in range(5):
        rock(acc, nz, (rng.uniform(-1.6, -0.4), -0.6 - k * 1.7, -0.08), rng.uniform(0.18, 0.32), rng.uniform(0.15, 0.25),
             rng.uniform(0.18, 0.3), rng.random(), rot=rng.uniform(0, 3), p=2.4, bt=0.5)
    for k in range(4):
        blob(acc, nz, (rng.uniform(-1.8, -1.3), -1.0 - k * 2.1, 0.25), rng.uniform(0.35, 0.5), rng.random(), squash=0.75)


def p_edge_moss(acc, nz, rng):
    for k in range(6):
        y = -0.6 - k * 1.4 + rng.uniform(-0.2, 0.2)
        moss_cap(acc, nz, (rng.uniform(-1.3, -0.6), y, -0.15), rng.uniform(0.6, 0.9), rng.uniform(0.5, 0.7),
                 rng.random(), rot=rng.uniform(0, 3), h=rng.uniform(0.28, 0.4))
    for k in range(6):
        rock(acc, nz, (rng.uniform(-1.4, -0.3), -0.4 - k * 1.45, -0.1), rng.uniform(0.2, 0.42), rng.uniform(0.18, 0.3),
             rng.uniform(0.2, 0.38), rng.random(), rot=rng.uniform(0, 3), p=2.4, bt=0.5)
    for k in range(3):
        blob(acc, nz, (rng.uniform(-1.8, -1.4), -1.4 - k * 2.6, 0.2), rng.uniform(0.4, 0.55), rng.random(), squash=0.75)


def p_riverbank(acc, nz, rng):
    from painterly_slabs import slab
    acc.use('earth')
    slab(acc, nz, (0.4, -5.0, -0.5), 2.2, 5.6, 1.5, 0.4, False, 0.0, [(6, 0.04, 0.3), (9, 0.03, 1.0)], bt=0.55,
         bb=0.1, bulge=0.0, dome=0.0, p=3.0, wob=0.08, lo_nt=22)
    acc.use('bark')
    for k in range(5):
        y0 = -0.8 - k * 2.0 + rng.uniform(-0.4, 0.4)
        sweep(acc, nz, [(0.6, y0, 1.02), (-0.9, y0 - 0.2, 0.85), (-1.7, y0 - 0.1, 0.25), (-2.1, y0 - 0.3, -0.35)],
              [0.18, 0.15, 0.11, 0.05], rng.random(), nt=6, seg=0.3)
    for k in range(7):
        rock(acc, nz, (rng.uniform(-2.4, -1.6), -0.5 - k * 1.4, -0.45), rng.uniform(0.3, 0.55), rng.uniform(0.25, 0.4),
             rng.uniform(0.45, 0.7), rng.random(), rot=rng.uniform(0, 3), kind='wet', p=2.5, bt=0.5)
    for k in range(5):
        moss_cap(acc, nz, (rng.uniform(-0.2, 1.4), -0.6 - k * 2.0, 0.85), rng.uniform(0.5, 0.8), rng.uniform(0.4, 0.6),
                 rng.random(), rot=rng.uniform(0, 3), h=0.22)
    for k in range(3):
        blob(acc, nz, (rng.uniform(-0.8, -0.4), -1.6 - k * 3.0, 0.75), rng.uniform(0.4, 0.55), rng.random(), squash=0.75)


def p_water_edge(acc, nz, rng):
    for k in range(14):
        y = -0.3 - k * 0.72 + rng.uniform(-0.15, 0.15)
        s_ = rng.uniform(0.22, 0.5)
        rock(acc, nz, (rng.uniform(-0.6, 0.6), y, -0.3), s_, s_ * rng.uniform(0.7, 0.9), s_ * rng.uniform(0.75, 1.0),
             rng.random(), rot=rng.uniform(0, 3), kind='wet', p=2.4, bt=0.5, dome=0.15)
    for k in range(4):
        tufty = (rng.uniform(0.4, 1.0), -1.0 - k * 2.4, -0.05)
        acc.use('blade')
        from painterly_slabs import tuft
        tuft(acc, rng, tufty, 0.9, rng.random(), False, blades=6)
    for k in range(3):
        moss_cap(acc, nz, (rng.uniform(0.3, 0.9), -1.8 - k * 3.0, -0.05), 0.4, 0.3, rng.random(), h=0.2)


EDGES = [
    dict(name='TR_PathEdge_Roots_8m', fn=p_edge_roots, lod=(1500, 700, 260), drop=(0.0, 0.5)),
    dict(name='TR_PathEdge_Moss_8m', fn=p_edge_moss, lod=(1300, 600, 220), drop=(0.0, 0.5)),
    dict(name='TR_Riverbank_10m', fn=p_riverbank, lod=(1500, 700, 260), drop=(0.0, 0.5), water=True, ground=None),
    dict(name='TR_WaterEdge_10m', fn=p_water_edge, lod=(1500, 700, 260), drop=(0.0, 0.4), water=True, ground=None,
         cull=False),
]

_S = {p['name']: p for p in STONE}
for _n in ('OB_Boulder_12', 'OB_Boulder_14', 'OB_Boulder_16', 'OB_Boulder_20', 'OB_WetRockLow_05'):
    _S[_n]['cut'] = CUT
_C = {p['name']: p for p in CANOPY}
ROOTS.append(_C['OB_BranchKnot_05'])
WATER += [_S[n] for n in ('OB_RiverRock_14', 'OB_SteppingRock_A', 'OB_SteppingRock_B')]
STONE = [p for p in STONE if p['name'] not in ('OB_RiverRock_14', 'OB_SteppingRock_A', 'OB_SteppingRock_B')]
BEAMS = [p for p in CANOPY if p['name'].startswith('TR_CanopyBeam')]
PLATFORM = [_C['TR_CanopyPlatform_20m']]
for p in EDGES[:2]:
    p['cut'] = CUT
EDGES[2]['cut'] = -0.6

SETS = {
    'roots': ('OB_Roots', ROOTS, 1024, OBS),
    'logs': ('OB_Logs', LOGS, 1024, OBS),
    'vines': ('OB_Vines', VINES, 1024, OBS),
    'water': ('OB_Water', WATER, 1024, OBS),
    'stone': ('OB_Stone', STONE, 1024, OBS),
    'hazard': ('OB_Hazard', HAZARD, 512, OBS),
    'pickups': ('PK_Pickups', PICKUPS, 512, OBS),
    'canopy': ('TR_Canopy', BEAMS, 1024, FOR),
    'platform': ('TR_CanopyPlatform', PLATFORM, 1024, FOR),
    'edges': ('TR_Edges', EDGES, 1024, FOR),
}

if __name__ == '__main__':
    a = E.args() or list(SETS)
    for k in a:
        nm, pieces, res, d = SETS[k]
        F.bake_set(nm, pieces, res, d, os.path.join(d, 'Textures'))
