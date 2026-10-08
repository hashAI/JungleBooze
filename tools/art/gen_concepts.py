import json, os, re, sys, base64, urllib.request, uuid, time
MD = '/home/user/JungleBooze/design/prompts/aurelia/pista.md'
OUT = '/home/user/JungleBooze/design/concepts/2026-10-08'
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
def gen(p, size):
    body = json.dumps({"model": "gpt-image-2", "prompt": p, "size": size, "quality": "high", "n": 1}).encode()
    r = urllib.request.Request('https://api.openai.com/v1/images/generations', data=body,
        headers={'Authorization': 'Bearer ' + KEY, 'Content-Type': 'application/json'})
    return json.load(urllib.request.urlopen(r, timeout=600))
def edit(p, size, ref):
    b = uuid.uuid4().hex
    parts = []
    for k, v in [('model', 'gpt-image-2'), ('prompt', p), ('size', size), ('quality', 'high'), ('n', '1')]:
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    parts.append(f'--{b}\r\nContent-Disposition: form-data; name="image[]"; filename="ref.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + open(ref, 'rb').read() + b'\r\n')
    parts.append(f'--{b}--\r\n'.encode())
    r = urllib.request.Request('https://api.openai.com/v1/images/edits', data=b''.join(parts),
        headers={'Authorization': 'Bearer ' + KEY, 'Content-Type': 'multipart/form-data; boundary=' + b})
    return json.load(urllib.request.urlopen(r, timeout=600))
kind, take = sys.argv[1], sys.argv[2]
suffix = sys.argv[3] if len(sys.argv) > 3 else ''
extra = sys.argv[4] if len(sys.argv) > 4 else ''
name = f'pista_real_take{take}_{kind}{suffix}.png'
try:
    if kind == 'turnaround':
        p = prompt('FORMAT_TURNAROUND', take)
        if extra: p = p.replace(' Avoid:', ' ' + extra + ' Avoid:')
        res = gen(p, '2400x1200')
    else:
        p = prompt('FORMAT_KEYART', take)
        if extra: p = p.replace(' Avoid:', ' ' + extra + ' Avoid:')
        res = edit(p, '1824x1216', os.path.join(OUT, f'pista_real_take{take}_turnaround.png'))
except urllib.error.HTTPError as e:
    print('HTTP', e.code, e.read().decode()[:800]); sys.exit(1)
open(os.path.join(OUT, name), 'wb').write(base64.b64decode(res['data'][0]['b64_json']))
open(os.path.join('/tmp/claude-0/-home-user-JungleBooze/6648f42c-bde1-5a4b-b000-2d9be4f20958/scratchpad/gen', name + '.prompt.txt'), 'w').write(p)
print('ok', name, res.get('usage', {}))
