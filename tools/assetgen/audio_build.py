#!/usr/bin/env python3
"""Builds the game-ready AURELIA audio set from the curated ElevenLabs takes.

Input: art_source/audio/elevenlabs/{raw,stems} (local only). Output: UnityProject/Assets/_Game/Audio/** as Ogg Vorbis
(oggenc), plus art_source/audio/elevenlabs/work/build_report.jsonl with the final loudness/peak of every file.

Processing (all numpy, 44.1 kHz float):
- SFX: trim lead/tail below -50 dBFS, optional high-pass (removes the sub-bass "whoomp" the generator adds to
  whooshes; inaudible on phone speakers and it eats headroom), mono fold-down (or the louder channel when the stereo
  correlation is below 0.2, so phasey takes don't cancel), 4 ms fade-in / 25 ms fade-out, loudness to the category
  target, true-peak ceiling -1 dBTP (4x oversampled peak).
- Footsteps: onset slicing of the 6 s takes into single steps, the most consistent ones kept.
- Ambience loops: seam crossfade (the head is blended into the tail, so the wrap is continuous), stereo kept.
- Music: bar-exact cuts on the 120 BPM grid (bar = 2.000 s), loop seams crossfaded with the material just before the
  loop start (or just after its end), stems keep their relative gains; one common gain per music set.

Usage: audio_build.py [--only PREFIX]
"""
import json
import os
import re
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "art_source", "audio", "elevenlabs", "raw")
STEMS = os.path.join(ROOT, "art_source", "audio", "elevenlabs", "stems")
WORK = os.path.join(ROOT, "art_source", "audio", "elevenlabs", "work")
OUT = os.path.join(ROOT, "UnityProject", "Assets", "_Game", "Audio")
SR = 44100

# Loudness targets (LUFS integrated; short one-shots are measured the same way, gating off).
TARGET = {"sfx": -16.0, "reward": -15.0, "soft": -20.0, "step": -21.0, "ui": -20.0, "creature": -18.0,
          "amb": -26.0, "amb_water": -24.0, "music": -16.0, "sting": -15.0}

