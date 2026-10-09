#!/usr/bin/env python3
"""Stem separation of a generated track (ElevenLabs /v1/music/stem-separation). Usage: elevenlabs_stems.py <in.mp3> <outdir>"""
import json, os, sys, time, uuid, urllib.request, zipfile, io
sys.path.insert(0, os.path.dirname(__file__))
import elevenlabs_audio as el

src, outdir = sys.argv[1], sys.argv[2]
os.makedirs(outdir, exist_ok=True)
_, before, _ = el.subscription()
boundary = uuid.uuid4().hex
data = open(src, "rb").read()
body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{os.path.basename(src)}\"\r\n"
        f"Content-Type: audio/mpeg\r\n\r\n").encode() + data + f"\r\n--{boundary}--\r\n".encode()
req = urllib.request.Request(el.API + "/music/stem-separation?output_format=mp3_44100_128", data=body, method="POST")
req.add_header("xi-api-key", el.KEY)
req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
with urllib.request.urlopen(req, timeout=900) as r:
    z = r.read()
zipfile.ZipFile(io.BytesIO(z)).extractall(outdir)
time.sleep(15)
_, after, limit = el.subscription()
rec = {"time": time.strftime("%Y-%m-%dT%H:%M:%S"), "id": "stems:" + os.path.basename(src), "kind": "stems",
       "file": os.path.relpath(outdir, el.ROOT), "credits": after - before, "credits_used_period": after, "limit": limit}
open(el.LOG, "a").write(json.dumps(rec) + "\n")
print(os.listdir(outdir), rec["credits"], "credits")
