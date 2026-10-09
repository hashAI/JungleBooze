#!/usr/bin/env python3
"""Painterly (F4_f) hero basin: turn the OpenAI cut-outs into kit textures.

Inputs (art_source/environment/openai/painterly/, cut with chroma_unmix.py first, see the environment README):
  atlases/p_atlas_<name>_<take>_cut.png, backdrops/p_bd_*_cut.png | p_bd_sky.png | p_bd_plate.png,
  swatches/p_swatch_rock_<take>.png, p_swatch_moss_<take>.png
Outputs:
  Plants/Textures/FP_<Atlas>_P_BaseColor.png (+ _Normal via normal_from_albedo.py)
  Backdrops/BD_F4f_*.png
  art_source/environment/work/painterly/swatch_rock_tile.png, swatch_moss_tile.png (bake inputs, made tileable)
Semi-transparent mist keeps a pink cast from the magenta key; every texel under alpha 0.9 gets the foliage clamp
(B <= 1.0 G, R <= 1.25 G) blended toward its luminance-matched warm grey, so mist edges read warm white.
Usage: python3 painterly_finish.py [atlases] [backdrops] [swatches]   (Blender's Python 3.11: numpy, scipy, Pillow)
"""
import os
import subprocess
import sys

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(REPO, 'art_source', 'environment', 'openai', 'painterly')
ENV = os.path.join(REPO, 'UnityProject', 'Assets', '_Game', 'Art', 'Environment')
WORK = os.path.join(REPO, 'art_source', 'environment', 'work', 'painterly')

ATLASES = {  # kit atlas name: (source cut, normal strength)
    'FP_BigLeaf_P': ('p_atlas_bigleaf_c1_cut.png', 2.5),
    'FP_Bellflower_P': ('p_atlas_bellflower_c1_cut.png', 2.5),
    'FP_Fronds_P': ('p_atlas_fronds_c2_cut.png', 2.0),
    'FP_Canopy_P': ('p_atlas_canopy_c1_cut.png', 2.0),
    'FP_ArchVines_P': ('p_atlas_vines_c1_cut.png', 1.8),
}
BACKDROPS = {
    'BD_F4f_Sky': 'p_bd_sky.png',
    'BD_F4f_FarRange': 'p_bd_far_range_cut.png',          # bottom clamp: see BOTTOM
    'BD_F4f_FarFalls': 'p_bd_falls_cliff_cut.png',
    'BD_F4f_MidPillars': 'p_bd_mid_pillars_cut.png',
    'BD_F4f_JungleWall': 'p_bd_jungle_wall_cut.png',
    'BD_F4f_Plate': 'p_bd_plate.png',
}
BOTTOM = {'BD_F4f_FarRange': 0.35}


def despill_soft(path_in, path_out, bottom=0.0):
    """bottom > 0: the lowest share of the content rows (where the mist meets the key) gets the clamp at every alpha;
    the far range's bottom trees came out red-pink there."""
    im = np.asarray(Image.open(path_in).convert('RGBA')).astype(np.float32) / 255
    F, a = im[..., :3].copy(), im[..., 3]
    if bottom > 0:
        h = a.shape[0]
        rows = np.where(a.max(1) > 0.1)[0]
        y0 = rows.max() - bottom * (rows.max() - rows.min())
        wr = np.clip((np.arange(h) - y0) / (0.08 * h), 0, 1)[:, None]
        cl = F.copy()
        cl[..., 2] = np.minimum(cl[..., 2], cl[..., 1] * 1.0)
        cl[..., 0] = np.minimum(cl[..., 0], cl[..., 1] * 1.2)
        F = F * (1 - wr[..., None]) + cl * wr[..., None]
    soft = np.clip((0.95 - a) / 0.3, 0, 1)          # 0 for solid texels, 1 for thin mist
    pinkish = np.clip((np.minimum(F[..., 0], F[..., 2]) - F[..., 1]) * 8 + 0.5, 0, 1)
    lum = F @ np.array([0.2126, 0.7152, 0.0722])
    grey = np.clip(lum[..., None] * np.array([1.04, 1.0, 0.9]), 0, 1)
    w = (soft * pinkish)[..., None]
    F = F * (1 - w) + grey * w
    F[..., 2] = np.where(a < 0.9, np.minimum(F[..., 2], F[..., 1] * 1.0), F[..., 2])
    F[..., 0] = np.where(a < 0.9, np.minimum(F[..., 0], F[..., 1] * 1.25), F[..., 0])
    Image.fromarray((np.dstack([F, a]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(path_out, optimize=True)


def tileable(path_in, path_out, blend=0.25):
    """Offset-and-crossfade: the seams move to the centre and are hidden by a soft blend with the original."""
    im = np.asarray(Image.open(path_in).convert('RGB')).astype(np.float32)
    h, w, _ = im.shape
    sh = np.roll(np.roll(im, h // 2, 0), w // 2, 1)
    y = np.abs(np.arange(h) - h / 2) / (h / 2)
    x = np.abs(np.arange(w) - w / 2) / (w / 2)
    # weight of the shifted copy: 1 near the image border (where the original has its seams), 0 in the middle
    m = np.clip((np.maximum(y[:, None], x[None, :]) - (1 - 2 * blend)) / (2 * blend), 0, 1) ** 1.2
    out = im * (1 - m[..., None]) + sh * m[..., None]
    Image.fromarray(out.clip(0, 255).astype(np.uint8)).save(path_out)


def main():
    a = sys.argv[1:] or ['atlases', 'backdrops', 'swatches']
    py = sys.executable
    if 'atlases' in a:
        tdir = os.path.join(ENV, 'Plants', 'Textures')
        for name, (src, k) in ATLASES.items():
            dst = os.path.join(tdir, name + '_BaseColor.png')
            Image.open(os.path.join(SRC, 'atlases', src)).save(dst, optimize=True)
            subprocess.check_call([py, os.path.join(REPO, 'tools', 'art', 'normal_from_albedo.py'), dst,
                                   os.path.join(tdir, name + '_Normal.png'), str(k)])
            print(name)
    if 'backdrops' in a:
        bdir = os.path.join(ENV, 'Backdrops')
        for name, src in BACKDROPS.items():
            p = os.path.join(SRC, 'backdrops', src)
            dst = os.path.join(bdir, name + '.png')
            if src.endswith('_cut.png'):
                despill_soft(p, dst, BOTTOM.get(name, 0.0))
            else:
                Image.open(p).convert('RGB').save(dst, optimize=True)
            print(name, Image.open(dst).size)
    if 'swatches' in a:
        os.makedirs(WORK, exist_ok=True)
        tileable(os.path.join(SRC, 'swatches', 'p_swatch_rock_c1.png'), os.path.join(WORK, 'swatch_rock_tile.png'))
        tileable(os.path.join(SRC, 'swatches', 'p_swatch_moss_c1.png'), os.path.join(WORK, 'swatch_moss_tile.png'))
        print('swatches')


if __name__ == '__main__':
    main()
