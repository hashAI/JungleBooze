"""Headless (pip bpy) mock of the Run scene camera view, for look reviews without Unity.

    python3 run_mock.py <EnvironmentArt dir> <out.png> [--before] [--world jungle|dusk] [--samples N] [--z HERO_Z]

It mirrors the game's layout rules, so keep the constants below in sync with the C# defaults:
  - camera: FollowCameraView + RunnerPresentationConfig (3.2 m up, 6 m behind, look at 1.0 m high 8 m ahead,
    vertical FOV 60, portrait 660x950);
  - --before: the pre-look-pass GroundView (stretched Ground_PathTile per 12 m tile, lane dashes, ink edges,
    lawn-green verges, one tree/bush every 9 m per side), flat ambient, flat fog-colored background;
  - default (after): GroundView trail strip + jungle floor, Jungle_Wall* cluster segments per side
    (EnvironmentLookConfig), SkyView gradient dome + far canopy rings, trilight ambient, new key light.
Shading imitates URP Simple Lit without shadows: albedo x (trilight ambient + key color x intensity x N.L), backface
culled, then Unity linear fog by view depth. Coordinates: Unity (x, y, z) -> Blender (x, z, y). An FBX made by
tools/blender/glb_to_fbx.py shows up in Unity turned 180 degrees about the vertical axis compared to Blender, so every
model instance gets that extra turn here (yaw_blender = 180 - yaw_unity). Imported models have their transforms
applied before copies are placed (otherwise copies inherit the import rotation and lie on their side)."""
import math
import os
import sys

import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
SRC, OUT = ARGS[0], ARGS[1]
BEFORE = "--before" in ARGS


def arg(name, default):
    return type(default)(ARGS[ARGS.index(name) + 1]) if name in ARGS else default


SAMPLES = arg("--samples", 8)
HERO_Z = arg("--z", 60.0)
WORLD = arg("--world", "jungle")

# ---- Game constants (mirror of C#) ----
LANE_W, LANES, MARGIN = 2.4, 3, 0.6
PATH_HALF = LANES * LANE_W * 0.5 + MARGIN  # 4.2
CAM_UP, CAM_BEHIND, LOOK_AHEAD, LOOK_H, FOV = 3.2, 6.0, 8.0, 1.0, 60.0
FOG_START, FOG_END = 45.0, 90.0
FAR_CLIP = 160.0

# EnvironmentLookConfig defaults (after).
TRAIL_HALF, TRAIL_PERIOD = 5.4, 6.0
STRIP_BEHIND, STRIP_AHEAD = 18.0, 114.0
FLOOR_INNER, FLOOR_OUTER, FLOOR_PERIOD, FLOOR_Y = 4.6, 44.0, 6.0, -0.02
WALL_LEN, WALL_BEHIND, WALL_INSET = 12.0, 12.0, 0.0
WALL_NAMES = ["Jungle_WallA", "Jungle_WallB", "Jungle_WallC"]
WALL_HEIGHT_MIN, WALL_HEIGHT_SPAN = 0.92, 0.22
SKY_RADIUS = 150.0
KEY_PITCH, KEY_YAW, KEY_INTENSITY = 38.0, -35.0, 1.15
AMBIENT_SKY_MIX, AMBIENT_EQUATOR_MIX, AMBIENT_GROUND_INK = 0.5, 0.55, 0.35


