#!/usr/bin/env python3
"""Paint the big sepia X between Pista's shoulder blades onto the Meshy texture (deterministic fix, Meshy omitted it).
Usage: paint_pista_x.py in.glb out_texture.png"""
import sys, numpy as np
sys.path.insert(0, __file__.rsplit("/", 1)[0])
import glb_tools as T
g, tex = T.load(sys.argv[1])
CY, HALF, W = 0.325, 0.060, 0.028   # model units (model is ~1.9 tall, feet at -0.95)
def region(P):
    x, y, z = P[:, 0], P[:, 1] - CY, P[:, 2]
    d1 = np.abs(x - y) / np.sqrt(2); d2 = np.abs(x + y) / np.sqrt(2)  # distance to the two diagonals
    return (z < 0.02) & (np.abs(x) < HALF + .01) & (np.abs(y) < HALF + .01) & ((d1 < W / 2) | (d2 < W / 2))
guard = lambda t: t[:, 2].astype(int) < t[:, 0].astype(int) + 20  # not teal sash
bb = (np.array([-HALF, CY - HALF, -0.5]), np.array([HALF, CY + HALF, 0.05]))
out = T.paint_by_position(g, tex, region, (122, 74, 34), guard, bb)
out.save(sys.argv[2])
