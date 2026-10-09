#!/usr/bin/env python3
"""OpenAI image generation for environment art (stdlib only). Key: OPENAI_API_KEY from the environment
(`set -a; source ~/.config/junglebooze/secrets.env; set +a`), never printed or written.

Usage:
  openai_images.py gen  <purpose> <prompt_file> <size> <out.png> [--transparent]
  openai_images.py edit <purpose> <prompt_file> <size> <out.png> <ref.jpg|png> [<ref> ...] [--transparent]
Model gpt-image-2, quality medium (owner approval 2026-10-09; cap for the environment task $25).
Every call is appended to art_source/environment/openai/calls.jsonl (model, quality, size, tokens, file, purpose,
estimated cost at $40 per 1M output image tokens + $10 per 1M input tokens, a conservative estimate).
"""
import base64
import json
import os
import sys
import time
import urllib.request
import uuid

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
LOG = os.path.join(REPO, 'art_source', 'environment', 'openai', 'calls.jsonl')
MODEL, QUALITY = 'gpt-image-2', 'medium'


def key():
    k = os.environ.get('OPENAI_API_KEY')
    if not k:
        sys.exit('OPENAI_API_KEY is not set')
    return k


def call(req):
    for attempt in range(3):
        try:
            return json.load(urllib.request.urlopen(req, timeout=900))
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:600]
            if e.code >= 500 and attempt < 2:
                time.sleep(10)
                continue
            sys.exit(f'HTTP {e.code}: {msg}')


def gen(prompt, size, transparent):
    body = {'model': MODEL, 'prompt': prompt, 'size': size, 'quality': QUALITY, 'n': 1, 'output_format': 'png'}
    if transparent:
        body['background'] = 'transparent'
    r = urllib.request.Request('https://api.openai.com/v1/images/generations', data=json.dumps(body).encode(),
                               headers={'Authorization': 'Bearer ' + key(), 'Content-Type': 'application/json'})
    return call(r)


def edit(prompt, size, refs, transparent):
    b = uuid.uuid4().hex
    fields = [('model', MODEL), ('prompt', prompt), ('size', size), ('quality', QUALITY), ('n', '1'),
              ('output_format', 'png')]
    if transparent:
        fields.append(('background', 'transparent'))
    parts = [f'--{b}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode() for k, v in fields]
    for i, ref in enumerate(refs):
        mime = 'image/png' if ref.endswith('.png') else 'image/jpeg'
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="image[]"; filename="ref{i}{os.path.splitext(ref)[1]}"'
                     f'\r\nContent-Type: {mime}\r\n\r\n'.encode() + open(ref, 'rb').read() + b'\r\n')
    parts.append(f'--{b}--\r\n'.encode())
    r = urllib.request.Request('https://api.openai.com/v1/images/edits', data=b''.join(parts),
                               headers={'Authorization': 'Bearer ' + key(),
                                        'Content-Type': 'multipart/form-data; boundary=' + b})
    return call(r)


def main():
    a = sys.argv[1:]
    transparent = '--transparent' in a
    a = [x for x in a if x != '--transparent']
    mode, purpose, pf, size, out = a[:5]
    refs = a[5:]
    prompt = open(pf).read().strip()
    res = gen(prompt, size, transparent) if mode == 'gen' else edit(prompt, size, refs, transparent)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, 'wb') as f:
        f.write(base64.b64decode(res['data'][0]['b64_json']))
    u = res.get('usage', {}) or {}
    cost = u.get('output_tokens', 0) * 40e-6 + u.get('input_tokens', 0) * 10e-6
    rec = {'date': time.strftime('%Y-%m-%dT%H:%M:%S'), 'model': MODEL, 'quality': QUALITY, 'size': size,
           'mode': mode, 'transparent': transparent, 'input_tokens': u.get('input_tokens'),
           'output_tokens': u.get('output_tokens'), 'est_usd': round(cost, 4),
           'file': os.path.relpath(out, REPO), 'refs': [os.path.relpath(r, REPO) for r in refs],
           'prompt_file': os.path.relpath(pf, REPO), 'purpose': purpose}
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    with open(LOG, 'a') as f:
        f.write(json.dumps(rec) + '\n')
    print(json.dumps({k: rec[k] for k in ('file', 'output_tokens', 'est_usd')}))


if __name__ == '__main__':
    main()
