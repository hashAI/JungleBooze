#!/usr/bin/env python3
"""Minimal Meshy API client for rigging and animation (stdlib only).

Key: MESHY_API_KEY from the environment (e.g. `set -a; source ~/.config/junglebooze/secrets.env; set +a`).
The key is never printed or written anywhere.
Every paid call is appended to <log> (JSON lines: time, endpoint, task id, consumed credits, balance after).
Endpoints checked against docs.meshy.ai on 2026-10-09 (rigging 5 credits; animation 3 credits per action).

Usage:
  meshy.py balance
  meshy.py library [search]                         free
  meshy.py rig <model.glb> <height_m> <basecolor.png> <log>
  meshy.py animate <rig_task_id> <log> <action_id> [<action_id> ...]     (1-10 ids, merged into one file)
  meshy.py wait <rigging|animations> <task_id> <log>
  meshy.py fetch <rigging|animations> <task_id> <out_dir>                 downloads every result URL
"""
import base64
import json
import os
import sys
import time
import urllib.request

API = 'https://api.meshy.ai/openapi/v1'


def req(method, path, body=None):
    key = os.environ.get('MESHY_API_KEY')
    if not key:
        sys.exit('MESHY_API_KEY is not set')
    data = json.dumps(body).encode() if body is not None else None
    r = urllib.request.Request(API + path, data=data, method=method,
                               headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(r, timeout=300) as resp:
            return json.loads(resp.read() or b'{}')
    except urllib.error.HTTPError as e:
        sys.exit(f'HTTP {e.code} on {method} {path}: {e.read().decode()[:500]}')


def balance():
    return req('GET', '/balance').get('balance')


def data_uri(path, mime):
    with open(path, 'rb') as f:
        return f'data:{mime};base64,' + base64.b64encode(f.read()).decode()


def log(path, rec):
    rec['time'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with open(path, 'a') as f:
        f.write(json.dumps(rec) + '\n')
    print(json.dumps(rec))


def wait(kind, tid, logpath):
    while True:
        t = req('GET', f'/{kind}/{tid}')
        if t['status'] in ('SUCCEEDED', 'FAILED', 'CANCELED'):
            break
        print(f"{t['status']} {t.get('progress')}%", flush=True)
        time.sleep(10)
    log(logpath, {'event': 'finished', 'endpoint': kind, 'task': tid, 'status': t['status'],
                  'consumed_credits': t.get('consumed_credits'), 'error': t.get('task_error'),
                  'balance_after': balance()})
    return t


def urls(obj, prefix=''):
    if isinstance(obj, dict):
        for k, v in obj.items():
            yield from urls(v, f'{prefix}{k}.')
    elif isinstance(obj, str) and obj.startswith('http'):
        yield prefix.rstrip('.'), obj


def main():
    cmd, a = sys.argv[1], sys.argv[2:]
    if cmd == 'balance':
        print(balance())
    elif cmd == 'library':
        q = f'?search={a[0]}' if a else ''
        for x in req('GET', '/animations/library' + q).get('result', []):
            print(x['action_id'], x['category'], x['sub_category'], x['name'], sep='\t')
    elif cmd == 'rig':
        glb, h, tex, logpath = a
        before = balance()
        body = {'model_url': data_uri(glb, 'model/gltf-binary'), 'height_meters': float(h),
                'texture_image_url': data_uri(tex, 'image/png')}
        tid = req('POST', '/rigging', body)['result']
        log(logpath, {'event': 'created', 'endpoint': 'rigging', 'task': tid, 'height_m': float(h),
                      'model': os.path.basename(glb), 'balance_before': before})
        wait('rigging', tid, logpath)
    elif cmd == 'animate':
        rig, logpath, ids = a[0], a[1], [int(x) for x in a[2:]]
        before = balance()
        body = {'rig_task_id': rig, 'action_ids': ids} if len(ids) > 1 else {'rig_task_id': rig, 'action_id': ids[0]}
        body['post_process'] = {'operation_type': 'change_fps', 'fps': 30}
        tid = req('POST', '/animations', body)['result']
        log(logpath, {'event': 'created', 'endpoint': 'animations', 'task': tid, 'rig': rig, 'action_ids': ids,
                      'balance_before': before})
        wait('animations', tid, logpath)
    elif cmd == 'wait':
        wait(a[0], a[1], a[2])
    elif cmd == 'fetch':
        kind, tid, out = a
        os.makedirs(out, exist_ok=True)
        t = req('GET', f'/{kind}/{tid}')
        with open(os.path.join(out, 'task.json'), 'w') as f:
            json.dump(t, f, indent=1)
        for name, u in urls(t.get('result') or {}):
            ext = u.split('?')[0].rsplit('.', 1)[-1]
            dst = os.path.join(out, f'{name}.{ext}')
            urllib.request.urlretrieve(u, dst)
            print('saved', dst, os.path.getsize(dst))
    else:
        sys.exit(__doc__)


if __name__ == '__main__':
    main()
