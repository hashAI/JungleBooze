"""Writes docs/sim-reports/2026-10-07-fixed-pivot-swing.md from out/pendulum_study.json (run pendulum_study.py first).

    python3 -I pendulum_study.py && python3 -I pendulum_report.py
"""

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pendulum_study as ps  # noqa: E402
import pendulum_model as pm  # noqa: E402
from pendulum_model import DEFAULT as P, SwingTrace, release_at, grade_for_tick  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
REPORT = os.path.join(REPO, "docs", "sim-reports", "2026-10-07-fixed-pivot-swing.md")
R = json.load(open(os.path.join(HERE, "out", "pendulum_study.json")))
C = dict((int(k), v) for k, v in R["canon"].items())
VC = [13, 14, 15, 16]
SPEC_PERFECT = {13: 5.9, 14: 7.6, 15: 9.2, 16: 10.9}
SPEC_EARLY = {13: 4.1, 14: 6.0, 15: 8.2, 16: 10.4}
SPEC_LATE = {13: 3.3, 14: 4.2, 15: 5.2, 16: 6.2}
SPEC_POOR = {13: 3.2, 14: 4.1, 15: 5.0, 16: 5.8}
SPEC_ANGLE = {13: 43.4, 14: 47.0, 15: 50.6, 16: 54.2}
SPEC_APEX_Y = {13: 7.4, 14: 8.2, 15: 8.9, 16: 9.6}


def f(x, n=2):
    return ("%." + str(n) + "f") % x


def table(head, rows):
    out = ["| " + " | ".join(head) + " |", "|" + "|".join(["---"] * len(head)) + "|"]
    for r in rows:
        out.append("| " + " | ".join(str(c) for c in r) + " |")
    return "\n".join(out)


def pct(x, n=1):
    return f(100.0 * x, n) + " %"


