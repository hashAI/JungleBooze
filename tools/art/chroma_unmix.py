#!/usr/bin/env python3
"""Magenta chroma-key unmix for AI backdrop layers (gpt-image-2 has no transparent output).
alpha from how magenta a pixel is (min(R,B) - G against the measured key), colour unmixed as
F = (C - (1 - a) K) / a, then colour bled into transparent texels (no fringes in mips/ASTC) and cropped to content.
Usage: python3 chroma_unmix.py <in_key.png> <out.png> [--pad 8] [--fade-bottom N] [--nocrop] [--foliage]   (needs numpy, Pillow, scipy)
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

a = sys.argv[1:]
src, dst = a[0], a[1]
pad = int(a[a.index('--pad') + 1]) if '--pad' in a else 8
im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32) / 255
h, w, _ = im.shape
# key colour = median of clearly magenta pixels
mag = (im[..., 0] > 0.8) & (im[..., 2] > 0.8) & (im[..., 1] < 0.25)
K = np.median(im[mag], axis=0)
k0 = min(K[0], K[2]) - K[1]
m = np.clip((np.minimum(im[..., 0], im[..., 2]) - im[..., 1]) / k0, 0, 1)
alpha = 1 - m
alpha = np.where(alpha < 0.03, 0, alpha)
alpha = np.where(alpha > 0.97, 1, alpha)
# generators leave a faint non-key line at the image border: clear weak alpha in a 4 px frame
b = np.zeros_like(alpha, bool); b[:4] = b[-4:] = True; b[:, :4] = b[:, -4:] = True
alpha = np.where(b & (alpha < 0.4), 0, alpha)
A = np.maximum(alpha, 1e-3)[..., None]
F = np.clip((im - (1 - alpha)[..., None] * K) / A, 0, 1)
# suppress any leftover magenta spill: B and R may not exceed G by more than the neighbourhood allows
spill = np.minimum(F[..., 0], F[..., 2]) - F[..., 1]
F[..., 0] -= np.clip(spill, 0, None) * 0.5
F[..., 2] -= np.clip(spill, 0, None)
# mist and haze edges: any remaining pink in semi-transparent texels becomes neutral (luminance kept)
lum = (F @ np.array([0.2126, 0.7152, 0.0722]))[..., None]
pink = np.clip(np.minimum(F[..., 0] - F[..., 1], F[..., 2] - F[..., 1] + 0.04) * 10, 0, 1)   # B~G and R>G: key spill
warm_grey = lum * np.array([1.02, 1.0, 0.96])
F = F * (1 - pink[..., None]) + np.clip(warm_grey, 0, 1) * pink[..., None]
# optional soft fade at the bottom edge (cards whose content runs off the generated frame)
if '--fade-bottom' in a:
    n = int(a[a.index('--fade-bottom') + 1])
    ys_ = np.where(alpha.max(1) > 0.1)[0]
    last = ys_.max() + 1
    ramp = np.clip((last - np.arange(h)) / n, 0, 1) ** 1.5
    alpha = alpha * ramp[:, None]
# rim clamp: within 6 px of the cut-out edge the generator tints leaves toward the key (salmon/pink rims).
# Natural colours here never have B > G or R > 1.2 G, so clamp them (keeps sunlit yellows).
near = ndimage.distance_transform_edt(alpha > 0.1) <= 6
spillish = near & (F[..., 2] > F[..., 1] * 0.97) & (F[..., 0] > F[..., 1] * 1.05)   # oranges/yellows have B << G
F[..., 2] = np.where(spillish, F[..., 1] * 0.97, F[..., 2])
F[..., 0] = np.where(spillish, np.minimum(F[..., 0], F[..., 1] * 1.2), F[..., 0])
# --foliage: green/brown-only sheets (vines, moss, canopy). Thin strands are mostly semi-transparent and keep a pink
# cast, so clamp every texel: B never above 0.9 G, R never above 1.25 G (browns stay brown, pink becomes olive).
if '--foliage' in a:
    F[..., 2] = np.minimum(F[..., 2], F[..., 1] * 0.9)
    F[..., 0] = np.minimum(F[..., 0], F[..., 1] * 1.25)
# bleed colour into transparent area (nearest opaque-ish texel)
solid = alpha > 0.5
idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
bled = F[idx[0], idx[1]]
F = np.where((alpha < 0.5)[..., None], bled, F)
ys, xs = np.where(alpha > 0.1)
y0, y1 = max(0, ys.min() - pad), min(h, ys.max() + pad + 1)
x0, x1 = max(0, xs.min() - pad), min(w, xs.max() + pad + 1)
if '--nocrop' in a:                      # atlases keep their full frame
    y0, y1, x0, x1 = 0, h, 0, w
out = np.dstack([F, alpha])[y0:y1, x0:x1]
Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGBA').save(dst, optimize=True)
print(f'{dst}: key {np.round(K, 3)}, crop {x1 - x0}x{y1 - y0} (from {w}x{h}), opaque {np.mean(alpha > 0.5):.2f}')
