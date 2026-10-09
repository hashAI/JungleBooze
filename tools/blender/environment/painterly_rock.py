"""Painterly (F4_f, option D trial) variants of the hero basin rock kit: <piece>_P next to the realistic pieces.

Shape: the realistic high poly is inflated a little, voxel-remeshed coarser and smoothed, so chips, grain and fine
cracks go away and the big braided strands stay. The hero arch has its own chunkier high (painterly_arch_high.py:
8 thick strands instead of 14, no weathering). LOD0 is decimated from that
simplified high (new UVs; same tri budgets as the realistic pieces).
Paint (baked to BaseColor from a procedural bake-source material on the simplified high, 'painted' rather than
photographic): a large warm-to-cool gradient (warm cream-tan on top and on up-facing planes, cooler grey-mauve low
and on down-facing planes), broad tonal patches, a faint per-cell tone (Voronoi cells about a strand diameter; the
crack lines between cells are off: ART_DIRECTION_PAINTERLY s2 allows no cracks inside a strand), painted AO in the crevices (warm dark
brown, not black), light edge highlights on convex top edges, moss as broad painted areas on up-facing faces
(OpenAI painted moss swatch, box projected), the OpenAI painted rock swatch as a soft brush-stroke overlay (about 10 % contrast).
Roughness: flat constants (stone 0.85, moss 0.9, wet travertine 0.35).
Normal: the simplified shape plus a faint stroke bump and the moss clumps (broad forms only, AD s3).
ARM: R = AO (colour bake of an AO node incl. ground contact), G = roughness, B = 0.

Inputs: work/rootstone/<piece>_high.blend, work/rootstone/<piece>_game.blend (anchors/empties are copied, renamed
<piece>_P_*), work/painterly/swatch_rock_tile.png, swatch_moss_tile.png (tools/art/painterly_finish.py swatches)
Outputs: Rootstone/<piece>_P.fbx (<piece>_P_LOD0..2; the hero arch: RS_HeroArch_P_A_LODn / _B_LODn, one material
each), Rootstone/Textures/<piece>_P_{BaseColor,Normal,ARM}.png (hero arch: _P_A_ / _P_B_), work/rootstone/<piece>_P_game.blend
Usage: blender ... -P painterly_rock.py -- <piece|all> [--simplify-only]
"""
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from bakekit import Nodes, srgb  # noqa: E402
from rootstone_bake import DETAIL_TILE_M, OUT  # noqa: E402
from heroarch_v2_bake import openness, set_targets  # noqa: E402

PAINT = os.path.join(E.REPO, 'art_source', 'environment', 'work', 'painterly')

# voxel: simplify remesh (None = smooth only, keeps attributes/anchors exact); smooth: (factor, iterations);
# inflate: m along the normal before the remesh; cell: carved plate size (m); away: normal direction the camera
# never sees (texel weight 0.6); halves: hero arch split x (two materials)
PIECES = {
    'RS_HeroArch': dict(src='RS_HeroArch_P', lod=(14000, 6300, 2100), res=2048, voxel=None, smooth=(0.5, 2),
                        inflate=0.0, cell=2.6,
                        away=(0, -1, 0), halves=-1.0, ext=0.5, ray=1.2, ao_dist=3.0, moss=1.0),
    'RS_PillarA': dict(lod=(4000, 1800, 600), res=1024, voxel=0.12, smooth=(0.5, 2), inflate=0.04, cell=1.7,
                       ext=0.4, ray=0.9, ao_dist=2.0, moss=1.0),
    'RS_PillarB': dict(lod=(4000, 1800, 600), res=1024, voxel=0.12, smooth=(0.5, 2), inflate=0.04, cell=1.7,
                       ext=0.4, ray=0.9, ao_dist=2.0, moss=1.0),
    'RS_ArchSmall': dict(lod=(4000, 1800, 600), res=1024, voxel=0.09, smooth=(0.5, 2), inflate=0.03, cell=1.2,
                         ext=0.3, ray=0.7, ao_dist=1.5, moss=1.0),
    'RS_Outcrop': dict(lod=(2500, 1100, 400), res=1024, voxel=0.05, smooth=(0.5, 2), inflate=0.02, cell=0.8,
                       ext=0.2, ray=0.5, ao_dist=0.8, moss=1.1),
    'RS_TravertineTiers': dict(lod=(5000, 2200, 800), res=1024, voxel=None, smooth=(0.5, 3), inflate=0.0, cell=1.4,
                               away=(0, 1, 0), ext=0.2, ray=0.5, ao_dist=1.5, moss=1.2, trav=True),
    'RS_LedgeLookout': dict(lod=(2500, 1100, 400), res=1024, voxel=0.05, smooth=(0.6, 3), inflate=0.0, cell=0.75,
                            away=(0, 1, 0), ext=0.15, ray=0.4, ao_dist=0.8, moss=1.0, trav=True),
}

