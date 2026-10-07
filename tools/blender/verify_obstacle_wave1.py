"""Numeric check of the obstacle wave 1 FBX files AFTER export (re-import with bpy). Zero credits.
Usage: python3 verify_obstacle_wave1.py <EnvironmentArt dir>
Per piece: tris vs budget, lowest vertex (embed), extents (Unity axes) vs the hitbox, a ghost-distance estimate
(spec 005 3.2: first visible surface along +z from every sample point of the front face, 0.5 m if none), and a hazard-red audit
(ochre atlas row only on hazard pieces, always with ink faces present). Exit code 1 if a hard check fails.
Unity axes from the re-imported mesh: unity = (bx, bz, -by) (the exporter maps Unity (x,y,z) -> Blender (x,-z,y))."""
import os, sys, math
import numpy as np
import bpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_obstacle_wave1 as w1
from build_crossing_kit import ROWS, NROW

GHOST_LIMIT = {"mover": 0.25, "static": 0.15}
HAZARD = ("Obst_Log_Trunk", "Obst_HangMat", "Obst_ButtressFin", "Obst_StandingStone", "Obst_WedgedSlab", "Obst_BarrelBoulder",
          "Obst_Furrow", "Obst_ThornCage")  # name prefixes allowed to carry the ochre row (hazard objects only)

def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path, axis_forward="-Z", axis_up="Y")
    ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    me = ob.data; me.calc_loop_triangles()
    P = np.array([(ob.matrix_world @ v.co)[:] for v in me.vertices])
    U = np.stack([P[:, 0], P[:, 2], -P[:, 1]], 1)  # unity xyz
    T = np.array([t.vertices[:] for t in me.loop_triangles])
    uv = me.uv_layers.active.data
    rows = set()
    for t in me.loop_triangles:
        v = uv[t.loops[0]].uv[1]; rows.add(int(v * NROW))
    used = {v for t in T for v in t}
    return U, T, rows, len(me.vertices) - len(used)

def ghost(U, T, hb, step=0.08, disc=False):
    x0, x1, y0, y1, z0, z1 = hb
    xs = np.arange(x0 + step / 2, x1, step); ys = np.arange(y0 + step / 2, y1, step)
    gx, gy = np.meshgrid(xs, ys)
    inside = np.ones(gx.size, bool)
    if disc: inside = ((gx.ravel() / ((x1 - x0) / 2)) ** 2 + ((gy.ravel() - (y0 + y1) / 2) / ((y1 - y0) / 2)) ** 2) <= (0.91 / 0.95) ** 2  # silhouette disc of a cylinder, minus the 0.04 m lump/crack sliver
    O = np.stack([gx.ravel(), gy.ravel(), np.full(gx.size, z0 - 1.0)], 1)
    A, B, C = U[T[:, 0]], U[T[:, 1]], U[T[:, 2]]; e1, e2 = B - A, C - A
    d = np.array([0.0, 0.0, 1.0]); h = np.cross(d, e2); a = np.einsum("ij,ij->i", e1, h)
    ok = np.abs(a) > 1e-10; f = np.where(ok, 1.0 / np.where(ok, a, 1), 0)
    out = np.full(len(O), 0.5)
    for i, o in enumerate(O):
        s = o - A; u = f * np.einsum("ij,ij->i", s, h); q = np.cross(s, e1); v = f * (q @ d)
        t = f * np.einsum("ij,ij->i", e2, q)
        hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 0)
        if hit.any():
            zhit = o[2] + t[hit].min() - z0   # distance behind the front plane (<=0 means a part pokes out in front)
            out[i] = min(0.5, max(0.0, zhit))
    return out, inside

def disc_note(name): return "Barrel" in name

def main():
    d = sys.argv[1]; bad = 0
    print(f"{'piece':22} {'tris':>5}/{'bud':<5} {'ymin':>6} | extents x[min,max] y[min,max] z[min,max] | hitbox | ghost p50/p95/max  >lim")
    for name, (role, hb, budget, note) in w1.SPEC.items():
        U, T, rows, loose = load(os.path.join(d, name + ".fbx"))
        lo, hi = U.min(0), U.max(0); tris = len(T); msgs = []
        if tris > budget: msgs.append("OVER BUDGET")
        if loose: msgs.append(f"{loose} loose verts")
        ochre = ROWS.index("ochre") in rows; ink = ROWS.index("ink") in rows
        if ochre and not name.startswith(HAZARD): msgs.append("RED ON NON-HAZARD")
        if ochre and not ink: msgs.append("RED WITHOUT INK")
        line = f"{name:22} {tris:5d}/{budget:<5d} {lo[1]:6.2f} | x[{lo[0]:6.2f},{hi[0]:6.2f}] y[{lo[1]:5.2f},{hi[1]:5.2f}] z[{lo[2]:5.2f},{hi[2]:5.2f}] |"
        if hb:
            g, inside = ghost(U, T, hb, disc=("Barrel" in name)); lim = GHOST_LIMIT["mover" if "Barrel" in name else "static"]
            cover = [("x-", lo[0] - hb[0]), ("x+", hb[1] - hi[0]), ("z-", lo[2] - hb[4]), ("z+", hb[5] - hi[2])]
            if lo[0] > hb[0] + 0.05 or hi[0] < hb[1] - 0.05: msgs.append("X SHORT")
            if lo[2] > hb[4] + 0.05 or hi[2] < hb[5] - 0.05: msgs.append("Z SHORT")
            if hi[1] < hb[3] - 0.05: msgs.append("TOP SHORT")
            if lo[1] > hb[2] + 0.03: msgs.append("FLOATS")
            line += f" hb x[{hb[0]:5.2f},{hb[1]:5.2f}] y[{hb[2]:3.1f},{hb[3]:3.1f}] z[{hb[4]:5.2f},{hb[5]:5.2f}] | {np.median(g):.3f}/{np.percentile(g, 95):.3f}/{g.max():.3f} {100 * (g > lim).mean():4.1f}%"
            if disc_note(name): line += f" [in-disc: p95 {np.percentile(g[inside], 95):.3f} max {g[inside].max():.3f} {100 * (g[inside] > lim).mean():4.1f}% | corner voids {100 * (~inside).mean():.0f}% of face]"
            g = g[inside]
            if (g > lim).mean() > 0.10: msgs.append(f"GHOST>{lim}")
        else:
            line += f" ({role}) {note}"
        print(line + ("   <-- " + "; ".join(msgs) if msgs else ""))
        bad += any(m not in ("",) and m.split()[0] not in ("GHOST>0.15", "GHOST>0.25") for m in msgs)
    print("HARD FAILURES:", bad)
    sys.exit(1 if bad else 0)

if __name__ == "__main__":
    main()
