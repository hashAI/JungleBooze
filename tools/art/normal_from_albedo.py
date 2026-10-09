#!/usr/bin/env python3
"""Tangent-space normal map (OpenGL, +Y up) for leaf/flower card atlases from albedo luminance + alpha.
Height = blurred luminance (veins and midribs are lighter, so they read raised) plus a soft dome from the alpha
distance field (leaf blades are thicker in the middle). Usage: normal_from_albedo.py <in_rgba.png> <out.png> [strength]"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage
src, dst = sys.argv[1], sys.argv[2]
k = float(sys.argv[3]) if len(sys.argv) > 3 else 2.5
im = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32) / 255
lum = im[..., :3] @ np.array([0.2126, 0.7152, 0.0722])
a = im[..., 3]
hgt = ndimage.gaussian_filter(lum, 1.0) * 0.6 + np.clip(ndimage.distance_transform_edt(a > 0.5) / 12, 0, 1) * 0.4
gx = ndimage.sobel(hgt, 1) * k
gy = ndimage.sobel(hgt, 0) * k
n = np.dstack([-gx, gy, np.ones_like(hgt)])          # image y runs down, OpenGL green is up
n /= np.linalg.norm(n, axis=2, keepdims=True)
out = (n * 0.5 + 0.5)
out[a < 0.02] = (0.5, 0.5, 1.0)
Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGB').save(dst, optimize=True)
print(dst)
