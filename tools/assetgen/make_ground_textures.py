#!/usr/bin/env python3
"""Procedural, seamlessly tiling ground textures for the run (Inkbound Pulp: flat colors, hard-edged bands, ink accents).
No generation service, no third-party art: everything is drawn here from periodic noise, so it tiles exactly.

    python3 make_ground_textures.py <out_dir>

Writes (into Resources/EnvironmentArt, loaded by GroundView):
  Ground_Trail_basecolor.png      1024x1024. U spans the trail cross-section x in [-TRAIL_HALF, +TRAIL_HALF] m
                                  (does NOT tile in U), V tiles every TRAIL_PERIOD m along the run.
                                  Dirt is near-white because the material tint is the world theme's Path color
                                  (cream in the Jungle); greens are pre-divided by the cream so they land on target.
  Ground_JungleFloor_basecolor.png 512x512, tiles in U and V every FLOOR_PERIOD m; tinted by the theme's Verge color.
Keep TRAIL_HALF / TRAIL_PERIOD / FLOOR_PERIOD in sync with EnvironmentLookConfig (C#) and tools/blender/run_mock.py."""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

TRAIL_HALF = 5.4      # m, half width of the trail mesh (path half width 4.2 + 1.2 m grassy edge)
PATH_HALF = 4.2       # m, 3 lanes x 2.4 + 2 x 0.6
LANES = (-2.4, 0.0, 2.4)
TRAIL_PERIOD = 6.0    # m along Z per texture repeat
FLOOR_PERIOD = 6.0    # m per repeat on the verges
CREAM = np.array([0xF3, 0xDF, 0xB2]) / 255.0
FLOOR_TINT = np.array([0x2A, 0x66, 0x36]) / 255.0  # StylePalette.JungleFloor
INK = np.array([0x1E, 0x1A, 0x24]) / 255.0


def periodic_noise(h, w, rng, octaves=((3, 1.0), (6, 0.5), (12, 0.25), (24, 0.12)), wrap_u=True):
    """Sum of random-phase cosines with integer frequencies: exactly periodic over the image in both axes."""
    y = np.arange(h)[:, None] / h
    x = np.arange(w)[None, :] / w
    out = np.zeros((h, w))
    for f, amp in octaves:
        for _ in range(6):
            fx = rng.integers(-f, f + 1)
            fy = rng.integers(-f, f + 1)
            if fx == 0 and fy == 0:
                continue
            ph = rng.random() * 2 * np.pi
            out += amp * np.cos(2 * np.pi * (fx * x + fy * y) + ph)
    out -= out.mean()
    return out / (np.abs(out).max() + 1e-9)


def stamp_pebbles(img, rng, count, rmin, rmax, colors, region=None, outline=2.0, wrap=(True, True)):
    """Flat-colored pebbles with an ink rim and a hard light band (two-tone), wrapped so the tile stays seamless."""
    h, w, _ = img.shape
    yy, xx = np.mgrid[0:h, 0:w]
    for _ in range(count):
        cx, cy = rng.random() * w, rng.random() * h
        if region is not None and not region(cx / w, cy / h):
            continue
        rx = rmin + rng.random() * (rmax - rmin)
        ry = rx * (0.55 + rng.random() * 0.35)
        a = rng.random() * np.pi
        col = colors[rng.integers(len(colors))]
        dx = xx - cx
        dy = yy - cy
        if wrap[0]:
            dx = (dx + w / 2) % w - w / 2
        if wrap[1]:
            dy = (dy + h / 2) % h - h / 2
        u = (dx * np.cos(a) + dy * np.sin(a)) / rx
        v = (-dx * np.sin(a) + dy * np.cos(a)) / ry
        d = np.sqrt(u * u + v * v)
        rim = d <= 1.0 + outline / rx
        body = d <= 1.0
        img[rim] = img[rim] * 0.0 + INK * 0.9 + img[rim] * 0.1
        img[body] = col
        lit = body & (v < -0.25) & (d < 0.8)
        img[lit] = np.minimum(1.0, col * 1.12)


