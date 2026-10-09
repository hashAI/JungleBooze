"""Painterly (F4_f) stacked-slab rock: RS_PillarA_P, RS_PillarB_P, RS_ArchSmall_P, RS_Outcrop_P.

ART_DIRECTION_PAINTERLY s2: "pillars and cliffs: stacked rounded slabs with flat sunlit tops carrying a moss/foliage
cap; silhouettes slightly exaggerated (taller, narrower)". Replaces the v1 _P pieces (realistic shapes simplified):
same names, same pivot (base centre at z = 0, Blender +Y = the camera side), same tri budgets and texture size.

Geometry is procedural and built directly as the game LOD0 (decimating a high would melt the seams); faces buried
inside another shell are culled, and the paint is baked onto the game mesh itself (selected-to-active from a high left
dark slivers where the two meshes' seams differ). LOD1/2 drop the small shells (blades, seam plants, moss) first.
- Slab: a rounded irregular outline (superellipse + 2-3 lobes), soft top bevel (~38 % of its height), smaller bottom
  bevel, slight side bulge, a gently domed flat top, a small tilt. Slabs overlap a little, so each seam is a V groove.
- Stacks: a random walk of slab offsets (lean for PillarB, an overhanging hook on top), some levels split into two
  blocks side by side (vertical clefts as in F4_f's towers).
- Bands (ArchSmall span): a rounded-rectangle section swept along an arc; two bands stacked like bent strata.
- Tops: a mossy cap, lumpy bushes (painted leaf clumps; rim bushes spill over) and fern/grass tufts (wide blades).
- Every slab top carries a moss cushion whose scalloped lip spills over the ledge, some ledges a moss mat draping
  down, and plants grow out of about half the seams.
Paint (baked from a procedural material on the high; 'pp' attribute: R = height in its slab, G = top-bevel highlight
mask, B = slab random, A = kind: 0 stone, 0.5 moss cap, 0.75 blade, 1 bush): warm cream top -> cool grey-olive base
over the whole object and within each slab, warm top faces / teal-grey undersides, vertical water-runoff streaks
(greener under the ledges), per-slab tone, painted warm-brown AO in the seams (AO node + slab bottom),
light edge highlight on top bevels, moss on up-facing ledges, the OpenAI rock swatch as a ~10 % stroke overlay, foliage
gradient core -> lit tip on bushes and blades. Roughness flat (stone 0.85, moss 0.9, leaves 0.6). Normal: broad forms.

Outputs: Rootstone/<piece>_P.fbx (<piece>_P_LOD0..2), Rootstone/Textures/<piece>_P_{BaseColor,Normal,ARM}.png,
work/rootstone/<piece>_P_game.blend.
Usage: blender ... -P painterly_slabs.py -- <piece|all> [--geo-only]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from bakekit import Nodes, srgb  # noqa: E402
from rootstone_bake import DETAIL_TILE_M, OUT  # noqa: E402
from heroarch_v2_bake import openness  # noqa: E402
from painterly_rock import PAINT, box_image, preview_mat  # noqa: E402

C = dict(light='#D6B47F', mid='#B08F5E', cool='#857761', shade='#625A48', crevice='#4D3A22', edge='#F7DFB2',
         moss_dark='#3B4A16', moss_mid='#566010', moss_light='#A9A03C',
         leaf_core='#1E300E', leaf_mid='#5A6012', leaf_tip='#BDAD47')


# ------------------------------------------------------------------ geometry
class Acc:
    """Accumulates (verts, faces, pp rows) of closed shells."""

    def __init__(self):
        self.V, self.F, self.A, self.n = [], [], [], 0

    def add(self, V, F, A):
        V = np.asarray(V, float)
        self.V.append(V)
        self.F.extend([tuple(int(i) + self.n for i in f) for f in F])
        self.A.append(np.asarray(A, float))
        self.n += len(V)

    def object(self, name):
        me = bpy.data.meshes.new(name)
        me.from_pydata(np.vstack(self.V).tolist(), [], self.F)
        me.validate()
        ca = me.color_attributes.new('pp', 'FLOAT_COLOR', 'POINT')
        ca.data.foreach_set('color', np.vstack(self.A).ravel().astype(np.float32))
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(name, me)
        E.link(ob)
        for p in me.polygons:
            p.use_smooth = True
        return ob


def grid_faces(nr, nt, pole_lo, pole_hi):
    """Faces for nr rings of nt verts (ring-major) with optional pole verts appended after the rings."""
    F = []
    for i in range(nr - 1):
        for j in range(nt):
            a, b = i * nt + j, i * nt + (j + 1) % nt
            F.append((a, b, b + nt, a + nt))
    k = nr * nt
    if pole_lo:
        for j in range(nt):
            F.append((k, (j + 1) % nt, j))
        k += 1
    if pole_hi:
        top = (nr - 1) * nt
        for j in range(nt):
            F.append((k, top + j, top + (j + 1) % nt))
    return F


def tilt_matrix(ax, ay):
    cx, sx, cy, sy = math.cos(ax), math.sin(ax), math.cos(ay), math.sin(ay)
    rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    return ry @ rx


def slab(acc, nz, c, rx, ry, h, rnd, hi, rot=0.0, lobes=(), bt=0.28, bb=0.14, bulge=0.025, dome=0.05, tilt=(0, 0),
         p=3.6, kind=0.0, wob=0.06, lo_nt=None, simple=False):
    """Rounded stacked slab: base centre c, outline radii rx/ry (superellipse exponent p, lobes [(k, amp, phase)]),
    height h. bt/bb: top/bottom bevel radius as fraction of h; bulge: side bulge (fraction of min radius)."""
    nt = 72 if hi else (lo_nt or (16 if min(rx, ry) > 2.9 else 12))
    th = np.linspace(0, 2 * math.pi, nt, endpoint=False) + rot
    ph = th - rot
    R = (np.abs(np.cos(ph) / rx) ** p + np.abs(np.sin(ph) / ry) ** p) ** (-1 / p)
    for k, amp, phs in lobes:
        R = R * (1 + amp * np.cos(k * ph + phs))
    Bt, Bb = bt * h, bb * h
    g = bulge * min(rx, ry)
    rings = []          # (s, ins, z, highlight, local t)
    na_b = 6 if hi else (1 if simple else 2)
    for a in np.radians(np.linspace(0, 90, na_b)):
        rings.append((1.0, Bb - Bb * math.sin(a), Bb - Bb * math.cos(a), 0.0))
    ns = 6 if hi else (0 if simple else 1)
    for z in np.linspace(Bb, h - Bt, ns + 2)[1:-1]:
        rings.append((1.0, -g * math.sin(math.pi * (z - Bb) / max(h - Bt - Bb, 1e-3)), z, 0.0))
    na_t = 9 if hi else (3 if simple else 4)
    for a in np.radians(np.linspace(0, 90, na_t)):
        hl = float(np.clip(1 - abs(math.degrees(a) - 50) / 40, 0, 1))
        rings.append((1.0, Bt - Bt * math.cos(a), h - Bt + Bt * math.sin(a), hl))
    for s in ((0.8, 0.6, 0.4, 0.2) if hi else (0.5,)):
        rings.append((s, s * Bt, h + dome * h * (1 - s * s), 0.0))
    M = tilt_matrix(*tilt)
    V, A = [], []
    for s, ins, z, hl in rings:
        rho = s * R - ins
        for j in range(nt):
            V.append((rho[j] * math.cos(th[j]), rho[j] * math.sin(th[j]), z))
            A.append((min(z / h, 1.0), hl, rnd, kind))
    V.append((0, 0, 0.0)); A.append((0, 0, rnd, kind))
    V.append((0, 0, h * (1 + dome))); A.append((1, 0, rnd, kind))
    V = np.array(V) @ M.T + np.asarray(c, float)
    # broad irregularity: horizontal push (slab outline wobble) + gentle top undulation; same field low and high
    rad = V[:, :2] - np.asarray(c, float)[:2]
    rn = np.linalg.norm(rad, axis=1, keepdims=True)
    d = nz.n3(V / max(2.0, 0.9 * min(rx, ry)) + rnd * 37)
    V[:, :2] += rad / (rn + 1e-6) * (d * wob * min(rx, ry))[:, None] * np.clip(rn / 1.0, 0, 1)
    V[:, 2] += nz.n3(V / 3.0 + 11 + rnd * 19) * 0.04 * h
    acc.add(V, grid_faces(len(rings), nt, True, True), A)


def band(acc, nz, curve, half_w, half_t, rnd, hi, p=3.2, kind=0.0, width_dir=(0, 1, 0)):
    """Rounded-rectangle section swept along `curve` (n x 3); half_w/half_t arrays (n). Width stays along width_dir;
    'up' of the section is the curve normal in the arch plane (top bevel highlight on the up side)."""
    nt = 48 if hi else 14
    C0 = np.asarray(curve, float)
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(C0, axis=0), axis=1))])
    nseg = max(6, int(L[-1] / (0.35 if hi else 2.2)))
    t = np.linspace(0, L[-1], nseg)
    Cc = np.stack([np.interp(t, L, C0[:, k]) for k in range(3)], -1)
    hw, ht = np.interp(t, L, half_w), np.interp(t, L, half_t)
    T = np.gradient(Cc, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    W = np.tile(np.asarray(width_dir, float), (len(Cc), 1))
    U = np.cross(W, T)
    U /= np.linalg.norm(U, axis=1, keepdims=True)
    U *= np.sign(U[:, 2:3] + 1e-6)          # section 'up' points up
    ph = np.linspace(0, 2 * math.pi, nt, endpoint=False)
    cw = np.sign(np.cos(ph)) * np.abs(np.cos(ph)) ** (2 / p)
    sw = np.sign(np.sin(ph)) * np.abs(np.sin(ph)) ** (2 / p)
    V = (Cc[:, None, :] + (hw[:, None] * cw[None, :])[..., None] * W[:, None, :]
         + (ht[:, None] * sw[None, :])[..., None] * U[:, None, :])
    hl = np.clip((np.sin(ph) > 0.35) * (np.abs(np.cos(ph)) > 0.3) * 1.0, 0, 1)
    A = np.zeros((len(Cc), nt, 4))
    A[..., 0] = (sw[None, :] + 1) / 2
    A[..., 1] = hl[None, :]
    A[..., 2] = rnd
    A[..., 3] = kind
    V = V.reshape(-1, 3)
    A = A.reshape(-1, 4).tolist()
    n = len(Cc)
    Vx = np.vstack([V, Cc[0] - T[0] * hw[0] * 0.2, Cc[-1] + T[-1] * hw[-1] * 0.2])
    A += [[0.5, 0, rnd, kind]] * 2
    d = nz.n3(Vx / 2.5 + rnd * 41)
    Vx += np.vstack([np.zeros((len(V), 3)) + U.repeat(nt, 0) * (d[:len(V)] * 0.05 * ht.repeat(nt))[:, None],
                     np.zeros((2, 3))])
    acc.add(Vx, grid_faces(n, nt, True, True), A)


def bush(acc, nz, c, r, rnd, hi, squash=0.75, res=(12, 7)):
    """Rounded shrub (kind 1): a sphere with cauliflower lobes (a few big leaf clumps), painted dark core -> lit tips."""
    nt, nr = (64, 32) if hi else res
    V, A = [], []
    for i in range(1, nr):
        el = math.pi * i / nr
        for j in range(nt):
            az = 2 * math.pi * j / nt
            V.append((math.sin(el) * math.cos(az), math.sin(el) * math.sin(az), math.cos(el)))
    V = np.array(V + [(0, 0, 1.0), (0, 0, -1.0)])
    lump = 1 + 0.32 * np.clip(nz.n3(V * 1.3 + rnd * 23), -0.2, 1.0) + 0.1 * nz.n3(V * 2.6 + rnd * 7)
    n = V.copy()
    V = V * lump[:, None] * r
    V[:, 2] = np.maximum(V[:, 2] * squash, -0.35 * r)        # flat-ish underside sitting on the rock
    V += np.asarray(c, float)
    A = [((n[k, 2] + 1) / 2, 0, rnd, 1.0) for k in range(len(V))]
    F = []
    rings = nr - 1
    for i in range(rings - 1):
        for j in range(nt):
            a0, a1 = i * nt + j, i * nt + (j + 1) % nt
            F.append((a0, a0 + nt, a1 + nt, a1))
    top, bot = rings * nt, rings * nt + 1
    for j in range(nt):
        F.append((top, j, (j + 1) % nt))
        F.append((bot, (rings - 1) * nt + (j + 1) % nt, (rings - 1) * nt + j))
    acc.add(V, F, A)


TUFTS = True       # wide fern/grass blades (v1's thin blades read as black spikes; these are 2.5x wider, lit tips)


def tuft(acc, rng, c, size, rnd, hi, blades=6):
    """Grass/fern tuft: 3-sided tapered blades fanning out from c (kind 0.75). Disabled by TUFTS (rng still
    advances so the layouts stay put)."""
    if not TUFTS:
        rng.uniform(size=blades * 3)
        return
    for b in range(blades):
        yaw = 2 * math.pi * (b + rng.uniform(-0.3, 0.3)) / blades
        lean = rng.uniform(0.35, 0.8)
        L = size * rng.uniform(0.7, 1.1)
        d = np.array([math.cos(yaw) * math.sin(lean), math.sin(yaw) * math.sin(lean), math.cos(lean)])
        w = 0.22 * size
        segs = 4 if hi else 2
        V, A = [], []
        base = np.asarray(c, float) + d * 0.0
        side = np.array([-math.sin(yaw), math.cos(yaw), 0.0])
        out = np.array([math.cos(yaw), math.sin(yaw), 0.0])
        for k in range(segs):
            t = k / segs
            p = base + d * L * t + out * 0.25 * L * t * t - np.array([0, 0, 0.2 * L * t * t])
            ww = w * (1 - t)
            for a in (0, 2.1, 4.2):
                V.append(p + ww * (math.cos(a) * side + math.sin(a) * np.cross(d, side) * 0.5))
                A.append((t, 0, rnd, 0.75))
        V.append(base + d * L + out * 0.25 * L - np.array([0, 0, 0.2 * L])); A.append((1, 0, rnd, 0.75))
        F = []
        for k in range(segs - 1):
            for j in range(3):
                a0, a1 = k * 3 + j, k * 3 + (j + 1) % 3
                F.append((a0, a1, a1 + 3, a0 + 3))
        tip = len(V) - 1
        for j in range(3):
            F.append(((segs - 1) * 3 + j, (segs - 1) * 3 + (j + 1) % 3, tip))
        F.append((2, 1, 0))
        acc.add(V, F, A)


# ------------------------------------------------------------------ compositions
def column(acc, nz, rng, hi, xy, heights, rx, ry, z0=0.0, rot=None, walk=0.3, lean=(0.0, 0.0), widen=None,
           p=3.6, bt=0.28, ledges=True, seam_plants=0.5):
    """A column of stacked blocky slabs: irregular heights (total kept), shared base rotation with jitter, slight
    tilts, a random walk of offsets. ledges: a mossy cushion on every slab top (it spills over each exposed ledge as
    a scalloped green lip; the buried part is culled later). seam_plants: chance of a small plant growing out of
    each seam. widen: per-slab radius factors. Returns [(centre, rx, ry, top z, rot)]."""
    x, y, z = xy[0], xy[1], z0
    rot = rng.uniform(0, math.pi) if rot is None else rot
    hs = np.array(heights) * rng.uniform(0.72, 1.3, len(heights))
    hs *= sum(heights) / hs.sum()
    out = []
    for i, h in enumerate(hs):
        f = widen[i] if widen is not None else 1.0
        x += lean[0] * h + rng.uniform(-walk, walk)
        y += lean[1] * h + rng.uniform(-walk, walk)
        r_ = rng.random()
        ro = rot + rng.uniform(-0.3, 0.3)
        lob = [(2, rng.uniform(0.02, 0.07), rng.uniform(0, 6.3)), (3, rng.uniform(0.02, 0.06), rng.uniform(0, 6.3)),
               (5, rng.uniform(0.01, 0.03), rng.uniform(0, 6.3))]
        tl = (rng.uniform(-0.09, 0.09), rng.uniform(-0.09, 0.09))
        a, b = rx * f * rng.uniform(0.85, 1.15), ry * f * rng.uniform(0.85, 1.15)
        slab(acc, nz, (x, y, z), a, b, h, r_, hi, ro, lob, tilt=tl, p=p, bt=bt)
        top = z + h * (1 + 0.05)
        rl = rng.random()
        if ledges and i < len(hs) - 1 and rl > 0.55:
            # moss mats draping over the ledge edge (F4_f: moss spilling down, not just a green line)
            for _ in range(1 + int(rl > 0.8)):
                ang = rng.uniform(0, 2 * math.pi)
                ex, ey = math.cos(ang) * a * 0.93, math.sin(ang) * b * 0.93
                slab(acc, nz, (x + ex, y + ey, top - 1.7), rng.uniform(1.4, 2.3), rng.uniform(0.7, 1.0), 1.9,
                     0.5, hi, ang + math.pi / 2, [(3, 0.08, rng.uniform(0, 6))], bt=0.8, bb=0.45, bulge=0.05,
                     dome=0.1, p=2.4, kind=0.5, wob=0.15, lo_nt=10, simple=True)
        if ledges and i < len(hs) - 1 and rl > 0.18:
            # moss cushion on the slab top, slightly wider than the slab: a scalloped lip spills over the ledge
            slab(acc, nz, (x, y, top - 0.8), a * 0.99, b * 0.99, 1.1, 0.5, hi, ro,
                 [(7, 0.07, rng.uniform(0, 6)), (4, 0.05, rng.uniform(0, 6))], bt=0.8, bb=0.2, bulge=0.0, dome=0.25,
                 p=p, kind=0.5, wob=0.14, tilt=tl, lo_nt=14, simple=True)
        if i > 0 and rng.random() < seam_plants:
            ang = rng.uniform(0, 2 * math.pi)
            rr = rng.uniform(0.7, 1.25)
            bush(acc, nz, (x + math.cos(ang) * a * 0.92, y + math.sin(ang) * b * 0.92, z + 0.1), rr, rng.random(),
                 hi, squash=0.8, res=(9, 5))         # a plant spilling over the ledge below this slab
        out.append(((x, y, z), a, b, z + h, ro))
        z += h * rng.uniform(0.9, 0.94)
    return out


def top_dressing(acc, nz, rng, hi, top, cap=True, bushes=3, tufts=4, scale=1.0, bush_r=(1.0, 1.7)):
    (x, y, z0), rx, ry, ztop, rot = top
    if cap:      # mossy cushion following the top slab's outline, lip hanging over the edge
        slab(acc, nz, (x, y, ztop - 0.55 * scale), rx * 0.99, ry * 0.99, 1.1 * scale, 0.5, hi, rot,
             [(3, 0.08, rng.uniform(0, 6)), (5, 0.06, rng.uniform(0, 6))], bt=0.75, bb=0.2, bulge=0.0, dome=0.4,
             p=3.0, kind=0.5, wob=0.1)
    zc = ztop + 0.5 * scale
    for b in range(bushes):
        a = rng.uniform(0, 2 * math.pi)
        rr = rng.uniform(0.0, 0.6) if b % 2 == 0 else rng.uniform(0.75, 1.0)     # every other one on the rim
        r = scale * rng.uniform(*bush_r)
        droop = 0.9 * r * max(0.0, rr - 0.6) / 0.4                                 # rim bushes spill over
        bush(acc, nz, (x + math.cos(a) * rx * rr, y + math.sin(a) * ry * rr, zc + 0.3 * r - droop), r, rng.random(),
             hi)
    for t in range(tufts):
        a = 2 * math.pi * (t + rng.uniform(0, 0.6)) / max(tufts, 1)
        rr = rng.uniform(0.65, 0.85)
        tuft(acc, rng, (x + math.cos(a) * rx * rr, y + math.sin(a) * ry * rr, zc - 0.15 * scale), 1.5 * scale,
             rng.random(), hi, blades=4)


def build_geo(name, hi):
    acc = Acc()
    nz = E.Noise(7)
    rng = np.random.default_rng(dict(RS_PillarA=11, RS_PillarB=23, RS_ArchSmall=31, RS_Outcrop=43)[name])
    if name == 'RS_PillarA':
        # tall karst tower, ~34 m (F4_f): tall rounded blocks in four columns of different heights (vertical clefts,
        # staggered seams, stepped silhouette, slight taper), a bushy crown spilling over the rim
        m = column(acc, nz, rng, hi, (0, 0), [7.0, 6.0, 5.4, 6.2, 5.0, 4.6], 3.9, 3.5, p=2.9, bt=0.2,
                   widen=[1.1, 1.0, 0.95, 0.92, 0.95, 1.02])
        b = column(acc, nz, rng, hi, (3.4, -1.6), [6.2, 5.0, 5.6], 2.7, 2.5, p=2.9, bt=0.2)
        c = column(acc, nz, rng, hi, (-3.2, 1.2), [7.0, 6.0, 5.0, 4.2], 2.6, 2.4, walk=0.2, p=2.9, bt=0.2)
        d = column(acc, nz, rng, hi, (1.0, 3.0), [5.0, 4.4], 2.3, 2.1, p=2.9, bt=0.2)
        top_dressing(acc, nz, rng, hi, m[-1], bushes=10, tufts=8, scale=1.0, bush_r=(1.6, 2.6))
        for t_ in (b[-1], c[-1], d[-1]):
            top_dressing(acc, nz, rng, hi, t_, cap=True, bushes=3, tufts=3, scale=0.8, bush_r=(1.2, 1.9))
    elif name == 'RS_PillarB':
        # leaning tower, ~31 m: tall blocks, the upper ones step out into an overhanging hook (+X), two buttress
        # columns at the foot
        m = column(acc, nz, rng, hi, (-1.5, 0), [6.6, 5.8, 5.2, 5.0, 4.2, 3.8], 3.8, 3.4, lean=(0.08, 0), walk=0.2,
                   p=2.9, bt=0.2, widen=[1.1, 1.0, 0.95, 1.0, 1.15, 1.25])
        h1 = column(acc, nz, rng, hi, (m[-2][0][0] + 2.8, m[-2][0][1] + 0.3), [3.6, 3.2], 2.7, 2.5,
                    z0=m[-2][0][2] + 0.4, rot=m[-2][4], walk=0.2, lean=(0.3, 0), p=2.9, bt=0.22)
        b = column(acc, nz, rng, hi, (-4.6, -1.4), [6.4, 5.0, 4.6], 2.6, 2.4, walk=0.2, p=2.9, bt=0.2)
        c = column(acc, nz, rng, hi, (1.6, 2.6), [5.6, 4.2], 2.3, 2.1, walk=0.2, p=2.9, bt=0.2)
        top_dressing(acc, nz, rng, hi, m[-1], bushes=9, tufts=7, scale=1.0, bush_r=(1.5, 2.4))
        top_dressing(acc, nz, rng, hi, h1[-1], cap=True, bushes=3, tufts=3, scale=0.9, bush_r=(1.2, 1.9))
        for t_ in (b[-1], c[-1]):
            top_dressing(acc, nz, rng, hi, t_, cap=True, bushes=3, tufts=2, scale=0.8, bush_r=(1.2, 1.8))
    elif name == 'RS_ArchSmall':
        # two blocky stacked legs and a span of two bent strata bands, ~33 x 16 x 20 m
        for sx in (-1, 1):
            column(acc, nz, rng, hi, (sx * 11.0, 0.0), [4.2, 3.6, 3.8, 3.0], 4.4, 4.2, walk=0.25,
                   widen=[1.15, 1.0, 0.95, 1.05])
            sc_ = column(acc, nz, rng, hi, (sx * 14.2, rng.uniform(-2, 2)), [3.6, 3.0], 2.4, 2.2, walk=0.2)
            top_dressing(acc, nz, rng, hi, sc_[-1], cap=True, bushes=2, tufts=2, scale=0.7)
        u = np.linspace(0, 1, 40)
        arc = np.stack([-11.8 + 23.6 * u, 0.25 * np.sin(u * 6.0), 11.6 + 5.4 * np.sin(np.pi * u) ** 0.8], -1)
        band(acc, nz, arc, np.full(40, 4.0) - 0.5 * np.sin(np.pi * u), np.full(40, 2.3) - 0.4 * np.sin(np.pi * u),
             0.21, hi, p=4.0)
        u2 = np.linspace(0, 1, 30)
        uu2 = 0.12 + 0.76 * u2
        ht2 = 1.3 * (0.7 + 0.3 * np.sin(np.pi * u2))
        top2 = 11.6 + 5.4 * np.sin(np.pi * uu2) ** 0.8 + 2.3 - 0.4 * np.sin(np.pi * uu2) + 0.7 * ht2
        arc2 = np.stack([-9.0 + 18.0 * u2, -0.7 + 0.3 * np.sin(u2 * 4), top2], -1)
        band(acc, nz, arc2, np.full(30, 3.0), ht2, 0.67, hi, p=4.0)
        for k in range(8):
            uu = 0.05 + 0.9 * (k + rng.uniform(0.1, 0.9)) / 8
            zt = float(np.interp(uu, u2, top2 + ht2 * 0.9))
            r = rng.uniform(1.3, 2.0)
            bush(acc, nz, (-9.0 + 18.0 * uu, rng.uniform(-1.6, 0.4), zt + 0.2 * r), r, rng.random(), hi)
        for k in range(3):
            uu = rng.uniform(0.15, 0.85)
            zt = 11.6 + 5.4 * math.sin(math.pi * uu) ** 0.8 + 2.0
            tuft(acc, rng, (-11.8 + 23.6 * uu, 3.0, zt), 1.4, rng.random(), hi, blades=4)
    elif name == 'RS_Outcrop':
        # overlapping soft lozenge blocks with mossy domes (rocks in water, path edges), ~14.5 x 8 x 4.5 m
        lz = dict(bt=0.42, bb=0.22, dome=0.12, p=3.0)
        slab(acc, nz, (-1.6, 0.2, -0.3), 5.0, 3.4, 2.7, 0.12, hi, 0.1, [(2, 0.05, 1.0), (3, 0.04, 2.0)], **lz)
        slab(acc, nz, (3.0, -0.6, -0.3), 3.6, 2.9, 3.4, 0.53, hi, 0.5, [(3, 0.05, 0.4)], tilt=(0.03, -0.04), **lz)
        slab(acc, nz, (-2.6, 0.7, 2.1), 3.0, 2.3, 1.7, 0.86, hi, -0.3, [(2, 0.06, 2.2)], tilt=(0.0, 0.05), **lz)
        slab(acc, nz, (6.2, 1.2, -0.3), 1.8, 1.5, 1.9, 0.33, hi, 0.7, [(2, 0.08, 0.3)], bt=0.5, bb=0.3, dome=0.2)
        slab(acc, nz, (-6.3, -1.0, -0.3), 1.5, 1.3, 1.5, 0.71, hi, 1.9, [(2, 0.08, 1.3)], bt=0.5, bb=0.3, dome=0.2)
        top_dressing(acc, nz, rng, hi, ((-2.6, 0.7, 2.1), 3.0, 2.3, 3.8, -0.3), cap=True, bushes=2, tufts=2,
                     scale=0.6, bush_r=(1.2, 1.7))
        bush(acc, nz, (3.6, -0.2, 3.3), 0.9, 0.3, hi)
        tuft(acc, rng, (2.0, -2.4, 2.4), 0.9, 0.4, hi, blades=4)
        tuft(acc, rng, (-4.8, -1.8, 2.0), 0.8, 0.8, hi, blades=4)
    ob = acc.object(f'{name}_P_' + ('high' if hi else 'LOD0'))
    E.cut_below(ob, -0.4)
    return ob


PIECES = {'RS_PillarA': dict(lod=(4000, 1800, 600), res=1024), 'RS_PillarB': dict(lod=(4000, 1800, 600), res=1024),
          'RS_ArchSmall': dict(lod=(4000, 1800, 600), res=1024), 'RS_Outcrop': dict(lod=(2500, 1100, 400), res=1024)}


def cull_hidden(ob, margin=0.03):
    """Delete low faces buried inside another shell (all 3 corners inside it): slab bottoms, intersections.
    Inside test: nearest point on the other shell, sign of the offset against its face normal (closed shells)."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    shell = {}
    sid = 0
    for f in bm.faces:
        if f.index in shell:
            continue
        stack_ = [f]
        shell[f.index] = sid
        while stack_:
            g = stack_.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in shell:
                        shell[h.index] = sid
                        stack_.append(h)
        sid += 1
    trees, boxes = [], []
    bm.verts.ensure_lookup_table()
    for k in range(sid):
        fs = [f for f in bm.faces if shell[f.index] == k]
        vs = sorted({v.index for f in fs for v in f.verts})
        remap = {v: i for i, v in enumerate(vs)}
        co = [bm.verts[v].co.copy() for v in vs]
        trees.append(BVHTree.FromPolygons(co, [[remap[v.index] for v in f.verts] for f in fs]))
        a = np.array([c[:] for c in co])
        boxes.append((a.min(0) - 0.1, a.max(0) + 0.1))
    bm.verts.ensure_lookup_table()

    def inside(p, k):
        lo, hi = boxes[k]
        if np.any(np.array(p[:]) < lo) or np.any(np.array(p[:]) > hi):
            return False
        loc, nrm, _, d = trees[k].find_nearest(p)
        return loc is not None and (p - loc).dot(nrm) < -margin
    dead = []
    for f in bm.faces:
        own = shell[f.index]
        if all(any(inside(v.co, k) for k in range(sid) if k != own) for v in f.verts):
            dead.append(f)
    n = len(dead)
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    bm.to_mesh(ob.data)
    bm.free()
    return n


