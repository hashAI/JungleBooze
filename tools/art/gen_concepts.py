import json, os, re, sys, base64, urllib.request, uuid, time
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MD = os.path.join(ROOT, 'design/prompts/aurelia/pista.md')
OUT = os.path.join(ROOT, 'design/concepts/2026-10-08')
os.makedirs(OUT, exist_ok=True)
txt = open(MD).read()
def block(token):
    m = re.search(r'\{' + token + r'\}[^\n]*\n```\n(.*?)\n```', txt, re.S)
    if not m: sys.exit('missing ' + token)
    return m.group(1).strip()
def prompt(fmt, take):
    subj = block('SUBJECT').replace('{LOOK}', block('LOOK'))
    return ' '.join([subj, block('STYLE'), block(fmt), block('TAKE_' + take), block('AVOID')])
KEY = os.environ['OPENAI_API_KEY']
# Draft cheaply, finalize only the picked take: QUALITY=low|medium|high, SIZE=WxH, FMT=jpeg|png
QUALITY = os.environ.get('QUALITY', 'medium')
SIZE = os.environ.get('SIZE', '1536x1024')
FMT = os.environ.get('FMT', 'jpeg')
def gen(p, size):
    body = json.dumps({"model": "gpt-image-2", "prompt": p, "size": size, "quality": QUALITY, "n": 1, "output_format": FMT}).encode()
    r = urllib.request.Request('https://api.openai.com/v1/images/generations', data=body,
        headers={'Authorization': 'Bearer ' + KEY, 'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(r, timeout=600))
def edit(p, size, ref):
    b = uuid.uuid4().hex
    parts = []
    for k, v in [('model', 'gpt-image-2'), ('prompt', p), ('size', size), ('quality', QUALITY), ('n', '1'), ('output_format', FMT)]:
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    parts.append(f'--{b}\r\nContent-Disposition: form-data; name="image[]"; filename="ref.jpg"\r\nContent-Type: image/jpeg\r\n\r\n'.encode() + open(ref, 'rb').read() + b'\r\n')
    parts.append(f'--{b}--\r\n'.encode())
    r = urllib.request.Request('https://api.openai.com/v1/images/edits', data=b''.join(parts),
        headers={'Authorization': 'Bearer ' + KEY, 'Content-Type': 'multipart/form-data; boundary=' + b})
    return json.load(urllib.request.urlopen(r, timeout=600))
kind, take = sys.argv[1], sys.argv[2]
suffix = sys.argv[3] if len(sys.argv) > 3 else ''
extra = sys.argv[4] if len(sys.argv) > 4 else ''
ext = 'jpg' if os.environ.get('FMT', 'jpeg') == 'jpeg' else 'png'
name = f'pista_real_take{take}_{kind}{suffix}.{ext}'
try:
    if kind == 'turnaround':
        p = prompt('FORMAT_TURNAROUND', take)
        if extra: p = p.replace(' Avoid:', ' ' + extra + ' Avoid:')
        res = gen(p, SIZE)
    elif kind == 'final':
        # Clean high-quality turnaround of the picked take, using its draft as the reference
        p = 'Use the girl from the reference image exactly: same face, hair, body and outfit. ' + prompt('FORMAT_TURNAROUND', take)
        if extra: p = p.replace(' Avoid:', ' ' + extra + ' Avoid:')
        res = edit(p, SIZE, os.path.join(OUT, f'pista_real_take{take}_turnaround.jpg'))
    else:
        p = prompt('FORMAT_KEYART', take)
        if extra: p = p.replace(' Avoid:', ' ' + extra + ' Avoid:')
        ref = [f for f in sorted(os.listdir(OUT)) if f.startswith(f'pista_real_take{take}_' + os.environ.get('REF', 'turnaround')) and not f.endswith('.txt')][0]
        res = edit(p, SIZE, os.path.join(OUT, ref))
except urllib.error.HTTPError as e:
    print('HTTP', e.code, e.read().decode()[:800]); sys.exit(1)
open(os.path.join(OUT, name), 'wb').write(base64.b64decode(res['data'][0]['b64_json']))
open(os.path.join(OUT, name + '.prompt.txt'), 'w').write(p)
print('ok', name, res.get('usage', {}))
