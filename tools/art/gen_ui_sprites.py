"""Procedural painterly UI sprites for AURELIA (9-slice panels, buttons, chips, sliders, glows).

  python3 tools/art/gen_ui_sprites.py   -> UnityProject/Assets/_Game/Art/UI/Sprites/*.png

Shapes are drawn at 4x and downsampled (clean anti-aliased edges). A soft low-frequency "brush" mottling (<= 6%
contrast, ART_DIRECTION_PAINTERLY s3 noise budget) keeps fills from looking flat-vector. Palette: warm gold, emerald,
turquoise (ART_DIRECTION_PAINTERLY s8). The 9-slice borders are listed in BORDERS (pixels), read by the Unity
importer (UiArtImporter).
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'UnityProject/Assets/_Game/Art/UI/Sprites')
os.makedirs(OUT, exist_ok=True)
SS = 4
RNG = np.random.default_rng(7)
BORDERS = {}


def hexc(h, a=255):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mottling(w, h, scale, amount):
    """Low-frequency brush mottling in [-amount, amount]."""
    small = RNG.random((max(2, h // scale), max(2, w // scale)))
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    img = img.filter(ImageFilter.GaussianBlur(scale / 3))
    a = np.asarray(img).astype(np.float32) / 255.0
    return (a - 0.5) * 2 * amount


def vgrad(w, h, top, bottom):
    t = np.linspace(0, 1, h, dtype=np.float32)[:, None, None]
    top = np.array(top, np.float32)[None, None, :]
    bottom = np.array(bottom, np.float32)[None, None, :]
    return np.broadcast_to(top * (1 - t) + bottom * t, (h, w, 4)).copy()


def rr_mask(w, h, r, inset=0):
    m = Image.new('L', (w * SS, h * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1],
                                        radius=max(0, (r - inset)) * SS, fill=255)
    return m.resize((w, h), Image.LANCZOS)


def ellipse_mask(w, h, inset=0):
    m = Image.new('L', (w * SS, h * SS), 0)
    ImageDraw.Draw(m).ellipse([inset * SS, inset * SS, (w - inset) * SS - 1, (h - inset) * SS - 1], fill=255)
    return m.resize((w, h), Image.LANCZOS)


def premul_layers(layers, w, h):
    acc_rgb = np.zeros((h, w, 3), np.float32)
    acc_a = np.zeros((h, w, 1), np.float32)
    for rgba, mask in layers:
        a = rgba[..., 3:4] / 255.0
        if mask is not None:
            a = a * (np.asarray(mask, np.float32)[..., None] / 255.0)
        acc_rgb = rgba[..., :3] * a + acc_rgb * (1 - a)
        acc_a = a + acc_a * (1 - a)
    rgb = np.where(acc_a > 1e-4, acc_rgb / np.maximum(acc_a, 1e-4), 0)
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(acc_a * 255, 0, 255)]).astype(np.uint8), 'RGBA')


def save(name, img, border):
    img.save(os.path.join(OUT, name + '.png'))
    BORDERS[name] = border


def rounded_panel(name, w, h, r, top, bottom, rim, rim_w, inner_hi, mott=0.05, border=None):
    fill = vgrad(w, h, top, bottom)
    fill[..., :3] *= (1 + mottling(w, h, 24, mott))[..., None]
    rim_layer = np.broadcast_to(np.array(rim, np.float32), (h, w, 4)).copy()
    hi = vgrad(w, h, inner_hi, (inner_hi[0], inner_hi[1], inner_hi[2], 0))
    hi_mask = rr_mask(w, h, r, rim_w + 1)
    # Highlight only on the top ~40%.
    fade = np.clip(1 - np.linspace(0, 1, h) / 0.45, 0, 1)[:, None]
    hi[..., 3] *= fade
    layers = [(rim_layer, rr_mask(w, h, r)), (fill, rr_mask(w, h, r, rim_w)), (hi, hi_mask)]
    save(name, premul_layers(layers, w, h), border or r + rim_w + 2)


def main():
    # Main panel: deep emerald-teal glass with a warm gold rim (vision board UI panels, painterly palette).
    rounded_panel('panel', 128, 128, 30, hexc('#1B4A42', 240), hexc('#0D2A26', 240), hexc('#E2BE6E'), 3,
                  hexc('#FFE7AE', 40), 0.06, 36)
    # Parchment card: toast, objective, ability card.
    rounded_panel('card', 128, 128, 24, hexc('#FBF0D2'), hexc('#EED8A6'), hexc('#B98A3E'), 3, hexc('#FFFFFF', 90), 0.04, 30)
    # Primary button: warm gold (dark ink label).
    rounded_panel('button_primary', 128, 96, 30, hexc('#FFE08A'), hexc('#DE9C33'), hexc('#7A4B12'), 3,
                  hexc('#FFFFFF', 120), 0.03, 34)
    # Secondary button: turquoise (cream label).
    rounded_panel('button_secondary', 128, 96, 30, hexc('#27877A'), hexc('#154F48'), hexc('#E2BE6E'), 2,
                  hexc('#BFF5EA', 60), 0.04, 34)
    # HUD chip: dark translucent pill with a thin gold rim (reads on bright sky and dark foliage).
    rounded_panel('chip', 96, 96, 40, hexc('#0E2A26', 200), hexc('#0A1F1C', 215), hexc('#E2BE6E', 230), 2,
                  hexc('#FFE7AE', 26), 0.03, 44)
    # Round icon button (pause, close).
    w = h = 128
    rim = np.broadcast_to(np.array(hexc('#E2BE6E'), np.float32), (h, w, 4)).copy()
    fill = vgrad(w, h, hexc('#16413A', 215), hexc('#0A1F1C', 225))
    save('circle', premul_layers([(rim, ellipse_mask(w, h)), (fill, ellipse_mask(w, h, 4))], w, h), 0)
    # Slider / toggle track and fill, knob.
    rounded_panel('track', 64, 32, 16, hexc('#0A1F1C', 230), hexc('#12342F', 230), hexc('#8C7543'), 2,
                  hexc('#000000', 0), 0.0, 15)
    rounded_panel('track_fill', 64, 32, 16, hexc('#7FE0CC'), hexc('#2E9A8A'), hexc('#E2BE6E'), 2,
                  hexc('#FFFFFF', 80), 0.0, 15)
    w = h = 96
    rim = np.broadcast_to(np.array(hexc('#7A4B12'), np.float32), (h, w, 4)).copy()
    fill = vgrad(w, h, hexc('#FFF1C2'), hexc('#E4AE4A'))
    save('knob', premul_layers([(rim, ellipse_mask(w, h, 2)), (fill, ellipse_mask(w, h, 6))], w, h), 0)
    # Soft radial glow (NEW RECORD, primary button halo, toast), white so it can be tinted.
    w = h = 128
    y, x = np.mgrid[0:h, 0:w]
    d = np.sqrt(((x - w / 2 + 0.5) / (w / 2)) ** 2 + ((y - h / 2 + 0.5) / (h / 2)) ** 2)
    a = np.clip(1 - d, 0, 1) ** 2.2
    img = np.dstack([np.full((h, w), 255), np.full((h, w), 255), np.full((h, w), 255), a * 255]).astype(np.uint8)
    save('glow', Image.fromarray(img, 'RGBA'), 0)
    # Soft shadow for panels (black, blurred rounded rect; 9-slice).
    w = h = 128
    m = rr_mask(w, h, 30, 24).filter(ImageFilter.GaussianBlur(12))
    a = np.asarray(m, np.float32) * 0.38
    img = np.dstack([np.zeros((h, w)), np.zeros((h, w)), np.zeros((h, w)), a]).astype(np.uint8)
    save('shadow', Image.fromarray(img, 'RGBA'), 56)
    # Edge scrims: vertical fade (black at the top edge to clear) so HUD text reads on bright sky.
    w, h = 8, 128
    a = (np.linspace(1, 0, h) ** 1.6)[:, None] * np.ones((1, w))
    img = np.dstack([np.zeros((h, w)), np.zeros((h, w)), np.zeros((h, w)), a * 255]).astype(np.uint8)
    save('scrim', Image.fromarray(img, 'RGBA'), 0)
    # Ornamental divider: gold line fading at both ends with a small diamond in the middle.
    w, h = 256, 16
    img = Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    for i in range(w * SS):
        t = abs(i / (w * SS) - 0.5) * 2
        alpha = int(255 * max(0.0, 1 - t ** 2))
        dr.line([(i, h * SS / 2 - SS * 0.75), (i, h * SS / 2 + SS * 0.75)], fill=(226, 190, 110, alpha))
    c = (w * SS / 2, h * SS / 2)
    s = 5 * SS
    dr.polygon([(c[0], c[1] - s), (c[0] + s, c[1]), (c[0], c[1] + s), (c[0] - s, c[1])], fill=(255, 224, 138, 255),
               outline=(122, 75, 18, 255))
    save('divider', img.resize((w, h), Image.LANCZOS), 0)
    # Plain white rounded rect (tintable progress fills, focus rings).
    rounded_panel('white', 64, 64, 14, hexc('#FFFFFF'), hexc('#FFFFFF'), hexc('#FFFFFF'), 0, hexc('#FFFFFF', 0), 0.0, 16)
    with open(os.path.join(OUT, 'borders.json'), 'w') as f:
        json.dump(BORDERS, f, indent=1, sort_keys=True)
    print('wrote', len(BORDERS), 'sprites to', OUT)


if __name__ == '__main__':
    main()
