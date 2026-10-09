"""Shared helpers for the AURELIA environment pipeline (headless Blender 4.2 LTS).

High poly -> game mesh -> xatlas UVs -> Cycles bake (albedo, tangent normal OpenGL, AO/rough) -> LODs -> FBX.
Run scripts with:
  PYTHONPATH="$HOME/Library/Application Support/Blender/4.2/scripts/modules" \
  ~/Applications/Blender42.app/Contents/MacOS/Blender -b --factory-startup --python-use-system-env -P <script> -- <args>
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
SRC = os.path.join(REPO, 'art_source', 'environment')
WORK = os.path.join(SRC, 'work')                  # git-ignored intermediates
PREVIEWS = os.path.join(SRC, 'previews')
UNITY_ENV = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Environment')
CC0_TEX = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'CC0', 'Textures')


def args():
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def work(*p):
    path = os.path.join(WORK, *p)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    return path


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def gpu_cycles(samples=16):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'METAL'
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == 'METAL'
    sc.cycles.device = 'GPU'
    sc.cycles.samples = samples
    return sc


def set_active(obj, *others):
    bpy.context.view_layer.update()
    for o in list(bpy.context.scene.objects):
        o.select_set(False)
    for o in others:
        o.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def tris(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


# ------------------------------------------------------------------ noise (numpy, deterministic)
class Noise:
    """Smooth value noise in 1D/3D from a seeded lattice (enough for wobble and variation)."""

    def __init__(self, seed):
        self.rng = np.random.default_rng(seed)
        self.lat = self.rng.random((32, 32, 32)) * 2 - 1
        self.l1 = self.rng.random(1024) * 2 - 1

    def n1(self, x):
        x = np.asarray(x, float)
        i = np.floor(x).astype(int)
        f = x - i
        f = f * f * (3 - 2 * f)
        a, b = self.l1[i % 1024], self.l1[(i + 1) % 1024]
        return a + (b - a) * f

    def fbm1(self, x, oct=3):
        return sum(self.n1(x * 2 ** o + 17.3 * o) / 2 ** o for o in range(oct)) / 1.75

    def n3(self, p):
        p = np.asarray(p, float)
        i = np.floor(p).astype(int)
        f = p - i
        f = f * f * (3 - 2 * f)
        out = 0
        for dx in (0, 1):
            for dy in (0, 1):
                for dz in (0, 1):
                    w = (np.where(dx, f[..., 0], 1 - f[..., 0]) * np.where(dy, f[..., 1], 1 - f[..., 1])
                         * np.where(dz, f[..., 2], 1 - f[..., 2]))
                    out = out + w * self.lat[(i[..., 0] + dx) % 32, (i[..., 1] + dy) % 32, (i[..., 2] + dz) % 32]
        return out

    def vec3(self, p):
        return np.stack([self.n3(p), self.n3(p + 31.7), self.n3(p + 71.3)], -1)


# ------------------------------------------------------------------ curves and tubes
def catmull(points, n):
    """Centripetal Catmull-Rom through points, resampled to n points by arc length."""
    P = np.asarray(points, float)
    P = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    dense = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1:i + 3]
        for t in np.linspace(0, 1, 40, endpoint=False):
            t2, t3 = t * t, t * t * t
            dense.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                                + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    dense.append(P[-2])
    return resample(np.array(dense), n)


def resample(D, n):
    seg = np.linalg.norm(np.diff(D, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, D[:, k]) for k in range(3)], -1)


def frames(C, up=(0, 0, 1)):
    """Parallel-transport frames (T, N, B) along polyline C."""
    T = np.gradient(C, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True) + 1e-9
    n0 = np.cross(T[0], up)
    if np.linalg.norm(n0) < 1e-3:
        n0 = np.cross(T[0], (1, 0, 0))
    n0 /= np.linalg.norm(n0)
    N = [n0]
    for i in range(1, len(C)):
        v = np.cross(T[i - 1], T[i])
        n = N[-1]
        if np.linalg.norm(v) > 1e-9:
            ax = v / np.linalg.norm(v)
            ang = math.acos(max(-1, min(1, float(np.dot(T[i - 1], T[i])))))
            n = (n * math.cos(ang) + np.cross(ax, n) * math.sin(ang) + ax * np.dot(ax, n) * (1 - math.cos(ang)))
        n = n - T[i] * np.dot(n, T[i])
        N.append(n / np.linalg.norm(n))
    N = np.array(N)
    return T, N, np.cross(T, N)


def tube(centers, radii, ring=14, groove=(0.0, 5, 0.0), cap=True, attr=None):
    """Tube mesh arrays around a polyline. groove = (depth, count, twist per metre) adds rope-fibre grooves.
    Returns (verts, faces, per-vertex attr rows)."""
    C = np.asarray(centers, float)
    R = np.asarray(radii, float)
    T, N, B = frames(C)
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(C, axis=0), axis=1))])
    ph = np.linspace(0, 2 * math.pi, ring, endpoint=False)
    depth, count, tw = groove
    ang = ph[None, :] + (tw * L)[:, None]
    mod = 1 + depth * (np.sin(count * ang) * 0.6 + np.sin((count * 2 + 1) * ang + 1.3) * 0.4)
    rr = R[:, None] * mod
    V = (C[:, None, :] + rr[..., None] * (np.cos(ph)[None, :, None] * N[:, None, :]
                                          + np.sin(ph)[None, :, None] * B[:, None, :]))
    V = V.reshape(-1, 3)
    F = []
    m = len(C)
    for i in range(m - 1):
        for j in range(ring):
            a, b = i * ring + j, i * ring + (j + 1) % ring
            F.append((a, b, b + ring, a + ring))
    if cap:
        c0 = len(V)
        V = np.vstack([V, C[0] - T[0] * R[0] * 0.5, C[-1] + T[-1] * R[-1] * 0.5])
        for j in range(ring):
            F.append((c0, (j + 1) % ring, j))
            F.append((c0 + 1, (m - 1) * ring + j, (m - 1) * ring + (j + 1) % ring))
    A = None
    if attr is not None:
        # R = strand random, G = metres along the strand, B/A = cos/sin of the angle around the strand
        # (cos/sin interpolate cleanly through the voxel remesh; a 0..1 phase leaves a seam line)
        u = np.repeat(L, ring)
        A = np.zeros((len(V), 4))
        A[:, 0] = attr[0]
        A[:len(u), 1] = u
        A[:len(u), 2] = np.tile(np.cos(ph), len(C))
        A[:len(u), 3] = np.tile(np.sin(ph), len(C))
    return V, F, A


def mesh_from(name, parts):
    """parts: list of (V, F, A). Builds one mesh object with a 'strand' float-color attribute."""
    Vs, Fs, As, off = [], [], [], 0
    for V, F, A in parts:
        Vs.append(V)
        Fs.extend([tuple(i + off for i in f) for f in F])
        As.append(A if A is not None else np.zeros((len(V), 4)))
        off += len(V)
    V = np.vstack(Vs)
    me = bpy.data.meshes.new(name)
    me.from_pydata(V.tolist(), [], Fs)
    me.validate()
    ca = me.color_attributes.new('strand', 'FLOAT_COLOR', 'POINT')
    ca.data.foreach_set('color', np.vstack(As).ravel().astype(np.float32))
    ob = bpy.data.objects.new(name, me)
    link(ob)
    return ob


def voxel_remesh(ob, size):
    set_active(ob)
    ob.data.remesh_voxel_size = size
    ob.data.use_remesh_preserve_attributes = True
    ob.data.use_remesh_preserve_volume = False
    ob.data.use_remesh_fix_poles = False
    bpy.ops.object.voxel_remesh()


def cut_below(ob, z):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1), clear_inner=True)
    bm.to_mesh(ob.data)
    bm.free()


def tex(name, kind, **kw):
    t = bpy.data.textures.new(name, kind)
    for k, v in kw.items():
        setattr(t, k, v)
    return t


def displace(ob, texture, strength, mid=0.5, size=None, apply=True):
    m = ob.modifiers.new('disp', 'DISPLACE')
    m.texture = texture
    m.strength = strength
    m.mid_level = mid
    m.texture_coords = 'OBJECT' if size is None else 'LOCAL'
    if apply:
        set_active(ob)
        bpy.ops.object.modifier_apply(modifier=m.name)


def smooth(ob, factor=0.5, iters=2):
    m = ob.modifiers.new('sm', 'SMOOTH')
    m.factor, m.iterations = factor, iters
    set_active(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)


def decimate_to(ob, target):
    n = tris(ob)
    if n <= target:
        return
    m = ob.modifiers.new('dec', 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.use_collapse_triangulate = True
    m.ratio = target / n
    set_active(ob)
    bpy.ops.object.modifier_apply(modifier=m.name)


def copy_obj(ob, name):
    c = ob.copy()
    c.data = ob.data.copy()
    c.name = c.data.name = name
    for m in list(c.modifiers):
        c.modifiers.remove(m)
    link(c)
    return c


# ------------------------------------------------------------------ UVs
def xatlas_uv(ob, padding_px=4, res=1024, name='UVMap'):
    import xatlas
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name=name)
    co = np.array([v.co[:] for v in me.vertices], np.float32)
    idx = np.array([p.vertices[:] for p in me.polygons], np.uint32)
    atlas = xatlas.Atlas()
    atlas.add_mesh(co, idx)
    copt = xatlas.ChartOptions()
    copt.max_iterations = 4
    copt.max_cost = 16
    copt.normal_seam_weight = 4
    pk = xatlas.PackOptions()
    pk.resolution = 0          # one unbounded sheet, scaled uniformly into 0-1 below
    pk.padding = padding_px
    pk.bilinear = True
    pk.rotate_charts = True
    pk.bruteForce = False
    # find texels-per-unit so everything fits one page
    area = sum(p.area for p in me.polygons)
    pk.texels_per_unit = res * 0.85 / math.sqrt(area)
    atlas.generate(copt, pk)
    vmap, tri, uvs = atlas[0]
    uvd = me.uv_layers[name].data
    assert atlas.atlas_count == 1, atlas.atlas_count
    sx = atlas.width / max(atlas.width, atlas.height)
    sy = atlas.height / max(atlas.width, atlas.height)
    for t, p in enumerate(me.polygons):
        for k, li in enumerate(p.loop_indices):
            u_, v_ = uvs[tri[t][k]]
            uvd[li].uv = (u_ * sx, v_ * sy)
    for p in me.polygons:
        p.use_smooth = True
    print(f'{ob.name}: xatlas {atlas.chart_count} charts, {atlas.width}x{atlas.height}, '
          f'util {atlas.utilization[0] if hasattr(atlas.utilization, "__len__") else atlas.utilization:.2f}, '
          f'~{res * math.sqrt(max(atlas.utilization if not hasattr(atlas.utilization, "__len__") else atlas.utilization[0], 1e-3) / area):.0f} px/m')


def xatlas_faces(ob, faces, res, padding_px=4, weights=None, rect=(0.0, 0.0, 1.0, 1.0), name='UVMap',
                 fill=0.85):
    """Unique UVs for a subset of a triangulated mesh's faces, packed into `rect` (u0, v0, u1, v1) of layer `name`.
    weights: per-face texel-density multiplier (1 = full). Faces are charted on copies scaled by their weight, so a
    face with weight 0.5 gets half the texels per metre; faces of different weight never share a chart.
    Returns the unique density (px/m) of weight-1 faces for a `res` texture."""
    import xatlas
    me = ob.data
    if name not in me.uv_layers:
        me.uv_layers.new(name=name)
    uvd = me.uv_layers[name].data
    co = np.array([v.co[:] for v in me.vertices], np.float64)
    w = np.ones(len(faces)) if weights is None else np.asarray(weights, float)
    key, pos, idx = {}, [], []
    for fi, f in enumerate(faces):
        p = me.polygons[f]
        assert len(p.vertices) == 3, 'triangulate first'
        tri = []
        for vi in p.vertices:
            k = (vi, round(float(w[fi]), 3))
            if k not in key:
                key[k] = len(pos)
                pos.append(co[vi] * w[fi])
            tri.append(key[k])
        idx.append(tri)
    pos = np.array(pos, np.float32)
    idx = np.array(idx, np.uint32)
    atlas = xatlas.Atlas()
    atlas.add_mesh(pos, idx)
    copt = xatlas.ChartOptions()
    copt.max_iterations = 4
    copt.max_cost = 16
    copt.normal_seam_weight = 4
    pk = xatlas.PackOptions()
    pk.resolution = 0
    pk.padding = padding_px
    pk.bilinear = True
    pk.rotate_charts = True
    rw, rh = rect[2] - rect[0], rect[3] - rect[1]
    area = sum(me.polygons[f].area * w[i] ** 2 for i, f in enumerate(faces))
    pk.texels_per_unit = res * math.sqrt(rw * rh) * fill / math.sqrt(area)
    atlas.generate(copt, pk)
    assert atlas.atlas_count == 1, atlas.atlas_count
    vmap, tri, uvs = atlas[0]
    big = max(atlas.width / rw, atlas.height / rh)       # texels of the atlas sheet per unit of the full texture
    for t, f in enumerate(faces):
        for k, li in enumerate(me.polygons[f].loop_indices):
            u_, v_ = uvs[tri[t][k]]
            uvd[li].uv = (rect[0] + u_ * atlas.width / big, rect[1] + v_ * atlas.height / big)
    dens = pk.texels_per_unit * res / big
    util = atlas.utilization[0] if hasattr(atlas.utilization, '__len__') else atlas.utilization
    print(f'{ob.name}: xatlas {len(faces)} faces, {atlas.chart_count} charts, sheet {atlas.width}x{atlas.height}, '
          f'util {util:.2f}, {dens:.1f} px/m at weight 1 ({res} px)')
    return dens


def triangulate(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(ob.data)
    bm.free()


def box_uv2(ob, tile_m, name='UV1_Detail'):
    """Second UV set: world-scale box projection (1 unit = tile_m metres) for a tiling detail texture."""
    me = ob.data
    if name in me.uv_layers:
        me.uv_layers.remove(me.uv_layers[name])
    uv = me.uv_layers.new(name=name)
    for p in me.polygons:
        n = p.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            a, b = [(v.y, v.z), (v.x, v.z), (v.x, v.y)][ax]
            uv.data[li].uv = (a / tile_m, b / tile_m)


# ------------------------------------------------------------------ bake
def new_image(name, res, non_color=False, alpha=False):
    img = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = 'Non-Color'
    return img


def bake_target(low, img):
    """Ensure low has a material whose active node is an image node with img."""
    if not low.data.materials:
        low.data.materials.append(bpy.data.materials.new(low.name + '_bake'))
    for mat in low.data.materials:
        mat.use_nodes = True
        nt = mat.node_tree
        node = nt.nodes.get('BAKE_TARGET') or nt.nodes.new('ShaderNodeTexImage')
        node.name = 'BAKE_TARGET'
        node.image = img
        nt.nodes.active = node


def rays_off(ob):
    """The bake target must not occlude: with selected-to-active, AO rays start on the high surface, and wherever
    the low mesh lies outside it they hit the low mesh at once (AO 0 in patches). Hide it from every ray type."""
    for k in ('visible_diffuse', 'visible_glossy', 'visible_transmission', 'visible_volume_scatter', 'visible_shadow'):
        setattr(ob, k, False)


def bake(high, low, kind, img, extrusion, ray, samples=8, pass_filter=None, normal_space='TANGENT'):
    sc = bpy.context.scene
    rays_off(low)
    sc.cycles.samples = samples
    bake_target(low, img)
    sc.render.bake.use_selected_to_active = high is not None
    sc.render.bake.cage_extrusion = extrusion
    sc.render.bake.max_ray_distance = ray
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    set_active(low, *( [high] if high is not None else []))
    kw = dict(type=kind, use_clear=True, margin=8)
    if pass_filter:
        kw['pass_filter'] = pass_filter
    if kind == 'NORMAL':
        kw.update(normal_space=normal_space, normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z')
    bpy.ops.object.bake(**kw)


def save_png(img, path, mode='RGB'):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc = bpy.context.scene
    s = sc.render.image_settings
    s.file_format = 'PNG'
    s.color_mode = mode
    s.color_depth = '8'
    s.compression = 90
    img.save_render(path, scene=sc)
    print('wrote', os.path.relpath(path, REPO))


def pixels(img):
    a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


def set_pixels(img, arr):
    img.pixels.foreach_set(arr.astype(np.float32).ravel())


# ------------------------------------------------------------------ export
def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.view_layer.update()
    for o in list(bpy.context.scene.objects):
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=False,
        mesh_smooth_type='FACE', use_tspace=False, use_mesh_modifiers=False, use_custom_props=False,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False, colors_type='LINEAR')
    print('exported', os.path.relpath(path, REPO), os.path.getsize(path))
