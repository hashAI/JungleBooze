"""Headless (pip bpy): builds the merged jungle wall segments for the run's verges.

    python3 build_jungle_kit.py <models_dir> <out_dir>

Reads the fitted source GLBs in <models_dir> (Art/Environment/Models: Foliage_TreeA/TreeB/Bush/FernClump/BigLeaf/
CanopyTree, Prop_RockCluster), decimates them, packs their textures into one 1024 atlas, and composes three
12 m wall segments (near row of ferns/leaves/rocks/tufts at the path edge, mid row of trees, back row of tall canopy
trees and a dark foliage curtain, flat blob shadows under the plants). Each segment is ONE mesh with ONE material, so a
segment costs one draw call. Writes to <out_dir> (Resources/EnvironmentArt):
  Jungle_Atlas_basecolor.png            shared atlas (EnvironmentArt maps every Jungle_* model to it)
  Jungle_WallA.fbx / Jungle_WallB.fbx / Jungle_WallC.fbx

Segment convention (Unity space, what GroundView expects): local x = 0 is the path edge and the plants extend toward
+x (away from the path), y = 0 is the ground, z spans -6..+6 m (a few leaves overhang so neighbours overlap).
GroundView also checks the mesh bounds at runtime and turns the segment around if it extends the other way.
Deterministic: fixed seeds, no randomness at runtime."""
import math
import os
import random
import sys

import bpy
import bmesh  # noqa: E402 (bmesh is only importable after bpy)
import numpy as np
from mathutils import Matrix, Vector
from PIL import Image

MODELS, OUT = sys.argv[-2], sys.argv[-1]
ATLAS = 1024
CELL = 256
PAD = 6
SEGMENT = 12.0

# Source -> (atlas cell x, cell y, cells wide, cells high, decimate ratio). 4x4 grid of 256 px cells.
SOURCES = {
    "Foliage_TreeA": (0, 0, 2, 2, 0.5),
    "Foliage_TreeB": (2, 0, 2, 2, 0.42),
    "Foliage_CanopyTree": (0, 2, 2, 2, 0.45),
    "Foliage_Bush": (2, 2, 1, 1, 0.5),
    "Foliage_FernClump": (3, 2, 1, 1, 0.55),
    "Foliage_BigLeaf": (2, 3, 1, 1, 0.32),
    "Prop_RockCluster": (3, 3, 1, 0.75, 0.6),  # bottom quarter of this cell holds the flat swatches
}


