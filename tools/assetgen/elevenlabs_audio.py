#!/usr/bin/env python3
"""ElevenLabs audio generation for AURELIA (sound effects + music) on the owner's existing plan.

Reads the manifest (design/prompts/audio/aurelia_audio.json), generates missing takes into
art_source/audio/elevenlabs/raw/<id>_t<n>.mp3 and logs every call (with the measured credit delta) to
art_source/audio/elevenlabs/calls.jsonl. The key comes from ~/.config/junglebooze/secrets.env and is never printed.

Usage:
  elevenlabs_audio.py credits                 # show plan, used / limit
  elevenlabs_audio.py gen [ids...] [--takes N] [--ceiling CREDITS]
      Generates takes for the given ids (all when omitted) until each has N takes (default: manifest "takes").
      Stops before a call when the credits used this period would pass --ceiling (default 60000).
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANIFEST = os.path.join(ROOT, "design", "prompts", "audio", "aurelia_audio.json")
OUT = os.path.join(ROOT, "art_source", "audio", "elevenlabs")
RAW = os.path.join(OUT, "raw")
LOG = os.path.join(OUT, "calls.jsonl")
API = "https://api.elevenlabs.io/v1"


def load_key():
    path = os.path.expanduser("~/.config/junglebooze/secrets.env")
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line.startswith("export "):
                line = line[7:]
            if line.startswith("ELEVENLABS_API_KEY="):
                return line.split("=", 1)[1].strip().strip('"').strip("'")
    raise SystemExit("ELEVENLABS_API_KEY not found in secrets.env")


KEY = load_key()


def request(path, body=None, query="", attempts=6):
    for n in range(attempts):
        try:
            return _request(path, body, query)
        except urllib.error.HTTPError as err:
            if err.code not in (429, 500, 502, 503) or n == attempts - 1:
                raise
            time.sleep(5 * (n + 1))


def _request(path, body=None, query=""):
    url = API + path + query
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if body is not None else "GET")
    req.add_header("xi-api-key", KEY)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=600) as r:
        return r.read()


def subscription():
    d = json.loads(request("/user/subscription"))
    return d["tier"], d["character_count"], d["character_limit"]


def generate(entry, take):
    kind = entry["kind"]
    if kind == "sfx":
        body = {"text": entry["prompt"], "model_id": "eleven_text_to_sound_v2",
                "prompt_influence": entry.get("influence", 0.5)}
        if entry.get("duration"):
            body["duration_seconds"] = entry["duration"]
        if entry.get("loop"):
            body["loop"] = True
        return request("/sound-generation", body, "?output_format=mp3_44100_128"), body
    if kind == "music":
        if "plan" in entry:
            body = {"composition_plan": entry["plan"], "model_id": entry.get("model", "music_v1")}
        else:
            body = {"prompt": entry["prompt"], "music_length_ms": int(entry["duration"] * 1000),
                    "force_instrumental": True, "model_id": entry.get("model", "music_v1")}
        return request("/music", body, "?output_format=mp3_44100_128"), body
    raise SystemExit("unknown kind " + kind)


def main():
    args = sys.argv[1:]
    if not args or args[0] == "credits":
        tier, used, limit = subscription()
        print(f"tier={tier} used={used} limit={limit} remaining={limit - used}")
        return
    if args[0] != "gen":
        raise SystemExit(__doc__)
    takes_override = None
    ceiling = 60000
    ids = []
    i = 1
    while i < len(args):
        if args[i] == "--takes":
            takes_override = int(args[i + 1]); i += 2
        elif args[i] == "--ceiling":
            ceiling = int(args[i + 1]); i += 2
        else:
            ids.append(args[i]); i += 1
    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)
    entries = [e for e in manifest["entries"] if not ids or e["id"] in ids]
    missing = set(ids) - {e["id"] for e in entries}
    if missing:
        raise SystemExit("unknown ids: " + ", ".join(sorted(missing)))
    os.makedirs(RAW, exist_ok=True)
    _, used, _ = subscription()
    for e in entries:
        want = takes_override or e.get("takes", 1)
        for t in range(1, want + 1):
            out = os.path.join(RAW, f"{e['id']}_t{t}.mp3")
            if os.path.exists(out):
                continue
            if used >= ceiling:
                print(f"STOP: credits used {used} reached ceiling {ceiling}")
                return
            try:
                audio, body = generate(e, t)
            except urllib.error.HTTPError as err:
                print(f"{e['id']} t{t}: HTTP {err.code} {err.read()[:300]!r}")
                continue
            with open(out, "wb") as f:
                f.write(audio)
            _, after, limit = subscription()
            delta = after - used
            used = after
            rec = {"time": time.strftime("%Y-%m-%dT%H:%M:%S"), "id": e["id"], "take": t, "kind": e["kind"],
                   "request": body, "file": os.path.relpath(out, ROOT), "bytes": len(audio),
                   "credits": delta, "credits_used_period": after, "limit": limit}
            with open(LOG, "a", encoding="utf-8") as f:
                f.write(json.dumps(rec) + "\n")
            print(f"{e['id']} t{t}: {len(audio)} B, {delta} credits (period {after}/{limit})")


if __name__ == "__main__":
    main()
