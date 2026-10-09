#!/usr/bin/env python3
"""A14 (iPhone 12) GPU-time estimate per budget layer from the offline perf audit.

Input: the draws_<view>.csv tables written by JungleBooze.Editor.Perf.PerfAudit (shaded fragments per draw at the
internal resolution, plus the Metal ALU/sample cost of each draw's shader variant). Model per draw:

    cycles per fragment = max(ALU slots / lanes, texture samples * filter cost / texture units, 1 / pixels per clock)
    time = fragments * cycles per fragment / (cores * clock) / efficiency(category)

Fixed costs (shadow map, vertex/tiling, post-processing, final upscale) are added from the constants below.
The device constants are public figures (see docs/perf/2026-10-iphone12-readiness.md, "GPU model"); the efficiency
factors are calibrated against the M4 run of the same bench player (--calibrate). Rough model: +-30 %.

usage: gpu_estimate.py <audit dir> [--device a14|m4] [--calibrate measured_m4_gpu_ms]
"""
import csv
import os
import sys

DEVICES = {
    # cores, clock GHz (sustained peak), ALU lanes per core, texture units per core, pixels per clock per core
    "a14": dict(cores=4, ghz=1.10, lanes=128, tmu=8, ppc=4, name="Apple A14 (iPhone 12)"),
    "m4": dict(cores=10, ghz=1.60, lanes=128, tmu=8, ppc=4, name="Apple M4 10-core GPU"),
}

# Share of peak reached per category: hidden-surface removal and big triangles for opaque; small alpha-tested
# triangles (2x2 quad overshading, HSR interrupts) for foliage; large overlapping cards for blended.
EFFICIENCY = {"opaque": 0.65, "cutout": 0.45, "blended": 0.60, "sky": 0.70}
# Texture filtering: trilinear/mip-biased reads cost about two bilinear lookups.
FILTER_COST = 1.5
# Per-pixel cost of the sky shader (SkyHdri: one cube sample, exposure) in ALU slots / samples.
SKY_ALU, SKY_TEX = 40, 1

# Fixed costs on A14 in ms at 1.7 MP internal (scaled by device throughput for other devices):
FIXED_A14 = {
    "shadow map (2048, ~58k caster tris, alpha-tested canopy casters)": 0.9,
    "vertex + tiling (~330k tris main view, Pista skinning)": 0.6,
    "bloom (prefilter + 5-level mip chain, half resolution)": 0.7,
    "uber post (ACES, LUT, vignette) + MSAA resolve": 0.5,
    "final upscale blit to 2532x1170 + UI": 0.4,
}


def throughput(dev):
    return dev["cores"] * dev["ghz"] * 1e9


def draw_ms(row, dev):
    frags = float(row["fragments"])
    if frags <= 0:
        return 0.0
    cat = row["category"]
    if cat == "sky":
        alu, tex = SKY_ALU, SKY_TEX
    else:
        alu = float(row["frag_alu_slots"] or 0)
        tex = float(row["frag_samples"] or 0) * FILTER_COST + float(row["frag_shadow_samples"] or 0)
    cycles = max(alu / dev["lanes"], tex / dev["tmu"], 1.0 / dev["ppc"])
    return frags * cycles / throughput(dev) / EFFICIENCY.get(cat, 0.6) * 1000.0


def estimate(path, dev):
    rows = list(csv.DictReader(open(path)))
    by_layer, by_cat = {}, {}
    pixels = None
    for r in rows:
        ms = draw_ms(r, dev)
        by_layer[r["layer"]] = by_layer.get(r["layer"], 0.0) + ms
        by_cat[r["category"]] = by_cat.get(r["category"], 0.0) + ms
        fpp = float(r["frag_per_pixel"])
        if fpp > 0 and pixels is None:
            pixels = float(r["fragments"]) / fpp
        r["ms"] = ms
    scale = throughput(DEVICES["a14"]) / throughput(dev)
    fixed = {k: v * scale for k, v in FIXED_A14.items()}
    return rows, by_layer, by_cat, fixed, pixels


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    audit = sys.argv[1]
    device = "a14"
    if "--device" in sys.argv:
        device = sys.argv[sys.argv.index("--device") + 1]
    dev = DEVICES[device]
    print(f"GPU estimate for {dev['name']}: {dev['cores']} cores x {dev['lanes']} lanes @ {dev['ghz']} GHz")
    for view in ("landscape", "portrait"):
        path = os.path.join(audit, f"draws_{view}.csv")
        if not os.path.exists(path):
            continue
        rows, by_layer, by_cat, fixed, pixels = estimate(path, dev)
        frag_total = sum(by_cat.values())
        fixed_total = sum(fixed.values())
        print(f"\n{view} ({pixels / 1e6:.2f} MP internal)")
        for cat in ("opaque", "cutout", "sky", "blended"):
            print(f"  {cat:8s} {by_cat.get(cat, 0.0):6.2f} ms")
        print("  by layer: " + ", ".join(f"{k} {v:.2f}" for k, v in sorted(by_layer.items())))
        top = sorted(rows, key=lambda r: -r["ms"])[:6]
        print("  top draws: " + "; ".join(f"{r['renderer']} {r['ms']:.2f}" for r in top))
        for k, v in fixed.items():
            print(f"  fixed    {v:6.2f} ms  {k}")
        total = frag_total + fixed_total
        print(f"  TOTAL    {total:6.2f} ms  (fragments {frag_total:.2f} + fixed {fixed_total:.2f}); "
              f"budget 12 ms, 60 fps limit 16.7 ms; sustained after throttling (x1/0.8): {total / 0.8:.2f} ms")
        if "--calibrate" in sys.argv:
            measured = float(sys.argv[sys.argv.index("--calibrate") + 1])
            print(f"  calibration: model {total:.2f} ms vs measured {measured:.2f} ms -> factor {measured / total:.2f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
