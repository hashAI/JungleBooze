#!/usr/bin/env python3
"""Headless-free GLB helpers (trimesh + numpy + Pillow): texel painting by 3D position, and a small
orthographic software renderer for previews. Usage: see __main__ in callers."""
import numpy as np
from PIL import Image

def load(path):
    import trimesh
    s = trimesh.load(path); g = list(s.geometry.values())[0]
    return g, g.visual.material.baseColorTexture.convert("RGB")

def paint_by_position(g, tex, region_fn, color, guard_fn=None, bbox=None):
    """For every texel covered by a triangle, interpolate the 3D position; paint `color` where region_fn(pos Nx3)->bool
    and guard_fn(rgb Nx3)->bool (existing texel) are true."""
    t = np.array(tex); H, W = t.shape[:2]
    uv = g.visual.uv; V = g.vertices
    for f in g.faces:
        p = V[f]
        if bbox is not None and (np.any(p.max(0) < bbox[0]) or np.any(p.min(0) > bbox[1])): continue
        q = uv[f] * [W, H]; q[:, 1] = H - q[:, 1]  # glTF v flipped
        x0, x1 = int(max(q[:, 0].min() - 1, 0)), int(min(q[:, 0].max() + 1, W - 1))
        y0, y1 = int(max(q[:, 1].min() - 1, 0)), int(min(q[:, 1].max() + 1, H - 1))
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        d = (q[1, 1] - q[2, 1]) * (q[0, 0] - q[2, 0]) + (q[2, 0] - q[1, 0]) * (q[0, 1] - q[2, 1])
        if abs(d) < 1e-9: continue
        a = ((q[1, 1] - q[2, 1]) * (xs - q[2, 0]) + (q[2, 0] - q[1, 0]) * (ys - q[2, 1])) / d
        b = ((q[2, 1] - q[0, 1]) * (xs - q[2, 0]) + (q[0, 0] - q[2, 0]) * (ys - q[2, 1])) / d
        c = 1 - a - b; m = (a >= -0.02) & (b >= -0.02) & (c >= -0.02)
        if not m.any(): continue
        pos = a[m][:, None] * p[0] + b[m][:, None] * p[1] + c[m][:, None] * p[2]
        ix, iy = xs[m].astype(int), ys[m].astype(int)
        ok = region_fn(pos)
        if guard_fn: ok &= guard_fn(t[iy, ix])
        t[iy[ok], ix[ok]] = color
    return Image.fromarray(t)

def render(g, tex, view="back", size=512):
    """Orthographic unlit render. view: front/back/left/right. Model faces +Z, up +Y."""
    V = g.vertices.copy(); uv = g.visual.uv; t = np.array(tex); H, W = t.shape[:2]
    ang = {"front": 0, "right": 90, "back": 180, "left": 270}[view]; r = np.radians(ang)
    R = np.array([[np.cos(r), 0, np.sin(r)], [0, 1, 0], [-np.sin(r), 0, np.cos(r)]])
    P = V @ R.T
    lo, hi = P[:, 1].min(), P[:, 1].max(); sc = size * 0.9 / (hi - lo)
    sx = P[:, 0] * sc + size / 2; sy = size * 0.95 - (P[:, 1] - lo) * sc
    img = np.full((size, size, 3), 217, np.uint8); zb = np.full((size, size), -1e9)
    for f in g.faces:
        px, py, pz = sx[f], sy[f], P[f, 2]
        x0, x1 = int(max(px.min(), 0)), int(min(px.max() + 1, size - 1)); y0, y1 = int(max(py.min(), 0)), int(min(py.max() + 1, size - 1))
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        d = (py[1] - py[2]) * (px[0] - px[2]) + (px[2] - px[1]) * (py[0] - py[2])
        if abs(d) < 1e-9: continue
        a = ((py[1] - py[2]) * (xs - px[2]) + (px[2] - px[1]) * (ys - py[2])) / d
        b = ((py[2] - py[0]) * (xs - px[2]) + (px[0] - px[2]) * (ys - py[2])) / d
        c = 1 - a - b; m = (a >= 0) & (b >= 0) & (c >= 0)
        z = a * pz[0] + b * pz[1] + c * pz[2]; m &= z > zb[y0:y1 + 1, x0:x1 + 1]
        if not m.any(): continue
        u = a * uv[f[0], 0] + b * uv[f[1], 0] + c * uv[f[2], 0]; v = a * uv[f[0], 1] + b * uv[f[1], 1] + c * uv[f[2], 1]
        tu = np.clip((u * W).astype(int), 0, W - 1); tv = np.clip(((1 - v) * H).astype(int), 0, H - 1)
        sub = img[y0:y1 + 1, x0:x1 + 1]; sub[m] = t[tv, tu][m]; zb[y0:y1 + 1, x0:x1 + 1][m] = z[m]
    return Image.fromarray(img)
