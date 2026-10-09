#!/usr/bin/env python3
"""Offline mix of an Expedition bot run's audio log (ExpeditionAudioRender) from the shipped clips, for the demo video.

Reproduces the runtime rules: cue volumes from the catalog asset, variation and pitch from the log, bus volumes at
the defaults (music 0.8 → squared taper, SFX/ambience 1.0), the music level table with danger switches on the bar
line (layer fade 1.5 s, section fade 0.5 s), sting ducking, soft stop, the results loop fade-in, and the A/B ambience
crossfades. Not modelled: per-play pitch/volume jitter and the underwater low-pass (both small).

Usage: audio_offline_mix.py <expedition_audio.jsonl> <out.wav> [--video in.mp4 --out demo.mp4]
"""
import glob
import json
import os
import re
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(ROOT, "UnityProject", "Assets", "_Game", "Audio")
CATALOG = os.path.join(ROOT, "UnityProject", "Assets", "_Game", "Config", "Audio", "Resources", "AudioCatalog.asset")
SR = 44100
CTRL = 441  # control rate: 10 ms

MUSIC_BUS = 0.8 ** 2
SFX_BUS = 10 ** (-2 / 20)  # AudioService.SfxTrimDb
LAYERS = ["mus_explore_melody", "mus_explore_drums", "mus_danger", "mus_layer_perc"]


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).astype(np.float64)


_cache = {}


def clip(name):
    if name not in _cache:
        hits = glob.glob(os.path.join(AUDIO, "**", name + ".ogg"), recursive=True)
        _cache[name] = decode(hits[0])
    return _cache[name]


def files_for(cue_id):
    base = cue_id.replace(".", "_")
    hits = sorted(glob.glob(os.path.join(AUDIO, "**", base + "_[0-9][0-9].ogg"), recursive=True))
    if not hits:
        hits = glob.glob(os.path.join(AUDIO, "**", base + ".ogg"), recursive=True)
    return [os.path.splitext(os.path.basename(h))[0] for h in hits]


def catalog():
    text = open(CATALOG, encoding="utf-8").read()
    vols = {}
    for m in re.finditer(r"- Id: (\S+)\n(?:.*\n)*?\s+Volume: ([\d.]+)", text):
        vols[m.group(1)] = float(m.group(2))
    lv = re.search(r"LevelVolumes:\n((?:\s+- [\d.]+\n)+)", text)
    levels = [float(x) for x in re.findall(r"- ([\d.]+)", lv.group(1))] if lv else None
    return vols, levels


def place(out, x, t, gain, semis=0.0):
    if semis:
        ratio = 2 ** (semis / 12)
        n = int(len(x) / ratio)
        idx = np.arange(n) * ratio
        x = np.stack([np.interp(idx, np.arange(len(x)), x[:, c]) for c in range(2)], axis=1)
    a = int(t * SR)
    if a >= len(out):
        return
    b = min(len(out), a + len(x))
    out[a:b] += x[:b - a] * gain


def approach(v, target, step):
    return min(target, v + step) if v < target else max(target, v - step)