def hexc(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def lin(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


THEMES = {
    "jungle": dict(key=hexc("FFD27A"), shadow=hexc("2E5B57"), horizon=hexc("FFE3A3"), top=hexc("FFB347"),
                   fog=hexc("E9C98A"), path=hexc("F3DFB2"), verge=hexc("2F7A3C"), lawn=hexc("3A8C3F")),
    "dusk": dict(key=hexc("FF9A5A"), shadow=hexc("2B3A67"), horizon=hexc("FF8C42"), top=hexc("2B3A67"),
                 fog=hexc("B0607A"), path=lerp(hexc("F3DFB2"), hexc("FF9A5A"), 0.35),
                 verge=lerp(hexc("2F7A3C"), hexc("2B3A67"), 0.45), lawn=hexc("3A8C3F")),
}
TH = THEMES[WORLD]
INK = hexc("1E1A24")


def unity_hash(index):
    """EnvironmentArt.Hash (C#), bit-exact."""
    m = (1 << 64) - 1
    x = (index * 0x9E3779B97F4A7C15) & m
    x ^= x >> 29
    x = (x * 0xBF58476D1CE4E5B9) & m
    x ^= x >> 32
    return x & 0xFFFFFFFF


def key_dir_unity(pitch, yaw):
    """Direction the light travels for Quaternion.Euler(pitch, yaw, 0) (Unity space)."""
    p, y = math.radians(pitch), math.radians(yaw)
    return (math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p))


def U(x, y, z):
    return Vector((x, z, y))


bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

if BEFORE:
    KEY = key_dir_unity(50.0, -30.0)
    KEY_I = 1.1
    flat = lerp(TH["shadow"], TH["horizon"], 0.55)
    AMB = (flat, flat, flat)
else:
    KEY = key_dir_unity(KEY_PITCH, KEY_YAW)
    KEY_I = KEY_INTENSITY
    AMB = (lerp(TH["horizon"], TH["top"], AMBIENT_SKY_MIX),
           lerp(TH["shadow"], TH["horizon"], AMBIENT_EQUATOR_MIX),
           lerp(TH["shadow"], INK, AMBIENT_GROUND_INK))
L_TO = Vector(U(*[-c for c in KEY])).normalized()  # toward the light, Blender space


def unity_lit(mat, albedo_socket_fn, tint=(1, 1, 1), fog=True, cull=True):
    """Rebuilds mat as: emission = albedo*tint*(ambient(N) + key*I*max(N.L,0)), fogged, backface culled."""
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.type not in ("TEX_IMAGE",):
            nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    alb = albedo_socket_fn(nt)
    tintn = nt.nodes.new("ShaderNodeMix"); tintn.data_type = "RGBA"; tintn.blend_type = "MULTIPLY"
    tintn.inputs["Factor"].default_value = 1.0
    nt.links.new(alb, tintn.inputs[6]); tintn.inputs[7].default_value = (*lin(tint), 1)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
    nt.links.new(geo.outputs["Normal"], dot.inputs[0]); dot.inputs[1].default_value = L_TO
    ndl = nt.nodes.new("ShaderNodeMath"); ndl.operation = "MAXIMUM"; ndl.inputs[1].default_value = 0.0
    nt.links.new(dot.outputs["Value"], ndl.inputs[0])
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(geo.outputs["Normal"], sep.inputs[0])
    up = nt.nodes.new("ShaderNodeMath"); up.operation = "MAXIMUM"; up.inputs[1].default_value = 0.0
    nt.links.new(sep.outputs["Z"], up.inputs[0])
    neg = nt.nodes.new("ShaderNodeMath"); neg.operation = "MULTIPLY"; neg.inputs[1].default_value = -1.0
    nt.links.new(sep.outputs["Z"], neg.inputs[0])
    dn = nt.nodes.new("ShaderNodeMath"); dn.operation = "MAXIMUM"; dn.inputs[1].default_value = 0.0
    nt.links.new(neg.outputs[0], dn.inputs[0])
    m1 = nt.nodes.new("ShaderNodeMix"); m1.data_type = "RGBA"
    nt.links.new(up.outputs[0], m1.inputs["Factor"]); m1.inputs[6].default_value = (*lin(AMB[1]), 1); m1.inputs[7].default_value = (*lin(AMB[0]), 1)
    m2 = nt.nodes.new("ShaderNodeMix"); m2.data_type = "RGBA"
    nt.links.new(dn.outputs[0], m2.inputs["Factor"]); nt.links.new(m1.outputs[2], m2.inputs[6]); m2.inputs[7].default_value = (*lin(AMB[2]), 1)
    keyc = nt.nodes.new("ShaderNodeMix"); keyc.data_type = "RGBA"; keyc.blend_type = "MULTIPLY"; keyc.inputs["Factor"].default_value = 1.0
    keyc.inputs[6].default_value = (*[c * KEY_I for c in lin(TH["key"])], 1)
    ndlc = nt.nodes.new("ShaderNodeCombineXYZ")
    for i in range(3):
        nt.links.new(ndl.outputs[0], ndlc.inputs[i])
    nt.links.new(ndlc.outputs[0], keyc.inputs[7])
    add = nt.nodes.new("ShaderNodeMix"); add.data_type = "RGBA"; add.blend_type = "ADD"; add.inputs["Factor"].default_value = 1.0
    nt.links.new(m2.outputs[2], add.inputs[6]); nt.links.new(keyc.outputs[2], add.inputs[7])
    lit = nt.nodes.new("ShaderNodeMix"); lit.data_type = "RGBA"; lit.blend_type = "MULTIPLY"; lit.inputs["Factor"].default_value = 1.0
    nt.links.new(tintn.outputs[2], lit.inputs[6]); nt.links.new(add.outputs[2], lit.inputs[7])
    col = lit.outputs[2]
    if fog:
        cam = nt.nodes.new("ShaderNodeCameraData")
        fr = nt.nodes.new("ShaderNodeMapRange"); fr.clamp = True
        fr.inputs[1].default_value = FOG_START; fr.inputs[2].default_value = FOG_END
        nt.links.new(cam.outputs["View Z Depth"], fr.inputs[0])
        fm = nt.nodes.new("ShaderNodeMix"); fm.data_type = "RGBA"
        nt.links.new(fr.outputs[0], fm.inputs["Factor"]); nt.links.new(col, fm.inputs[6]); fm.inputs[7].default_value = (*lin(TH["fog"]), 1)
        col = fm.outputs[2]
    em = nt.nodes.new("ShaderNodeEmission"); nt.links.new(col, em.inputs[0])
    if cull:
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(geo.outputs["Backfacing"], mix.inputs[0]); nt.links.new(em.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
    else:
        nt.links.new(em.outputs[0], out.inputs[0])


def image_albedo(path):
    def fn(nt):
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(path, check_existing=True)
        tex.interpolation = "Linear"
        return tex.outputs["Color"]
    return fn


def flat_albedo(color):
    def fn(nt):
        rgb = nt.nodes.new("ShaderNodeRGB"); rgb.outputs[0].default_value = (*lin(color), 1)
        return rgb.outputs[0]
    return fn


def existing_albedo(nt):
    texs = [n for n in nt.nodes if n.type == "TEX_IMAGE"]
    if texs:
        return texs[0].outputs["Color"]
    rgb = nt.nodes.new("ShaderNodeRGB"); rgb.outputs[0].default_value = (0.5, 0.5, 0.5, 1)
    return rgb.outputs[0]


def flat_mat(name, color, fog=True):
    m = bpy.data.materials.new(name); m.use_nodes = True
    unity_lit(m, flat_albedo(color), fog=fog)
    return m


BASE = {}


def load(name, tint=(1, 1, 1)):
    """Imports <name>.fbx once, applies transforms, relinks its texture (or <name>_basecolor.png), Unity-lit material."""
    if name in BASE:
        return BASE[name]
    path = os.path.join(SRC, name + ".fbx")
    if not os.path.exists(path):
        BASE[name] = None
        return None
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    for o in meshes:
        o.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name + "_src"
    png = os.path.join(SRC, name + "_basecolor.png")
    atlas = os.path.join(SRC, "Jungle_Atlas_basecolor.png")
    if name.startswith("Jungle_") and os.path.exists(atlas):
        png = atlas
    for i, slot in enumerate(o.material_slots):
        m = bpy.data.materials.new(name + "_m%d" % i); m.use_nodes = True
        unity_lit(m, image_albedo(png) if os.path.exists(png) else existing_albedo, tint)
        slot.material = m
    o.location = (0, 0, -500)
    BASE[name] = o
    return o


def place(name, ux, uy, uz, yaw=0.0, scale=(1, 1, 1)):
    """Places a copy like Unity would show it: position, yaw (deg, Unity), scale (Unity axes x, y, z)."""
    src = BASE.get(name) or load(name)
    if src is None:
        return None
    o = src.copy(); sc.collection.objects.link(o)
    o.location = U(ux, uy, uz)
    o.rotation_euler = (0, 0, math.radians(180.0 - yaw))
    o.scale = (scale[0], scale[2], scale[1])
    return o


def box(name, color, center, size, fog=True):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(*center))
    o = bpy.context.object; o.name = name; o.scale = (size[0], size[2], size[1])
    o.data.materials.append(flat_mat(name, color, fog))
    return o


def grid_mesh(name, xs, z0, z1, zstep, y, u_of_x, v_period, mat):
    """Flat strip (Unity x across xs, z from z0 to z1) with world-space UVs: u = u_of_x(x), v = z / v_period."""
    verts, faces, uvs = [], [], []
    zs = [z0 + i * zstep for i in range(int(round((z1 - z0) / zstep)) + 1)]
    for z in zs:
        for x in xs:
            verts.append(U(x, y, z))
    n = len(xs)
    for j in range(len(zs) - 1):
        for i in range(n - 1):
            a = j * n + i
            faces.append((a, a + 1, a + n + 1, a + n))
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new()
    for poly in me.polygons:
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            x, z = verts[vi].x, verts[vi].y
            uvl.data[li].uv = (u_of_x(x), z / v_period)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob)
    ob.data.materials.append(mat)
    return ob


