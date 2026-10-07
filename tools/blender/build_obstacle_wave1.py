"""Obstacle wave 1 (Jungle, five common archetypes), spec 005 sections 5-9 and 15.2. Procedural bpy, ZERO API credits,
deterministic (fixed seeds). Called by build_crossing_kit.py (shared 512 px palette atlas, same exporter); run that script.
Conventions (Unity space, metres): x lateral, y up, z along the path (hero runs +z, so the lethal FRONT face is at -z).
Origin = footprint centre of the hitbox, y=0 = ribbon surface. Static bodies are authored with EMBED m sunk below y=0 (spec G1,
lowest vertex at -EMBED); the visible part fills the hitbox from y=0. Decals (Wallow, Furrow, SandBar) sit at y=+0.012..0.1.
HITBOXES (read from Gameplay/Track/ObstacleKitDesignValues.cs, ObstacleShape(width, depth, bottom, top)):
  LowBarrier 2.04 x 0.6 x y0..0.8 | HighBarrier 2.04 x 0.5 x y1.1..3.0 | FullBlock 2.04 x 1.0 x y0..3.0
  Mover 1.90 x 1.6 x y0..1.9 | LaneDenial (thorn) 2.04 x 4.0 x y0..2.2
SPEC (below) is the single source for the numeric check in verify_obstacle_wave1.py."""
import math
import bpy, bmesh
from mathutils import Vector
import build_crossing_kit as ck
from build_crossing_kit import Builder, ROWS

EMBED = 0.06   # G1: lowest vertex of a static body is this far below the ribbon (spec 3.4: 0.03..0.12). Set 0 for "base on y=0".
W, LANE = 1.02, 2.4  # half hitbox width (2.04 m), lane pitch

# ------------------------------------------------------------------ numeric spec (hitbox per piece)
# name: (role, hitbox (x0,x1,y0,y1,z0,z1) or None, tri budget, note)
def _hb(w, d, y0, y1): return (-w / 2, w / 2, y0, y1, -d / 2, d / 2)
SPEC = {}
for nm, L in (("Obst_Log_Trunk", 2.04), ("Obst_Log_TrunkB", 4.44), ("Obst_Log_TrunkC", 6.84)):
    SPEC[nm] = ("body", _hb(L, 0.6, 0, 0.8), 1400, "low barrier; +0.15 m overhang per free end; top <= 0.88")
SPEC.update({
    "Obst_RootPlate": ("ctx", None, 1200, "root disc 2.8 x 2.3 m, 0.6 thick; CP zone <= 2.5 m"),
    "Obst_RootPlateB": ("ctx", None, 1200, "root disc 2.3 x 1.7 m, 0.6 thick; CP zone <= 2.5 m"),
    "Obst_Stump": ("ctx", None, 500, "stump, splinter scar, <= 1.6 m"),
    "Obst_RockHump": ("ctx", None, 400, "rock under the single-lane log end, <= 0.8 m"),
})
for nm in ("Obst_HangMat", "Obst_HangMatB", "Obst_HangMatC"):
    SPEC[nm] = ("body", _hb(2.04, 0.5, 1.1, 3.0), 1200, "high barrier; tips >= 1.05, top to 3.07")
SPEC.update({
    "Obst_Limb": ("ctx", None, 900, "limb 5.0 m, origin = trunk end, underside 4.6 m"),
    "Obst_LimbB": ("ctx", None, 900, "limb 9.6 m, origin = trunk end, underside 4.6 m"),
    "Obst_LianaTie": ("ctx", None, 120, "tie 1.6 m, origin = top (limb), hangs along -Y"),
})
for nm in ("Obst_ButtressFin", "Obst_ButtressFinB", "Obst_ButtressFinC"):
    SPEC[nm] = ("body", _hb(2.04, 1.0, 0, 3.0), 1600, "full block; ragged top 3.0..4.0 merges into the trunk behind")
SPEC["Obst_StandingStone"] = ("body", _hb(2.04, 1.0, 0, 3.0), 800, "full block skin 1")
SPEC["Obst_StandingStoneB"] = ("body", _hb(2.04, 1.0, 0, 3.0), 800, "full block skin 1")
SPEC["Obst_WedgedSlab"] = ("body", _hb(2.04, 1.0, 0, 3.0), 900, "full block skin 2; leans 1.5 deg in +x")
SPEC["Obst_RootWeb"] = ("ctx", None, 300, "roots crawling out from a fin base; <= 0.12 m in the lane")
for nm in ("Obst_BarrelBoulder", "Obst_BarrelBoulderB"):
    SPEC[nm] = ("body", _hb(1.90, 1.6, 0, 1.9), 900, "mover; axis along z, R 0.95, L 1.6; centre y 0.92 (0.03 embed)")
SPEC.update({
    "Obst_Wallow": ("decal", None, 200, "2.4 x 2.2 m hollow decal, rim <= 0.10"),
    "Obst_Chock": ("ctx", None, 120, "wedge stone + stake + pebbles"),
    "Obst_Furrow": ("decal", None, 150, "2.4 x 0.7 m strip along x (one lane shift)"),
    "Obst_SandBar": ("decal", None, 150, "2.4 x 2.2 m catch decal"),
})
for nm in ("Obst_ThornCage", "Obst_ThornCageB", "Obst_ThornCageC"):
    SPEC[nm] = ("body", _hb(2.04, 4.0, 0, 2.2), 1800, "thorn patch lane; thorns may poke <= 0.25 m out")
SPEC.update({
    "Obst_CaneWall": ("ctx", None, 1200, "front wall panel 2.04 x 0.5 x 2.2"),
    "Obst_CaneWallB": ("ctx", None, 1200, "side wall panel 0.5 x 4.0 x 2.2 (long axis z)"),
    "Obst_RootCrown": ("ctx", None, 600, "bush root crown, CP zone <= 2.5 m"),
    "Obst_ThornLitter": ("ctx", None, 100, "cane + leaf litter, <= 0.12 m"),
})

# ------------------------------------------------------------------ helpers
def sgnpow(v, e): return math.copysign(abs(v) ** e, v)
def V(*a): return Vector(a)