def main():
    L = []
    w = L.append
    fz, fzw = R["fuzz"], R["fuzz_wide"]
    w("# Sim report: spec 004 fixed-pivot swing (T401, exact simulation)")
    w("")
    w("**Date:** 2026-10-07 | **Author:** balance-simulator | **Spec:** `docs/specs/004-fixed-pivot-swing.md` | **Model:** `tools/sim/pendulum_model.py` (Python, float64 reference, float32 cross-check)")
    w("")
    w("**What was run:** the spec 004 swing implemented literally (fixed pivot 17 m, rope 14 m, swing gravity 22, 60 Hz symplectic Euler with the Taylor polynomial sine/cosine, catch clamp 13..16, 6 tick blend, windows 27 / 42..52 in ticks, 30 deg impulse, floors 6 and 2, flight gravity 16, guided chains, 16 m chasm with rim 4 m before the pivot) plus the spec 001 movement model (`runner_model.py`) for the ground take-off window. Not the C# code: the C# runner does not have the swing yet. Everything below is a property of the spec as written.")
    w("")
    w("**Reproduce:** `cd tools/sim && python3 -I pendulum_study.py && python3 -I pendulum_report.py` (about 30 s, pure Python plus numpy for the float32 check). Tests: `python3 -I -m pytest tools/sim/tests -q` (33 pass). Seeds: fuzz 40101 (wide 40102), energy 40102, chain 40103, bots 40104, float32 40105. Every number is deterministic from these seeds. Full per-tick table for entry speeds 8..28 step 2 and every release tick: `docs/sim-reports/data/2026-10-07-fixed-pivot-swing-tables.csv` (%d rows)." % R["n_table_rows"])
    w("")
    w("No parameter was changed. Recommendations are at the end and go to game-designer.")
    w("")
    w("## Headline")
    w("")
    w("The spec's hand numbers were wrong in ways that matter. The design itself works: **0 of %s releases land in the chasm** and the worst landing is 2.86 m past the far edge. But:" % format(fz["total"] + fzw["total"], ","))
    w("")
    w("1. **Apex tick is 85 (84..85), not 78..80.** The spec used the quarter period from the lowest point, but the swing starts at -5.1 deg, 5 to 6 ticks before the lowest point. Time to the apex from the grab is 1.417 s, not 1.30..1.33 s. AC-407 as written fails.")
    w("2. **The spec's Perfect landing numbers are 1.2 to 1.9 m too short and its Good-early ones about 1.2 m too long** (hand calc placed the rope at 35.6 deg at tick 47.5; the integrator has 32.0 deg). Perfect lands **6.6 to 13.6 m** past the edge (spec: 6 to 11), so the claim fails at vC 15 and 16.")
    w("3. **The 3.2 m guarantee is false: the minimum is 2.86 m** (Good, release at the first Good tick 27, vC 13). Poor is 3.32 m (spec 3.2: correct). 2.86 m is still safe; AC-413's 14.5 m floor holds with 0.36 m to spare.")
    w("4. **Energy drift of the specified integrator is 1.27 %, not 0.1 %.** It is bounded (no secular growth, checked over 6,000 ticks), so it is a tolerance fix, not a bug.")
    w("5. **Chain second swings do not always have vC = 13**: after a Good-early release the arrival speed is up to 14.78 m/s, so vC of the next swing is 13 to 14.78 (AC-419 as written fails).")
    w("6. Ground take-off windows are **1 to 3 ticks longer** than the spec's hand formula (the formula forgot the 0.25 m hitbox half depth at the rim). The ACs pass with margin.")
    w("7. The bot Perfect rates match the spec's analytic table (expert 80.9, average 38.6, new 26.9 %). Average sits only 1.4 points under the 40 % cap.")
    w("")
    w("## Targets")
    w("")
    rows = []
    rows.append(["S-401 (AC-413)", "vIn 8..28 step 0.5 x 1,000 random grab offsets, every release tick 27..apex + Poor: landing >= sP+14.5 and <= sP+30, 0 chasm landings",
                 "%s releases (+%s with grabs anywhere in the zone): min %s (%s past edge), max %s; 0 violations" % (
                     format(fz["total"], ","), format(fzw["total"], ","), f(fz["min_land"][0]), f(fz["min_land"][0] - 12), f(fz["max_land"][0])), "**PASS**"])
    rows.append(["Spec 4.6 / 6", "every valid release incl. Poor lands >= 3.2 m past the far edge",
                 "min %s m (vC 13, Good, tick 27); Poor min %s m" % (f(C[13]["min_land"][0]), f(C[13]["rel"]["poor"]["past"])), "**FAIL** (restate as >= 2.8 m)"])
    rows.append(["Spec 4.6 / 6", "Perfect lands 6 to 11 m past the edge", "%s to %s m (per vC: 13 %s..%s, 14 %s..%s, 15 %s..%s, 16 %s..%s)" % (
        f(min(C[v]["perfect_range"][0] for v in VC)), f(max(C[v]["perfect_range"][1] for v in VC)),
        f(C[13]["perfect_range"][0]), f(C[13]["perfect_range"][1]), f(C[14]["perfect_range"][0]), f(C[14]["perfect_range"][1]),
        f(C[15]["perfect_range"][0]), f(C[15]["perfect_range"][1]), f(C[16]["perfect_range"][0]), f(C[16]["perfect_range"][1])), "**FAIL** at vC 15, 16"])
    rows.append(["S-402", "tables within +-0.3 m landing, +-2 ticks apex, +-0.5 deg angle",
                 "Poor: all within 0.13 m. Good-late within 0.34 m. Angles within 0.47 deg. Perfect: 1.2 to 1.9 m off. Good-early: 1.2 to 1.3 m off. Apex tick 85 vs 78..80: 5 to 7 ticks off",
                 "**FAIL** (Perfect, Good-early, apex tick)"])
    w_e = R["bots"]
    rows.append(["S-403 / GDD 7.6", "Perfect: expert >= 70 %, average 25..40 %; Poor <= 6 % all",
                 "expert %s, average %s, new %s (20k trials per speed; exact analytic 81.0 / 38.9 / 27.6). Poor 0.0 / 0.3 / 3.5 %%" % tuple(
                     pct(w_e[n]["total"]["Perfect"] / float(w_e[n]["total"]["n"])) for n in ("expert", "average", "new")), "**PASS** (average margin 1.4 pt)"])
    rows.append(["S-404 / AC-403", "energy drift <= 0.1 % in 100 ticks, 1,000 start states", "max %s (bounded, no secular growth); velocity Verlet would give %s" % (
        pct(R["energy64"]["max_rel_drift"], 2), pct(R["verlet100"], 3)), "**FAIL** (restate as <= 1.5 %)"])
    rows.append(["S-404 / AC-406", "theta never above 62 deg for vIn 0..40; peak angle within 0.5 deg of 43.4 / 47.0 / 50.6 / 54.2",
                 "max %s deg over 1,000 states; peaks %s / %s / %s / %s deg (+0.47 .. +0.36 off)" % (
                     f(R["energy64"]["max_theta_deg"], 1), *[f(C[v]["apex_deg"]) for v in VC]), "**PASS** (marginal on 13: use the measured values)"])
    rows.append(["AC-407", "first omega <= 0 at tick 78 / 78 / 79 / 80 (+-2)", "85 / 85 / 85 / 85 (84..85 over all normal grab offsets)", "**FAIL**"])
    rows.append(["Spec 4.3", "rope never slack for vC 13..16", "min tension %s / %s / %s / %s m/s^2 (positive; slack means < 0)" % tuple(f(C[v]["min_tension"], 1) for v in VC), "**PASS**"])
    rows.append(["Spec 4.2", "28 m/s without clamp: rope goes slack", "yes: slack at %s deg from tick 78; 24 m/s is still taut (apex 86.3 deg, min tension 1.3)" % f(R["slack"][-1]["slack_deg"], 1), "**PASS**"])
    rows.append(["S-405 / AC-418", "chain, 10,000 trials per grade and vC: T in [0.85, 1.5], arrival feet 1.5 +-0.1, catch 100 %",
                 "120,000 trials: 100 %% catches, T %s..%s s, feet 1.500 exactly" % (
                     f(min(v["T_min"] for v in R["chain"].values()), 3), f(max(v["T_max"] for v in R["chain"].values()), 3)), "**PASS**"])
    rows.append(["AC-419", "second and third swing have vC = 13.000 (arrival vx < 13)", "vC of the next swing is 13.00 .. 14.78 (Good-early at vC 16 arrives at 14.78)", "**FAIL**"])
    rows.append(["S-406 / AC-427", "take-off window 280 / 350 / 410 ms at 8 / 10 / 13 m/s, within +-1 tick of 286 / 355 / 418 / 499 (21 m/s)",
                 "%s / %s / %s / %s ms at 8 / 10 / 13 / 21 m/s" % tuple(f(R["takeoff_key"][str(v)], 0) for v in (8, 10, 13, 21)), "**PASS** (AC floors) / **FAIL** (+-1 tick of hand numbers)"])
    rows.append(["GDD 7.6 grab", "grab success: new >= 75 %, average >= 90 % (jump-timing bot)",
                 "new %s, average %s, expert %s (profile sigmas 100 / 67 / 33 ms); at 2x sigma: new 73..80 %%, average 90..95 %%" % tuple(
                     pct(1 - w_e[n]["total"]["Miss"] / float(w_e[n]["total"]["n"])) for n in ("new", "average", "expert")), "**PASS** (thin at 2x sigma, slow speeds)"])
    rows.append(["AC-408", "DeterministicMath error <= 1e-7 on [-1.3, 1.3]", "float64: sin %.1e, cos %.1e. float32: sin %.2e, cos %.2e" % (
        R["poly64"][0], R["poly64"][1], R["poly32"][0], R["poly32"][1]), "**PASS** (float64) / **FAIL by 4 %** (float32 sin)"])
    rows.append(["AC-435", "float vs double: x,y,z within 1e-4, theta within 1e-6", "1,000 random swings: max d(theta) %.1e, d(omega) %.1e, d(z) %.1e m, d(y) %.1e m, landing %.1e m; apex tick identical in 1000/1000" % (
        R["f32"]["max_dtheta"], R["f32"]["max_domega"], R["f32"]["max_dz"], R["f32"]["max_dy"], R["f32"]["max_dland"]), "**PASS** (theta only 1.7x under 1e-6: use 5e-6)"])
    rows.append(["AC-414", "Perfect(center) - Poor >= 2.5 m", "%s / %s / %s / %s m at vC 13..16" % tuple(f(C[v]["rel"]["p_center"]["past"] - C[v]["rel"]["poor"]["past"]) for v in VC), "**PASS**"])
    rows.append(["AC-411", "auto release no later than tick 96", "tick 85; margin 11 ticks", "**PASS**"])
    w(table(["#", "Target", "Measured", "Result"], rows))
    w("")
    w("## 1. Tables of 4.2 to 4.6, exact")
    w("")
    w("Convention (pinned in the model docstring and the tests): swing tick k = ticks since the grab tick; a swipe at tick k >= 27 uses the state after k-1 integrations; the auto release uses the state after k_a integrations. Canonical grab: z = pivot - 1.25 (first tick of the zone), theta0 = -5.12 deg. A different grab offset inside the zone changes landings by at most the offset (see fuzz). Rope angle is theta at the release state.")
    w("")
    w("### 1.1 Swing (spec 4.3 table)")
    w("")
    w(table(["vC (m/s)", "peak theta, simulated (spec)", "peak theta, closed form from the bottom", "apex tick (spec)", "apex time from grab", "T/4 closed form, ticks", "hand height at apex (spec 'rope end')", "feet height at apex"],
            [[v, "%s deg (%s)" % (f(C[v]["apex_deg"]), SPEC_ANGLE[v]), f(C[v]["apex_deg_closed"]) + " deg", "%d (%s)" % (C[v]["k_apex"], {13: "78", 14: "78.5", 15: "79", 16: "80"}[v]),
              f(C[v]["apex_ms"] / 1000.0, 3) + " s", f(C[v]["t4_closed_ticks"], 1), "%s m (%s)" % (f(C[v]["apex_hand_y"]), SPEC_APEX_Y[v]), f(C[v]["apex_feet_y"]) + " m"] for v in VC]))
    w("")
    w("The spec's 'apex height of the rope end above the path' (7.4 / 8.2 / 8.9 / 9.6) is 0.5 to 0.7 m higher than the geometry gives (17 - 14 cos theta). The simulated peak is 0.35 to 0.47 deg above the closed form because the swing starts 5.1 deg before the lowest point with speed vC at that point, not at the bottom.")
    w("")
    w("### 1.2 Perfect (center tick 47), spec 4.6 first table")
    w("")
    rows = []
    for v in VC:
        r = C[v]["rel"]["p_center"]
        rows.append([v, f(r["rope_deg"], 1), f(r["dist"], 1), "%s, %s" % (f(r["vx"], 1), f(r["vy"], 1)), f(r["y0"], 1), f(r["t"], 2),
                     f(r["land"], 1), "**%s** (spec %s)" % (f(r["past"], 1), SPEC_PERFECT[v]), "%s .. %s" % (f(C[v]["perfect_range"][0], 1), f(C[v]["perfect_range"][1], 1))])
    w(table(["vC", "rope angle deg", "grab-to-release m", "release (vx, vy) m/s", "feet y m", "flight s", "landing past pivot m", "past far edge m", "Perfect window range past edge"], rows))
    w("")
    w("Spec hand values at 13..16: rope 35.6 / 38.4 / 41.2 / 43.9 deg, distance 9.4 / 9.9 / 10.5 / 11.0 m, release speed (8.6, 5.8) .. (9.2, 7.9), feet 3.9 / 4.3 / 4.7 / 5.2 m, flight 1.14 / 1.24 / 1.33 / 1.43 s. Measured flight times (1.15 .. 1.45 s) agree; angle, distance, feet height and vx do not (the hand calc put the rope about 3.6 deg too far along).")
    w("")
    w("### 1.3 Other grades, spec 4.6 second table (past the far edge, m)")
    w("")
    w(table(["vC", "Good-early tick 27 (spec)", "Good-late tick 66 (spec)", "Poor, apex (spec)", "best Good (tick 41)", "Perfect min..max"],
            [[v, "%s (%s)" % (f(C[v]["rel"]["early"]["past"]), SPEC_EARLY[v]), "%s (%s)" % (f(C[v]["rel"]["late"]["past"]), SPEC_LATE[v]),
              "%s (%s)" % (f(C[v]["rel"]["poor"]["past"]), SPEC_POOR[v]), f(C[v]["good_max_land"]), "%s .. %s" % (f(C[v]["perfect_range"][0]), f(C[v]["perfect_range"][1]))] for v in VC]))
    w("")
    w("Perfect is the **longest throw** at every vC (min Perfect > max Good by 1.46 / 1.20 / 0.85 / 0.34 m at vC 13 / 14 / 15 / 16). The spec text 'the longest throw is an earlier Good' (4.4, R404) is not true. The maximum of the whole swing is at tick 42, the first Perfect tick; landing then falls monotonically through the Perfect window (about 0.07 m per tick at vC 13, 0.21 at vC 16).")
    w("")
    w("### 1.4 Landing past the far edge by release tick (canonical grab, m)")
    w("")
    rows = []
    for k in [27, 30, 33, 36, 39, 41, 42, 45, 47, 50, 52, 53, 56, 60, 66, 72, 78, 84, 85]:
        row = [k, f(k * 1000 / 60.0, 0), "Perfect" if 42 <= k < 53 else "Good"]
        for v in VC:
            tr = SwingTrace(v, ps.Z_GRAB)
            if k > tr.k_apex:
                row.append("-")
            elif k == tr.k_apex:
                row.append("%s (Good by swipe; auto %s)" % (f(release_at(tr, k, grade_for_tick(k, k)).z_land - 12), f(release_at(tr, k, "Poor", auto=True).z_land - 12)))
            else:
                row.append(f(release_at(tr, k, grade_for_tick(k, tr.k_apex)).z_land - 12))
        rows.append(row)
    w(table(["tick", "ms", "grade", "vC 13", "vC 14", "vC 15", "vC 16"], rows))
    w("")
    w("### 1.5 Totals the spec quotes")
    w("")
    gl = [C[v]["grab_to_release_range"] for v in VC]
    w(table(["Quantity", "Spec", "Measured"],
            [["Grab-to-release distance, any grade, any vC", "6.7 to 12.6 m", "%s to %s m" % (f(min(a for a, b in gl), 1), f(max(b for a, b in gl), 1))],
             ["Grab-to-release distance, Perfect centre", "9.4 to 11.0 m", "%s to %s m (whole Perfect window %s to %s)" % (
                 f(C[13]["rel"]["p_center"]["dist"], 1), f(C[16]["rel"]["p_center"]["dist"], 1), f(C[13]["rel"]["p_first"]["dist"], 1), f(C[16]["rel"]["p_last"]["dist"], 1))],
             ["Release speed, Perfect (total)", "10.3 to 12.1 m/s", "%s to %s m/s (centre)" % (f(C[13]["rel"]["p_center"]["speed"], 1), f(C[16]["rel"]["p_center"]["speed"], 1))],
             ["Release speed, Good-early (total)", "12.7 to 14.4 m/s", "%s to %s m/s" % (f(C[13]["rel"]["early"]["speed"], 1), f(C[16]["rel"]["early"]["speed"], 1))],
             ["Highest feet height in a Perfect flight", "4.9 (vC 13) to 7.1 m (16)", "%s to %s m" % (f(R["perfect_max_feet"]["13"], 1), f(R["perfect_max_feet"]["16"], 1))],
             ["Whole airborne time grab to landing, Perfect", "1.93 to 2.22 s", "%s to %s s" % (f(R["cost"][0]["t"] if False else (46 / 60.0 + C[13]["rel"]["p_center"]["t"]), 2), f(46 / 60.0 + C[16]["rel"]["p_center"]["t"], 2))],
             ["Perfect ring time after release (vy / 16)", "0.36 to 0.49 s", "%s to %s s" % (f(C[13]["rel"]["p_center"]["vy"] / 16, 2), f(C[16]["rel"]["p_center"]["vy"] / 16, 2))],
             ["Perfect minus Poor", "3 to 5 m", "%s to %s m" % (f(C[13]["rel"]["p_center"]["past"] - C[13]["rel"]["poor"]["past"], 1), f(C[16]["rel"]["p_center"]["past"] - C[16]["rel"]["poor"]["past"], 1))]]))
    w("")
    w("### 1.6 Entry speeds 8 to 28 (canonical grab)")
    w("")
    rows = []
    for v in ps.ENTRY_SPEEDS:
        tr = SwingTrace(v, ps.Z_GRAB)
        e = release_at(tr, 27, "Good")
        pc = release_at(tr, 47, "Perfect")
        po = release_at(tr, tr.k_apex, "Poor", auto=True)
        rows.append([v, f(tr.v_c, 0), "%+d" % round(tr.v_c - v), f(math.degrees(max(tr.theta)), 1), tr.k_apex, f(e.z_land - 12), f(pc.z_land - 12), f(po.z_land - 12),
                     f(pc.z_land - ps.Z_GRAB, 1)])
    w(table(["vIn m/s", "vC", "catch change", "peak deg", "apex tick", "Good tick 27 past edge", "Perfect tick 47 past edge", "Poor past edge", "Perfect grab-to-landing m"], rows))
    w("")
    w("Only vC matters (the clamp makes every entry below 13 identical and every entry above 16 identical); the grab offset inside the zone is the only other input.")
    w("")
    w("## 2. Fairness fuzz (S-401)")
    w("")
    w(table(["Run", "Releases checked", "Min landing (past edge)", "Max landing (past edge)", "Violations", "Apex ticks seen", "Min rope tension"],
            [["vIn 8..28 step 0.5, 1,000 grab offsets each in the first-tick range (seed 40101)", format(fz["total"], ","), "%s (%s) at vIn %s, tick %s" % (f(fz["min_land"][0], 3), f(fz["min_land"][0] - 12, 2), fz["min_land"][1]["vIn"], fz["min_land"][1]["tick"]),
              "%s (%s) at vIn %s, tick %s" % (f(fz["max_land"][0], 3), f(fz["max_land"][0] - 12, 2), fz["max_land"][1]["vIn"], fz["max_land"][1]["tick"]), fz["bad"], fz["k_apex_set"], f(fz["min_tension"], 1)],
             ["same, 300 offsets each anywhere in the zone -1.25..+1.25 (seed 40102)", format(fzw["total"], ","), "%s (%s)" % (f(fzw["min_land"][0], 3), f(fzw["min_land"][0] - 12, 2)),
              "%s (%s)" % (f(fzw["max_land"][0], 3), f(fzw["max_land"][0] - 12, 2)), fzw["bad"], "%d..%d" % (min(fzw["k_apex_set"]), max(fzw["k_apex_set"])), f(fzw["min_tension"], 1)]]))
    w("")
    w("Violation = landing < sP+14.5, > sP+30, or inside the chasm (footprint within 0.25 m of the far edge). The wide run shows the apex tick falls to 73 when the grab is late in the zone (a chain arrival or a late coyote catch near the pivot): the HUD estimate should use the actual start angle, or the ring should be driven by the real omega.")
    w("")
    w("**Pad clearance** (chunk data of spec section 9, landing max sP+25.61): V-03/V-04 pivot 34, pad coins from 62: max landing 59.6, 2.4 m to spare. V-05 last pivot 52: max landing 77.6, coins from 78: 0.4 m to spare. V-06 last pivot 70: max landing 95.6, coins from 96: 0.4 m. 20 m clear: V-05 97.6 <= 110, V-06 115.6 <= 120. All fit; the coin starts of V-05/V-06 should move back 4 m.")
    w("")
    w("## 3. Integrator, determinism")
    w("")
    w(table(["Check", "Result"],
            [["Polynomial sin (x^11), cos (x^12) vs math.sin/cos on [-1.3, 1.3], float64", "max error %.2e / %.2e" % tuple(R["poly64"])],
             ["same polynomials evaluated in float32", "max error %.2e / %.2e (the sin result is 4 percent above the 1e-7 limit; it is float32 rounding, not the polynomial)" % tuple(R["poly32"])],
             ["asin for theta0 (not in the spec's DeterministicMath)", "the spec calls asin; math.asin is platform dependent in principle. Series a + a^3/6 + 3a^5/40 + 15a^7/336 for |a| <= sin 10 deg has max error %.1e rad (tested)" % R["asin_err"]],
             ["Energy drift of symplectic Euler, 100 ticks, 1,000 start states", "max %s (vC 13..16 at the canonical grab: %s)" % (pct(R["energy64"]["max_rel_drift"], 2), ", ".join(pct(R["drift_by_vc"][str(v)], 2) for v in VC))],
             ["Same, 6,000 ticks in 600 tick blocks", "%s .. %s per block: bounded, no secular drift" % (pct(min(R["long_drift"]), 3), pct(max(R["long_drift"]), 3))],
             ["Velocity Verlet for comparison (not the spec)", pct(R["verlet100"], 3) + " over 100 ticks"],
             ["float32 vs float64, 1,000 random swings (seed 40105)", "max d(theta) %.2e rad, d(omega) %.2e, d(z) %.2e m, d(y) %.2e m, Perfect landing %.2e m; apex tick identical in all" % (
                 R["f32"]["max_dtheta"], R["f32"]["max_domega"], R["f32"]["max_dz"], R["f32"]["max_dy"], R["f32"]["max_dland"])],
             ["float32 energy drift (vC 13..16)", ", ".join(pct(R["drift_by_vc32"][str(v)], 3) for v in VC) + " (same as float64 to 4 digits)"]]))
    w("")
    w("Reading: float and double agree to 6e-7 rad, far inside gameplay needs, but they are **not bit-identical**, so a replay hash (AC-408 'equal state hashes') only holds between runs of the same precision on the same arithmetic. The spec says the swing is 'in double'; keep that, or compare goldens with tolerance. Risk left open: an ARM64 JIT that fuses multiply-add would change float32 bits (not measurable here).")
    w("")
    w("## 4. Rope tension and the 28 m/s claim")
    w("")
    w(table(["Unclamped entry (m/s)", "v^2/(2gL)", "peak deg (closed form)", "peak deg (sim)", "min tension L w^2 + g cos(theta) (m/s^2)", "first slack tick", "slack angle"],
            [[r["v"], f(r["ratio"], 3), f(r["apex_deg"], 1), f(r["sim_peak_deg"], 1), f(r["min_tension"], 2), r["first_slack_tick"] if r["first_slack_tick"] is not None else "never",
              (f(r["slack_deg"], 1) + " deg") if r["slack_deg"] else "-"] for r in R["slack"]]))
    w("")
    w("The claim holds: 28 m/s (ratio 1.27) goes slack at 100.5 deg, tick 78. 24 m/s stays taut (86 deg, tension 1.3), 21 m/s is steep (73.5 deg). Inside the clamp range tension never goes below 12.7 m/s^2.")
    w("")
    w("## 5. Chains (S-405), D = 18 m, 10,000 trials per grade and vC")
    w("")
    rows = []
    for g in ("Perfect", "Good", "Poor"):
        for v in VC:
            c = R["chain"]["%s/%d" % (g, v)]
            vn = c["vc_next"]
            rows.append([g, v, "%d / %d" % (c["success"], c["n"]), "%s .. %s" % (f(c["T_min"], 3), f(c["T_max"], 3)), "%s .. %s" % (f(c["vx_min"], 1), f(c["vx_max"], 2)),
                         "%d / %d" % (c["clamp_lo"], c["clamp_hi"]), "%s .. %s" % (f(min(vn), 2), f(max(vn), 2)), "%s .. %s" % (f(c["grab_y_min"], 2), f(c["grab_y_max"], 2))])
    w(table(["Grade", "vC", "caught", "T s", "arrival vx m/s", "T clamped min / max", "vC of next swing", "feet y at the grab tick"], rows))
    w("")
    w("Arrival feet height at the vine z is 1.500 m in every trial (guided by construction). The release-grade chains need no change. Notes: (a) Perfect at vC 16 hits the 0.85 s lower clamp in 69 % of trials, so the ChainFlightMinS value is active; (b) Poor chains are high lobs: guided T 1.10..1.38 s, vy 3.7..8.4 m/s, apex 7.3..7.6 m (spec said 8 m); (c) they cross the 3.6 m grab-height limit late: the first grab tick is at z = vine - 0.93 .. -1.00 with y just under 3.6 m, so the height rule, not the 2.5 m zone length, decides the grab tick (the zone still leaves 2.2 m after it); (d) vC of the next swing is 13 only after Perfect and Poor; after Good it is up to 14.78 (AC-419).")
    w("")
    w("## 6. Take-off window (S-406), GrabEarlinessMs 550 vs 450")
    w("")
    w("Model: spec 001 `Runner` at constant speed over the 16 m chasm, a Jump command on each candidate tick; success = AIRBORNE with y > 0 inside the zone (|z - sP| <= 1.25, y < 3.6), at most 33 ticks (550 ms) after the take-off tick. All windows are contiguous.")
    w("")
    w(table(["Speed m/s", "Window with 550 (ticks / ms)", "Window with 450 (ms)", "Spec 004 4.1 formula (ms)", "Model minus formula (ticks)"],
            [[r["v"], "%d / %s" % (r["ticks"], f(r["ms"], 0)), f(r["ms450"], 0), f(r["formula_ms"], 0), "%+.1f" % ((r["ms"] - r["formula_ms"]) * 0.06)] for r in R["takeoff"]]))
    w("")
    w("Why the model is longer: the late bound is the coyote jump. The footprint leaves the ground when z - 0.25 >= rim, then 5 ticks of coyote time, so the last take-off is at rim + 0.25 + 0.0833 v, not rim + 0.08 v. The early bound matches the spec (sP - 1.25 - 0.55 v). From 24 m/s the window is capped by the 33 tick earliness rule (550 ms). At 450 the window is 233 to 300 ms for 8 to 10 m/s, shorter than the old 355 ms, so 550 is needed (R405 confirmed).")
    w("")
    w("## 7. Fairness bots (S-403 and GDD 7.6)")
    w("")
    w("Release timing: swipe tick = round(N(783 ms, sigma) x 0.06), centred on the Perfect window (tick 47); sigma 70 / 180 / 260 ms. No retry after an early swipe (<= tick 17 expires). Jump timing: take-off tick = window centre + round(N(0, s)), s = the existing `bots.py` profile sigma (33 / 67 / 100 ms), plus the existing decision error rate (0.2 / 0.5 / 2 %) as a no-grab. Six speeds (12, 13.5, 15.5, 17.5, 19, 21 m/s) x 20,000 trials per profile = 120,000 trials per profile. Miss = no grab on a chasm vine (a Missed vine death; no retry modelled, so pessimistic).")
    w("")
    rows = []
    for n in ("expert", "average", "new"):
        t = R["bots"][n]["total"]
        N = float(t["n"])
        g = N - t["Miss"]
        a = R["bots_analytic"][n]
        rows.append([n, "%d / %d ms" % (ps.PROFILES[n][0], ps.PROFILES[n][1]), pct(t["Perfect"] / N), pct(t["Good"] / N), pct(t["Poor"] / N), pct(t["Miss"] / N),
                     "%s / %s / %s" % (pct(t["Perfect"] / g), pct(t["Good"] / g), pct(t["Poor"] / g)), "%s / %s / %s" % tuple(pct(x) for x in a)])
    w(table(["Profile", "release / jump sigma", "Perfect", "Good", "Poor", "Miss (no grab)", "given a grab: P / G / Poor", "exact analytic (release only) P / G / Poor"], rows))
    w("")
    w("Targets: expert Perfect >= 70 %% (**PASS**, 80.9 vs exact 81.0); average 25..40 %% (**PASS**, 38.6; given a grab 38.8; exact 38.9); Poor <= 6 %% (**PASS**, new 3.5 %%). Grab success: expert %s, average %s, new %s vs targets average >= 90 %%, new >= 75 %% (**PASS**). Per-speed grab success, new: %s." % (
        pct(1 - R["bots"]["expert"]["total"]["Miss"] / 120000.0), pct(1 - R["bots"]["average"]["total"]["Miss"] / 120000.0), pct(1 - R["bots"]["new"]["total"]["Miss"] / 120000.0),
        ", ".join("%s m/s %s" % (v, pct(c["grab"] / float(c["n"]))) for v, c in R["bots"]["new"]["per_speed"].items())))
    w("")
    w("**Sensitivity (the jump sigma doubled, 67 -> 134 ms for average, 100 -> 200 for new):** average grab %s (target 90), new grab %s (target 75), per speed for new %s. Pooled over speeds both still pass, but new fails at the slow speeds (12 and 13.5 m/s: 72.7 and 74.6 %%) and average is at 90.1 %% at 12 m/s. The grab targets therefore pass only while the profile sigmas are right and are thinnest at 12 m/s (a 450 ms window). This is why GrabEarlinessMs must not go below 550." % (
        pct(1 - R["bots_x2"]["average"]["total"]["Miss"] / 120000.0), pct(1 - R["bots_x2"]["new"]["total"]["Miss"] / 120000.0),
        ", ".join("%s: %s" % (v, pct(c["grab"] / float(c["n"]))) for v, c in R["bots_x2"]["new"]["per_speed"].items())))
    w("")
    w("**Perfect share (exact) vs release sigma and Perfect width:**")
    w("")
    sb = R["sens_bots"]
    sig = ["70", "100", "120", "150", "180", "220", "260"]
    w(table(["Perfect width"] + ["sigma " + s for s in sig],
            [["%d ticks (%s ms)" % (int(wd), f(int(wd) * 1000 / 60.0, 0))] + [pct(sb[wd][s]) for s in sig] for wd in ("9", "11", "13")]))
    w("")
    w("Average (180 ms) is 38.9 % with the spec's 11 ticks. If real average players are 150 ms, Perfect becomes 45.9 % (over the 40 % cap), and the spec's remedy of 9 ticks gives 38.3 % at 150 ms but then expert (70 ms) is 71.6 %, just above the 70 % floor. 11 ticks is correct for sigma 180; 9 is correct for sigma 150. Both pairs fit the GDD; the choice depends on the real average player (T406/playtest), not on this model.")
    w("")
    w("## 8. Cost of a swing in distance (S-407, new model only)")
    w("")
    w(table(["Run speed", "Grade", "Grab to landing s", "Distance covered m", "Distance a runner at that speed would cover m", "Loss m", "Loss s"],
            [[r["v"], r["grade"], f(r["t"], 2), f(r["dist"], 1), f(r["run_dist"], 1), f(r["loss_m"], 1), f(r["loss_s"], 2)] for r in R["cost"]]))
    w("")
    w("Spec 4.7 'about 22 m, about 1 s at 21 m/s' holds for Perfect (20.5 m, 0.98 s). Poor at 21 m/s costs 33 m, 1.6 s. At 10 m/s a Perfect swing gains 1.2 m (the clamp pulls the runner up to 13 m/s). The old model kept the run speed (distance v x 2.35 s), so there is no comparable loss in it; whether 1.6 s of lost progress for Poor at top speed is acceptable is a score-design question for game-designer.")
    w("")
    w("## 9. What-if (not applied)")
    w("")
    w(table(["Candidate change", "Min landing past edge", "Perfect min .. max", "min (Perfect - best Good) at one vC"],
            [[r["name"], f(r["min_past"]), "%s .. %s" % (f(r["perfect_min"]), f(r["perfect_max"])), f(r["ladder"])] for r in R["what_if"]]))
    w("")
    w("`GoodStartMs 500` (tick 30) lifts the minimum to 3.23 m (the worst case moves to tick 78 at vC 13, so a '3.2 m' guarantee would then be true). `CatchMaxSpeedMps 15` caps Perfect at 11.45 m (10.9 at the centre; tick 42 is still just over 11) and widens the Perfect-over-Good ladder to 0.85 m. `PerfectImpulseMps 2.0` is rejected: the best Good (tick 41, impulse 1.5) would then out-throw the Perfect window (ladder -1.33 m). None is needed for safety; they are feel/presentation calls for game-designer.")
    w("")
    w("## 10. Spec numbers to change, with the recommended value")
    w("")
    w("Section numbers refer to `docs/specs/004-fixed-pivot-swing.md`. 'Pure number' = replace a hand number with the exact one, no gameplay change.")
    w("")
    rec = [
        ["4.3 table, 4.4 text, AC-407, feel target 'swing to the apex'", "Apex tick 78 / 78.5 / 79 / 80; 'about 1,300 ms'; 1.30 to 1.33 s", "**85 (84..85), 1,417 ms** from the grab; 1.30 to 1.33 s is the time from the lowest point. HUD `SwingPhase` = tick / 85. SwingMaxMs 1600 (96 ticks) still leaves 11 ticks", "Pure number. The ring would end early with 78."],
        ["4.3 table", "Peak 43.4 / 47.0 / 50.6 / 54.2 deg; apex height 7.4 / 8.2 / 8.9 / 9.6 m", "**43.9 / 47.4 / 51.0 / 54.6 deg; rope end 6.9 / 7.5 / 8.2 / 8.9 m** (feet 5.2 / 5.8 / 6.4 / 7.1 m); AC-406 tolerance +-0.1 deg around these", "Pure number."],
        ["4.4 table (angles at vC 14), 4.3 'Perfect = 34 to 41 deg'", "Good 24..34, Perfect 34..41, late 41..47 deg", "Good 19.3..30.6, **Perfect 31.3..37.7**, late %s..47.4 deg (vC 14)" % f(release_at(SwingTrace(14, ps.Z_GRAB), 53, "Good").rope_deg, 1) + "", "Pure number."],
        ["4.6 first table", "Perfect landing past edge 5.9 / 7.6 / 9.2 / 10.9; distance 9.4..11.0; feet 3.9..5.2", "**7.1 / 9.0 / 10.9 / 12.8**; distance 8.7 / 9.2 / 9.8 / 10.3; feet 3.4 / 3.7 / 4.1 / 4.5; release (vx, vy) (10.2, 6.2) / (10.5, 6.9) / (10.7, 7.7) / (10.9, 8.5); flight 1.15 / 1.24 / 1.34 / 1.45 s", "Pure number."],
        ["4.6 second table", "Good-early 4.1 / 6.0 / 8.2 / 10.4; Good-late 3.3 / 4.2 / 5.2 / 6.2; Poor 3.2 / 4.1 / 5.0 / 5.8", "Good-early **2.9 / 4.8 / 6.9 / 9.2**; Good-late 3.4 / 4.5 / 5.5 / 6.5; Poor 3.3 / 4.2 / 5.1 / 5.9", "Pure number."],
        ["4.6 reading, section 6 'hard guarantee', R404", "Every valid release >= 3.2 m past the edge; Perfect 6 to 11; the longest throw is an earlier Good", "**>= 2.85 m** (Good at tick 27, vC 13); Poor >= 3.3 m; **Perfect 6.6 to 13.6 m** and Perfect is the longest throw at every vC", "Pure number. Optional design change in section 9 above."],
        ["AC-413 / S-401", "landing >= sP+14.5 and <= sP+30", "Keep both bounds. Measured 14.86 and 25.61 (4.4 m clear above, 0.36 m clear below)", "No change."],
        ["AC-414", "Perfect(centre) - Poor >= 2.5", "Keep; measured 3.8 / 4.9 / 5.8 / 6.9", "No change."],
        ["4.3 'Energy drift <= 0.1 %', AC-403", "0.1 % over 100 ticks", "**<= 1.5 %** (measured 1.27 % max, bounded). Keep the symplectic Euler: velocity Verlet (0.010 %) costs a second sin per tick and changes every golden, and gameplay does not need it", "Tolerance."],
        ["4.3 validator, 'vCmax <= 18.1'", "closed form from the lowest point", "The start angle adds energy: **vCmax <= 17.8** (start angle at the 10 deg clamp). At vC 18 the true peak is 62.03 deg", "Pure number (config validator rule: cos(theta0max) - vC^2/(2gL) >= cos(62 deg))."],
        ["4.3 'DeterministicMath ... max error 1e-7', AC-408", "Sin/Cos error <= 1e-7", "float64 5e-9 / 4e-10 passes. If the swing runs in float32: sin 1.04e-7. State the limit as **2e-7 in float32 or keep the swing state in double** (the spec already says double)", "Tolerance."],
        ["4.2 catch / 11.1", "theta0 = asin(...)", "DeterministicMath has no asin. Add the 4-term series (error 4.5e-9 rad) `a(1 + a^2(1/6 + a^2(3/40 + a^2 15/336)))`, or compute theta0 once with `Math.Asin` and keep it out of the hash path", "Missing piece."],
        ["AC-435", "theta within 1e-6", "**5e-6** (float vs double differ by 6e-7 over 1,000 random swings)", "Tolerance."],
        ["4.8, AC-419", "'arrival vx 6 to 12.1 m/s ... every chain swing after the first has vC 13'", "**Arrival vx 6.0 to 14.78 m/s; vC of the next swing 13 to 14.78** (13 after Perfect and Poor)", "Pure number."],
        ["4.8 chain check", "Poor at vC 13: vy 8.7, apex 8 m; Perfect at vC 16: T 0.90", "Poor vC 13: vy 8.4 m/s, T 1.38 s, apex 7.4 m; Perfect vC 16: T 0.85..0.89 (69 % clamped at 0.85)", "Pure number."],
        ["4.1 take-off window formula and AC-427", "[sP - 1.25 - 0.55 v, rim + 0.08 v], 286 / 355 / 418 / 499 ms at 8 / 10 / 13 / 21", "**[sP - 1.25 - 0.55 v, rim + 0.25 + 0.0833 v]**; **333 / 400 / 467 / 533 ms**; capped at 550 ms from 24 m/s; AC-427 floors 280 / 350 / 410 stay valid", "Pure number. GrabEarlinessMs 550 confirmed (450 gives 233 / 300 ms at 8 / 10 m/s)."],
        ["Section 9 chunk data", "V-05 pad coins from 78, V-06 from 96", "Max landing sP+25.6 gives 77.6 and 95.6: move the pad coin starts to **82** (V-05) and **100** (V-06); V-03/V-04 (62) are fine", "Chunk data."],
        ["4.4 'Poor' wording", "automatic release on the first tick with omega <= 0 (tick 78 to 80)", "tick 84 or 85 for a normal grab; down to 73 for a grab late in the zone", "Pure number."],
        ["6 bot table", "expert 81.0 / average 38.9 / new 27.6 % Perfect", "Confirmed by 120k trials: 80.9 / 38.6 / 26.9 %. New Poor 3.5 %. The spec's Poor for new (5.5 % = 2.6 late + 2.9 early) is wrong in the late part: swipes after the apex tick 85 are 0.7 %, early (<= tick 17) 2.9 %, Poor 3.6 %, Good 68.8 %", "Pure number."],
    ]
    w(table(["Spec place", "Spec says", "Exact value", "Kind"], rec))
    w("")
    w("## 11. Recommended parameter table for the engineer")
    w("")
    w("All gameplay parameters stay at the spec 004 section 5 values: the simulation finds the design safe and the numbers below are what the C# tests must reproduce. Nothing here is a tuning change.")
    w("")
    w(table(["Parameter", "Value", "Check"],
            [["RopeLengthM / PivotHeightM", "14.0 / 17.0 (derived)", "hand always on the circle (<= 2e-6 m)"],
             ["SwingGravityMps2", "22.0", "-"],
             ["CatchMin / CatchMaxSpeedMps", "13.0 / 16.0", "peak 43.9..54.6 deg"],
             ["MaxSwingAngleDeg validator", "62; vCmax <= 17.8 (start-angle aware)", "-"],
             ["GrabMaxAngleDeg / start angle", "10 / asin(clamp((z - sP)/14, +-0.17365))", "theta0 = -5.12 deg at z - sP = -1.25"],
             ["Integrator", "symplectic Euler, omega first, 60 Hz, state in double", "drift <= 1.5 % (1.27 measured)"],
             ["DeterministicMath", "sin Taylor x^11, cos Taylor x^12 (Horner in x^2), asin series 4 terms", "5e-9 / 4e-10 / 4.5e-9"],
             ["GoodStartMs / PerfectStartMs / PerfectWidthMs", "450 / 700 / 183 -> ticks 27 / 42 / 53 (exclusive)", "-"],
             ["ReleaseBufferMs", "150 (9 ticks)", "swipe at tick 18..26 fires at 27; <= 17 expires"],
             ["Apex / ApexTicksEstimate", "85 for vC 13..16 (84..85 at normal grabs)", "SwingMaxMs 1600 = 96 ticks"],
             ["Impulses Perfect / Good / Poor", "3.0 / 1.5 / 0.0 at 30 deg", "-"],
             ["ReleaseMinForward / ReleaseMinUp", "6.0 / 2.0", "Poor release is exactly (6.0, 2.0)"],
             ["LaunchGravityMps2", "16", "-"],
             ["Chasm", "16 m, rim sP-4, far edge sP+12", "worst landing +2.86, best +13.6"],
             ["GrabEarlinessMs", "550", "windows 333 / 400 / 467 / 533 ms at 8 / 10 / 13 / 21"],
             ["Chain: spacing / T range / arrive feet", "18 / 0.85..1.5 / 1.5", "100 % catch"],
             ["Pad coin start V-05 / V-06", "82 / 100 (was 78 / 96)", "landing <= sP+25.6"]]))
    w("")
    w("## 12. Golden traces (schema v2)")
    w("")
    w("`python3 -I tools/sim/pendulum_golden.py` writes (and `--check` verifies) these under `tools/sim/golden/`. Schema v2 is additive over v1 (documented in `tools/sim/README.md`); v1 files 01..06 are untouched and byte-identical.")
    w("")
    w(table(["File", "Scenario", "Key expected values"],
            [["07_vine_perfect_release", "entry 15, Perfect swipe at tick 47", "vC 15, landing 10.82 m past the far edge"],
             ["08_vine_poor_auto_release", "entry 14, no swipe", "auto release tick 85, floors (6, 2), +4.19 m"],
             ["09a_vine_catch_clamp_entry_8", "entry 8 -> 13 m/s, Good at tick 27", "+2.91 m (the minimum case)"],
             ["09b_vine_catch_clamp_entry_21", "entry 21 -> 16 m/s, Good at tick 60", "+6.71 m"],
             ["10_vine_chain_two", "entry 21 -> Perfect at 47 -> guided to vine B 18 m on (T 0.85) -> Good at B tick 40", "B grabbed on tick 90 at vC 13, final landing z 135.15"],
             ["11_vine_missed_over_chasm", "spec 001 movement, jump 12 ticks after the window, no grab", "falls: Died Fell on tick 57 (vine layer relabels MissedVine)"],
             ["12_vine_boost_entry", "entry 26 (boost x1.6) -> 16 m/s, Perfect at tick 45", "identical swing to entry 21 (multiplier plays no role), +13.02 m"]]))
    w("")
    w("Per tick: `t`, `state` (Carried / Falling / Landed), `x`, `y` (feet), `z`, `hand_y`, `swing {theta, omega}` while carried, `launch {vx, vy, y0, vxRaw, vyRaw}` on the release tick, `events`. Tolerance in each file: x, y, z 1e-4, theta and omega 5e-6.")
    w("")
    w("## 13. Not verified / limits")
    w("")
    w("- This is the Python model; the C# runner has no swing yet. S-408 (full-run regression) and S-407's comparison with the old model need the C# code (T406).")
    w("- The hero's lateral blend (x) is not modelled (lane center only); the y blend is in the goldens.")
    w("- No retry after a missed grab or an expired early swipe is modelled (pessimistic Miss/Poor).")
    w("- The grab-height rule `top > 1.6 m` is always true (hero top = y + 1.8 > 1.6 for y > 0); the effective height rule is `y < 3.6`. It matters for Poor chain lobs (section 5).")
    w("- 'Missed vine share of deaths <= 15 %' needs the full run bots and the obstacle course (T406).")
    w("- Git: `pendulum_model.py`, `pendulum_study.py` and the CSV were already included in a WIP commit (d7e136d) made outside this task while it was running. This task made no commit; the remaining files are uncommitted.")
    w("")
    with open(REPORT, "w") as fh:
        fh.write("\n".join(L) + "\n")
    print("wrote", REPORT)


if __name__ == "__main__":
    main()
