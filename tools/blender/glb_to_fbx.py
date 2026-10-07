"""Headless (pip bpy): convert fitted environment GLBs to FBX + base color PNG for Unity (no glTFast needed).
Usage: python3 glb_to_fbx.py <models_dir> [Name ...]   Writes <Name>.fbx (Y up, -Z forward, 1 unit = 1 m) and <Name>.png beside the GLB."""
import os, sys, bpy

def convert(d, name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(d, name + ".glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in bpy.context.scene.objects: o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    for o in meshes:  # bake any parent/import transform into the mesh; names stay clean
        o.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1: bpy.ops.object.join()
    ob = bpy.context.active_object; ob.name = name; ob.data.name = name
    for mat in ob.data.materials:
        for n in mat.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                n.image.name = name; n.image.filepath_raw = os.path.join(d, name + ".png"); n.image.file_format = "PNG"; n.image.save()
        mat.name = name
    bpy.ops.export_scene.fbx(filepath=os.path.join(d, name + ".fbx"), use_selection=True, object_types={"MESH"}, path_mode="COPY",
        embed_textures=True, add_leaf_bones=False, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0, apply_unit_scale=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
    print("ok", name, len(ob.data.polygons))

if __name__ == "__main__":
    d = sys.argv[1]; names = sys.argv[2:] or sorted(f[:-4] for f in os.listdir(d) if f.endswith(".glb"))
    for n in names: convert(d, n)
