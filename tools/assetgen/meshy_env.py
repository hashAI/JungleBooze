#!/usr/bin/env python3
"""Meshy client for environment assets: text-to-image, image-to-image, image-to-3d (stdlib only).

Key: MESHY_API_KEY from the environment (`set -a; source ~/.config/junglebooze/secrets.env; set +a`).
The key is never printed or written anywhere. Every paid call is appended to the JSON-lines log
(default art_source/environment/meshy/calls.jsonl) with task id, credits and the local result path.
Endpoints and prices checked against docs.meshy.ai on 2026-10-09:
  text-to-image / image-to-image: nano-banana 3, nano-banana-2 6, nano-banana-pro 9
  image-to-3d: meshy-6-lite mesh only 5; latest (7.1) mesh only 20, +5 for geometry_resolution 2k/4k.

Usage (one job per call, blocks until done, downloads results):
  meshy_env.py t2i  <task> <name> <model> <aspect> <prompt_file> <out_dir> [--nobg]
  meshy_env.py i2i  <task> <name> <model> <aspect> <prompt_file> <out_dir> <ref.png> [<ref.png> ...] [--nobg]
  meshy_env.py i23d <task> <name> <model> <image.png> <out_dir> [--ultra] [--polycount N]
  meshy_env.py balance
"""
import base64
import json
import os
import sys
import time
import urllib.request

API = 'https://api.meshy.ai/openapi/v1'
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
LOG = os.path.join(REPO, 'art_source', 'environment', 'meshy', 'calls.jsonl')


def req(method, path, body=None):
    key = os.environ.get('MESHY_API_KEY')
    if not key:
        sys.exit('MESHY_API_KEY is not set')
    data = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(API + path, data=data, method=method,
                               headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(r, timeout=300) as resp:
                return json.loads(resp.read() or b'{}')
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:500]
            if e.code >= 500 and attempt < 4 and method == 'GET':
                time.sleep(5)
                continue
            sys.exit(f'HTTP {e.code} on {method} {path}: {msg}')
        except urllib.error.URLError:
            if attempt < 4 and method == 'GET':
                time.sleep(5)
                continue
            raise


def data_uri(path):
    mime = 'image/png' if path.lower().endswith('.png') else 'image/jpeg'
    with open(path, 'rb') as f:
        return f'data:{mime};base64,' + base64.b64encode(f.read()).decode()


def rel(p):
    return os.path.relpath(p, REPO)


def log(rec):
    with open(LOG, 'a') as f:
        f.write(json.dumps(rec) + '\n')


def wait(kind, tid):
    while True:
        t = req('GET', f'/{kind}/{tid}')
        if t['status'] in ('SUCCEEDED', 'FAILED', 'CANCELED'):
            return t
        time.sleep(8)


def download(url, path):
    with urllib.request.urlopen(url, timeout=600) as r, open(path, 'wb') as f:
        f.write(r.read())


def image_job(kind, a):
    task, name, model, aspect, prompt_file, out_dir = a[:6]
    refs = [x for x in a[6:] if not x.startswith('--')]
    nobg = '--nobg' in a
    prompt = open(prompt_file).read().strip()
    body = {'ai_model': model, 'prompt': prompt, 'aspect_ratio': aspect}
    if nobg:
        body['remove_background'] = True
    if kind == 'image-to-image':
        body['reference_image_urls'] = [data_uri(r) for r in refs]
    ts = time.strftime('%Y-%m-%dT%H:%M:%S')
    tid = req('POST', f'/{kind}', body)['result']
    t = wait(kind, tid)
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for i, u in enumerate(t.get('image_urls') or []):
        p = os.path.join(out_dir, f'{name}.png' if i == 0 else f'{name}_{i}.png')
        download(u, p)
        paths.append(rel(p))
    rec = {'ts': ts, 'task': task, 'name': name, 'endpoint': f'POST /openapi/v1/{kind}', 'model': model,
           'aspect_ratio': aspect, 'remove_background': nobg, 'refs': [rel(r) for r in refs] or None,
           'prompt': prompt, 'task_id': tid, 'status': t['status'], 'credits': t.get('consumed_credits'),
           'error': t.get('task_error'), 'result_path': paths[0] if len(paths) == 1 else paths}
    log(rec)
    print(json.dumps({k: rec[k] for k in ('name', 'status', 'credits', 'result_path', 'error')}))


def i23d(a):
    task, name, model, image, out_dir = a[:5]
    body = {'image_url': data_uri(image), 'ai_model': model, 'should_texture': False,
            'target_formats': ['glb'], 'should_remesh': False}
    if '--polycount' in a:
        body['should_remesh'] = True
        body['target_polycount'] = int(a[a.index('--polycount') + 1])
    if '--ultra' in a:
        body['geometry_resolution'] = '2k'
    ts = time.strftime('%Y-%m-%dT%H:%M:%S')
    tid = req('POST', '/image-to-3d', body)['result']
    t = wait('image-to-3d', tid)
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for k, u in (t.get('model_urls') or {}).items():
        if u and k in ('glb', 'pre_remeshed_glb'):
            p = os.path.join(out_dir, f'{name}.glb' if k == 'glb' else f'{name}_pre_remesh.glb')
            download(u, p)
            paths.append(rel(p))
    if t.get('thumbnail_url'):
        p = os.path.join(out_dir, f'{name}_thumb.png')
        download(t['thumbnail_url'], p)
    body.pop('image_url')
    rec = {'ts': ts, 'task': task, 'name': name, 'endpoint': 'POST /openapi/v1/image-to-3d', 'model': model,
           'options': body, 'input_image': rel(image), 'task_id': tid, 'status': t['status'],
           'credits': t.get('consumed_credits'), 'error': t.get('task_error'), 'result_path': paths}
    log(rec)
    print(json.dumps({k: rec[k] for k in ('name', 'status', 'credits', 'result_path', 'error')}))


def main():
    cmd, a = sys.argv[1], sys.argv[2:]
    if cmd == 'balance':
        print(req('GET', '/balance').get('balance'))
    elif cmd == 't2i':
        image_job('text-to-image', a)
    elif cmd == 'i2i':
        image_job('image-to-image', a)
    elif cmd == 'i23d':
        i23d(a)
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main()
