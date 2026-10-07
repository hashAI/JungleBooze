"""T401 study: runs every check of docs/specs/004-fixed-pivot-swing.md against pendulum_model.py.

    python3 -I pendulum_study.py            # compute everything, write out/pendulum_study.json + the tables CSV
    python3 -I pendulum_study.py --report   # also (re)write docs/sim-reports/2026-10-07-fixed-pivot-swing.md

All random streams are seeded (SEEDS below); a failing case prints the seed and parameters that reproduce it.
Takes about 2 to 4 minutes (pure Python).
"""

import json
import math
import os
import random
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pendulum_model as pm  # noqa: E402
from pendulum_model import (  # noqa: E402
    DEFAULT as P, SwingTrace, release_at, grade_for_tick, resolve_swipe, run_swing, det_sin, det_cos,
    guide_chain, chain_catch, takeoff_window, catch_speed, apex_angle_deg, quarter_period_s, slack_angle_deg,
)

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
CSV_PATH = os.path.join(REPO, "docs", "sim-reports", "data", "2026-10-07-fixed-pivot-swing-tables.csv")

SEEDS = dict(fuzz=40101, energy=40102, chain=40103, bots=40104, f32=40105)
ENTRY_SPEEDS = [8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28]
VC_LIST = [13, 14, 15, 16]
Z_GRAB = -P.zone_half_m          # canonical grab: first tick of the zone, z = sP - 1.25
FAR = P.far_edge_m


def past_edge(rel):
    return rel.z_land - FAR


# ------------------------------------------------------------------------------------------ tables
def tables():
    rows = []
    for v in ENTRY_SPEEDS:
        tr = SwingTrace(v, Z_GRAB)
        zg = tr.hand_z(0)
        for k in range(P.good_start_tick, tr.k_apex + 1):
            gr = grade_for_tick(k, tr.k_apex)
            r = release_at(tr, k, gr)
            rows.append(dict(vIn=v, vC=tr.v_c, tick=k, ms=round(k * 1000.0 / 60, 1), grade=gr,
                             rope_deg=r.rope_deg, grab_to_release_m=r.z0 - zg, vt=r.vt, vx=r.vx, vy=r.vy,
                             speed=math.hypot(r.vx, r.vy), launch_deg=math.degrees(math.atan2(r.vy, r.vx)),
                             feet_y0=r.y0, flight_s=r.t_land, land_m=r.z_land, past_edge_m=r.z_land - FAR))
        r = release_at(tr, tr.k_apex, "Poor", auto=True)
        rows.append(dict(vIn=v, vC=tr.v_c, tick=tr.k_apex, ms=round(tr.k_apex * 1000.0 / 60, 1), grade="Poor(auto)",
                         rope_deg=r.rope_deg, grab_to_release_m=r.z0 - zg, vt=r.vt, vx=r.vx, vy=r.vy,
                         speed=math.hypot(r.vx, r.vy), launch_deg=math.degrees(math.atan2(r.vy, r.vx)),
                         feet_y0=r.y0, flight_s=r.t_land, land_m=r.z_land, past_edge_m=r.z_land - FAR))
    return rows


def write_csv(rows):
    os.makedirs(os.path.dirname(CSV_PATH), exist_ok=True)
    cols = ["vIn", "vC", "tick", "ms", "grade", "rope_deg", "grab_to_release_m", "vt", "vx", "vy", "speed",
            "launch_deg", "feet_y0", "flight_s", "land_m", "past_edge_m"]
    with open(CSV_PATH, "w") as f:
        f.write(",".join(cols) + "\n")
        for r in rows:
            f.write(",".join((("%.4f" % r[c]) if isinstance(r[c], float) else str(r[c])) for c in cols) + "\n")


