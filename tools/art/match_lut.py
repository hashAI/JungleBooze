#!/usr/bin/env python3
"""Keyframe-matched grading LUT for URP's Color Lookup (hero basin, ADR 0008).

Builds a 32^3 LUT strip (1024x32 PNG, gamma-space values, linear import) that maps the colours of a render made
*without* the LUT toward the colour distribution of the keyframe: per-channel histogram matching (monotonic tone
curves per channel, which fixes the cast, black/white points and contrast), blended with identity by --strength.
Only the centre crop of the render at the keyframe's aspect is compared, so both frames show the same view.

  python3 tools/art/match_lut.py --render /tmp/junglebooze-hero/F4_landscape.png \
      --keyframe design/aurelia/keyframes/F4_e_openai_medium.jpg \
      --out UnityProject/Assets/_Game/Art/HeroBasin/Grade/HeroBasin_F4e_Lut.png --strength 0.7

Layout (URP ApplyLut2D): x = blue_slice * 32 + red, y = green from the bottom (PNG row 0 is the top).
Re-run after a lighting change: always measure a render made with the LUT turned off (contribution 0).
"""
import argparse

import numpy as np
from PIL import Image

SIZE = 32


def centre_crop(image, aspect):
    w, h = image.size
    if w / h > aspect:
        cw = int(round(h * aspect))
        x0 = (w - cw) // 2
        return image.crop((x0, 0, x0 + cw, h))
    ch = int(round(w / aspect))
    y0 = (h - ch) // 2
    return image.crop((0, y0, w, y0 + ch))


def channel_curve(source, target, bins=256):
    """Monotonic curve (bins values in 0..1) mapping the source distribution onto the target distribution."""
    s_hist, _ = np.histogram(source, bins=bins, range=(0.0, 1.0))
    t_hist, _ = np.histogram(target, bins=bins, range=(0.0, 1.0))
    s_cdf = np.cumsum(s_hist).astype(np.float64)
    t_cdf = np.cumsum(t_hist).astype(np.float64)
    s_cdf /= s_cdf[-1]
    t_cdf /= t_cdf[-1]
    centres = (np.arange(bins) + 0.5) / bins
    curve = np.interp(s_cdf, t_cdf, centres)
    # Smooth a little (a box filter over 9 bins) so sparse bins do not make steps, keep it monotonic.
    kernel = np.ones(9) / 9.0
    padded = np.pad(curve, 4, mode="edge")
    curve = np.convolve(padded, kernel, mode="valid")
    return np.maximum.accumulate(np.clip(curve, 0.0, 1.0))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--render", required=True)
    parser.add_argument("--keyframe", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--strength", type=float, default=0.7, help="0 = identity, 1 = full histogram match")
    args = parser.parse_args()

    key = Image.open(args.keyframe).convert("RGB")
    render = centre_crop(Image.open(args.render).convert("RGB"), key.width / key.height)
    work = (512, int(round(512 * key.height / key.width)))
    k = np.asarray(key.resize(work, Image.BILINEAR), dtype=np.float64) / 255.0
    r = np.asarray(render.resize(work, Image.BILINEAR), dtype=np.float64) / 255.0

    bins = 256
    centres = (np.arange(bins) + 0.5) / bins
    curves = []
    for c in range(3):
        matched = channel_curve(r[..., c].ravel(), k[..., c].ravel(), bins)
        curves.append(centres + (matched - centres) * args.strength)

    grid = np.arange(SIZE) / (SIZE - 1.0)
    lut = np.zeros((SIZE, SIZE * SIZE, 3))
    for b in range(SIZE):
        for g in range(SIZE):
            row = SIZE - 1 - g
            x0 = b * SIZE
            lut[row, x0:x0 + SIZE, 0] = np.interp(grid, centres, curves[0])
            lut[row, x0:x0 + SIZE, 1] = np.interp(grid[g], centres, curves[1])
            lut[row, x0:x0 + SIZE, 2] = np.interp(grid[b], centres, curves[2])

    Image.fromarray(np.clip(np.round(lut * 255.0), 0, 255).astype(np.uint8), "RGB").save(args.out)
    for c, name in enumerate("RGB"):
        samples = ", ".join("%.2f->%.2f" % (v, np.interp(v, centres, curves[c])) for v in (0.1, 0.25, 0.5, 0.75, 0.9))
        print("%s: %s" % (name, samples))
    print("wrote", args.out)


if __name__ == "__main__":
    main()
