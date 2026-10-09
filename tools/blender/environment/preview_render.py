"""Lit previews of a game-ready environment .blend (LOD0 + LOD strip), keyframe-like light: warm sun ahead-left,
CC0 sky HDRI, light haze. Usage: -- <game.blend> <out_prefix> [lod_prefix]
Writes <out_prefix>_views.jpg (3 views) and <out_prefix>_lods.jpg (LOD0/1/2 side by side, wireframe overlay-free)."""
import glob, math, os, sys
import bpy
from mathutils import Vector
sys.path.insert(0, os.path.dirname(__file__))
import envlib as E
from PIL import Image

a = E.args(); bpy.ops.wm.open_mainfile(filepath=a[0]); prefix = a[1]
sc = E.gpu_cycles(64); sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
W, H = 1280, 800
sc.render.resolution_x, sc.render.resolution_y = W, H
meshes = [o for o in sc.objects if o.type == 'MESH']
lod0 = [o for o in meshes if '_LOD0' in o.name or not any(f'_LOD{i}' in o.name for i in range(3))]
others = [o for o in meshes if o not in lod0]
hdr = glob.glob(os.path.join(E.REPO, 'UnityProject/Assets/_Game/Art/CC0/HDRI/kloofendal*/*.hdr')) + \
      glob.glob(os.path.join(E.REPO, 'UnityProject/Assets/_Game/Art/CC0/HDRI/kloofendal*/*.exr'))
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']
if hdr:
    env = w.node_tree.nodes.new('ShaderNodeTexEnvironment'); env.image = bpy.data.images.load(hdr[0])
    w.node_tree.links.new(env.outputs[0], bg.inputs[0]); bg.inputs[1].default_value = 0.9
L = bpy.data.lights.new('sun', 'SUN'); L.energy = 4.5; L.angle = math.radians(1.5); L.color = (1.0, 0.9, 0.78)
so = bpy.data.objects.new('sun', L); sc.collection.objects.link(so)
# ground with CC0 forest floor
gm = bpy.data.materials.new('ground'); gm.use_nodes = True
gt = gm.node_tree; gimg = gt.nodes.new('ShaderNodeTexImage')
gimg.image = bpy.data.images.load(os.path.join(E.CC0_TEX, 'forest_ground_05', 'forest_ground_05_diff_2k.jpg'))
gmp = gt.nodes.new('ShaderNodeMapping'); gmp.inputs['Scale'].default_value = (0.4, 0.4, 0.4)
gtc = gt.nodes.new('ShaderNodeTexCoord'); gt.links.new(gtc.outputs['Object'], gmp.inputs[0])
gt.links.new(gmp.outputs[0], gimg.inputs[0]); gt.links.new(gimg.outputs[0], gt.nodes['Principled BSDF'].inputs['Base Color'])

def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])

g = None
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 40; cam.data.clip_end = 5000

def shoot(objs, ang, elev, path, dist_mult=1.0, sun=(-45, 32)):
    for o in meshes: o.hide_render = o not in objs
    lo, hi = bounds([o for o in objs if o.name != g.name]); ctr = (lo + hi) / 2; size = max((hi - lo).x, (hi - lo).y, (hi - lo).z)
    r = math.radians(ang); d = size * 1.25 * dist_mult
    cam.location = ctr + Vector((math.sin(r) * d, -math.cos(r) * d, -ctr.z + max(1.7, size * elev)))
    cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
    # sun ahead-left of the camera (into the camera a little, keyframe light)
    so.rotation_euler = (math.radians(90 - sun[1]), 0, r + math.radians(180 + sun[0]))
    sc.render.filepath = path; bpy.ops.render.render(write_still=True); return path

lo, hi = bounds(lod0); size = (hi - lo).length
bpy.ops.mesh.primitive_plane_add(size=size * 10, location=((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 0))
g = bpy.context.active_object; g.name = 'ground'; g.data.materials.append(gm); meshes.append(g); lod0g = lod0 + [g]
tmp = prefix + '_tmp'
views = [shoot(lod0g, 25, 0.12, f'{tmp}0.png'), shoot(lod0g, 140, 0.25, f'{tmp}1.png', sun=(-150, 40)),
         shoot(lod0g, 250, 0.05, f'{tmp}2.png', 0.6)]
ims = [Image.open(v).convert('RGB') for v in views]
sheet = Image.new('RGB', (W * 2, H * 2), (20, 20, 20))
sheet.paste(ims[0].resize((W * 2 // 1, H * 2 // 1)).crop((0, 0, W * 2, H * 2)).resize((W * 2, H * 2)), (0, 0)) if False else None
sheet.paste(ims[0], (0, 0)); sheet.paste(ims[1], (W, 0)); sheet.paste(ims[2], (0, H))
# lod strip in the 4th quadrant
lods = sorted([o for o in meshes if '_LOD' in o.name], key=lambda o: o.name)
if lods:
    names = sorted({o.name.rsplit('_LOD', 1)[0] for o in lods})
    tiles = []
    for i in range(3):
        objs = [o for o in lods if o.name.endswith(f'_LOD{i}')] + [g]
        tiles.append(Image.open(shoot(objs, 25, 0.12, f'{tmp}l{i}.png')).convert('RGB').resize((W // 3, H // 3)))
    q = Image.new('RGB', (W, H), (20, 20, 20))
    for i, t in enumerate(tiles): q.paste(t, ((W // 3) * i, H // 3))
    sheet.paste(q, (W, H))
sheet.save(prefix + '_views.jpg', quality=88)
for f in glob.glob(tmp + '*.png'): os.remove(f)
print('wrote', prefix + '_views.jpg')
