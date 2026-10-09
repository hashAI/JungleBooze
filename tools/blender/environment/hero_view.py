"""Render a piece from the F4_e hero camera side (env HV_APPEND=<blend>[:...] adds objects from other files) (Blender +Y = Unity -Z, the side the basin camera sees) next to the
keyframe crop, for side-by-side comparison. Usage: -- <blend> <out.jpg> [cam x y z] [target x y z] [lens] [crop l t r b]
Env HV_KEYFRAME=<repo-relative image> swaps the keyframe (painterly: F4_f). Clay if the meshes have no image textures; otherwise their materials. Sun warm, low, from upper left (keyframe)."""
import math, os, sys
import bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(__file__))
import envlib as E
from PIL import Image
a = E.args(); bpy.ops.wm.open_mainfile(filepath=a[0]); out = a[1]
# HV_APPEND=<blend>[:<blend>...]: also bring in those files' objects (e.g. FP_ArchVines on the arch, same local space)
for extra in filter(None, os.environ.get('HV_APPEND', '').split(':')):
    with bpy.data.libraries.load(extra) as (src, dst):
        dst.objects = list(src.objects)
    for o in dst.objects:
        if o is not None and o.type == 'MESH':
            bpy.context.scene.collection.objects.link(o)
f = [float(x) for x in a[2:]]
cam_p = Vector(f[0:3]) if len(f) >= 3 else Vector((0, 52, 5))
tgt = Vector(f[3:6]) if len(f) >= 6 else Vector((0, 0, 21))
lens = f[6] if len(f) >= 7 else 26
crop = f[7:11] if len(f) >= 11 else (0.25, 0.0, 0.85, 0.6)
sc = E.gpu_cycles(48); sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
sc.render.resolution_x, sc.render.resolution_y = 1000, 667
meshes = [o for o in sc.objects if o.type == 'MESH']
for o in meshes:
    if any(f'_LOD{i}' in o.name for i in (1, 2)):
        o.hide_render = True
textured = any(n.bl_idname == 'ShaderNodeTexImage' for o in meshes for m in o.data.materials if m and m.node_tree
               for n in m.node_tree.nodes)
if not textured:
    mat = bpy.data.materials.new('clay'); mat.use_nodes = True
    bs = mat.node_tree.nodes['Principled BSDF']; bs.inputs['Base Color'].default_value = (0.55, 0.5, 0.42, 1)
    bs.inputs['Roughness'].default_value = 0.85
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(mat)
        for p in o.data.polygons: p.use_smooth = True
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.75, 0.72, 0.62, 1)
w.node_tree.nodes['Background'].inputs[1].default_value = 0.7
L = bpy.data.lights.new('sun', 'SUN'); L.energy = 4.0; L.angle = math.radians(2); L.color = (1.0, 0.88, 0.72)
so = bpy.data.objects.new('sun', L); sc.collection.objects.link(so)
d = Vector((-0.55, 0.35, -0.6)).normalized()           # sun upper left of frame, a little behind the arch
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = lens; cam.data.clip_end = 5000
cam.location = cam_p
cam.rotation_euler = (tgt - cam_p).to_track_quat('-Z', 'Y').to_euler()
tmp = out + '.png'; sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
r = Image.open(tmp).convert('RGB')
kf = Image.open(os.path.join(E.REPO, os.environ.get('HV_KEYFRAME', 'design/aurelia/keyframes/F4_e_openai_medium.jpg'))).convert('RGB')
W, H = kf.size
k = kf.crop((int(crop[0] * W), int(crop[1] * H), int(crop[2] * W), int(crop[3] * H)))
k = k.resize((int(k.width * r.height / k.height), r.height))
s = Image.new('RGB', (k.width + r.width + 10, r.height), (20, 20, 20)); s.paste(k, (0, 0)); s.paste(r, (k.width + 10, 0))
s.save(out, quality=88); os.remove(tmp); print('wrote', out)
