#!/usr/bin/env python3
"""Objective "listen check" for generated audio (numpy + ffmpeg only).

For each file: duration, integrated loudness (LUFS) and true peak (ffmpeg ebur128), sample peak, clipped-sample
count, lead/tail silence, spectral centroid and band energy split (low <250 Hz, mid 250-4k, high >4k), onset rate
and an autocorrelation tempo estimate, stereo correlation, a coarse loudness envelope (8 segments) and, with --loop,
the seam: RMS jump and spectral distance between the last and first 50 ms, and the sample discontinuity at the wrap.

Usage: audio_analyze.py [--loop] [--json] files...
"""
import json
import re
import subprocess
import sys

import numpy as np

SR = 44100


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2)


def ebur128(path):
    err = subprocess.run(["ffmpeg", "-nostats", "-i", path, "-filter_complex", "ebur128=peak=true", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    summary = err[err.rfind("Summary:"):]
    i = re.search(r"I:\s+(-?[\d.]+|-inf) LUFS", summary)
    tp = re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", summary)
    lra = re.search(r"LRA:\s+(-?[\d.]+) LU", summary)
    f = lambda m: float(m.group(1)) if m and m.group(1) != "-inf" else float("-inf")
    return f(i), f(tp), f(lra)


def db(x):
    return 20 * np.log10(max(float(x), 1e-9))


def spectrum(mono):
    n = len(mono)
    if n < 2048:
        mono = np.pad(mono, (0, 2048 - n))
    win = np.hanning(len(mono))
    mag = np.abs(np.fft.rfft(mono * win)) ** 2
    freqs = np.fft.rfftfreq(len(mono), 1 / SR)
    return freqs, mag


def bands(freqs, mag):
    tot = mag.sum() + 1e-12
    low = mag[freqs < 250].sum() / tot
    high = mag[freqs > 4000].sum() / tot
    centroid = float((freqs * mag).sum() / tot)
    return low, 1 - low - high, high, centroid


def onsets(mono):
    hop = 512
    frames = len(mono) // hop
    if frames < 4:
        return 0.0, 0.0
    env = np.sqrt(np.mean(mono[: frames * hop].reshape(frames, hop) ** 2, axis=1))
    flux = np.maximum(0, np.diff(np.log(env + 1e-6)))
    thr = np.mean(flux) + 1.5 * np.std(flux)
    peaks = [i for i in range(1, len(flux) - 1) if flux[i] > thr and flux[i] >= flux[i - 1] and flux[i] >= flux[i + 1]]
    rate = len(peaks) / (len(mono) / SR)
    # tempo: autocorrelation of the flux between 70 and 180 BPM
    f = flux - flux.mean()
    ac = np.correlate(f, f, mode="full")[len(f) - 1:]
    fps = SR / hop
    lo, hi = int(fps * 60 / 180), int(fps * 60 / 70)
    bpm = 0.0
    if hi < len(ac):
        lag = lo + int(np.argmax(ac[lo:hi]))
        bpm = 60 * fps / lag
    return rate, bpm


def analyze(path, loop):
    x = decode(path)
    mono = x.mean(axis=1)
    dur = len(x) / SR
    lufs, tp, lra = ebur128(path)
    peak = np.abs(x).max()
    clipped = int((np.abs(x) >= 0.999).sum())
    thr = 10 ** (-50 / 20)
    nz = np.where(np.abs(mono) > thr)[0]
    lead = nz[0] / SR if len(nz) else dur
    tail = (len(mono) - nz[-1]) / SR if len(nz) else dur
    freqs, mag = spectrum(mono)
    low, mid, high, centroid = bands(freqs, mag)
    rate, bpm = onsets(mono)
    l, r = x[:, 0], x[:, 1]
    corr = float(np.corrcoef(l, r)[0, 1]) if np.std(l) > 0 and np.std(r) > 0 else 1.0
    seg = np.array_split(mono, 8)
    env = [float(round(db(np.sqrt(np.mean(s ** 2))), 1)) for s in seg]
    out = {"file": path.split("/")[-1], "dur": round(dur, 2), "lufs": lufs, "tp": tp, "lra": lra,
           "peak_db": round(db(peak), 2), "clipped": clipped, "lead_s": round(lead, 3), "tail_s": round(tail, 3),
           "low": round(low, 2), "mid": round(mid, 2), "high": round(high, 2), "centroid": int(centroid),
           "onsets_per_s": round(rate, 2), "bpm": round(bpm, 1), "stereo_corr": round(corr, 2), "env": env}
    if loop:
        n = int(0.05 * SR)
        a, b = mono[-n:], mono[:n]
        ra, rb = np.sqrt(np.mean(a ** 2)), np.sqrt(np.mean(b ** 2))
        _, ma = spectrum(a)
        _, mb = spectrum(b)
        ma, mb = np.log(ma + 1e-9), np.log(mb + 1e-9)
        sd = float(np.sqrt(np.mean((ma - mb) ** 2)))
        jump = float(np.abs(x[-1] - x[0]).max())
        out.update({"seam_rms_jump_db": round(db(rb) - db(ra), 2), "seam_spec_dist": round(sd, 2),
                    "seam_sample_jump": round(jump, 4)})
    return out


def detail(path, step):
    """Per-window stats: RMS dB, centroid Hz, low/high share, onsets/s (to describe structure over time)."""
    x = decode(path)
    mono = x.mean(axis=1)
    n = int(step * SR)
    rows = []
    for i in range(0, len(mono) - n // 2, n):
        w = mono[i:i + n]
        f, m = spectrum(w)
        low, mid, high, c = bands(f, m)
        rate, _ = onsets(w)
        rows.append(f"{i / SR:5.1f}s rms={db(np.sqrt(np.mean(w ** 2))):6.1f} cen={int(c):5d} low={low:.2f} high={high:.3f} ons={rate:4.1f}")
    return rows


def main():
    args = sys.argv[1:]
    loop = "--loop" in args
    as_json = "--json" in args
    files = [a for a in args if not a.startswith("--")]
    for a in args:
        if a.startswith("--detail="):
            for f in files:
                print(f)
                print("\n".join(detail(f, float(a.split("=")[1]))))
            return
    for f in files:
        r = analyze(f, loop)
        if as_json:
            print(json.dumps(r))
        else:
            print(" ".join(f"{k}={v}" for k, v in r.items()))


if __name__ == "__main__":
    main()
