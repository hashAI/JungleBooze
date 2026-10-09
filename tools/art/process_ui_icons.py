"""Slices the OpenAI icon sheet (4x3 on flat magenta) into UI icon sprites and draws the small glyphs.

  python3 tools/art/process_ui_icons.py [art_source/ui/openai/icons.png]
    -> UnityProject/Assets/_Game/Art/UI/Icons/<name>.png (256x256, transparent)

Magenta unmix: alpha from how magenta a pixel is, colour unmixed as F = (C - (1 - a) K) / a, then colour bled into
transparent texels so ASTC/mips never show a pink fringe. health_empty is an outline-only version of the leaf heart
(shape, not colour, tells empty from full: colourblind-safe).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'art_source/ui/openai/icons.png')
OUT = os.path.join(ROOT, 'UnityProject/Assets/_Game/Art/UI/Icons')
NAMES = ['coin', 'crystal', 'health', 'shield', 'settings', 'journal', 'abilities', 'discovery', 'record', 'camp',
         'distance', 'lock']
SIZE = 256
PAD = 10
SS = 4


def unmix(rgb):
    im = rgb.astype(np.float32) / 255
    mag = (im[..., 0] > 0.8) & (im[..., 2] > 0.8) & (im[..., 1] < 0.25)
    key = np.median(im[mag], axis=0)
    k0 = min(key[0], key[2]) - key[1]
    m = np.clip((np.minimum(im[..., 0], im[..., 2]) - im[..., 1]) / k0, 0, 1)
    alpha = 1 - m
    alpha = np.where(alpha < 0.04, 0, alpha)
    alpha = np.where(alpha > 0.96, 1, alpha)
    a = np.maximum(alpha, 1e-3)[..., None]
    fg = np.clip((im - (1 - alpha)[..., None] * key) / a, 0, 1)
    # Spill: magenta-tinted edge texels (R and B both above G) pulled toward neutral.
    spill = np.minimum(fg[..., 0], fg[..., 2]) - fg[..., 1]
    fix = (spill > 0.05) & (alpha < 0.9)
    avg = (fg[..., 0] + fg[..., 1] + fg[..., 2]) / 3
    for c in range(3):
        fg[..., c] = np.where(fix, avg * 0.6 + fg[..., c] * 0.4, fg[..., c])
    return fg, alpha


def bleed(rgba):
    """Copies colour into transparent texels from the nearest opaque ones (repeated blur-dilate)."""
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3].astype(np.float32) / 255
    known = a > 0.5
    out = rgb.copy()
    for _ in range(24):
        img = Image.fromarray(np.uint8(out))
        blurred = np.asarray(img.filter(ImageFilter.BoxBlur(2)), np.float32)
        kimg = Image.fromarray(np.uint8(known * 255)).filter(ImageFilter.BoxBlur(2))
        k = np.asarray(kimg, np.float32) / 255
        est = np.where(k[..., None] > 0.01, blurred / np.maximum(k[..., None], 0.01), out)
        out = np.where(known[..., None], rgb, est)
        known = known | (k > 0.01)
    rgba = rgba.copy()
    rgba[..., :3] = np.clip(out, 0, 255).astype(np.uint8)
    return rgba


def fit(rgba):
    a = rgba[..., 3]
    ys, xs = np.where(a > 8)
    crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    img = Image.fromarray(crop, 'RGBA')
    inner = SIZE - 2 * PAD
    s = inner / max(img.width, img.height)
    img = img.resize((max(1, round(img.width * s)), max(1, round(img.height * s))), Image.LANCZOS)
    canvas = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((SIZE - img.width) // 2, (SIZE - img.height) // 2))
    return canvas


def outline_version(img):
    a = np.asarray(img.split()[3], np.float32) / 255
    mask = Image.fromarray(np.uint8(a * 255))
    inner = np.asarray(mask.filter(ImageFilter.MinFilter(19)), np.float32) / 255
    ring = np.clip(a - inner, 0, 1)
    rgba = np.zeros((SIZE, SIZE, 4), np.float32)
    cream = np.array([252, 240, 203], np.float32)
    dark = np.array([10, 31, 28], np.float32)
    # Dark translucent interior + cream ring.
    rgba[..., :3] = np.where(ring[..., None] > 0.02, cream, dark)
    rgba[..., 3] = np.clip(ring * 255 + inner * 0.45 * 255, 0, 255)
    return Image.fromarray(np.uint8(rgba), 'RGBA')


def glyph(name, draw_fn):
    big = Image.new('RGBA', (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    draw_fn(d, SIZE * SS)
    img = big.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))


CREAM = (252, 240, 203, 255)
INK = (40, 26, 8, 255)


def pause(d, s):
    w = s * 0.17
    gap = s * 0.12
    h0, h1 = s * 0.2, s * 0.8
    for x in (s / 2 - gap / 2 - w, s / 2 + gap / 2):
        d.rounded_rectangle([x, h0, x + w, h1], radius=w * 0.35, fill=CREAM, outline=INK, width=int(s * 0.025))


def chevron(points_fn):
    def draw(d, s):
        pts = points_fn(s)
        d.line(pts, fill=INK, width=int(s * 0.16), joint='curve')
        d.line(pts, fill=CREAM, width=int(s * 0.10), joint='curve')
        for p in (pts[0], pts[-1]):
            r = s * 0.05
            d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=CREAM)
    return draw


def close(d, s):
    a, b = s * 0.25, s * 0.75
    for p in ([(a, a), (b, b)], [(a, b), (b, a)]):
        d.line(p, fill=INK, width=int(s * 0.16))
    for p in ([(a, a), (b, b)], [(a, b), (b, a)]):
        d.line(p, fill=CREAM, width=int(s * 0.10))


def play(d, s):
    d.polygon([(s * 0.3, s * 0.18), (s * 0.82, s * 0.5), (s * 0.3, s * 0.82)], fill=CREAM, outline=INK,
              width=int(s * 0.03))


def main():
    os.makedirs(OUT, exist_ok=True)
    sheet = np.asarray(Image.open(SRC).convert('RGB'))
    fg, alpha = unmix(sheet)
    rgba = np.dstack([np.uint8(fg * 255), np.uint8(alpha * 255)])
    h, w = alpha.shape
    cw, ch = w // 4, h // 3
    for i, name in enumerate(NAMES):
        r, c = divmod(i, 4)
        cell = rgba[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw]
        img = fit(bleed(cell))
        img.save(os.path.join(OUT, name + '.png'))
        if name == 'health':
            outline_version(img).save(os.path.join(OUT, 'health_empty.png'))
    glyph('pause', pause)
    glyph('back', chevron(lambda s: [(s * 0.62, s * 0.2), (s * 0.32, s * 0.5), (s * 0.62, s * 0.8)]))
    glyph('next', chevron(lambda s: [(s * 0.38, s * 0.2), (s * 0.68, s * 0.5), (s * 0.38, s * 0.8)]))
    glyph('check', chevron(lambda s: [(s * 0.2, s * 0.52), (s * 0.42, s * 0.74), (s * 0.8, s * 0.28)]))
    glyph('close', close)
    glyph('play', play)
    print('icons written to', OUT)


if __name__ == '__main__':
    main()