def loft(b, rings, colfn, cap0=True, cap1=True):
    """Skin through rings (equal point counts). colfn(i, k, normal) -> colour name (k<0: cap). Normals point away from the axis."""
    bm = b.bm; vr = [[bm.verts.new(p) for p in r] for r in rings]; n = len(vr[0]); recs = []
    cen = [sum((Vector(p) for p in r), Vector()) / len(r) for r in rings]
    for i in range(len(vr) - 1):
        c = (cen[i] + cen[i + 1]) / 2
        for k in range(n):
            try: f = bm.faces.new([vr[i][k], vr[i][(k + 1) % n], vr[i + 1][(k + 1) % n], vr[i + 1][k]])
            except ValueError: continue
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - c) < 0: f.normal_flip()
            recs.append((f, i, k))
    axis = cen[-1] - cen[0]
    for idx, (ring, on, sgn) in enumerate(((vr[0], cap0, -1), (vr[-1], cap1, 1))):
        if on:
            try: f = bm.faces.new(ring)
            except ValueError: continue
            f.normal_update()
            if f.normal.dot(axis) * sgn < 0: f.normal_flip()
            recs.append((f, 0 if idx == 0 else len(rings) - 1, -1 - idx))
    for f, i, k in recs:
        f.normal_update(); f[b.col] = ROWS.index(colfn(i, k, f.normal.copy()))
    return vr

def lump(b, centre, radii, colfn, sub=1, jit=0.1, ymin=None, ymax=None):
    res = bmesh.ops.create_icosphere(b.bm, subdivisions=sub, radius=1.0)
    for v in res["verts"]:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2])) * (1 + b.rng.uniform(-jit, jit)) + Vector(centre)
        if ymin is not None: v.co.y = max(v.co.y, ymin)
        if ymax is not None: v.co.y = min(v.co.y, ymax)
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.normal_update(); f[b.col] = ROWS.index(colfn(f.normal.copy()))

def ring_xz(cx, y, cz, hx, hz, n, ex=2.0, jit=None, rng=None):
    out = []
    for k in range(n):
        a = 2 * math.pi * k / n; j = 1 + (rng.uniform(-jit, 0) if jit else 0)
        out.append((cx + hx * j * sgnpow(math.cos(a), 2 / ex), y, cz + hz * j * sgnpow(math.sin(a), 2 / ex)))
    return out

