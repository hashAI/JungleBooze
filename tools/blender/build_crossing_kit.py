"""Headless (pip bpy): procedural FIRST jungle-crossing kit. Zero API credits, deterministic (fixed seeds).
Pieces: Tree_Trunk, Tree_Branch, Vine_Liana, Vine_Tuft (the four slots EnvironmentArt.cs fills), plus
Foliage_FernCluster, and (v2) Prop_Rock, Prop_Root, Vine_Creeper (scenery slots; Vine_Creeper is not wired yet).
Usage: python3 build_crossing_kit.py <EnvironmentArt dir> [--glb-dir <dir>]
Writes <Name>.fbx + <Name>_basecolor.png (512 px palette atlas, flat colour + 3 hard bands, no painted outlines)
into <EnvironmentArt dir>. FBX is Y up, -Z forward, 1 unit = 1 m, transforms applied (same exporter flags as glb_to_fbx.py).
--glb-dir also writes <Name>.glb for the software contact sheet (tools/assetgen/env_preview.py).
Geometry is authored directly in Unity space (x right, y up, z forward; metres) and rotated into Blender space on build.
Conventions: Tree_Trunk origin = base centre; Tree_Branch local +X runs trunk -> path, origin at the trunk end;
Vine_Liana hangs along -Y from the origin at its top; Vine_Tuft origin = centre, flower faces -Z.
v2: Prop_Rock origin = base centre (about 1.3 x 0.7 x 1.1 m); Prop_Root origin = base centre, long axis X (2.0 x 0.5 x 0.5 m);
Vine_Creeper hangs along -Y from its top (5 m, 4 cm thick, dark green, no gold/orange).
v3: also builds the obstacle wave 1 pieces (Obst_*, see build_obstacle_wave1.py); --no-obstacles skips them."""
import math, os, random, sys
import bpy, bmesh
from mathutils import Vector

PAL = {  # name: (base, shadow). Palette from tools/assetgen/jungle_crossing_prompts.py PALETTES['jungle']; no hazard red.
    "bark":     ("#6B4A32", "#3E2A22"),
    "bark_alt": ("#57402E", "#2E2019"),
    "leaf":     ("#3A8C3F", "#1B4D4A"),
    "leaf_lt":  ("#8DC63F", "#3A8C3F"),
    "moss":     ("#5FA845", "#2A6B3C"),
    "liana":    ("#3A8C3F", "#1B4D4A"),
    "gold":     ("#FFC43D", "#C98A1E"),
    "orange":   ("#F28C28", "#B5651D"),
    "stone":    ("#8A7B68", "#4E463C"),   # v2 (same family as the gray-box rock colour)
    "creeper":  ("#2B6B3A", "#143D2A"),   # v2: thin dark-green creeper, darker than the live liana
    # v3 (obstacle wave 1, spec 005): rows are APPENDED so every earlier row keeps its index and colour.
    "soil":      ("#7A5C3E", "#3F2E21"),  # displaced soil, lips, wallow rim
    "pad":       ("#3A2F2A", "#1E1A24"),  # dark contact / bowl colour
    "sand":      ("#D9C08E", "#9C8456"),  # sand bar, scuffed gravel
    "heartwood": ("#E6D6B0", "#B39B6E"),  # splintered break (pale wood)
    "thorn":     ("#3E4A2E", "#1F2A18"),  # thorn green (spec 005 section 9)
    "ink":       ("#1E1A24", "#0F0D12"),  # style guide ink: thorn tips, mark flanks
    "ochre":     ("#D7263D", "#8F1A2A"),  # HAZARD RED, red-ochre trail marks ONLY (spec 005 G8), always flanked by ink
    "pod":       ("#B09A62", "#6E5C36"),  # seed pods (non-hazard colour)
}
ROWS = list(PAL)
CW, CH, NCOL, NROW = 64, 16, 8, 32  # 512 px atlas: row (16 px, 32 rows since v3) = colour, column (64 px) = shade (0 shadow band, 1 base, 2 light band)
W_ATLAS = CW * NCOL

def hexc(h): return tuple(int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))
def lerp(a, b, t): return tuple(x + (y - x) * t for x, y in zip(a, b))

def shade_color(name, s):
    base, shadow = map(hexc, PAL[name])
    if s == 0: return lerp(base, shadow, 0.55)
    if s == 1: return base
    return lerp(base, hexc("#FFE9A0"), 0.22)