# id -> (folder, category, [(take, options)], options)
# options: hp (Hz), trim (start,end seconds), gain_db, stereo (keep stereo), mono_channel
SFX = {
    # movement
    "sfx.jump": ("SFX", "sfx", [("sfx.jump_t2", {}), ("sfx.jump_t1", {"hp": 180})]),
    "sfx.land": ("SFX", "soft", [("sfx.land_t1", {"hp": 60}), ("sfx.land_t2", {"hp": 60}), ("sfx.land_t4", {"hp": 60})]),
    "sfx.land.hard": ("SFX", "sfx", [("sfx.land.hard_t1", {"hp": 50})]),
    "sfx.slide": ("SFX", "sfx", [("sfx.slide_t1", {}), ("sfx.slide_t2", {})]),
    "sfx.dodge": ("SFX", "sfx", [("sfx.dodge_t1", {"hp": 150}), ("sfx.release_t2", {"hp": 200, "trim": (0.0, 0.45)})]),
    "sfx.edgeBrush": ("SFX", "soft", [("sfx.edgeBrush_t1", {}), ("sfx.edgeBrush_t2", {"hp": 120})]),
    "sfx.hit.minor": ("SFX", "sfx", [("sfx.hit.minor_t1", {"hp": 60}), ("sfx.hit.minor_t2", {"hp": 80})]),
    "sfx.hit.thorns": ("SFX", "sfx", [("sfx.hit.thorns_t1", {})]),
    "sfx.crash": ("SFX", "sfx", [("sfx.crash_t1", {"hp": 40}), ("sfx.crash_t2", {"hp": 40})]),
    "sfx.fall": ("SFX", "sfx", [("sfx.fall_t1", {"hp": 80})]),
    "sfx.gap.whoosh": ("SFX", "sfx", [("sfx.gap.whoosh_t1", {"hp": 150})]),
    "sfx.fail.crash": ("SFX", "sting", [("sfx.fail.crash_t1", {"hp": 60})]),
    # water
    "sfx.water.enter": ("SFX", "sfx", [("sfx.water.enter_t1", {"hp": 60}), ("sfx.water.enter_t2", {"hp": 60})]),
    "sfx.water.wadeIn": ("SFX", "sfx", [("sfx.water.wadeIn_t1", {})]),
    "sfx.water.exit": ("SFX", "sfx", [("sfx.water.exit_t1", {})]),
    "sfx.swim.stroke": ("SFX", "soft", [("sfx.swim.stroke_t1", {}), ("sfx.swim.stroke_t2", {})]),
    "sfx.dive": ("SFX", "sfx", [("sfx.dive_t1", {}), ("sfx.dive_t2", {})]),
    "sfx.surface": ("SFX", "sfx", [("sfx.surface_t1", {})]),
    "sfx.leap": ("SFX", "sfx", [("sfx.leap_t1", {}), ("sfx.leap_t2", {})]),
    "sfx.splash": ("SFX", "sfx", [("sfx.splash_t1", {}), ("sfx.splash_t2", {})]),
    "sfx.bump.water": ("SFX", "sfx", [("sfx.bump.water_t1", {})]),
    "sfx.deepDive": ("SFX", "sfx", [("sfx.deepDive_t1", {"hp": 40})]),
    "sfx.curtain.pass": ("SFX", "sfx", [("sfx.curtain.pass_t1", {}), ("sfx.curtain.pass_t2", {})]),
    # vine / canopy
    "sfx.vine.grab": ("SFX", "sfx", [("sfx.vine.grab_t1", {}), ("sfx.vine.grab_t2", {})]),
    "sfx.vine.creak": ("SFX", "soft", [("sfx.vine.creak_t1", {}), ("sfx.vine.creak_t2", {})]),
    "sfx.vine.window": ("SFX", "ui", [("sfx.vine.window_t1", {})]),
    "sfx.release": ("SFX", "sfx", [("sfx.release_t2", {"hp": 200}), ("sfx.gap.whoosh_t2", {"hp": 250, "trim": (0.0, 0.6)})]),
    "sfx.perfect": ("SFX", "reward", [("sfx.perfect_t1", {}), ("sfx.perfect_t2", {})]),
    "sfx.branch.creak": ("SFX", "soft", [("sfx.branch.creak_t1", {})]),
    "sfx.beam.land": ("SFX", "sfx", [("sfx.beam.land_t1", {"hp": 60})]),
    # pickups / rewards
    "sfx.coin": ("SFX", "reward", [("sfx.coin_t1", {}), ("sfx.coin_t3", {})]),
    "sfx.crystal": ("SFX", "reward", [("sfx.crystal_t2", {}), ("sfx.crystal_t1", {})]),
    "sfx.cleanLine": ("SFX", "reward", [("sfx.cleanLine_t1", {}), ("sfx.cleanLine_t2", {})]),
    "sfx.shield.pickup": ("SFX", "reward", [("sfx.shield.pickup_t2", {"hp": 60})]),
    "sfx.shield.break": ("SFX", "sfx", [("sfx.shield.break_t2", {}), ("sfx.shield.break_t1", {})]),
    "sfx.shield.expire": ("SFX", "soft", [("sfx.shield.expire_t1", {})]),
    "sfx.powerup": ("SFX", "reward", [("sfx.powerup_t1", {})]),
    "sfx.revive": ("SFX", "reward", [("sfx.revive_t1", {})]),
    "sfx.discovery": ("SFX", "reward", [("sfx.discovery_t1", {})]),
    # creature
    "sfx.sailback.chirp": ("Creatures", "creature", [("sfx.sailback.chirp_t1", {}), ("sfx.sailback.chirp_t3", {}), ("sfx.sailback.chirp_t2", {})]),
    "sfx.sailback.sailFlare": ("Creatures", "creature", [("sfx.sailback.sailFlare_t2", {}), ("sfx.sailback.sailFlare_t1", {})]),
    "sfx.sailback.launch": ("Creatures", "creature", [("sfx.sailback.launch_t1", {})]),
    "sfx.sailback.distant": ("Creatures", "soft", [("sfx.sailback.distant_t1", {"stereo": True})]),
    # UI
    "ui.tap": ("UI", "ui", [("ui.tap_t2", {}), ("ui.tap_t1", {})]),
    "ui.back": ("UI", "ui", [("ui.back_t1", {})]),
    "ui.confirm": ("UI", "ui", [("ui.confirm_t2", {})]),
    "ui.toggle": ("UI", "ui", [("ui.toggle_t1", {"hp": 80})]),
    "ui.pause": ("UI", "ui", [("ui.pause_t1", {"hp": 80})]),
    "ui.countdown": ("UI", "ui", [("ui.countdown_t2", {})]),
    "ui.countUp": ("UI", "soft", [("ui.countUp_t2", {"trim": (0.0, 0.2)})]),
    "ui.newRecord": ("UI", "reward", [("ui.newRecord_t2", {})]),
    "ui.objective": ("UI", "ui", [("ui.objective_t1", {})]),
    "ui.learn": ("UI", "reward", [("ui.learn_t2", {})]),
    "ui.toast": ("UI", "ui", [("ui.toast_t1", {})]),
    "ui.denied": ("UI", "ui", [("ui.denied_t1", {"hp": 60})]),
    "amb.cue.risky": ("Ambience", "soft", [("amb.cue.risky_t1", {"hp": 80, "stereo": True})]),
}

