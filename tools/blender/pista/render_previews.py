"""Render review previews of Pista (EEVEE, neutral studio light) and a labelled contact sheet.

Usage:
  blender -b --python-use-system-env -P render_previews.py -- <input .blend|.glb|.fbx> <out_prefix> [action] [frames]
    - no action: turnaround (front, 3/4, side, back) + face and backpack close-ups -> <out_prefix>_<view>.png + _sheet.jpg
    - action + frames (e.g. "run 8"): that many evenly spaced frames of one cycle, side and 3/4 rows, with a
      ground grid and foot-height markers for sliding checks -> <out_prefix>_contact.jpg
Camera framing is fixed in meters (character is 1.65 m, feet at the origin, facing Blender -Y), so sheets from
different versions are directly comparable.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

a = C.args()
src, prefix = a[0], a[1]
action = a[2] if len(a) > 2 else None
nframes = int(a[3]) if len(a) > 3 else 8
RES = 1024

if src.endswith('.blend'):
    C.open_blend(src)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if src.endswith('.fbx'):
        bpy.ops.import_scene.fbx(filepath=src)
    else:
        bpy.ops.import_scene.gltf(filepath=src)
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type in ('CAMERA', 'LIGHT'):
        bpy.data.objects.remove(o, do_unlink=True)

sc.render.engine = 'BLENDER_EEVEE_NEXT'
sc.eevee.taa_render_samples = 64
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.film_transparent = False
sc.view_settings.view_transform = 'AgX'
sc.view_settings.look = 'None'
world = bpy.data.worlds.new('Studio')
sc.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.32, 0.33, 0.35, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6


def light(name, kind, energy, loc, size=1.0, color=(1, 1, 1)):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'AREA':
        ld.size = size
    ob = bpy.data.objects.new(name, ld)
    sc.collection.objects.link(ob)
    ob.location = loc
    d = Vector((0, 0, 1.0)) - Vector(loc)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return ob


lights = [light('key', 'AREA', 260, (-1.6, -2.4, 2.6), 1.5, (1.0, 0.96, 0.9)),
          light('fill', 'AREA', 90, (2.2, -1.8, 1.4), 2.0, (0.85, 0.9, 1.0)),
          light('rim', 'AREA', 200, (0.6, 2.6, 2.4), 1.0)]

cam_d = bpy.data.cameras.new('cam')
cam = bpy.data.objects.new('cam', cam_d)
sc.collection.objects.link(cam)
sc.camera = cam


def shoot(path, yaw_deg, target, dist, lens, height=None):
    """yaw 0 = camera in front of the character (character faces -Y)."""
    cam_d.lens = lens
    y = math.radians(yaw_deg)
    t = Vector(target)
    pos = t + Vector((math.sin(y) * dist, -math.cos(y) * dist, 0))
    if height is not None:
        pos.z = height
    cam.location = pos
    cam.rotation_euler = (t - pos).to_track_quat('-Z', 'Y').to_euler()
    # lights follow the camera so every view is lit like the front one
    for lo, base in zip(lights, ((-1.6, -2.4, 2.6), (2.2, -1.8, 1.4), (0.6, 2.6, 2.4))):
        bx, by, bz = base
        lo.location = (bx * math.cos(y) - by * math.sin(y), bx * math.sin(y) + by * math.cos(y), bz)
        lo.rotation_euler = (Vector((0, 0, 1.0)) - lo.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('rendered', path)
    return path


def sheet(paths, labels, out, cols):
    from PIL import Image, ImageDraw
    ims = [Image.open(p).convert('RGB') for p in paths]
    w, h = ims[0].size
    rows = math.ceil(len(ims) / cols)
    s = Image.new('RGB', (w * cols, h * rows), 'white')
    d = ImageDraw.Draw(s)
    for i, (im, lb) in enumerate(zip(ims, labels)):
        x, y = (i % cols) * w, (i // cols) * h
        s.paste(im, (x, y))
        d.text((x + 12, y + 10), lb, fill=(255, 255, 255))
    s.save(out, quality=90)
    print('sheet', out)


os.makedirs(os.path.dirname(prefix), exist_ok=True)
if action is None:
    views = [('front', 0, (0, 0, 0.83), 2.9, 50), ('34', -40, (0, 0, 0.83), 2.9, 50),
             ('side', -90, (0, 0, 0.83), 2.9, 50), ('back', 180, (0, 0, 0.83), 2.9, 50),
             ('face', -15, (0, -0.02, 1.52), 1.0, 85), ('backpack', 160, (0, 0.1, 1.15), 1.6, 70)]
    paths = [shoot(f'{prefix}_{n}.png', yaw, t, d, lens) for n, yaw, t, d, lens in views]
    sheet(paths, [v[0] for v in views], f'{prefix}_sheet.jpg', 3)
else:
    arm = next(o for o in sc.objects if o.type == 'ARMATURE')
    act = bpy.data.actions[action]
    arm.animation_data_create()
    arm.animation_data.action = act
    f0, f1 = act.frame_range
    sc.render.resolution_x = sc.render.resolution_y = 512
    sc.eevee.taa_render_samples = 32
    # ground grid: 10 cm lines so foot sliding is visible frame to frame
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=40, y_subdivisions=40, size=4)
    g = bpy.context.active_object
    m = bpy.data.materials.new('grid'); m.use_nodes = True
    nt = m.node_tree; bs = nt.nodes['Principled BSDF']
    ch = nt.nodes.new('ShaderNodeTexChecker'); ch.inputs['Scale'].default_value = 40
    ch.inputs['Color1'].default_value = (0.25, 0.25, 0.25, 1); ch.inputs['Color2'].default_value = (0.4, 0.4, 0.4, 1)
    nt.links.new(ch.outputs['Color'], bs.inputs['Base Color']); g.data.materials.append(m)
    paths, labels = [], []
    for row, yaw in (('side', -90), ('34', -35)):
        for i in range(nframes):
            f = f0 + (f1 - f0) * i / nframes
            sc.frame_set(int(f), subframe=f - int(f))
            paths.append(shoot(f'{prefix}_{row}_{i}.png', yaw, (0, 0, 0.85), 4.2, 50, height=1.0))
            labels.append(f'{row} f{f:.1f}')
    sheet(paths, labels, f'{prefix}_contact.jpg', nframes)
    for p in paths:
        os.remove(p)
