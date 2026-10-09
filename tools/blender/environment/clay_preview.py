"""Quick clay render of a .blend (all meshes): 3 views side by side. Usage: -- <blend> <out.png> [cam_dist_mult]"""
import math, os, sys
import bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(__file__))
import envlib as E
a = E.args(); bpy.ops.wm.open_mainfile(filepath=a[0])
mult = float(a[2]) if len(a) > 2 else 1.0
sc = E.gpu_cycles(24); sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 640, 640
sc.view_settings.view_transform = 'AgX'
objs = [o for o in sc.objects if o.type == 'MESH']
mat = bpy.data.materials.new('clay'); mat.use_nodes = True
nt = mat.node_tree; bs = nt.nodes['Principled BSDF']
at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'strand'
ramp = nt.nodes.new('ShaderNodeMapRange'); ramp.inputs[3].default_value = 0.45; ramp.inputs[4].default_value = 0.62
nt.links.new(at.outputs['Color'], nt.nodes.new('ShaderNodeSeparateColor').inputs[0])
sep = [n for n in nt.nodes if n.bl_idname == 'ShaderNodeSeparateColor'][0]
nt.links.new(sep.outputs[0], ramp.inputs[0]); nt.links.new(ramp.outputs[0], bs.inputs['Base Color'])
bs.inputs['Roughness'].default_value = 0.85
for o in objs:
    o.data.materials.clear(); o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; size = (hi - lo).length
bpy.ops.mesh.primitive_plane_add(size=size * 4, location=(ctr.x, ctr.y, 0))
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.55, 0.65, 0.78, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.5
L = bpy.data.lights.new('sun', 'SUN'); L.energy = 4; L.angle = math.radians(3)
so = bpy.data.objects.new('sun', L); so.rotation_euler = (math.radians(55), 0, math.radians(-35)); sc.collection.objects.link(so)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
from PIL import Image
tiles = []
for i, ang in enumerate([20, 70, 160]):
    r = math.radians(ang); d = size * 1.05 * mult
    cam.location = ctr + Vector((math.sin(r) * d, -math.cos(r) * d, size * 0.12))
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    f = a[1].replace('.png', f'_{i}.png'); sc.render.filepath = f
    bpy.ops.render.render(write_still=True); tiles.append(f)
ims = [Image.open(t) for t in tiles]; s = Image.new('RGB', (640 * 3, 640))
for i, im in enumerate(ims): s.paste(im.convert('RGB'), (640 * i, 0))
s.save(a[1]); [os.remove(t) for t in tiles]
