#!/usr/bin/env python3
"""Procedural Pickup_Boost: double chevron prism, 1 m tall, centered, front faces -Z. Pink #FF3FA4 front/back, ink #1E1A24 sides.
Procedural (no Meshy credits): the text-to-3D attempts did not produce a double chevron. Usage: make_boost.py <out.glb>"""
import sys
import numpy as np, trimesh
from PIL import Image

def chevron(y0):
    # outline CCW seen from +Z: apex O, right outer R1, right inner R2, inner apex I, left inner L2, left outer L1
    return np.array([(0, y0 + .46), (.5, y0), (.5, y0 - .24), (0, y0 + .22), (-.5, y0 - .24), (-.5, y0)])

def build(depth=0.16):
    V, F, UV = [], [], []
    def tri(a, b, c, uv):
        i = len(V); V.extend([a, b, c]); F.append([i, i + 1, i + 2]); UV.extend([uv] * 3)
    FRONT, SIDE = (.25, .5), (.75, .5)
    for y0 in (0.29, -0.23):
        P = chevron(y0); zf, zb = -depth / 2, depth / 2   # front = -Z
        for quad in ([0, 1, 2, 3], [0, 3, 4, 5]):
            q = P[quad]
            for a, b, c in ((0, 1, 2), (0, 2, 3)):  # CCW from +Z -> back cap faces +Z; front cap is reversed
                tri((*q[a], zb), (*q[b], zb), (*q[c], zb), FRONT)
                tri((*q[c], zf), (*q[b], zf), (*q[a], zf), FRONT)
        for i in range(6):  # side walls
            a, b = P[i], P[(i + 1) % 6]
            tri((*a, zf), (*b, zf), (*b, zb), SIDE); tri((*a, zf), (*b, zb), (*a, zb), SIDE)
    V = np.array(V, float); V[:, 1] -= (V[:, 1].min() + V[:, 1].max()) / 2; V *= 1.0 / np.ptp(V[:, 1])
    m = trimesh.Trimesh(V, np.array(F)[:, ::-1], process=False)
    tex = np.zeros((16, 16, 3), np.uint8); tex[:, :8] = (255, 63, 164); tex[:, 8:] = (30, 26, 36)
    m.visual = trimesh.visual.TextureVisuals(uv=np.array(UV), material=trimesh.visual.material.PBRMaterial(
        baseColorTexture=Image.fromarray(tex), metallicFactor=0.0, roughnessFactor=1.0, name="Pickup_Boost"))
    return m

if __name__ == "__main__":
    m = build(); m.export(sys.argv[1]); print("Pickup_Boost", len(m.faces), m.extents.round(3).tolist())
