"""Paint the worn treasure-map canvas patch with a bold sepia X (take B nod, ART_DIRECTION 7.2).

Pure numpy + Pillow, deterministic (fixed seed). No text, no logos (checklist A3).
Writes RGBA color (alpha = patch shape with frayed edge) and a grayscale height map used for the bump.

Usage: python paint_map_patch.py <out_dir> [size_px]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

out = sys.argv[1] if len(sys.argv) > 1 else '.'
N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
rng = np.random.default_rng(1607)
os.makedirs(out, exist_ok=True)


def fbm(n, octaves=6, base=4):
    acc = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        res = base * 2 ** o
        g = rng.random((res + 1, res + 1)).astype(np.float32)
        acc += amp * np.asarray(Image.fromarray(g).resize((n, n), Image.BICUBIC))
        tot += amp
        amp *= 0.5
    acc /= tot
    return (acc - acc.min()) / (acc.max() - acc.min() + 1e-6)


yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N   # v down, u right

# --- Patch shape: slightly rotated rectangle with a noisy, frayed edge -------------------------------------
ang = np.radians(-2.0)
cu, cv = xx - 0.5, yy - 0.5
ru = cu * np.cos(ang) - cv * np.sin(ang)
rv = cu * np.sin(ang) + cv * np.cos(ang)
edge_noise = (fbm(N, 5, 8) - 0.5) * 0.035 + (rng.random((N, N)).astype(np.float32) - 0.5) * 0.006
half = 0.455
d_edge = half - np.maximum(np.abs(ru), np.abs(rv)) + edge_noise   # >0 inside
alpha = np.clip(d_edge / 0.006, 0, 1)

# --- Canvas color: tan with mottling, weave, stains --------------------------------------------------------
canvas = np.array([196, 168, 122], np.float32) / 255
mott = fbm(N, 6, 3)
weave = 0.5 + 0.5 * np.sin(xx * N * np.pi / 3.0) * np.sin(yy * N * np.pi / 3.0)
grain = fbm(N, 3, 128)
col = canvas[None, None, :] * (0.84 + 0.22 * mott[..., None]) * (0.95 + 0.07 * weave[..., None]) * (0.95 + 0.08 * grain[..., None])
stain = np.clip((fbm(N, 4, 2) - 0.62) * 3.0, 0, 1)
col = col * (1 - 0.18 * stain[..., None]) + np.array([120, 92, 58], np.float32)[None, None] / 255 * 0.18 * stain[..., None]
# darker, grubby rim
rim = np.clip(1 - d_edge / 0.06, 0, 1) ** 1.6
col *= (1 - 0.30 * rim[..., None])

ink = np.array([96, 58, 30], np.float32) / 255       # sepia
ink_faint = np.array([150, 112, 70], np.float32) / 255


def stroke_mask(p0, p1, width, rough=0.25):
    """Distance-to-segment brush stroke with a ragged, dry-brush edge."""
    p0 = np.array(p0, np.float32); p1 = np.array(p1, np.float32)
    d = p1 - p0
    t = np.clip(((xx - p0[0]) * d[0] + (yy - p0[1]) * d[1]) / (d @ d), 0, 1)
    dist = np.hypot(xx - (p0[0] + t * d[0]), yy - (p0[1] + t * d[1]))
    w = width * (0.85 + 0.3 * np.sin(t * np.pi))       # fatter in the middle
    w = w * (1 + rough * (fbm(N, 4, 16) - 0.5) + 0.25 * (fbm(N, 3, 96) - 0.5))
    dry = np.clip(0.78 + 0.5 * fbm(N, 3, 64), 0, 1)          # dry-brush gaps inside the stroke
    return np.clip((w - dist) / 0.003, 0, 1) * dry


def put(mask, color, strength=1.0):
    global col
    m = (mask * strength)[..., None]
    color = color if color.ndim == 3 else color[None, None]
    col = col * (1 - m) + color * m


# faded map marks: two contour-like wavy lines and a dotted trail toward the X (no text)
for k, (a0, ph) in enumerate([(0.30, 0.4), (0.72, 2.1)]):
    curve = a0 + 0.05 * np.sin(xx * 9 + ph) + 0.02 * np.sin(xx * 23 + ph * 2)
    put(np.clip(1 - np.abs(yy - curve) / 0.0035, 0, 1) * (np.abs(ru) < 0.40), ink_faint, 0.55)
trail_t = np.linspace(0, 1, 15)
for t in trail_t:
    px = 0.14 + t * 0.30
    py = 0.82 - t * 0.26 + 0.04 * np.sin(t * 6)
    dot = np.clip((0.0085 - np.hypot(xx - px, yy - py)) / 0.002, 0, 1)
    put(dot, ink_faint, 0.75)

# the bold X, slightly right of center and above, like the concept
xc, yc, s = 0.56, 0.47, 0.20
xm = np.maximum(stroke_mask((xc - s, yc - s), (xc + s, yc + s), 0.034),
                stroke_mask((xc + s * 0.95, yc - s * 1.05), (xc - s * 0.9, yc + s), 0.032))
ink_var = (0.85 + 0.3 * fbm(N, 4, 10))[..., None]   # uneven ink load
put(xm, ink[None, None] * ink_var, 0.93)

# stitching just inside the edge (short dark-brown dashes)
inset = np.abs(np.maximum(np.abs(ru), np.abs(rv)) - (half - 0.045))
along = np.where(np.abs(ru) > np.abs(rv), rv, ru)
dash = (np.mod(along * 46, 1.0) < 0.55)
put(np.clip(1 - inset / 0.005, 0, 1) * dash, np.array([70, 48, 30], np.float32) / 255, 0.85)

# grime overlay and clamp to the albedo range (ART_DIRECTION 10.2: no pure black/white)
col *= (0.93 + 0.07 * fbm(N, 5, 6))[..., None]
col = np.clip(col, 40 / 255, 225 / 255)

# height for the bump: canvas raised, stitches raised, edge rolled, weave
height = np.clip(d_edge / 0.05, 0, 1) * 0.6 + 0.08 * weave + 0.3 * np.clip(1 - inset / 0.005, 0, 1) * dash
height = height * alpha

rgba = np.dstack([col, alpha]).clip(0, 1)
Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), 'RGBA').save(os.path.join(out, 'patch_color.png'))
Image.fromarray((height * 255 + 0.5).astype(np.uint8), 'L').filter(ImageFilter.GaussianBlur(1.2)).save(
    os.path.join(out, 'patch_height.png'))
print('patch written to', out)