# ART_DIRECTION_PAINTERLY s3/s8 hexes are picked from the lit F4_f image (sun colour included); as albedo under the
# warm sun they rendered mustard-olive, so the stone albedo is the same hue family, less saturated (lit render of
# these lands near the s8 sandstone values). Painted AO and edge highlight as s3.
C = dict(
    light='#CFB084', mid='#A68864', cool='#7A6E5E', mauve='#8C7B70', crevice='#4D3A22', edge='#F7DFB2',
    dirt='#6A5642', moss_dark='#34420F', moss_mid='#5F600F', moss_light='#AFA23E', wet='#655A3F', algae='#278677')


def close_holes(ob):
    """The highs are cut open below ground (cut_below): cap them so the voxel remesh sees a closed volume."""
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    edges = [e for e in bm.edges if e.is_boundary]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(ob.data)
    bm.free()
    return len(edges)


def simplify(ob, sp):
    zmin = min(v.co.z for v in ob.data.vertices)
    if sp['voxel']:
        print(ob.name, 'capped boundary edges', close_holes(ob))
    if sp['inflate'] > 0:
        m = ob.modifiers.new('inflate', 'DISPLACE')
        m.strength, m.mid_level = sp['inflate'], 0.0
        E.set_active(ob)
        bpy.ops.object.modifier_apply(modifier=m.name)
    if sp['voxel']:
        E.voxel_remesh(ob, sp['voxel'])
    E.smooth(ob, *sp['smooth'])
    if sp['voxel']:
        E.cut_below(ob, zmin + 0.02)
    for p in ob.data.polygons:
        p.use_smooth = True
    print(ob.name, 'simplified tris', E.tris(ob))


def box_image(n, path, coord, tile, non_color=False):
    mp = n.node('ShaderNodeMapping', vector_type='POINT')
    mp.inputs['Scale'].default_value = (1 / tile,) * 3
    n.link(coord, mp.inputs[0])
    img = n.node('ShaderNodeTexImage', projection='BOX', projection_blend=0.4)
    img.image = bpy.data.images.load(path)
    if non_color:
        img.image.colorspace_settings.name = 'Non-Color'
    n.link(mp.outputs[0], img.inputs[0])
    return img


