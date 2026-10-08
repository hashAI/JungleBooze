"""Render preview views of a GLB character (front, 3/4, side, back) with Cycles on CPU.
Usage: python render_turntable.py <model.glb> <out_dir> [height_m]
Runs with Blender's Python module (pip install bpy) or `blender -b -P` (pass args after `--`)."""
import sys, os, math
import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
src, out = args[0], args[1]
height = float(args[2]) if len(args) > 2 else 1.65
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']

# Scale to the target height, feet on the ground, centered
def bounds():
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (Vector((min(p[i] for p in pts) for i in range(3))), Vector((max(p[i] for p in pts) for i in range(3))))
lo, hi = bounds()
s = height / (hi.z - lo.z)
root = bpy.data.objects.new('root', None); bpy.context.scene.collection.objects.link(root)
for o in objs:
    if o.parent is None: o.parent = root
root.scale = (s, s, s); bpy.context.view_layer.update()
lo, hi = bounds(); c = (lo + hi) / 2
root.location = (-c.x, -c.y, -lo.z); bpy.context.view_layer.update()
lo, hi = bounds()
print(f'height {hi.z - lo.z:.3f} m  width {hi.x - lo.x:.3f} m  depth {hi.y - lo.y:.3f} m')

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 768, 1024
sc.view_settings.view_transform = 'AgX'
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.62, 0.64, 0.66, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.6
for name, rot, energy in [('key', (50, 0, 35), 4.0), ('fill', (60, 0, -60), 1.5), ('rim', (60, 0, 170), 3.0)]:
    L = bpy.data.lights.new(name, 'SUN'); L.energy = energy; L.angle = math.radians(8)
    ob = bpy.data.objects.new(name, L); ob.rotation_euler = [math.radians(a) for a in rot]; sc.collection.objects.link(ob)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 85
target = Vector((0, 0, height * 0.52)); dist = height * 3.4
# glTF front faces -Y in Blender after import; angle 0 = front
for label, ang in [('front', 0), ('three_quarter', 40), ('side', 90), ('back', 180)]:
    a = math.radians(ang)
    cam.location = target + Vector((math.sin(a) * dist, -math.cos(a) * dist, 0.15))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, f'{label}.png'); bpy.ops.render.render(write_still=True)
    print('rendered', label)