def render(log, total):
    vols, levels = catalog()
    if not levels:
        levels = [0.85, 0, 0, 0, 1, 0.5, 0, 0, 1, 0.9, 0, 0.7, 0, 0, 1, 0.9]
    n = int(total * SR) + SR
    out = np.zeros((n, 2))
    music = np.zeros((n, 2))
    amb = np.zeros((n, 2))
    stings = []

    # --- SFX / UI cues and stings
    for e in log:
        if e["type"] == "cue":
            names = files_for(e["id"])
            if not names:
                continue
            name = names[e["clip"] if 0 <= e["clip"] < len(names) else 0]
            g = vols.get(e["id"], 1.0) * e["gain"]
            target = amb if e["id"].startswith("amb.") else out
            if target is out:
                g *= SFX_BUS
            place(target, clip(name), e["t"], g, e["semis"])
        elif e["type"] == "sting":
            x = clip(e["id"].replace(".", "_"))
            g = vols.get(e["id"], 0.9) * MUSIC_BUS
            stings.append([e["t"], e["t"] + len(x) / SR, x, g])

    # One sting source: a new sting stops the previous one (MusicPlayer.PlaySting).
    for i, st in enumerate(stings):
        end = stings[i + 1][0] if i + 1 < len(stings) else st[1]
        n_keep = int(max(0.0, min(st[1], end) - st[0]) * SR)
        place(out, st[2][:n_keep], st[0], st[3])
        st[1] = min(st[1], end)
    stings = [(st[0], st[1]) for st in stings]

    # --- Ambience channels (A/B crossfade, linear fades like AmbienceChannel)
    channels = {}
    for e in log:
        if e["type"] == "amb":
            channels.setdefault(e["channel"], []).append(e)
    for ch, events in channels.items():
        voices = []  # [name, start, vol, target, fade]
        steps = int(n / CTRL)
        bufs = {}
        ev = 0
        for k in range(steps):
            t = k * CTRL / SR
            while ev < len(events) and events[ev]["t"] <= t:
                e = events[ev]
                for v in voices:
                    v[3] = 0.0
                    v[4] = max(e["fade"], 0.01)
                if e["id"]:
                    voices.append([e["id"], t, 0.0, e["gain"], max(e["fade"], 0.01)])
                ev += 1
            for v in voices:
                v[2] = approach(v[2], v[3], (CTRL / SR) / v[4])
                if v[2] > 0:
                    x = bufs.setdefault(v[0], clip(v[0].replace(".", "_")))
                    a = k * CTRL
                    off = int((t - v[1]) * SR) % len(x)
                    seg = np.take(x, np.arange(off, off + CTRL) % len(x), axis=0)
                    amb[a:a + CTRL] += seg * v[2]
            voices = [v for v in voices if v[2] > 0 or v[3] > 0]

    # --- Music: intro + synced layers, level table, bar-gated danger, ducks, soft stop, results
    starts = [e for e in log if e["type"] == "music"]
    steps = int(n / CTRL)
    loop_start = None
    intro_start = None
    level = 1
    cur = [levels[4 + i] for i in range(4)]
    tgt = list(cur)
    gate = None
    stop, stop_t = 1.0, 1.0
    res_v, res_t, res_start = 0.0, 0.0, None
    ev = 0
    layer_x = [clip(l) for l in LAYERS]
    intro = clip("mus_theme_intro")
    results = clip("mus_results")
    for k in range(steps):
        t = k * CTRL / SR
        while ev < len(starts) and starts[ev]["t"] <= t:
            e = starts[ev]
            a = e["action"]
            if a == "start":
                intro_start = e["t"] + 0.1 if e["value"] == 1 else None
                loop_start = e["t"] + 0.1 + (len(intro) / SR if e["value"] == 1 else 0)
                level = 1
                cur = [levels[4 + i] for i in range(4)]
                tgt = list(cur)
                gate = None
                stop = stop_t = 1.0
                res_t = 0.0
            elif a == "level":
                new = max(0, min(3, e["value"]))
                if new != level:
                    section = (new == 3) != (level == 3)
                    level = new
                    tgt = [levels[new * 4 + i] for i in range(4)]
                    if section and loop_start is not None:
                        bars = max(0.0, np.ceil((t - loop_start) / 2.0 - 1e-9))
                        gate = loop_start + bars * 2.0
            elif a == "stopSoft":
                stop_t = 0.0
            elif a == "results":
                stop_t = 0.0
                res_t = 1.0
                res_start = t
                res_v = 0.0
            ev += 1
        dt = CTRL / SR
        open_ = gate is None or t >= gate
        for i in range(4):
            sectional = gate is not None and i != 3
            if sectional and not open_:
                continue
            fade = 0.5 if sectional else 1.5
            cur[i] = approach(cur[i], tgt[i], dt / fade)
        if gate is not None and open_ and all(abs(cur[i] - tgt[i]) < 1e-4 for i in range(4)):
            gate = None
        ducked = any(s <= t < e for s, e in stings)
        stop = approach(stop, stop_t, dt / 1.2)
        res_v = approach(res_v, res_t, dt / 1.5)
        duck = 0.35 if ducked else 1.0
        a = k * CTRL
        idx = np.arange(a, a + CTRL)
        m = stop * duck * MUSIC_BUS
        if intro_start is not None and loop_start is not None and intro_start <= t < loop_start:
            off = int((t - intro_start) * SR)
            if off + CTRL <= len(intro):
                music[a:a + CTRL] += intro[off:off + CTRL] * m
        if loop_start is not None and t >= loop_start and stop > 0:
            off = int(round((t - loop_start) * SR))
            for i in range(4):
                if cur[i] > 0:
                    x = layer_x[i]
                    music[a:a + CTRL] += np.take(x, np.arange(off, off + CTRL) % len(x), axis=0) * cur[i] * m
        if res_start is not None and res_v > 0:
            off = int((t - res_start) * SR)
            music[a:a + CTRL] += np.take(results, np.arange(off, off + CTRL) % len(results), axis=0) * res_v * MUSIC_BUS * duck

    mix = out + music + amb
    return mix[:int(total * SR)]


def main():
    args = sys.argv[1:]
    log_path, wav = args[0], args[1]
    log = [json.loads(l) for l in open(log_path) if l.strip()]
    total = max(e["t"] for e in log)
    mix = render(log, total)
    peak = np.abs(mix).max()
    clipped = int((np.abs(mix) > 1.0).sum())
    print(f"duration {total:.1f}s peak {20 * np.log10(peak + 1e-12):.2f} dBFS, samples over 0 dBFS: {clipped}")
    y = np.clip(mix, -1, 1)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", wav],
                   input=y.astype(np.float32).tobytes(), check=True)
    if "--video" in args:
        video = args[args.index("--video") + 1]
        dst = args[args.index("--out") + 1]
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", video, "-i", wav, "-map", "0:v", "-map", "1:a", "-c:v", "copy",
                        "-c:a", "aac", "-b:a", "160k", "-shortest", "-movflags", "+faststart", dst], check=True)
        print("muxed", dst)


if __name__ == "__main__":
    main()