_ATLAS = None
def atlas_pixels():
    global _ATLAS
    if _ATLAS is None:
        W = W_ATLAS; rows = []
        for y in range(W):
            r = y // CH; line = []
            for x in range(W):
                s = x // CW
                c = shade_color(ROWS[r], s) if r < len(ROWS) and s < 3 else (0.5, 0.5, 0.5)
                line.extend((c[0], c[1], c[2], 1.0))
            rows.append(line)
        _ATLAS = [v for line in rows for v in line]
    return _ATLAS

LIGHT = Vector((0.35, 0.75, 0.55)).normalized()  # key from above-front (Unity space)

class Builder:
    def __init__(self, name, seed):
        self.name, self.rng = name, random.Random(seed)
        self.bm = bmesh.new(); self.col = self.bm.faces.layers.int.new("col")
        self.cur = "bark"

    def tag(self, faces, colour):
        for f in faces: f[self.col] = ROWS.index(colour)

    def tube(self, pts, radii, sides, colfn, cap_end=False, cap_start=False, phase=0.0):
        """Tapered tube along pts. colfn(ring, side, normal) -> colour name. Faces wind outward."""
        bm, rings, up = self.bm, [], Vector((0, 1, 0))
        for i, (p, r) in enumerate(zip(pts, radii)):
            p = Vector(p)
            t = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
            ref = up if abs(t.dot(up)) < 0.9 else Vector((1, 0, 0))
            b1 = t.cross(ref).normalized(); b2 = t.cross(b1).normalized()
            rings.append(([bm.verts.new(p + (b1 * math.cos(a) + b2 * math.sin(a)) * r)
                           for a in (phase + 2 * math.pi * k / sides for k in range(sides))], p))
        for i in range(len(rings) - 1):
            for k in range(sides):
                vs = [rings[i][0][k], rings[i][0][(k + 1) % sides], rings[i + 1][0][(k + 1) % sides], rings[i + 1][0][k]]
                try: f = bm.faces.new(vs)
                except ValueError: continue
                f.normal_update(); c = f.calc_center_median() - rings[i][1]
                if f.normal.dot(c) < 0: f.normal_flip()
                f[self.col] = ROWS.index(colfn(i, k, f.normal.copy()))
        for ring, end in ((rings[-1], cap_end), (rings[0], cap_start)):
            if end:
                f = bm.faces.new(ring[0]); f.normal_update()
                if (f.normal.dot(Vector(pts[-1]) - Vector(pts[0])) > 0) != (ring is rings[-1]): f.normal_flip()
                f[self.col] = ROWS.index(colfn(len(rings) - 1, 0, f.normal.copy()))

    def blob(self, centre, scale, colour, jitter=0.12):
        """Low-poly leaf mass: icosphere (80 tris), squashed and seeded-jittered."""
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=1.0)
        for v in res["verts"]:
            v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
            v.co *= 1 + self.rng.uniform(-jitter, jitter)
            v.co += Vector(centre)
        faces = list({f for v in res["verts"] for f in v.link_faces})
        self.tag(faces, colour)

    def leaf(self, base, direction, length, width, droop, colour, midf=0.45):
        """Double-sided bent leaf (8 tris): base, two side verts, ridge-mid, tip."""
        bm = self.bm; d = Vector(direction).normalized()
        side = d.cross(Vector((0, 1, 0)))
        if side.length < 1e-3: side = Vector((1, 0, 0))
        side.normalize(); nrm = side.cross(d).normalized()
        b = Vector(base); mid = b + d * length * midf + nrm * 0.02
        tip = b + d * length + Vector((0, -droop, 0)); l = mid + side * width * 0.5; r = mid - side * width * 0.5
        mid_hi = mid + nrm * width * 0.12
        pp = (b, l, mid_hi, r, tip)
        vs = [bm.verts.new(p) for p in pp]; vb = [bm.verts.new(p - nrm * 0.01) for p in pp]  # separate verts for the back side
        front = [(0, 1, 2), (0, 2, 3), (1, 4, 2), (2, 4, 3)]
        out = []
        for tri in front:
            out.append(bm.faces.new([vs[i] for i in tri]))
            out.append(bm.faces.new([vb[i] for i in reversed(tri)]))
        for f in out: f.normal_update()
        self.tag(out, colour)

    def finish(self, d, glb_dir):
        bm = self.bm
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            f.normal_update(); n = f.normal
            dp = n.dot(LIGHT); s = 2 if dp > 0.38 else (1 if dp > -0.12 else 0)
            row = f[self.col]
            u, v = (s + 0.5) / NCOL, (row + 0.5) / NROW
            for lp in f.loops: lp[uv].uv = (u, v)
        tris = len(bm.faces)
        rot = Vector  # Unity (x,y,z) -> Blender (x,-z,y): same convention as glTF import, so FBX axes land Y up / -Z forward
        for v in bm.verts: v.co = Vector((v.co.x, -v.co.z, v.co.y))
        mesh = bpy.data.meshes.new(self.name); bm.to_mesh(mesh); bm.free()
        ob = bpy.data.objects.new(self.name, mesh); bpy.context.scene.collection.objects.link(ob)
        for p in mesh.polygons: p.use_smooth = False
        png = os.path.join(d, self.name + "_basecolor.png")
        img = bpy.data.images.new(self.name + "_basecolor", W_ATLAS, W_ATLAS)
        img.pixels = atlas_pixels(); img.filepath_raw = png; img.file_format = "PNG"; img.save()
        mat = bpy.data.materials.new(self.name); mat.use_nodes = True
        nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]; tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img; tex.interpolation = "Closest"; nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 1.0
        mesh.materials.append(mat)
        for o in bpy.context.scene.objects: o.select_set(o is ob)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, self.name + ".fbx"), use_selection=True, object_types={"MESH"},
            path_mode="COPY", embed_textures=True, add_leaf_bones=False, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
            apply_unit_scale=True, axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE")
        if glb_dir:
            os.makedirs(glb_dir, exist_ok=True)
            bpy.ops.export_scene.gltf(filepath=os.path.join(glb_dir, self.name + ".glb"), export_format="GLB", use_selection=True)
        zs = [v.co.z for v in mesh.vertices]; xs = [v.co.x for v in mesh.vertices]; ys = [v.co.y for v in mesh.vertices]
        print(f"ok {self.name}: {tris} tris, X[{min(xs):.2f},{max(xs):.2f}] Y(unity Z)[{min(ys):.2f},{max(ys):.2f}] "
              f"unity-height [{min(zs):.2f},{max(zs):.2f}]")
        return tris

