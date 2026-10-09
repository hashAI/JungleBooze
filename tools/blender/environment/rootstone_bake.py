"""Rootstone kit, stage 2: game mesh + LODs + baked texture set + FBX for Unity.

Input:  work/rootstone/<piece>_high.blend (rootstone_high.py)
Output: UnityProject/Assets/_Game/Art/Environment/Rootstone/<piece>.fbx  (<piece>_LOD0/1/2 meshes -> Unity LODGroup)
        .../Rootstone/Textures/<piece>_BaseColor.png (sRGB), _Normal.png (OpenGL, tangent), _ARM.png (AO, rough, metal)
        work/rootstone/<piece>_game.blend (for previews)
UV0 = unique baked maps (xatlas). UV1 = world box projection, 1 unit = DETAIL_TILE_M metres, for a tiling detail
normal (CC0 rock_face_03) so the stone stays crisp up close.
Look target: F4_e (primary), P1, F1: pale warm grey-ochre stone, darker lichen mottles, moss on tops and in grooves.
Usage: blender ... -P rootstone_bake.py -- <piece|all>
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
from rootstone_high import PIECES  # noqa: E402
from ledges_high import LEDGES  # noqa: E402

DETAIL_TILE_M = 4.0
OUT = os.path.join(E.UNITY_ENV, 'Rootstone')
# piece: (LOD0 tris, LOD1, LOD2, texture size)
BUDGET = {
    'RS_HeroArch': (10000, 4500, 1500, 2048),
    'RS_PillarA': (4000, 1800, 600, 1024),
    'RS_PillarB': (4000, 1800, 600, 1024),
    'RS_ArchSmall': (4000, 1800, 600, 1024),
    'RS_Outcrop': (2500, 1100, 400, 1024),
    'RS_PoolTerrace_A': (2500, 1100, 400, 1024),
    'RS_PoolTerrace_B': (1800, 800, 300, 1024),
    'RS_LedgeLookout': (2500, 1100, 400, 1024),
}


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (1.0,)


def stone_material(scale):
    """Bake-source material on the high poly. scale ~ size of the piece (1 = 10 cm voxel pieces)."""
    m = bpy.data.materials.new('rootstone_src')
    m.use_nodes = True
    nt = m.node_tree
    N, L = nt.nodes, nt.links
    bs = N['Principled BSDF']
    tc = N.new('ShaderNodeTexCoord')
    geo = N.new('ShaderNodeNewGeometry')

    def node(kind, **kw):
        n = N.new(kind)
        for k, v in kw.items():
            if k.startswith('in_'):
                n.inputs[int(k[3:])].default_value = v
            else:
                setattr(n, k, v)
        return n

    def mix(a, b, fac, blend='MIX'):
        n = node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        for src, slot in ((fac, 0), (a, 6), (b, 7)):
            if isinstance(src, (int, float)):
                n.inputs[slot].default_value = src
            elif isinstance(src, tuple):
                n.inputs[slot].default_value = src
            else:
                L.new(src, n.inputs[slot])
        return n.outputs[2]

    def maprange(src, a, b, c=0.0, d=1.0):
        n = node('ShaderNodeMapRange', in_1=a, in_2=b, in_3=c, in_4=d)
        L.new(src, n.inputs[0])
        return n.outputs[0]

    def noise(sc, detail=6, rough=0.55, coord=None):
        n = node('ShaderNodeTexNoise', in_2=sc, in_3=detail, in_4=rough)
        L.new(coord or tc.outputs['Object'], n.inputs[0])
        return n.outputs['Fac']

    # photographic grain from CC0 rock_face_03 (box projected); only its luminance is used
    mp = node('ShaderNodeMapping', vector_type='POINT')
    mp.inputs['Scale'].default_value = (1 / (3.0 * scale),) * 3
    L.new(tc.outputs['Object'], mp.inputs[0])
    img = node('ShaderNodeTexImage', projection='BOX', projection_blend=0.35)
    img.image = bpy.data.images.load(os.path.join(E.CC0_TEX, 'rock_face_03', 'rock_face_03_diff_2k.jpg'))
    L.new(mp.outputs[0], img.inputs[0])
    lum = node('ShaderNodeRGBToBW')
    L.new(img.outputs['Color'], lum.inputs[0])
    grain = maprange(lum.outputs[0], 0.05, 0.6, 0.72, 1.12)

    at = node('ShaderNodeAttribute', attribute_name='strand')
    sep = node('ShaderNodeSeparateColor')
    L.new(at.outputs['Color'], sep.inputs[0])
    strand_var = maprange(sep.outputs[0], 0, 1, 0.86, 1.08)
    # streaks along each strand (G = position along the strand)
    # fibre coordinates: seamless around the strand (cos/sin of the phase), stretched along it (metres)
    cs = node('ShaderNodeMath', operation='MULTIPLY', in_1=1.0); L.new(sep.outputs[2], cs.inputs[0])
    sn = node('ShaderNodeMath', operation='MULTIPLY', in_1=1.0); L.new(at.outputs['Alpha'], sn.inputs[0])
    along = node('ShaderNodeMath', operation='MULTIPLY', in_1=0.06 / scale)
    L.new(sep.outputs[1], along.inputs[0])
    fib = node('ShaderNodeCombineXYZ')
    L.new(cs.outputs[0], fib.inputs[0]); L.new(sn.outputs[0], fib.inputs[1]); L.new(along.outputs[0], fib.inputs[2])
    seed = node('ShaderNodeVectorMath', operation='ADD')
    L.new(fib.outputs[0], seed.inputs[0])
    sv = node('ShaderNodeCombineXYZ'); L.new(sep.outputs[0], sv.inputs[0]); L.new(sep.outputs[0], sv.inputs[2])
    L.new(sv.outputs[0], seed.inputs[1])
    fn = node('ShaderNodeTexNoise', in_2=4.0, in_3=3.0, in_4=0.5)
    L.new(seed.outputs[0], fn.inputs[0])
    fibre = maprange(fn.outputs['Fac'], 0.38, 0.62)            # 0 = groove line, 1 = ridge
    streak = maprange(fibre, 0.0, 1.0, 0.9, 1.03)
    fn2 = node('ShaderNodeTexNoise', in_2=1.6, in_3=4.0, in_4=0.5)
    L.new(seed.outputs[0], fn2.inputs[0])
    band = maprange(fn2.outputs['Fac'], 0.35, 0.65, 0.9, 1.08)    # broad tonal bands along strands

    base = mix(srgb('#C6B79E'), srgb('#AC9C83'), noise(0.08 / scale, 3, 0.5))      # pale warm cream-ochre (F4_e)
    v = node('ShaderNodeMath', operation='MULTIPLY')
    L.new(grain, v.inputs[0]); L.new(strand_var, v.inputs[1])
    v1 = node('ShaderNodeMath', operation='MULTIPLY')
    L.new(v.outputs[0], v1.inputs[0]); L.new(band, v1.inputs[1])
    v2 = node('ShaderNodeMath', operation='MULTIPLY')
    L.new(v1.outputs[0], v2.inputs[0]); L.new(streak, v2.inputs[1])
    colm = node('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
    colm.inputs[0].default_value = 1.0
    L.new(base, colm.inputs[6])
    gray = node('ShaderNodeCombineColor')
    for i in range(3):
        L.new(v2.outputs[0], gray.inputs[i])
    L.new(gray.outputs[0], colm.inputs[7])
    col = colm.outputs[2]
    # lichen mottles: dark grey-brown blotches and pale crust (F4_e)
    lich_d = maprange(noise(2.2 / scale, 10, 0.7), 0.57, 0.66, 0, 0.65)
    col = mix(col, srgb('#6B6455'), lich_d)
    lich_p = maprange(noise(0.35 / scale, 6, 0.6, None), 0.6, 0.72, 0, 0.18)
    col = mix(col, srgb('#D2CCBD'), lich_p)

    spk_d = maprange(noise(9.0 / scale, 2, 0.5), 0.66, 0.70, 0, 0.45)
    col = mix(col, srgb('#5E584A'), spk_d)
    spk_p = maprange(noise(7.0 / scale, 2, 0.5, None), 0.30, 0.26, 0, 0.35)
    col = mix(col, srgb('#E2DCCB'), spk_p)
    # cavity darkening (grooves between strands)
    ao = node('ShaderNodeAmbientOcclusion', samples=16, only_local=True, in_1=0.9 * scale)
    cav = maprange(ao.outputs['AO'], 0.25, 1.0, 0.28, 1.0)
    cm = node('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
    cm.inputs[0].default_value = 1.0
    L.new(col, cm.inputs[6])
    cg = node('ShaderNodeCombineColor')
    for i in range(3):
        L.new(cav, cg.inputs[i])
    L.new(cg.outputs[0], cm.inputs[7])
    col = cm.outputs[2]

    # moss: up-facing, broken by noise, thicker in grooves on top
    sepn = node('ShaderNodeSeparateXYZ')
    L.new(geo.outputs['Normal'], sepn.inputs[0])
    up = maprange(sepn.outputs[2], 0.15, 0.7)
    patch = maprange(noise(0.3 / scale, 8, 0.7), 0.36, 0.55)
    groove = maprange(ao.outputs['AO'], 0.8, 0.35, 0, 1.0)
    m1 = node('ShaderNodeMath', operation='ADD', use_clamp=True)
    L.new(patch, m1.inputs[0]); L.new(groove, m1.inputs[1])
    moss = node('ShaderNodeMath', operation='MULTIPLY', use_clamp=True)
    L.new(up, moss.inputs[0]); L.new(m1.outputs[0], moss.inputs[1])
    moss_edge = maprange(moss.outputs[0], 0.22, 0.62)
    moss_a = mix(srgb('#28351A'), srgb('#3E5121'), noise(0.5 / scale, 6, 0.65))
    moss_col = mix(moss_a, srgb('#66703A'), maprange(noise(2.4 / scale, 5, 0.7), 0.55, 0.75, 0, 0.5))
    # moss over stone: thin moss lets the stone through (no flat stickers)
    moss_col = mix(moss_col, srgb('#6E6A50'), maprange(moss_edge, 0.0, 0.5, 0.55, 0.0))
    col = mix(col, moss_col, moss_edge)
    # dirt and damp at the base (blends into the forest floor)
    sepp = node('ShaderNodeSeparateXYZ')
    L.new(tc.outputs['Object'], sepp.inputs[0])
    dirt = maprange(sepp.outputs[2], 0.0, 1.6 * scale + 0.4, 0.85, 0.0)
    dn = maprange(noise(0.8 / scale, 6, 0.6), 0.3, 0.7, 0.6, 1.0)
    dirtm = node('ShaderNodeMath', operation='MULTIPLY', use_clamp=True)
    L.new(dirt, dirtm.inputs[0]); L.new(dn, dirtm.inputs[1])
    col = mix(col, srgb('#4E3D2C'), dirtm.outputs[0])
    L.new(col, bs.inputs['Base Color'])

    rough = mix((0.72, 0.72, 0.72, 1), (0.95, 0.95, 0.95, 1), moss_edge)
    rough = mix(rough, (0.9, 0.9, 0.9, 1), maprange(noise(0.5 / scale, 6, 0.6), 0.35, 0.65, 0.0, 0.6))
    rough = mix(rough, (0.6, 0.6, 0.6, 1), maprange(ao.outputs['AO'], 0.7, 0.3, 0.0, 0.5))
    rr = node('ShaderNodeRGBToBW')
    L.new(rough, rr.inputs[0])
    L.new(rr.outputs[0], bs.inputs['Roughness'])
    bs.inputs['Metallic'].default_value = 0.0

    # micro bump (grain + moss fluff) goes into the normal bake
    hb0 = node('ShaderNodeMath', operation='MULTIPLY_ADD', in_1=0.5)
    L.new(lum.outputs[0], hb0.inputs[0]); L.new(fibre, hb0.inputs[2])
    hb = node('ShaderNodeMath', operation='ADD')
    L.new(hb0.outputs[0], hb.inputs[0])
    fl = node('ShaderNodeMath', operation='MULTIPLY')
    L.new(noise(6.0 / scale, 4, 0.7), fl.inputs[0]); L.new(moss_edge, fl.inputs[1])
    L.new(fl.outputs[0], hb.inputs[1])
    bump = node('ShaderNodeBump', in_0=0.5, in_1=0.06 * scale)
    L.new(hb.outputs[0], bump.inputs['Height'])
    L.new(bump.outputs[0], bs.inputs['Normal'])
    return m


def build(name):
    lod0, lod1, lod2, res = BUDGET[name]
    spec = PIECES.get(name) or LEDGES[name]
    scale = spec.get('feature', spec['voxel'] / 0.1)     # texture feature scale (ledges: metres)
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', name + '_high.blend'))
    sc = E.gpu_cycles(8)
    high = bpy.data.objects[name + '_high']
    assert 'strand' in high.data.color_attributes, 'strand attribute lost in remesh'
    for p in high.data.polygons:
        p.use_smooth = True
    high.data.materials.clear()
    high.data.materials.append(stone_material(scale))

    low = E.copy_obj(high, name + '_LOD0')
    E.decimate_to(low, lod0)
    E.xatlas_uv(low, padding_px=6 if res >= 2048 else 4, res=res)
    E.box_uv2(low, DETAIL_TILE_M)
    for a in list(low.data.color_attributes):
        low.data.color_attributes.remove(a)
    low.data.materials.clear()
    low.data.materials.append(bpy.data.materials.new('MI_' + name))
    print(name, 'LOD0 tris', E.tris(low))

    # ground plane for AO contact darkening
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = 'bake_ground'

    ext, ray = 10 * spec['voxel'] * (1.5 if res >= 2048 else 1.0), 40 * spec['voxel']
    imgs = {}
    imgs['base'] = E.new_image(name + '_BaseColor', res)
    E.bake(high, low, 'DIFFUSE', imgs['base'], ext, ray, samples=16, pass_filter={'COLOR'})
    imgs['nrm'] = E.new_image(name + '_Normal', res, non_color=True)
    E.bake(high, low, 'NORMAL', imgs['nrm'], ext, ray, samples=8)
    ao = E.new_image(name + '_ao', res, non_color=True)
    sc.world = bpy.data.worlds.new('w')
    E.bake(high, low, 'AO', ao, ext, ray, samples=96)
    rgh = E.new_image(name + '_rough', res, non_color=True)
    E.bake(high, low, 'ROUGHNESS', rgh, ext, ray, samples=4)
    bpy.data.objects.remove(ground)

    a = E.pixels(ao)[..., 0]
    r = E.pixels(rgh)[..., 0]
    arm = E.new_image(name + '_ARM', res, non_color=True)
    out = np.zeros((res, res, 4), np.float32)
    out[..., 0] = np.clip(a, 0, 1) ** 1.2
    out[..., 1] = r
    out[..., 3] = 1
    E.set_pixels(arm, out)
    tdir = os.path.join(OUT, 'Textures')
    E.save_png(imgs['base'], os.path.join(tdir, name + '_BaseColor.png'))
    E.save_png(imgs['nrm'], os.path.join(tdir, name + '_Normal.png'))
    E.save_png(arm, os.path.join(tdir, name + '_ARM.png'))

    # preview material on the game mesh
    mat = low.data.materials[0]
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        if n.name == 'BAKE_TARGET':
            nt.nodes.remove(n)
    bsdf = nt.nodes['Principled BSDF']
    tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = imgs['base']
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = imgs['nrm']
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

    l1 = E.copy_obj(low, name + '_LOD1'); E.decimate_to(l1, lod1)
    l2 = E.copy_obj(low, name + '_LOD2'); E.decimate_to(l2, lod2)
    objs = [low, l1, l2]
    if 'mouth' in spec:
        c, rad = spec['mouth']['c'], spec['mouth']['r']
        em = bpy.data.objects.new(name + '_WaterfallMouth', None)
        em.location = (c[0], c[1], c[2] - rad[2] * 0.85)
        E.link(em)
        objs.append(em)
    for o in objs:
        print(o.name, 'tris', E.tris(o) if o.type == 'MESH' else '-')
    bpy.data.objects.remove(high)
    E.export_fbx(objs, os.path.join(OUT, name + '.fbx'))
    for im in list(imgs.values()) + [arm]:
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', name + '_game.blend'), compress=True)
    sizes = {o.name: tuple(round(x, 1) for x in o.dimensions) for o in objs if o.type == 'MESH'}
    print('SUMMARY', name, sizes[name + '_LOD0'], [E.tris(o) for o in objs if o.type == 'MESH'], res)


if __name__ == '__main__':
    a = E.args()
    for nm in (list(BUDGET) if not a or a[0] == 'all' else a):
        build(nm)
