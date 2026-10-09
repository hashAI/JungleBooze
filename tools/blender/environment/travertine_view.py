"""Preview of the travertine tiers with their water sheets: downstream hero-like view next to the keyframe pools,
and a top view with the anchors. Usage: -- <blend> <out.jpg>"""
import math, os, sys
import bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(__file__))
import envlib as E
from PIL import Image, ImageDraw
a = E.args(); bpy.ops.wm.open_mainfile(filepath=a[0]); out = a[1]
sc = E.gpu_cycles(int(os.environ.get('PV_SAMPLES', 64))); sc.cycles.use_denoising = True  # PV_SAMPLES: faster drafts
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
sc.render.resolution_x, sc.render.resolution_y = 1000, 667
if not any(o.name.endswith('_Water') for o in sc.objects):
    wf = os.path.join(E.UNITY_ENV, 'Rootstone', 'RS_TravertineTiers_Water.fbx')
    if os.path.exists(wf):
        bpy.ops.import_scene.fbx(filepath=wf, axis_forward='-Z', axis_up='Y')
objs = [o for o in sc.objects if o.type == 'MESH']
for o in objs:
    if any(f'_LOD{i}' in o.name for i in (1, 2)):
        o.hide_render = True
rock = [o for o in objs if not o.name.endswith('_Water') and not o.hide_render]
textured = any(n.bl_idname == 'ShaderNodeTexImage' for o in rock for m in o.data.materials if m and m.node_tree
               for n in m.node_tree.nodes)
if not textured:
    clay = bpy.data.materials.new('clay'); clay.use_nodes = True
    clay.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.3, 0.28, 0.22, 1)
    for o in rock:
        o.data.materials.clear(); o.data.materials.append(clay)
        for p in o.data.polygons: p.use_smooth = True
wm = bpy.data.materials.new('water'); wm.use_nodes = True
nt = wm.node_tree; bs = nt.nodes['Principled BSDF']
at = nt.nodes.new('ShaderNodeVertexColor'); at.layer_name = 'Col'
sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(at.outputs[0], sep.inputs[0])
mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
mix.inputs[6].default_value = (0.15, 0.62, 0.58, 1); mix.inputs[7].default_value = (0.02, 0.2, 0.22, 1)
nt.links.new(sep.outputs[1], mix.inputs[0])
fm = nt.nodes.new('ShaderNodeMix'); fm.data_type = 'RGBA'; fm.inputs[7].default_value = (0.9, 0.93, 0.92, 1)
nt.links.new(mix.outputs[2], fm.inputs[6]); nt.links.new(sep.outputs[0], fm.inputs[0])
nt.links.new(fm.outputs[2], bs.inputs['Base Color'])
bs.inputs['Roughness'].default_value = 0.08
alpha = nt.nodes.new('ShaderNodeMapRange'); alpha.inputs[3].default_value = 0.55; alpha.inputs[4].default_value = 0.95
nt.links.new(sep.outputs[1], alpha.inputs[0]); nt.links.new(alpha.outputs[0], bs.inputs['Alpha'])
for o in objs:
    if o.name.endswith('_Water'):
        o.data.materials.clear(); o.data.materials.append(wm)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, 0.3))
basin = bpy.context.active_object; basin.data.materials.append(wm)
basin.data.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT').data.foreach_set('color', [0, 0.5, 0, 1] * 4)
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = (0.8, 0.78, 0.7, 1)
w.node_tree.nodes['Background'].inputs[1].default_value = 0.8
L = bpy.data.lights.new('sun', 'SUN'); L.energy = 4.0; L.angle = math.radians(2); L.color = (1.0, 0.88, 0.72)
so = bpy.data.objects.new('sun', L); sc.collection.objects.link(so)
so.rotation_euler = Vector((-0.5, 0.6, -0.45)).normalized().to_track_quat('-Z', 'Y').to_euler()
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.clip_end = 2000
def shot(p, t, lens, path):
    cam.location = Vector(p); cam.data.lens = lens
    cam.rotation_euler = (Vector(t) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path; bpy.ops.render.render(write_still=True); return Image.open(path).convert('RGB')
r1 = shot((-4, -23, 10.5), (0, 0, 2.6), 26, out + '_a.png')
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 32
r2 = shot((0, -0.5, 60), (0, -0.5, 0), 50, out + '_b.png')
# anchors on the top view
d = ImageDraw.Draw(r2)
def to_px(v):
    return (500 + v.x / 32 * 1000, 333 - (v.y + 0.5) / 32 * 1000)
for e in sc.objects:
    if e.type == 'EMPTY':
        x, y = to_px(e.location)
        col = (255, 60, 60) if 'Cascade' in e.name else (255, 230, 0) if 'Lip' in e.name else (40, 220, 255)
        d.ellipse((x - 4, y - 4, x + 4, y + 4), fill=col)
kf = Image.open(os.path.join(E.REPO, 'design/aurelia/keyframes/F4_e_openai_medium.jpg')).convert('RGB')
W, H = kf.size
k = kf.crop((int(0.3 * W), int(0.5 * H), int(0.95 * W), int(0.95 * H))); k = k.resize((int(k.width * 667 / k.height), 667))
s = Image.new('RGB', (k.width + 2010, 667), (20, 20, 20)); s.paste(k, (0, 0)); s.paste(r1, (k.width + 5, 0)); s.paste(r2, (k.width + 1010, 0))
s.save(out, quality=88); [os.remove(out + x) for x in ('_a.png', '_b.png')]; print('wrote', out)