def drop_shells(ob, lod):
    """LOD1: drop grass blades and small seam plants; LOD2: keep only stone and the big top bushes (the decimator
    cannot share 600 tris over ~70 small shells: it shredded them)."""
    import bmesh
    me = ob.data
    kinds = np.zeros(len(me.vertices))
    ca = me.color_attributes['pp']
    cols = np.empty(len(me.vertices) * 4, np.float32)
    ca.data.foreach_get('color', cols)
    kinds = cols.reshape(-1, 4)[:, 3]
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    seen, dead = set(), []
    for v0 in bm.verts:
        if v0.index in seen:
            continue
        comp, st = [], [v0]
        seen.add(v0.index)
        while st:
            v = st.pop()
            comp.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w.index not in seen:
                    seen.add(w.index)
                    st.append(w)
        k = float(np.mean([kinds[v.index] for v in comp]))
        co = np.array([v.co[:] for v in comp])
        diag = float(np.linalg.norm(co.max(0) - co.min(0)))
        if 0.6 < k < 0.9 or (k >= 0.9 and diag < 2.4) or (lod == 2 and (0.4 < k < 0.6 or (k >= 0.9 and diag < 4.0))):
            dead.extend(comp)
    bmesh.ops.delete(bm, geom=dead, context='VERTS')
    bm.to_mesh(me)
    bm.free()
    return len(dead)