def canonical(vc):
    """Per-vC summary rows used by the report (canonical grab z = sP - 1.25)."""
    tr = SwingTrace(vc, Z_GRAB)
    zg = tr.hand_z(0)
    out = dict(vC=vc, k_apex=tr.k_apex, theta0_deg=math.degrees(float(tr.theta[0])),
               apex_deg=math.degrees(max(float(t) for t in tr.theta)),
               apex_deg_closed=apex_angle_deg(vc),
               apex_hand_y=tr.hand_y(tr.k_apex), apex_feet_y=tr.feet_y(tr.k_apex),
               apex_ms=tr.k_apex * 1000.0 / 60, t4_closed_ticks=quarter_period_s(vc) * 60, rel={})
    for name, k, gr in (("early", P.good_start_tick, "Good"), ("p_first", P.perfect_start_tick, "Perfect"),
                        ("p_center", 47, "Perfect"), ("p_last", P.perfect_end_tick - 1, "Perfect"),
                        ("late", 66, "Good"), ("good_max", P.perfect_start_tick - 1, "Good")):
        r = release_at(tr, k, gr)
        out["rel"][name] = rel_dict(r, zg)
    r = release_at(tr, tr.k_apex, "Poor", auto=True)
    out["rel"]["poor"] = rel_dict(r, zg)
    # extremes over all valid swipe ticks
    allr = [(k, release_at(tr, k, grade_for_tick(k, tr.k_apex))) for k in range(P.good_start_tick, tr.k_apex + 1)]
    out["min_land"] = min((past_edge(r), k) for k, r in allr)
    out["max_land"] = max((past_edge(r), k) for k, r in allr)
    per = [past_edge(r) for k, r in allr if P.perfect_start_tick <= k < P.perfect_end_tick]
    good = [past_edge(r) for k, r in allr if not (P.perfect_start_tick <= k < P.perfect_end_tick)]
    out["perfect_range"] = (min(per), max(per))
    out["good_max_land"] = max(good)
    out["good_min_land"] = min(good)
    out["grab_to_release_range"] = (min(r.z0 - zg for k, r in allr), max(r.z0 - zg for k, r in allr))
    out["min_tension"] = min(tr.tension(n) for n in range(tr.k_apex + 1))
    return out


def rel_dict(r, zg):
    return dict(tick=r.tick, rope_deg=r.rope_deg, dist=r.z0 - zg, vx=r.vx, vy=r.vy, vt=r.vt, y0=r.y0, t=r.t_land,
                land=r.z_land, past=r.z_land - FAR, speed=math.hypot(r.vx, r.vy),
                launch_deg=math.degrees(math.atan2(r.vy, r.vx)), z0=r.z0)


# ------------------------------------------------------------------------------------------ S-401 fuzz
def fuzz(n_off=1000, wide=False):
    """vIn 8..28 step 0.5, n_off random grab offsets each, every release tick 27..apex plus auto."""
    rng = random.Random(SEEDS["fuzz"] + (1 if wide else 0))
    lo_land, hi_land = (1e9, None), (-1e9, None)
    deaths = 0
    total = 0
    k_apexes = set()
    worst_spd = {}
    min_perfect_gap = 1e9
    min_tension = 1e9
    for i in range(41):
        v = 8.0 + 0.5 * i
        for j in range(n_off):
            z_rel = rng.uniform(-P.zone_half_m, P.zone_half_m) if wide else rng.uniform(
                -P.zone_half_m, -P.zone_half_m + v / 60.0)
            tr = SwingTrace(v, z_rel)
            k_apexes.add(tr.k_apex)
            min_tension = min(min_tension, min(tr.tension(n) for n in range(tr.k_apex + 1)))
            best_perfect = None
            for k in range(P.good_start_tick, tr.k_apex + 1):
                gr = grade_for_tick(k, tr.k_apex)
                r = release_at(tr, k, gr)
                total += 1
                if r.z_land < lo_land[0]:
                    lo_land = (r.z_land, dict(vIn=v, z_rel=z_rel, tick=k, seed=SEEDS["fuzz"] + (1 if wide else 0), j=j))
                if r.z_land > hi_land[0]:
                    hi_land = (r.z_land, dict(vIn=v, z_rel=z_rel, tick=k, j=j))
                if r.z_land <= FAR + 0.25 or r.z_land > 30.0 or r.z_land < 14.5:
                    deaths += 1
            r = release_at(tr, tr.k_apex, "Poor", auto=True)
            total += 1
            if r.z_land < lo_land[0]:
                lo_land = (r.z_land, dict(vIn=v, z_rel=z_rel, tick="auto", j=j))
            if r.z_land > hi_land[0]:
                hi_land = (r.z_land, dict(vIn=v, z_rel=z_rel, tick="auto", j=j))
            if r.z_land <= FAR + 0.25 or r.z_land > 30.0 or r.z_land < 14.5:
                deaths += 1
    return dict(total=total, bad=deaths, min_land=lo_land, max_land=hi_land, k_apex_set=sorted(k_apexes),
                min_tension=min_tension)