def trail(size=1024, seed=7):
    rng = np.random.default_rng(seed)
    h = w = size
    x_m = (np.arange(w) + 0.5) / w * (2 * TRAIL_HALF) - TRAIL_HALF  # m across
    X = np.broadcast_to(x_m[None, :], (h, w))
    img = np.ones((h, w, 3))
    n1 = periodic_noise(h, w, rng)
    n2 = periodic_noise(h, w, rng, octaves=((8, 1.0), (16, 0.5), (32, 0.25)))

    # Sand bands: two hard-edged darker tones (no gradients, style guide 3.2).
    band1 = n1 > 0.18
    band2 = n1 > 0.48
    img[band1] = np.array([0.95, 0.92, 0.88])
    img[band2] = np.array([0.89, 0.85, 0.80])

    # Worn foot tracks down each lane centre: slightly smoother, lighter sand with wavy hard edges.
    for lx in LANES:
        edge = 0.42 + 0.1 * n2
        track = np.abs(X - lx) < edge
        img[track & ~band2 & ~band1] = np.array([0.985, 0.97, 0.95])
        img[track & band2] = np.array([0.91, 0.86, 0.79])

    # Fine speckle (grit) in hard dots.
    grit = periodic_noise(h, w, rng, octaves=((64, 1.0), (96, 0.8)))
    img[grit > 0.62] *= 0.90

    # Pebbles: more toward the edges, few in the lanes so coins and obstacles stay clean.
    peb = [np.array(c) / 255.0 / CREAM.clip(0.5) for c in ((0xB9, 0xAE, 0x9C), (0xA6, 0x98, 0x86), (0xC9, 0xBC, 0xA2))]
    peb = [np.clip(p, 0, 1) for p in peb]
    stamp_pebbles(img, rng, 220, 3, 9, peb, region=lambda u, v: abs(u * 2 * TRAIL_HALF - TRAIL_HALF) > 3.0, wrap=(False, True))
    stamp_pebbles(img, rng, 70, 2, 6, peb, region=lambda u, v: abs(u * 2 * TRAIL_HALF - TRAIL_HALF) <= 3.0, wrap=(False, True))


    # Grassy edge: dirt darkens (damp soil), then a jagged grass line with blade tips pointing inward,
    # then solid floor green that matches the verge (FLOOR_TINT x 0.85 floor base), all pre-divided by cream.
    rim_noise = periodic_noise(h, w, rng, octaves=((5, 1.0), (11, 0.6), (40, 0.35)))
    ax = np.abs(X)
    soil = (ax > PATH_HALF - 0.35 + 0.12 * rim_noise)
    img[soil] = img[soil] * np.array([0.84, 0.78, 0.70])
    # Blades: sawtooth along V gives pointed tufts; height varies with noise.
    v_m = (np.arange(h)[:, None] + 0.5) / h * TRAIL_PERIOD
    # Irregular blades: 42 per period (tiles), each with its own height and lean; a second, coarser row of tufts.
    blades = 42
    heights = rng.random(blades) * 0.22 + 0.08
    leans = (rng.random(blades) - 0.5) * 0.5
    t = v_m / TRAIL_PERIOD * blades + 0.25 * rim_noise
    k = np.floor(t).astype(int) % blades
    f = t - np.floor(t)
    saw = np.abs(f - 0.5 - leans[k] * (1 - np.abs(f - 0.5) * 2)) * 2.0  # 0 at the (leaning) tip, 1 between blades
    tufts = 9
    th = rng.random(tufts) * 0.25
    t2 = v_m / TRAIL_PERIOD * tufts + 0.15 * rim_noise
    k2 = np.floor(t2).astype(int) % tufts
    bump = np.sin(np.pi * (t2 - np.floor(t2))) * th[k2]
    grass_line = PATH_HALF + 0.02 + 0.14 * rim_noise - bump + (heights[k] + 0.06) * np.minimum(1.0, saw * 1.3)
    grass = ax > grass_line
    ink_line = (ax > grass_line - 0.035) & ~grass
    lit_green = np.array([0x4E, 0x9A, 0x3E]) / 255.0 / CREAM
    mid_green = np.array([0x3A, 0x7F, 0x3A]) / 255.0 / CREAM
    floor = FLOOR_TINT * 0.85 / CREAM
    img[ink_line] = INK / CREAM * 1.6
    img[grass] = lit_green
    img[grass & (ax > grass_line + 0.25)] = mid_green
    img[ax > PATH_HALF + 0.75 + 0.15 * rim_noise] = floor
    return Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8))


def jungle_floor(size=512, seed=11):
    """Near-white base x theme Verge tint: leaf litter in two darker flat tones plus a few light leaf glints."""
    rng = np.random.default_rng(seed)
    h = w = size
    img = np.full((h, w, 3), 0.85)
    n = periodic_noise(h, w, rng, octaves=((4, 1.0), (9, 0.6), (18, 0.3)))
    img[n > 0.25] = 0.74
    img[n > 0.55] = 0.62
    img[n < -0.45] = 0.92
    leaf_cols = [np.array([0.70, 0.66, 0.60]), np.array([0.98, 0.95, 0.80]), np.array([0.60, 0.62, 0.58])]
    stamp_pebbles(img, rng, 160, 3, 8, leaf_cols, outline=1.0)
    return Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8))


if __name__ == "__main__":
    out = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
    out.mkdir(parents=True, exist_ok=True)
    trail().save(out / "Ground_Trail_basecolor.png", optimize=True)
    jungle_floor().save(out / "Ground_JungleFloor_basecolor.png", optimize=True)
    print("wrote", out / "Ground_Trail_basecolor.png", out / "Ground_JungleFloor_basecolor.png")
