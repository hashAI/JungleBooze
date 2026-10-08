"""Step 7: join body + ponytail into one mesh (one draw call, one material) and export.

Outputs:
  clean/pista_clean.glb            static game mesh, 2k BaseColor/Normal/ARM (ORM) as PNG, +Y up, faces +Z
  work/pista_upload_meshy.glb      same mesh with only a 1k JPEG base color (small upload for Meshy auto-rigging)
  work/T_Pista_BaseColor_1k.png    copy of the 1k albedo for Meshy's texture_image_url
  work/pista_07_joined.blend
Input: work/pista_06_baked.blend
"""
import os
import shutil
import sys

import bpy

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402
import composite  # noqa: E402

C.open_blend(C.work('pista_06_baked.blend'))
body, pony = bpy.data.objects[C.BODY], bpy.data.objects[C.PONYTAIL]
# tag ponytail vertices so step 8 can weight them to the swing bones
g = pony.vertex_groups.new(name='ponytail_mask')
g.add(range(len(pony.data.vertices)), 1.0, 'REPLACE')
for o in bpy.context.view_layer.objects:
    o.select_set(o in (body, pony))
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
obj = body
obj.name = obj.data.name = 'SK_Pista'
for o in list(bpy.data.objects):
    if o is not obj:
        bpy.data.objects.remove(o, do_unlink=True)
mat = obj.data.materials[0]
mat.name = 'M_Pista'
fixed = obj.data.validate(verbose=True, clean_customdata=False)   # degenerate/duplicate faces from decimation
print('validate fixed something:', fixed)
print('joined tris', C.tri_count(obj), 'materials', len(obj.data.materials))
C.save_blend(C.work('pista_07_joined.blend'))


def export(path, fmt):
    C.set_active(obj)
    bpy.ops.export_scene.gltf(filepath=path, use_selection=True, export_format='GLB', export_yup=True,
                              export_apply=True, export_image_format=fmt, export_materials='EXPORT',
                              export_normals=True, export_texcoords=True, export_tangents=False,
                              export_animations=False, export_skins=False)
    # (vertex groups are not exported to glTF without a skin, so the mask stays internal)
    print('exported', path, os.path.getsize(path))


export(os.path.join(C.CLEAN, 'pista_clean.glb'), 'AUTO')

# small upload: 1k albedo only
tex = os.path.join(C.CLEAN, 'textures')
composite.build_material(mat, tex, '1k')
nt = mat.node_tree
bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
for n in list(nt.nodes):
    if n.type == 'TEX_IMAGE' and 'BaseColor' not in n.image.name:
        nt.nodes.remove(n)
    elif n.type in ('NORMAL_MAP', 'SEPARATE_COLOR', 'GROUP'):
        nt.nodes.remove(n)
bsdf.inputs['Roughness'].default_value = 0.8
export(C.work('pista_upload_meshy.glb'), 'JPEG')
shutil.copy(os.path.join(tex, 'T_Pista_BaseColor_1k.png'), C.work('T_Pista_BaseColor_1k.png'))