STEPS = {"sfx.step.dirt": "sfx.step.dirt_t1", "sfx.step.moss": "sfx.step.moss_t1", "sfx.step.wood": "sfx.step.wood_t1",
         "sfx.step.shallow": "sfx.step.shallow_t1"}

AMB = {  # id -> (take, category, high-shelf cut dB above 8 kHz)
    "amb.forest": ("amb.forest_t1", "amb", -3.0),
    "amb.river": ("amb.river_t2", "amb_water", 0.0),
    "amb.rapids": ("amb.rapids_t1", "amb_water", 0.0),
    "amb.falls.roar": ("amb.falls.roar_t1", "amb_water", -2.0),
    "amb.canopy.wind": ("amb.canopy.wind_t2", "amb", 0.0),
    "amb.underwater": ("amb.underwater_t1", "amb", 0.0),
    "amb.grotto": ("amb.grotto_t1", "amb", 0.0),
    "amb.secret.dripEcho": ("amb.secret.dripEcho_t1", "amb", 0.0),
}

STINGS = {"mus.sting.discovery": "mus.sting.discovery_t1", "mus.sting.secret": "mus.sting.secret_t2",
          "mus.sting.unlock": "mus.sting.unlock_t2", "mus.reveal.vista": "mus.reveal.vista_t1",
          "mus.sting.gameover": "mus.sting.gameover_t1"}

BAR = 2.0  # 120 BPM, 4/4


def decode(path, ch=2):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, ch).astype(np.float64)


def lufs(x):
    """Integrated loudness via ffmpeg ebur128 on a temp wav (handles mono/stereo)."""
    tmp = os.path.join(WORK, "_meas.wav")
    write_wav(tmp, x)
    err = subprocess.run(["ffmpeg", "-nostats", "-i", tmp, "-filter_complex", "ebur128", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    m = re.findall(r"I:\s+(-?[\d.]+) LUFS", err)
    v = float(m[-1]) if m else -70.0
    if v <= -69.9:
        # very short or gated out: fall back to RMS-based estimate (K-weighting ignored)
        v = 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9) - 0.7
    return v


def true_peak(x):
    n = len(x)
    up = []
    for c in range(x.shape[1]):
        X = np.fft.rfft(x[:, c])
        y = np.fft.irfft(X, n * 4) * 4
        up.append(np.abs(y).max())
    return 20 * np.log10(max(up) + 1e-12)


def write_wav(path, x):
    y = np.clip(x, -1, 1)
    pcm = (y * 32767).astype("<i2")
    ch = x.shape[1]
    import wave
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def write_ogg(path, x, quality):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = os.path.join(WORK, "_enc.wav")
    write_wav(tmp, x)
    subprocess.run(["oggenc", "-Q", "-q", str(quality), "-o", path, tmp], check=True)


def ffilter(x, af):
    tmp = os.path.join(WORK, "_flt.wav")
    write_wav_f(tmp, x)
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", tmp, "-af", af, "-f", "f32le", "-"], capture_output=True,
                         check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, x.shape[1]).astype(np.float64)[:len(x)]


