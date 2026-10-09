"""OpenAI (gpt-image-2, medium) generation for the AURELIA UI: icon sheet and home backdrops.

Usage (key from ~/.config/junglebooze/secrets.env):
  python3 tools/art/gen_ui_art.py icons            -> art_source/ui/openai/icons_sheet.png (1536x1024, transparent)
  python3 tools/art/gen_ui_art.py home_portrait    -> art_source/ui/openai/home_portrait.jpg (1024x1536, edit of F4_f)
  python3 tools/art/gen_ui_art.py home_landscape   -> art_source/ui/openai/home_landscape.jpg (1536x1024, edit of F4_f)
Every call is appended to art_source/environment/openai/calls.jsonl (owner rule).
"""
import base64
import json
import os
import sys
import urllib.error
import urllib.request
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'art_source/ui/openai')
LOG = os.path.join(ROOT, 'art_source/environment/openai/calls.jsonl')
REF = os.path.join(ROOT, 'design/aurelia/keyframes/F4_f_painterly_openai.jpg')
os.makedirs(OUT, exist_ok=True)


def key():
    k = os.environ.get('OPENAI_API_KEY')
    if k:
        return k
    for line in open(os.path.expanduser('~/.config/junglebooze/secrets.env')):
        line = line.strip()
        if line.startswith('export '):
            line = line[7:]
        if line.startswith('OPENAI_API_KEY='):
            return line.split('=', 1)[1].strip().strip('"').strip("'")
    sys.exit('OPENAI_API_KEY missing')


ICONS = (
    "A sprite sheet of 12 matching mobile game UI icons for a painterly jungle exploration game, arranged in a precise "
    "grid of 4 columns and 3 rows, each icon centered in its own equal cell with wide empty transparent padding around "
    "it, no icon touching another, no grid lines, no text, no letters, no numbers. Style: hand-painted premium "
    "stylized game icons, bold simple readable silhouettes, soft painted shading, warm golden rim light from the top "
    "left, a thin dark warm-brown outline around each icon, saturated warm gold, emerald green and turquoise palette, "
    "readable at 32 pixels. Row 1: (1) a round gold coin with a turquoise gem in its center, seen from the front; "
    "(2) a faceted turquoise-cyan crystal shard with a bright highlight; (3) a heart shape made of a single glossy "
    "emerald leaf; (4) a round translucent turquoise shield bubble with a gold rim. Row 2: (5) a gold cog gear; "
    "(6) a leather-bound explorer journal book with a gold leaf emblem on the cover; (7) a gold compass rose star "
    "with an emerald center gem (skills/abilities); (8) a sparkling magnifying glass over a leaf (discovery). "
    "Row 3: (9) a golden laurel wreath trophy cup (new record); (10) a canvas expedition camp tent with a small flag "
    "(home); (11) a pair of small boot footprints with a dotted trail (distance); (12) a gold padlock (locked). "
    "The whole background is one flat solid pure magenta color (#FF00FF), no gradient, no shadow on the background; "
    "no magenta or pink inside any icon. Every icon fully inside its cell."
)

HOME_PORTRAIT = (
    "Repaint this exact painterly jungle scene as a tall vertical mobile game title-screen backdrop: same painterly "
    "stylized-realistic style, same warm golden sunlight, same braided sandstone root arch, same tall waterfall and "
    "turquoise stepped pools, lush emerald foliage and orange bell-flowers. Recompose for portrait: the arch and the "
    "waterfall fill the middle of the frame; the upper quarter is calmer golden sky and soft haze (room for a title "
    "logo); the lower quarter is a darker, simple mossy path and foliage foreground (room for buttons). The young "
    "explorer girl with a ponytail and backpack stands small on the path in the lower middle, seen from behind, "
    "looking at the falls. Important composition: she stands on a mossy rock ledge at the middle-left of the frame, "
    "at about 52-62% of the image height, never in the bottom third. The bottom 30% of the frame is a simple, darker "
    "foreground of large leaves and moss with no figure in it. No text, no logo, no UI, no frame."
)

