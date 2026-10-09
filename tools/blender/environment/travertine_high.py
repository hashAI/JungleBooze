"""Travertine pool tiers v2 (F4_e): natural, irregular limestone terraces stepping down toward the camera, with
curved lobed lips, uneven drop heights, raised wet rims, mossy crests and notches where 4-6 small cascades spill.

Replaces the round saucer pools. Built as a heightfield from a U-shaped potential field: each lip is a contour of
the field plus its own lobes and noise, so the lips are nested, convex downstream and never parallel. Each drop has a
bullnose profile (rounded top, near-vertical face), each pool a raised rim crest behind its lip and a dipped floor.
The sheet is solidified, voxel remeshed and weathered (bulging lips, drip fluting on the drops, facets, cracks).

Axes: downstream (where the cascades fall) is Blender -Y = Unity local +Z. Turn the piece so +Z faces the camera.
z = 0 is the apron at the foot of the front drop (put the main basin water at about z = 0.3).

Anchors (empties, read by EnvironmentKit as "<suffix after the last underscore>"):
  RS_TravertineTiers_Pool<t>          tier t water surface centre (t = 0 top/back .. 3 front); its z is the water level
  RS_TravertineTiers_Lip<i>P<jj>      lip i polyline, left to right (Blender -X to +X), on the lip edge at crest height
  RS_TravertineTiers_Cascade<k>L / R  left / right end of cascade k's spill notch on the lip edge, at its water level
  RS_TravertineTiers_Cascade<k>F      cascade k's foot: where it lands on the next water level down
Usage: blender ... -P travertine_high.py [-- --quick]      (--quick: heightfield + clay only, for layout checks)
Then bake: blender ... -P travertine_bake.py
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_high import rockify  # noqa: E402

NAME = 'RS_TravertineTiers'
SEED = 71
B_CURVE, W_CURVE = 4.5, 26.0          # U-shape of the field: lips bulge downstream by up to B_CURVE m
LIP_Y = [5.0, 0.6, -3.9, -8.3]        # lip positions at x = 0 (downstream = -Y)
LEVEL = [6.2, 4.35, 2.75, 1.25]       # pool floor reference level per tier (z); the front drop falls to 0
RIM_H = 0.36                          # rim crest above the tier level
DIP = 0.85                             # pool floor dip behind the rim
NOTCH_D = 0.3                         # notch depth below the crest
# cascades: (lip, x centre, width)
NOTCHES = [(0, -1.2, 3.6), (1, 0.6, 4.0), (1, 7.0, 2.2), (2, -1.8, 3.2), (3, 1.0, 4.6), (3, -8.0, 2.6)]
FOOT = dict(x=(-15.0, 15.0), y=(-13.0, 11.5), centre=(0.0, -1.0), radii=(14.5, 12.8))
GRID = 0.1


class Field:
    def __init__(self, seed):
        self.nz = E.Noise(seed)
        rng = np.random.default_rng(seed)
        self.lobes = []
        for i in range(len(LIP_Y)):
            lo, x = [], -15.0
            while x < 15.0:                                   # scallops: convex bulges downstream
                lam = rng.uniform(2.6, 5.5)
                lo.append((x + lam / 2, rng.uniform(0.3, 1.6), lam * 0.42))
                x += lam
            self.lobes.append(lo)
        self.S = [-y - B_CURVE for y in LIP_Y]

    def s(self, x, y):
        return -y - B_CURVE * (1 - (2 * x / W_CURVE) ** 2) + 0.5 * self.nz.n3(np.stack([x / 7, y / 7, 0 * x + 3.3], -1))

    def lip_off(self, i, x):
        o = 1.3 * self.nz.fbm1(x / 9.0 + 13.7 * i) + 0.4 * self.nz.fbm1(x / 3.0 + 5.1 * i)
        for c, a, w in self.lobes[i]:
            o = o + a * np.exp(-((x - c) / w) ** 2)
        return o

    def e(self, i, x, y):
        """Signed distance-like value to lip i: > 0 downstream of it."""
        return self.s(x, y) - self.S[i] - self.lip_off(i, x)

    def notch(self, i, x):
        """0..1, 1 inside a spill notch of lip i (soft edges)."""
        out = np.zeros_like(x)
        for li, c, w in NOTCHES:
            if li == i:
                out = np.maximum(out, np.clip((w / 2 + 0.35 - np.abs(x - c)) / 0.7, 0, 1))
        return out


def heights(F, X, Y):
    nl = len(LIP_Y)
    E_ = np.stack([F.e(i, X, Y) for i in range(nl)])            # (nl, ...)
    t = np.sum(E_ > 0, axis=0)                                   # tier index; nl = apron below the front lip
    lev = np.array(LEVEL + [0.0])
    H = np.zeros_like(X)
    for k in range(nl + 1):
        m = t == k
        if not m.any():
            continue
        h = np.full(X[m].shape, lev[k])
        if k < nl:                                                # pool: rim crest behind its own front lip, dipped floor
            d = -E_[k][m]
            notch = F.notch(k, X[m])
            rim = RIM_H * (1 - notch) + (RIM_H - NOTCH_D) * notch
            wide = 0.6 + 0.5 * (0.5 + 0.5 * F.nz.n3(np.stack([X[m] / 3, Y[m] / 3, 0 * X[m] + 1.7], -1)))  # rim width varies
            prof = np.exp(-(np.maximum(d - 0.25, 0) / wide) ** 4)                                # flat-topped crest
            h = h + rim * prof - DIP * np.clip((d - 0.6) / 1.8, 0, 1) ** 1.5 * (1 - prof)
            h = h + 0.06 * F.nz.n3(np.stack([X[m] * 0.8, Y[m] * 0.8, 0 * X[m] + 7.1], -1))
        else:
            h = h - 0.15 * np.clip(E_[nl - 1][m] / 3, 0, 1)
        if k > 0:                                                 # drop of the lip just upstream: bullnose profile
            i = k - 1
            e = E_[i][m]
            notch = F.notch(i, X[m])
            top = LEVEL[i] + RIM_H * (1 - notch) + (RIM_H - NOTCH_D) * notch
            drop = top - lev[k]
            wd = 0.3 + 0.18 * drop
            x01 = np.clip(e / wd, 0, 1)
            f = 1 - np.sqrt(np.clip(1 - x01 ** 2, 0, 1))
            hd = top - drop * f
            h = np.where(e < wd, np.maximum(h, hd), h)
        H[m] = h
    # footprint: irregular rounded outline; outside, the apron falls away under the terrain
    cx, cy = FOOT['centre']
    rx, ry = FOOT['radii']
    r = np.sqrt(((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2) + 0.07 * F.nz.fbm1(np.arctan2(Y - cy, X - cx) * 3 + 5)
    H = np.where(r > 1, np.minimum(H, -0.6 - (r - 1) * 4), H - np.clip((r - 0.93) / 0.07, 0, 1) * 0.4 * (H > 0.2))
    return H, t, E_


def heightfield_mesh(F):
    xs = np.arange(FOOT['x'][0], FOOT['x'][1] + 1e-6, GRID)
    ys = np.arange(FOOT['y'][0], FOOT['y'][1] + 1e-6, GRID)
    X, Y = np.meshgrid(xs, ys)
    H, _, _ = heights(F, X, Y)
    ny, nx = X.shape
    V = np.stack([X.ravel(), Y.ravel(), H.ravel()], 1)
    idx = np.arange(nx * ny).reshape(ny, nx)
    a, b, c, d = idx[:-1, :-1].ravel(), idx[:-1, 1:].ravel(), idx[1:, 1:].ravel(), idx[1:, :-1].ravel()
    Fc = np.stack([a, b, c, d], 1)
    me = bpy.data.meshes.new(NAME + '_high')
    me.vertices.add(len(V)); me.vertices.foreach_set('co', V.ravel().astype(np.float32))
    me.loops.add(Fc.size); me.loops.foreach_set('vertex_index', Fc.ravel().astype(np.int32))
    me.polygons.add(len(Fc))
    me.polygons.foreach_set('loop_start', np.arange(0, Fc.size, 4).astype(np.int32))
    me.update(); me.validate()
    ob = bpy.data.objects.new(NAME + '_high', me)
    E.link(ob)
    return ob


def boulders(F, rng):
    """Mossy rock islands in the pools and plunge stones at the cascade feet (keyframe)."""
    from ledges_high import blob
    out = []
    for k, (li, c, w) in enumerate(NOTCHES):          # stones in the splash zone, either side of each fall
        for side in (-1, 1):
            if rng.random() < 0.6:
                x = c + side * (w / 2 + rng.uniform(-0.3, 0.6))
                y = -(F.S[li] + F.lip_off(li, np.array([x]))[0] + B_CURVE * (1 - (2 * x / W_CURVE) ** 2)) - rng.uniform(0.9, 1.6)
                z = LEVEL[li + 1] if li + 1 < len(LEVEL) else 0.0
                r = rng.uniform(0.45, 0.85)
                out.append(blob((x, y, z), (r * 1.3, r, r * 0.75), rng, SEED * 100 + k * 2 + (side > 0)))
    for i in range(len(LEVEL)):                         # rock blocks along the lips (chunky mossy ledges), not in notches
        xs = np.linspace(-13, 13, 27)
        for x in xs + rng.uniform(-0.4, 0.4, len(xs)):
            if rng.random() > 0.42 or F.notch(i, np.array([x]))[0] > 0 or any(abs(x - c) < w / 2 + 0.9 for li, c, w in NOTCHES if li == i):
                continue
            xa = np.array([x])
            y = np.array([-(F.S[i] + F.lip_off(i, xa)[0] + B_CURVE * (1 - (2 * x / W_CURVE) ** 2))])
            for _ in range(6):
                y = y + F.e(i, xa, y)
            if np.hypot((x - FOOT['centre'][0]) / FOOT['radii'][0], (y[0] - FOOT['centre'][1]) / FOOT['radii'][1]) > 0.9:
                continue
            r = rng.uniform(0.55, 1.1)
            z = LEVEL[i] + RIM_H * rng.uniform(0.2, 1.1)
            out.append(blob((x, float(y[0]) + r * 0.45, z), (r * rng.uniform(1.2, 1.9), r * 0.95, r * 0.62), rng,
                            SEED * 500 + i * 40 + int((x + 15) * 1.3)))
    for t in range(len(LEVEL)):                         # islands inside the pools
        for j in range(2 if t < 3 else 3):
            for _ in range(40):
                x, y = rng.uniform(-11, 11), rng.uniform(-11, 10)
                e = [float(F.e(i, np.array([x]), np.array([y]))[0]) for i in range(len(LEVEL))]
                tt = sum(v > 0 for v in e)
                if tt == t and -e[t] > 2.0 and (t == 0 or e[t - 1] > 1.2):
                    r = rng.uniform(0.6, 1.3)
                    out.append(blob((x, y, LEVEL[t] - 0.2), (r * 1.4, r, r * 0.55 + 0.25), rng, SEED * 300 + t * 7 + j))
                    break
    return out


def lip_bulge_and_flute(ob, F):
    """Lips bulge out a little over their drops (rounded travertine lips) and the drops get vertical drip fluting."""
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    Nv = np.empty(n * 3); me.vertex_normals.foreach_get('vector', Nv); Nv = Nv.reshape(-1, 3)
    steep = np.clip((0.55 - np.abs(Nv[:, 2])) / 0.4, 0, 1)
    _, t, E_ = heights(F, P[:, 0], P[:, 1])
    bul = np.zeros(n)
    for i in range(len(LEVEL)):
        e = E_[i]
        top = LEVEL[i] + RIM_H
        near = np.exp(-(e / 0.9) ** 2) * np.clip(1 - (top - P[:, 2]) / 0.7, 0, 1)
        bul = np.maximum(bul, near)
    q = np.stack([P[:, 0] * 2.6, P[:, 1] * 2.6, P[:, 2] * 0.22], 1)
    flute = F.nz.n3(q) * 0.65 + F.nz.n3(q * 2.3 + 9) * 0.35
    P = P + Nv * (0.16 * bul * steep + 0.09 * flute * steep)[:, None]
    me.vertices.foreach_set('co', P.ravel())
    me.update()


def attributes(ob, F):
    """'trav' float colour: R = wet (drops, lip faces and water lines), G = rim crest, B = under water, A = random."""
    me = ob.data
    n = len(me.vertices)
    P = np.empty(n * 3); me.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
    Nv = np.empty(n * 3); me.vertex_normals.foreach_get('vector', Nv); Nv = Nv.reshape(-1, 3)
    _, t, E_ = heights(F, P[:, 0], P[:, 1])
    water = np.array(water_levels() + [0.3])
    wl = water[t]
    under = np.clip((wl - P[:, 2]) / 0.08, 0, 1)
    wet = np.clip(1 - np.abs(P[:, 2] - wl) / 0.25, 0, 1) * 0.8           # water line band
    steep = np.clip((0.7 - np.abs(Nv[:, 2])) / 0.5, 0, 1)
    rim = np.zeros(n)
    for i in range(len(LEVEL)):
        e = E_[i]
        lipzone = (e > -0.2) & (e < 1.4)
        notch = F.notch(i, P[:, 0])
        wet = np.maximum(wet, lipzone * steep * (0.55 + 0.45 * notch))       # every drop is damp, cascades soaked
        wet = np.maximum(wet, (np.abs(e) < 0.5) * notch)
        rim = np.maximum(rim, np.exp(-((e + 0.35) / 0.45) ** 2) * (1 - notch) * (Nv[:, 2] > 0.3))
    col = np.zeros((n, 4), np.float32)
    col[:, 0] = np.clip(wet, 0, 1)
    col[:, 1] = rim
    col[:, 2] = under
    col[:, 3] = np.random.default_rng(SEED).random()
    ca = me.color_attributes.new('trav', 'FLOAT_COLOR', 'POINT')
    ca.data.foreach_set('color', col.ravel())


def anchors(F):
    from bakekit import empty
    objs = []
    water = water_levels() + [0.3]
    xs = np.linspace(-14.5, 14.5, 581)
    for i in range(len(LEVEL)):
        y = -(F.S[i] + F.lip_off(i, xs) + B_CURVE * (1 - (2 * xs / W_CURVE) ** 2))
        for _ in range(6):                  # Newton on the noisy field (de/dy is about -1): lip edge where e = 0
            y = y + F.e(i, xs, y)
        H, t, _ = heights(F, xs, y + 0.05)
        inside = H > LEVEL[i] - 0.05                                # where the lip really exists (inside the outline)
        ids = np.where(inside)[0]
        if len(ids) < 2:
            continue
        pick = np.linspace(ids.min(), ids.max(), 14).astype(int)
        for j, q in enumerate(pick):
            objs.append(empty(f'{NAME}_Lip{i}P{j:02d}', (xs[q], y[q], float(H[q]))))
    for t_ in range(len(LEVEL)):
        X, Y = np.meshgrid(np.arange(-14, 14, 0.5), np.arange(-12, 11, 0.5))
        _, tt, E_ = heights(F, X, Y)
        m = (tt == t_) & (-E_[t_] > 1.0)
        objs.append(empty(f'{NAME}_Pool{t_}', (float(X[m].mean()), float(Y[m].mean()), water[t_])))
    for k, (li, c, w) in enumerate(NOTCHES):
        for tag, x in (('L', c - w / 2), ('R', c + w / 2), ('F', c)):
            xa = np.array([x])
            y = np.array([-(F.S[li] + F.lip_off(li, xa)[0] + B_CURVE * (1 - (2 * x / W_CURVE) ** 2))])
            for _ in range(6):
                y = y + F.e(li, xa, y)
            if tag == 'F':
                drop = water[li] - water[li + 1]
                objs.append(empty(f'{NAME}_Cascade{k}{tag}', (x, float(y[0]) - (0.45 + 0.22 * drop), water[li + 1])))
            else:
                objs.append(empty(f'{NAME}_Cascade{k}{tag}', (x, float(y[0]), water[li])))
    return objs


def water_levels():
    """Water surface z per tier: the notch crest plus a 7 cm spill sheet (rims elsewhere stand 13 cm proud)."""
    return [LEVEL[i] + RIM_H - NOTCH_D + 0.07 for i in range(len(LEVEL))]


def water_mesh(F, cell=0.4):
    """One flat water sheet per tier (ADR 0008 Water.shader colours, same encoding as the hero basin water):
    R = foam (shallow edges, rapids before each notch, impact at each cascade foot), G = depth / 3 m,
    B = tier / 4, A = soft edge smoothstep(-0.4, 0.15, depth). UV0 = metres (Blender x, y)."""
    from scipy import ndimage
    wl = water_levels()
    xs = np.arange(FOOT['x'][0], FOOT['x'][1] + 1e-6, cell)
    ys = np.arange(FOOT['y'][0], FOOT['y'][1] + 1e-6, cell)
    X, Y = np.meshgrid(xs, ys)
    H, t, E_ = heights(F, X, Y)
    V, Fc, C, UV = [], [], [], []
    sm = lambda a, b, x: np.clip((x - a) / (b - a), 0, 1) ** 2 * (3 - 2 * np.clip((x - a) / (b - a), 0, 1))  # noqa: E731
    for k in range(len(LEVEL)):
        m = ndimage.binary_dilation(t == k, iterations=1) & (H < wl[k] + 0.3)
        depth = wl[k] - H
        foam = 1 - sm(0.05, 0.7, depth)
        for li, c, w in NOTCHES:
            if li == k:      # rapids: water speeds up toward the notch
                foam = np.maximum(foam, np.exp(-((X - c) / (w * 0.6)) ** 2) * np.exp(-(np.maximum(-E_[k], 0) / 1.2) ** 2) * 0.8)
            if li == k - 1:  # impact foam at the cascade foot
                foam = np.maximum(foam, np.exp(-((X - c) / (w * 0.7)) ** 2) * np.exp(-(np.maximum(E_[li], 0) / 1.8) ** 2))
        ids = -np.ones(X.shape, int)
        ny, nx = X.shape
        quads = m[:-1, :-1] & m[:-1, 1:] & m[1:, 1:] & m[1:, :-1]
        need = np.zeros_like(m)
        need[:-1, :-1] |= quads; need[:-1, 1:] |= quads; need[1:, 1:] |= quads; need[1:, :-1] |= quads
        jj, ii = np.where(need)
        base = len(V)
        ids[jj, ii] = base + np.arange(len(jj))
        for a, b in zip(jj, ii):
            V.append((X[a, b], Y[a, b], wl[k]))
            d = depth[a, b]
            C.append((min(1.0, foam[a, b]), min(1.0, max(0.0, d / 3.0)), k / 4.0, float(sm(-0.4, 0.15, d))))
            UV.append((X[a, b], Y[a, b]))
        qj, qi = np.where(quads)
        for a, b in zip(qj, qi):
            Fc.append((ids[a, b], ids[a, b + 1], ids[a + 1, b + 1], ids[a + 1, b]))
    me = bpy.data.meshes.new(NAME + '_Water')
    me.from_pydata(V, [], Fc)
    uvl = me.uv_layers.new(name='UVMap')
    for p in me.polygons:
        for li in p.loop_indices:
            uvl.data[li].uv = UV[me.loops[li].vertex_index]
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    ca.data.foreach_set('color', np.array(C, np.float32).ravel())
    ob = bpy.data.objects.new(NAME + '_Water', me)
    E.link(ob)
    print(NAME, 'water tris', E.tris(ob))
    return ob


def build(quick=False):
    E.reset()
    F = Field(SEED)
    rng = np.random.default_rng(SEED)
    ob = heightfield_mesh(F)
    sol = ob.modifiers.new('sol', 'SOLIDIFY')
    sol.thickness, sol.offset = 2.0, -1.0           # grid faces point up: grow the slab downward
    E.set_active(ob)
    bpy.ops.object.modifier_apply(modifier='sol')
    parts = boulders(F, rng)
    if parts:
        from ledges_high import join
        ob = join([ob] + parts, NAME + '_high')
    E.voxel_remesh(ob, 0.3 if quick else 0.045)
    print(NAME, 'remeshed faces', len(ob.data.polygons))
    if not quick:
        E.smooth(ob, 0.5, 2)
        lip_bulge_and_flute(ob, F)
        E.displace(ob, E.tex('lumps', 'CLOUDS', noise_scale=1.2, noise_depth=2), 0.1)
        rockify(ob, 1.4, 0.7, 0.5, SEED, max_cut=0.09)
        rockify(ob, 0.5, 1.0, 0.5, SEED + 1, max_cut=0.035)
        crack = E.tex('crack', 'VORONOI', noise_scale=0.5, distance_metric='DISTANCE',
                      weight_1=-1.0, weight_2=1.0, noise_intensity=1.0)
        E.displace(ob, crack, 0.03, mid=0.0)
        E.displace(ob, E.tex('grain', 'STUCCI', noise_scale=0.12, turbulence=4.0), 0.01)
    E.cut_below(ob, -0.45)
    attributes(ob, F)
    anchors(F)                                      # empties saved with the .blend, exported by the bake
    water_mesh(F)
    print(NAME, 'high tris', E.tris(ob))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + ('_quick' if quick else '_high') + '.blend'),
                                compress=True)


if __name__ == '__main__':
    build(quick='--quick' in E.args())
