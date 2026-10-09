"""Shared bake path for single-material environment pieces (travertine tiers, lookout ledge v2).

high (with its bake-source material) -> LOD0 (decimate, weighted xatlas UVs, detail box UV1) -> Cycles bake
(BaseColor, tangent Normal OpenGL, AO + roughness -> ARM) -> LOD1/2 -> FBX + game .blend.
Output names follow the kit: Rootstone/<name>.fbx (<name>_LOD0/1/2 + extra empties),
Rootstone/Textures/<name>_BaseColor|_Normal|_ARM.png.
"""
import os

import bpy
import numpy as np
from mathutils import Vector

import envlib as E
from rootstone_bake import DETAIL_TILE_M, OUT


class Nodes:
    """Tiny node-graph helper: n.node(kind, in_0=.., attr=..), n.mix(a, b, fac), n.maprange(src, a, b, c, d)."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.N, self.L = self.nt.nodes, self.nt.links

    def node(self, kind, **kw):
        n = self.N.new(kind)
        for k, v in kw.items():
            if k.startswith('in_'):
                n.inputs[int(k[3:])].default_value = v
            else:
                setattr(n, k, v)
        return n

    def link(self, a, b):
        self.L.new(a, b)

    def _in(self, sock, v):
        if isinstance(v, (int, float, tuple)):
            sock.default_value = v
        else:
            self.L.new(v, sock)

    def mix(self, a, b, fac, blend='MIX'):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        self._in(n.inputs[0], fac)
        self._in(n.inputs[6], a)
        self._in(n.inputs[7], b)
        return n.outputs[2]

    def math(self, op, a, b=0.0, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self._in(n.inputs[0], a)
        self._in(n.inputs[1], b)
        return n.outputs[0]

    def maprange(self, src, a, b, c=0.0, d=1.0):
        n = self.node('ShaderNodeMapRange', in_1=a, in_2=b, in_3=c, in_4=d)
        self._in(n.inputs[0], src)
        return n.outputs[0]

    def noise(self, coord, scale, detail=6, rough=0.55):
        n = self.node('ShaderNodeTexNoise', in_2=scale, in_3=detail, in_4=rough)
        self.L.new(coord, n.inputs[0])
        return n.outputs['Fac']

    def gray(self, v):
        n = self.node('ShaderNodeCombineColor')
        for i in range(3):
            self._in(n.inputs[i], v)
        return n.outputs[0]


def srgb(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c) + (1.0,)


def bake_ao(high, low, img, ext, ray, samples, dist):
    """AO through a colour bake: the high mesh temporarily wears a material whose base colour is an Ambient Occlusion
    node (scene geometry incl. the bake ground, `dist` m). The Cycles AO pass under selected-to-active left large open
    up-facing areas at 0 on the travertine tiers; colour bakes have been reliable throughout."""
    keep = list(high.data.materials)
    m = bpy.data.materials.new('ao_src')
    m.use_nodes = True
    nt = m.node_tree
    aon = nt.nodes.new('ShaderNodeAmbientOcclusion')
    aon.samples = 32
    aon.only_local = False
    aon.inputs['Distance'].default_value = dist
    nt.links.new(aon.outputs['AO'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    high.data.materials.clear()
    high.data.materials.append(m)
    E.bake(high, low, 'DIFFUSE', img, ext, ray, samples=samples, pass_filter={'COLOR'})
    high.data.materials.clear()
    for k in keep:
        high.data.materials.append(k)


def bake_piece(name, high, lod_tris, res=1024, weight_fn=None, ext=0.25, ray=0.6, extras=(), ao_samples=96,
               ao_power=1.2, ao_dist=1.5):
    """high: object with its bake-source material. weight_fn(polygon) -> texel weight (None = uniform).
    extras: objects (empties) exported with the LODs. Returns the LOD0 density in px/m."""
    sc = E.gpu_cycles(8)
    for p in high.data.polygons:
        p.use_smooth = True
    low = E.copy_obj(high, name + '_LOD0')
    E.decimate_to(low, lod_tris[0])
    E.triangulate(low)
    for an in [a.name for a in low.data.color_attributes]:
        try:                                      # bake-only attributes; the game mesh exports none (ADR 0007)
            low.data.color_attributes.remove(low.data.color_attributes[an])
        except RuntimeError:
            low.data.attributes.remove(low.data.attributes[an]) if an in low.data.attributes else None
    low.data.materials.clear()
    mat = bpy.data.materials.new('MI_' + name)
    mat.use_nodes = True
    low.data.materials.append(mat)
    me = low.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name='UVMap')
    faces = [p.index for p in me.polygons]
    w = [weight_fn(p) for p in me.polygons] if weight_fn else None
    dens = E.xatlas_faces(low, faces, res, padding_px=4, weights=w)
    E.box_uv2(low, DETAIL_TILE_M)

    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.45))
    ground = bpy.context.active_object
    ground.name = 'bake_ground'
    base = E.new_image(name + '_BaseColor', res)
    E.bake(high, low, 'DIFFUSE', base, ext, ray, samples=16, pass_filter={'COLOR'})
    nrm = E.new_image(name + '_Normal', res, non_color=True)
    E.bake(high, low, 'NORMAL', nrm, ext, ray, samples=8)
    ao = E.new_image(name + '_ao', res, non_color=True)
    sc.world = bpy.data.worlds.new('w')
    bake_ao(high, low, ao, ext, ray, ao_samples, ao_dist)
    rgh = E.new_image(name + '_rough', res, non_color=True)
    E.bake(high, low, 'ROUGHNESS', rgh, ext, ray, samples=4)
    bpy.data.objects.remove(ground)
    arm = E.new_image(name + '_ARM', res, non_color=True)
    o = np.zeros((res, res, 4), np.float32)
    o[..., 0] = np.clip(E.pixels(ao)[..., 0], 0, 1) ** ao_power
    o[..., 1] = E.pixels(rgh)[..., 0]
    o[..., 3] = 1
    E.set_pixels(arm, o)
    tdir = os.path.join(OUT, 'Textures')
    E.save_png(base, os.path.join(tdir, name + '_BaseColor.png'))
    E.save_png(nrm, os.path.join(tdir, name + '_Normal.png'))
    E.save_png(arm, os.path.join(tdir, name + '_ARM.png'))

    nt = mat.node_tree
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

    l1 = E.copy_obj(low, name + '_LOD1'); E.decimate_to(l1, lod_tris[1])
    l2 = E.copy_obj(low, name + '_LOD2'); E.decimate_to(l2, lod_tris[2])
    bpy.data.objects.remove(high)
    objs = [low, l1, l2] + list(extras)
    E.export_fbx(objs, os.path.join(OUT, name + '.fbx'))
    for im in (base, nrm, arm):
        im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=E.work('rootstone', name + '_game.blend'), compress=True)
    print('SUMMARY', name, tuple(round(x, 1) for x in low.dimensions), [E.tris(x) for x in (low, l1, l2)], res,
          f'{dens:.1f} px/m')
    return dens


def empty(name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = Vector(loc)
    e.empty_display_size = 0.3
    E.link(e)
    return e
