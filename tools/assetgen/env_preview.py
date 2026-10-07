#!/usr/bin/env python3
"""Software preview renderer + contact sheet for environment GLBs (trimesh, numpy, Pillow; no GPU/Blender).
Usage: env_preview.py <out.png> <glb> [glb ...]   Each cell: 3/4 view and side view, flat-shaded with the base color texture."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import trimesh

def load(path):
    s = trimesh.load(path, force="mesh")
    tex = None
    try: tex = np.array(s.visual.material.baseColorTexture.convert("RGB"))
    except Exception: pass
    return s, tex

def render(m, tex, yaw, pitch, size=320):
    V = np.asarray(m.vertices, float); c = (m.bounds[0] + m.bounds[1]) / 2; V = V - c
    ry, rx = np.radians(yaw), np.radians(pitch)
    Ry = np.array([[np.cos(ry), 0, np.sin(ry)], [0, 1, 0], [-np.sin(ry), 0, np.cos(ry)]])
    Rx = np.array([[1, 0, 0], [0, np.cos(rx), -np.sin(rx)], [0, np.sin(rx), np.cos(rx)]])
    P = V @ Ry.T @ Rx.T
    ext = max(np.ptp(P[:, 0]), np.ptp(P[:, 1])); sc = size * 0.86 / ext
    sx = P[:, 0] * sc + size / 2; sy = size / 2 - P[:, 1] * sc
    img = np.full((size, size, 3), 226, np.uint8); zb = np.full((size, size), -1e9)
    uv = getattr(m.visual, "uv", None); H, W = (tex.shape[:2] if tex is not None else (1, 1))
    L = np.array([0.4, 0.7, 0.6]); L /= np.linalg.norm(L)
    for f in np.asarray(m.faces):
        px, py, pz = sx[f], sy[f], P[f, 2]
        x0, x1 = int(max(px.min(), 0)), int(min(px.max() + 1, size - 1)); y0, y1 = int(max(py.min(), 0)), int(min(py.max() + 1, size - 1))
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        d = (py[1] - py[2]) * (px[0] - px[2]) + (px[2] - px[1]) * (py[0] - py[2])
        if abs(d) < 1e-9: continue
        a = ((py[1] - py[2]) * (xs - px[2]) + (px[2] - px[1]) * (ys - py[2])) / d
        b = ((py[2] - py[0]) * (xs - px[2]) + (px[0] - px[2]) * (ys - py[2])) / d
        cc = 1 - a - b; k = (a >= 0) & (b >= 0) & (cc >= 0)
        z = a * pz[0] + b * pz[1] + cc * pz[2]; k &= z > zb[y0:y1 + 1, x0:x1 + 1]
        if not k.any(): continue
        n = np.cross(P[f[1]] - P[f[0]], P[f[2]] - P[f[0]]); nn = np.linalg.norm(n)
        sh = 0.55 + 0.45 * abs((n / nn) @ L) if nn > 0 else 1.0
        if tex is not None and uv is not None:
            u = a * uv[f[0], 0] + b * uv[f[1], 0] + cc * uv[f[2], 0]; v = a * uv[f[0], 1] + b * uv[f[1], 1] + cc * uv[f[2], 1]
            col = tex[np.clip(((1 - v) * H).astype(int), 0, H - 1), np.clip((u * W).astype(int), 0, W - 1)]
        else: col = np.full(a.shape + (3,), 160)
        sub = img[y0:y1 + 1, x0:x1 + 1]; sub[k] = (col[k] * sh).astype(np.uint8); zb[y0:y1 + 1, x0:x1 + 1][k] = z[k]
    return Image.fromarray(img)

def sheet(paths, out, size=320, cols=2):
    cells = []
    for p in paths:
        m, tex = load(p); tris = len(m.faces)
        a, b = render(m, tex, 35, 20, size), render(m, tex, 90, 0, size)
        cell = Image.new("RGB", (size * 2, size + 22), (226, 226, 226)); cell.paste(a, (0, 22)); cell.paste(b, (size, 22))
        ImageDraw.Draw(cell).text((6, 5), f"{Path(p).stem}  {tris} tris  size {np.round(m.extents, 2).tolist()}", fill=(20, 20, 20))
        cells.append(cell)
    rows = (len(cells) + cols - 1) // cols; w, h = cells[0].size
    S = Image.new("RGB", (w * cols, h * rows), (255, 255, 255))
    for i, c in enumerate(cells): S.paste(c, ((i % cols) * w, (i // cols) * h))
    S.save(out)

if __name__ == "__main__":
    sheet(sys.argv[2:], sys.argv[1])