# ---- Camera (FollowCameraView at hero x = 0, y = 0) ----
cam = bpy.data.cameras.new("cam"); cam.sensor_fit = "VERTICAL"; cam.angle = math.radians(FOV)
cam.clip_start = 0.3; cam.clip_end = FAR_CLIP
co = bpy.data.objects.new("cam", cam); sc.collection.objects.link(co); sc.camera = co
eye = U(0, CAM_UP, HERO_Z - CAM_BEHIND)
co.location = eye
co.rotation_euler = (U(0, LOOK_H, HERO_Z + LOOK_AHEAD) - eye).to_track_quat("-Z", "Y").to_euler()

world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (*lin(TH["fog"]), 1); bg.inputs[1].default_value = 1.0

# ---- Gameplay readability stand-ins (hero, coins, a low barrier) ----
hero_z = HERO_Z
box("HeroBody", hexc("EFE0BD"), (0, 0.75, hero_z), (0.5, 0.9, 0.35))
box("HeroSash", hexc("178F8A"), (0, 0.95, hero_z), (0.52, 0.12, 0.37))
box("HeroHair", hexc("2B1B14"), (0, 1.4, hero_z), (0.55, 0.45, 0.45))
box("HeroShadow", lerp(TH["path"], INK, 0.35), (0, 0.012, hero_z), (0.9, 0.002, 0.6))
load("Pickup_Coin")
for k in range(6):
    place("Pickup_Coin", -2.4, 1.0, hero_z + 10 + k * 2.5)
