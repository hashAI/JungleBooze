"""Previews for the Expedition forest kit (Blender Cycles, warm F4_f-like light, painted trail ground).

lineup <out.jpg> <game.blend> [<game.blend> ...] [--row M] [--h PX]
    every *_LOD0 mesh of the given game files in rows (front faces toward the camera), a 1.65 m Pista proxy at the
    start of each row, labels; plus a grayscale copy (<out>_gray.jpg) for the value check (AD s3.4).
lods <out.jpg> <game.blend>   LOD0/1/2 of each piece side by side (wire-free), labels.
Env: PV_SAMPLES (default 32).
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402

TRAIL = os.path.join(E.UNITY_ENV, 'Forest', 'Textures', 'TR_TrailGround_P_BaseColor.png')


def setup(W, H, samples=None):
    sc = E.gpu_cycles(int(os.environ.get('PV_SAMPLES', samples or 32)))
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    sc.render.resolution_x, sc.render.resolution_y = W, H
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    sky = nt.nodes.new('ShaderNodeTexGradient')
    sky.gradient_type = 'LINEAR'
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Rotation'].default_value = (0, -math.pi / 2, 0)
    nt.links.new(tc.outputs['Generated'], mp.inputs[0])
    nt.links.new(mp.outputs[0], sky.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.85, 0.72, 0.5, 1)
    ramp.color_ramp.elements[1].color = (0.32, 0.55, 0.78, 1)
    nt.links.new(sky.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], nt.nodes['Background'].inputs[0])
    nt.nodes['Background'].inputs[1].default_value = 0.9
    L = bpy.data.lights.new('sun', 'SUN')
    L.energy = 4.2
    L.angle = math.radians(2.0)
    L.color = (1.0, 0.86, 0.66)
    so = bpy.data.objects.new('sun', L)
    sc.collection.objects.link(so)
    return sc, so


def ground_mat():
    gm = bpy.data.materials.new('trail')
    gm.use_nodes = True
    nt = gm.node_tree
    img = nt.nodes.new('ShaderNodeTexImage')
    img.image = bpy.data.images.load(TRAIL)
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (0.25, 0.25, 0.25)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    nt.links.new(tc.outputs['Object'], mp.inputs[0])
    nt.links.new(mp.outputs[0], img.inputs[0])
    nt.links.new(img.outputs[0], nt.nodes['Principled BSDF'].inputs['Base Color'])
    nt.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.92
    return gm


WATERY = ('Float', 'Debris', 'Snag', 'LowBranch_Water', 'RiverRock', 'SteppingRock', 'Riverbank', 'WaterEdge',
          'Driftwood')


def water_mat():
    m = bpy.data.materials.new('water')
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.03, 0.25, 0.2, 1)
    b.inputs['Roughness'].default_value = 0.08
    b.inputs['Alpha'].default_value = 0.72
    m.blend_method = 'BLEND'
    return m


def proxy(loc):
    """Pista stand-in: 1.65 m, white/orange/charcoal like her outfit, so scale and value read."""
    objs = []
    for z, r, h, col in ((0.42, 0.14, 0.84, (0.08, 0.08, 0.09)), (1.12, 0.19, 0.6, (0.85, 0.82, 0.76)),
                         (1.52, 0.12, 0.26, (0.55, 0.36, 0.25))):
        bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=h, location=(loc[0], loc[1], z))
        o = bpy.context.active_object
        m = bpy.data.materials.new('px')
        m.use_nodes = True
        m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = col + (1,)
        o.data.materials.append(m)
        objs.append(o)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.12, location=(loc[0] - 0.19, loc[1] + 0.02, 1.25))
    o = bpy.context.active_object
    m = bpy.data.materials.new('px_or')
    m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.94, 0.3, 0.05, 1)
    o.data.materials.append(m)
    objs.append(o)
    return objs


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def load_lod(paths, lod='_LOD0'):
    out = []
    for p in paths:
        with bpy.data.libraries.load(p) as (src, dst):
            dst.objects = [n for n in src.objects if n.endswith(lod)]
        for o in dst.objects:
            if o is not None and o.type == 'MESH':
                E.link(o)
                out.append(o)
    return out


def label(img_path, items, cam, sc, out, font_px=22):
    from bpy_extras.object_utils import world_to_camera_view
    from PIL import Image, ImageDraw, ImageFont
    im = Image.open(img_path).convert('RGB')
    d = ImageDraw.Draw(im)
    try:
        f = ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf', font_px)
    except OSError:
        f = ImageFont.load_default()
    W, H = im.size
    for text, p in items:
        v = world_to_camera_view(sc, cam, Vector(p))
        x, y = v.x * W, (1 - v.y) * H
        tw = d.textlength(text, font=f)
        d.rectangle([x - tw / 2 - 4, y - 2, x + tw / 2 + 4, y + font_px + 4], fill=(20, 20, 20))
        d.text((x - tw / 2, y), text, fill=(240, 235, 220), font=f)
    im.save(out, quality=90)
    g = im.convert('L').convert('RGB')
    g.save(out.replace('.jpg', '_gray.jpg'), quality=88)


def lineup(out, paths, row_w=34.0, H=1100):
    E.reset()
    objs = load_lod(paths)
    objs.sort(key=lambda o: o.name)
    W = int(H * 1.9)
    sc, sun = setup(W, H)
    x, y, rowh = 0.0, 0.0, 0.0
    items, starts = [], []
    gap = 1.6
    for o in objs:
        lo, hi = bounds([o])
        w = hi.x - lo.x
        if x > 0 and x + w > row_w:
            x, y = 0.0, y - rowh - 4.0
            rowh = 0.0
        if x == 0.0:
            starts.append((x - 1.2, y))
        o.location = (x - lo.x + 0.6, y - hi.y, (1.0 - lo.z) if o.name.startswith('PK_') else 0)
        items.append((o.name.replace('_LOD0', ''), (x + w / 2 + 0.6, y - hi.y + hi.y + 0.4, lo.z - 0.0)))
        rowh = max(rowh, hi.y - lo.y)
        x += w + gap
    for s in starts:
        proxy((s[0], s[1] - 0.4))
    lo, hi = bounds(objs)
    gmat, wmat = ground_mat(), water_mat()
    for o in objs:
        olo, ohi = bounds([o])
        wet = any(k in o.name for k in WATERY)
        if olo.z < -0.8 and not wet:
            continue                                  # gap lips: their own top is the floor
        bpy.ops.mesh.primitive_plane_add(size=1, location=((olo.x + ohi.x) / 2, (olo.y + ohi.y) / 2, 0))
        g = bpy.context.active_object
        g.scale = ((ohi.x - olo.x) + 1.4, (ohi.y - olo.y) + 1.4, 1)
        g.data.materials.append(wmat if wet else gmat)
    bpy.ops.mesh.primitive_plane_add(size=1, location=((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, -9.0))
    g = bpy.context.active_object
    g.scale = ((hi.x - lo.x) + 60, (hi.y - lo.y) + 60, 1)
    g.data.materials.append(gmat)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 50
    ctr = (lo + hi) / 2
    span = max(hi.x - lo.x, (hi.y - lo.y) * 1.9)
    d = span * 0.92
    cam.location = Vector((ctr.x - 0.1 * d, hi.y + d * 0.62, d * 0.42))
    cam.rotation_euler = (Vector((ctr.x, ctr.y, 0.8)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sun.rotation_euler = (math.radians(50), 0, math.radians(200))
    for i, (t, p) in enumerate(items):
        items[i] = (t, (p[0], p[1] + 0.8, 0.0))
    tmp = out + '.png'
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    label(tmp, items, cam, sc, out)
    os.remove(tmp)
    print('wrote', out)


def lods(out, path, H=700):
    E.reset()
    rows = []
    for i in range(3):
        rows.append(load_lod([path], f'_LOD{i}'))
    names = sorted({o.name.rsplit('_LOD', 1)[0] for o in rows[0]})
    sc, sun = setup(int(H * 2.2), H)
    x = 0.0
    items = []
    for nm in names:
        objs = [next(o for o in rows[i] if o.name == f'{nm}_LOD{i}') for i in range(3)]
        lo, hi = bounds([objs[0]])
        w = hi.x - lo.x
        for i, o in enumerate(objs):
            o.location = (x - lo.x, -i * ((hi.y - lo.y) + 1.5) * 0 + 0, 0)
            o.location.x += i * (w + 0.4) * 0
            o.location.y = -i * 0.0
            o.location.z = 0
            o.location.x = x - lo.x + i * (w + 0.5)
        items.append((nm.replace('OB_', '').replace('TR_', '').replace('PK_', ''), (x + 1.5 * w, hi.y + 0.6, 0)))
        x += 3 * (w + 0.5) + 2.0
    allo = [o for r in rows for o in r]
    lo, hi = bounds(allo)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = (hi.x - lo.x) * 1.04
    ctr = (lo + hi) / 2
    cam.location = Vector((ctr.x, hi.y + 40, 30 + ctr.z))
    cam.rotation_euler = (Vector((ctr.x, ctr.y, ctr.z)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sun.rotation_euler = (math.radians(50), 0, math.radians(200))
    tmp = out + '.png'
    sc.render.filepath = tmp
    bpy.ops.render.render(write_still=True)
    label(tmp, items, cam, sc, out, 18)
    os.remove(tmp)


def creature(out, path, H=520):
    """Pose sheet: each clip at a key frame, 3/4 top view + side view, and the LOD strip."""
    from PIL import Image
    E.reset()
    bpy.ops.wm.open_mainfile(filepath=path)
    sc, sun = setup(int(H * 1.3), H, 32)
    ao = next(o for o in sc.objects if o.type == 'ARMATURE')
    lods = sorted([o for o in sc.objects if o.type == 'MESH'], key=lambda o: o.name)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 60
    sun.rotation_euler = (math.radians(45), 0, math.radians(20))
    shots = [('Glide', 12, (35, 40)), ('Flap', 4, (35, 40)), ('Flap', 12, (90, 8)), ('Perch', 0, (35, 30)),
             ('Alert', 12, (35, 30)), ('Glide', 12, (180, -25))]
    tiles = []
    for i, (act, f, (az, el)) in enumerate(shots):
        ao.animation_data.action = bpy.data.actions[act]
        sc.frame_set(f)
        for o in lods:
            o.hide_render = not o.name.endswith('LOD0')
        r, e = math.radians(az), math.radians(el)
        d = 2.0
        cam.location = Vector((math.sin(r) * math.cos(e) * d, -math.cos(r) * math.cos(e) * d, math.sin(e) * d))
        cam.rotation_euler = (-cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f'{out}_{i}.png'
        bpy.ops.render.render(write_still=True)
        tiles.append((f'{act} f{f}', f'{out}_{i}.png'))
    W, Hh = sc.render.resolution_x, sc.render.resolution_y
    sheet = Image.new('RGB', (W * 3, Hh * 2))
    from PIL import ImageDraw
    for i, (t, pth) in enumerate(tiles):
        im = Image.open(pth).convert('RGB')
        ImageDraw.Draw(im).text((10, 10), t, fill=(255, 255, 255))
        sheet.paste(im, ((i % 3) * W, (i // 3) * Hh))
        os.remove(pth)
    sheet.save(out, quality=90)
    print('wrote', out)


def card(img_path, width, loc, name):
    """Backdrop card: emission (painted light) with straight alpha, facing +Y (the camera)."""
    img = bpy.data.images.load(img_path)
    w, h = img.size
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc, rotation=(math.pi / 2, 0, 0))
    o = bpy.context.active_object
    o.name = name
    o.scale = (width, width * h / w, 1)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes):
        if nd.type != 'OUTPUT_MATERIAL':
            nt.nodes.remove(nd)
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img; t.extension = 'CLIP'
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Strength'].default_value = 1.0
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mx = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(t.outputs['Color'], em.inputs['Color'])
    nt.links.new(t.outputs['Alpha'], mx.inputs[0])
    nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], nt.nodes['Material Output'].inputs[0])
    m.blend_method = 'BLEND'
    o.data.materials.append(m)
    return o


def place(name, loc, rot=0.0, src=None):
    """Instance (linked duplicate) of an already loaded LOD0 object (+ its crowns)."""
    out = []
    for nm in (name + '_LOD0', name + '_Crowns_LOD0'):
        base = bpy.data.objects.get(nm)
        if base is None:
            continue
        o = base.copy()
        o.location = loc
        o.rotation_euler = (0, 0, rot)
        bpy.context.scene.collection.objects.link(o)
        out.append(o)
    return out


def chase(out, H=640):
    """Readability test: the kit on a 7 m path from the spec 101 landscape camera at vMax (16 m/s)."""
    from PIL import Image
    E.reset()
    W = WK = os.path.join(E.SRC, 'work')
    blends = [os.path.join(WK, 'forest', f + '_game.blend') for f in
              ('OB_Roots', 'OB_Logs', 'OB_Vines', 'OB_Stone', 'OB_Hazard', 'PK_Pickups', 'TR_Edges', 'FT_Stiltwood_A',
               'FT_Stiltwood_B', 'FT_Stiltwood_Gate', 'FT_MidTrees')]
    blends += [os.path.join(WK, 'plants', f + '_game.blend') for f in ('FP_Fern_P_Clump', 'FP_PalmFern_P_Clump',
                                                                     'FP_Bellflower_P_Clump')]
    objs = load_lod(blends)
    for o in objs:
        o.hide_render = True
    sc, sun = setup(int(H * 19.5 / 9), H, 48)
    sun.rotation_euler = (math.radians(52), 0, math.radians(150))
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -110, 0))
    g = bpy.context.active_object
    g.scale = (9.0, 240, 1)
    g.data.materials.append(ground_mat())
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -110, -0.05))
    f = bpy.context.active_object
    f.scale = (300, 300, 1)
    fm = bpy.data.materials.new('floor'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.05, 0.1, 0.03, 1)
    f.data.materials.append(fm)
    P = []
    for k in range(16):
        y = 6 - k * 8
        P += place('TR_PathEdge_Roots_8m' if k % 2 else 'TR_PathEdge_Moss_8m', (-3.6, y, 0))
        P += place('TR_PathEdge_Moss_8m' if k % 2 else 'TR_PathEdge_Roots_8m', (3.6, y - 8, 0), math.pi)
    P += place('FT_Stiltwood_Gate', (0, -2, 0))
    for k, (x, y, nm, r) in enumerate([(-16, -20, 'FT_Stiltwood_A', 0.3), (17, -38, 'FT_Stiltwood_B', 1.2),
                                        (-19, -70, 'FT_Stiltwood_B', 2.0), (18, -95, 'FT_Stiltwood_A', 2.6),
                                        (-15, -120, 'FT_Stiltwood_A', 4.0), (9.5, -12, 'FT_MidTree_A', 0.0),
                                        (-10, -48, 'FT_MidTree_B', 1.0), (10.5, -66, 'FT_MidTree_A', 2.0),
                                        (-9.5, -88, 'FT_MidTree_A', 3.0), (11, -110, 'FT_MidTree_B', 4.0)]):
        P += place(nm, (x, y, 0), r)
    import random
    rnd = random.Random(3)
    for k in range(40):
        side = -1 if k % 2 else 1
        nm = rnd.choice(['FP_Fern_P_Clump', 'FP_PalmFern_P_Clump', 'FP_Fern_P_Clump', 'FP_Bellflower_P_Clump'])
        P += place(nm, (side * rnd.uniform(5.4, 9.0), -k * 3.2 + rnd.uniform(-1, 1), 0), rnd.uniform(0, 6.3))
    # obstacles at spec-like spacing (s = metres ahead of Pista); Blender x = -Unity x
    for nm, s_, x in [('OB_RootLow_05', 14, 0), ('OB_BranchHigh_10', 30, 0), ('OB_Boulder_14', 44, -1.5),
                      ('OB_Thorns_25', 56, 2.25), ('OB_LogWalk_09', 68, 0), ('OB_VineCurtain_7m', 84, 0),
                      ('OB_RootWall_4m', 100, 1.5), ('OB_Boulder_20', 112, -2.0)]:
        P += place(nm, (x, -s_, 0))
    for k in range(6):
        P += place('PK_Coin', (0, -4 - k * 1.5, 1.0))
    for k in range(3):
        P += place('PK_Coin', (0, -12.6 - k * 1.4, 1.15 + 0.35 * math.sin(math.pi * (k + 1) / 4)))
    P += place('PK_Crystal', (1.5, -37, 1.1))
    P += place('PK_Shield', (-2.5, -61, 2.3))
    for o in P:
        o.hide_render = False
    bd = os.path.join(E.UNITY_ENV, 'Backdrops')
    card(os.path.join(bd, 'BD_Forest_Far.png'), 520, (0, -330, 60), 'far')
    card(os.path.join(bd, 'BD_Forest_Mid.png'), 300, (0, -200, 40), 'mid')
    bg = sc.world.node_tree.nodes['Background']
    for l in list(bg.inputs[0].links):
        sc.world.node_tree.links.remove(l)
    bg.inputs[0].default_value = (0.42, 0.62, 0.8, 1)
    bg.inputs[1].default_value = 1.0
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle = math.radians(61)
    cam.data.clip_end = 2000
    cam.rotation_euler = (math.radians(90 - 13), 0, math.pi)
    px = proxy((0, 0))
    y0 = [o.location.y for o in px]
    for k, s0 in enumerate((0.0, 20.0, 58.0)):         # Pista at s = 0, 20 (root + branch close), 58 (log + curtain)
        cam.location = (0, 6.5 - s0, 3.4)
        for o, yy in zip(px, y0):
            o.location.y = yy - s0
        sc.render.filepath = out + f'{k}.png'
        bpy.ops.render.render(write_still=True)
    ims = [Image.open(out + f'{k}.png').convert('RGB') for k in range(3)]
    sheet = Image.new('RGB', (ims[0].width, ims[0].height * 3))
    for k, im in enumerate(ims):
        sheet.paste(im, (0, k * im.height))
        os.remove(out + f'{k}.png')
    sheet.save(out, quality=88)
    sheet.convert('L').convert('RGB').save(out.replace('.jpg', '_gray.jpg'), quality=85)
    sheet.resize((sheet.width // 3, sheet.height // 3), Image.LANCZOS).save(out.replace('.jpg', '_small.jpg'), quality=90)
    print('wrote', out)


if __name__ == '__main__':
    a = E.args()
    if a[0] == 'lineup':
        rw = float(a[a.index('--row') + 1]) if '--row' in a else 34.0
        paths = [x for x in a[2:] if x.endswith('.blend')]
        lineup(a[1], paths, rw)
    elif a[0] == 'chase':
        chase(a[1])
    elif a[0] == 'creature':
        creature(a[1], a[2])
    elif a[0] == 'lods':
        lods(a[1], a[2])