# ------------------------------------------------------------------ paint
def slab_material(height):
    m = bpy.data.materials.new('slab_src')
    m.use_nodes = True
    n = Nodes(m)
    bs = n.N['Principled BSDF']
    tc = n.node('ShaderNodeTexCoord')
    geo = n.node('ShaderNodeNewGeometry')
    P = tc.outputs['Object']
    sepp = n.node('ShaderNodeSeparateXYZ'); n.link(P, sepp.inputs[0])
    sepn = n.node('ShaderNodeSeparateXYZ'); n.link(geo.outputs['Normal'], sepn.inputs[0])
    nz = sepn.outputs[2]
    at = n.node('ShaderNodeAttribute', attribute_name='pp')
    pp = n.node('ShaderNodeSeparateColor'); n.link(at.outputs['Color'], pp.inputs[0])
    tl, hl, rnd = pp.outputs[0], pp.outputs[1], pp.outputs[2]
    kind = at.outputs['Alpha']
    hgt = n.maprange(sepp.outputs[2], 0.0, height)
    up = n.maprange(nz, -0.7, 0.95)

    # stone: whole-object warm top -> cool base, plus each slab lighter at its top (painted form)
    warm = n.math('ADD', n.math('MULTIPLY', hgt, 0.55),
                  n.math('ADD', n.math('MULTIPLY', up, 0.25), n.math('MULTIPLY', tl, 0.2)), clamp=True)
    col = n.mix(srgb(C['shade']), srgb(C['cool']), n.maprange(warm, 0.05, 0.35))
    col = n.mix(col, srgb(C['mid']), n.maprange(warm, 0.3, 0.6))
    col = n.mix(col, srgb(C['light']), n.maprange(warm, 0.55, 0.95))
    col = n.mix(col, n.gray(n.maprange(rnd, 0, 1, 0.89, 1.08)), 1.0, 'MULTIPLY')        # per-slab tone
    col = n.mix(col, srgb('#8C7B70'), n.maprange(rnd, 0.3, 0.0, 0.0, 0.3))              # a few mauve-grey slabs
    col = n.mix(col, srgb(C['light']), n.maprange(n.noise(P, 1 / 9.0, 2, 0.4), 0.56, 0.7, 0, 0.25))
    col = n.mix(col, srgb(C['cool']), n.maprange(n.noise(P, 1 / 6.0, 2, 0.4), 0.42, 0.3, 0, 0.25))
    # warm sunlit top faces, cool teal-grey undersides (AD s3/s4: shadows lean teal)
    col = n.mix(col, srgb('#E2C38C'), n.maprange(nz, 0.55, 0.95, 0.0, 0.55))
    col = n.mix(col, srgb('#5B6461'), n.maprange(nz, -0.1, -0.8, 0.0, 0.55))
    # water-runoff streaks: vertically stretched broad strokes on the side faces, darker and a bit greener under
    # each ledge (where the moss drains)
    smp = n.node('ShaderNodeMapping', vector_type='POINT')
    smp.inputs['Scale'].default_value = (1 / 0.7, 1 / 0.7, 1 / 9.0)
    n.link(P, smp.inputs[0])
    stk = n.node('ShaderNodeTexNoise', in_2=1.0, in_3=2.0, in_4=0.45)
    n.link(smp.outputs[0], stk.inputs[0])
    side = n.maprange(n.math('ABSOLUTE', nz), 0.65, 0.2)
    streak = n.math('MULTIPLY', n.maprange(stk.outputs['Fac'], 0.52, 0.66), side)
    col = n.mix(col, srgb('#6E5C45'), n.math('MULTIPLY', streak, 0.5))
    drip = n.math('MULTIPLY', n.math('MULTIPLY', n.maprange(stk.outputs['Fac'], 0.45, 0.6), side),
                  n.maprange(tl, 0.55, 0.95))
    col = n.mix(col, srgb('#4E5A22'), n.math('MULTIPLY', drip, 0.55))
    rk = box_image(n, os.path.join(PAINT, 'swatch_rock_tile.png'), P, 5.0)
    lum = n.node('ShaderNodeRGBToBW'); n.link(rk.outputs['Color'], lum.inputs[0])
    col = n.mix(col, n.gray(n.maprange(lum.outputs[0], 0.25, 0.75, 0.94, 1.05)), 1.0, 'MULTIPLY')
    # painted AO: seams (AO node) and the tucked-in slab bottoms, warm brown
    ao = n.node('ShaderNodeAmbientOcclusion', samples=16, only_local=False)
    ao.inputs['Distance'].default_value = 1.6
    aof = ao.outputs['AO']
    col = n.mix(col, srgb(C['crevice']), n.maprange(aof, 0.9, 0.3, 0.0, 0.65))
    col = n.mix(col, srgb(C['crevice']), n.math('MULTIPLY', n.maprange(tl, 0.22, 0.0, 0.0, 0.6),
                                                n.math('SUBTRACT', 1.0, n.maprange(kind, 0.4, 0.5))))
    # light edge highlight on the top bevels
    col = n.mix(col, srgb(C['edge']), n.math('MULTIPLY', n.math('MULTIPLY', hl, n.maprange(nz, 0.15, 0.7)), 0.6))
    # moss on up-facing ledges (painted, soft edge) + the mossy cap kind
    brk = n.maprange(n.noise(P, 1 / 3.0, 2, 0.45), 0.34, 0.56)
    opn = n.maprange(aof, 0.5, 0.8)                     # no moss down in the seams (read as black lines)
    moss = n.maprange(n.math('MULTIPLY', n.math('MULTIPLY', n.maprange(nz, 0.5, 0.8), brk), opn), 0.1, 0.5)
    capk = n.maprange(kind, 0.4, 0.5)
    moss = n.math('MAXIMUM', moss, n.math('MULTIPLY', capk, n.maprange(n.math('ADD', nz, n.math('MULTIPLY', brk, 0.5)),
                                                                       -0.6, 0.2)))
    ms = box_image(n, os.path.join(PAINT, 'swatch_moss_tile.png'), P, 2.6)
    mflat = n.mix(srgb(C['moss_dark']), srgb(C['moss_mid']), n.maprange(nz, -0.4, 0.5))
    mflat = n.mix(mflat, srgb(C['moss_light']), n.maprange(n.math('ADD', nz, n.math('MULTIPLY', hgt, 0.2)),
                                                           0.75, 1.15, 0.0, 0.7))
    mcol = n.mix(mflat, ms.outputs['Color'], 0.3)
    mcol = n.mix(mcol, srgb(C['moss_dark']), n.maprange(aof, 0.7, 0.2, 0.0, 0.35))
    col = n.mix(col, mcol, moss)
    # foliage (bushes kind 1, blades kind 0.75): dark core -> lit tips, painted leaf clumps (Voronoi cells)
    vo = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='F1')
    vo.inputs['Scale'].default_value = 1.6
    n.link(P, vo.inputs['Vector'])
    vsep = n.node('ShaderNodeSeparateColor'); n.link(vo.outputs['Color'], vsep.inputs[0])
    vd = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='DISTANCE_TO_EDGE')
    vd.inputs['Scale'].default_value = 1.6
    n.link(P, vd.inputs['Vector'])
    lf = n.math('ADD', n.math('MULTIPLY', tl, 0.55), n.math('MULTIPLY', up, 0.45))
    lf = n.math('ADD', lf, n.maprange(vsep.outputs[0], 0, 1, -0.12, 0.12))
    lf = n.math('ADD', lf, n.math('MULTIPLY', n.maprange(kind, 0.7, 0.8), n.maprange(kind, 0.9, 1.0, 0.35, 0.0)))
    fcol = n.mix(srgb(C['leaf_core']), srgb(C['leaf_mid']), n.maprange(lf, 0.15, 0.55))
    fcol = n.mix(fcol, srgb(C['leaf_tip']), n.maprange(lf, 0.55, 1.0, 0.0, 0.85))
    fcol = n.mix(fcol, srgb(C['leaf_core']), n.math('MULTIPLY', n.maprange(vd.outputs['Distance'], 0.0, 0.2, 0.3, 0.0),
                                                    n.maprange(lf, 0.2, 0.7)))
    fcol = n.mix(fcol, srgb(C['leaf_core']), n.math('MULTIPLY', n.maprange(aof, 0.7, 0.2, 0.0, 0.5),
                                                    n.maprange(kind, 0.8, 0.95, 0.3, 1.0)))
    fol = n.maprange(kind, 0.6, 0.75)
    col = n.mix(col, fcol, fol)
    n.link(col, bs.inputs['Base Color'])
    rough = n.maprange(moss, 0, 1, 0.85, 0.9)
    rough = n.math('ADD', n.math('MULTIPLY', rough, n.math('SUBTRACT', 1.0, fol)), n.math('MULTIPLY', fol, 0.6))
    n.link(rough, bs.inputs['Roughness'])
    bs.inputs['Metallic'].default_value = 0.0
    # broad normal only: faint strokes on stone, clumps on moss and leaves
    msl = n.node('ShaderNodeRGBToBW'); n.link(ms.outputs['Color'], msl.inputs[0])
    hgtb = n.math('ADD', n.math('MULTIPLY', lum.outputs[0], 0.1), n.math('MULTIPLY', n.math('MULTIPLY', msl.outputs[0],
                                                                                             moss), 0.5))
    hgtb = n.math('ADD', hgtb, n.math('MULTIPLY', n.math('MULTIPLY', vd.outputs['Distance'], fol), 1.2))
    bump = n.node('ShaderNodeBump', in_0=0.5, in_1=0.06)
    n.link(hgtb, bump.inputs['Height'])
    n.link(bump.outputs[0], bs.inputs['Normal'])
    return m