def write_wav_f(path, x):
    """32-bit float wav (no clipping before gain staging)."""
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", str(x.shape[1]), "-i", "-",
                    "-c:a", "pcm_f32le", path], input=x.astype(np.float32).tobytes(), check=True)


def highpass(x, hz):
    if not hz:
        return x
    return ffilter(x, f"highpass=f={hz}:poles=2,highpass=f={hz}:poles=2")


def high_shelf(x, hz, gain_db):
    if gain_db == 0:
        return x
    return ffilter(x, f"highshelf=f={hz}:g={gain_db}")


def trim_silence(x, thr_db=-50.0, keep_tail=0.03):
    thr = 10 ** (thr_db / 20)
    m = np.abs(x).max(axis=1)
    idx = np.where(m > thr)[0]
    if len(idx) == 0:
        return x
    a = max(0, idx[0] - int(0.002 * SR))
    b = min(len(x), idx[-1] + int(keep_tail * SR))
    return x[a:b]


def fades(x, fin=0.004, fout=0.025):
    y = x.copy()
    ni, no = int(fin * SR), min(int(fout * SR), len(x) // 2)
    if ni:
        y[:ni] *= np.linspace(0, 1, ni)[:, None]
    if no:
        y[-no:] *= np.linspace(1, 0, no)[:, None] ** 2
    return y


def to_mono(x):
    l, r = x[:, 0], x[:, 1]
    corr = np.corrcoef(l, r)[0, 1] if np.std(l) > 0 and np.std(r) > 0 else 1.0
    if corr < 0.2:
        m = l if np.sqrt(np.mean(l ** 2)) >= np.sqrt(np.mean(r ** 2)) else r
    else:
        m = 0.5 * (l + r)
    return m[:, None], float(corr)


def level(x, target, ceiling=-1.0, limit_db=0.0):
    g = target - lufs(x)
    y = x * 10 ** (g / 20)
    tp = true_peak(y)
    if tp > ceiling:
        over = tp - ceiling
        if limit_db > 0 and over > 1.0:
            # Peaky impacts: let a look-ahead limiter take up to limit_db of the overshoot (transients only).
            take = min(limit_db, over)
            y = y * 10 ** ((ceiling - tp + take) / 20)
            lim = 10 ** ((ceiling - 0.3) / 20)
            pad = np.zeros((int(0.01 * SR), y.shape[1]))
            y = ffilter(np.vstack([y, pad]), f"alimiter=limit={lim:.4f}:attack=1:release=40:level=disabled:asc=1")[:len(y)]
        tp = true_peak(y)
        if tp > ceiling:
            y = y * 10 ** ((ceiling - tp) / 20)
    return y


REPORT = []


def emit(cid, folder, idx, x, quality, extra=None):
    name = cid.replace(".", "_") + (f"_{idx:02d}" if idx else "") + ".ogg"
    path = os.path.join(OUT, folder, name)
    write_ogg(path, x, quality)
    rec = {"id": cid, "file": os.path.relpath(path, ROOT), "dur": round(len(x) / SR, 3), "ch": x.shape[1],
           "lufs": round(lufs(x), 1), "tp": round(true_peak(x), 2), "bytes": os.path.getsize(path)}
    if extra:
        rec.update(extra)
    REPORT.append(rec)
    print(json.dumps(rec))


def build_sfx(only):
    for cid, (folder, cat, takes) in SFX.items():
        if only and not cid.startswith(only):
            continue
        for i, (take, o) in enumerate(takes, 1):
            x = decode(os.path.join(RAW, take + ".mp3"))
            x = x * 0.7  # headroom for the decoder overs (no flat-topped clipping in the sources)
            if "trim" in o:
                x = x[int(o["trim"][0] * SR):int(o["trim"][1] * SR)]
            x = highpass(x, o.get("hp", 30))
            corr = None
            if not o.get("stereo"):
                x, corr = to_mono(x)
            x = trim_silence(x)
            x = fades(x)
            x = level(x, TARGET[cat], limit_db=6.0)
            emit(cid, folder, i, x, 6, {"take": take, "corr": corr})


def build_steps(only):
    for cid, take in STEPS.items():
        if only and not cid.startswith(only):
            continue
        x = decode(os.path.join(RAW, take + ".mp3"))
        x, _ = to_mono(x)
        x = highpass(x, 50)
        m = x[:, 0]
        hop = 220
        n = len(m) // hop
        env = np.sqrt(np.mean(m[:n * hop].reshape(n, hop) ** 2, axis=1))
        envdb = 20 * np.log10(env + 1e-9)
        peak = envdb.max()
        # onsets: rises of > 9 dB within 15 ms above (peak - 30 dB), at least 180 ms apart
        ons = []
        for i in range(3, n):
            if envdb[i] > peak - 30 and envdb[i] - envdb[i - 3] > 9 and (not ons or i - ons[-1] > int(0.26 * SR / hop)):
                ons.append(i)
        cuts = []
        for k, i in enumerate(ons):
            a = max(0, i * hop - int(0.012 * SR))
            b = (ons[k + 1] * hop - int(0.015 * SR)) if k + 1 < len(ons) else len(m)
            b = min(b, a + int(0.32 * SR))
            seg = x[a:b]
            if len(seg) > int(0.08 * SR):
                cuts.append(seg)
        # keep the 6 most consistent steps (closest to the median RMS)
        rms = np.array([np.sqrt(np.mean(c ** 2)) for c in cuts])
        if len(cuts) == 0:
            print("NO STEPS", cid)
            continue
        med = np.median(rms)
        order = np.argsort(np.abs(np.log(rms / med)))[:6]
        for j, k in enumerate(sorted(order), 1):
            seg = fades(trim_silence(cuts[k], -55), 0.002, 0.03)
            seg = level(seg, TARGET["step"], limit_db=4.0)
            emit(cid, "Footsteps", j, seg, 6, {"take": take, "onsets": len(ons)})


def seam_loop(x, xf):
    """Pre-made loop: blend the first xf seconds into the tail; result wraps continuously."""
    n = int(xf * SR)
    head, body = x[:n], x[n:].copy()
    t = np.linspace(0, 1, n)[:, None]
    body[-n:] = body[-n:] * np.cos(t * np.pi / 2) + head * np.sin(t * np.pi / 2)
    return body


def build_amb(only):
    for cid, (take, cat, shelf) in AMB.items():
        if only and not cid.startswith(only):
            continue
        x = decode(os.path.join(RAW, take + ".mp3"))
        x = highpass(x, 30)
        x = high_shelf(x, 8000, shelf)
        x = seam_loop(x, 0.25)
        x = level(x, TARGET[cat], -3.0)
        emit(cid, "Ambience", 0, x, 4, {"take": take})


def cut_loop(x, start, end, xf=0.5, pre=True):
    """Bar-exact loop [start, end) seconds. pre=True: the last xf seconds are blended with the material just before
    start (wrap continues naturally into start). pre=False: the first xf seconds are blended with the material just
    after end."""
    a, b, n = int(round(start * SR)), int(round(end * SR)), int(xf * SR)
    y = x[a:b].copy()
    t = np.linspace(0, 1, n)[:, None]
    if pre:
        y[-n:] = y[-n:] * np.cos(t * np.pi / 2) + x[a - n:a] * np.sin(t * np.pi / 2)
    else:
        y[:n] = y[:n] * np.sin(t * np.pi / 2) + x[b:b + n] * np.cos(t * np.pi / 2)
    return y


def build_music(only):
    if only and not only.startswith("mus"):
        return
    st = lambda d, s: decode(os.path.join(STEMS, d, s + ".mp3"))
    # Explore set from the main theme (24..72 s, 24 bars) + intro (0..24 s)
    mo, mb, md = st("theme_main", "other"), st("theme_main", "bass"), st("theme_main", "drums")
    n = min(len(mo), len(mb), len(md))
    mo, mb, md = mo[:n], mb[:n], md[:n]
    bass_g, drum_g = 10 ** (-3 / 20), 10 ** (-8 / 20)
    melody = mo + mb * bass_g
    full_explore = melody + md * drum_g
    # Danger section (conditioned variation, 16..48 s) and the percussion layer (candidate B drums, 32..64 s)
    do_, db_, dd = st("theme_danger", "other"), st("theme_danger", "bass"), st("theme_danger", "drums")
    n2 = min(len(do_), len(db_), len(dd))
    danger = do_[:n2] + db_[:n2] * bass_g + dd[:n2] * 10 ** (-5 / 20)
    perc = st("candB", "drums")
    # One common gain for the whole set: the explore mix (L1) sits at the music target.
    g = 10 ** ((TARGET["music"] - lufs(full_explore[int(24 * SR):int(72 * SR)])) / 20)
    parts = {
        "mus.theme.intro": full_explore[:int(24 * SR)] * g,
        "mus.explore.melody": cut_loop(melody, 24.0, 72.0) * g,
        "mus.explore.drums": cut_loop(md * 10 ** (-2 / 20), 24.0, 72.0) * g,  # played at 0.5 (explore) / 0.9 (risky)
        "mus.danger": cut_loop(danger, 16.0, 48.0, pre=False) * g,
        "mus.layer.perc": cut_loop(perc, 32.0, 64.0) * g * 10 ** (-4 / 20),
    }
    intro = parts["mus.theme.intro"]
    intro[:int(0.01 * SR)] *= np.linspace(0, 1, int(0.01 * SR))[:, None]
    peak = max(true_peak(v) for v in parts.values())
    if peak > -1.0:  # keep relative gains: scale the whole set
        s = 10 ** ((-1.0 - peak) / 20)
        parts = {k: v * s for k, v in parts.items()}
    for cid, x in parts.items():
        if cid in ("mus.explore.drums",):
            x, _ = to_mono(x)  # drum stem is mono (corr 0.99)
        emit(cid, "Music", 0, x, 4, {"loop": cid != "mus.theme.intro", "bars": round(len(x) / SR / BAR, 3)})
    # Results loop (4..36 s, 16 bars; the tail is blended with the bar before the start)
    r = decode(os.path.join(RAW, "mus.results_t1.mp3"))
    r = cut_loop(r, 4.0, 36.0)
    emit("mus.results", "Music", 0, level(r, TARGET["music"] - 2), 4, {"loop": True, "bars": 16})
    for cid, take in STINGS.items():
        x = decode(os.path.join(RAW, take + ".mp3"))
        x = fades(trim_silence(x, -55, 0.05), 0.003, 0.25)
        emit(cid, "Music", 0, level(x, TARGET["sting"]), 4, {"take": take})


def main():
    only = None
    if "--only" in sys.argv:
        only = sys.argv[sys.argv.index("--only") + 1]
    os.makedirs(WORK, exist_ok=True)
    build_music(only)
    build_amb(only)
    build_steps(only)
    build_sfx(only)
    with open(os.path.join(WORK, "build_report.jsonl"), "w") as f:
        for r in REPORT:
            f.write(json.dumps(r) + "\n")


if __name__ == "__main__":
    main()