# ---------------------------------------------------------------- pieces
def build_trunk():
    b = Builder("Tree_Trunk", 101); rng = b.rng
    ys = [0, 2, 5, 9, 14, 20, 27, 34]
    pts, rad = [], []
    for y in ys:
        t = y / 34.0
        r = (1.55 if y == 0 else 1.5 - 0.55 * t) * (1 + rng.uniform(-0.06, 0.06))
        pts.append((0.35 * math.sin(t * 3.1) , y, 0.25 * math.sin(t * 2.3 + 1)))  # slight lean/wobble
        rad.append(r)
    pts[0] = (0, 0, 0)
    b.tube(pts, rad, 10, lambda i, k, n: "bark" if (k + (i % 2)) % 2 == 0 else "bark_alt", cap_end=True)
    # buttress roots: six flared fins sunk into the ground, splaying to 3.5 m
    for j in range(6):
        a = 2 * math.pi * j / 6 + rng.uniform(-0.2, 0.2); dx, dz = math.cos(a), math.sin(a)
        rp, rr, n = [], [], 5
        for i in range(n):
            t = i / (n - 1); rd = 0.8 + 2.8 * t; y = 5.5 * (1 - t) ** 2 - 0.3 * t
            rp.append((dx * rd, y, dz * rd)); rr.append(0.8 - 0.4 * t)
        b.tube(rp, rr, 5, lambda i, k, nn: "bark_alt" if nn.y < 0.2 else "bark", phase=a)
    # leaf canopy cluster on top (the trunk top is hidden inside it)
    top = pts[-1]
    for j in range(11):
        a = 2 * math.pi * j / 11 + rng.uniform(-0.2, 0.2); rd = rng.uniform(2.2, 3.4)
        c = (top[0] + math.cos(a) * rd, rng.uniform(32.0, 35.0), top[2] + math.sin(a) * rd)
        b.blob(c, (rng.uniform(1.9, 2.6), rng.uniform(1.2, 1.6), rng.uniform(1.9, 2.6)), ["leaf", "leaf", "moss"][j % 3])
    for c, s, col in (((top[0], 36.4, top[2]), (3.0, 1.7, 3.0), "leaf_lt"), ((top[0] + 1.2, 37.4, top[2] - 0.8), (1.9, 1.2, 1.9), "leaf_lt"),
                      ((top[0] - 1.5, 36.2, top[2] + 1.4), (1.8, 1.1, 1.8), "moss")):
        b.blob(c, s, col)
    return b

