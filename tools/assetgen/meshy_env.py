#!/usr/bin/env python3
"""Environment kit: Meshy text-to-3D (preview + refine) with a hard credit cap and a task ledger.
Key from env MESHY_API_KEY (never printed). Usage: meshy_env.py <PrefabName> <polycount> [ai_model]
Prompts live in ENV_PROMPTS below. Raw GLBs go to $ENV_RAW (default tools/assetgen/raw_env, untracked); fit with fit_env.py."""
import json, os, sys, time, requests
from datetime import date
from pathlib import Path
BASE = "https://api.meshy.ai/openapi"
H = {"Authorization": "Bearer " + os.environ.get("MESHY_API_KEY", "")}
HERE = Path(__file__).parent
LEDGER = HERE / "env_spend_log.json"
RAW = Path(os.environ.get("ENV_RAW", HERE / "raw_env"))
CAP = 450
STYLE = ("Inkbound Pulp style: stylized low-poly game prop, bold flat colors, two or three hard shading bands, "
         "dark ink-colored (#1E1A24) edge accents, hand-painted, no text, no logo, single object.")
from env_prompts import ENV_PROMPTS  # noqa: E402

def bal(): return requests.get(BASE + "/v1/balance", headers=H, timeout=60).json()["balance"]
def led(): return json.loads(LEDGER.read_text()) if LEDGER.exists() else {"start_balance": bal(), "entries": []}
def save(l): LEDGER.write_text(json.dumps(l, indent=1))

def wait(path, tid):
    while True:
        r = requests.get(f"{BASE}{path}/{tid}", headers=H, timeout=60).json()
        if r["status"] in ("SUCCEEDED", "FAILED", "CANCELED"): return r
        time.sleep(10)

def run(name, poly, model):
    l = led(); spent = l["start_balance"] - bal()
    if spent + 30 > CAP: sys.exit(f"cap: {spent} spent")
    pr = ENV_PROMPTS[name]
    b0 = bal()
    body = {"mode": "preview", "prompt": pr["prompt"] + " " + STYLE, "ai_model": model, "topology": "triangle",
            "should_remesh": True, "target_polycount": int(poly), "target_formats": ["glb"]}
    r = requests.post(BASE + "/v2/text-to-3d", headers=H, json=body, timeout=120); r.raise_for_status()
    pid = r.json()["result"]; p = wait("/v2/text-to-3d", pid)
    if p["status"] != "SUCCEEDED": l["entries"].append({"name": name, "preview": pid, "status": p["status"]}); save(l); sys.exit("preview failed")
    body = {"mode": "refine", "preview_task_id": pid, "ai_model": model, "enable_pbr": False, "texture_resolution": "2k",
            "texture_prompt": pr["texture"], "target_formats": ["glb"]}
    r = requests.post(BASE + "/v2/text-to-3d", headers=H, json=body, timeout=120); r.raise_for_status()
    rid = r.json()["result"]; t = wait("/v2/text-to-3d", rid)
    RAW.mkdir(parents=True, exist_ok=True)
    if t["status"] == "SUCCEEDED":
        (RAW / f"{name}.glb").write_bytes(requests.get(t["model_urls"]["glb"], timeout=300).content)
    l["entries"].append({"date": str(date.today()), "name": name, "model": model, "preview_task": pid, "refine_task": rid,
                         "status": t["status"], "credits": b0 - bal(), "polycount_req": int(poly)})
    save(l); print(name, t["status"], "credits", b0 - bal(), "total", l["start_balance"] - bal())

if __name__ == "__main__":
    run(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else "meshy-6")