def painted_material(sp, height):
    m = bpy.data.materials.new('painterly_src')
    m.use_nodes = True
    n = Nodes(m)
    bs = n.N['Principled BSDF']
    tc = n.node('ShaderNodeTexCoord')
    geo = n.node('ShaderNodeNewGeometry')
    P = tc.outputs['Object']
    cell = sp['cell']
    sepp = n.node('ShaderNodeSeparateXYZ'); n.link(P, sepp.inputs[0])
    sepn = n.node('ShaderNodeSeparateXYZ'); n.link(geo.outputs['Normal'], sepn.inputs[0])
    nz = sepn.outputs[2]
    hgt = n.maprange(sepp.outputs[2], 0.0, height)

    # 1. big warm-to-cool gradient: cool low and on down-facing planes, warm light on top and up-facing planes
    up = n.maprange(nz, -0.6, 0.9)
    warm = n.math('ADD', n.math('MULTIPLY', hgt, 0.45), n.math('MULTIPLY', up, 0.55), clamp=True)
    col = n.mix(srgb(C['cool']), srgb(C['mid']), n.maprange(warm, 0.05, 0.5))
    col = n.mix(col, srgb(C['light']), n.maprange(warm, 0.45, 0.95))
    # 2. broad painted tonal patches (few octaves: shapes, not noise)
    col = n.mix(col, srgb(C['light']), n.maprange(n.noise(P, 1 / (cell * 3.5), 2, 0.4), 0.55, 0.7, 0, 0.35))
    col = n.mix(col, srgb(C['mauve']), n.maprange(n.noise(P, 1 / (cell * 2.5), 2, 0.4), 0.42, 0.3, 0, 0.3))

    # 3. carved plates: Voronoi cells about a strand diameter; per-cell tone, light bevel inside the edge, dark line
    vo = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='DISTANCE_TO_EDGE')
    vo.inputs['Scale'].default_value = 1 / cell
    vo.inputs['Randomness'].default_value = 0.85
    n.link(P, vo.inputs['Vector'])
    vf = n.node('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature='F1')
    vf.inputs['Scale'].default_value = 1 / cell
    vf.inputs['Randomness'].default_value = 0.85
    n.link(P, vf.inputs['Vector'])
    cell_rand = n.node('ShaderNodeSeparateColor'); n.link(vf.outputs['Color'], cell_rand.inputs[0])
    tone = n.maprange(cell_rand.outputs[0], 0, 1, 0.95, 1.04)
    col = n.mix(col, n.gray(tone), 1.0, 'MULTIPLY')
    d = vo.outputs['Distance']
    bevel = n.maprange(d, 0.035, 0.12, 0.35, 0.0)
    line = n.maprange(d, 0.0, 0.03, 0.85, 0.0)
    # AO for painted crevices (local + ground contact)
    ao = n.node('ShaderNodeAmbientOcclusion', samples=16, only_local=False)
    ao.inputs['Distance'].default_value = sp['ao_dist']
    aof = ao.outputs['AO']
    openf = n.maprange(aof, 0.35, 0.75)                 # no plate lines down in the crevices
    if sp.get('plates'):      # off by default: ART_DIRECTION_PAINTERLY s2 "no cracks inside a strand"
        col = n.mix(col, srgb(C['edge']), n.math('MULTIPLY', bevel, openf))
        col = n.mix(col, srgb(C['crevice']), n.math('MULTIPLY', line, openf))

    # 4. painted brush strokes: the OpenAI rock swatch's luminance as a soft overlay
    rk = box_image(n, os.path.join(PAINT, 'swatch_rock_tile.png'), P, cell * 3.0)
    lum = n.node('ShaderNodeRGBToBW'); n.link(rk.outputs['Color'], lum.inputs[0])
    col = n.mix(col, n.gray(n.maprange(lum.outputs[0], 0.25, 0.75, 0.94, 1.05)), 1.0, 'MULTIPLY')

    # 5. painted AO: warm dark brown in the crevices
    col = n.mix(col, srgb(C['crevice']), n.maprange(aof, 0.75, 0.1, 0.0, 0.6))

    # 6. light edge highlights on convex, up-facing edges
    pt = n.maprange(geo.outputs['Pointiness'], 0.51, 0.58)
    edge = n.math('MULTIPLY', pt, n.maprange(nz, 0.0, 0.6), clamp=True)
    col = n.mix(col, srgb(C['edge']), n.math('MULTIPLY', edge, 0.7))

    # 7. dirt at the base, travertine wet faces / algae under water
    col = n.mix(col, srgb(C['dirt']), n.maprange(sepp.outputs[2], -0.3, 0.5 + 0.15 * cell, 0.7, 0.0))
    wet = None
    if sp.get('trav'):
        at = n.node('ShaderNodeAttribute', attribute_name='trav')
        st = n.node('ShaderNodeSeparateColor'); n.link(at.outputs['Color'], st.inputs[0])
        wet = st.outputs[0]
        col = n.mix(col, srgb(C['wet']), n.math('MULTIPLY', wet, 0.6))
        col = n.mix(col, srgb(C['algae']), n.math('MULTIPLY', st.outputs[2], 0.55))

    # 8. moss: broad painted areas on up-facing faces (soft painted edge), the moss swatch for its leaf clumps
    mv = sp['moss']
    mup = n.maprange(nz, 0.3 - 0.15 * (mv - 1), 0.75 - 0.15 * (mv - 1))
    brk = n.maprange(n.noise(P, 1 / (cell * 2.2), 2, 0.45), 0.32, 0.56)
    moss = n.maprange(n.math('MULTIPLY', mup, brk), 0.15, 0.7)
    if sp.get('trav'):
        moss = n.math('MAXIMUM', moss, n.math('MULTIPLY', st.outputs[1], n.maprange(brk, 0, 1, 0.3, 1.0)))
        moss = n.math('MULTIPLY', moss, n.maprange(st.outputs[2], 0.0, 0.3, 1.0, 0.0), clamp=True)
    # painted moss: a flat olive gradient (dark at the patch edge, gold-green on the sunlit top) carries the colour;
    # the swatch only adds its leaf clumps at 35 % (AD s3 noise budget), tiled large
    ms = box_image(n, os.path.join(PAINT, 'swatch_moss_tile.png'), P, max(2.4, cell * 1.6))
    flat = n.mix(srgb(C['moss_dark']), srgb(C['moss_mid']), n.maprange(moss, 0.0, 0.7))
    flat = n.mix(flat, srgb(C['moss_light']), n.maprange(nz, 0.75, 1.0, 0.0, 0.6))
    mcol = n.mix(flat, ms.outputs['Color'], 0.35)
    col = n.mix(col, mcol, moss)
    n.link(col, bs.inputs['Base Color'])

    rough = n.maprange(moss, 0, 1, 0.85, 0.9)          # flat constants (AD s3): stone 0.85, wet rock 0.35
    if wet is not None:
        rough = n.math('SUBTRACT', rough, n.math('MULTIPLY', wet, 0.5))
    n.link(rough, bs.inputs['Roughness'])
    bs.inputs['Metallic'].default_value = 0.0

    # broad normal: shallow crack grooves + faint strokes + moss clumps
    h = n.math('MULTIPLY', lum.outputs[0], 0.12)
    msl = n.node('ShaderNodeRGBToBW'); n.link(ms.outputs['Color'], msl.inputs[0])
    h = n.math('ADD', h, n.math('MULTIPLY', n.math('MULTIPLY', msl.outputs[0], moss), 0.6))
    bump = n.node('ShaderNodeBump', in_0=0.6, in_1=0.035 * cell)
    n.link(h, bump.inputs['Height'])
    n.link(bump.outputs[0], bs.inputs['Normal'])
    return m


def bake_all(high, low, kind, imgs, ext, ray, samples, **kw):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    E.rays_off(low)
    set_targets(low, imgs)
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = ext
    sc.render.bake.max_ray_distance = ray
    sc.render.bake.margin = 8
    sc.render.bake.margin_type = 'EXTEND'
    E.set_active(low, high)
    a = dict(type=kind, use_clear=True, margin=8)
    a.update(kw)
    bpy.ops.object.bake(**a)


def preview_mat(mat, base, nrm, arm):
    nt = mat.node_tree
    if 'BAKE_TARGET' in nt.nodes:
        nt.nodes.remove(nt.nodes['BAKE_TARGET'])
    bsdf = nt.nodes['Principled BSDF']
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = base
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = nrm
    ta = nt.nodes.new('ShaderNodeTexImage'); ta.image = arm
    nm = nt.nodes.new('ShaderNodeNormalMap')
    sp = nt.nodes.new('ShaderNodeSeparateColor')
    aom = nt.nodes.new('ShaderNodeMix'); aom.data_type = 'RGBA'; aom.blend_type = 'MULTIPLY'
    aom.inputs[0].default_value = 1.0
    nt.links.new(tb.outputs[0], aom.inputs[6]); nt.links.new(ta.outputs[0], sp.inputs[0])
    cg = nt.nodes.new('ShaderNodeCombineColor')
    for i in range(3):
        nt.links.new(sp.outputs[0], cg.inputs[i])
    nt.links.new(cg.outputs[0], aom.inputs[7])
    nt.links.new(aom.outputs[2], bsdf.inputs['Base Color'])
    nt.links.new(tn.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bsdf.inputs['Normal'])
    nt.links.new(sp.outputs[1], bsdf.inputs['Roughness'])


def copy_extras(name):
    """Empties (anchors, WaterfallMouth, Stand) from the realistic game file, renamed <name>_P_*."""
    path = E.work('rootstone', name + '_game.blend')
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = list(src.objects)
    out = []
    for o in dst.objects:
        if o is None:
            continue
        if o.type == 'EMPTY':
            o.name = name + '_P' + o.name[len(name):] if o.name.startswith(name) else o.name + '_P'
            o.parent = None
            E.link(o)
            out.append(o)
        else:
            bpy.data.objects.remove(o)
    return out


def fit_mouth(lod0, extras):
    """The painterly arch has its own crown: move the WaterfallMouth under its underside (ray up from the ground)."""
    from mathutils.bvhtree import BVHTree
    import bmesh
    mouth = [e for e in extras if e.name.endswith('_WaterfallMouth')]
    if not mouth:
        return
    bm = bmesh.new()
    deps = bpy.context.evaluated_depsgraph_get()
    for o in lod0:
        bm.from_object(o, deps)
    bvh = BVHTree.FromBMesh(bm)
    m = mouth[0]
    for dx in (0, -1.5, 1.5, -3, 3):
        hit = bvh.ray_cast(Vector((m.location.x + dx, m.location.y, 1.0)), Vector((0, 0, 1)))
        if hit[0] is not None and 20 < hit[0].z < 48:
            m.location = (hit[0].x, hit[0].y, hit[0].z - 0.4)
            break
    else:
        raise AssertionError('no crown underside above the mouth')
    bm.free()
    print('WaterfallMouth', tuple(round(v, 2) for v in m.location))


def build(name, simplify_only=False):
    sp = PIECES[name]
    pn = name + '_P'
    src = sp.get('src', name)
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', src + '_high.blend'))
    for o in list(bpy.context.scene.objects):
        if o.name != src + '_high':
            bpy.data.objects.remove(o)
    sc = E.gpu_cycles(8)
    high = bpy.data.objects[src + '_high']
    simplify(high, sp)
    if simplify_only:
        bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', pn + '_simple.blend'), compress=True)
        return
    height = max(1.0, max((high.matrix_world @ Vector(c)).z for c in high.bound_box))
    high.data.materials.clear()
    high.data.materials.append(painted_material(sp, height))

    low = E.copy_obj(high, pn + '_LOD0')
    E.decimate_to(low, sp['lod'][0])
    E.triangulate(low)
    for an in [x.name for x in low.data.color_attributes]:      # bake-only attributes; the game mesh exports none
        try:
            low.data.color_attributes.remove(low.data.color_attributes[an])
        except RuntimeError:
            pass
    low.data.materials.clear()
    halves = ('A', 'B') if 'halves' in sp else ('',)
    mats = []
    for h in halves:
        mt = bpy.data.materials.new(f'MI_{pn}' + (f'_{h}' if h else ''))
        mt.use_nodes = True
        low.data.materials.append(mt)
        mats.append(mt)
    me = low.data
    op = openness(low, dirs=16)
    away = Vector(sp.get('away', (0, 0, -1)))
    groups, wts = [[] for _ in halves], [[] for _ in halves]
    for p in me.polygons:
        k = (0 if p.center.x < sp['halves'] else 1) if 'halves' in sp else 0
        p.material_index = k
        w = 0.6 if p.normal.dot(away) > 0.35 else (0.75 if p.normal.z > 0.6 else 1.0)
        groups[k].append(p.index)
        wts[k].append(round(w * (0.4 + 0.6 * min(1.0, op[p.index] / 0.7)), 1))
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name='UVMap')
    res = sp['res']
    dens = [E.xatlas_faces(low, groups[k], res, padding_px=6 if res >= 2048 else 4, weights=wts[k])
            for k in range(len(halves))]
    print(pn, 'LOD0 tris', E.tris(low), 'px/m', [round(d, 1) for d in dens])

    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.45 if sp.get('trav') else 0.0))
    ground = bpy.context.active_object
    ground.name = 'bake_ground'
    ext, ray = sp['ext'], sp['ray']

    def imgs(tag, non_color=False):
        return [E.new_image(f'{pn}' + (f'_{h}' if h else '') + f'_{tag}', res, non_color=non_color) for h in halves]

    base = imgs('BaseColor')
    sc.world = bpy.data.worlds.new('w')
    bake_all(high, low, 'DIFFUSE', base, ext, ray, 32, pass_filter={'COLOR'})
    nrm = imgs('Normal', True)
    bake_all(high, low, 'NORMAL', nrm, ext, ray, 8, normal_space='TANGENT', normal_r='POS_X', normal_g='POS_Y',
             normal_b='POS_Z')
    ao = imgs('ao', True)
    keep = list(high.data.materials)
    aom = bpy.data.materials.new('ao_src'); aom.use_nodes = True
    aon = aom.node_tree.nodes.new('ShaderNodeAmbientOcclusion')
    aon.samples, aon.only_local = 32, False
    aon.inputs['Distance'].default_value = sp['ao_dist']
    aom.node_tree.links.new(aon.outputs['AO'], aom.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    high.data.materials.clear(); high.data.materials.append(aom)
    bake_all(high, low, 'DIFFUSE', ao, ext, ray, 96, pass_filter={'COLOR'})
    high.data.materials.clear()
    for k in keep:
        high.data.materials.append(k)
    rgh = imgs('rough', True)
    bake_all(high, low, 'ROUGHNESS', rgh, ext, ray, 4)
    bpy.data.objects.remove(ground)

    tdir = os.path.join(OUT, 'Textures')
    arms = []
    for k, h in enumerate(halves):
        arm = E.new_image(f'{pn}' + (f'_{h}' if h else '') + '_ARM', res, non_color=True)
        o = np.zeros((res, res, 4), np.float32)
        o[..., 0] = np.clip(E.pixels(ao[k])[..., 0], 0, 1) ** 1.2       # albedo already has painted AO: keep it soft
        o[..., 1] = E.pixels(rgh[k])[..., 0]
        o[..., 3] = 1
        E.set_pixels(arm, o)
        arms.append(arm)
        stem = pn + (f'_{h}' if h else '')
        E.save_png(base[k], os.path.join(tdir, stem + '_BaseColor.png'))
        E.save_png(nrm[k], os.path.join(tdir, stem + '_Normal.png'))
        E.save_png(arm, os.path.join(tdir, stem + '_ARM.png'))
        preview_mat(mats[k], base[k], nrm[k], arm)

    lods = [low]
    for i, t in ((1, sp['lod'][1]), (2, sp['lod'][2])):
        o = E.copy_obj(low, f'{pn}_LOD{i}')
        E.decimate_to(o, t)
        lods.append(o)
    bpy.data.objects.remove(high)
    objs = []
    for i, o in enumerate(lods):
        me = o.data
        me.normals_split_custom_set_from_vertices([v.normal[:] for v in me.vertices])
        if len(halves) == 1:
            E.box_uv2(o, DETAIL_TILE_M)
            objs.append(o)
            continue
        E.set_active(o)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.separate(type='MATERIAL')
        bpy.ops.object.mode_set(mode='OBJECT')
        parts = [x for x in bpy.context.scene.objects if x.type == 'MESH' and (x == o or x.name.startswith(o.name + '.'))]
        for x in parts:
            h = 'A' if x.data.materials[x.data.polygons[0].material_index].name.endswith('_A') else 'B'
            x.name = x.data.name = f'{pn}_{h}_LOD{i}'
            E.box_uv2(x, DETAIL_TILE_M)
            objs.append(x)
    objs.sort(key=lambda x: x.name)
    for x in objs:
        print(x.name, 'tris', E.tris(x))
    extras = copy_extras(name)
    fit_mouth([x for x in objs if x.name.endswith('_LOD0')], extras)
    E.export_fbx(objs + extras, os.path.join(OUT, pn + '.fbx'))
    for im in base + nrm + arms:
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', pn + '_game.blend'), compress=True)
    lod_tris = [sum(E.tris(x) for x in objs if x.name.endswith(f'_LOD{i}')) for i in range(3)]
    pts = [x.matrix_world @ Vector(c) for x in objs if x.name.endswith('_LOD0') for c in x.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 1) for i in range(3)]
    print('SUMMARY', pn, size, lod_tris, res, 'px/m', [round(d, 1) for d in dens], 'extras', len(extras))


if __name__ == '__main__':
    a = E.args()
    names = [x for x in a if not x.startswith('--')]
    for nm in (list(PIECES) if not names or names[0] == 'all' else names):
        build(nm, simplify_only='--simplify-only' in a)
