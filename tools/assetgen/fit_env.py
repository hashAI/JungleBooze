#!/usr/bin/env python3
"""Fit raw Meshy environment GLBs to the Art/Environment/README.md fit rules (headless: trimesh + numpy + Pillow).
Per asset: optional cleanup cuts, optional texture painting (hazard stripes), texture downsize (<=1024), scale/pivot, export.
Usage: fit_env.py <raw_dir> <out_dir> [Name ...]   Prints name, tris, size, texture size."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image
import trimesh
sys.path.insert(0, str(Path(__file__).parent))
import glb_tools as G

RED, INK = (215, 38, 61), (30, 26, 36)
CUBE = "cube"
# kind: cube (non-uniform to unit cube), sphere, coin, icon, tile, edge, prop(height m), foliage(height m or width m)
SPEC = {
 "Pickup_Coin": dict(kind="coin", tex=512), "Obstacle_LowBarrier": dict(kind=CUBE, tex=512),
 "Obstacle_HighBarrier": dict(kind=CUBE, tex=1024), "Obstacle_FullBlock": dict(kind=CUBE, tex=512),
 "Obstacle_Boulder": dict(kind="sphere", tex=512), "Hazard_ThornPatch": dict(kind=CUBE, tex=1024),
 "Hazard_StrikeColumn": dict(kind=CUBE, tex=512), "Pickup_Magnet": dict(kind="icon", tex=512),
 "Pickup_Shield": dict(kind="icon", tex=512), "Pickup_Boost": dict(kind="icon", tex=512),
 "Ground_PathTile": dict(kind="tile", tex=1024), "Ground_RavineEdge": dict(kind="edge", tex=1024),
 "Prop_VineBranch": dict(kind=CUBE, tex=512), "Prop_Signpost": dict(kind="real", height=2.0, tex=512),
 "Foliage_TreeA": dict(kind="real", height=4.0, tex=1024), "Foliage_TreeB": dict(kind="real", height=6.0, tex=1024),
 "Foliage_Bush": dict(kind="real", width=1.8, tex=512),
}

def keep_faces(m, mask):
    m.update_faces(mask); m.remove_unreferenced_vertices()

def cleanup(name, m):
    c = m.triangles_center
    if name == "Pickup_Coin":  # drop the stand legs: keep the disc only
        yc = m.bounds[1][1] - (m.bounds[1][0] - m.bounds[0][0]) / 2
        keep_faces(m, np.hypot(c[:, 0], c[:, 1] - yc) <= 0.86)
    elif name == "Hazard_ThornPatch":  # drop the loose ground skirt around the slab
        ext = np.abs(m.vertices[m.faces])[:, :, [0, 2]].max(axis=(1, 2)); low = m.vertices[m.faces][:, :, 1].min(1)
        keep_faces(m, ~((ext > 0.78) & (low < -0.26)) & ~(c[:, 1] < -0.33) | (c[:, 1] > -0.26) & (ext <= 0.9))
    elif name == "Obstacle_LowBarrier":  # drop the little sapling on top of the log
        keep_faces(m, c[:, 1] < m.bounds[0][1] + 0.62 * (m.bounds[1][1] - m.bounds[0][1]) * 1.0)

def stripes(d, w): return (np.floor(d / w).astype(int) % 2) == 0

def paint(name, m, tex):
    if name == "Obstacle_HighBarrier":  # red/ink diagonal stripes on the pale plank only
        reg = lambda p: p[:, 1] > 0.2
        grd = lambda t: t.mean(1) > 185
        def col(p): return None
        for sel, colr in ((True, RED), (False, INK)):
            tex = G.paint_by_position(m, tex, lambda p, s=sel: reg(p) & (stripes(p[:, 0] * 0.7 + p[:, 1], 0.14) == s), colr, grd)
    elif name == "Hazard_ThornPatch":  # stripe the slab sides
        for sel, colr in ((True, RED), (False, INK)):
            tex = G.paint_by_position(m, tex, lambda p, s=sel: (p[:, 1] < 0.14) & (p[:, 1] > -0.4) & (stripes((p[:, 0] + p[:, 2]) * 0.7 + p[:, 1], 0.13) == s), colr)
    return tex

def place(spec, m):
    lo, hi = m.bounds; ext = hi - lo; ctr = (lo + hi) / 2; k = spec["kind"]; V = m.vertices
    if k == CUBE: V[:] = (V - ctr) / ext
    elif k == "sphere": V[:] = (V - ctr) / ext
    elif k == "coin": V[:] = (V - ctr) * (0.5 / max(ext[0], ext[1]))
    elif k == "icon":  # face -Z, ~1 m across, centered
        V[:] = (V - ctr) * (1.0 / max(ext[0], ext[1])); V[:, [0, 2]] *= -1
    elif k == "tile":
        s = np.array([1 / ext[0], (1 / ext[0] + 1 / ext[2]) / 2, 1 / ext[2]]); V[:] = (V - ctr) * s; V[:, 1] -= V[:, 1].max()
    elif k == "edge":
        s = np.array([1 / ext[0], (1 / ext[0] + 1 / ext[2]) / 2, 1 / ext[2]]); V[:] = (V - ctr) * s
        V[:, 1] -= V[:, 1].max(); V[:, 2] -= V[:, 2].max()
    elif k == "real":
        s = spec["height"] / ext[1] if "height" in spec else spec["width"] / max(ext[0], ext[2])
        base = V[V[:, 1] < lo[1] + 0.03 * ext[1]]; bx, bz = base[:, 0].mean(), base[:, 2].mean()
        V[:, 0] -= bx; V[:, 2] -= bz; V[:, 1] -= lo[1]; V *= s
    m.vertices = V

def fit(raw, out, names):
    Path(out).mkdir(parents=True, exist_ok=True); res = {}
    for n in names:
        sp = SPEC[n]; m = trimesh.load(f"{raw}/{n}.glb", force="mesh")
        tex = m.visual.material.baseColorTexture.convert("RGB"); uv = m.visual.uv.copy()
        tex = tex.resize((1024, 1024), Image.LANCZOS)
        cleanup(n, m)
        if m.visual.uv is None or len(m.visual.uv) != len(m.vertices): raise SystemExit("uv lost on " + n)
        tex = paint(n, m, tex)
        tex = tex.resize((sp["tex"],) * 2, Image.LANCZOS) if sp["tex"] != 1024 else tex
        place(sp, m)
        mat = trimesh.visual.material.PBRMaterial(baseColorTexture=tex, metallicFactor=0.0, roughnessFactor=1.0, name=n)
        m.visual = trimesh.visual.TextureVisuals(uv=m.visual.uv, material=mat)
        m.export(f"{out}/{n}.glb")
        res[n] = (len(m.faces), np.round(m.extents, 3).tolist(), np.round(m.bounds[0], 3).tolist(), sp["tex"])
        print(n, *res[n])
    return res

if __name__ == "__main__":
    fit(sys.argv[1], sys.argv[2], sys.argv[3:] or list(SPEC))
