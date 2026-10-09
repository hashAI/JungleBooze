#!/usr/bin/env python3
"""Procedural mist band card (in-house, no AI): soft fbm mist, white-warm, alpha fades top/bottom, tiles in X.
Usage: python3 mist_band.py <out.png> [width height seed]"""
import sys
import numpy as np
from PIL import Image
out = sys.argv[1]
W, H = (int(sys.argv[2]), int(sys.argv[3])) if len(sys.argv) > 3 else (1024, 256)
seed = int(sys.argv[4]) if len(sys.argv) > 4 else 7
rng = np.random.default_rng(seed)
def octave(fx, fy):
    g = rng.random((fy + 1, fx))
    g = np.concatenate([g, g[:, :1]], 1)          # wrap in X so the card tiles
    yy = np.linspace(0, fy, H, endpoint=False); xx = np.linspace(0, fx, W, endpoint=False)
    y0 = yy.astype(int); x0 = xx.astype(int); ty = (yy - y0)[:, None]; tx = (xx - x0)[None, :]
    ty = ty * ty * (3 - 2 * ty); tx = tx * tx * (3 - 2 * tx)
    a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty
n = sum(octave(4 * 2 ** o, 1 * 2 ** o + 1) / 2 ** o for o in range(6)) / 1.97
y = np.linspace(0, 1, H)[:, None]
profile = np.clip(np.sin(np.pi * y) ** 1.6, 0, 1) * np.clip(1.15 - y * 0.5, 0, 1)
alpha = np.clip((n - 0.32) * 2.2, 0, 1) * profile
alpha = np.clip(alpha * 0.85, 0, 1)
col = np.ones((H, W, 3)) * np.array([0.93, 0.94, 0.93]) - (1 - n[..., None]) * np.array([0.10, 0.09, 0.07])
img = np.dstack([np.clip(col, 0, 1), alpha])
Image.fromarray((img * 255 + 0.5).astype(np.uint8), 'RGBA').save(out, optimize=True)
print(out, W, H)
