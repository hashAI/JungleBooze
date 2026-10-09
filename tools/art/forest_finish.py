#!/usr/bin/env python3
"""Expedition forest kit: OpenAI images -> kit textures and bake inputs.

Inputs (art_source/environment/openai/forest/): f_swatch_trail_c2.png, f_swatch_bark_c1.png,
  f_bd_forest_far_key.png, f_bd_forest_mid_key.png, f_bd_river_valley_key.png (magenta key)
Outputs:
  Forest/Textures/TR_TrailGround_P_BaseColor.png, _Normal.png   tiling trail ground (1 tile = 4 m suggested)
  Backdrops/BD_Forest_Far.png, BD_Forest_Mid.png, BD_River_Valley.png   straight alpha, colour bled
  art_source/environment/work/forest/swatch_trail_tile.png, swatch_bark_tile.png   (bake inputs)
Usage: python3 forest_finish.py [ground] [backdrops]   (Blender's Python 3.11: numpy, scipy, Pillow)
"""
import os
import subprocess
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(__file__))
from painterly_finish import despill_soft, tileable  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(REPO, 'art_source', 'environment', 'openai', 'forest')
ENV = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Environment')
WORK = os.path.join(REPO, 'art_source', 'environment', 'work', 'forest')
BACKDROPS = {'BD_Forest_Far': 'f_bd_forest_far', 'BD_Forest_Mid': 'f_bd_forest_mid',
             'BD_River_Valley': 'f_bd_river_valley'}


def tile_normal(src, dst, k=1.6, blur=1.5):
    """Tangent normal (OpenGL) from the luminance of a tiling albedo, wrap-around filters so it still tiles."""
    im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32) / 255
    lum = ndimage.gaussian_filter(im @ np.array([0.2126, 0.7152, 0.0722]), blur, mode='wrap')
    gx = ndimage.sobel(lum, 1, mode='wrap') * k
    gy = ndimage.sobel(lum, 0, mode='wrap') * k
    n = np.dstack([-gx, gy, np.ones_like(lum)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray((n * 127.5 + 127.5 + 0.5).clip(0, 255).astype(np.uint8), 'RGB').save(dst, optimize=True)


def grade_trail(path, target=(0xA4, 0x8A, 0x55), sat=0.6, contrast=1.3):
    """Albedo, not the lit hex: the generated earth is ~73 % saturated mustard (renders orange under the warm sun).
    Desaturate, widen the value range (calm broad patches stay), then shift the mean to `target` (lit by the
    sun this lands near AD s8 path earth #B99643, shade #5E4B18)."""
    im = np.asarray(Image.open(path).convert('RGB')).astype(np.float32) / 255
    lum = (im @ np.array([0.2126, 0.7152, 0.0722]))[..., None]
    im = lum + (im - lum) * sat
    m = im.reshape(-1, 3).mean(0)
    im = m + (im - m) * contrast
    im = im - im.reshape(-1, 3).mean(0) + np.array(target) / 255
    Image.fromarray((im.clip(0, 1) * 255 + 0.5).astype(np.uint8)).save(path)


def main():
    a = sys.argv[1:] or ['ground', 'backdrops']
    os.makedirs(WORK, exist_ok=True)
    if 'ground' in a:
        tdir = os.path.join(ENV, 'Forest', 'Textures')
        os.makedirs(tdir, exist_ok=True)
        t = os.path.join(WORK, 'swatch_trail_tile.png')
        tileable(os.path.join(SRC, 'f_swatch_trail_c2.png'), t, blend=0.3)
        grade_trail(t)
        dst = os.path.join(tdir, 'TR_TrailGround_P_BaseColor.png')
        Image.open(t).save(dst, optimize=True)
        tile_normal(dst, os.path.join(tdir, 'TR_TrailGround_P_Normal.png'))
        tileable(os.path.join(SRC, 'f_swatch_bark_c1.png'), os.path.join(WORK, 'swatch_bark_tile.png'), blend=0.3)
        print('ground + bark swatches')
    if 'backdrops' in a:
        bdir = os.path.join(ENV, 'Backdrops')
        for name, src in BACKDROPS.items():
            cut = os.path.join(WORK, src + '_cut.png')
            subprocess.check_call([sys.executable, os.path.join(REPO, 'tools', 'art', 'chroma_unmix.py'),
                                   os.path.join(SRC, src + '_key.png'), cut])
            despill_soft(cut, os.path.join(bdir, name + '.png'), 0.0)
            print(name, Image.open(os.path.join(bdir, name + '.png')).size)


if __name__ == '__main__':
    main()