def hexc(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# Flat swatches (sRGB), drawn in the strip under the rock texture. Shadow = jungle floor in shade.
SWATCHES = {
    "shadow": hexc("173A22"),
    "curtain_dark": hexc("12332F"),
    "curtain_mid": hexc("1A4A3C"),
    "curtain_light": hexc("27673F"),
    "grass_lit": hexc("5DA845"),
    "grass_dark": hexc("2F6E35"),
    "trunk": hexc("3A2E2A"),
    "ink": hexc("1E1A24"),
}


def cell_uv_rect(cx, cy, cw, ch):
    """Inner (padded) UV rect of a cell block; atlas v is bottom-up, cell rows are top-down."""
    x0, y0 = cx * CELL + PAD, cy * CELL + PAD
    x1, y1 = (cx + cw) * CELL - PAD, (cy + ch) * CELL - PAD
    return x0 / ATLAS, 1 - y1 / ATLAS, x1 / ATLAS, 1 - y0 / ATLAS, (int(x0), int(y0), int(x1), int(y1))


def build_atlas():
    import trimesh
    atlas = np.zeros((ATLAS, ATLAS, 3), np.uint8)
    for name, (cx, cy, cw, ch, _) in SOURCES.items():
        tex = trimesh.load(os.path.join(MODELS, name + ".glb"), force="mesh").visual.material.baseColorTexture.convert("RGB")
        _, _, _, _, (x0, y0, x1, y1) = cell_uv_rect(cx, cy, cw, ch)
        img = np.asarray(tex.resize((x1 - x0, y1 - y0), Image.LANCZOS))
        if name != "Prop_RockCluster":
            img = lush(img)
        # Edge-extend into the padding so mips do not bleed the neighbour's colors.
        img = np.pad(img, ((PAD, PAD), (PAD, PAD), (0, 0)), mode="edge")
        atlas[y0 - PAD:y1 + PAD, x0 - PAD:x1 + PAD] = img
    # Swatch strip: bottom quarter of cell (3, 3).
    sx0, sy0 = 3 * CELL, int(3.75 * CELL)
    sw = CELL // 4
    sh = CELL // 8
    uv = {}
    for i, (k, col) in enumerate(SWATCHES.items()):
        x = sx0 + (i % 4) * sw
        y = sy0 + (i // 4) * sh
        atlas[y:y + sh, x:x + sw] = col
        uv[k] = ((x + sw / 2) / ATLAS, 1 - (y + sh / 2) / ATLAS)
    Image.fromarray(atlas).save(os.path.join(OUT, "Jungle_Atlas_basecolor.png"), optimize=True)
    return uv


def lush(img):
    """Grades Meshy's teal-gray foliage toward the style guide's saturated jungle greens (hue pulled toward
    green, saturation x1.35, a touch brighter). Browns, trunks and flowers outside the green hues are untouched."""
    hsv = np.asarray(Image.fromarray(img).convert("HSV")).astype(np.float32)
    h, s_, v = hsv[..., 0] * 360 / 255, hsv[..., 1], hsv[..., 2]
    green = (h > 70) & (h < 200)
    h = np.where(green, h - 0.45 * (h - 118), h)
    s_ = np.where(green, np.minimum(255, s_ * 1.35 + 12), s_)
    v = np.where(green, np.minimum(255, v * 1.1), v)
    out = np.stack([h * 255 / 360, s_, v], -1).astype(np.uint8)
    return np.asarray(Image.fromarray(out, "HSV").convert("RGB"))


# Unity <-> Blender for an FBX written with axis_forward -Z, axis_up Y, baked space transform:
# Unity shows a Blender point g as A @ g.
A = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))
A_INV = A.inverted()


