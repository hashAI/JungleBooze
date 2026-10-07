#!/usr/bin/env python3
"""Concept sheets (OpenAI Images) and 3D models (Meshy) for the environment kit, with hard spend caps."""
import base64, json, os, sys, time, urllib.request
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEDGER = Path(__file__).with_name("spend_ledger.json")
MAX_IMAGES, MAX_CREDITS = 4, 200
PREVIEW_CREDITS, REFINE_CREDITS = 20, 10  # assumed; the ledger records the balance delta when known
STYLE = ("Inkbound Pulp: bold ink outlines, flat colors with two or three hard shading bands, warm pulp-adventure "
         "jungle palette, props concept sheet on plain parchment background, no text, no logos.")
GROUPS = {
    "obstacles": "log barrier to jump, hanging log to slide under, mossy rock block, rolling boulder, thorn patch, "
                 "falling-rock lane strike column; hazards use red-and-ink stripes",
    "pickups": "gold coin with turquoise gem center, magnet U icon, shield dome icon, double chevron boost icon",
    "ground": "cream jungle path tile, ravine lip edge, vine branch and signpost",
    "foliage": "two low-poly jungle trees and a bush",
}

def load_env():
    for line in (Path.home() / ".config/junglebooze/secrets.env").read_text().splitlines():
        if "=" in line and not line.startswith("#"):
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"'))

def ledger():
    return json.loads(LEDGER.read_text()) if LEDGER.exists() else {"images": 0, "credits": 0, "log": []}

def save(l):
    LEDGER.write_text(json.dumps(l, indent=2))

def call(url, data=None, headers=None):
    req = urllib.request.Request(url, data=json.dumps(data).encode() if data is not None else None, headers=headers or {})
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=300) as r:
        return json.load(r)

def meshy(path, data=None):
    return call("https://api.meshy.ai/openapi" + path, data, {"Authorization": "Bearer " + os.environ["MESHY_API_KEY"]})

def balance():
    print("Meshy balance:", meshy("/v1/balance"))

def concept(group):
    l = ledger()
    if l["images"] >= MAX_IMAGES:
        sys.exit("image cap reached")
    out = ROOT / "design/concepts/2026-10-08"
    out.mkdir(parents=True, exist_ok=True)
    prompt = f"Props concept sheet: {GROUPS[group]}. {STYLE}"
    r = call("https://api.openai.com/v1/images/generations", {"model": "gpt-image-1", "prompt": prompt, "size": "1536x1024"},
             {"Authorization": "Bearer " + os.environ["OPENAI_API_KEY"]})
    (out / f"props_{group}.png").write_bytes(base64.b64decode(r["data"][0]["b64_json"]))
    l["images"] += 1; l["log"].append({"date": str(date.today()), "kind": "concept", "group": group}); save(l)

def model(name, prompt):
    l = ledger()
    if l["credits"] + PREVIEW_CREDITS + REFINE_CREDITS > MAX_CREDITS:
        sys.exit("credit cap would be exceeded")
    task = meshy("/v2/text-to-3d", {"mode": "preview", "prompt": prompt + " " + STYLE, "art_style": "realistic",
                                     "target_polycount": 1200, "topology": "triangle"})["result"]
    while True:
        t = meshy("/v2/text-to-3d/" + task)
        if t["status"] in ("SUCCEEDED", "FAILED", "CANCELED"):
            break
        time.sleep(10)
    l["credits"] += PREVIEW_CREDITS; l["log"].append({"date": str(date.today()), "kind": "model", "name": name, "status": t["status"]}); save(l)
    if t["status"] == "SUCCEEDED":
        dest = ROOT / "UnityProject/Assets/_Game/Art/Environment/Models" / (name + ".glb")
        with urllib.request.urlopen(t["model_urls"]["glb"]) as r:
            dest.write_bytes(r.read())
        print("saved", dest)

if __name__ == "__main__":
    load_env()
    cmd = sys.argv[1]
    {"balance": lambda: balance(), "concept": lambda: concept(sys.argv[2]), "model": lambda: model(sys.argv[2], sys.argv[3])}[cmd]()