load("Obstacle_LowBarrier")
place("Obstacle_LowBarrier", 2.4, 0.3, hero_z + 22, scale=(2.0, 0.6, 1.0))
load("Obstacle_HighBarrier")
place("Obstacle_HighBarrier", 0.0, 1.9, hero_z + 40, scale=(2.2, 0.5, 0.4))

if BEFORE:
    # GroundView before the look pass.
    TILE = 12.0
    first = math.floor((hero_z - 12.0) / TILE)
    count = math.ceil((12.0 + FOG_END) / TILE) + 1
    for i in range(count):
        tz = (first + i + 0.5) * TILE
        place("Ground_PathTile", 0, 0, tz, scale=(PATH_HALF * 2, 1, TILE))
        for lane in range(3):
            box("Dash", lerp(TH["path"], INK, 0.25), ((lane - 1) * LANE_W, 0.01, tz), (0.12, 0.01, 4.0))
    for s in (-1, 1):
        box("Verge", TH["lawn"], (s * (PATH_HALF + 20), -0.11, hero_z + 60), (40, 0.2, 160))
        box("Edge", INK, (s * (PATH_HALF - 0.075), 0.01, hero_z + 60), (0.15, 0.01, 160))
    names = ["Foliage_TreeA", "Foliage_TreeB", "Foliage_Bush"]
    per_side = math.ceil(count * TILE / 9.0) + 1
    first = math.floor((hero_z - 12.0) / 9.0)
    for side in range(2):
        for n in range(per_side):
            index = first + n
            h = unity_hash(index * 2 + side)
            name = names[h % 3]
            inset = 1.5 + ((h >> 8) % 1000) / 1000.0 * 9.0
            x = (-1 if side == 0 else 1) * (PATH_HALF + inset)
            place(name, x, 0, (index + 0.5) * 9.0, yaw=float((h >> 20) % 360))
