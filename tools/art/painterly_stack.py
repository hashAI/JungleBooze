#!/usr/bin/env python3
"""Preview sheets for the painterly (F4_f) kit, next to the target keyframe.
  BD_F4f_stack_test.jpg: F4_f | painterly backdrop layers stacked back to front (sky, far range, falls, mist band,
                         mid pillars, jungle wall) roughly where the basin camera sees them
  FP_P_atlases.jpg:      the five painterly atlases over a flat sky-blue background
Usage: python3 painterly_stack.py   (Blender's Python 3.11: numpy, Pillow)
"""
import os

from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
ENV = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Environment')
PREV = os.path.join(REPO, 'art_source', 'environment', 'previews')
KF = os.path.join(REPO, 'design', 'aurelia', 'keyframes', 'F4_f_painterly_openai.jpg')


def layer(canvas, name, width, cx, bottom, opacity=1.0):
    im = Image.open(os.path.join(ENV, 'Backdrops', name + '.png')).convert('RGBA')
    h = int(im.height * width / im.width)
    im = im.resize((width, h), Image.LANCZOS)
    if opacity < 1:
        a = im.getchannel('A').point(lambda v: int(v * opacity))
        im.putalpha(a)
    canvas.alpha_composite(im, (int(cx - width / 2), int(bottom - h)))


def stack():
    W, H = 1536, 1024
    c = Image.open(os.path.join(ENV, 'Backdrops', 'BD_F4f_Sky.png')).convert('RGBA').resize((W, H))
    layer(c, 'BD_F4f_FarRange', 1600, W / 2, 700)
    layer(c, 'BD_F4f_FarFalls', 300, 790, 820)
    layer(c, 'BD_MistBand', 1800, W / 2, 820, 0.7)
    layer(c, 'BD_F4f_MidPillars', 1500, W / 2 - 80, 1060)
    layer(c, 'BD_F4f_JungleWall', 1600, W / 2, 1330)
    kf = Image.open(KF).convert('RGB').resize((W, H))
    s = Image.new('RGB', (W * 2 + 12, H), (20, 20, 20))
    s.paste(kf, (0, 0))
    s.paste(c.convert('RGB'), (W + 12, 0))
    s = s.resize((s.width // 2, s.height // 2), Image.LANCZOS)
    out = os.path.join(PREV, 'BD_F4f_stack_test.jpg')
    s.save(out, quality=88)
    print('wrote', out)


def atlases():
    names = ['FP_BigLeaf_P', 'FP_Bellflower_P', 'FP_Fronds_P', 'FP_Canopy_P', 'FP_ArchVines_P']
    T = 512
    s = Image.new('RGB', (T * len(names), T), (120, 150, 185))
    for i, n in enumerate(names):
        im = Image.open(os.path.join(ENV, 'Plants', 'Textures', n + '_BaseColor.png')).convert('RGBA').resize((T, T))
        bg = Image.new('RGBA', (T, T), (120, 150, 185, 255))
        bg.alpha_composite(im)
        s.paste(bg.convert('RGB'), (i * T, 0))
    out = os.path.join(PREV, 'FP_P_atlases.jpg')
    s.save(out, quality=88)
    print('wrote', out)


if __name__ == '__main__':
    stack()
    atlases()