# ------------------------------------------------------------------------------------------ S-404 energy / angle
def energy_drift(F=float, n_states=1000):
    rng = random.Random(SEEDS["energy"])
    worst = (0.0, None)
    worst_theta = 0.0
    for i in range(n_states):
        v = rng.uniform(0.0, 40.0)
        z_rel = rng.uniform(-P.zone_half_m, P.zone_half_m)
        tr = SwingTrace(v, z_rel, max_ticks=100, F=F)
        # unreleased: keep integrating past the apex (SwingTrace stops at omega <= 0), so build a free 100-tick run
        th, om = free_swing(v, z_rel, 100, F)
        e = [0.5 * P.rope_length_m ** 2 * float(o) ** 2 + P.swing_gravity_mps2 * P.rope_length_m * (1 - math.cos(float(t)))
             for t, o in zip(th, om)]
        d = max(abs(x - e[0]) / e[0] for x in e)
        if d > worst[0]:
            worst = (d, dict(vIn=v, z_rel=z_rel))
        worst_theta = max(worst_theta, max(abs(float(t)) for t in th))
    return dict(max_rel_drift=worst[0], at=worst[1], max_theta_deg=math.degrees(worst_theta))


def free_swing(v_in, z_rel, n, F=float):
    L = F(P.rope_length_m)
    gl = F(P.swing_gravity_mps2) / L
    dt = F(1.0) / F(60)
    th = F(pm.start_angle(z_rel))
    om = F(catch_speed(v_in)) / L
    ths, oms = [th], [om]
    for _ in range(n):
        om = om - gl * det_sin(th, F) * dt
        th = th + om * dt
        ths.append(th)
        oms.append(om)
    return ths, oms


def drift_profile(vc, F=float):
    th, om = free_swing(vc, Z_GRAB, 100, F)
    e = [0.5 * P.rope_length_m ** 2 * float(o) ** 2 + P.swing_gravity_mps2 * P.rope_length_m * (1 - math.cos(float(t)))
         for t, o in zip(th, om)]
    return max(abs(x - e[0]) / e[0] for x in e)


# ------------------------------------------------------------------------------------------ slack
def slack_table():
    rows = []
    for v in (8, 10, 13, 16, 18, 21, 24, 28):
        c = 1.0 - v * v / (2 * P.swing_gravity_mps2 * P.rope_length_m)
        sa = slack_angle_deg(v)
        # simulate an UNCLAMPED pendulum with omega0 = v/L from the bottom-ish start, 140 ticks, min tension
        th, om = free_unclamped(v, 140)
        ten = [P.rope_length_m * float(o) ** 2 + P.swing_gravity_mps2 * math.cos(float(t)) for t, o in zip(th, om)]
        first_slack = next((i for i, x in enumerate(ten) if x < 0), None)
        rows.append(dict(v=v, ratio=v * v / (2 * P.swing_gravity_mps2 * P.rope_length_m), cos_max=c,
                         apex_deg=apex_angle_deg(v), slack_deg=sa, min_tension=min(ten), first_slack_tick=first_slack,
                         sim_peak_deg=math.degrees(max(float(t) for t in th))))
    return rows


def free_unclamped(v, n):
    """Integrator without the det_sin range (math.sin) since unclamped angles exceed 1.3 rad."""
    L = P.rope_length_m
    gl = P.swing_gravity_mps2 / L
    dt = 1 / 60.0
    th = pm.start_angle(Z_GRAB)
    om = v / L
    ths, oms = [th], [om]
    for _ in range(n):
        om -= gl * math.sin(th) * dt
        th += om * dt
        ths.append(th)
        oms.append(om)
    return ths, oms


