#!/usr/bin/env python3
"""Generate Pista (rigged, animated) and Duko (static) with the Meshy API from the locked concept sheets.

Run on the owner's Mac (the cloud container cannot reach api.meshy.ai):

    python3 tools/assetgen/meshy_characters.py balance
    python3 tools/assetgen/meshy_characters.py hero-model     # multi-image to 3D from the front/side/back crops
    python3 tools/assetgen/meshy_characters.py hero-rig       # rigging, also returns free walking/running clips
    python3 tools/assetgen/meshy_characters.py hero-anims     # jump, slide, stumble, idle (ids in meshy_actions.json)
    python3 tools/assetgen/meshy_characters.py macaw-model    # multi-image to 3D from front/side/back (top view unused)
    python3 tools/assetgen/meshy_characters.py ledger

The key is read from the MESHY_API_KEY environment variable, or from ~/.config/junglebooze/secrets.env
(a line MESHY_API_KEY=...). It is never printed or written anywhere. Every paid call is appended to
tools/assetgen/meshy_ledger.jsonl (credits are ESTIMATES until checked against `balance`) and refused when it
would push the total above CREDIT_CAP.

NOTE: endpoint paths and field names were written from memory of the Meshy v1 API and could not be checked from
the cloud container. Verify against the current Meshy docs on first run; the script fails loudly on unknown
responses and never retries a paid call on its own.
"""
import base64
import io
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONCEPTS = ROOT / "design" / "concepts" / "2026-10-07"
OUT = ROOT / "UnityProject" / "Assets" / "_Game" / "Art" / "Characters" / "Resources" / "Characters"
LEDGER = Path(__file__).with_name("meshy_ledger.jsonl")
ACTIONS = Path(__file__).with_name("meshy_actions.json")
API = "https://api.meshy.ai/openapi"
CREDIT_CAP = 250  # owner cap, 2026-10-07 (lowered from 550)

# Estimated credits per call (Meshy pricing at time of writing; confirm with `balance`).
EST = {"model": 35, "rig": 5, "anim": 3}
HERO_TRIS = 7500    # budget: hero <= 8k tris
MACAW_TRIS = 3800   # budget: companion <= 4k tris


def key():
    k = os.environ.get("MESHY_API_KEY")
    if not k:
        f = Path.home() / ".config" / "junglebooze" / "secrets.env"
        if f.exists():
            for line in f.read_text().splitlines():
                if line.startswith("MESHY_API_KEY="):
                    k = line.split("=", 1)[1].strip().strip('"').strip("'")
    if not k:
        sys.exit("MESHY_API_KEY not set (env or ~/.config/junglebooze/secrets.env)")
    return k


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer " + key())
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        sys.exit("HTTP %d on %s %s: %s" % (e.code, method, path, e.read().decode()[:400]))


def spent():
    if not LEDGER.exists():
        return 0
    return sum(json.loads(l)["credits_est"] for l in LEDGER.read_text().splitlines() if l.strip())


def charge(kind, what, task_id):
    cost = EST[kind]
    with LEDGER.open("a") as f:
        f.write(json.dumps({"date": time.strftime("%Y-%m-%d"), "what": what, "task": task_id, "credits_est": cost}) + "\n")


def guard(kind):
    if spent() + EST[kind] > CREDIT_CAP:
        sys.exit("Refusing: estimated spend %d + %d would exceed the %d credit cap" % (spent(), EST[kind], CREDIT_CAP))
    bal = call("GET", "/v1/balance").get("balance")
    if bal is not None and bal < EST[kind]:
        sys.exit("Refusing: balance %s is below the estimated cost %d" % (bal, EST[kind]))


def wait(path, label):
    while True:
        t = call("GET", path)
        st = t.get("status")
        print("  %s: %s %s%%" % (label, st, t.get("progress", "?")))
        if st == "SUCCEEDED":
            return t
        if st in ("FAILED", "CANCELED"):
            sys.exit("%s failed: %s" % (label, t.get("task_error")))
        time.sleep(10)


def download(url, dest):
    dest.parent.mkdir(parents=True, exist_ok=True)
    urllib.request.urlretrieve(url, dest)
    print("  saved", dest.relative_to(ROOT))


