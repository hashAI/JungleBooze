"""Sky dome and far canopy rings, built exactly like SkyView.cs builds them (keep the two in sync).
Vertices are in Unity space relative to the camera (x right, y up, z forward); colors are sRGB tuples.
Used by run_mock.py."""
import math

DOME_RADIUS = 150.0
DOME_SEGMENTS = 24
# (elevation deg, color key, mix toward the next key) ; keys: fog, horizon, top
DOME_RINGS = [(-40.0, "fog"), (0.0, "fog"), (3.0, "horizon"), (9.0, "low"), (18.0, "high"), (30.0, "top"), (90.0, "top")]
RING_SEGMENTS = 384
RINGS = [
    # radius, base height above the camera, bump amplitudes, phase seed, haze (0 = shadow tint, 1 = fog)
    (135.0, 4.0, (3.0, 2.0, 1.6, 1.1), 0.7, 0.72),
    (118.0, 0.5, (2.4, 1.6, 1.4, 1.0), 2.9, 0.55),
]
RING_BOTTOM = -30.0
FREQS = (5, 13, 23, 37)  # low hills + rounded tree crowns (|sin| bumps)


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def dome_colors(th):
    return {"fog": th["fog"], "horizon": th["horizon"], "low": lerp(th["horizon"], th["top"], 0.35),
            "high": lerp(th["horizon"], th["top"], 0.75), "top": th["top"]}


def ring_height(base, amps, phase, a):
    h = base
    for f, amp in zip(FREQS, amps):
        h += amp * abs(math.sin(f * a + phase * f))
    return h


def build(th):
    out = {}
    cols = dome_colors(th)
    verts, faces, vc = [], [], []
    for (el, key) in DOME_RINGS:
        e = math.radians(el)
        for s in range(DOME_SEGMENTS):
            a = 2 * math.pi * s / DOME_SEGMENTS
            verts.append((math.cos(e) * math.sin(a) * DOME_RADIUS, math.sin(e) * DOME_RADIUS, math.cos(e) * math.cos(a) * DOME_RADIUS))
            vc.append(cols[key])
    n = DOME_SEGMENTS
    for r in range(len(DOME_RINGS) - 1):
        for s in range(n):
            a, b = r * n + s, r * n + (s + 1) % n
            faces.append((a, b, b + n, a + n))
    out["SkyDome"] = (verts, faces, vc)

    verts, faces, vc = [], [], []
    for (radius, base, amps, phase, haze) in RINGS:
        top_col = lerp(th["shadow"], th["fog"], haze)
        mid_col = lerp(top_col, th["fog"], 0.6)
        start = len(verts)
        for s in range(RING_SEGMENTS):
            a = 2 * math.pi * s / RING_SEGMENTS
            x, z = math.sin(a) * radius, math.cos(a) * radius
            verts.append((x, ring_height(base, amps, phase, a), z)); vc.append(top_col)
            verts.append((x, 0.0, z)); vc.append(mid_col)
            verts.append((x, RING_BOTTOM, z)); vc.append(th["fog"])
        for s in range(RING_SEGMENTS):
            a, b = start + s * 3, start + ((s + 1) % RING_SEGMENTS) * 3
            faces.append((a, b, b + 1, a + 1))
            faces.append((a + 1, b + 1, b + 2, a + 2))
    out["FarCanopy"] = (verts, faces, vc)
    return out
