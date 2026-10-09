"""Expedition forest kit (painterly, F4_f): shared library for obstacles, pickups, path pieces and trees.

Builds on the painterly stacked-slab recipe (painterly_slabs.py): geometry is procedural and built directly as the
game LOD0, buried faces are culled, and the paint is a procedural material baked onto the game mesh itself.
New here:
- KAcc: an accumulator whose vertices carry a material kind ('mk', table MK) and a grain coordinate ('bc': for swept
  tubes (radius cos, radius sin, metres along), so bark grain runs along every branch with no seam).
- sweep(): a tube along a curve with a superellipse section (flat-topped logs, beams), ridges, wobble, end caps
  (end-grain caps optional).
- One paint material for every kind (stone, wet stone, moss, foliage, obstacle bark, driftwood, stiltwood bark,
  thorn, crimson thorn tips and warning blooms, earth, vine, coin gold, gem, crystal, brass, cloth, teal).
- bake_set(): several pieces share ONE texture set and one material (ASTC atlas, fewer draws): pieces are built,
  culled and decimated one by one, laid out on a grid, joined, unwrapped together (xatlas, weighted), baked (BaseColor,
  Normal, roughness, metallic, AO -> ARM), then split back per piece, LOD1/2 made (small shells dropped first, then
  decimate), and exported one FBX per piece. Gameplay boxes are measured on the final LOD0 and reported.

Axes as the rest of the kit: Blender -Y = Unity local +Z (forward, the run direction), Blender +X = Unity local -X,
Blender +Y = the camera side. FBX export through envlib.export_fbx (Y-up, scale 1).
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from bakekit import Nodes, srgb  # noqa: E402
from heroarch_v2_bake import openness  # noqa: E402
from painterly_rock import PAINT, box_image, preview_mat  # noqa: E402
from painterly_slabs import Acc, bush, cull_hidden, slab, tuft  # noqa: E402,F401

ENV = E.UNITY_ENV
FPAINT = os.path.join(E.SRC, 'work', 'forest')          # tileable forest swatches (tools/art/forest_finish.py)

MK = dict(stone=0, moss=1, blade=2, bush=3, bark=4, drift=5, thorn=6, thorntip=7, earth=8, gold=9, gem=10,
          crystal=11, brass=12, vine=13, sbark=14, wet=15, endgrain=16, cloth=17, bloom=18, teal=19, cream=20,
          dark=21)


# ------------------------------------------------------------------ accumulator
class KAcc(Acc):
    """Acc with a material kind per vertex (self.mk applies to every add() that doesn't pass one) and a grain
    coordinate. water=True marks the piece as sitting in water (wet band below z 0.05 in the paint)."""

    def __init__(self, water=False):
        super().__init__()
        self.K, self.Bc, self.mk, self.water = [], [], 0.0, water

    def add(self, V, F, A, bc=None, mk=None):
        V = np.asarray(V, float)
        super().add(V, F, A)
        self.K.append(np.full(len(V), self.mk if mk is None else mk, float))
        self.Bc.append(np.asarray(bc, float) if bc is not None else V.copy())

    def use(self, kind):
        self.mk = float(MK[kind])
        return self

    def object(self, name):
        for a in self.A:
            a[:, 3] = 1.0 if self.water else 0.0     # pp alpha = 'in water' flag (slab() wrote its kind there)
        ob = super().object(name)
        me = ob.data
        a = me.attributes.new('mk', 'FLOAT', 'POINT')
        a.data.foreach_set('value', np.concatenate(self.K).astype(np.float32))
        b = me.attributes.new('bc', 'FLOAT_VECTOR', 'POINT')
        b.data.foreach_set('vector', np.vstack(self.Bc).ravel().astype(np.float32))
        return ob


# ------------------------------------------------------------------ shapes
def sweep(acc, nz, pts, radii, rnd=0.5, nt=8, seg=0.45, flat=1.0, p=2.0, wob=0.06, caps=(True, True),
          cap_mk=None, ridge=(0, 0.0, 0.0), up=(0, 0, 1), smooth=True, mk=None, roll=0.0):
    """Tube along pts (catmull-rom if smooth) with radii interpolated along it. Section: superellipse exponent p
    (2 round, 4 flat-sided), side half-width = flat x radius, 'up' half-height = radius (section up ~ world up for
    level curves). ridge = (count, amplitude, twist rad/m) for bark ridges. caps: rounded pole ends, or (cap_mk)
    separate flat end-grain caps. Returns the centre line."""
    P = np.asarray(pts, float)
    Ltot = float(np.sum(np.linalg.norm(np.diff(P, axis=0), axis=1)))
    n = max(3, int(math.ceil(Ltot / seg)) + 1)
    C = E.catmull(P, n) if (smooth and len(P) > 2) else E.resample(P, n)
    t = np.linspace(0, 1, n)
    R = np.interp(t, np.linspace(0, 1, len(radii)), radii)
    FL = np.interp(t, np.linspace(0, 1, len(np.atleast_1d(flat))), np.atleast_1d(flat))
    T, N, B = E.frames(C, up)
    U = -B
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(C, axis=0), axis=1))])
    ph = np.linspace(0, 2 * math.pi, nt, endpoint=False) + roll
    cw = np.sign(np.cos(ph)) * np.abs(np.cos(ph)) ** (2 / p)
    sw = np.sign(np.sin(ph)) * np.abs(np.sin(ph)) ** (2 / p)
    k, amp, tw = ridge
    rr = 1 + amp * np.cos(k * ph[None, :] + tw * L[:, None]) if k else np.ones((n, nt))
    side = ((R * FL)[:, None] * cw[None, :] * rr)[..., None] * N[:, None, :]
    upv = (R[:, None] * sw[None, :] * rr)[..., None] * U[:, None, :]
    V = C[:, None, :] + side + upv
    dirn = side + upv
    dirn /= np.linalg.norm(dirn, axis=2, keepdims=True) + 1e-9
    if wob:
        d = nz.n3(V / np.maximum(0.6, R[:, None, None] * 2.5) + rnd * 17.0)
        V = V + dirn * (d * wob * R[:, None])[..., None]
    A = np.zeros((n, nt, 4))
    A[..., 0] = t[:, None]
    A[..., 1] = np.clip((sw[None, :] - 0.5) / 0.4, 0, 1)
    A[..., 2] = rnd
    bc = np.stack([R[:, None] * np.cos(ph)[None, :], R[:, None] * np.sin(ph)[None, :],
                   np.repeat(L[:, None], nt, 1)], -1)
    V, A, bc = V.reshape(-1, 3), A.reshape(-1, 4), bc.reshape(-1, 3)
    F = []
    for i in range(n - 1):
        for j in range(nt):
            a, b = i * nt + j, i * nt + (j + 1) % nt
            F.append((a, b, b + nt, a + nt))
    extra_V, extra_A, extra_bc = [], [], []
    m = len(V)
    if cap_mk is None:
        for end, (ci, sgn) in enumerate(((0, -1), (n - 1, 1))):
            if not caps[end]:
                continue
            extra_V.append(C[ci] + sgn * T[ci] * R[ci] * 0.45)
            extra_A.append((t[ci], 0, rnd, 0))
            extra_bc.append((0, 0, L[ci] + sgn * R[ci] * 0.45))
            pole = m + len(extra_V) - 1
            for j in range(nt):
                a, b = ci * nt + j, ci * nt + (j + 1) % nt
                F.append((pole, b, a) if sgn < 0 else (pole, a, b))
        acc.add(np.vstack([V] + ([np.array(extra_V)] if extra_V else [])), F,
                np.vstack([A] + ([np.array(extra_A)] if extra_A else [])),
                bc=np.vstack([bc] + ([np.array(extra_bc)] if extra_bc else [])), mk=mk)
    else:
        acc.add(V, F, A, bc=bc, mk=mk)
        for end, (ci, sgn) in enumerate(((0, -1), (n - 1, 1))):
            if not caps[end]:
                continue
            ring = V[ci * nt:(ci + 1) * nt]
            ctr = ring.mean(0) + sgn * T[ci] * R[ci] * 0.04
            cv = np.vstack([ring, ctr[None]])
            ca = np.zeros((nt + 1, 4))
            ca[:nt, 0] = 1.0
            ca[:, 2] = rnd
            cb = np.vstack([np.stack([R[ci] * np.cos(ph), R[ci] * np.sin(ph), np.zeros(nt)], -1), [[0, 0, 0]]])
            cf = [((j + 1) % nt, j, nt) if sgn < 0 else (j, (j + 1) % nt, nt) for j in range(nt)]
            acc.add(cv, cf, ca, bc=cb, mk=cap_mk)
    return C


def cone(acc, base, direction, length, r, rnd=0.5, sides=4, mk=None):
    """Thorn: an open cone (pp R = 0 at the base, 1 at the tip)."""
    d = np.asarray(direction, float)
    d /= np.linalg.norm(d)
    a = np.cross(d, (0, 0, 1) if abs(d[2]) < 0.9 else (1, 0, 0))
    a /= np.linalg.norm(a)
    b = np.cross(d, a)
    base = np.asarray(base, float)
    V = [base + r * (math.cos(2 * math.pi * k / sides) * a + math.sin(2 * math.pi * k / sides) * b) for k in range(sides)]
    V.append(base + d * length)
    F = [(k, (k + 1) % sides, sides) for k in range(sides)]
    A = [(0, 0, rnd, 0)] * sides + [(1, 0, rnd, 0)]
    acc.add(np.array(V), F, np.array(A, float), mk=mk)


def lathe(acc, prof, nt=24, c=(0, 0, 0), rot=None, rnd=0.5, mk=None, hl=None, star=None):
    """Surface of revolution about local z: prof = [(r, z)], r = 0 rows become poles. rot: 3x3 matrix applied
    before the offset. pp R = row parameter (0..1); hl: per-row highlight weights. star = (points, depth): radius
    modulated into a star (compass-rose emboss)."""
    ph = np.linspace(0, 2 * math.pi, nt, endpoint=False)
    V, A, rows = [], [], []
    for i, (r, z) in enumerate(prof):
        if r <= 1e-6:
            rows.append([len(V)])
            V.append((0, 0, z))
            A.append((i / max(1, len(prof) - 1), hl[i] if hl else 0, rnd, 0))
            continue
        rr = r * np.ones(nt)
        if star:
            k, dep = star
            rr = r * (1 - dep * (0.5 - 0.5 * np.cos(k * ph)) ** 3)
        rows.append(list(range(len(V), len(V) + nt)))
        for j in range(nt):
            V.append((rr[j] * math.cos(ph[j]), rr[j] * math.sin(ph[j]), z))
            A.append((i / max(1, len(prof) - 1), hl[i] if hl else 0, rnd, 0))
    F = []
    for a_, b_ in zip(rows[:-1], rows[1:]):
        if len(a_) == 1 and len(b_) == 1:
            continue
        if len(a_) == 1:
            F += [(a_[0], b_[j], b_[(j + 1) % nt]) for j in range(nt)]
        elif len(b_) == 1:
            F += [(a_[j], a_[(j + 1) % nt], b_[0]) for j in range(nt)]
        else:
            F += [(a_[j], a_[(j + 1) % nt], b_[(j + 1) % nt], b_[j]) for j in range(nt)]
    V = np.array(V, float)
    if rot is not None:
        V = V @ np.asarray(rot).T
    acc.add(V + np.asarray(c, float), F, np.array(A, float), mk=mk)


def facet_prism(acc, base, axis, length, r, sides=6, tip=0.35, rnd=0.5, mk=None):
    """Faceted crystal: a prism with a pointed tip, every face its own vertices (flat shading after export)."""
    d = np.asarray(axis, float)
    d /= np.linalg.norm(d)
    a = np.cross(d, (0, 0, 1) if abs(d[2]) < 0.9 else (1, 0, 0))
    a /= np.linalg.norm(a)
    b = np.cross(d, a)
    base = np.asarray(base, float)
    ring = lambda h, rr: [base + d * h + rr * (math.cos(2 * math.pi * k / sides + rnd) * a +
                                              math.sin(2 * math.pi * k / sides + rnd) * b) for k in range(sides)]
    r0, r1 = ring(0, r * 0.85), ring(length * (1 - tip), r)
    apex = base + d * length
    for k in range(sides):
        k2 = (k + 1) % sides
        acc.add(np.array([r0[k], r0[k2], r1[k2], r1[k]]), [(0, 1, 2, 3)],
                np.array([(0, 0, rnd, 0), (0, 0, rnd, 0), (0.7, 1, rnd, 0), (0.7, 1, rnd, 0)]), mk=mk)
        acc.add(np.array([r1[k], r1[k2], apex]), [(0, 1, 2)],
                np.array([(0.7, 1, rnd, 0), (0.7, 1, rnd, 0), (1, 1, rnd, 0)]), mk=mk)


def blob(acc, nz, c, r, rnd, squash=0.75, res=(9, 5), kind='bush'):
    """Leaf clump (painted dark core -> lit tips) or a moss/earth/rock lump (kind). Restores the current kind."""
    prev = acc.mk
    acc.use(kind)
    bush(acc, nz, c, r, rnd, False, squash=squash, res=res)
    acc.mk = prev


def rock(acc, nz, c, rx, ry, h, rnd, rot=0.0, kind='stone', p=3.0, bt=0.42, bb=0.22, dome=0.12, tilt=(0, 0),
         lobes=None, lo_nt=None):
    prev = acc.mk
    acc.use(kind)
    lob = lobes if lobes is not None else [(2, 0.05, rnd * 6), (3, 0.04, rnd * 9)]
    slab(acc, nz, c, rx, ry, h, rnd, False, rot, lob, bt=bt, bb=bb, dome=dome, p=p, tilt=tilt, lo_nt=lo_nt)
    acc.mk = prev


def moss_cap(acc, nz, c, rx, ry, rnd, rot=0.0, h=0.25):
    prev = acc.mk
    acc.use('moss')
    slab(acc, nz, c, rx, ry, h, rnd, False, rot, [(5, 0.08, rnd * 5), (3, 0.06, rnd * 3)], bt=0.8, bb=0.2,
         bulge=0.0, dome=0.3, p=2.6, wob=0.14, lo_nt=12, simple=True)
    acc.mk = prev


# ------------------------------------------------------------------ paint
PAL = dict(
    # gameplay stone: a little darker / cooler than the scenery rootstone so obstacles sit below the path in value
    st_shade='#463F37', st_cool='#5F574D', st_mid='#7A6A53', st_light='#9A8565', st_top='#AE9770', st_under='#3F4A49',
    crevice='#4D3A22', edge='#F7DFB2',
    moss_dark='#2C3E12', moss_mid='#4A6119', moss_light='#86973A',
    # gameplay chunks keep the safe-route emerald readable (PAINTERLY s8 note): greener than the olive-gold basin
    leaf_core='#15301A', leaf_mid='#2E5C24', leaf_tip='#94A83E',
    # obstacle bark: dark body, bright worn top rim (reads against the golden path at speed)
    bk_shade='#2A1E16', bk_mid='#463223', bk_lit='#684B32', bk_rim='#A57C4C', bk_under='#2A302D', bk_groove='#24190F',
    dr_shade='#6B6257', dr_mid='#A39682', dr_lit='#D7CDB9',
    sb_shade='#4C4843', sb_mid='#7D6B58', sb_lit='#B59E80', sb_top='#D2BE9B', sb_groove='#3A3029',
    th_stem='#21171A', th_lit='#47303A', crimson='#9E2238', crimson_lit='#C9475C',
    ea_deep='#3B2E15', ea_side='#6A5226', ea_lit='#9C7D3F',
    gold='#F2C230', gold_dark='#B9821A', gold_hi='#FFF0A6', gem='#2EC4B6', gem_hi='#B6F3EC',
    cry='#2DB3CC', cry_hi='#C9F7FC', cry_deep='#1B6E8C', brass='#A77A36', brass_hi='#DDB662',
    vine_dark='#36421A', vine_mid='#62702A', vine_lit='#9A9A3E', end_a='#C99F69', end_b='#9E7448',
    cloth='#E8DCC0', cloth_sh='#B9A98A', teal='#1D8A82', teal_hi='#46B9AC', cream='#F1E3C3', wet_algae='#3D5A3A',
    dark='#1E1A17')


def kit_material(mode='color'):
    """mode: 'color' (base colour + broad bump), 'rough' or 'metal' (that scalar as base colour, for the ARM bake)."""
    m = bpy.data.materials.new('kit_src_' + mode)
    m.use_nodes = True
    n = Nodes(m)
    bs = n.N['Principled BSDF']
    tc = n.node('ShaderNodeTexCoord')
    geo = n.node('ShaderNodeNewGeometry')
    P = tc.outputs['Object']
    sepp = n.node('ShaderNodeSeparateXYZ'); n.link(P, sepp.inputs[0])
    sepn = n.node('ShaderNodeSeparateXYZ'); n.link(geo.outputs['Normal'], sepn.inputs[0])
    nz = sepn.outputs[2]
    up = n.maprange(nz, -0.7, 0.95)
    at = n.node('ShaderNodeAttribute', attribute_name='pp')
    pp = n.node('ShaderNodeSeparateColor'); n.link(at.outputs['Color'], pp.inputs[0])
    tl, hl, rnd = pp.outputs[0], pp.outputs[1], pp.outputs[2]
    water = at.outputs['Alpha']
    mk = n.node('ShaderNodeAttribute', attribute_name='mk').outputs['Fac']
    bcv = n.node('ShaderNodeAttribute', attribute_name='bc').outputs['Vector']
    hz = n.node('ShaderNodeAttribute', attribute_name='hz').outputs['Fac']
    ao = n.node('ShaderNodeAmbientOcclusion', samples=16, only_local=False)
    ao.inputs['Distance'].default_value = 1.0
    aof = ao.outputs['AO']

    def sel(k):
        return n.maprange(n.math('ABSOLUTE', n.math('SUBTRACT', mk, float(k))), 0.3, 0.5, 1.0, 0.0)

    def c(h):
        return srgb(PAL[h] if h in PAL else h)

    # grain coordinate: bc stretched along the branch (z = metres along)
    gm = n.node('ShaderNodeMapping', vector_type='POINT')
    gm.inputs['Scale'].default_value = (1 / 0.22, 1 / 0.22, 1 / 1.8)
    n.link(bcv, gm.inputs[0])
    gnz = n.node('ShaderNodeTexNoise', in_2=1.0, in_3=3.0, in_4=0.5)
    n.link(gm.outputs[0], gnz.inputs[0])
    grain = gnz.outputs['Fac']
    brk = n.maprange(n.noise(P, 1 / 2.6, 2, 0.4), 0.3, 0.5)          # broad moss shapes, few holes (painterly)
    rk = box_image(n, os.path.join(PAINT, 'swatch_rock_tile.png'), P, 3.0)
    rlum = n.node('ShaderNodeRGBToBW'); n.link(rk.outputs['Color'], rlum.inputs[0])
    ms = box_image(n, os.path.join(PAINT, 'swatch_moss_tile.png'), P, 1.6)
    mlum = n.node('ShaderNodeRGBToBW'); n.link(ms.outputs['Color'], mlum.inputs[0])

    # ---- moss (also used on top of stone and bark)
    mflat = n.mix(c('moss_dark'), c('moss_mid'), n.maprange(nz, -0.4, 0.5))
    mflat = n.mix(mflat, c('moss_light'), n.maprange(n.math('ADD', nz, n.math('MULTIPLY', hz, 0.25)), 0.75, 1.2, 0, 0.7))
    mcol = n.mix(mflat, ms.outputs['Color'], 0.3)
    mcol = n.mix(mcol, c('moss_dark'), n.maprange(aof, 0.7, 0.2, 0.0, 0.4))
    moss_up = n.math('MULTIPLY', n.math('MULTIPLY', n.maprange(nz, 0.55, 0.85), brk), n.maprange(aof, 0.5, 0.8))
    moss_up = n.maprange(moss_up, 0.1, 0.5)

    # ---- stone (gameplay boulders, gap rocks)
    warm = n.math('ADD', n.math('MULTIPLY', hz, 0.5), n.math('ADD', n.math('MULTIPLY', up, 0.25),
                                                             n.math('MULTIPLY', tl, 0.25)), clamp=True)
    st = n.mix(c('st_shade'), c('st_cool'), n.maprange(warm, 0.05, 0.35))
    st = n.mix(st, c('st_mid'), n.maprange(warm, 0.3, 0.6))
    st = n.mix(st, c('st_light'), n.maprange(warm, 0.55, 0.95))
    st = n.mix(st, n.gray(n.maprange(rnd, 0, 1, 0.88, 1.08)), 1.0, 'MULTIPLY')
    st = n.mix(st, c('st_top'), n.maprange(nz, 0.55, 0.95, 0.0, 0.35))
    st = n.mix(st, c('st_under'), n.maprange(nz, -0.1, -0.8, 0.0, 0.6))
    st = n.mix(st, n.gray(n.maprange(rlum.outputs[0], 0.25, 0.75, 0.93, 1.06)), 1.0, 'MULTIPLY')
    st = n.mix(st, c('crevice'), n.maprange(aof, 0.9, 0.3, 0.0, 0.65))
    st = n.mix(st, c('edge'), n.math('MULTIPLY', n.math('MULTIPLY', hl, n.maprange(nz, 0.15, 0.7)), 0.55))
    st = n.mix(st, n.gray(0.78), 1.0, 'MULTIPLY')       # obstacles must sit below the path in value (AD s3.4, D2)
    st_m = n.mix(st, mcol, moss_up)
    # wet stone: darker, a little teal, algae band at the waterline
    wt = n.mix(st, n.gray(0.62), 1.0, 'MULTIPLY')
    wt = n.mix(wt, c('#3E5C66'), 0.12)
    wt = n.mix(wt, c('wet_algae'), n.math('MULTIPLY', n.maprange(sepp.outputs[2], -0.35, -0.05), n.maprange(sepp.outputs[2], 0.35, 0.1)))
    wt = n.mix(wt, mcol, n.math('MULTIPLY', moss_up, n.maprange(sepp.outputs[2], 0.3, 0.6)))

    # ---- obstacle bark (dark body, bright top rim)
    bw = n.math('ADD', n.math('MULTIPLY', up, 0.65), n.math('MULTIPLY', hz, 0.35), clamp=True)
    bk = n.mix(c('bk_shade'), c('bk_mid'), n.maprange(bw, 0.2, 0.55))
    bk = n.mix(bk, c('bk_lit'), n.maprange(bw, 0.55, 0.9))
    bk = n.mix(bk, n.gray(n.maprange(rnd, 0, 1, 0.9, 1.08)), 1.0, 'MULTIPLY')
    bk = n.mix(bk, c('bk_groove'), n.math('MULTIPLY', n.maprange(grain, 0.52, 0.66), 0.65))
    bk = n.mix(bk, c('bk_lit'), n.math('MULTIPLY', n.maprange(grain, 0.4, 0.3), 0.35))
    rim = n.math('MULTIPLY', n.maprange(nz, 0.45, 0.85), n.math('ADD', 0.35, n.math('MULTIPLY', hl, 0.65)))
    bk = n.mix(bk, c('bk_rim'), n.math('MULTIPLY', n.math('MULTIPLY', rim, n.maprange(hl, 0.0, 0.6, 0.3, 1.0)), 0.4))
    bk = n.mix(bk, c('bk_under'), n.maprange(nz, -0.2, -0.8, 0.0, 0.55))
    bk = n.mix(bk, c('bk_groove'), n.maprange(aof, 0.85, 0.3, 0.0, 0.75))
    bk_m = n.mix(bk, mcol, n.math('MULTIPLY', moss_up, 0.55))
    # driftwood (bleached, pale)
    dr = n.mix(c('dr_shade'), c('dr_mid'), n.maprange(bw, 0.15, 0.5))
    dr = n.mix(dr, c('dr_lit'), n.maprange(bw, 0.5, 0.9))
    dr = n.mix(dr, c('#7A6E60'), n.math('MULTIPLY', n.maprange(grain, 0.5, 0.66), 0.6))
    dr = n.mix(dr, c('crevice'), n.maprange(aof, 0.85, 0.3, 0.0, 0.6))
    # stiltwood bark (scenery: paler grey-brown, long ridges, moss on top faces)
    sb = n.mix(c('sb_shade'), c('sb_mid'), n.maprange(bw, 0.15, 0.5))
    sb = n.mix(sb, c('sb_lit'), n.maprange(bw, 0.5, 0.85))
    sb = n.mix(sb, c('sb_top'), n.maprange(nz, 0.6, 0.95, 0.0, 0.5))
    sb = n.mix(sb, n.gray(n.maprange(rnd, 0, 1, 0.92, 1.06)), 1.0, 'MULTIPLY')
    sb = n.mix(sb, c('sb_groove'), n.math('MULTIPLY', n.maprange(grain, 0.5, 0.68), 0.7))
    sb = n.mix(sb, c('sb_top'), n.math('MULTIPLY', n.maprange(grain, 0.38, 0.27), 0.4))
    sb = n.mix(sb, c('#4A5652'), n.maprange(nz, -0.2, -0.8, 0.0, 0.45))
    sb = n.mix(sb, c('crevice'), n.maprange(aof, 0.85, 0.3, 0.0, 0.7))
    sbm = n.maprange(n.math('MULTIPLY', n.maprange(nz, 0.55, 0.85),
                            n.maprange(n.noise(P, 1 / 2.0, 2, 0.45), 0.42, 0.6)), 0.2, 0.6)      # moss on tops only
    sb = n.mix(sb, mcol, sbm)
    # end grain caps: rings
    rr = n.node('ShaderNodeVectorMath', operation='LENGTH'); n.link(bcv, rr.inputs[0])
    rings = n.math('SINE', n.math('MULTIPLY', rr.outputs['Value'], 55.0))
    eg = n.mix(c('end_a'), c('end_b'), n.maprange(rings, -0.3, 0.6))
    eg = n.mix(eg, c('bk_mid'), n.maprange(tl, 0.8, 0.98))
    # vine / rope
    vn = n.mix(c('vine_dark'), c('vine_mid'), n.maprange(bw, 0.2, 0.55))
    vn = n.mix(vn, c('vine_lit'), n.maprange(bw, 0.6, 0.95))
    vn = n.mix(vn, c('vine_dark'), n.math('MULTIPLY', n.maprange(grain, 0.5, 0.64), 0.6))
    # thorns, crimson tips, warning blooms
    th = n.mix(c('th_stem'), c('th_lit'), n.maprange(up, 0.4, 0.95))
    tt = n.mix(th, c('crimson'), n.maprange(tl, 0.15, 0.55))
    tt = n.mix(tt, c('crimson_lit'), n.maprange(tl, 0.8, 1.0, 0.0, 0.7))
    bl = n.mix(c('crimson'), c('crimson_lit'), n.maprange(up, 0.4, 1.0))
    bl = n.mix(bl, c('#E0A040'), n.maprange(tl, 0.12, 0.0))
    # earth (gap faces, banks): trail swatch on top faces
    tr = box_image(n, os.path.join(FPAINT, 'swatch_trail_tile.png'), P, 4.0)
    ea = n.mix(c('ea_deep'), c('ea_side'), n.maprange(n.math('ADD', hz, n.math('MULTIPLY', up, 0.4)), 0.2, 0.75))
    ea = n.mix(ea, c('ea_lit'), n.maprange(nz, 0.3, 0.7, 0.0, 0.6))
    ea = n.mix(ea, tr.outputs['Color'], n.maprange(nz, 0.7, 0.92, 0.0, 0.9))
    ea = n.mix(ea, c('#4E3E1C'), n.math('MULTIPLY', n.maprange(n.noise(P, 1 / 0.8, 2, 0.5), 0.55, 0.7), 0.5))
    ea = n.mix(ea, c('crevice'), n.maprange(aof, 0.85, 0.3, 0.0, 0.7))
    # foliage
    vo = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='F1')
    vo.inputs['Scale'].default_value = 4.0
    n.link(P, vo.inputs['Vector'])
    vsep = n.node('ShaderNodeSeparateColor'); n.link(vo.outputs['Color'], vsep.inputs[0])
    vd = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='DISTANCE_TO_EDGE')
    vd.inputs['Scale'].default_value = 4.0
    n.link(P, vd.inputs['Vector'])
    lf = n.math('ADD', n.math('MULTIPLY', tl, 0.55), n.math('MULTIPLY', up, 0.45))
    lf = n.math('ADD', lf, n.maprange(vsep.outputs[0], 0, 1, -0.12, 0.12))
    fc = n.mix(c('leaf_core'), c('leaf_mid'), n.maprange(lf, 0.15, 0.55))
    fc = n.mix(fc, c('leaf_tip'), n.maprange(lf, 0.55, 1.0, 0.0, 0.85))
    fc = n.mix(fc, c('leaf_core'), n.math('MULTIPLY', n.maprange(vd.outputs['Distance'], 0.0, 0.2, 0.3, 0.0),
                                          n.maprange(lf, 0.2, 0.7)))
    fc = n.mix(fc, c('leaf_core'), n.maprange(aof, 0.7, 0.2, 0.0, 0.45))
    # metals, gems, cloth
    au = n.mix(c('gold_dark'), c('gold'), n.maprange(n.math('ADD', up, hl), 0.2, 0.9))
    au = n.mix(au, c('gold_hi'), n.math('MULTIPLY', hl, 0.6))
    au = n.mix(au, c('gold_dark'), n.maprange(aof, 0.9, 0.5, 0.0, 0.6))
    gm_ = n.mix(c('gem'), c('gem_hi'), n.maprange(tl, 0.6, 1.0, 0.0, 0.7))
    cr = n.mix(c('cry_deep'), c('cry'), n.maprange(n.math('ADD', tl, n.math('MULTIPLY', up, 0.3)), 0.0, 0.7))
    cr = n.mix(cr, c('cry_hi'), n.maprange(n.math('ADD', tl, n.math('MULTIPLY', hl, 0.2)), 0.8, 1.15, 0.0, 0.8))
    br = n.mix(c('brass'), c('brass_hi'), n.maprange(n.math('ADD', up, hl), 0.3, 1.2))
    br = n.mix(br, c('crevice'), n.maprange(aof, 0.9, 0.5, 0.0, 0.5))
    cl = n.mix(c('cloth_sh'), c('cloth'), n.maprange(up, 0.2, 0.8))
    te = n.mix(c('teal'), c('teal_hi'), n.maprange(n.math('ADD', up, hl), 0.3, 1.2))
    te = n.mix(te, c('#0F4F4C'), n.maprange(aof, 0.9, 0.5, 0.0, 0.5))
    crm = n.mix(c('cloth_sh'), c('cream'), n.maprange(n.math('ADD', up, hl), 0.2, 1.0))

    layers = [(1, mcol), (2, fc), (3, fc), (4, bk_m), (5, dr), (6, th), (7, tt), (8, ea), (9, au), (10, gm_),
              (11, cr), (12, br), (13, vn), (14, sb), (15, wt), (16, eg), (17, cl), (18, bl), (19, te), (20, crm),
              (21, c('dark'))]
    col = st_m
    for k, cc in layers:
        col = n.mix(col, cc, sel(k))
    # wet band on everything in water (driftwood, floating logs, snags): darker, teal-shifted below the waterline
    wetf = n.math('MULTIPLY', water, n.maprange(sepp.outputs[2], 0.06, -0.08))
    col = n.mix(col, n.mix(col, c('#20302C'), 0.55), wetf)

    rough = {0: 0.85, 1: 0.9, 2: 0.6, 3: 0.6, 4: 0.85, 5: 0.8, 6: 0.7, 7: 0.55, 8: 0.92, 9: 0.32, 10: 0.2, 11: 0.12,
             12: 0.38, 13: 0.75, 14: 0.85, 15: 0.38, 16: 0.8, 17: 0.9, 18: 0.55, 19: 0.45, 20: 0.6, 21: 0.9}
    rv = 0.85
    for k, v in rough.items():
        if k:
            rv = n.math('ADD', n.math('MULTIPLY', rv, n.math('SUBTRACT', 1.0, sel(k))), n.math('MULTIPLY', sel(k), v))
    rv = n.math('ADD', n.math('MULTIPLY', rv, n.math('SUBTRACT', 1.0, n.math('MULTIPLY', wetf, 0.6))),
                n.math('MULTIPLY', n.math('MULTIPLY', wetf, 0.6), 0.3))
    metal = n.math('ADD', sel(9), sel(12), clamp=True)
    if mode == 'color':
        n.link(col, bs.inputs['Base Color'])
        hb = n.math('ADD', n.math('MULTIPLY', rlum.outputs[0], 0.08), n.math('MULTIPLY', mlum.outputs[0],
                                                                             n.math('ADD', sel(1), moss_up)))
        hb = n.math('ADD', hb, n.math('MULTIPLY', n.math('MULTIPLY', vd.outputs['Distance'],
                                                         n.math('ADD', sel(2), sel(3))), 1.0))
        hb = n.math('ADD', hb, n.math('MULTIPLY', grain, n.math('MULTIPLY', n.math('ADD', n.math('ADD', sel(4), sel(14)),
                                                                                     n.math('ADD', sel(5), sel(13))), 0.5)))
        bump = n.node('ShaderNodeBump', in_0=0.5, in_1=0.05)
        n.link(hb, bump.inputs['Height'])
        n.link(bump.outputs[0], bs.inputs['Normal'])
    elif mode == 'rough':
        n.link(n.gray(rv), bs.inputs['Base Color'])
    else:
        n.link(n.gray(metal), bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value = 0.8
    bs.inputs['Metallic'].default_value = 0.0
    return m


# ------------------------------------------------------------------ helpers
def set_hz(ob, z0=None, z1=None):
    me = ob.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    z = co.reshape(-1, 3)[:, 2]
    lo, hi = (z.min() if z0 is None else z0), (z.max() if z1 is None else z1)
    a = me.attributes.get('hz') or me.attributes.new('hz', 'FLOAT', 'POINT')
    a.data.foreach_set('value', np.clip((z - lo) / max(hi - lo, 1e-3), 0, 1).astype(np.float32))


def verts(ob):
    co = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get('co', co)
    return co.reshape(-1, 3)


def unity_box(V):
    """Blender verts -> Unity-local AABB dict (x = -bx, y = bz, z = -by)."""
    U = np.stack([-V[:, 0], V[:, 2], -V[:, 1]], -1)
    return U.min(0), U.max(0)


def measure(ob, hit):
    """Check the visual against the intended gameplay box (Unity local metres, pivot = (0,0,0)).
    hit: dict(kind, x=(x0,x1), z=(z0,z1), y=(y0,y1)). Returns a short text for the report."""
    V = verts(ob)
    U = np.stack([-V[:, 0], V[:, 2], -V[:, 1]], -1)
    x0, x1 = hit.get('x', (-99, 99))
    z0, z1 = hit.get('z', (-99, 99))
    inside = (U[:, 0] >= x0) & (U[:, 0] <= x1) & (U[:, 2] >= z0) & (U[:, 2] <= z1)
    k = hit['kind']
    if k in ('low', 'thorn', 'walk', 'float', 'snag'):
        top = U[inside, 1].max() if inside.any() else float('nan')
        return f'top {top:.2f} m in box (intended {hit["y"][1]:.2f})'
    if k in ('high', 'lowbranch'):
        sel_ = inside & (U[:, 1] < hit['y'][0] + 0.5)
        bot = U[inside, 1].min() if inside.any() else float('nan')
        return f'lowest {bot:.2f} m over the span (intended bottom {hit["y"][0]:.2f})' + ('' if sel_.any() else '')
    if k in ('block', 'rock'):
        band = (U[:, 1] > 0.15) & (U[:, 1] < 1.8)
        return (f'width {U[band, 0].max() - U[band, 0].min():.2f} m at 0.15-1.8 m, x {U[band, 0].min():.2f}..'
                f'{U[band, 0].max():.2f}, height {U[:, 1].max():.2f}')
    return ''


def fit_piece(ob, pc):
    """fit_w: centre the 0.15-1.8 m band on x = 0 and scale x so its width is exactly fit_w (blockers).
    align_front: move the piece along y so the front face (Blender max y of the part inside |x| <= 3.5, z > 0.05)
    sits at y = 0 (Unity z = 0 = the obstacle's authored front face)."""
    me = ob.data
    if pc.get('clamp_z') is not None:          # gap lips: the top must be the floor plane exactly
        V = verts(ob)
        V[:, 2] = np.minimum(V[:, 2], pc['clamp_z'])
        me.vertices.foreach_set('co', V.ravel())
        me.update()
    if pc.get('fit_w'):
        V = verts(ob)
        band = (V[:, 2] > 0.15) & (V[:, 2] < 1.8)
        x0, x1 = V[band, 0].min(), V[band, 0].max()
        me.transform(Matrix.Translation((-(x0 + x1) / 2, 0, 0)))
        me.transform(Matrix.Diagonal((pc['fit_w'] / (x1 - x0), 1, 1, 1)))
    if pc.get('align_front'):
        V = verts(ob)
        inner = (np.abs(V[:, 0]) <= pc.get('front_x', 3.5)) & (V[:, 2] > 0.05)
        me.transform(Matrix.Translation((0, -V[inner, 1].max(), 0)))


def drop_small(ob, diag):
    """Delete connected components whose bounding-box diagonal is below diag (m)."""
    if diag <= 0:
        return 0
    bm = bmesh.new()
    bm.from_mesh(ob.data)
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
        co = np.array([v.co[:] for v in comp])
        if np.linalg.norm(co.max(0) - co.min(0)) < diag:
            dead.extend(comp)
    bmesh.ops.delete(bm, geom=dead, context='VERTS')
    bm.to_mesh(ob.data)
    bm.free()
    return len(dead)


def keep_faces(ob, attr, value):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    lay = bm.faces.layers.int.get(attr)
    dead = [f for f in bm.faces if f[lay] != value]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(ob.data)
    bm.free()


def bake_self(ob, kind, img, samples, **kw):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    E.bake_target(ob, img)
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.margin = 6
    sc.render.bake.margin_type = 'EXTEND'
    E.set_active(ob)
    a = dict(type=kind, use_clear=True, margin=6)
    a.update(kw)
    bpy.ops.object.bake(**a)


def swap_mat(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)


# ------------------------------------------------------------------ the shared-atlas bake
def bake_set(set_name, pieces, res, mesh_dir, tex_dir, spacing=40.0, cols=6, samples=32, blend_name=None):
    """pieces: list of dict(name, fn(acc, nz, rng) [, water, lod=(t0,t1,t2), drop=(d1,d2), w, ground (z or None),
    cut (z or None), cull, seed, hit, empties {suffix: (x,y,z) Blender}, keep_attrs]).
    Writes <mesh_dir>/<name>.fbx (+ _LOD0..2 meshes, empties) and <tex_dir>/<set_name>_{BaseColor,Normal,ARM}.png."""
    E.reset()
    sc = E.gpu_cycles(8)
    built = []
    for i, pc in enumerate(pieces):
        acc = KAcc(water=pc.get('water', False))
        nz = E.Noise(pc.get('seed', 7 + 13 * i))
        rng = np.random.default_rng(pc.get('seed', 101 + i))
        pc['fn'](acc, nz, rng)
        ob = acc.object(pc['name'] + '_LOD0')
        fit_piece(ob, pc)
        if pc.get('cut') is not None:
            E.cut_below(ob, pc['cut'])
        E.triangulate(ob)
        raw = E.tris(ob)
        culled = cull_hidden(ob) if pc.get('cull', True) else 0
        E.decimate_to(ob, pc['lod'][0])
        set_hz(ob)
        me = ob.data
        fa = me.attributes.new('piece', 'INT', 'FACE')
        fa.data.foreach_set('value', np.full(len(me.polygons), i, np.int32))
        off = Vector(((i % cols) * spacing, (i // cols) * spacing, 0))
        me.transform(Matrix.Translation(off))
        print(f'{pc["name"]}: built {raw} tris, culled {culled} faces, LOD0 {E.tris(ob)}')
        built.append((pc, ob, off))
    # join
    objs = [b[1] for b in built]
    E.set_active(objs[0], *objs[1:])
    bpy.ops.object.join()
    J = objs[0]
    J.name = set_name + '_bake'
    me = J.data
    E.triangulate(J)
    me.normals_split_custom_set_from_vertices([v.normal[:] for v in me.vertices])
    op = openness(J, dirs=12, dist=2.0)
    pidx = np.empty(len(me.polygons), np.int32)
    me.attributes['piece'].data.foreach_get('value', pidx)
    wts = []
    for p in me.polygons:
        pc = pieces[pidx[p.index]]
        w = pc.get('w', 1.0)
        if pc.get('directional', True):
            w *= 0.55 if p.normal.z < -0.5 else (0.8 if p.normal.y < -0.5 else 1.0)
        w *= 0.45 + 0.55 * min(1.0, op[p.index] / 0.7)
        if pc.get('wz'):
            w *= pc['wz'](p.center.z)
        wts.append(round(w, 1))
    me.uv_layers.new(name='UVMap')
    dens = E.xatlas_faces(J, list(range(len(me.polygons))), res, padding_px=4, weights=wts)
    grounds = []
    for pc, _, off in built:
        if pc.get('ground', 0.0) is not None:
            bpy.ops.mesh.primitive_plane_add(size=min(spacing - 4, 30), location=off + Vector((0, 0, pc.get('ground', 0.0) - 0.01)))
            grounds.append(bpy.context.active_object)
    sc.world = bpy.data.worlds.new('w')
    base = E.new_image(set_name + '_BaseColor', res)
    swap_mat(J, kit_material('color'))
    bake_self(J, 'DIFFUSE', base, samples, pass_filter={'COLOR'})
    nrm = E.new_image(set_name + '_Normal', res, non_color=True)
    bake_self(J, 'NORMAL', nrm, 4, normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z')
    rgh = E.new_image(set_name + '_rough', res, non_color=True)
    swap_mat(J, kit_material('rough'))
    bake_self(J, 'DIFFUSE', rgh, 2, pass_filter={'COLOR'})
    met = E.new_image(set_name + '_metal', res, non_color=True)
    swap_mat(J, kit_material('metal'))
    bake_self(J, 'DIFFUSE', met, 2, pass_filter={'COLOR'})
    aom = bpy.data.materials.new('ao_src')
    aom.use_nodes = True
    aon = aom.node_tree.nodes.new('ShaderNodeAmbientOcclusion')
    aon.samples, aon.only_local = 32, False
    aon.inputs['Distance'].default_value = 1.0
    aom.node_tree.links.new(aon.outputs['AO'], aom.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    swap_mat(J, aom)
    ao = E.new_image(set_name + '_ao', res, non_color=True)
    bake_self(J, 'DIFFUSE', ao, 64, pass_filter={'COLOR'})
    for g in grounds:
        bpy.data.objects.remove(g)
    arm = E.new_image(set_name + '_ARM', res, non_color=True)
    o = np.zeros((res, res, 4), np.float32)
    o[..., 0] = np.clip(E.pixels(ao)[..., 0], 0.35, 1) ** 1.1
    o[..., 1] = E.pixels(rgh)[..., 0]
    o[..., 2] = (E.pixels(met)[..., 0] > 0.5).astype(np.float32)
    o[..., 3] = 1
    E.set_pixels(arm, o)
    E.save_png(base, os.path.join(tex_dir, set_name + '_BaseColor.png'))
    E.save_png(nrm, os.path.join(tex_dir, set_name + '_Normal.png'))
    E.save_png(arm, os.path.join(tex_dir, set_name + '_ARM.png'))
    mt = bpy.data.materials.new('MI_' + set_name)
    mt.use_nodes = True
    preview_mat(mt, base, nrm, arm)
    spn = [x for x in mt.node_tree.nodes if x.type == 'SEPARATE_COLOR'][0]
    mt.node_tree.links.new(spn.outputs[2], mt.node_tree.nodes['Principled BSDF'].inputs['Metallic'])
    swap_mat(J, mt)
    report = []
    for i, (pc, _, off) in enumerate(built):
        lo0 = E.copy_obj(J, pc['name'] + '_LOD0')
        keep_faces(lo0, 'piece', i)
        lo0.data.transform(Matrix.Translation(-off))
        lods = [lo0]
        for L in (1, 2):
            lo = E.copy_obj(lo0, f'{pc["name"]}_LOD{L}')
            drop_small(lo, pc.get('drop', (0.0, 0.0))[L - 1])
            E.decimate_to(lo, pc['lod'][L])
            lods.append(lo)
        hitinfo = measure(lo0, pc['hit']) if pc.get('hit') else ''
        for lo in lods:
            for an in [a.name for a in lo.data.attributes if a.name in ('pp', 'mk', 'bc', 'hz', 'piece')]:
                lo.data.attributes.remove(lo.data.attributes[an])
            for an in [x.name for x in lo.data.color_attributes]:
                lo.data.color_attributes.remove(lo.data.color_attributes[an])
            lo.data.normals_split_custom_set_from_vertices([v.normal[:] for v in lo.data.vertices])
        exp = list(lods)
        from bakekit import empty
        for suf, loc in pc.get('empties', {}).items():
            exp.append(empty(f'{pc["name"]}_{suf}', loc))
        if pc.get('post'):
            exp += pc['post'](pc)
        E.export_fbx(exp, os.path.join(mesh_dir, pc['name'] + '.fbx'))
        lo_, hi_ = unity_box(verts(lo0))
        size = hi_ - lo_
        row = dict(name=pc['name'], size=tuple(round(float(x), 2) for x in size), lo=tuple(round(float(x), 2) for x in lo_),
                   hi=tuple(round(float(x), 2) for x in hi_), tris=[E.tris(x) for x in lods], hit=hitinfo)
        report.append(row)
        print('SUMMARY', row)
    bpy.data.objects.remove(J)
    for im in (base, nrm, arm):
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('forest', (blend_name or set_name) + '_game.blend'), compress=True)
    print(f'SET {set_name}: {res}px, {dens:.1f} px/m at weight 1')
    import json
    with open(E.work('forest', set_name + '_report.json'), 'w') as f:
        json.dump(dict(set=set_name, res=res, density=round(dens, 1), pieces=report), f, indent=1)
    return report