# ------------------------------------------------------------------------------------------ chains (S-405)
def chain_trials(n=10000):
    rng = random.Random(SEEDS["chain"])
    out = {}
    for grade in ("Perfect", "Good", "Poor"):
        for vc in VC_LIST:
            Ts, feet, succ, vxs, gy, gz, tclamp_lo, tclamp_hi = [], [], 0, [], [], [], 0, 0
            vc_next = set()
            worst_fail = None
            for i in range(n):
                z_rel = rng.uniform(-P.zone_half_m, -P.zone_half_m + vc / 60.0)
                tr = SwingTrace(vc, z_rel)
                if grade == "Poor":
                    r = release_at(tr, tr.k_apex, "Poor", auto=True)
                elif grade == "Perfect":
                    k = rng.randrange(P.perfect_start_tick, P.perfect_end_tick)
                    r = release_at(tr, k, "Perfect")
                else:
                    ks = [k for k in range(P.good_start_tick, tr.k_apex + 1)
                          if not (P.perfect_start_tick <= k < P.perfect_end_tick)]
                    r = release_at(tr, rng.choice(ks), "Good")
                T, vx, vy = guide_chain(r, P.chain_spacing_m)
                c = chain_catch(r, T, vx, vy)
                Ts.append(T)
                vxs.append(vx)
                if c["ok"]:
                    succ += 1
                    gy.append(c["y"])
                    gz.append(c["z_rel_B"])
                elif worst_fail is None:
                    worst_fail = dict(i=i, z_rel=z_rel, tick=r.tick, T=T, vx=vx, vy=vy, c=c)
                t = T
                feet.append(r.y0 + vy * t - 0.5 * 16.0 * t * t)  # arrival feet at Z (should be 1.5)
                tclamp_lo += (T <= P.chain_flight_min_s + 1e-12)
                tclamp_hi += (T >= P.chain_flight_max_s - 1e-12)
                vc_next.add(catch_speed(vx))
            out["%s/%d" % (grade, vc)] = dict(
                grade=grade, vc=vc, n=n, success=succ, T_min=min(Ts), T_max=max(Ts), vx_min=min(vxs), vx_max=max(vxs),
                feet_min=min(feet), feet_max=max(feet), grab_y_min=min(gy) if gy else None,
                grab_y_max=max(gy) if gy else None, grab_z_min=min(gz) if gz else None, grab_z_max=max(gz) if gz else None,
                clamp_lo=tclamp_lo, clamp_hi=tclamp_hi, vc_next=sorted(vc_next), worst_fail=worst_fail)
    return out


# ------------------------------------------------------------------------------------------ take-off windows (S-406)
def takeoff_table():
    rows = []
    for v in ENTRY_SPEEDS:
        w = takeoff_window(v)
        w450 = takeoff_window(v, earliness_ms=450)
        rim = -P.chasm_rim_before_pivot_m
        formula_len_m = (rim + 0.08 * v) - (-P.zone_half_m - 0.55 * v)
        rows.append(dict(v=v, ticks=w["n"], ms=w["ms"], lo=w["lo"], hi=w["hi"], ms450=w450["ms"],
                         formula_ms=formula_len_m / v * 1000.0, contiguous=w["contiguous"]))
    return rows


def window_sets(speeds):
    return dict((v, takeoff_window(v)) for v in speeds)


# ------------------------------------------------------------------------------------------ bots
PROFILES = {
    # name: (release sigma ms, jump sigma ms, decision error rate) - jump sigma and error rate are the
    # timing_sigma_ms and error_rate of the existing tools/sim/bots.py profiles
    "expert": (70.0, 33.0, 0.002),
    "average": (180.0, 67.0, 0.005),
    "new": (260.0, 100.0, 0.02),
}
BOT_SPEEDS = [12.0, 13.5, 15.5, 17.5, 19.0, 21.0]


def phi(x):
    return 0.5 * (1.0 + math.erf(x / math.sqrt(2.0)))


def swipe_mu_ms():
    """Window centre in ticks: Perfect ticks 42..52 -> tick 47 -> time of tick 47 start."""
    c = (P.perfect_start_tick + P.perfect_end_tick - 1) / 2.0
    return c * 1000.0 / 60.0


def analytic_grades(sigma_ms, k_apex):
    """Exact probabilities (no sampling error) for swipe tick s = round(N(mu, sigma) * 0.06)."""
    mu = swipe_mu_ms()

    def cdf_tick_le(k):  # P(round(x*0.06) <= k) = P(x*0.06 < k + 0.5)
        return phi(((k + 0.5) * 1000.0 / 60.0 - mu) / sigma_ms)
    perfect = cdf_tick_le(P.perfect_end_tick - 1) - cdf_tick_le(P.perfect_start_tick - 1)
    buf_lo = P.good_start_tick - P.release_buffer_ticks  # 18: earliest swipe that still fires at 27
    good = (cdf_tick_le(P.perfect_start_tick - 1) - cdf_tick_le(buf_lo - 1)) + (cdf_tick_le(k_apex) - cdf_tick_le(P.perfect_end_tick - 1))
    poor = 1.0 - perfect - good
    return perfect, good, poor


