#!/usr/bin/env python3
"""Contact sheet for the obstacle wave 1 GLBs from the HERO's side (software renderer env_preview.py, no GPU).
Usage: obstacles_sheet.py <out.png> <glb> [glb ...] [--cols N] [--size PX]
Cells: front 3/4 view (hero approaching from -z, yaw 215) and head-on front (yaw 180), pieces in Unity axes (+z = path direction).
env_preview.py's own views look from +z (the rear), which hides the red-ochre marks on the lethal front face."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).parent))
import numpy as np
import env_preview as ep

def main():
    a = sys.argv[1:]; cols = int(a[a.index("--cols") + 1]) if "--cols" in a else 2; size = int(a[a.index("--size") + 1]) if "--size" in a else 320
    for flag in ("--cols", "--size"):
        if flag in a: i = a.index(flag); del a[i:i + 2]
    out, paths = a[0], a[1:]; cells = []
    for p in paths:
        m, tex = ep.load(p)
        l, r = ep.render(m, tex, 215, 22, size), ep.render(m, tex, 180, 4, size)
        cell = Image.new("RGB", (size * 2, size + 22), (226, 226, 226)); cell.paste(l, (0, 22)); cell.paste(r, (size, 22))
        ImageDraw.Draw(cell).text((6, 5), f"{Path(p).stem}  {len(m.faces)} tris  size {np.round(m.extents, 2).tolist()} (x,y,z)", fill=(20, 20, 20))
        cells.append(cell)
    rows = (len(cells) + cols - 1) // cols; w, h = cells[0].size
    S = Image.new("RGB", (w * cols, h * rows), (255, 255, 255))
    for i, c in enumerate(cells): S.paste(c, ((i % cols) * w, (i // cols) * h))
    S.save(out)

if __name__ == "__main__":
    main()
