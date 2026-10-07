"""Hand rig for Duko (macaw): 7 bones (Root, Body, Head, Wing_L, Wing_R, Tail1, Tail2) with geometric skin weights and
three looping actions (Idle, Flap, TailWag). Exports GLB + FBX + base color PNG.
Run with the `bpy` pip module:  python3 rig_duko.py Duko_game.glb out_dir
Model space (Blender, Z up, feet at z=0 is NOT the perch; model height 0.85, origin at the bottom of the tail tip)."""
import sys, os, math, bpy
from mathutils import Vector
src, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
for o in list(bpy.context.scene.objects):
    if o is not obj: bpy.data.objects.remove(o)
obj.parent = None; bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
obj.name = "Duko"
vs = [v.co.copy() for v in obj.data.vertices]
H = max(v.z for v in vs); print("height", H)
# landmark heights as fractions of model height (from the concept: tail hangs from ~46%, head from ~73%)
Z_TAIL, Z_HEAD, Z_SHOULDER = 0.46 * H, 0.73 * H, 0.70 * H
cy = sum(v.y for v in vs) / len(vs)
# armature
arm_d = bpy.data.armatures.new("DukoRig"); arm = bpy.data.objects.new("DukoRig", arm_d)
bpy.context.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
def bone(name, head, tail, parent=None):
    b = arm_d.edit_bones.new(name); b.head = head; b.tail = tail
    if parent: b.parent = arm_d.edit_bones[parent]; b.use_connect = False
    return b
by = cy
bone("Root", (0, by, Z_TAIL), (0, by, Z_TAIL + 0.05))
bone("Body", (0, by, Z_TAIL), (0, by, Z_HEAD), "Root")
bone("Head", (0, by, Z_HEAD), (0, by, H), "Body")
bone("Tail1", (0, by, Z_TAIL), (0, by, Z_TAIL * 0.5), "Root")
bone("Tail2", (0, by, Z_TAIL * 0.5), (0, by, 0.0), "Tail1")
bone("Wing_L", (0.07, by, Z_SHOULDER), (0.09, by, Z_TAIL * 0.8), "Body")
bone("Wing_R", (-0.07, by, Z_SHOULDER), (-0.09, by, Z_TAIL * 0.8), "Body")
bpy.ops.object.mode_set(mode="OBJECT")
# weights
def sstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
groups = {n: obj.vertex_groups.new(name=n) for n in ["Root", "Body", "Head", "Tail1", "Tail2", "Wing_L", "Wing_R"]}
for i, v in enumerate(vs):
    ax = abs(v.x)
    wing = sstep(0.05, 0.085, ax) * sstep(Z_TAIL * 0.6, Z_TAIL * 1.0, v.z) * (1 - sstep(Z_SHOULDER, Z_SHOULDER + 0.06, v.z))
    fwd = cy - v.y  # model faces -Y in Blender; feet/claws sit in front of the folded wings
    wing *= 1 - sstep(0.0, 0.05, fwd) * (1 - sstep(Z_SHOULDER - 0.12, Z_SHOULDER - 0.02, v.z))
    side = "Wing_L" if v.x >= 0 else "Wing_R"
    rest = 1 - wing
    head = sstep(Z_HEAD - 0.06, Z_HEAD + 0.04, v.z)
    tail = 1 - sstep(Z_TAIL - 0.08, Z_TAIL + 0.04, v.z)
    t2 = 1 - sstep(Z_TAIL * 0.5 - 0.06, Z_TAIL * 0.5 + 0.06, v.z)
    w = {"Head": head, "Tail1": tail * (1 - t2), "Tail2": tail * t2}
    w["Body"] = max(0.0, 1 - head - tail)
    for k, val in w.items():
        if val * rest > 0.001: groups[k].add([i], val * rest, "REPLACE")
    if wing > 0.001: groups[side].add([i], wing, "REPLACE")
mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = arm
obj.parent = arm
# actions
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="POSE")
for pb in arm.pose.bones: pb.rotation_mode = "XYZ"
def action(name, frames, keys):
    act = bpy.data.actions.new(name); arm.animation_data_create(); arm.animation_data.action = act
    for pb in arm.pose.bones: pb.rotation_euler = (0, 0, 0)
    for f, poses in keys:
        for bn, rot in poses.items():
            pb = arm.pose.bones[bn]; pb.rotation_euler = [math.radians(a) for a in rot]
            pb.keyframe_insert("rotation_euler", frame=f)
    act.use_fake_user = True; act.frame_range = (0, frames)
    return act
# bone-local axes: wing bones point down (-Z); local Y is along the bone, so X/Z rotate. Flap = swing outward.
def fl(a): return {"Wing_L": (0, 0, -a), "Wing_R": (0, 0, a)}
action("Idle", 48, [(0, {"Head": (0, 0, 0), "Tail1": (0, 0, 0)}), (24, {"Head": (6, 0, 4), "Tail1": (4, 0, 0)}), (48, {"Head": (0, 0, 0), "Tail1": (0, 0, 0)})])
action("Flap", 12, [(0, {**fl(10), "Body": (0, 0, 0)}), (6, {**fl(70), "Body": (3, 0, 0)}), (12, {**fl(10), "Body": (0, 0, 0)})])
action("TailWag", 24, [(0, {"Tail1": (0, 0, -18), "Tail2": (0, 0, -10)}), (12, {"Tail1": (0, 0, 18), "Tail2": (0, 0, 10)}), (24, {"Tail1": (0, 0, -18), "Tail2": (0, 0, -10)})])
for a in bpy.data.actions: print("ACTION", a.name, tuple(a.frame_range))
arm.animation_data.action = bpy.data.actions["Idle"]
bpy.ops.object.mode_set(mode="OBJECT")
bpy.context.scene.render.fps = 24
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=os.path.join(outdir, "Duko_rigged.glb"), export_format="GLB", export_image_format="JPEG",
                          export_animation_mode="ACTIONS", export_force_sampling=True, export_yup=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, "Duko_rigged.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         path_mode="COPY", embed_textures=True, add_leaf_bones=False, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
img = obj.data.materials[0].node_tree.nodes
for n in img:
    if n.type == "TEX_IMAGE": n.image.save_render(os.path.join(outdir, "Duko_basecolor.png"))