def bots(n=20000, jump_sigma_scale=1.0):
    rng = random.Random(SEEDS["bots"])
    wins = window_sets(BOT_SPEEDS)
    k_apex = {13: SwingTrace(13, Z_GRAB).k_apex, 14: SwingTrace(14, Z_GRAB).k_apex,
              15: SwingTrace(15, Z_GRAB).k_apex, 16: SwingTrace(16, Z_GRAB).k_apex}
    mu = swipe_mu_ms()
    res = {}
    for name, (sr, sj, err) in PROFILES.items():
        per_speed = {}
        tot = dict(Perfect=0, Good=0, Poor=0, Miss=0, n=0)
        for v in BOT_SPEEDS:
            w = wins[v]
            tk = w["ticks"]
            center = tk[len(tk) // 2]
            ka = k_apex[int(catch_speed(v))]
            c = dict(Perfect=0, Good=0, Poor=0, Miss=0, n=0, grab=0)
            for _ in range(n):
                c["n"] += 1
                if rng.random() < err:
                    c["Miss"] += 1
                    continue
                j = center + int(round(rng.gauss(0.0, sj * jump_sigma_scale) * 0.06))
                if j not in w["ticks"]:
                    c["Miss"] += 1
                    continue
                c["grab"] += 1
                s = int(round((mu + rng.gauss(0.0, sr)) * 0.06))
                rt, gr = resolve_swipe(s, ka)
                c[gr] += 1
            per_speed[v] = c
            for k in tot:
                tot[k] += c[k]
        res[name] = dict(per_speed=per_speed, total=tot)
    return res


def analytic_bot_table():
    out = {}
    for name, (sr, sj, err) in PROFILES.items():
        ka = SwingTrace(15, Z_GRAB).k_apex
        out[name] = analytic_grades(sr, ka)
    return out


# ------------------------------------------------------------------------------------------ float32 vs float64
def f32_vs_f64(n=1000):
    import numpy as np
    rng = random.Random(SEEDS["f32"])
    max_dth = max_dom = max_dland = max_dz = max_dy = 0.0
    k_diff = 0
    max_dth_at = None
    for i in range(n):
        v = rng.uniform(8.0, 28.0)
        z_rel = rng.uniform(-P.zone_half_m, -P.zone_half_m + v / 60.0)
        a = SwingTrace(v, z_rel, F=float)
        b = SwingTrace(v, z_rel, F=np.float32)
        if a.k_apex != b.k_apex:
            k_diff += 1
        m = min(a.k_apex, b.k_apex)
        for k in range(m + 1):
            dth = abs(float(b.theta[k]) - a.theta[k])
            if dth > max_dth:
                max_dth, max_dth_at = dth, dict(vIn=v, z_rel=z_rel, tick=k)
            max_dom = max(max_dom, abs(float(b.omega[k]) - a.omega[k]))
            max_dz = max(max_dz, abs(b.hand_z(k) - a.hand_z(k)))
            max_dy = max(max_dy, abs(b.hand_y(k) - a.hand_y(k)))
        k = 47
        ra = release_at(a, k, "Perfect")
        rb = release_at(b, k, "Perfect")
        max_dland = max(max_dland, abs(ra.z_land - rb.z_land))
    return dict(n=n, k_apex_mismatch=k_diff, max_dtheta=max_dth, at=max_dth_at, max_domega=max_dom, max_dz=max_dz,
                max_dy=max_dy, max_dland=max_dland)


# ------------------------------------------------------------------------------------------ cost of a swing (S-407)
def swing_cost():
    rows = []
    for v in (10.0, 15.0, 21.0):
        vc = catch_speed(v)
        tr = SwingTrace(v, Z_GRAB)
        zg = tr.hand_z(0)
        for name, k, gr, auto in (("Perfect", 47, "Perfect", False), ("Poor", tr.k_apex, "Poor", True)):
            r = release_at(tr, k, gr, auto=auto)
            t_air = (r.anchor + 1 if not auto else r.anchor) / 60.0 + r.t_land  # grab tick to landing
            t_air = r.anchor / 60.0 + r.t_land
            dist = r.z_land - zg
            rows.append(dict(v=v, grade=name, t=t_air, dist=dist, run_dist=v * t_air, loss_m=v * t_air - dist,
                             loss_s=(v * t_air - dist) / v))
    return rows



# ------------------------------------------------------------------------------------------ extras
def sens_bots():
    """Exact (analytic) Perfect share vs release sigma and Perfect width (ticks), swipe centred on the window."""
    out = {}
    ka = SwingTrace(15, Z_GRAB).k_apex
    for width in (9, 11, 13):
        saved = (P.perfect_start_ms, P.perfect_width_ms)
        start = 47 - width // 2  # centred on tick 47
        P.perfect_start_ms = start * 1000.0 / 60.0
        P.perfect_width_ms = width * 1000.0 / 60.0
        row = {}
        mu = (start + start + width - 1) / 2.0 * 1000.0 / 60.0
        for sg in (70, 100, 120, 150, 180, 220, 260):
            def cdf(k):
                return phi(((k + 0.5) * 1000.0 / 60.0 - mu) / sg)
            row[sg] = cdf(start + width - 1) - cdf(start - 1)
        P.perfect_start_ms, P.perfect_width_ms = saved
        out[width] = row
    return out


def what_if():
    """Candidate parameter changes (NOT applied anywhere): landing past the far edge over all valid releases."""
    cands = [("baseline (spec 004)", {}),
             ("CatchMaxSpeedMps 15", dict(catch_max_speed_mps=15.0)),
             ("PerfectImpulseMps 2.0", dict(perfect_impulse_mps=2.0)),
             ("GoodStartMs 500", dict(good_start_ms=500.0)),
             ("GoodStartMs 500 + PerfectImpulseMps 2.0", dict(good_start_ms=500.0, perfect_impulse_mps=2.0)),
             ("CatchMaxSpeedMps 15 + GoodStartMs 500", dict(catch_max_speed_mps=15.0, good_start_ms=500.0))]
    out = []
    for name, ov in cands:
        q = pm.Params(**ov)
        mn, mx_perf, mn_perf, mx_good = 1e9, -1e9, 1e9, -1e9
        ladder = 1e9
        vcs = [13.0 + i * 0.5 for i in range(int((q.catch_max_speed_mps - 13.0) / 0.5) + 1)]
        for vc in vcs:
            tr = SwingTrace(vc, Z_GRAB, q)
            vp, vg = [], []
            for k in range(q.good_start_tick, tr.k_apex + 1):
                gr = grade_for_tick(k, tr.k_apex, q)
                r = release_at(tr, k, gr, q)
                pe = r.z_land - q.far_edge_m
                mn = min(mn, pe)
                if gr == "Perfect":
                    mx_perf, mn_perf = max(mx_perf, pe), min(mn_perf, pe)
                    vp.append(pe)
                else:
                    mx_good = max(mx_good, pe)
                    vg.append(pe)
            ladder = min(ladder, min(vp) - max(vg))
            r = release_at(tr, tr.k_apex, "Poor", q, auto=True)
            mn = min(mn, r.z_land - q.far_edge_m)
        out.append(dict(name=name, min_past=mn, perfect_min=mn_perf, perfect_max=mx_perf, good_max=mx_good,
                        ladder=ladder))
    return out


def verlet_drift(vc=14.0):
    """Velocity Verlet (kick-drift-kick) energy drift for comparison; NOT the spec integrator."""
    L, g, dt = P.rope_length_m, P.swing_gravity_mps2, 1 / 60.0
    th, om = pm.start_angle(Z_GRAB), vc / L
    e0 = 0.5 * L * L * om * om + g * L * (1 - math.cos(th))
    worst = 0.0
    for _ in range(100):
        om -= 0.5 * (g / L) * math.sin(th) * dt
        th += om * dt
        om -= 0.5 * (g / L) * math.sin(th) * dt
        e = 0.5 * L * L * om * om + g * L * (1 - math.cos(th))
        worst = max(worst, abs(e - e0) / e0)
    return worst


def long_run_drift(vc=14.0, n=6000):
    """Is the symplectic Euler energy error bounded (no secular growth)? Max |dE|/E0 per 600-tick block."""
    th, om = free_swing(vc, Z_GRAB, n)
    e = [0.5 * P.rope_length_m ** 2 * float(o) ** 2 + P.swing_gravity_mps2 * P.rope_length_m * (1 - math.cos(float(t)))
         for t, o in zip(th, om)]
    return [max(abs(x - e[0]) / e[0] for x in e[i:i + 600]) for i in range(0, n, 600)]


def perfect_flight_max_feet():
    out = {}
    for vc in VC_LIST:
        tr = SwingTrace(vc, Z_GRAB)
        r = release_at(tr, 47, "Perfect")
        out[vc] = r.y0 + r.vy ** 2 / (2 * P.launch_gravity_mps2)
    return out


def chain_grab_z_poor():
    out = {}
    for vc in VC_LIST:
        tr = SwingTrace(vc, Z_GRAB)
        r = release_at(tr, tr.k_apex, "Poor", auto=True)
        T, vx, vy = guide_chain(r, P.chain_spacing_m)
        c = chain_catch(r, T, vx, vy)
        out[vc] = dict(T=T, vx=vx, vy=vy, z_rel_B=c["z_rel_B"], y=c["y"], apex_y=r.y0 + vy ** 2 / 32.0)
    return out


def poly_asin_error():
    lim = math.sin(math.radians(P.grab_max_angle_deg))
    return max(abs(pm.det_asin(-lim + 2 * lim * i / 10000) - math.asin(-lim + 2 * lim * i / 10000)) for i in range(10001))


def cap_validator():
    """Largest vC whose true peak angle (start at the clamp angle -10 deg and at -5.1 deg) stays <= 62 deg."""
    out = {}
    for th0 in (math.radians(10.0), math.radians(5.1225)):
        out[round(math.degrees(th0), 2)] = math.sqrt(2 * P.swing_gravity_mps2 * P.rope_length_m * (
            math.cos(th0) - math.cos(math.radians(P.max_swing_angle_deg))))
    out["closed_form_from_bottom"] = math.sqrt(2 * P.swing_gravity_mps2 * P.rope_length_m * (
        1 - math.cos(math.radians(P.max_swing_angle_deg))))
    return out


def takeoff_13_21():
    return dict((v, takeoff_window(v)["ms"]) for v in (8, 10, 13, 21))


def main():
    t0 = time.time()
    os.makedirs(OUT, exist_ok=True)
    R = {}
    R["poly64"] = pm.poly_error_report(float)
    R["poly32"] = pm.poly_error_report(__import__("numpy").float32)
    rows = tables()
    write_csv(rows)
    R["n_table_rows"] = len(rows)
    R["canon"] = dict((vc, canonical(vc)) for vc in VC_LIST)
    R["fuzz"] = fuzz(1000, False)
    R["fuzz_wide"] = fuzz(300, True)
    R["energy64"] = energy_drift(float)
    R["drift_by_vc"] = dict((vc, drift_profile(vc)) for vc in VC_LIST)
    R["drift_by_vc32"] = dict((vc, drift_profile(vc, __import__("numpy").float32)) for vc in VC_LIST)
    R["slack"] = slack_table()
    R["chain"] = chain_trials(10000)
    R["takeoff"] = takeoff_table()
    R["bots"] = bots(20000)
    R["bots_x2"] = bots(20000, 2.0)
    R["bots_analytic"] = analytic_bot_table()
    R["f32"] = f32_vs_f64(1000)
    R["cost"] = swing_cost()
    R["sens_bots"] = sens_bots()
    R["what_if"] = what_if()
    R["verlet100"] = verlet_drift()
    R["long_drift"] = long_run_drift()
    R["perfect_max_feet"] = perfect_flight_max_feet()
    R["chain_poor"] = chain_grab_z_poor()
    R["asin_err"] = poly_asin_error()
    R["cap"] = cap_validator()
    R["takeoff_key"] = takeoff_13_21()
    R["seconds"] = time.time() - t0
    with open(os.path.join(OUT, "pendulum_study.json"), "w") as f:
        json.dump(R, f, indent=1, default=str)
    print("done in %.0f s" % R["seconds"])
    return R


if __name__ == "__main__":
    R = main()