else:
    # Trail strip + jungle floor (GroundView, look pass).
    snap = math.floor(hero_z / TRAIL_PERIOD) * TRAIL_PERIOD
    trail_png = os.path.join(SRC, "Ground_Trail_basecolor.png")
    floor_png = os.path.join(SRC, "Ground_JungleFloor_basecolor.png")
    tm = bpy.data.materials.new("trail"); tm.use_nodes = True
    unity_lit(tm, image_albedo(trail_png), TH["path"])
    xs = [-TRAIL_HALF, -PATH_HALF, 0.0, PATH_HALF, TRAIL_HALF]
    grid_mesh("Trail", xs, snap - STRIP_BEHIND, snap + STRIP_AHEAD, TRAIL_PERIOD, 0.0,
              lambda x: (x + TRAIL_HALF) / (2 * TRAIL_HALF), TRAIL_PERIOD, tm)
    fm = bpy.data.materials.new("floor"); fm.use_nodes = True
    unity_lit(fm, image_albedo(floor_png), TH["verge"])
    for s in (-1, 1):
        xs = sorted([s * FLOOR_INNER, s * FLOOR_OUTER])
        grid_mesh("Floor", xs, snap - STRIP_BEHIND, snap + STRIP_AHEAD, TRAIL_PERIOD, FLOOR_Y,
                  lambda x: x / FLOOR_PERIOD, FLOOR_PERIOD, fm)

    # Jungle wall segments.
    walls = [n for n in WALL_NAMES if load(n) is not None]
    per_side = math.ceil((WALL_BEHIND + STRIP_AHEAD) / WALL_LEN) + 1
    first = math.floor((hero_z - WALL_BEHIND) / WALL_LEN)
    for side in range(2):
        for n in range(per_side):
            index = first + n
            h = unity_hash(index * 2 + side)
            name = walls[h % len(walls)]
            mirror = -1.0 if (h >> 4) & 1 else 1.0
            hs = WALL_HEIGHT_MIN + ((h >> 8) % 1000) / 1000.0 * WALL_HEIGHT_SPAN
            x = (1 if side == 1 else -1) * (PATH_HALF + WALL_INSET)
            place(name, x, 0, (index + 0.5) * WALL_LEN, yaw=0.0 if side == 1 else 180.0, scale=(1, hs, mirror))

    # SkyView: dome (vertex colors by elevation) + far canopy rings, unlit and unfogged, around the camera.
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import sky_shapes as SS
    for nm, (verts, faces, cols) in SS.build(TH).items():
        me = bpy.data.meshes.new(nm); me.from_pydata([eye + Vector((v[0], v[2], v[1])) for v in verts], [], faces)
        ca = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        for i, c in enumerate(cols):
            ca.data[i].color = (*lin(c), 1)
        ob = bpy.data.objects.new(nm, me); sc.collection.objects.link(ob)
        m = bpy.data.materials.new(nm); m.use_nodes = True; nt = m.node_tree
        for x in list(nt.nodes):
            nt.nodes.remove(x)
        a = nt.nodes.new("ShaderNodeVertexColor"); a.layer_name = "Col"
        e = nt.nodes.new("ShaderNodeEmission"); o2 = nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(a.outputs[0], e.inputs[0]); nt.links.new(e.outputs[0], o2.inputs[0])
        me.materials.append(m)

sc.render.engine = "CYCLES"
sc.cycles.samples = SAMPLES
sc.cycles.use_denoising = False
sc.cycles.max_bounces = 0
sc.cycles.transparent_max_bounces = 16
sc.cycles.device = "CPU"
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
sc.render.resolution_x, sc.render.resolution_y = 660, 950
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("wrote", OUT)