HOME_LANDSCAPE = (
    "Repaint this exact painterly jungle scene as a wide mobile game title-screen backdrop: same painterly "
    "stylized-realistic style, same warm golden sunlight, braided sandstone root arch, tall waterfall, turquoise "
    "stepped pools, lush emerald foliage and orange bell-flowers. Recompose: the arch and waterfall sit in the right "
    "two thirds; the left third is calmer, softer golden haze and sunlit foliage with low detail (room for a title "
    "logo and a menu); the bottom edge is a darker, simple mossy foreground. The young explorer girl with a ponytail "
    "and backpack stands on a rocky ledge in the right third of the frame (around 70-80% of the width), seen from "
    "behind, looking at the falls. The left 45% of the frame has no figure. No text, no logo, no UI, no frame."
)


def post_json(url, body):
    req = urllib.request.Request(url, data=json.dumps(body).encode(), headers={
        'Authorization': 'Bearer ' + key(), 'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(req, timeout=600))


def post_edit(prompt, size, fmt, ref):
    b = uuid.uuid4().hex
    parts = []
    for k, v in [('model', 'gpt-image-2'), ('prompt', prompt), ('size', size), ('quality', 'medium'), ('n', '1'),
                 ('output_format', fmt)]:
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    parts.append(f'--{b}\r\nContent-Disposition: form-data; name="image[]"; filename="ref.jpg"\r\n'
                 f'Content-Type: image/jpeg\r\n\r\n'.encode() + open(ref, 'rb').read() + b'\r\n')
    parts.append(f'--{b}--\r\n'.encode())
    req = urllib.request.Request('https://api.openai.com/v1/images/edits', data=b''.join(parts), headers={
        'Authorization': 'Bearer ' + key(), 'Content-Type': 'multipart/form-data; boundary=' + b})
    return json.load(urllib.request.urlopen(req, timeout=600))


def log(entry):
    with open(LOG, 'a') as f:
        f.write(json.dumps(entry) + '\n')


def main():
    kind = sys.argv[1]
    suffix = sys.argv[2] if len(sys.argv) > 2 else ''
    try:
        if kind == 'icons':
            size, fmt, mode = '1536x1024', 'png', 'generate'
            res = post_json('https://api.openai.com/v1/images/generations', {
                'model': 'gpt-image-2', 'prompt': ICONS, 'size': size, 'quality': 'medium', 'n': 1,
                'output_format': fmt})
            prompt = ICONS
        elif kind == 'home_portrait':
            size, fmt, mode, prompt = '1024x1536', 'jpeg', 'edit', HOME_PORTRAIT
            res = post_edit(prompt, size, fmt, REF)
        elif kind == 'home_landscape':
            size, fmt, mode, prompt = '1536x1024', 'jpeg', 'edit', HOME_LANDSCAPE
            res = post_edit(prompt, size, fmt, REF)
        else:
            sys.exit('unknown kind ' + kind)
    except urllib.error.HTTPError as e:
        print('HTTP', e.code, e.read().decode()[:800])
        sys.exit(1)
    ext = 'png' if fmt == 'png' else 'jpg'
    name = f'{kind}{suffix}.{ext}'
    path = os.path.join(OUT, name)
    open(path, 'wb').write(base64.b64decode(res['data'][0]['b64_json']))
    open(path + '.prompt.txt', 'w').write(prompt)
    log({'date': '2026-10-09', 'model': 'gpt-image-2', 'quality': 'medium', 'size': size, 'mode': mode,
         'file': os.path.relpath(path, ROOT), 'purpose': 'ui ' + kind, 'est_usd': 0.073 if mode == 'generate' else 0.09})
    print('ok', name, res.get('usage', {}))


if __name__ == '__main__':
    main()