# ------------------------------------------------------------------ bake + export
def bake_self(ob, kind, img, samples, **kw):
    """Bake the piece's own paint material onto itself (no selected-to-active): the slab seams of the game mesh
    are exactly where they are painted, so no buried-surface slivers (high/low mismatch) can show."""
    sc = bpy.context.scene
    sc.cycles.samples = samples
    E.bake_target(ob, img)
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    E.set_active(ob)
    a = dict(type=kind, use_clear=True, margin=8)
    a.update(kw)
    bpy.ops.object.bake(**a)


def build(name, geo_only=False):
    sp = PIECES[name]
    pn = name + '_P'
    E.reset()
    sc = E.gpu_cycles(8)
    low = build_geo(name, False)
    E.triangulate(low)
    print(pn, 'built tris', E.tris(low))
    print(pn, 'hidden faces culled', cull_hidden(low))
    E.decimate_to(low, sp['lod'][0])
    if geo_only:
        bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', pn + '_slabgeo.blend'), compress=True)
        return
    me = low.data
    me.normals_split_custom_set_from_vertices([v.normal[:] for v in me.vertices])
    height = max((low.matrix_world @ Vector(c)).z for c in low.bound_box)
    paint = slab_material(height)
    me.materials.append(paint)
    op = openness(low, dirs=16, dist=4.0)
    wts = []
    for p in me.polygons:
        w = 0.5 if p.normal.z < -0.5 else (0.8 if p.normal.y < -0.4 else 1.0)  # undersides, far side (-Y)
        wts.append(round(w * (0.4 + 0.6 * min(1.0, op[p.index] / 0.7)), 1))
    me.uv_layers.new(name='UVMap')
    res = sp['res']
    dens = E.xatlas_faces(low, list(range(len(me.polygons))), res, padding_px=4, weights=wts)
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.3))
    ground = bpy.context.active_object
    sc.world = bpy.data.worlds.new('w')

    base = E.new_image(pn + '_BaseColor', res)
    bake_self(low, 'DIFFUSE', base, 32, pass_filter={'COLOR'})
    nrm = E.new_image(pn + '_Normal', res, non_color=True)      # the paint's broad bump only (AD s3)
    bake_self(low, 'NORMAL', nrm, 8, normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z')
    rgh = E.new_image(pn + '_rough', res, non_color=True)
    bake_self(low, 'ROUGHNESS', rgh, 4)
    aom = bpy.data.materials.new('ao_src'); aom.use_nodes = True
    aon = aom.node_tree.nodes.new('ShaderNodeAmbientOcclusion')
    aon.samples, aon.only_local = 32, False
    aon.inputs['Distance'].default_value = 1.6
    aom.node_tree.links.new(aon.outputs['AO'], aom.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    me.materials.clear(); me.materials.append(aom)
    ao = E.new_image(pn + '_ao', res, non_color=True)
    bake_self(low, 'DIFFUSE', ao, 96, pass_filter={'COLOR'})
    bpy.data.objects.remove(ground)

    arm = E.new_image(pn + '_ARM', res, non_color=True)
    o = np.zeros((res, res, 4), np.float32)
    o[..., 0] = np.clip(E.pixels(ao)[..., 0], 0.3, 1) ** 1.2      # albedo already has the painted seam AO
    o[..., 1] = E.pixels(rgh)[..., 0]
    o[..., 3] = 1
    E.set_pixels(arm, o)
    tdir = os.path.join(OUT, 'Textures')
    E.save_png(base, os.path.join(tdir, pn + '_BaseColor.png'))
    E.save_png(nrm, os.path.join(tdir, pn + '_Normal.png'))
    E.save_png(arm, os.path.join(tdir, pn + '_ARM.png'))
    me.materials.clear()
    mt = bpy.data.materials.new(f'MI_{pn}')
    mt.use_nodes = True
    me.materials.append(mt)
    preview_mat(mt, base, nrm, arm)
    lods = [low]
    for i in (1, 2):
        lo = E.copy_obj(low, f'{pn}_LOD{i}')
        print(lo.name, 'dropped shells', drop_shells(lo, i))
        E.decimate_to(lo, sp['lod'][i])
        lods.append(lo)
    for o_ in lods:
        for an in [x.name for x in o_.data.color_attributes]:   # bake-only attribute; the game mesh exports none
            o_.data.color_attributes.remove(o_.data.color_attributes[an])
    for o_ in lods:
        o_.data.normals_split_custom_set_from_vertices([v.normal[:] for v in o_.data.vertices])
        E.box_uv2(o_, DETAIL_TILE_M)
    E.export_fbx(lods, os.path.join(OUT, pn + '.fbx'))
    for im in (base, nrm, arm):
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', pn + '_game.blend'), compress=True)
    pts = [low.matrix_world @ Vector(c) for c in low.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 1) for i in range(3)]
    print('SUMMARY', pn, size, [E.tris(x) for x in lods], res, f'{dens:.1f} px/m')


if __name__ == '__main__':
    a = E.args()
    names = [x for x in a if not x.startswith('--')]
    for nm in (list(PIECES) if not names or names[0] == 'all' else names):
        build(nm, geo_only='--geo-only' in a)