def cuboid(b, x0, x1, y0, y1, z0, z1, colfn):
    r = lambda y: [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    loft(b, [r(y0), r(y1)], colfn)

def thorn(b, base, d, length, r, big=True):
    """Thorn hook: brown frustum + ink tip (big, 13 tris) or ink pyramid (small, 6 tris). Tips ink always (spec 9.1)."""
    d = Vector(d).normalized(); base = Vector(base)
    if big:
        b.tube([base, base + d * length * 0.5, base + d * length], [r, r * 0.55, 0.004], 3,
               lambda i, k, n: "bark_alt" if i == 0 else "ink", cap_start=True, cap_end=True)
    else:
        b.tube([base, base + d * length], [r, 0.004], 3, lambda i, k, n: "ink", cap_end=False)

def cane(b, pts, r0, r1, sides, colfn=None):
    n = len(pts); rad = [r0 + (r1 - r0) * i / (n - 1) for i in range(n)]
    b.tube(pts, rad, sides, colfn or (lambda i, k, nn: "thorn" if (i + k) % 3 else "bark_alt"), cap_end=True)

def flat_surface(b, verts_rows, colfn):
    """Open grid/rings of verts (rows of equal length, closed ring if closed) as up-facing faces. colfn(i,k)->colour."""
    bm = b.bm; vr = [[bm.verts.new(p) for p in r] for r in verts_rows]; n = len(vr[0])
    for i in range(len(vr) - 1):
        for k in range(n):
            vs = [vr[i][k], vr[i][(k + 1) % n], vr[i + 1][(k + 1) % n], vr[i + 1][k]]
            try: f = bm.faces.new(vs)
            except ValueError: continue
            f.normal_update()
            if f.normal.y < 0: f.normal_flip()
            f[b.col] = ROWS.index(colfn(i, k))
    return vr

def quad_up(b, x0, x1, z0, z1, y, colour):
    vs = [b.bm.verts.new(p) for p in ((x0, y, z0), (x0, y, z1), (x1, y, z1), (x1, y, z0))]
    f = b.bm.faces.new(vs); f.normal_update()
    if f.normal.y < 0: f.normal_flip()
    f[b.col] = ROWS.index(colour)

# ------------------------------------------------------------------ A1 LOW BARRIER
def build_log(name, L, seed):
    b = Builder(name, seed); rng = b.rng
    HW, H, OV, N, NE = 0.3, 0.8, 0.15, 20, 5.0
    cy, hy = (H - EMBED) / 2, (H + EMBED) / 2
    xe = L / 2 + OV; M = max(4, int(round(2 * xe / 0.55)))
    rings = []
    for i in range(M + 1):
        x = -xe + 2 * xe * i / M; end = i in (0, M)
        s = 1.0 if i == M // 2 else 1 - rng.uniform(0.0, 0.025)
        if end: s = 0.94
        ring = []
        for k in range(N):
            a = 2 * math.pi * k / N
            z = -HW * s * sgnpow(math.cos(a), 2 / NE); y = cy + hy * s * sgnpow(math.sin(a), 2 / NE)
            ring.append((x + (rng.uniform(0.0, 0.11) * (1 if i == 0 else -1) if end else 0.0), y, z))
        rings.append(ring)
    def col(i, k, n):
        if k < 0: return "heartwood"
        if i in (0, M - 1): return "heartwood" if rng.random() < 0.3 else ("bark_alt" if k % 3 == 0 else "bark")
        if k in (2, 3): return "ochre"      # red-ochre trail mark on the top-front ridge (spec 5.1 / 3.5)
        if k in (1, 4): return "ink"        # ink flank
        if n.z > 0.45: return "moss"
        if n.y > 0.7: return "moss" if rng.random() < 0.3 else "bark"
        return "bark_alt" if (k + (i // 3)) % 3 == 0 else "bark"   # longitudinal grain
    loft(b, rings, col)
    for j in range(max(2, int(L / 0.7))):  # displaced soil lip on the front base (<= 0.12 m high)
        x = -L / 2 + (j + 0.5) * L / max(2, int(L / 0.7)) + rng.uniform(-0.1, 0.1)
        lump(b, (x, 0.0, -HW - 0.03), (0.3, 0.11, 0.13), lambda n: "soil", sub=1, jit=0.12, ymin=-EMBED, ymax=0.12)
    for j in range(max(2, int(L / 1.2))):  # moss pads on the rear top (flush with the 0.8 m ridge)
        x = -L / 2 + (j + 0.5) * L / max(2, int(L / 1.2)) + rng.uniform(-0.15, 0.15)
        lump(b, (x, 0.66, 0.1), (0.3, 0.13, 0.17), lambda n: "moss", sub=1, jit=0.05, ymax=0.8)
    for sx in (-1, 1):  # branch stub, 0.25 m, at each end on the rear side
        x = sx * (L / 2 - 0.15)
        b.tube([(x, 0.4, 0.22), (x, 0.46, 0.5)], [0.055, 0.04], 5, lambda i, k, n: "bark_alt", cap_end=True)
    return b

def build_rootplate(name, w, h, seed):
    b = Builder(name, seed); rng = b.rng
    N, T, bot = 18, 0.3, -0.10
    cy, hy = (h + bot) / 2, (h - bot) / 2
    jit = [1 + rng.uniform(-0.2, 0.0) for _ in range(N)]
    def ring(x, s):
        return [(x, cy + hy * s * jit[k] * math.sin(2 * math.pi * k / N), (w / 2) * s * jit[k] * math.cos(2 * math.pi * k / N)) for k in range(N)]
    # face toward -x: torn soil face; face toward +x: bark (where the trunk joins)
    loft(b, [ring(-T, 0.84), ring(0.0, 1.0), ring(T, 0.84)],
         lambda i, k, n: ("soil" if rng.random() < 0.6 else "bark_alt") if k == -1 else ("bark" if k == -2 else ("moss" if (n.y > 0.6 and rng.random() < 0.5) else ("bark_alt" if (i + k) % 2 else "bark"))))
    for j in range(14):  # torn roots: thick stubs near the rim, thinner curling ones inside; uneven, not a wagon wheel
        a = 2 * math.pi * j / 14 + rng.uniform(-0.25, 0.25); R = rng.uniform(0.45, 1.0); r0 = rng.uniform(0.1, 0.45); bend = rng.uniform(-0.35, 0.35)
        pts = []
        for t in (0.0, 0.33, 0.66, 1.0):
            rr = r0 + (R - r0) * t; aa = a + bend * t
            pts.append((-T - 0.03 - 0.05 * math.sin(math.pi * t) - (0.22 if (t == 1.0 and R > 0.9) else 0.0), cy + math.sin(aa) * hy * rr, math.cos(aa) * (w / 2) * rr))
        pts = [(x, max(y, bot + 0.1), z) for x, y, z in pts]
        cane(b, pts, rng.uniform(0.07, 0.12), 0.025, 5, lambda i, k, n: "bark" if k % 2 else "bark_alt")
    for j in range(5):  # soil clumps clinging to the face
        a = rng.uniform(0, 6.28); R = rng.uniform(0.25, 0.7)
        lump(b, (-T - 0.04, cy + math.sin(a) * hy * R, math.cos(a) * (w / 2) * R), (0.1, 0.16, 0.2), lambda n: "soil", sub=1, ymin=bot)
    return b

def build_stump(name="Obst_Stump", seed=511):
    b = Builder(name, seed); rng = b.rng
    N = 10; levels = [(-EMBED, 0.64), (0.25, 0.56), (0.65, 0.5), (0.9, 0.47)]
    rings = []
    for li, (y, r) in enumerate(levels):
        top = li == len(levels) - 1
        rings.append([(r * (1 + rng.uniform(-0.05, 0.05)) * math.cos(2 * math.pi * k / N),
                       y + (rng.uniform(-0.05, 0.3) if top else 0), r * (1 + rng.uniform(-0.05, 0.05)) * math.sin(2 * math.pi * k / N)) for k in range(N)])
    loft(b, rings, lambda i, k, n: "heartwood" if k == -2 else ("moss" if (i == 2 and rng.random() < 0.5) else ("bark_alt" if k % 2 else "bark")))
    for j in range(4):  # flared roots
        a = 2 * math.pi * j / 4 + 0.5 + rng.uniform(-0.2, 0.2)
        pts = [(math.cos(a) * rr, y, math.sin(a) * rr) for rr, y in ((0.3, 0.55), (0.6, 0.22), (0.95, -0.02))]
        cane(b, pts, 0.17, 0.07, 5, lambda i, k, n: "bark" if k % 2 else "bark_alt")
    for j in range(3):  # splinter spikes on the break (matching scar)
        a = 2 * math.pi * j / 3 + 0.3; p = V(math.cos(a) * 0.25, 0.9, math.sin(a) * 0.25)
        b.tube([p, p + V(math.cos(a) * 0.05, 0.55 + 0.2 * j, math.sin(a) * 0.05)], [0.08, 0.01], 3, lambda i, k, n: "heartwood", cap_end=True)
    return b

def build_rockhump(name="Obst_RockHump", seed=521):
    b = Builder(name, seed)
    for c, r in (((0.0, 0.0, 0.0), (0.62, 0.7, 0.48)), ((0.42, 0.0, -0.18), (0.45, 0.4, 0.34)), ((-0.4, 0.0, 0.2), (0.4, 0.34, 0.3))):
        lump(b, c, r, lambda n: "moss" if n.y > 0.55 else "stone", sub=2 if r[1] > 0.5 else 1, jit=0.06, ymin=-EMBED)
    return b

# ------------------------------------------------------------------ A2 HIGH BARRIER
def build_hangmat(name, seed, variant):
    b = Builder(name, seed); rng = b.rng
    TOP = 3.05
    rows = [(-0.18, 14, 0.0), (0.0, 13, 0.5), (0.18, 14, 0.0)]
    for ri, (z, n, off) in enumerate(rows):
        pitch = 1.9 / 13
        for j in range(n):
            x = -0.95 + (j + off) * pitch + rng.uniform(-0.015, 0.015) if off else -0.95 + j * pitch + rng.uniform(-0.015, 0.015)
            tip = rng.uniform(1.05, 1.2) if ri == 0 or rng.random() < 0.7 else rng.uniform(1.4, 2.0)
            if variant == "B" and ri > 0 and rng.random() < 0.12: tip = rng.uniform(1.6, 2.3)
            if variant == "C" and ri == 2 and j % 3 == 0: tip = rng.uniform(1.9, 2.5)
            wob = rng.uniform(-0.04, 0.04); mid = (TOP + tip) / 2
            rx = (0.1, 0.085, 0.06)
            rings = [[(x + w * math.cos(a), y, z + 0.06 * math.sin(a)) for a in (0, math.pi / 2, math.pi, 3 * math.pi / 2)]
                     for (y, w, x) in ((TOP, rx[0], x), (mid, rx[1], x + wob), (tip, rx[2], x + wob * 0.5))]
            leafy = (variant == "B" and rng.random() < 0.45) or rng.random() < 0.15
            palette = ("leaf", "moss") if leafy else (("creeper", "bark", "liana", "bark_alt") if ri != 1 else ("bark", "bark_alt", "creeper"))
            c0 = rng.choice(palette)
            loft(b, rings, lambda i, k, nn: c0 if (i + k) % 3 else rng.choice(palette), cap0=False, cap1=True)
            if rng.random() < 0.22:
                b.leaf((x, mid + rng.uniform(-0.3, 0.3), z - 0.06 if ri == 0 else z), (rng.uniform(-0.3, 0.3), -1.0, -0.35 if ri == 0 else rng.uniform(-0.2, 0.2)),
                       0.24, 0.16, 0.0, "leaf" if rng.random() < 0.6 else "moss")
    pts = [(x, 2.96 + 0.02 * math.sin(x * 3), 0.0) for x in (-1.08, -0.5, 0.5, 1.08)]
    b.tube(pts, [0.11, 0.12, 0.12, 0.11], 8, lambda i, k, n: "bark_alt" if k % 2 else "bark", cap_start=True, cap_end=True)  # top rail the ties attach to
    # one tied cloth band (ochre flanked by ink) on a front bundle, y 1.10..1.32 (G8, keeps "red underside = slide")
    cx = rng.choice((-0.5, -0.25, 0.25, 0.5)) if variant != "A" else 0.0
    for y0, y1, c in ((1.10, 1.14, "ink"), (1.14, 1.28, "ochre"), (1.28, 1.32, "ink")):
        cuboid(b, cx - 0.27, cx + 0.27, y0, y1, -0.28, -0.1, lambda i, k, n, c=c: c)
    if variant == "C":  # a snapped leafy bough caught in the lianas on top (skin 1 flavour), stays above the hitbox top
        for c, r in (((-0.5, 3.06, 0.0), (0.55, 0.2, 0.3)), ((0.35, 3.08, 0.05), (0.6, 0.22, 0.3))):
            lump(b, c, r, lambda n: "leaf" if n.y > 0 else "moss", sub=1, jit=0.15, ymin=3.0)
    return b

def build_limb(name, L, seed):
    b = Builder(name, seed); rng = b.rng
    n = 11; pts, rad = [], []
    for i in range(n):
        t = i / (n - 1); x = -0.25 + (L + 0.25) * t; r = 0.35 - 0.175 * t
        pts.append((x, 4.6 + r + 0.3 * (1 - t) ** 2, 0.15 * math.sin(t * 3.0))); rad.append(r)
    b.tube(pts, rad, 8, lambda i, k, nn: "moss" if nn.y > 0.75 and i > 0 else ("bark_alt" if k % 2 else "bark"), cap_start=True, cap_end=True)
    lump(b, (0.0, pts[0][1], 0.0), (0.5, 0.5, 0.5), lambda nn: "bark_alt", sub=1, jit=0.12)  # burl where it leaves the trunk
    for t, side, s, c in ((0.25, 1, 0.8, "leaf"), (0.45, -1, 0.9, "moss"), (0.65, 1, 0.8, "leaf"), (0.85, -1, 0.65, "leaf_lt")):
        p = pts[int(t * (n - 1))]
        b.blob((p[0], p[1] + 0.3 + 0.1 * s, side * 0.25), (s * 1.0, s * 0.45, s * 0.7), c)
    return b

def build_liana_tie(name="Obst_LianaTie", seed=541):
    b = Builder(name, seed); rng = b.rng
    pts = [(0.03 * math.sin(i * 1.3), -1.6 * i / 7, 0.02 * math.cos(i * 1.7)) for i in range(8)]
    b.tube(pts, [0.016] * 8, 4, lambda i, k, n: "liana" if (i + k) % 3 else "creeper", cap_start=False, cap_end=False)
    b.tube([(pts[0][0], -0.03, pts[0][2]), (pts[0][0], -0.17, pts[0][2])], [0.045, 0.045], 6, lambda i, k, n: "bark_alt", cap_start=True, cap_end=True)
    b.tube([(pts[7][0], -1.48, pts[7][2]), (pts[7][0], -1.6, pts[7][2])], [0.05, 0.05], 6, lambda i, k, n: "bark_alt" if k % 2 else "bark", cap_start=True, cap_end=True)  # knot wrap at the mat
    return b

# ------------------------------------------------------------------ A3 FULL BLOCK
def _fin_ring(y, pts_front, z_back, zshift=0.0, sx=1.0):
    r = [(x * sx, y, z + zshift) for x, z in pts_front]
    r += [(W * sx, y, z_back), (0.0, y, z_back + 0.12), (-W * sx, y, z_back)]
    return r

def build_fin(name, seed, period, amp, topvar):
    b = Builder(name, seed); rng = b.rng
    F = 17
    front = []
    for j in range(F):
        valley = (j % period) == period // 2 if period > 2 else (j % 2 == 1)
        front.append((max(-W, min(W, -W + 2 * W * j / (F - 1) + (rng.uniform(-0.025, 0.025) if 0 < j < F - 1 else 0.0))), -0.5 + (amp * rng.uniform(0.5, 1.3) if valley else 0.0)))
    levels = [-EMBED, 0.8, 0.9, 1.1, 1.2, 2.0, 3.0]
    rings = [_fin_ring(y, front, 0.5) for y in levels]
    rings.append(_fin_ring(3.45, front, 0.5, zshift=0.18, sx=0.9))
    # ragged top: front vertices >= 3.0 (box top), rear vertices rise to 4.0 as the fin merges into the trunk
    top = rings[-1]
    for k in range(len(top)):
        front_v = k < F
        h = rng.uniform(3.02, 3.15) if front_v and k % 2 == 0 else (rng.uniform(3.2, 3.5) if front_v else rng.uniform(3.6, 4.0))
        if topvar == "C" and 5 <= k <= 9: h += 0.3
        top[k] = (top[k][0], h, top[k][2])
    def col(i, k, n):
        if k == -2: return "bark_alt" if rng.random() < 0.5 else "moss"
        if k == -1: return "bark_alt"
        if n.z > 0.6: return "bark_alt"
        if i == 2 and n.z < 0.6: return "ochre"      # y 0.9..1.1 band wraps front and sides (hip height)
        if i in (1, 3) and n.z < 0.6: return "ink"   # ink flanks y 0.8..0.9 and 1.1..1.2
        if n.z < -0.3 and k < F and (k % period) == period // 2: return "moss"
        return "bark" if (i + k) % 2 else "bark_alt"
    loft(b, rings, col)
    for j in range(3):  # moss clumps on the rear shoulder
        lump(b, (-0.6 + 0.6 * j, 3.05, 0.3), (0.35, 0.12, 0.25), lambda n: "moss", sub=1, jit=0.1, ymin=3.0)
    return b

def build_standing_stone(name, seed, notched):
    b = Builder(name, seed); rng = b.rng
    D, c = 0.5, 0.12
    def ring(y, s):
        w, d = W * s, D * s
        return [(-w + c, y, -d), (w - c, y, -d), (w, y, -d + c), (w, y, d - c), (w - c, y, d), (-w + c, y, d), (-w, y, d - c), (-w, y, -d + c)]
    levels = [-EMBED, 0.8, 0.9, 1.1, 1.2, 2.0, 2.7, 3.0]
    rings = [ring(y, 1.0 if y in (0.8, 1.1, 2.0) else 1 - rng.uniform(0.0, 0.015)) for y in levels]
    top = rings[-1]
    for k in range(8):
        top[k] = (top[k][0], 3.0 - (rng.uniform(0.0, 0.1) if k not in (0, 1) else 0.0) - (0.28 if notched and k in (4, 5, 6) else 0.0), top[k][2])
    def col(i, k, n):
        if k == -2: return "moss"
        if k == -1: return "stone"
        if i == 2 and n.y < 0.5: return "ochre"
        if i in (1, 3) and n.y < 0.5: return "ink"
        if n.y > 0.5: return "moss"
        return "moss" if rng.random() < 0.3 else "stone"
    loft(b, rings, col)
    for v0 in (0.0, math.pi):  # vine wrap: two spirals around the slab
        pts = []
        for i in range(20):
            t = i / 19; th = v0 + 2 * math.pi * 2.2 * t
            pts.append((max(-1, min(1, 1.5 * math.cos(th))) * (W + 0.03), 0.15 + 2.55 * t, max(-1, min(1, 1.5 * math.sin(th))) * (D + 0.03)))
        b.tube(pts, [0.035] * 20, 3, lambda i, k, n: "liana" if (i + k) % 4 else "leaf_lt", cap_end=True)
    for j in range(8):  # ring of ferns at the base
        a = 2 * math.pi * j / 8 + rng.uniform(-0.2, 0.2)
        ex, ez = math.cos(a) * 0.9, math.sin(a) * 0.45
        b.leaf((ex, 0.05, ez), (math.cos(a), 0.5, math.sin(a)), 0.38, 0.22, 0.2, ["leaf", "leaf_lt", "moss"][j % 3])
    return b

def build_wedged_slab(name="Obst_WedgedSlab", seed=561):
    b = Builder(name, seed); rng = b.rng
    D, c, t = 0.5, 0.1, math.tan(math.radians(1.5))
    def ring(y):
        s = 1.0
        base = [(-W * s + c, -D), (W * s - c, -D), (W * s, -D + c), (W * s, D - c), (W * s - c, D), (-W * s + c, D), (-W * s, D - c), (-W * s, -D + c)]
        return [(x + t * max(y, 0), y, z) for x, z in base]
    levels = [-EMBED, 0.8, 0.9, 1.1, 1.2, 2.0, 3.0]
    rings = [ring(y) for y in levels]
    for k in range(8): rings[-1][k] = (rings[-1][k][0], 3.0 - (rng.uniform(0.0, 0.06) if k > 1 else 0.0), rings[-1][k][2])
    loft(b, rings, lambda i, k, n: "stone" if k == -1 else ("moss" if k == -2 else ("ochre" if i == 2 and n.y < 0.5 else ("ink" if i in (1, 3) and n.y < 0.5 else ("moss" if (n.y > 0.5 or rng.random() < 0.18) else "stone")))))
    for j, (x, z, r) in enumerate(((W + 0.3, -0.15, 0.4), (W + 0.25, 0.35, 0.33), (W + 0.6, 0.1, 0.3), (-W - 0.2, 0.3, 0.3))):  # jam stones at the base
        lump(b, (x, 0.0, z), (r, r * 0.8, r * 0.9), lambda n: "moss" if n.y > 0.6 else "stone", sub=1, jit=0.12, ymin=-EMBED)
    return b

def build_rootweb(name="Obst_RootWeb", seed=571):
    b = Builder(name, seed); rng = b.rng
    # origin = fin footprint centre, roots start at the fin base (z=-0.5) and crawl toward the path (-z) and sideways.
    for j in range(8):
        x0 = -0.9 + 1.8 * j / 7; tx = x0 + rng.uniform(-0.45, 0.45); L = rng.uniform(0.4, 0.62)
        pts = [(x0, 0.03, -0.5), ((x0 + tx) / 2, 0.02, -0.5 - L / 2), (tx, 0.0, -0.5 - L)]
        cane(b, pts, 0.055, 0.02, 4, lambda i, k, n: "bark" if k % 2 else "bark_alt")
    for sx in (-1, 1):  # two heavier roots to the verge side, up to 0.4 m beyond |x| 1.5
        pts = [(sx * 0.8, 0.07, -0.45), (sx * 1.35, 0.06, -0.55), (sx * 1.7, 0.12, -0.5), (sx * 2.0, 0.3, -0.4)]
        cane(b, pts, 0.1, 0.06, 5, lambda i, k, n: "bark" if k % 2 else "bark_alt")
        pts = [(sx * 0.5, 0.05, -0.5), (sx * 0.9, 0.04, -0.3), (sx * 1.5, 0.1, -0.1)]
        cane(b, pts, 0.07, 0.03, 4, lambda i, k, n: "bark" if k % 2 else "bark_alt")
    return b

# ------------------------------------------------------------------ A4 MOVER
def build_barrel(name, seed, mirror):
    b = Builder(name, seed); rng = b.rng
    R, N, CY = 0.95, 20, 0.95 - 0.03
    # (z, radius, colour of the segment AFTER this ring, crack sector id or None). Lichen is patchy, cracks are partial arcs
    # (no full-circle grooves: those read as barrel hoops). The 0.35 m red-ochre ring flanked by ink wraps the middle (G8).
    spec = [(-0.80, 0.80, "stone", None), (-0.65, R, "stone", None), (-0.47, R, "crack", 0), (-0.45, R - 0.07, "crack", 0), (-0.43, R, "stone", None),
            (-0.23, R, "ink", None), (-0.175, R, "ochre", None), (0.175, R, "ink", None), (0.23, R, "stone", None), (0.50, R, "crack", 1),
            (0.53, R - 0.07, "crack", 1), (0.56, R, "stone", None), (0.65, R, "stone", None), (0.80, 0.80, "stone", None)]
    if mirror:  # variant B: mirrored layout (a segment's colour belongs to the ring before it, so shift by one)
        n = len(spec); cols = [spec[n - 2 - j][2:] for j in range(n - 1)] + [spec[0][2:]]
        spec = [(-spec[n - 1 - j][0], spec[n - 1 - j][1]) + tuple(cols[j]) for j in range(n)]
    sectors = {0: rng.randrange(0, N - 6), 1: rng.randrange(0, N - 6)}
    if mirror: sectors = {0: (sectors[0] + 9) % (N - 6), 1: (sectors[1] + 5) % (N - 6)}
    wob = [rng.uniform(0.0, 0.025) for _ in spec]
    rings = []
    for i, (z, r, _, crack) in enumerate(spec):
        ring = []
        for k in range(N):
            a = 2 * math.pi * k / N; end = i in (0, len(spec) - 1)
            rr = r - wob[i] - (rng.uniform(0.0, 0.04) if k not in (0, N // 4, N // 2, 3 * N // 4) or end else 0.0)
            if crack is not None and r < R and not (sectors[crack] <= k < sectors[crack] + 6): rr = R - wob[i] - rng.uniform(0.0, 0.04)
            if k in (2, 3) and abs(z) < 0.7: rr *= 0.955   # worn flat on one face
            ring.append((rr * math.cos(a), CY + rr * math.sin(a), z))
        rings.append(ring)
    seg = [(c, cr) for _, _, c, cr in spec]
    def col(i, k, n):
        if k < 0: return "stone" if rng.random() < 0.7 else "moss"
        c, cr = seg[i]
        if c == "crack": return "bark_alt" if sectors[cr] <= k < sectors[cr] + 6 else ("moss" if rng.random() < 0.3 else "stone")
        if c == "stone": return "moss" if (rng.random() < (0.5 if n.y > 0.3 else 0.2)) else "stone"   # patchy lichen, mostly on top
        return c
    loft(b, rings, col)
    return b

def build_wallow(name="Obst_Wallow", seed=581, sand=False, rx=1.2, rz=1.1):
    b = Builder(name, seed); rng = b.rng
    N = 16 if sand else 20
    prof = [(0.0, 0.012), (0.45, 0.012), (0.75, 0.02), (0.9, 0.10 if sand else 0.08), (1.0, 0.02)]
    if sand: prof = [(0.0, 0.012), (0.5, 0.014), (0.8, 0.05), (0.92, 0.10), (1.0, 0.02)]
    rows = []
    for i, (f, y) in enumerate(prof):
        if i == 0: rows.append([(0.0, y, 0.0)] * 1 + [])
        else: rows.append([(f * rx * (1 + rng.uniform(-0.04, 0.0)) * math.cos(2 * math.pi * k / N), y, f * rz * (1 + rng.uniform(-0.04, 0.0)) * math.sin(2 * math.pi * k / N)) for k in range(N)])
    bm = b.bm; c0 = bm.verts.new((0.0, prof[0][1], 0.0)); vr = [[bm.verts.new(p) for p in r] for r in rows[1:]]
    for k in range(N):  # centre fan
        f = bm.faces.new([c0, vr[0][(k + 1) % N], vr[0][k]]); f.normal_update()
        if f.normal.y < 0: f.normal_flip()
        f[b.col] = ROWS.index("sand" if sand and rng.random() < 0.7 else "pad")
    for i in range(len(vr) - 1):
        for k in range(N):
            f = bm.faces.new([vr[i][k], vr[i][(k + 1) % N], vr[i + 1][(k + 1) % N], vr[i + 1][k]]); f.normal_update()
            if f.normal.y < 0: f.normal_flip()
            if sand: c = "pad" if (i == 0 and rng.random() < 0.18) else ("sand" if i < 3 else "soil")  # fresh impact marks
            else: c = "pad" if i == 0 else ("soil" if i == 1 or rng.random() < 0.6 else "pad") if i < 3 else "soil"
            f[b.col] = ROWS.index(c)
    return b

def build_sandbar(): return build_wallow("Obst_SandBar", 591, sand=True)

def build_chock(name="Obst_Chock", seed=601):
    b = Builder(name, seed); rng = b.rng
    hull = [(-0.25, -EMBED, -0.2), (0.25, -EMBED, -0.2), (0.25, -EMBED, 0.2), (-0.25, -EMBED, 0.2), (-0.25, 0.06, -0.2), (-0.25, 0.06, 0.2), (0.25, 0.3, -0.18), (0.25, 0.3, 0.18)]
    vs = [b.bm.verts.new(p) for p in hull]
    res = bmesh.ops.convex_hull(b.bm, input=vs)
    bmesh.ops.delete(b.bm, geom=res["geom_interior"], context="VERTS")
    for f in [g for g in res["geom"] if isinstance(g, bmesh.types.BMFace)]:
        f.normal_update(); f[b.col] = ROWS.index("moss" if f.normal.y > 0.7 and rng.random() < 0.3 else "stone")
    b.tube([(0.5, -0.1, 0.25), (0.46, 0.45, 0.22)], [0.05, 0.045], 4, lambda i, k, n: "bark", cap_end=True)
    for x, z in ((-0.4, -0.3), (-0.5, 0.15), (0.1, 0.4)):
        lump(b, (x, 0.0, z), (0.09, 0.07, 0.08), lambda n: "stone", sub=1, ymin=-0.02)
    return b

def build_furrow(name="Obst_Furrow", seed=611):
    b = Builder(name, seed); rng = b.rng
    xs = [-1.2 + 2.4 * i / 8 for i in range(9)]; zs = [-0.35, -0.24, -0.12, 0.12, 0.24, 0.35]
    ys = [0.02, 0.025, 0.012, 0.012, 0.025, 0.02]
    rows = [[(x, ys[j] + rng.uniform(0, 0.004), zs[j]) for x in xs] for j in range(len(zs))]
    bm = b.bm; vr = [[bm.verts.new(p) for p in r] for r in rows]
    for j in range(len(zs) - 1):
        for i in range(len(xs) - 1):
            f = bm.faces.new([vr[j][i], vr[j][i + 1], vr[j + 1][i + 1], vr[j + 1][i]]); f.normal_update()
            if f.normal.y < 0: f.normal_flip()
            f[b.col] = ROWS.index("pad" if j == 2 else ("soil" if j in (1, 3) else ("sand" if rng.random() < 0.5 else "soil")))
    for x in (-0.5, 0.5):  # ochre ticks every 1 m on both edges, flanked by ink
        for zc in (-0.33, 0.33):
            for dx0, dx1, c in ((-0.11, -0.06, "ink"), (-0.06, 0.06, "ochre"), (0.06, 0.11, "ink")):
                quad_up(b, x + dx0, x + dx1, zc - 0.07, zc + 0.07, 0.03, c)
    return b

# ------------------------------------------------------------------ A5 THORN PATCH
def _mass(b, rng, D, H, ex=6.0, tint=None):
    HW, HD = W, D / 2
    pts_n = 28
    def ring(y):
        out = []
        for k in range(pts_n):
            a = 2 * math.pi * k / pts_n; c, s_ = math.cos(a), math.sin(a)
            x, z = HW * sgnpow(c, 2 / ex), HD * sgnpow(s_, 2 / ex)
            ins = rng.uniform(0, 0.025) if z < -HD * 0.9 else rng.uniform(0, 0.09)  # front stays a wall, sides/back are ragged
            out.append((x - math.copysign(ins, x) * min(1, abs(x) / HW * 1.2), y, z - math.copysign(ins, z) * min(1, abs(z) / HD * 1.2)))
        return out
    rings = [ring(y) for y in (-EMBED, 0.4, 0.9, 1.4, 1.8, 2.1, H)]
    top = rings[-1]
    for k in range(pts_n): top[k] = (top[k][0], H + rng.uniform(-0.02, 0.08), top[k][2])  # ragged 2.18..2.28, never below the box top
    def col(i, k, n):
        if k < 0: return "thorn"
        if tint is not None: return tint(i, k, n)
        r = rng.random()
        return "bark_alt" if r < 0.12 else ("leaf" if r < 0.17 else "thorn")
    loft(b, rings, col)
    return

def _cage(b, rng, D, H, stripes=True, back=True):
    HD = D / 2
    for x in (-0.8, 0.0, 0.8):  # front posts with ochre stripes (3 of the cage posts)
        lean = rng.uniform(-0.04, 0.04)
        ys = [-EMBED, 0.9, 1.0, 1.2, 1.3, H]
        pts = [(x + lean * (y / H), y, -HD + 0.07) for y in ys]
        b.tube(pts, [0.14, 0.13, 0.13, 0.13, 0.13, 0.11], 5,
               lambda i, k, n: ("ink" if i in (1, 3) else ("ochre" if i == 2 else "bark")) if stripes else "bark", cap_end=True)
    if back:
        for x in (-0.8, 0.0, 0.8):
            b.tube([(x, -EMBED, HD - 0.1), (x + rng.uniform(-0.05, 0.05), 1.1, HD - 0.1), (x, H - 0.1, HD - 0.1)], [0.15, 0.13, 0.1], 5, lambda i, k, n: "bark_alt" if k % 2 else "bark", cap_end=True)
    for sx in (-0.9, 0.9):  # arching limbs across the top
        pts = [(sx, H - 0.15 + 0.12 * math.sin(math.pi * t) - 0.1 * t, -HD + 0.07 + (D - 0.3) * t) for t in (0, 0.17, 0.33, 0.5, 0.67, 0.83, 1.0)]
        b.tube(pts, [0.11, 0.1, 0.09, 0.09, 0.09, 0.1, 0.1], 5, lambda i, k, n: "bark_alt" if k % 2 else "bark", cap_end=True)

def _canes_front(b, rng, D, H, n_canes, n_big, n_small, face="front"):
    HD = D / 2
    for j in range(n_canes):
        x = -0.95 + 1.9 * j / (n_canes - 1) + rng.uniform(-0.03, 0.03); z = -HD + 0.01
        top = H + rng.uniform(0.0, 0.08)
        pts = [(x + rng.uniform(-0.05, 0.05) * i, top * i / 3 - EMBED * (1 - i / 3), z - 0.02 - 0.03 * math.sin(i * 2.0)) for i in range(4)]
        cane(b, pts, 0.045, 0.03, 4)
    for j in range(n_big):  # big hooks on the front face, tips ink, <= 0.2 m out of the plane
        x = rng.uniform(-0.95, 0.95); y = rng.uniform(0.4, H - 0.1)
        thorn(b, (x, y, -HD - 0.01), (rng.uniform(-0.5, 0.5), rng.uniform(0.2, 0.9), -0.45), rng.uniform(0.15, 0.26), 0.035, True)
    for j in range(n_small):
        x = rng.uniform(-0.95, 0.95); y = rng.uniform(0.3, H); sx = rng.choice((-1, 1))
        thorn(b, (x, y, -HD - 0.01), (rng.uniform(-0.6, 0.6), rng.uniform(0.3, 1.0), -0.4), rng.uniform(0.12, 0.18), 0.028, False)

def build_thorncage(name, seed, skin):
    b = Builder(name, seed); rng = b.rng
    D, H = 4.0, 2.2
    if skin == 2:  # kudzu-like hooked vines over a rock mound with a flat top
        for c, r in (((0.0, 0.0, -0.9), (0.95, 0.9, 1.0)), ((-0.15, 0.0, 0.8), (0.9, 0.95, 1.05)), ((0.0, 0.0, 0.0), (0.98, 0.7, 1.9))):
            lump(b, c, r, lambda n: "stone" if n.y < 0.6 else "moss", sub=2, jit=0.06, ymin=-EMBED, ymax=1.45)
        _mass(b, rng, D, H, ex=6.0, tint=lambda i, k, n: "thorn" if rng.random() < 0.85 else "leaf")
        _cage(b, rng, D, H, back=False)
        _canes_front(b, rng, D, H, 10, 26, 24)
        for j in range(6):  # hooked vine loops over the top
            x = rng.uniform(-0.9, 0.9); z0 = rng.uniform(-1.8, 1.0)
            cane(b, [(x, H - 0.1, z0), (x + 0.1, H + 0.05, z0 + 0.4), (x - 0.1, H - 0.05, z0 + 0.8)], 0.04, 0.025, 4)
        return b
    _mass(b, rng, D, H)
    _cage(b, rng, D, H)
    if skin == 1:  # hedge along a fallen trunk: its spine runs the length of the patch, showing at both ends
        b.tube([(0.1, 0.4, -2.15), (0.1, 0.52, -1.0), (0.1, 0.55, 1.0), (0.1, 0.45, 2.1)], [0.32, 0.4, 0.4, 0.3], 8, lambda i, k, n: "heartwood" if k == -1 or k == -2 else ("bark_alt" if k % 2 else "bark"), cap_start=True, cap_end=True)
    _canes_front(b, rng, D, H, 11 if skin == 0 else 9, 22 if skin == 0 else 20, 16)
    for j in range(8):  # side canes + a few hanging seed pods
        zz = -1.6 + 3.2 * j / 7; sx = 1 if j % 2 else -1
        cane(b, [(sx * (W - 0.02), 0.0, zz), (sx * (W + 0.0), 1.1, zz + 0.12), (sx * (W - 0.03), H - 0.05, zz)], 0.04, 0.03, 4)
    for j in range(2):
        lump(b, (rng.uniform(-0.6, 0.6), 1.9, -2.0 + 0.05), (0.07, 0.11, 0.07), lambda n: "pod", sub=1, jit=0.05)
    return b

def _wall_panel(b, rng, length, axis, n_canes, n_big):
    """Tangled cane wall panel 0.5 deep x 2.2 tall; axis 'x' = front panel (long axis x), 'z' = side panel (long axis z)."""
    half = length / 2
    def P(u, y, v): return (u, y, v) if axis == "x" else (v, y, u)
    rings = [[P(u, y, v) for u, v in ((-half, -0.2), (half, -0.2), (half, 0.2), (-half, 0.2))] for y in (-EMBED, 1.1, 2.12)]
    loft(b, rings, lambda i, k, n: "thorn" if i < 1 or rng.random() < 0.7 else "bark_alt")
    for j in range(n_canes):
        u = -half + 0.08 + (length - 0.16) * j / (n_canes - 1); v = rng.uniform(-0.2, 0.2)
        lean = rng.uniform(-0.12, 0.12); top = 2.2 + rng.uniform(-0.1, 0.06)
        pts = [P(u + lean * i / 3 + rng.uniform(-0.03, 0.03), top * i / 3 - EMBED * (1 - i / 3), v + rng.uniform(-0.05, 0.05)) for i in range(4)]
        cane(b, pts, 0.05, 0.03, 4)
    for j in range(n_big):
        u = rng.uniform(-half + 0.1, half - 0.1); y = rng.uniform(0.4, 2.1); v = rng.choice((-0.27, 0.27))
        d = (rng.uniform(-0.5, 0.5), rng.uniform(0.2, 0.9), 0.0)
        dd = Vector((d[0], d[1], 0.9 * (1 if v > 0 else -1)))
        base = P(u, y, v); dvec = (dd.x, dd.y, dd.z) if axis == "x" else (dd.z, dd.y, dd.x)
        thorn(b, base, dvec, rng.uniform(0.14, 0.24), 0.035, True)

def build_canewall(name, seed, axis):
    b = Builder(name, seed); rng = b.rng
    if axis == "x": _wall_panel(b, rng, 2.04, "x", 20, 28)
    else: _wall_panel(b, rng, 4.0, "z", 24, 26)
    return b

def build_rootcrown(name="Obst_RootCrown", seed=631):
    b = Builder(name, seed); rng = b.rng
    lump(b, (0.0, 0.4, 0.0), (0.5, 0.45, 0.5), lambda n: "bark_alt" if n.y < 0.3 else "bark", sub=2, jit=0.12, ymin=-EMBED)
    for j in range(6):
        a = 2 * math.pi * j / 6 + rng.uniform(-0.2, 0.2); L = rng.uniform(0.9, 1.3)
        pts = [(math.cos(a) * rr, y, math.sin(a) * rr) for rr, y in ((0.2, 0.6), (0.5, 0.35), (0.85, 0.1), (L, -EMBED + 0.02))]
        cane(b, pts, 0.17, 0.06, 5, lambda i, k, n: "bark" if k % 2 else "bark_alt")
    for j in range(10):  # upward cane fan, <= 2.5 m
        a = 2 * math.pi * j / 10 + rng.uniform(-0.2, 0.2); lean = rng.uniform(0.25, 0.7); h = rng.uniform(1.6, 2.4)
        pts = [(math.cos(a) * lean * t * h * 0.5, 0.6 + (h - 0.6) * t, math.sin(a) * lean * t * h * 0.5) for t in (0, 0.33, 0.66, 1.0)]
        cane(b, pts, 0.045, 0.02, 3)
    for j in range(8):
        a = rng.uniform(0, 6.28); y = rng.uniform(0.9, 1.9); r = 0.18 + (y - 0.6) * 0.1
        thorn(b, (math.cos(a) * r, y, math.sin(a) * r), (math.cos(a), 0.5, math.sin(a)), 0.15, 0.025, False)
    return b

def build_thornlitter(name="Obst_ThornLitter", seed=641):
    b = Builder(name, seed); rng = b.rng
    for j in range(4):
        a = rng.uniform(0, 3.14); L = rng.uniform(0.5, 0.9); x, z = rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5)
        b.tube([(x, 0.03, z), (x + math.cos(a) * L, 0.045, z + math.sin(a) * L)], [0.025, 0.018], 3, lambda i, k, n: "thorn" if k else "bark_alt", cap_end=True)
    for j in range(4):
        a = rng.uniform(0, 6.28)
        b.leaf((rng.uniform(-0.6, 0.6), 0.02, rng.uniform(-0.6, 0.6)), (math.cos(a), 0.0, math.sin(a)), 0.3, 0.2, 0.0, ["leaf", "moss", "leaf_lt", "leaf"][j])
    for j in range(2):
        lump(b, (rng.uniform(-0.5, 0.5), 0.0, rng.uniform(-0.5, 0.5)), (0.07, 0.05, 0.06), lambda n: "pod", sub=1, ymin=0.0)
    return b

# ------------------------------------------------------------------ registry
def builders():
    f = []
    f += [lambda: build_log("Obst_Log_Trunk", 2.04, 701), lambda: build_log("Obst_Log_TrunkB", 4.44, 702), lambda: build_log("Obst_Log_TrunkC", 6.84, 703)]
    f += [lambda: build_rootplate("Obst_RootPlate", 2.8, 2.3, 711), lambda: build_rootplate("Obst_RootPlateB", 2.3, 1.7, 712)]
    f += [build_stump, build_rockhump]
    f += [lambda: build_hangmat("Obst_HangMat", 721, "A"), lambda: build_hangmat("Obst_HangMatB", 722, "B"), lambda: build_hangmat("Obst_HangMatC", 723, "C")]
    f += [lambda: build_limb("Obst_Limb", 5.0, 731), lambda: build_limb("Obst_LimbB", 9.6, 732), build_liana_tie]
    f += [lambda: build_fin("Obst_ButtressFin", 741, 2, 0.07, "A"), lambda: build_fin("Obst_ButtressFinB", 742, 3, 0.06, "B"), lambda: build_fin("Obst_ButtressFinC", 743, 4, 0.07, "C")]
    f += [lambda: build_standing_stone("Obst_StandingStone", 751, False), lambda: build_standing_stone("Obst_StandingStoneB", 752, True), build_wedged_slab, build_rootweb]
    f += [lambda: build_barrel("Obst_BarrelBoulder", 761, False), lambda: build_barrel("Obst_BarrelBoulderB", 762, True), build_wallow, build_chock, build_furrow, build_sandbar]
    f += [lambda: build_thorncage("Obst_ThornCage", 771, 0), lambda: build_thorncage("Obst_ThornCageB", 772, 1), lambda: build_thorncage("Obst_ThornCageC", 773, 2)]
    f += [lambda: build_canewall("Obst_CaneWall", 781, "x"), lambda: build_canewall("Obst_CaneWallB", 782, "z"), build_rootcrown, build_thornlitter]
    return f