def unity_matrix(pos, yaw=0.0, scale=1.0, lean=0.0, sy=1.0):
    """Unity-space TRS: translate pos, yaw about Y (deg, +z toward +x), lean about Z (deg, + tips the top toward -x)."""
    t = Matrix.Translation(Vector(pos))
    y = math.radians(yaw)
    ry = Matrix(((math.cos(y), 0, math.sin(y), 0), (0, 1, 0, 0), (-math.sin(y), 0, math.cos(y), 0), (0, 0, 0, 1)))
    a = math.radians(lean)
    rz = Matrix(((math.cos(a), -math.sin(a), 0, 0), (math.sin(a), math.cos(a), 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
    s = Matrix.Diagonal((scale, scale * sy, scale, 1))
    return t @ ry @ rz @ s


def to_blender(m_unity):
    return A_INV @ m_unity @ A


def load_source(name):
    bpy.ops.object.select_all(action="DESELECT")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, name + ".glb"))
    meshes = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in [o for o in bpy.data.objects if o not in before and o.type != "MESH"]:
        bpy.data.objects.remove(o)
    for o in meshes:
        o.parent = None
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    cx, cy, cw, ch, ratio = SOURCES[name]
    mod = ob.modifiers.new("dec", "DECIMATE"); mod.ratio = ratio; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    u0, v0, u1, v1, _ = cell_uv_rect(cx, cy, cw, ch)
    uvl = ob.data.uv_layers.active.data
    for d in uvl:
        u, v = min(max(d.uv[0], 0.0), 1.0), min(max(d.uv[1], 0.0), 1.0)
        d.uv = (u0 + u * (u1 - u0), v0 + v * (v1 - v0))
    ob.data.materials.clear()
    ob.name = name + "_src"
    ob.hide_render = True
    return ob


def new_mesh_object(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def flat_uv(bm, uv):
    layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for loop in f.loops:
            loop[layer].uv = uv


def blob(rng, radius, squash, uvs):
    """Faceted foliage blob for the far curtain (icosphere, jittered, flat-shaded), in Unity-local units."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        j = 0.82 + rng.random() * 0.3
        v.co = Vector((v.co.x * j * radius * squash[0], v.co.y * j * radius * squash[1], v.co.z * j * radius * squash[2]))
    layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        # Up/sun-facing facets lighter, lower facets darker: painted bands (style guide 3.2).
        key = "curtain_light" if n.z > 0.55 else ("curtain_mid" if n.z > -0.1 else "curtain_dark")
        for loop in f.loops:
            loop[layer].uv = uvs[key]
    return bm


def disc(radius, uv, sides=10):
    bm = bmesh.new()
    verts = [bm.verts.new((math.cos(2 * math.pi * i / sides) * radius, math.sin(2 * math.pi * i / sides) * radius * 0.8, 0.0)) for i in range(sides)]
    bm.faces.new(verts)
    flat_uv(bm, uv)
    return bm


def tuft(rng, uvs):
    """Small grass tuft: five blades (one triangle each, both faces) with a dark base, ~0.35 m tall."""
    bm = bmesh.new()
    layer = bm.loops.layers.uv.verify()
    for i in range(5):
        a = rng.random() * math.pi
        h = 0.22 + rng.random() * 0.2
        w = 0.05 + rng.random() * 0.03
        lean = (rng.random() - 0.5) * 0.25
        dx, dy = math.cos(a) * w, math.sin(a) * w
        ox, oy = (rng.random() - 0.5) * 0.18, (rng.random() - 0.5) * 0.18
        tip = (ox + lean, oy + lean * 0.5, h)
        for order in (0, 1):  # two one-sided triangles (Unity culls back faces)
            v0 = bm.verts.new((ox - dx, oy - dy, 0)); v1 = bm.verts.new((ox + dx, oy + dy, 0)); v2 = bm.verts.new(tip)
            f = bm.faces.new((v0, v1, v2) if order == 0 else (v2, v1, v0))
            for loop in f.loops:
                loop[layer].uv = uvs["grass_lit"] if loop.vert is v2 else uvs["grass_dark"]
    return bm


def bm_object_unity(name, bm, m_unity_local):
    """bm is authored in Blender axes (z up) like an imported model; placed with a Unity-space matrix."""
    ob = new_mesh_object(name, bm)
    ob.matrix_world = to_blender(m_unity_local)
    return ob


def compose(variant, src, uvs):
    rng = random.Random({"A": 11, "B": 23, "C": 37}[variant])
    parts = []

    def put(name, x, z, scale, yaw=None, lean=0.0, sy=1.0, shadow=None):
        yaw = rng.random() * 360 if yaw is None else yaw
        ob = src[name].copy(); ob.data = src[name].data.copy(); ob.hide_render = False
        bpy.context.scene.collection.objects.link(ob)
        ob.matrix_world = to_blender(unity_matrix((x, 0, z), yaw, scale, lean, sy))
        parts.append(ob)
        if shadow:
            parts.append(bm_object_unity("shadow", disc(shadow * scale, uvs["shadow"]), unity_matrix((x, 0.03, z), rng.random() * 360)))

    def jit(a, b):
        return a + rng.random() * (b - a)

    # Path-edge tufts: real scale, right at the edge, never on the lanes.
    for i in range(14):
        z = -6 + (i + 0.2 + rng.random() * 0.6) * SEGMENT / 14
        x = jit(-0.15, 0.45) if i % 2 == 0 else jit(0.8, 3.5)
        parts.append(bm_object_unity("tuft", tuft(rng, uvs), unity_matrix((x, 0, z), rng.random() * 360, jit(0.9, 1.4) * (1 + x * 0.25))))

    # Each variant: near row at the path edge, one tall "edge giant" leaning slightly over the path margin (frames the
    # top corners of the portrait screen), a mid row of trees with bushes between the trunks, and a tall back tree.
    if variant == "A":
        put("Foliage_FernClump", jit(0.5, 0.9), -4.2, jit(0.9, 1.2), shadow=0.5)
        put("Foliage_BigLeaf", jit(1.2, 1.7), -1.4, jit(1.1, 1.4))
        put("Foliage_FernClump", jit(0.6, 1.1), 2.4, jit(1.0, 1.3), shadow=0.5)
        put("Prop_RockCluster", jit(0.5, 0.9), 5.0, jit(0.6, 0.8))
        put("Foliage_TreeB", 2.4, 0.6, 1.35, lean=7.0, shadow=0.7)
        put("Foliage_Bush", jit(3.0, 3.6), -3.6, jit(1.1, 1.4), shadow=0.8)
        put("Foliage_Bush", jit(4.6, 5.4), 3.4, jit(1.2, 1.5), shadow=0.8)
        put("Foliage_TreeA", jit(5.0, 5.8), -2.0, jit(1.35, 1.55), shadow=1.0)
        put("Foliage_CanopyTree", jit(8.5, 9.5), 2.0, jit(1.0, 1.15))
    elif variant == "B":
        put("Prop_RockCluster", jit(0.4, 0.8), -4.6, jit(0.7, 0.9))
        put("Foliage_FernClump", jit(0.6, 1.1), -1.8, jit(1.0, 1.3), shadow=0.5)
        put("Foliage_BigLeaf", jit(1.1, 1.6), 1.2, jit(1.0, 1.3))
        put("Foliage_FernClump", jit(0.5, 1.0), 4.4, jit(0.9, 1.2), shadow=0.5)
        put("Foliage_CanopyTree", 2.8, -3.0, 0.8, lean=6.0, shadow=0.9)
        put("Foliage_Bush", jit(3.2, 3.8), 3.0, jit(1.1, 1.4), shadow=0.8)
        put("Foliage_TreeB", jit(5.2, 6.0), 4.5, jit(1.15, 1.3), shadow=0.6)
        put("Foliage_Bush", jit(5.0, 5.8), 0.0, jit(1.2, 1.5), shadow=0.8)
        put("Foliage_TreeA", jit(7.5, 8.5), -1.0, jit(1.5, 1.7), shadow=1.0)
    else:
        put("Foliage_FernClump", jit(0.5, 0.9), -4.5, jit(1.0, 1.2), shadow=0.5)
        put("Foliage_CanopyTree", 2.6, -0.8, 0.95, lean=9.0, shadow=0.9)
        put("Foliage_BigLeaf", jit(1.0, 1.5), 2.0, jit(1.1, 1.4))
        put("Foliage_FernClump", jit(0.6, 1.1), 4.6, jit(1.0, 1.3), shadow=0.5)
        put("Foliage_Bush", jit(3.6, 4.2), 3.6, jit(1.1, 1.4), shadow=0.8)
        put("Foliage_Bush", jit(4.0, 4.8), -4.2, jit(1.1, 1.4), shadow=0.8)
        put("Foliage_TreeA", jit(6.0, 6.8), 2.0, jit(1.3, 1.5), shadow=1.0)
        put("Foliage_TreeB", jit(8.0, 9.0), -3.5, jit(1.2, 1.4), shadow=0.6)

    # Far curtain: tall dark faceted canopy masses that close the view behind the trees.
    for z in (-4.5, 0.0, 4.5):
        r = jit(3.8, 4.8)
        parts.append(bm_object_unity("curtain", blob(rng, r, (0.75, 1.0, 1.35), uvs),
                                     unity_matrix((jit(11.5, 13.0), r * 0.9, z + jit(-0.8, 0.8)), rng.random() * 360)))

    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.make_single_user(object=True, obdata=True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    name = "Jungle_Wall" + variant
    ob.name = name; ob.data.name = name
    mat = bpy.data.materials.get("Jungle_Atlas") or bpy.data.materials.new("Jungle_Atlas")
    ob.data.materials.clear(); ob.data.materials.append(mat)
    bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(ob.data); bm.free()
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             path_mode="STRIP", embed_textures=False, add_leaf_bones=False, apply_scale_options="FBX_SCALE_ALL",
                             global_scale=1.0, apply_unit_scale=True, axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE")
    xs = [(A @ ob.matrix_world @ v.co.to_4d()).x for v in ob.data.vertices]
    print(name, "tris", len(ob.data.polygons), "unity x range %.2f..%.2f" % (min(xs), max(xs)))
    bpy.data.objects.remove(ob)


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    uvs = build_atlas()
    src = {n: load_source(n) for n in SOURCES}
    for n, o in src.items():
        print(n, "decimated tris", len(o.data.polygons))
    for v in "ABC":
        compose(v, src, uvs)


main()
