"""RS_HeroArch_P v2: the painterly hero arch as clean, evenly spaced round strands (F4_f), built as the game mesh.

v1 (painterly_arch_high.py + painterly_rock.py RS_HeroArch) voxel-merged 8 strands, then decimated a 2 M-tri high to
14k tris: the strands melted into each other and the seams went soft and uneven. Here the game mesh is the strands
themselves: 8 round tubes (three ropes of 3/3/2, even spacing inside each rope, constant radius, a gentle twist), the
same spine, footprint, leg splay and root tails as v2 (Span placement and WaterfallMouth stay valid), no voxel merge.
Faces buried inside another strand are culled (painterly_slabs.cull_hidden), the mesh is decimated to the budget
(mostly along the straight runs), and the paint (painterly_rock.painted_material: warm top / cool base gradient,
tonal patches, warm-brown painted AO in the seams, top-edge highlights, moss on up-facing tops, rock-swatch strokes)
is baked onto the game mesh itself, so the seams are painted exactly where they are.
Outputs as before: Rootstone/RS_HeroArch_P.fbx (RS_HeroArch_P_A_LODn / _B_LODn split at Blender x = -1, one material
each, + RS_HeroArch_P_WaterfallMouth), Rootstone/Textures/RS_HeroArch_P_{A,B}_{BaseColor,Normal,ARM}.png (2048),
work/rootstone/RS_HeroArch_P_game.blend. Then painterly_plants.py vines.
Usage: blender ... -P painterly_arch_strands.py [-- --geo-only]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_bake import DETAIL_TILE_M, OUT  # noqa: E402
from rootstone_high import keyed  # noqa: E402
from heroarch_v2_bake import openness, set_targets  # noqa: E402
from heroarch_v2_high import SPEC as V2, splay  # noqa: E402
from painterly_rock import PIECES as RP, copy_extras, fit_mouth, painted_material, preview_mat  # noqa: E402
from painterly_slabs import cull_hidden  # noqa: E402

NAME = 'RS_HeroArch_P'
LOD = (15000, 6300, 2100)          # v1: 14,000 / 6,299 / 2,100 (cap +10 %: 15,400)
RES = 2048
SPLIT_X = -1.0
SP = dict(
    ropes=[dict(n=3, splay=(1.25, 0.7)), dict(n=3, splay=(0.55, 1.2)), dict(n=2, splay=(0.9, 0.95))],
    rope_off=0.62, rope_r=0.62, rope_turns=0.6, strand_turns=0.85, ring=14, step=0.9, seed=112)


def strands():
    """Centre lines and radii of the 8 strands (+ the recessed core), as v2 but even and round."""
    rng = np.random.default_rng(SP['seed'])
    nz = E.Noise(SP['seed'])
    spine = np.array(V2['spine'], float)
    length = float(np.sum(np.linalg.norm(np.diff(spine, axis=0), axis=1)))
    m = int(length / 0.35)
    C = E.catmull(spine, m)
    T, N, B = E.frames(C)
    u = np.linspace(0, 1, m)
    Rb = keyed(V2['rb'], u) * (1 + 0.05 * nz.fbm1(u * length / 14 + 3.1))
    sa, sb = splay(u)
    out = []

    def tail(P, r, end, center, theta):
        """Planted end: the strand leaves its rope and runs out over the ground as a root (as v2, gentler curl)."""
        e = P[0] if end == 0 else P[-1]
        d = np.array([e[0] - center[0], e[1] - center[1], 0.0])
        if np.linalg.norm(d) < 1e-3:
            d = np.array([math.cos(theta), math.sin(theta), 0])
        d /= np.linalg.norm(d)
        side = np.array([-d[1], d[0], 0])
        r0 = r[0] if end == 0 else r[-1]
        L = r0 * rng.uniform(2.2, 3.8)
        curl = rng.uniform(-0.35, 0.35)
        tp, tr = [], []
        for k in range(1, 15):
            t = k / 14
            p = e + d * L * t + side * L * curl * t * t
            p[2] = e[2] * (1 - t) ** 1.3 - r0 * 0.9 * t ** 1.5
            tp.append(p)
            tr.append(r0 * (1 - 0.4 * t))
        if end == 0:
            return np.array(tp[::-1] + list(P)), np.array(tr[::-1] + list(r))
        return np.array(list(P) + tp), np.array(list(r) + tr)

    core = (u > 0.14) & (u < 0.86)
    cr = Rb[core] * 0.42 * np.clip(np.minimum(u[core] - 0.14, 0.86 - u[core]) / 0.08, 0.15, 1)
    out.append((C[core], cr, 10))
    sid = 0
    for k, rope in enumerate(SP['ropes']):
        phi = 2 * math.pi * k / len(SP['ropes']) + 2 * math.pi * SP['rope_turns'] * u + 0.25 * nz.fbm1(u * 4 + k * 7.1)
        rho = Rb * (SP['rope_off'] + rope['splay'][0] * sa + rope['splay'][1] * sb)
        Rk = C + rho[:, None] * (np.cos(phi)[:, None] * N + np.sin(phi)[:, None] * B)
        Rsub = Rb * SP['rope_r']
        Tk, Nk, Bk = E.frames(Rk)
        nst = rope['n']
        sdir = 1 if k % 2 == 0 else -1
        for j in range(nst):
            sid += 1
            psi = (2 * math.pi * j / nst + 2 * math.pi * SP['strand_turns'] * sdir * u
                   + 0.08 * nz.fbm1(u * 5 + sid * 3.7))
            sig = Rsub * (0.62 if nst == 3 else 0.5)
            # soft plate bulges along the strand (~6 m), the painted segments of F4_f without cracks (AD s2)
            bul = 1 + 0.035 * np.sin(2 * math.pi * u * length / 7.0 + sid * 2.3)
            rad = Rsub * (0.52 if nst == 3 else 0.56) * (1 + 0.04 * nz.fbm1(u * length / 8 + sid * 9.1)) * bul
            P = Rk + sig[:, None] * (np.cos(psi)[:, None] * Nk + np.sin(psi)[:, None] * Bk)
            P, r = tail(P, rad, 0, Rk[0], psi[0])
            P, r = tail(P, r, 1, Rk[-1], psi[-1])
            out.append((P, r, SP['ring']))
    # one thin root wrapping the outside of the right leg (F4_f: a few loose roots, not many)
    sel = u > 0.62
    th = 1.2 - 2 * math.pi * 1.1 * u[sel]
    off = Rb[sel] * (SP['rope_off'] + SP['rope_r'] * 1.05 + 0.6 * sb[sel])
    P = C[sel] + off[:, None] * (np.cos(th)[:, None] * N[sel] + np.sin(th)[:, None] * B[sel])
    r = Rb[sel] * 0.13
    r[:10] *= np.linspace(0.4, 1, 10)
    r[-12:] *= np.linspace(1, 0.5, 12)
    P[-12:, 2] -= np.linspace(0, 1.0, 12) * r[-12:] * 2.0       # the end dives into the ground
    out.append((P, r, 10))
    return out


def build_mesh():
    E.reset()
    parts = []
    for P, r, ring in strands():
        L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
        n = max(4, int(L[-1] / SP['step']))
        t = np.linspace(0, L[-1], n)
        Pr = np.stack([np.interp(t, L, P[:, k]) for k in range(3)], -1)
        rr = np.interp(t, L, r)
        parts.append(E.tube(Pr, rr, ring=ring, cap=True, attr=(0.0, 0.0)))
    ob = E.mesh_from(NAME + '_LOD0', parts)
    for a in list(ob.data.color_attributes):
        ob.data.color_attributes.remove(a)
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(ob.data)
    bm.free()
    E.cut_below(ob, -0.6)
    E.triangulate(ob)
    print(NAME, 'built tris', E.tris(ob))
    print(NAME, 'hidden faces culled', cull_hidden(ob))
    print(NAME, 'after cull', E.tris(ob))
    E.decimate_to(ob, LOD[0])
    for p in ob.data.polygons:
        p.use_smooth = True
    return ob


def set_mats(me, mats, idx):
    """Replace the material slots, keeping the per-face half index (materials.clear() resets material_index)."""
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    me.polygons.foreach_set('material_index', idx)


def bake_self(ob, kind, imgs, samples, **kw):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    set_targets(ob, imgs)
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    E.set_active(ob)
    a = dict(type=kind, use_clear=True, margin=8)
    a.update(kw)
    bpy.ops.object.bake(**a)


def build(geo_only=False):
    low = build_mesh()
    sc = E.gpu_cycles(8)
    if geo_only:
        bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_strandgeo.blend'), compress=True)
        return
    me = low.data
    me.normals_split_custom_set_from_vertices([v.normal[:] for v in me.vertices])
    height = max((low.matrix_world @ Vector(c)).z for c in low.bound_box)
    sp = dict(RP['RS_HeroArch'])
    halves = ('A', 'B')
    paints = []
    for h in halves:                                  # same paint, one slot per half (one bake target each)
        pm = painted_material(sp, height)
        pm.name = f'paint_{h}'
        me.materials.append(pm)
        paints.append(pm)
    op = openness(low, dirs=16)
    groups, wts = [[], []], [[], []]
    away = Vector((0, -1, 0))
    for p in me.polygons:
        k = 0 if p.center.x < SPLIT_X else 1
        p.material_index = k
        w = 0.6 if p.normal.dot(away) > 0.35 else (0.75 if p.normal.z > 0.6 else 1.0)
        groups[k].append(p.index)
        wts[k].append(round(w * (0.4 + 0.6 * min(1.0, op[p.index] / 0.7)), 1))
    idx = np.array([p.material_index for p in me.polygons], np.int32)
    me.uv_layers.new(name='UVMap')
    dens = [E.xatlas_faces(low, groups[k], RES, padding_px=6, weights=wts[k]) for k in range(2)]
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0.0))
    ground = bpy.context.active_object
    sc.world = bpy.data.worlds.new('w')

    def imgs(tag, nc=False):
        return [E.new_image(f'{NAME}_{h}_{tag}', RES, non_color=nc) for h in halves]
    base = imgs('BaseColor')
    bake_self(low, 'DIFFUSE', base, 32, pass_filter={'COLOR'})
    nrm = imgs('Normal', True)
    bake_self(low, 'NORMAL', nrm, 8, normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y', normal_b='POS_Z')
    rgh = imgs('rough', True)
    bake_self(low, 'ROUGHNESS', rgh, 4)
    aoms = []
    for h in halves:
        aom = bpy.data.materials.new(f'ao_{h}'); aom.use_nodes = True
        aon = aom.node_tree.nodes.new('ShaderNodeAmbientOcclusion')
        aon.samples, aon.only_local = 32, False
        aon.inputs['Distance'].default_value = sp['ao_dist']
        aom.node_tree.links.new(aon.outputs['AO'], aom.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
        aoms.append(aom)
    set_mats(me, aoms, idx)
    ao = imgs('ao', True)
    # AO source: a once-subdivided copy (smooth, within cm of the game mesh): AO rays cast from the long, thin
    # triangles of the decimated mesh itself self-shadow in triangle-shaped patches
    src = E.copy_obj(low, NAME + '_aosrc')
    sub = src.modifiers.new('sub', 'SUBSURF')
    sub.levels = 1
    E.set_active(src)
    bpy.ops.object.modifier_apply(modifier=sub.name)
    E.rays_off(low)
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.25
    sc.render.bake.max_ray_distance = 0.6
    sc.cycles.samples = 96
    set_targets(low, ao)
    E.set_active(low, src)
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True, margin=8)
    bpy.data.objects.remove(src)
    for k in ('visible_diffuse', 'visible_glossy', 'visible_transmission', 'visible_volume_scatter', 'visible_shadow'):
        setattr(low, k, True)
    bpy.data.objects.remove(ground)

    tdir = os.path.join(OUT, 'Textures')
    mats = []
    for k, h in enumerate(halves):
        arm = E.new_image(f'{NAME}_{h}_ARM', RES, non_color=True)
        o = np.zeros((RES, RES, 4), np.float32)
        o[..., 0] = np.clip(E.pixels(ao[k])[..., 0], 0.3, 1) ** 1.2
        o[..., 1] = E.pixels(rgh[k])[..., 0]
        o[..., 3] = 1
        E.set_pixels(arm, o)
        for im, tag in ((base[k], 'BaseColor'), (nrm[k], 'Normal'), (arm, 'ARM')):
            E.save_png(im, os.path.join(tdir, f'{NAME}_{h}_{tag}.png'))
            im.pack()
        mt = bpy.data.materials.new(f'MI_{NAME}_{h}')
        mt.use_nodes = True
        preview_mat(mt, base[k], nrm[k], arm)
        mats.append(mt)
    set_mats(me, mats, idx)

    lods = [low]
    for i in (1, 2):
        o_ = E.copy_obj(low, f'{NAME}_LOD{i}')
        E.decimate_to(o_, LOD[i])
        lods.append(o_)
    objs = []
    for i, o_ in enumerate(lods):
        o_.data.normals_split_custom_set_from_vertices([v.normal[:] for v in o_.data.vertices])
        E.set_active(o_)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.separate(type='MATERIAL')
        bpy.ops.object.mode_set(mode='OBJECT')
        parts = [x for x in bpy.context.scene.objects if x.type == 'MESH' and (x == o_ or x.name.startswith(o_.name + '.'))]
        for x in parts:
            h = 'A' if x.data.materials[x.data.polygons[0].material_index].name.endswith('_A') else 'B'
            x.name = x.data.name = f'{NAME}_{h}_LOD{i}'
            E.box_uv2(x, DETAIL_TILE_M)
            objs.append(x)
    objs.sort(key=lambda x: x.name)
    extras = copy_extras('RS_HeroArch')
    for e in extras:                        # copy_extras names them RS_HeroArch_P_*
        print('extra', e.name)
    fit_mouth([x for x in objs if x.name.endswith('_LOD0')], extras)
    E.export_fbx(objs + extras, os.path.join(OUT, NAME + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', NAME + '_game.blend'), compress=True)
    lod_tris = [sum(E.tris(x) for x in objs if x.name.endswith(f'_LOD{i}')) for i in range(3)]
    pts = [x.matrix_world @ Vector(c) for x in objs if x.name.endswith('_LOD0') for c in x.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 1) for i in range(3)]
    print('SUMMARY', NAME, size, lod_tris, RES, 'px/m', [round(d, 1) for d in dens])


if __name__ == '__main__':
    build(geo_only='--geo-only' in E.args())
