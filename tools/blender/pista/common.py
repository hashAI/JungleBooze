"""Shared helpers for the Pista cleanup pipeline (headless Blender / bpy 4.2).

Every step script reads the previous step's .blend from art_source/pista/work/ and writes its own,
so a single step can be rerun after a change. Run order and commands: art_source/pista/README.md.
"""
import os
import sys

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
ART = os.path.join(REPO, 'art_source', 'pista')
WORK = os.path.join(ART, 'work')          # intermediate .blend/.png files, not committed
CLEAN = os.path.join(ART, 'clean')        # final GLB/FBX + mobile textures, committed (LFS)
PREVIEWS = os.path.join(ART, 'previews')
MESHY_GLB = os.path.join(ART, 'meshy', 'pista.glb')
MESHY_HIGH = os.path.join(ART, 'meshy', 'pista_pre_remeshed.glb')

HEIGHT_M = 1.65          # ART_DIRECTION 7.4
TRI_BUDGET = 20000       # hero, realistic look: ADR 0004 has no tri number, ARCHITECTURE 10.2 (15k) is the stylized plan; [ASSUMED] 20k

BODY = 'SK_Pista_Body'
PONYTAIL = 'SK_Pista_Ponytail'


def args():
    """Arguments after '--' (blender -b -P) or after the script name (python script.py)."""
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]


def work(name):
    os.makedirs(WORK, exist_ok=True)
    return os.path.join(WORK, name)


def open_blend(path):
    bpy.ops.wm.open_mainfile(filepath=path)


def save_blend(path):
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print('saved', path)


def tri_count(obj):
    me = obj.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def set_active(obj):
    bpy.context.view_layer.update()
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
