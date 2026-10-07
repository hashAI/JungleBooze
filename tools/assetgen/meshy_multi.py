#!/usr/bin/env python3
"""Meshy multi-image-to-3D runner. Key from env MESHY_API_KEY (never printed).
Usage: meshy_multi.py create <name> <outdir> <polycount> img1 [img2 ...]
       meshy_multi.py poll <task_id> <outdir> <name>
"""
import base64, json, os, sys, time, requests
BASE = "https://api.meshy.ai/openapi/v1"
H = {"Authorization": "Bearer " + os.environ["MESHY_API_KEY"]}

def data_uri(p):
    return "data:image/png;base64," + base64.b64encode(open(p, "rb").read()).decode()

def create(name, outdir, poly, imgs):
    body = {"image_urls": [data_uri(i) for i in imgs], "ai_model": "meshy-6",
            "topology": "triangle", "target_polycount": int(poly), "should_remesh": True,
            "should_texture": True, "enable_pbr": False, "texture_resolution": "2k",
            "pose_mode": "a-pose" if name == "Pista" else "", "image_enhancement": False,
            "target_formats": ["glb"], "multi_view_thumbnails": True, "origin_at": "bottom"}
    if os.environ.get("MESHY_TEXTURE_PROMPT"): body["texture_prompt"] = os.environ["MESHY_TEXTURE_PROMPT"]
    r = requests.post(BASE + "/multi-image-to-3d", headers=H, json=body, timeout=120)
    print(r.status_code, r.text[:300]); r.raise_for_status()
    return r.json()["result"]

def poll(tid, outdir, name):
    while True:
        r = requests.get(f"{BASE}/multi-image-to-3d/{tid}", headers=H, timeout=60).json()
        print(r.get("status"), r.get("progress"), flush=True)
        if r["status"] in ("SUCCEEDED", "FAILED", "CANCELED"): break
        time.sleep(15)
    os.makedirs(outdir, exist_ok=True)
    json.dump({k: v for k, v in r.items()}, open(os.path.join(outdir, name + "_task.json"), "w"), indent=1)
    if r["status"] != "SUCCEEDED": print(r.get("task_error")); return
    def dl(u, fn): open(os.path.join(outdir, fn), "wb").write(requests.get(u, timeout=300).content)
    dl(r["model_urls"]["glb"], name + ".glb")
    if r.get("thumbnail_url"): dl(r["thumbnail_url"], name + "_thumb.png")
    for k, u in (r.get("thumbnail_urls") or {}).items(): dl(u, f"{name}_thumb_{k}.png")
    print("done")

if __name__ == "__main__":
    if sys.argv[1] == "create": print("TASK", create(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5:]))
    else: poll(sys.argv[2], sys.argv[3], sys.argv[4])
