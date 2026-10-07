"""Headless character cleanup: import GLB, optional texture swap, collapse-decimate to a triangle target (UVs kept),
scale to game height, origin at feet center, export GLB. Texture is resized to a max size.
Run with the `bpy` pip module:  python3 process_character.py in.glb out.glb target_tris height_m max_tex [texture.png]
"""
import sys, bpy, bmesh
src, dst, target, height, maxtex = sys.argv[1], sys.argv[2], int(sys.argv[3]), float(sys.argv[4]), int(sys.argv[5])
tex = sys.argv[6] if len(sys.argv) > 6 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
for o in bpy.context.scene.objects:
    if o is not obj: bpy.data.objects.remove(o)
obj.parent = None; obj.matrix_world.identity()
bpy.context.view_layer.objects.active = obj; obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mat = obj.data.materials[0]
node = next(n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE")
if tex:
    img = bpy.data.images.load(tex); img.colorspace_settings.name = "sRGB"; node.image = img
else:
    img = node.image
if max(img.size) > maxtex: img.scale(maxtex, maxtex)
img.pack()
def tris(): 
    d = bpy.context.evaluated_depsgraph_get(); m = obj.evaluated_get(d).to_mesh(); n = sum(len(p.vertices) - 2 for p in m.polygons); return n
n0 = tris()
if n0 > target:
    mod = obj.modifiers.new("dec", "DECIMATE"); mod.ratio = target / n0 * 0.995; mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
# scale to height, origin at feet center
bb = [obj.matrix_world @ v.co for v in obj.data.vertices]
ys = [v.z for v in bb]  # blender Z up
h = max(ys) - min(ys); s = height / h
xs = [v.x for v in bb]; zs = [v.y for v in bb]
bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True)
for v in obj.data.vertices:
    v.co.x = (v.co.x - (max(xs) + min(xs)) / 2) * s
    v.co.y = (v.co.y - (max(zs) + min(zs)) / 2) * s
    v.co.z = (v.co.z - min(ys)) * s
obj.data.update()
print("TRIS", n0, "->", tris(), "tex", tuple(img.size), "height", height)
bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", export_image_format="JPEG", export_jpeg_quality=92, export_yup=True)