def crops(sheet, panels):
    """Cut the turnaround sheet into equal-width panels; returns data URIs (needs Pillow)."""
    from PIL import Image
    im = Image.open(CONCEPTS / sheet).convert("RGB")
    w, h = im.size
    uris = []
    for i in range(panels):
        c = im.crop((i * w // panels, 0, (i + 1) * w // panels, h))
        buf = io.BytesIO()
        c.save(buf, "PNG")
        uris.append("data:image/png;base64," + base64.b64encode(buf.getvalue()).decode())
    return uris


def model(name, sheet, panels, order, tris, pose):
    guard("model")
    uris = crops(sheet, panels)
    body = {
        "image_urls": [uris[i] for i in order],  # Meshy takes at most 4 images
        "ai_model": "meshy-6",
        "topology": "triangle",
        "target_polycount": tris,
        "should_texture": True,
        "enable_pbr": False,       # one base-color texture; keeps the hand-painted look
        "symmetry_mode": "auto",
        "pose_mode": pose,
    }
    t = call("POST", "/v1/multi-image-to-3d", body)
    tid = t["result"]
    charge("model", name + " model", tid)
    done = wait("/v1/multi-image-to-3d/" + tid, name + " model")
    (OUT / name).mkdir(parents=True, exist_ok=True)
    (OUT / name / (name + "_task.txt")).write_text(tid)
    download(done["model_urls"]["fbx"], OUT / name / (name + ".fbx"))
    download(done["model_urls"]["glb"], OUT / name / (name + "_source.glb"))
    if done.get("texture_urls"):
        download(done["texture_urls"][0]["base_color"], OUT / name / (name + "_basecolor.png"))


def hero_rig():
    guard("rig")
    tid = (OUT / "Pista" / "Pista_task.txt").read_text().strip()
    t = call("POST", "/v1/rigging", {"input_task_id": tid, "height_meters": 1.2})
    rid = t["result"]
    charge("rig", "Pista rig", rid)
    done = wait("/v1/rigging/" + rid, "rig")
    (OUT / "Pista" / "Pista_rig_task.txt").write_text(rid)
    r = done["result"]
    # Rigged character replaces the static FBX so the prefab has the skeleton. Free basic clips: walking, running.
    download(r["rigged_character_fbx_url"], OUT / "Pista" / "Pista.fbx")
    anims = r.get("basic_animations", {})
    for key_, name in (("running_armature_fbx_url", "run"), ("walking_armature_fbx_url", "walk")):
        if anims.get(key_):
            download(anims[key_], OUT / "Pista" / ("Pista_%s.fbx" % name))


def hero_anims():
    if not ACTIONS.exists():
        sys.exit("Create %s as {\"jump\": <action_id>, \"slide\": ..., \"stumble\": ..., \"idle\": ...} "
                 "using ids from the Meshy animation library, then rerun." % ACTIONS.name)
    actions = json.loads(ACTIONS.read_text())
    rid = (OUT / "Pista" / "Pista_rig_task.txt").read_text().strip()
    for name in ("jump", "slide", "stumble", "idle"):
        if name not in actions:
            print("  skipping", name, "(no action id)")
            continue
        guard("anim")
        t = call("POST", "/v1/animations", {"rig_task_id": rid, "action_id": actions[name]})
        aid = t["result"]
        charge("anim", "Pista anim " + name, aid)
        done = wait("/v1/animations/" + aid, name)
        download(done["result"]["animation_fbx_url"], OUT / "Pista" / ("Pista_%s.fbx" % name))


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "balance":
        print("balance:", call("GET", "/v1/balance").get("balance"), "| estimated spent by this script:", spent(), "of", CREDIT_CAP)
    elif cmd == "hero-model":
        model("Pista", "hero_take1_turnaround.png", 3, [0, 1, 2], HERO_TRIS, "a-pose")
    elif cmd == "hero-rig":
        hero_rig()
    elif cmd == "hero-anims":
        hero_anims()
    elif cmd == "macaw-model":
        model("Duko", "macaw_take1_turnaround.png", 4, [0, 1, 2], MACAW_TRIS, "")
    elif cmd == "ledger":
        print(LEDGER.read_text() if LEDGER.exists() else "(empty)", "total est:", spent())
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main()