def build_branch():
    b = Builder("Tree_Branch", 202); rng = b.rng
    xs = [-0.3, 0.5, 1.5, 2.7, 4.0, 5.4, 6.8, 8.0]
    pts = [(x, 0.4 * math.sin(math.pi / 2 * max(x, 0) / 8.0), 0.0) for x in xs]
    rad = [1.0, 0.9, 0.82, 0.74, 0.66, 0.6, 0.54, 0.5]
    b.tube(pts, rad, 8, lambda i, k, n: "moss" if n.y > 0.75 and i > 0 else ("bark_alt" if k % 2 else "bark"), cap_end=True)
    # leaf tufts on top and sides, kept off the underside so the vine knot stays readable
    for x, dy, dz, s, col in ((1.4, 0.75, 0.3, 0.8, "leaf"), (3.0, 0.8, -0.35, 0.95, "moss"), (4.6, 0.7, 0.4, 0.8, "leaf"),
                              (6.0, 0.65, -0.3, 0.7, "leaf_lt"), (2.2, 0.15, 0.9, 0.6, "leaf"), (5.2, 0.1, -0.85, 0.55, "moss")):
        y = 0.4 * math.sin(math.pi / 2 * x / 8.0)
        b.blob((x, y + dy, dz), (s * 1.1, s * 0.7, s), col)
    return b

def build_liana():
    b = Builder("Vine_Liana", 303); rng = b.rng
    n, L = 22, 6.0; pts, rad = [], []
    for i in range(n):
        t = i / (n - 1)
        pts.append((0.2 * math.sin(4 * math.pi * t), -L * t, 0.12 * (1 - math.cos(4 * math.pi * t)) * 0.5))
        rad.append(0.11 - 0.035 * min(1, t * 6) if t < 0.2 else 0.075 - 0.01 * t)
    b.tube(pts, rad, 6, lambda i, k, nn: "leaf_lt" if (k + i) % 3 == 0 else "liana", cap_end=True)
    for j, t in enumerate((0.12, 0.3, 0.46, 0.62, 0.78, 0.9)):
        p = pts[int(t * (n - 1))]; a = j * 2.4 + 0.5
        b.leaf(p, (math.cos(a), 0.1, math.sin(a)), 0.5, 0.3, 0.14, "leaf" if j % 2 else "moss")
    return b

def build_tuft():
    b = Builder("Vine_Tuft", 404); rng = b.rng
    for j in range(7):  # gold leaves splay behind and around the flower (the flower faces -Z)
        a = 2 * math.pi * j / 7
        b.leaf((0, 0.02, 0.04), (math.cos(a) * 0.9, math.sin(a) * 0.9, 0.55), 0.27, 0.24, 0.05, "gold", midf=0.6)
    for j in range(8):  # orange flower, petals face -Z
        a = 2 * math.pi * j / 8; a2 = 2 * math.pi * (j + 1) / 8; am = (a + a2) / 2
        c = Vector((0, 0, -0.1))
        p0 = c + Vector((math.cos(a), math.sin(a), 0)) * 0.07; p1 = c + Vector((math.cos(a2), math.sin(a2), 0)) * 0.07
        tip = c + Vector((math.cos(am), math.sin(am), -0.04)) * 0.17
        v = [b.bm.verts.new(p) for p in (p0, tip, p1)]
        v2 = [b.bm.verts.new(p + Vector((0, 0, 0.01))) for p in (p0, tip, p1)]
        fs = [b.bm.faces.new(v), b.bm.faces.new(v2[::-1])]
        for f in fs: f.normal_update()
        b.tag(fs, "orange")
    res = bmesh.ops.create_cone(b.bm, cap_ends=True, segments=6, radius1=0.07, radius2=0.0, depth=0.07,
                                matrix=__import__("mathutils").Matrix.Translation((0, 0, -0.14)) @ __import__("mathutils").Matrix.Rotation(math.pi, 4, "X"))
    fcs = list({f for v in res["verts"] for f in v.link_faces}); b.tag(fcs, "gold")
    return b

def build_fern():
    b = Builder("Foliage_FernCluster", 505); rng = b.rng
    for j in range(9):
        a = 2 * math.pi * j / 9 + rng.uniform(-0.2, 0.2)
        b.leaf((0, 0.05, 0), (math.cos(a), 0.7 + 0.5 * (j % 3) / 2, math.sin(a)), rng.uniform(1.0, 1.4), 0.4, 0.45, ["leaf", "leaf_lt", "moss"][j % 3])
    return b

