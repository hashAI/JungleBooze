"""Step 1: import the Meshy GLB, weld UV-seam splits, scale to 1.65 m, feet on the origin, face Unity +Z.

Orientation: Meshy/glTF characters face glTF +Z. Blender's glTF importer maps glTF +Z to Blender -Y, and both
the glTF exporter and the FBX exporter (forward -Z, up Y) map Blender -Y back to Unity +Z. So the model must face
Blender -Y here. The script measures where the face is (nose = most forward point of the head) and rotates
if needed, then applies all transforms.

Output: art_source/pista/work/pista_01_prepared.blend
"""
import math
import os
import sys

import bpy  # must be imported before bmesh/mathutils when bpy runs as a module
import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=C.MESHY_GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1, meshes
obj = meshes[0]
obj.name = C.BODY
obj.data.name = C.BODY
C.set_active(obj)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
tris_in = C.tri_count(obj)

# Weld vertices the glTF importer split along UV seams (UVs are per-loop, so they survive).
bm = bmesh.new()
bm.from_mesh(obj.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bm.to_mesh(obj.data)
bm.free()
print(f'welded {before} -> {len(obj.data.vertices)} verts')

# Face direction check: boot toes stick out further forward from the shins than the heels do.
co = [v.co for v in obj.data.vertices]
zmin = min(c.z for c in co); zmax = max(c.z for c in co); H = zmax - zmin
shin = [c for c in co if zmin + 0.12 * H < c.z < zmin + 0.18 * H]
sole = [c for c in co if c.z < zmin + 0.03 * H]
shin_y = sum(c.y for c in shin) / len(shin)
toe_minus = shin_y - min(c.y for c in sole)    # reach toward -Y
toe_plus = max(c.y for c in sole) - shin_y     # reach toward +Y
faces_minus_y = toe_minus > toe_plus
print('boot reach -Y %.3f  +Y %.3f -> faces -Y (Unity +Z): %s' % (toe_minus, toe_plus, faces_minus_y))
if not faces_minus_y:
    obj.rotation_euler.z = math.pi
    bpy.ops.object.transform_apply(rotation=True)

# Scale to 1.65 m and put the feet on the origin (centered in X/Y between the feet).
co = [v.co for v in obj.data.vertices]
zmin = min(c.z for c in co); zmax = max(c.z for c in co)
s = C.HEIGHT_M / (zmax - zmin)
feet = [c for c in co if c.z < zmin + 0.02 * (zmax - zmin)]
fx = (min(c.x for c in feet) + max(c.x for c in feet)) / 2
fy = (min(c.y for c in feet) + max(c.y for c in feet)) / 2
for v in obj.data.vertices:
    v.co = Vector(((v.co.x - fx) * s, (v.co.y - fy) * s, (v.co.z - zmin) * s))
obj.data.update()

co = [v.co for v in obj.data.vertices]
print('height %.4f m, x %.3f..%.3f, y %.3f..%.3f, scale %.4f, tris %d -> %d' % (
    max(c.z for c in co) - min(c.z for c in co), min(c.x for c in co), max(c.x for c in co),
    min(c.y for c in co), max(c.y for c in co), s, tris_in, C.tri_count(obj)))
C.save_blend(C.work('pista_01_prepared.blend'))
