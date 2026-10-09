"""Travertine tiers v2, stage 2: bake + LODs + FBX (bakekit), plus the per-tier water sheet as its own FBX.

Input:  work/rootstone/RS_TravertineTiers_high.blend (travertine_high.py; mesh with the 'trav' attribute, anchors,
        RS_TravertineTiers_Water)
Output: Rootstone/RS_TravertineTiers.fbx (_LOD0/1/2 + anchors), Rootstone/RS_TravertineTiers_Water.fbx,
        Rootstone/Textures/RS_TravertineTiers_{BaseColor,Normal,ARM}.png (1024)
travertine_material() is also the lookout ledge's stone (ledge_v2_high.py), so the foreground rock and the tiers match.
Usage: blender ... -P travertine_bake.py
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import envlib as E  # noqa: E402
import bakekit as K  # noqa: E402
from rootstone_bake import OUT  # noqa: E402
import travertine_high as T  # noqa: E402

LOD_TRIS = (5000, 2200, 800)


def travertine_material(moss_boost=1.0, wet_scale=1.0):
    """Damp grey-ochre limestone (darker than the rootstone, F4_e foreground rock and terraces): strata bands,
    lichen, cavity darkening; 'trav' R = wet (dark, glossy), G = rim crest / flat top (moss), B = under water (algae)."""
    m = bpy.data.materials.new('travertine_src')
    m.use_nodes = True
    n = K.Nodes(m)
    bs = n.N['Principled BSDF']
    tc = n.node('ShaderNodeTexCoord')
    geo = n.node('ShaderNodeNewGeometry')
    obj = tc.outputs['Object']
    mp = n.node('ShaderNodeMapping', vector_type='POINT')
    mp.inputs['Scale'].default_value = (0.45,) * 3
    n.link(obj, mp.inputs[0])
    img = n.node('ShaderNodeTexImage', projection='BOX', projection_blend=0.35)
    img.image = bpy.data.images.load(os.path.join(E.CC0_TEX, 'rock_face_03', 'rock_face_03_diff_2k.jpg'))
    n.link(mp.outputs[0], img.inputs[0])
    lum = n.node('ShaderNodeRGBToBW')
    n.link(img.outputs['Color'], lum.inputs[0])
    grain = n.maprange(lum.outputs[0], 0.05, 0.6, 0.7, 1.12)
    at = n.node('ShaderNodeAttribute', attribute_name='trav')
    sep = n.node('ShaderNodeSeparateColor')
    n.link(at.outputs['Color'], sep.inputs[0])
    wet = n.math('MULTIPLY', sep.outputs[0], wet_scale, clamp=True)
    rim, under = sep.outputs[1], sep.outputs[2]

    base = n.mix(K.srgb('#958C74'), K.srgb('#736C5A'), n.noise(obj, 0.25, 4, 0.55))
    # travertine strata: faint horizontal bands, warped
    band = n.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction='Z', wave_profile='SIN', in_1=1.6, in_2=2.0)
    n.link(obj, band.inputs[0])
    strata = n.maprange(band.outputs['Fac'], 0.0, 1.0, 0.92, 1.05)
    v = n.math('MULTIPLY', grain, strata)
    col = n.mix(base, n.gray(v), 1.0, 'MULTIPLY')
    col = n.mix(col, K.srgb('#4F4B3D'), n.maprange(n.noise(obj, 1.8, 8, 0.7), 0.57, 0.68, 0, 0.6))     # lichen
    col = n.mix(col, K.srgb('#B5AE98'), n.maprange(n.noise(obj, 0.5, 5, 0.6), 0.62, 0.72, 0, 0.25))    # pale crust
    ao = n.node('ShaderNodeAmbientOcclusion', samples=16, only_local=True, in_1=0.5)
    cav = n.maprange(ao.outputs['AO'], 0.3, 1.0, 0.25, 1.0)
    col = n.mix(col, n.gray(cav), 1.0, 'MULTIPLY')
    # wet: dark and slightly green near spills
    col = n.mix(col, K.srgb('#38372C'), n.math('MULTIPLY', wet, 0.78))
    # under water: olive-brown algae film
    col = n.mix(col, K.srgb('#3E4630'), n.math('MULTIPLY', under, 0.85))
    # moss: up-facing, patchy; thick on rim crests and flat tops; none under water, less where soaked
    sn = n.node('ShaderNodeSeparateXYZ'); n.link(geo.outputs['Normal'], sn.inputs[0])
    up = n.maprange(sn.outputs[2], 0.3 - 0.15 * (moss_boost - 1), 0.75 - 0.2 * (moss_boost - 1))
    patch = n.maprange(n.noise(obj, 0.7, 8, 0.7), 0.42 - 0.1 * (moss_boost - 1), 0.56 - 0.1 * (moss_boost - 1))
    groove = n.maprange(ao.outputs['AO'], 0.85, 0.45, 0, 0.8)
    mm = n.math('ADD', n.math('ADD', patch, groove, clamp=True), n.math('MULTIPLY', rim, 0.9), clamp=True)
    mm = n.math('MULTIPLY', up, mm, clamp=True)
    mm = n.math('MULTIPLY', mm, n.math('SUBTRACT', 1.0, under), clamp=True)
    mm = n.math('MULTIPLY', mm, n.math('SUBTRACT', 1.0, n.math('MULTIPLY', wet, 0.6)), clamp=True)
    moss = n.maprange(mm, 0.2, 0.6)
    moss_col = n.mix(K.srgb('#2E3D18'), K.srgb('#4F6223'), n.noise(obj, 1.2, 6, 0.65))
    moss_col = n.mix(moss_col, K.srgb('#7C8434'), n.maprange(n.noise(obj, 5.0, 5, 0.7), 0.55, 0.75, 0, 0.55))
    col = n.mix(col, moss_col, moss)
    n.link(col, bs.inputs['Base Color'])
    rough = n.mix((0.82, 0.82, 0.82, 1), (0.95, 0.95, 0.95, 1), moss)
    rough = n.mix(rough, (0.28, 0.28, 0.28, 1), n.math('MULTIPLY', wet, 0.9))
    rough = n.mix(rough, (0.35, 0.35, 0.35, 1), under)
    rr = n.node('ShaderNodeRGBToBW'); n.link(rough, rr.inputs[0])
    n.link(rr.outputs[0], bs.inputs['Roughness'])
    hb = n.math('ADD', n.math('MULTIPLY', n.noise(obj, 9.0, 4, 0.7), moss), lum.outputs[0])
    bump = n.node('ShaderNodeBump', in_0=0.5, in_1=0.05)
    n.link(hb, bump.inputs['Height'])
    n.link(bump.outputs[0], bs.inputs['Normal'])
    return m


def build():
    bpy.ops.wm.open_mainfile(filepath=E.work('rootstone', T.NAME + '_high.blend'))
    high = bpy.data.objects[T.NAME + '_high']
    high.data.materials.clear()
    high.data.materials.append(travertine_material())
    water = bpy.data.objects[T.NAME + '_Water']
    wm = bpy.data.materials.new('MI_' + T.NAME + '_Water')
    water.data.materials.append(wm)
    E.export_fbx([water], os.path.join(OUT, T.NAME + '_Water.fbx'))
    F = T.Field(T.SEED)
    wl = np.array(T.water_levels() + [0.3])

    def weight(p):
        c = p.center
        _, t, _ = T.heights(F, np.array([c.x]), np.array([c.y]))
        if p.normal.z > 0.6 and c.z < wl[int(t[0])] - 0.05:
            return 0.4                     # pool floors under water
        if p.normal.y > 0.35:
            return 0.6                     # faces looking upstream (away from the camera)
        return 1.0

    anchors = [o for o in bpy.context.scene.objects if o.type == 'EMPTY']
    bpy.data.objects.remove(water)
    K.bake_piece(T.NAME, high, LOD_TRIS, res=1024, weight_fn=weight, ext=0.2, ray=0.5, extras=anchors)


if __name__ == '__main__':
    build()