def build_rock():
    """Mossy boulder cluster: three squashed icospheres (one big, two small) sunk flat at y=0, moss on the upward faces."""
    b = Builder("Prop_Rock", 606); rng = b.rng
    for c, sc, jit in (((0.0, 0.0, 0.0), (0.62, 0.62, 0.52), 0.14), ((0.5, 0.0, -0.32), (0.4, 0.38, 0.36), 0.16),
                       ((-0.42, 0.0, 0.38), (0.3, 0.28, 0.3), 0.16)):
        res = bmesh.ops.create_icosphere(b.bm, subdivisions=2, radius=1.0)
        for v in res["verts"]:
            v.co = Vector((v.co.x * sc[0], v.co.y * sc[1], v.co.z * sc[2])) * (1 + rng.uniform(-jit, jit))
            v.co += Vector(c); v.co.y = max(v.co.y, 0.0)  # flat base on the ground; origin = base centre
        faces = list({f for v in res["verts"] for f in v.link_faces})
        for f in faces:
            f.normal_update(); f[b.col] = ROWS.index("moss" if f.normal.y > 0.72 else "stone")
    for j, (x, z, a) in enumerate(((0.05, 0.05, 0.4), (0.5, -0.3, 2.0), (-0.4, 0.4, 3.6), (0.55, -0.1, 5.2))):
        y = 0.52 if j == 0 else (0.36 if j in (1, 3) else 0.26)
        b.leaf((x, y, z), (math.cos(a), 0.5, math.sin(a)), 0.28, 0.16, 0.08, ["leaf", "leaf_lt", "leaf", "moss"][j])
    return b

def build_root():
    """Arching tree root along X (2.0 m long, 0.5 m high): flared feet on the ground, moss on top, leaf tufts, one side rootlet."""
    b = Builder("Prop_Root", 707); rng = b.rng
    n = 11; pts, rad = [], []
    for i in range(n):
        t = i / (n - 1); e = abs(2 * t - 1)  # 1 at the feet, 0 at the crown
        r = 0.105 + 0.1 * e ** 2.2
        pts.append((-1.0 + 2.0 * t, r + (0.5 - 0.105 - 0.105 - 0.0) * math.sin(math.pi * t) ** 0.8 * 0.95 - 0.0, 0.05 * math.sin(2 * math.pi * t)))
        rad.append(r)
    b.tube(pts, rad, 6, lambda i, k, nn: "moss" if nn.y > 0.7 else ("bark_alt" if nn.y < 0.0 or k % 3 == 0 else "bark"), cap_start=True, cap_end=True)
    rp = [(-0.88, 0.1, 0.1), (-0.95, 0.09, 0.3), (-1.0, 0.07, 0.5)]  # side rootlet on the left foot
    b.tube(rp, [0.07, 0.05, 0.035], 5, lambda i, k, nn: "bark_alt", cap_end=True)
    for j, (t, side) in enumerate(((0.2, 1), (0.38, -1), (0.55, 1), (0.74, -1), (0.9, 1), (0.5, -1))):
        p = pts[int(t * (n - 1))]; a = math.pi / 2 * side + rng.uniform(-0.5, 0.5)
        b.leaf((p[0], p[1] + 0.06, p[2]), (math.cos(a) * 0.5, 0.9, math.sin(a)), 0.3, 0.18, 0.1, ["leaf", "leaf_lt", "moss"][j % 3])
    return b

def build_creeper():
    """Thin non-grabbable creeper: 5 m, 4 cm thick, dark green, five small sparse leaves, no tuft, no gold/orange."""
    b = Builder("Vine_Creeper", 808); rng = b.rng
    n, L = 18, 5.0; pts, rad = [], []
    for i in range(n):
        t = i / (n - 1)
        pts.append((0.12 * math.sin(5 * math.pi * t), -L * t, 0.08 * math.sin(3 * math.pi * t + 1)))
        rad.append(0.028 - 0.01 * t)
    b.tube(pts, rad, 4, lambda i, k, nn: "creeper", cap_end=True)
    for j, t in enumerate((0.18, 0.4, 0.58, 0.76, 0.92)):
        p = pts[int(t * (n - 1))]; a = j * 2.4 + 1.0
        b.leaf(p, (math.cos(a), 0.15, math.sin(a)), 0.26, 0.14, 0.07, "creeper" if j % 2 else "leaf")
    return b

def main():
    d = sys.argv[1]; glb = sys.argv[sys.argv.index("--glb-dir") + 1] if "--glb-dir" in sys.argv else None
    os.makedirs(d, exist_ok=True); total = {}
    fns = [build_trunk, build_branch, build_liana, build_tuft, build_fern, build_rock, build_root, build_creeper]
    if "--no-obstacles" not in sys.argv:  # v3: obstacle wave 1 (spec 005), same atlas, same exporter
        import build_obstacle_wave1 as w1
        fns += w1.builders()
    for fn in fns:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        b = fn(); total[b.name] = b.finish(d, glb)
    print(total)

if __name__ == "__main__":
    main()
